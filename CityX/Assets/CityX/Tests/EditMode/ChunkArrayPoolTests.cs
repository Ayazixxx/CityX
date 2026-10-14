using System.Collections.Generic;
using System.Threading;
using CityX.View;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The arrays the streamer's chunk meshes are made in are used again instead of thrown away (ChunkArrayPool, 2026-10-14: streaming threw
    /// away ~60 MB a second flying, a garbage collection every tenth of a second, each stopping every thread for 2-7 ms). What the pool must
    /// do: hand out arrays at least as long as asked for and never much longer, give back the same arrays it was given, never hold more
    /// than its limit, and never hand one array to two threads at once.
    /// </summary>
    public sealed class ChunkArrayPoolTests
    {
        // Element types of the tests' own, so the pools these tests fill are not the ones the game uses.
        private struct Probe
        {
            public int Owner;
        }

        private struct Big
        {
            public long A, B, C, D;   // 32 bytes
        }

        private struct Held
        {
            public int Value;
        }

        [Test]
        public void ArraysAreAtLeastAsLongAsAskedAndAtMostAHalfOctaveLonger()
        {
            foreach (int asked in new[] { 1, 1000, 1024, 1025, 1448, 1449, 5000, 65000, 100000, 300000, 1000000 })
            {
                int[] array = ChunkArrayPool<int>.Rent(asked);
                Assert.GreaterOrEqual(array.Length, asked, $"asked for {asked}");
                Assert.LessOrEqual(array.Length, System.Math.Max(ChunkArrayPool.MinLength, (int)(asked * 1.42) + 1),
                    $"asked for {asked}, got {array.Length}: more than a half-octave over");
                ChunkArrayPool<int>.Return(array);
            }
        }

        [Test]
        public void AnArrayGivenBackIsTheOneHandedOutNextForTheSameStep()
        {
            Held[] first = ChunkArrayPool<Held>.Rent(5000);
            ChunkArrayPool<Held>.Return(first);
            Held[] again = ChunkArrayPool<Held>.Rent(4500);   // the same step (5792)
            Assert.AreSame(first, again, "a chunk of nearly the same size made a new array instead of using the one given back");

            Held[] other = ChunkArrayPool<Held>.Rent(4500);
            Assert.AreNotSame(again, other, "one array was handed out twice");
            ChunkArrayPool<Held>.Return(again);
            ChunkArrayPool<Held>.Return(other);
        }

        private struct Other
        {
            public long A, B, C, D;   // 32 bytes, like Big
        }

        private static List<T[]> RentMany<T>(int count) where T : struct
        {
            var rented = new List<T[]>();
            for (int i = 0; i < count; i++)
            {
                rented.Add(ChunkArrayPool<T>.Rent(32768));   // 1 MB each at 32 bytes an element
            }
            return rented;
        }

        private static int ComeBack<T>(List<T[]> given, int asked) where T : struct
        {
            int reused = 0;
            for (int i = 0; i < asked; i++)
            {
                if (given.Contains(ChunkArrayPool<T>.Rent(32768))) reused++;
            }
            return reused;
        }

        [Test]
        public void WhatIsHeldNeverPassesTheLimit()
        {
            long limitBefore = ChunkArrayPool.MaxPooledBytes;
            try
            {
                ChunkArrayPool.Clear();
                ChunkArrayPool.MaxPooledBytes = 4L * 1024 * 1024;
                List<Big[]> rented = RentMany<Big>(12);
                foreach (Big[] array in rented)
                {
                    ChunkArrayPool<Big>.Return(array);
                    Assert.LessOrEqual(ChunkArrayPool.PooledBytes, ChunkArrayPool.MaxPooledBytes, "the pool holds more than its limit");
                }

                // All of one size, as wanted as each other: the four that fitted are kept, the rest went to the collector.
                Assert.AreEqual(4, ComeBack(rented, 12), "the pool kept a different number of arrays than its limit has room for");
            }
            finally
            {
                ChunkArrayPool.MaxPooledBytes = limitBefore;
                ChunkArrayPool.Clear();
            }
        }

        [Test]
        public void RoomIsMadeByDroppingTheSizesWantedLeastLately()
        {
            long limitBefore = ChunkArrayPool.MaxPooledBytes;
            try
            {
                ChunkArrayPool.Clear();
                ChunkArrayPool.MaxPooledBytes = 4L * 1024 * 1024;

                // Two of one kind are used and given back; then three of another, wanted later, are given back into a pool with room for four.
                List<Other[]> early = RentMany<Other>(2);
                foreach (Other[] array in early) ChunkArrayPool<Other>.Return(array);
                List<Big[]> late = RentMany<Big>(3);
                foreach (Big[] array in late) ChunkArrayPool<Big>.Return(array);
                Assert.LessOrEqual(ChunkArrayPool.PooledBytes, ChunkArrayPool.MaxPooledBytes);

                // The third of the later kind made room by dropping one of the earlier kind: all three come back, and one of the two.
                Assert.AreEqual(3, ComeBack(late, 3), "an array wanted lately was dropped while one wanted earlier was kept");
                Assert.AreEqual(1, ComeBack(early, 2), "room was not made by dropping the kind wanted least lately");
            }
            finally
            {
                ChunkArrayPool.MaxPooledBytes = limitBefore;
                ChunkArrayPool.Clear();
            }
        }

        [Test]
        public void AnArrayIsNeverHandedToTwoThreadsAtOnce()
        {
            const int Threads = 8;
            const int Rounds = 400;
            var failures = new int[1];
            var threads = new Thread[Threads];
            for (int t = 0; t < Threads; t++)
            {
                int owner = t + 1;
                threads[t] = new Thread(() =>
                {
                    for (int round = 0; round < Rounds; round++)
                    {
                        Probe[] array = ChunkArrayPool<Probe>.Rent(2000 + (round % 3) * 700);
                        for (int i = 0; i < array.Length; i++) array[i].Owner = owner;
                        Thread.SpinWait(50);
                        for (int i = 0; i < array.Length; i++)
                        {
                            if (array[i].Owner != owner)
                            {
                                Interlocked.Increment(ref failures[0]);
                                break;
                            }
                        }
                        ChunkArrayPool<Probe>.Return(array);
                    }
                });
            }
            foreach (Thread thread in threads) thread.Start();
            foreach (Thread thread in threads) thread.Join();
            Assert.AreEqual(0, failures[0], "two threads wrote into the same array at once");
        }

        [Test]
        public void SomethingNotFromThePoolIsNotTakenIn()
        {
            long held = ChunkArrayPool.PooledBytes;
            ChunkArrayPool<Held>.Return(new Held[1000]);   // not a step's length
            ChunkArrayPool<Held>.Return(null);
            Assert.AreEqual(held, ChunkArrayPool.PooledBytes);
        }
    }
}
