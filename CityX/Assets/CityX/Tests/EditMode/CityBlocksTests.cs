using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Block massing is the first thing in this codebase where one chunk's
    /// answer has to agree with another chunk's answer without the two ever
    /// meeting. That agreement is what these tests are really about.
    ///
    /// <see cref="EveryConsumedChunkIsClaimedByTheAnchorItNames"/> and
    /// <see cref="NoTwoBuildingsEverOverlap"/> are the load-bearing pair. If
    /// either fails, the world has buildings growing through each other or
    /// holes where a chunk decided it was covered by something that never got
    /// built -- and both would only show up on some seeds, in some places,
    /// which is exactly the failure a purity property is supposed to make
    /// impossible.
    /// </summary>
    public class CityBlocksTests
    {
        private const int Seed = 20260810;
        private const int Sweep = 96;   // chunks each way: 24 blocks square

        private static CityBlocks Blocks()
        {
            return new CityBlocks(Seed);
        }

        [Test]
        public void StreetChunksAreNeverBuiltOn()
        {
            CityBlocks blocks = Blocks();
            for (int x = -Sweep; x < Sweep; x++)
            {
                for (int y = -Sweep; y < Sweep; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    Assert.AreEqual(LotKind.Street, blocks.RoleFor(x, y).Kind,
                        $"({x},{y}) is a street but was assigned a lot role");
                }
            }
        }

        [Test]
        public void EveryBuildableChunkGetsExactlyOneRole()
        {
            CityBlocks blocks = Blocks();
            for (int x = -Sweep; x < Sweep; x++)
            {
                for (int y = -Sweep; y < Sweep; y++)
                {
                    if (CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    LotKind kind = blocks.RoleFor(x, y).Kind;
                    Assert.AreNotEqual(LotKind.Street, kind,
                        $"({x},{y}) is buildable but came back as a street");
                }
            }
        }

        /// <summary>
        /// The cross-chunk agreement test.
        ///
        /// A covered chunk names the anchor it believes owns it. That anchor is
        /// then asked, independently, what it thinks it is building -- and its
        /// footprint has to actually reach the chunk that named it. Nothing is
        /// passed between the two calls; both derive it from the block seed.
        /// </summary>
        [Test]
        public void EveryConsumedChunkIsClaimedByTheAnchorItNames()
        {
            CityBlocks blocks = Blocks();
            int consumed = 0;

            for (int x = -Sweep; x < Sweep; x++)
            {
                for (int y = -Sweep; y < Sweep; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    if (role.Kind != LotKind.Consumed)
                    {
                        continue;
                    }
                    consumed++;

                    LotRole anchor = blocks.RoleFor(role.AnchorX, role.AnchorY);
                    Assert.AreEqual(LotKind.Building, anchor.Kind,
                        $"({x},{y}) says ({role.AnchorX},{role.AnchorY}) covers it, but that chunk " +
                        $"is {anchor.Kind} -- the two disagree from their own sides");

                    Assert.IsTrue(
                        x >= anchor.AnchorX && x < anchor.AnchorX + anchor.SpanX &&
                        y >= anchor.AnchorY && y < anchor.AnchorY + anchor.SpanY,
                        $"({x},{y}) claims to be covered by ({role.AnchorX},{role.AnchorY}), whose " +
                        $"{anchor.SpanX}x{anchor.SpanY} footprint does not reach it");
                }
            }

            Assert.Greater(consumed, 0, "no multi-chunk buildings at all -- superlots are not happening");
        }

        /// <summary>
        /// Paints every building's full footprint onto a grid and fails if any
        /// cell is claimed twice, or if a buildable cell is claimed by nobody.
        /// </summary>
        [Test]
        public void NoTwoBuildingsEverOverlap()
        {
            CityBlocks blocks = Blocks();
            var owner = new Dictionary<(int, int), (int, int)>();

            for (int x = -Sweep; x < Sweep; x++)
            {
                for (int y = -Sweep; y < Sweep; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    if (!role.Builds)
                    {
                        continue;
                    }

                    for (int dx = 0; dx < role.SpanX; dx++)
                    {
                        for (int dy = 0; dy < role.SpanY; dy++)
                        {
                            var cell = (x + dx, y + dy);
                            Assert.IsFalse(owner.ContainsKey(cell),
                                $"chunk {cell} is covered by both {owner.GetValueOrDefault(cell)} and ({x},{y})");
                            owner[cell] = (x, y);
                        }
                    }
                }
            }

            // And nothing buildable was left out. Inset by one block so the
            // sweep edge does not count anchors whose footprint runs off it.
            for (int x = -Sweep + CityLayout.StreetPeriod; x < Sweep - CityLayout.StreetPeriod; x++)
            {
                for (int y = -Sweep + CityLayout.StreetPeriod; y < Sweep - CityLayout.StreetPeriod; y++)
                {
                    if (CityLayout.IsStreetChunk(x, y))
                    {
                        Assert.IsFalse(owner.ContainsKey((x, y)),
                            $"a building was laid over street chunk ({x},{y})");
                        continue;
                    }
                    Assert.IsTrue(owner.ContainsKey((x, y)),
                        $"buildable chunk ({x},{y}) is claimed by nobody -- a hole in the city");
                }
            }
        }

        /// <summary>
        /// A footprint that ran past the block interior would put a tower in the
        /// road. It would also be seed-dependent, so it would ship.
        /// </summary>
        [Test]
        public void NoFootprintEverReachesIntoAStreet()
        {
            CityBlocks blocks = Blocks();
            for (int x = -Sweep; x < Sweep; x++)
            {
                for (int y = -Sweep; y < Sweep; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    if (!role.Builds)
                    {
                        continue;
                    }

                    for (int dx = 0; dx < role.SpanX; dx++)
                    {
                        for (int dy = 0; dy < role.SpanY; dy++)
                        {
                            Assert.IsFalse(CityLayout.IsStreetChunk(x + dx, y + dy),
                                $"the {role.SpanX}x{role.SpanY} at ({x},{y}) covers street chunk " +
                                $"({x + dx},{y + dy})");
                        }
                    }
                }
            }
        }

        [Test]
        public void BlockShapesFollowTheirWeights()
        {
            CityBlocks blocks = Blocks();
            var shapes = new Dictionary<string, int>();
            int blockCount = 0;

            // One probe per block, at its own interior origin, so each block is
            // counted once regardless of what it turned out to be.
            for (int bx = -60; bx < 60; bx++)
            {
                for (int by = -60; by < 60; by++)
                {
                    blockCount++;
                    string shape = "1x1";
                    for (int ix = 0; ix < CityLayout.BuildableSpan; ix++)
                    {
                        for (int iy = 0; iy < CityLayout.BuildableSpan; iy++)
                        {
                            LotRole role = blocks.RoleFor(
                                bx * CityLayout.StreetPeriod + 1 + ix,
                                by * CityLayout.StreetPeriod + 1 + iy);
                            if (role.IsMultiChunk)
                            {
                                shape = $"{role.SpanX}x{role.SpanY}";
                            }
                        }
                    }
                    shapes.TryGetValue(shape, out int count);
                    shapes[shape] = count + 1;
                }
            }

            foreach (var pair in shapes)
            {
                TestContext.WriteLine($"{pair.Key}: {pair.Value} blocks ({100.0 * pair.Value / blockCount:F1}%)");
            }

            AssertShare(shapes, blockCount, "2x2", CityBlocks.TowerChance);
            AssertShare(shapes, blockCount, "2x1", CityBlocks.SlabWideChance);
            AssertShare(shapes, blockCount, "1x2", CityBlocks.SlabDeepChance);
            AssertShare(shapes, blockCount, "1x1", CityBlocks.NoSuperlotChance);
        }

        /// <summary>
        /// Guards a specific and very plausible bug: sharing one salt between
        /// the shape roll and the two anchor rolls. Everything else would still
        /// pass, and every tower in the world would sit in the same corner of
        /// its block.
        /// </summary>
        [Test]
        public void BigBuildingsAreNotAlwaysInTheSameCornerOfTheirBlock()
        {
            CityBlocks blocks = Blocks();
            var corners = new Dictionary<(int, int), int>();

            for (int bx = -60; bx < 60; bx++)
            {
                for (int by = -60; by < 60; by++)
                {
                    for (int ix = 0; ix < CityLayout.BuildableSpan; ix++)
                    {
                        for (int iy = 0; iy < CityLayout.BuildableSpan; iy++)
                        {
                            LotRole role = blocks.RoleFor(
                                bx * CityLayout.StreetPeriod + 1 + ix,
                                by * CityLayout.StreetPeriod + 1 + iy);
                            if (!role.Builds || !role.IsMultiChunk)
                            {
                                continue;
                            }
                            var corner = (ix, iy);
                            corners.TryGetValue(corner, out int count);
                            corners[corner] = count + 1;
                        }
                    }
                }
            }

            var summary = new List<string>();
            foreach (var pair in corners)
            {
                summary.Add($"({pair.Key.Item1},{pair.Key.Item2})={pair.Value}");
            }
            summary.Sort();
            TestContext.WriteLine("anchor corners used: " + string.Join(", ", summary));

            Assert.GreaterOrEqual(corners.Count, 4,
                "big buildings only ever anchor at a couple of positions -- the anchor roll is " +
                "correlated with the shape roll, most likely a shared salt");
        }

        [Test]
        public void TheSameChunkAlwaysGetsTheSameRole()
        {
            CityBlocks a = Blocks();
            CityBlocks b = Blocks();
            for (int x = -40; x < 40; x++)
            {
                for (int y = -40; y < 40; y++)
                {
                    LotRole first = a.RoleFor(x, y);
                    LotRole second = b.RoleFor(x, y);
                    Assert.AreEqual(first.Kind, second.Kind, $"({x},{y}) kind is not deterministic");
                    Assert.AreEqual(first.SpanX, second.SpanX, $"({x},{y}) spanX is not deterministic");
                    Assert.AreEqual(first.SpanY, second.SpanY, $"({x},{y}) spanY is not deterministic");
                }
            }
        }

        [Test]
        public void DifferentSeedsMassBlocksDifferently()
        {
            CityBlocks a = new CityBlocks(1);
            CityBlocks b = new CityBlocks(2);

            int differing = 0;
            int total = 0;
            for (int x = -40; x < 40; x++)
            {
                for (int y = -40; y < 40; y++)
                {
                    if (CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    total++;
                    LotRole ra = a.RoleFor(x, y);
                    LotRole rb = b.RoleFor(x, y);
                    if (ra.Kind != rb.Kind || ra.SpanX != rb.SpanX || ra.SpanY != rb.SpanY)
                    {
                        differing++;
                    }
                }
            }

            Assert.Greater(differing, total * 0.2,
                $"only {differing}/{total} lots differ between two seeds -- the world seed is barely reaching massing");
        }

        /// <summary>
        /// Blocks have to tile into negative space. BlockIndex truncating
        /// instead of flooring would make block -1 four chunks wide on one side
        /// of the origin and misalign every block west or south of it.
        /// </summary>
        [Test]
        public void BlocksTileCorrectlyThroughTheOrigin()
        {
            Assert.AreEqual(0, CityLayout.BlockIndex(0));
            Assert.AreEqual(0, CityLayout.BlockIndex(3));
            Assert.AreEqual(1, CityLayout.BlockIndex(4));
            Assert.AreEqual(-1, CityLayout.BlockIndex(-1));
            Assert.AreEqual(-1, CityLayout.BlockIndex(-4));
            Assert.AreEqual(-2, CityLayout.BlockIndex(-5));

            // Every block must own exactly StreetPeriod consecutive chunks.
            var widths = new Dictionary<int, int>();
            for (int c = -40; c < 40; c++)
            {
                int block = CityLayout.BlockIndex(c);
                widths.TryGetValue(block, out int count);
                widths[block] = count + 1;
            }
            foreach (var pair in widths)
            {
                if (pair.Key <= -10 || pair.Key >= 9)
                {
                    continue;   // partial blocks at the ends of the sweep
                }
                Assert.AreEqual(CityLayout.StreetPeriod, pair.Value,
                    $"block {pair.Key} owns {pair.Value} chunks instead of {CityLayout.StreetPeriod}");
            }
        }

        private static void AssertShare(Dictionary<string, int> shapes, int total, string key, double expected)
        {
            shapes.TryGetValue(key, out int count);
            double share = (double)count / total;
            Assert.AreEqual(expected, share, 0.03,
                $"{key} blocks are {share:P1} of the city, expected about {expected:P1}");
        }
    }
}
