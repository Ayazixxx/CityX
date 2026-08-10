using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The walkway lattice.
    ///
    /// <see cref="BothEndsOfABridgeAgreeWithoutTalkingToEachOther"/> is the one
    /// that matters. Chunks generate independently, so a bridge is decided
    /// twice -- once by the building at each end -- and if those two answers
    /// ever differ the world gets bridges built from one side only. Nothing
    /// else here would catch that.
    /// </summary>
    public class SkybridgeLayoutTests
    {
        private const int Seed = 20260810;
        private const int Sweep = 60;

        /// <summary>Every adjacent building pair in a sweep, with its bridges.</summary>
        private static List<(BuildingSite a, BuildingSite b, List<Skybridge> bridges)> AllPairs(int worldSeed)
        {
            CityWorld world = new CityWorld(worldSeed);
            SkybridgeLayout layout = new SkybridgeLayout(worldSeed);
            var pairs = new List<(BuildingSite, BuildingSite, List<Skybridge>)>();
            var seen = new HashSet<(int, int, int, int)>();

            for (int x = -Sweep; x <= Sweep; x++)
            {
                for (int y = -Sweep; y <= Sweep; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;   // only visit each building once, at its anchor
                    }

                    foreach (BuildingSite neighbour in new[]
                             {
                                 SkybridgeLayout.EastNeighbour(world, site),
                                 SkybridgeLayout.NorthNeighbour(world, site),
                             })
                    {
                        if (!neighbour.Exists)
                        {
                            continue;
                        }
                        var key = (site.AnchorX, site.AnchorY, neighbour.AnchorX, neighbour.AnchorY);
                        if (!seen.Add(key))
                        {
                            continue;
                        }
                        pairs.Add((site, neighbour, layout.BridgesBetween(site, neighbour)));
                    }
                }
            }
            return pairs;
        }

        /// <summary>
        /// The load-bearing one. Ask each end independently and demand the same
        /// answer, height for height.
        /// </summary>
        [Test]
        public void BothEndsOfABridgeAgreeWithoutTalkingToEachOther()
        {
            SkybridgeLayout layout = new SkybridgeLayout(Seed);
            int compared = 0;

            foreach (var (a, b, forward) in AllPairs(Seed))
            {
                List<Skybridge> backward = layout.BridgesBetween(b, a);
                compared++;

                Assert.AreEqual(forward.Count, backward.Count,
                    $"({a.AnchorX},{a.AnchorY}) says {forward.Count} bridges to " +
                    $"({b.AnchorX},{b.AnchorY}), which says {backward.Count} back");

                for (int i = 0; i < forward.Count; i++)
                {
                    Assert.AreEqual(forward[i].Height, backward[i].Height,
                        $"bridge {i} between ({a.AnchorX},{a.AnchorY}) and ({b.AnchorX},{b.AnchorY}) " +
                        $"is at {forward[i].Height}m from one end and {backward[i].Height}m from the other");
                }
            }

            Assert.Greater(compared, 200, "not enough pairs sampled for this to mean anything");
        }

        /// <summary>
        /// A bridge has to meet a real floor slab at BOTH ends. The two
        /// buildings usually stand on different terraces, so this only works
        /// because terrace steps are a whole number of floors.
        /// </summary>
        [Test]
        public void EveryBridgeLandsOnAWholeFloorInBothBuildings()
        {
            foreach (var (a, b, bridges) in AllPairs(Seed))
            {
                foreach (Skybridge bridge in bridges)
                {
                    double floors = bridge.Height / SkybridgeLayout.FloorHeight;
                    Assert.AreEqual(System.Math.Round(floors), floors, 1e-3,
                        $"a bridge at {bridge.Height}m is not on a floor boundary");

                    foreach (BuildingSite end in new[] { a, b })
                    {
                        double aboveBase = (bridge.Height - end.BaseHeight) / SkybridgeLayout.FloorHeight;
                        Assert.AreEqual(System.Math.Round(aboveBase), aboveBase, 1e-3,
                            $"a bridge at {bridge.Height}m meets ({end.AnchorX},{end.AnchorY}) " +
                            $"{aboveBase:0.00} floors up -- halfway through a wall");
                    }
                }
            }
        }

        [Test]
        public void NoBridgeEverLeavesTheBuildingsItConnects()
        {
            int total = 0;
            foreach (var (a, b, bridges) in AllPairs(Seed))
            {
                foreach (Skybridge bridge in bridges)
                {
                    total++;
                    foreach (BuildingSite end in new[] { a, b })
                    {
                        Assert.GreaterOrEqual(bridge.Height, end.BaseHeight,
                            $"a bridge at {bridge.Height}m is below the ground floor of " +
                            $"({end.AnchorX},{end.AnchorY}) at {end.BaseHeight}m");
                        Assert.LessOrEqual(bridge.Height, end.Top,
                            $"a bridge at {bridge.Height}m is above the roof of " +
                            $"({end.AnchorX},{end.AnchorY}) at {end.Top}m -- it attaches to sky");
                    }
                }
            }
            Assert.Greater(total, 200, "not enough bridges sampled");
        }

        [Test]
        public void TwoBridgesNeverOccupyTheSameHeight()
        {
            foreach (var (a, b, bridges) in AllPairs(Seed))
            {
                var heights = new HashSet<float>();
                foreach (Skybridge bridge in bridges)
                {
                    Assert.IsTrue(heights.Add(bridge.Height),
                        $"({a.AnchorX},{a.AnchorY})-({b.AnchorX},{b.AnchorY}) has two bridges " +
                        $"at {bridge.Height}m");
                }
            }
        }

        [Test]
        public void ShortBuildingsAreNotPartOfTheLattice()
        {
            foreach (var (a, b, bridges) in AllPairs(Seed))
            {
                if (a.Params.Height < SkybridgeLayout.MinBuildingHeight ||
                    b.Params.Height < SkybridgeLayout.MinBuildingHeight)
                {
                    Assert.IsEmpty(bridges,
                        $"a {System.Math.Min(a.Params.Height, b.Params.Height):0}m building at " +
                        $"({a.AnchorX},{a.AnchorY}) got a walkway");
                }
            }
        }

        /// <summary>
        /// The reference art shows three or four levels of walkway crossing
        /// overhead at once. One bridge per pair would be the old Chongqing-
        /// photo read, not the one the user asked for.
        /// </summary>
        [Test]
        public void PairsStackSeveralLevelsOfWalkway()
        {
            var levelCounts = new Dictionary<int, int>();
            int bridgedPairs = 0;
            int totalPairs = 0;

            foreach (var (_, _, bridges) in AllPairs(Seed))
            {
                totalPairs++;
                levelCounts.TryGetValue(bridges.Count, out int n);
                levelCounts[bridges.Count] = n + 1;
                if (bridges.Count > 0)
                {
                    bridgedPairs++;
                }
            }

            int deepest = 0;
            foreach (var pair in levelCounts)
            {
                if (pair.Key > deepest)
                {
                    deepest = pair.Key;
                }
            }
            var lines = new List<string>();
            for (int i = 0; i <= deepest; i++)
            {
                levelCounts.TryGetValue(i, out int n);
                lines.Add($"{i} bridge(s): {n} pairs ({100.0 * n / totalPairs:F1}%)");
            }
            TestContext.WriteLine(string.Join("\n", lines));

            double bridgedShare = bridgedPairs / (double)totalPairs;
            Assert.Greater(bridgedShare, 0.10,
                $"only {bridgedShare:P0} of adjacent pairs are bridged -- there is no lattice");
            Assert.Less(bridgedShare, 0.80,
                $"{bridgedShare:P0} of pairs are bridged -- the canyons are roofed over, " +
                "and the canyons are half the reference");

            // WHY THIS ASSERTION IS A SHARE AND NOT "at least one exists".
            // The first version of this test demanded only that SOME pair
            // somewhere had three levels. It passed on 17 pairs out of 11000
            // while the city was, in every practical sense, a single-bridge
            // city -- 65.8% of pairs unbridged and 1.7% carrying more than one
            // level. A test that green-lights the exact outcome it was written
            // to prevent is worse than no test, so it now measures the share.
            int multiLevel = 0;
            foreach (var pair in levelCounts)
            {
                if (pair.Key >= 2)
                {
                    multiLevel += pair.Value;
                }
            }
            double multiShare = multiLevel / (double)bridgedPairs;
            TestContext.WriteLine($"{multiShare:P1} of bridged pairs carry more than one level");
            Assert.Greater(multiShare, 0.15,
                $"only {multiShare:P1} of bridged pairs stack more than one walkway -- the reference " +
                "is a lattice at several heights, not one bridge per gap");
        }

        [Test]
        public void ABuildingNeverBridgesToItself()
        {
            foreach (var (a, b, bridges) in AllPairs(Seed))
            {
                Assert.IsFalse(a.AnchorX == b.AnchorX && a.AnchorY == b.AnchorY,
                    $"({a.AnchorX},{a.AnchorY}) was paired with itself");
                foreach (Skybridge bridge in bridges)
                {
                    Assert.IsFalse(
                        bridge.FromAnchorX == bridge.ToAnchorX && bridge.FromAnchorY == bridge.ToAnchorY,
                        "a bridge connects a building to itself");
                }
            }
        }

        [Test]
        public void TheSameSeedAlwaysProducesTheSameLattice()
        {
            SkybridgeLayout first = new SkybridgeLayout(4242);
            SkybridgeLayout second = new SkybridgeLayout(4242);
            CityWorld world = new CityWorld(4242);

            for (int x = -25; x <= 25; x++)
            {
                for (int y = -25; y <= 25; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists)
                    {
                        continue;
                    }
                    BuildingSite east = SkybridgeLayout.EastNeighbour(world, site);
                    if (!east.Exists)
                    {
                        continue;
                    }

                    List<Skybridge> a = first.BridgesBetween(site, east);
                    List<Skybridge> b = second.BridgesBetween(site, east);
                    Assert.AreEqual(a.Count, b.Count, $"({x},{y}) bridge count is not deterministic");
                    for (int i = 0; i < a.Count; i++)
                    {
                        Assert.AreEqual(a[i].Height, b[i].Height, $"({x},{y}) bridge {i} height");
                    }
                }
            }
        }
    }

    /// <summary>
    /// The facade that ties blocks, generator and terrain together.
    /// </summary>
    public class CityWorldTests
    {
        private const int Seed = 20260810;

        /// <summary>
        /// A covered chunk must resolve to the same building as its anchor --
        /// otherwise a 2x2 tower is four different towers depending on which
        /// chunk you ask.
        /// </summary>
        [Test]
        public void CoveredChunksResolveToTheSameBuildingAsTheirAnchor()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -60; x <= 60; x++)
            {
                for (int y = -60; y <= 60; y++)
                {
                    LotRole role = world.RoleAt(x, y);
                    if (role.Kind != LotKind.Consumed)
                    {
                        continue;
                    }
                    checkedCount++;

                    BuildingSite viaCovered = world.BuildingAt(x, y);
                    BuildingSite viaAnchor = world.BuildingAt(role.AnchorX, role.AnchorY);

                    Assert.AreEqual(viaAnchor.Exists, viaCovered.Exists, $"({x},{y}) existence");
                    if (!viaAnchor.Exists)
                    {
                        continue;
                    }
                    Assert.AreEqual(viaAnchor.AnchorX, viaCovered.AnchorX, $"({x},{y}) anchorX");
                    Assert.AreEqual(viaAnchor.AnchorY, viaCovered.AnchorY, $"({x},{y}) anchorY");
                    Assert.AreEqual(viaAnchor.Params.Height, viaCovered.Params.Height, $"({x},{y}) height");
                    Assert.AreEqual(viaAnchor.BaseHeight, viaCovered.BaseHeight, $"({x},{y}) base");
                    Assert.AreEqual(viaAnchor.Params.Mass, viaCovered.Params.Mass, $"({x},{y}) mass");
                }
            }

            Assert.Greater(checkedCount, 300, "not enough covered chunks sampled");
        }

        [Test]
        public void StreetChunksHoldNoBuilding()
        {
            CityWorld world = new CityWorld(Seed);
            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    Assert.IsFalse(world.BuildingAt(x, y).Exists, $"({x},{y}) is a street");
                }
            }
        }

        /// <summary>
        /// Chunk seeds must not collide on a grid. A city IS a grid, so a hash
        /// that collides on structured input would tile the same building
        /// across the world in a visible pattern.
        /// </summary>
        [Test]
        public void ChunkSeedsDoNotCollideAcrossTheGrid()
        {
            CityWorld world = new CityWorld(Seed);
            var seeds = new HashSet<int>();
            int total = 0;
            int collisions = 0;

            for (int x = -100; x <= 100; x++)
            {
                for (int y = -100; y <= 100; y++)
                {
                    total++;
                    if (!seeds.Add(world.ChunkSeed(x, y)))
                    {
                        collisions++;
                    }
                }
            }

            // Birthday bound: ~40401 draws from 2^32 expects about 0.19
            // collisions, so a handful is normal and a pattern is not.
            TestContext.WriteLine($"{collisions} collisions in {total} chunk seeds");
            Assert.Less(collisions, 10,
                $"{collisions} chunk seeds collide across a {total}-chunk grid -- the seed hash " +
                "is picking up the grid structure and buildings will visibly repeat");
        }

        [Test]
        public void PodiumNeverReachesAboveTheGroundFloor()
        {
            CityWorld world = new CityWorld(Seed);
            float deepest = 0.0f;

            for (int x = -60; x <= 60; x++)
            {
                for (int y = -60; y <= 60; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists)
                    {
                        continue;
                    }
                    Assert.GreaterOrEqual(site.PodiumDepth, 0.0f,
                        $"({x},{y}) has a podium reaching above its own ground floor");
                    if (site.PodiumDepth > deepest)
                    {
                        deepest = site.PodiumDepth;
                    }
                }
            }

            TestContext.WriteLine($"deepest podium in the sweep: {deepest}m");
        }
    }
}
