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
    ///
    /// SAMPLING. Zones are regional -- a core is a kilometre across -- so a
    /// single window near the origin sees one or two of them and says nothing
    /// about the city. Statistical tests here sweep <see cref="Windows"/>: a
    /// spread of windows across a wide area, so a share means the city's share.
    /// </summary>
    public class CityBlocksTests
    {
        private const int Seed = 20260810;

        /// <summary>Half-width, in chunks, of one sample window.</summary>
        private const int Half = 40;

        private static CityBlocks Blocks()
        {
            return new CityBlocks(Seed);
        }

        /// <summary>Window centres spread over ~10km, including the origin's downtown.</summary>
        private static IEnumerable<(int x, int y)> Windows()
        {
            foreach (int gx in new[] { -260, -130, 0, 130, 260 })
            {
                foreach (int gy in new[] { -260, -130, 0, 130, 260 })
                {
                    yield return (gx, gy);
                }
            }
        }

        private static IEnumerable<(int x, int y)> SampleChunks()
        {
            foreach (var (wx, wy) in Windows())
            {
                for (int x = wx - Half; x < wx + Half; x++)
                {
                    for (int y = wy - Half; y < wy + Half; y++)
                    {
                        yield return (x, y);
                    }
                }
            }
        }

        [Test]
        public void StreetChunksAreNeverBuiltOn()
        {
            CityBlocks blocks = Blocks();
            foreach (var (x, y) in SampleChunks())
            {
                if (!CityLayout.IsStreetChunk(x, y))
                {
                    continue;
                }
                Assert.AreEqual(LotKind.Street, blocks.RoleFor(x, y).Kind,
                    $"({x},{y}) is a street but was assigned a lot role");
            }
        }

        [Test]
        public void EveryBuildableChunkGetsExactlyOneRole()
        {
            CityBlocks blocks = Blocks();
            foreach (var (x, y) in SampleChunks())
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

            foreach (var (x, y) in SampleChunks())
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
                Assert.AreEqual(anchor.SpanX, role.SpanX, $"({x},{y}) disagrees with its anchor about the span");
                Assert.AreEqual(anchor.SpanY, role.SpanY, $"({x},{y}) disagrees with its anchor about the span");
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
            foreach (var (wx, wy) in Windows())
            {
                var owner = new Dictionary<(int, int), (int, int)>();
                for (int x = wx - Half; x < wx + Half; x++)
                {
                    for (int y = wy - Half; y < wy + Half; y++)
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

                // And nothing buildable was left out. Inset by the widest a
                // block can be, so a building anchored just outside the window
                // (and so not painted) cannot make an edge chunk look orphaned.
                int inset = CityLayout.MaxStreetGap;
                for (int x = wx - Half + inset; x < wx + Half - inset; x++)
                {
                    for (int y = wy - Half + inset; y < wy + Half - inset; y++)
                    {
                        LotKind kind = blocks.RoleFor(x, y).Kind;
                        if (kind == LotKind.Street || kind == LotKind.Open)
                        {
                            Assert.IsFalse(owner.ContainsKey((x, y)),
                                $"a building was laid over {kind} chunk ({x},{y})");
                            continue;
                        }
                        Assert.IsTrue(owner.ContainsKey((x, y)),
                            $"buildable chunk ({x},{y}) is claimed by nobody -- a hole in the city");
                    }
                }
            }
        }

        /// <summary>
        /// Open blocks have to be WHOLE blocks. A courtyard that is open on
        /// seven of its nine lots and has a tower on the other two is not a
        /// courtyard, it is a mistake -- and it would look like one.
        /// </summary>
        [Test]
        public void OpenBlocksAreOpenAllTheWayAcross()
        {
            CityBlocks blocks = Blocks();
            var seenBlocks = new HashSet<(int, int)>();
            int openBlocks = 0;
            int totalBlocks = 0;
            double expectedOpen = 0.0;

            foreach (var (x, y) in SampleChunks())
            {
                if (!CityLayout.TryGetBlockRect(x, y, out int minX, out int minY, out int width, out int height))
                {
                    continue;
                }
                if (!seenBlocks.Add((minX, minY)))
                {
                    continue;   // already accounted for via an earlier chunk in the same block
                }
                totalBlocks++;
                expectedOpen += CityBlocks.OpenBlockChanceFor(CityBlocks.ZoneOfBlock(minX, minY, width, height));
                if (!blocks.IsOpenBlock(minX, minY))
                {
                    continue;
                }
                openBlocks++;

                for (int ix = 0; ix < width; ix++)
                {
                    for (int iy = 0; iy < height; iy++)
                    {
                        Assert.AreEqual(LotKind.Open, blocks.RoleFor(minX + ix, minY + iy).Kind,
                            $"block ({minX},{minY}) is open but chunk ({minX + ix},{minY + iy}) inside it is not");
                    }
                }
            }

            double share = openBlocks / (double)totalBlocks;
            double expected = expectedOpen / totalBlocks;
            TestContext.WriteLine($"{share:P1} of blocks are open ({openBlocks}/{totalBlocks}); zones predict {expected:P1}");
            Assert.AreEqual(expected, share, 0.03, "open blocks are not appearing at their stated rate");
            Assert.Greater(share, 0.05, "the city has no parks or plazas to speak of");
            Assert.Less(share, 0.20, "the city is mostly empty ground");
        }

        /// <summary>
        /// Green belts are allowed to be big, but a strip of open blocks that
        /// runs on and on reads as the city failing to generate.
        /// </summary>
        [Test]
        public void OpenBlocksDoNotClumpIntoDeadDistricts()
        {
            CityBlocks blocks = Blocks();
            int worstRun = 0;
            int worstRunMetres = 0;

            foreach (var (wx, wy) in Windows())
            {
                for (int y = wy - Half; y < wy + Half; y += 7)
                {
                    int run = 0;
                    int runMetres = 0;
                    (int, int)? lastBlock = null;

                    for (int x = wx - Half; x < wx + Half; x++)
                    {
                        if (!CityLayout.TryGetBlockRect(x, y, out int minX, out int minY, out int width, out _))
                        {
                            continue;
                        }
                        if (lastBlock == (minX, minY))
                        {
                            continue;   // still the same block as the last chunk
                        }
                        lastBlock = (minX, minY);

                        if (blocks.IsOpenBlock(minX, minY))
                        {
                            run++;
                            runMetres += (width + 1) * (int)CityLayout.ChunkSize;
                        }
                        else
                        {
                            run = 0;
                            runMetres = 0;
                        }

                        if (run > worstRun)
                        {
                            worstRun = run;
                            worstRunMetres = runMetres;
                        }
                    }
                }
            }

            TestContext.WriteLine($"longest unbroken row of open blocks: {worstRun} ({worstRunMetres}m)");
            Assert.LessOrEqual(worstRun, 6,
                $"{worstRun} open blocks in a row -- that is about {worstRunMetres}m of nothing, " +
                "which reads as the city failing to generate rather than as a park");
        }

        /// <summary>
        /// A footprint that ran past the block interior would put a tower in the
        /// road. It would also be seed-dependent, so it would ship.
        /// </summary>
        [Test]
        public void NoFootprintEverReachesIntoAStreet()
        {
            CityBlocks blocks = Blocks();
            foreach (var (x, y) in SampleChunks())
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

        /// <summary>
        /// A lot never spills out of its own block: every chunk it covers must
        /// belong to the same block as its anchor.
        /// </summary>
        [Test]
        public void EveryLotStaysInsideItsOwnBlock()
        {
            CityBlocks blocks = Blocks();
            foreach (var (x, y) in SampleChunks())
            {
                LotRole role = blocks.RoleFor(x, y);
                if (!role.Builds || !role.IsMultiChunk)
                {
                    continue;
                }
                Assert.IsTrue(CityLayout.TryGetBlockRect(x, y, out int minX, out int minY, out int width, out int height));
                Assert.GreaterOrEqual(x, minX);
                Assert.GreaterOrEqual(y, minY);
                Assert.LessOrEqual(x + role.SpanX, minX + width, $"the lot at ({x},{y}) runs out of its block on X");
                Assert.LessOrEqual(y + role.SpanY, minY + height, $"the lot at ({x},{y}) runs out of its block on Y");
            }
        }

        /// <summary>
        /// The zones are what shape the massing: a downtown block is full of
        /// tower lots, an old town has none, and the outskirts keep them rare.
        /// Measured over the whole sample, per zone.
        /// </summary>
        [Test]
        public void TowerLotsClusterInTheCoreAndVanishFromTheOldTown()
        {
            CityBlocks blocks = Blocks();
            var lots = new Dictionary<ZoneKind, int>();
            var towers = new Dictionary<ZoneKind, int>();

            foreach (var (x, y) in SampleChunks())
            {
                LotRole role = blocks.RoleFor(x, y);
                if (!role.Builds)
                {
                    continue;
                }
                ZoneKind zone = CityChunkGenerator.ZoneOfRole(role);
                lots.TryGetValue(zone, out int l);
                lots[zone] = l + 1;
                if (role.SpanX == 2 && role.SpanY == 2)
                {
                    towers.TryGetValue(zone, out int t);
                    towers[zone] = t + 1;
                }
            }

            double Share(ZoneKind z)
            {
                lots.TryGetValue(z, out int l);
                towers.TryGetValue(z, out int t);
                TestContext.WriteLine($"{z}: {t}/{l} lots are 2x2 towers");
                return l == 0 ? 0.0 : t / (double)l;
            }

            Assert.Greater(lots.GetValueOrDefault(ZoneKind.Core), 200, "the sample never reached a downtown core");
            Assert.AreEqual(0, towers.GetValueOrDefault(ZoneKind.OldTown), "an old town grew a 2x2 tower lot");
            Assert.Greater(Share(ZoneKind.Core), Share(ZoneKind.Residential) * 1.5,
                "downtown is not noticeably more tower-heavy than the residential districts");
            Assert.Greater(Share(ZoneKind.Residential), Share(ZoneKind.Outskirts),
                "the residential districts are not more tower-heavy than the outskirts");
        }

        /// <summary>
        /// Guards a specific and very plausible bug: sharing one salt between
        /// the split rolls and the shape rolls. Everything else would still
        /// pass, and every tower in the world would sit in the same corner of
        /// its block.
        /// </summary>
        [Test]
        public void BigBuildingsAreNotAlwaysInTheSameCornerOfTheirBlock()
        {
            CityBlocks blocks = Blocks();
            var corners = new HashSet<(int, int)>();

            foreach (var (x, y) in SampleChunks())
            {
                LotRole role = blocks.RoleFor(x, y);
                if (!role.Builds || !role.IsMultiChunk)
                {
                    continue;
                }
                CityLayout.TryGetBlockRect(x, y, out int minX, out int minY, out _, out _);
                corners.Add((x - minX, y - minY));
            }

            TestContext.WriteLine($"{corners.Count} distinct in-block anchor positions used");
            Assert.GreaterOrEqual(corners.Count, 8,
                "big buildings only ever anchor at a couple of positions -- the split rolls are " +
                "correlated with the shape rolls, most likely a shared salt");
        }

        /// <summary>
        /// Big blocks must fill with several lots, not one building and a sea
        /// of singles -- the point of subdividing.
        /// </summary>
        [Test]
        public void BigBlocksSplitIntoManyLotsOfMixedSizes()
        {
            CityBlocks blocks = Blocks();
            var seen = new HashSet<(int, int)>();
            int bigBlocks = 0;
            int mixed = 0;

            foreach (var (x, y) in SampleChunks())
            {
                if (!CityLayout.TryGetBlockRect(x, y, out int minX, out int minY, out int width, out int height) ||
                    width < 5 || height < 5 || !seen.Add((minX, minY)) || blocks.IsOpenBlock(minX, minY))
                {
                    continue;
                }
                bigBlocks++;
                var spans = new HashSet<(int, int)>();
                for (int ix = 0; ix < width; ix++)
                {
                    for (int iy = 0; iy < height; iy++)
                    {
                        LotRole role = blocks.RoleFor(minX + ix, minY + iy);
                        if (role.Builds)
                        {
                            spans.Add((role.SpanX, role.SpanY));
                        }
                    }
                }
                if (spans.Count >= 2)
                {
                    mixed++;
                }
            }

            TestContext.WriteLine($"{mixed}/{bigBlocks} big blocks hold more than one lot size");
            Assert.Greater(bigBlocks, 50, "the sample found almost no big blocks");
            Assert.Greater(mixed / (double)bigBlocks, 0.85, "big blocks are not mixing lot sizes");
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
                    Assert.AreEqual(first.AnchorX, second.AnchorX, $"({x},{y}) anchorX is not deterministic");
                    Assert.AreEqual(first.AnchorY, second.AnchorY, $"({x},{y}) anchorY is not deterministic");
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
    }
}
