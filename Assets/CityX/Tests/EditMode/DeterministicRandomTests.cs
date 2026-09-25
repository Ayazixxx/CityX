using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The generator underneath the whole "world is a seed, not a level"
    /// guarantee. If these fail, a Linux shard and a Windows client build
    /// different cities and every system that trusts world geometry is
    /// quietly wrong.
    ///
    /// Note what is NOT asserted here: specific magic numbers copied from a
    /// run of this same code. That would only prove the code still does what
    /// it did, which is circular. What is asserted are the PROPERTIES the
    /// guarantee actually needs -- reproducibility, independence, range, and
    /// absence of bias.
    /// </summary>
    public class DeterministicRandomTests
    {
        [Test]
        public void SameSeedProducesTheSameSequence()
        {
            DeterministicRandom a = new DeterministicRandom(12345);
            DeterministicRandom b = new DeterministicRandom(12345);

            for (int i = 0; i < 500; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(),
                    $"two generators seeded identically diverged at draw {i} -- server and client " +
                    "would build different cities");
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentSequences()
        {
            DeterministicRandom a = new DeterministicRandom(1);
            DeterministicRandom b = new DeterministicRandom(2);

            int identical = 0;
            for (int i = 0; i < 200; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    identical++;
                }
            }
            Assert.Less(identical, 3,
                $"{identical}/200 draws matched between seeds 1 and 2 -- adjacent seeds are not " +
                "being decorrelated, so neighbouring chunks would look alike");
        }

        /// <summary>
        /// Chunk seeds are derived by hashing coordinates, so nearby chunks
        /// get numerically close seeds. A generator that does not scramble
        /// its seed properly would give them near-identical first draws, and
        /// the city would come out visibly striped.
        /// </summary>
        [Test]
        public void AdjacentSeedsDoNotProduceCorrelatedFirstDraws()
        {
            List<double> firstDraws = new List<double>();
            for (int seed = 1000; seed < 1064; seed++)
            {
                firstDraws.Add(new DeterministicRandom(seed).NextDouble());
            }

            // With 64 well-spread samples in [0,1), every quarter should get
            // several. A striped generator piles them into one or two.
            int[] buckets = new int[4];
            foreach (double value in firstDraws)
            {
                buckets[(int)(value * 4.0)]++;
            }
            foreach (int count in buckets)
            {
                Assert.Greater(count, 3,
                    $"first draws from 64 consecutive seeds landed in buckets [{string.Join(",", buckets)}] " +
                    "-- consecutive seeds are correlated and the city would come out striped");
            }
        }

        [Test]
        public void NextDoubleStaysInRangeAndNeverReachesOne()
        {
            DeterministicRandom rng = new DeterministicRandom(7);
            for (int i = 0; i < 20000; i++)
            {
                double value = rng.NextDouble();
                Assert.GreaterOrEqual(value, 0.0);
                // Strictly less than 1: a value of exactly 1.0 would push
                // every `(int)(r * count)` index one past the end of its array.
                Assert.Less(value, 1.0);
            }
        }

        [Test]
        public void RangeRespectsItsBounds()
        {
            DeterministicRandom rng = new DeterministicRandom(99);
            for (int i = 0; i < 5000; i++)
            {
                double value = rng.Range(9.0, 18.0);
                Assert.GreaterOrEqual(value, 9.0);
                Assert.Less(value, 18.0);
            }
        }

        /// <summary>
        /// Below() uses rejection sampling specifically to avoid modulo bias.
        /// The roof-type list has 8 entries; a biased picker would make some
        /// roofs visibly more common than intended.
        /// </summary>
        [Test]
        public void BelowIsUnbiasedAcrossBuckets()
        {
            const int Buckets = 8;
            const int Draws = 80000;
            int[] counts = new int[Buckets];

            DeterministicRandom rng = new DeterministicRandom(4242);
            for (int i = 0; i < Draws; i++)
            {
                int index = rng.Below(Buckets);
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, Buckets);
                counts[index]++;
            }

            int expected = Draws / Buckets;
            foreach (int count in counts)
            {
                // Generous band: this is checking for real bias, not for a
                // suspiciously perfect distribution.
                Assert.Greater(count, expected * 0.9,
                    $"bucket distribution {string.Join(",", counts)} is skewed low");
                Assert.Less(count, expected * 1.1,
                    $"bucket distribution {string.Join(",", counts)} is skewed high");
            }
        }

        [Test]
        public void BelowHandlesDegenerateCounts()
        {
            DeterministicRandom rng = new DeterministicRandom(1);
            Assert.AreEqual(0, rng.Below(1));
            Assert.AreEqual(0, rng.Below(0));
            Assert.AreEqual(0, rng.Below(-5));
        }

        /// <summary>
        /// Independent streams from the same seed must not march in lockstep.
        /// The original leans on this: special-building type and basement
        /// presence each get their own salted generator specifically so that
        /// adding an optional feature does not reshuffle every building in
        /// the city.
        /// </summary>
        [Test]
        public void SeparateStreamsFromOneSeedAreIndependent()
        {
            DeterministicRandom main = new DeterministicRandom(555UL);
            DeterministicRandom salted = new DeterministicRandom(555UL, 0x53504543UL);

            int identical = 0;
            for (int i = 0; i < 200; i++)
            {
                if (main.NextUInt() == salted.NextUInt())
                {
                    identical++;
                }
            }
            Assert.Less(identical, 3,
                "a salted stream tracked the main stream -- the trick the original uses to add " +
                "optional features without reshuffling the whole city would not work");
        }
    }
}
