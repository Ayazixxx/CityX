using System.Collections.Generic;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;

namespace CityX.View
{
    /// <summary>
    /// Arrays for the city chunks' meshes, used again instead of left to the garbage collector (2026-10-14).
    ///
    /// Every chunk that streamed in had its mesh made in fresh arrays on a worker thread -- a detailed block of towers is 250-380k vertices,
    /// tens of MB once its lists had grown and been copied -- and a garbage collection stops EVERY thread while it runs, the main one
    /// included. So the streamer rents the arrays it hands the main thread here and gives them back once the mesh is built.
    ///
    /// An array is at least as long as asked for, not exactly: lengths come in half-octave steps (1024, 1448, 2048, 2896, ...), so one is
    /// at most 41% longer than needed, and the caller says how much of it is in use. What is held for reuse never passes
    /// <see cref="MaxPooledBytes"/>: when an array given back would pass it, room is made by dropping arrays of the steps rented least
    /// lately, of any type -- sizes the chunks streaming now do not need -- and if nothing held was wanted less lately than the array given
    /// back, that one goes to the collector instead. Thread-safe: rented on the workers, given back on the workers and the main thread.
    /// </summary>
    public static class ChunkArrayPool
    {
        /// <summary>
        /// The most bytes of arrays held for reuse, all element types together (a test lowers it). Without a tight limit the pool grew to
        /// hold every size of chunk at once (350 MB after a 30 s flight at jetpack speed); with this one it keeps the sizes in use -- the
        /// same flight made only 12 MB of new arrays -- and lets the rest go.
        /// </summary>
        public static long MaxPooledBytes { get; set; } = 192L * 1024 * 1024;

        /// <summary>The shortest array handed out.</summary>
        public const int MinLength = 1024;

        /// <summary>Half-octave steps from MinLength: the longest is MinLength * 2^((Steps - 1) / 2), over 50 million.</summary>
        public const int Steps = 32;

        private static long _pooledBytes;
        private static long _madeBytes;
        private static long _tick;

        /// <summary>Bytes held for reuse right now.</summary>
        public static long PooledBytes => Interlocked.Read(ref _pooledBytes);

        /// <summary>Bytes of new arrays made since the game started (a rent nothing pooled could serve). For tests and the benchmark.</summary>
        public static long MadeBytes => Interlocked.Read(ref _madeBytes);

        /// <summary>The length of the arrays of one step.</summary>
        public static int LengthOf(int step)
        {
            int octave = step >> 1;
            int baseLength = (step & 1) == 0 ? MinLength : 1448;   // 1024 * sqrt(2), to a multiple of 8
            return baseLength << octave;
        }

        /// <summary>The step whose arrays are the shortest at least <paramref name="length"/> long (-1: longer than any step).</summary>
        public static int StepFor(int length)
        {
            for (int step = 0; step < Steps; step++)
            {
                if (LengthOf(step) >= length)
                {
                    return step;
                }
            }
            return -1;
        }

        /// <summary>The arrays of one element type, as the pool sees them, so it can make room across types.</summary>
        internal abstract class Shelf
        {
            public abstract string Name { get; }

            /// <summary>The step holding something whose last rent is the longest ago, and that rent's tick (long.MaxValue: holds nothing).</summary>
            public abstract long Coldest(out int step);

            /// <summary>Drops one array held at a step (to the collector).</summary>
            public abstract void DropOne(int step);

            public abstract long HeldBytes();

            /// <summary>Drops everything held.</summary>
            public abstract void DropAll();
        }

        private static readonly List<Shelf> Shelves = new List<Shelf>();

        internal static void Register(Shelf shelf)
        {
            lock (Shelves)
            {
                Shelves.Add(shelf);
            }
        }

        internal static long NextTick() => Interlocked.Increment(ref _tick);

        internal static void Release(long bytes) => Interlocked.Add(ref _pooledBytes, -bytes);

        internal static void Made(long bytes) => Interlocked.Add(ref _madeBytes, bytes);

        /// <summary>
        /// Holds <paramref name="bytes"/> more for an array being given back, making room if it must by dropping arrays of the steps rented
        /// least lately -- only ones wanted less lately than <paramref name="lastRent"/>, the given-back array's own step's. False: no room.
        /// </summary>
        internal static bool Hold(long bytes, long lastRent)
        {
            while (true)
            {
                long held = Interlocked.Read(ref _pooledBytes);
                if (held + bytes <= MaxPooledBytes)
                {
                    if (Interlocked.CompareExchange(ref _pooledBytes, held + bytes, held) == held)
                    {
                        return true;
                    }
                    continue;
                }
                lock (Shelves)
                {
                    Shelf coldest = null;
                    int coldestStep = -1;
                    long coldestTick = lastRent;
                    foreach (Shelf shelf in Shelves)
                    {
                        long tick = shelf.Coldest(out int step);
                        if (tick < coldestTick)
                        {
                            coldest = shelf;
                            coldestStep = step;
                            coldestTick = tick;
                        }
                    }
                    if (coldest == null)
                    {
                        return false;   // everything held was wanted more lately: this one is the one to let go
                    }
                    coldest.DropOne(coldestStep);
                }
            }
        }

        /// <summary>Drops everything held (to the collector), whatever its type: for tests, or to give memory back.</summary>
        public static void Clear()
        {
            lock (Shelves)
            {
                foreach (Shelf shelf in Shelves)
                {
                    shelf.DropAll();
                }
            }
        }

        /// <summary>What is held, element type by element type (for the benchmark): "Vector3 120 MB, ...".</summary>
        public static string Held()
        {
            var text = new System.Text.StringBuilder();
            lock (Shelves)
            {
                foreach (Shelf shelf in Shelves)
                {
                    long bytes = shelf.HeldBytes();
                    if (bytes > 0)
                    {
                        text.Append(text.Length > 0 ? ", " : "").Append($"{shelf.Name} {bytes / (1024.0 * 1024.0):0} MB");
                    }
                }
            }
            return text.ToString();
        }
    }

    /// <summary>The arrays of one element type (see <see cref="ChunkArrayPool"/>).</summary>
    public static class ChunkArrayPool<T> where T : struct
    {
        private static readonly Stack<T[]>[] Held = MakeSteps();

        /// <summary>When each step was last rented from (ChunkArrayPool's tick): what is wanted least lately goes first when room is made.</summary>
        private static readonly long[] LastRent = new long[ChunkArrayPool.Steps];

        private static readonly int ElementBytes = UnsafeUtility.SizeOf<T>();

        static ChunkArrayPool()
        {
            ChunkArrayPool.Register(new TypeShelf());
        }

        private static Stack<T[]>[] MakeSteps()
        {
            var steps = new Stack<T[]>[ChunkArrayPool.Steps];
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = new Stack<T[]>();
            }
            return steps;
        }

        /// <summary>An array at least <paramref name="length"/> long. Its contents are whatever was left in it: write before you read.</summary>
        public static T[] Rent(int length)
        {
            int step = ChunkArrayPool.StepFor(length);
            if (step < 0)
            {
                ChunkArrayPool.Made((long)length * ElementBytes);
                return new T[length];   // past every step: never held
            }
            Volatile.Write(ref LastRent[step], ChunkArrayPool.NextTick());
            Stack<T[]> held = Held[step];
            lock (held)
            {
                if (held.Count > 0)
                {
                    T[] array = held.Pop();
                    ChunkArrayPool.Release((long)array.Length * ElementBytes);
                    return array;
                }
            }
            int made = ChunkArrayPool.LengthOf(step);
            ChunkArrayPool.Made((long)made * ElementBytes);
            return new T[made];
        }

        /// <summary>Gives an array from <see cref="Rent"/> back. Null, or one whose length is no step, is ignored.</summary>
        public static void Return(T[] array)
        {
            if (array == null)
            {
                return;
            }
            int step = ChunkArrayPool.StepFor(array.Length);
            if (step < 0 || ChunkArrayPool.LengthOf(step) != array.Length)
            {
                return;
            }
            if (!ChunkArrayPool.Hold((long)array.Length * ElementBytes, Volatile.Read(ref LastRent[step])))
            {
                return;   // the pool is full of sizes wanted more lately: this one goes to the collector
            }
            Stack<T[]> held = Held[step];
            lock (held)
            {
                held.Push(array);
            }
        }

        private sealed class TypeShelf : ChunkArrayPool.Shelf
        {
            public override string Name => typeof(T).Name;

            public override long Coldest(out int step)
            {
                step = -1;
                long coldest = long.MaxValue;
                for (int s = 0; s < Held.Length; s++)
                {
                    long tick = Volatile.Read(ref LastRent[s]);
                    if (tick >= coldest)
                    {
                        continue;
                    }
                    lock (Held[s])
                    {
                        if (Held[s].Count > 0)
                        {
                            coldest = tick;
                            step = s;
                        }
                    }
                }
                return coldest;
            }

            public override void DropOne(int step)
            {
                lock (Held[step])
                {
                    if (Held[step].Count > 0)
                    {
                        ChunkArrayPool.Release((long)Held[step].Pop().Length * ElementBytes);
                    }
                }
            }

            public override long HeldBytes()
            {
                long bytes = 0;
                foreach (Stack<T[]> held in Held)
                {
                    lock (held)
                    {
                        foreach (T[] array in held)
                        {
                            bytes += (long)array.Length * ElementBytes;
                        }
                    }
                }
                return bytes;
            }

            public override void DropAll()
            {
                foreach (Stack<T[]> held in Held)
                {
                    lock (held)
                    {
                        while (held.Count > 0)
                        {
                            ChunkArrayPool.Release((long)held.Pop().Length * ElementBytes);
                        }
                    }
                }
            }
        }
    }
}
