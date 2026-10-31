using System;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The Subversion-style city plan (World/Plan): deterministic, sane, and shaped the way a city is -- buildings on lots that face
    /// roads, none in the river trench, none on top of another, a spawn on a real junction.
    /// </summary>
    public sealed class PlanWorldTests
    {
        private const int Seed = 81565117;

        /// <summary>A smaller city than the game's, so the whole fixture runs in about a second.</summary>
        private static PlanConfig SmallCity()
        {
            PlanConfig cfg = PlanWorld.DefaultConfig();
            cfg.Half = 1200.0;
            return cfg;
        }

        private static PlanWorld _world;

        private static PlanWorld World => _world ?? (_world = PlanWorld.Create(Seed, SmallCity()));

        [Test]
        public void TheSameSeedGrowsTheSameCity()
        {
            PlanWorld a = PlanWorld.Create(Seed, SmallCity());
            PlanWorld b = PlanWorld.Create(Seed, SmallCity());

            Assert.AreEqual(a.Data.Nodes.Count, b.Data.Nodes.Count);
            Assert.AreEqual(a.Data.Edges.Count, b.Data.Edges.Count);
            Assert.AreEqual(a.Buildings.Length, b.Buildings.Length);
            for (int i = 0; i < a.Buildings.Length; i += 37)
            {
                Assert.AreEqual(a.Buildings[i].CentreX, b.Buildings[i].CentreX, 0.0, $"building {i} moved");
                Assert.AreEqual(a.Buildings[i].CentreZ, b.Buildings[i].CentreZ, 0.0, $"building {i} moved");
                Assert.AreEqual(a.Buildings[i].Params.Height, b.Buildings[i].Params.Height, $"building {i} changed height");
            }
        }

        [Test]
        public void ADifferentSeedGrowsADifferentCity()
        {
            PlanWorld other = PlanWorld.Create(Seed + 1, SmallCity());
            Assert.AreNotEqual(World.Data.Edges.Count, other.Data.Edges.Count, "two seeds made identical road networks");
        }

        [Test]
        public void ThereIsARealCityNotAHandfulOfBuildings()
        {
            Assert.Greater(World.Data.Edges.Count, 500);
            Assert.Greater(World.Data.Blocks.Count, 150);
            Assert.Greater(World.Buildings.Length, 1500);
        }

        [Test]
        public void BuildingsHaveVariedSizesAndHeights()
        {
            double minW = double.MaxValue, maxW = 0, minH = double.MaxValue, maxH = 0;
            foreach (BuildingSite b in World.Buildings)
            {
                minW = Math.Min(minW, b.Params.Width);
                maxW = Math.Max(maxW, b.Params.Width);
                minH = Math.Min(minH, b.Params.Height);
                maxH = Math.Max(maxH, b.Params.Height);
            }
            Assert.Greater(maxW / minW, 3.0, "every building is about the same width");
            Assert.Greater(maxH / minH, 8.0, "every building is about the same height");
        }

        [Test]
        public void BuildingsAreTurnedEveryWhichWayToFaceTheirStreets()
        {
            int aligned = 0, diagonal = 0;
            foreach (BuildingSite b in World.Buildings)
            {
                double ax = Math.Abs(b.AxisX), az = Math.Abs(b.AxisZ);
                if (ax > 0.97 || az > 0.97) aligned++;
                else diagonal++;
            }
            Assert.Greater(diagonal, World.Buildings.Length / 5, "nearly every building lines up with the world axes -- this is a grid city");
            Assert.Greater(aligned, 20, "no building lines up with a street running along an axis");
        }

        [Test]
        public void AxisIsAUnitVector()
        {
            foreach (BuildingSite b in World.Buildings)
            {
                Assert.AreEqual(1.0, b.AxisX * b.AxisX + b.AxisZ * b.AxisZ, 1e-6);
            }
        }

        [Test]
        public void NoBuildingStandsInTheTrench()
        {
            int wet = 0;
            foreach (BuildingSite b in World.Buildings)
            {
                if (World.Fields.IsWater(b.CentreX, b.CentreZ)) wet++;
            }
            Assert.AreEqual(0, wet, "buildings are standing in the river bed");
        }

        [Test]
        public void BuildingsDoNotOverlapEachOther()
        {
            int overlapping = 0, corners = 0;
            int[] sx = { -1, 1, 1, -1 }, sz = { -1, -1, 1, 1 };
            foreach (BuildingSite b in World.Buildings)
            {
                if (b.IsOpenGround) continue;   // a surface car park is open ground: BuildingAtHeight looks through it on purpose
                for (int c = 0; c < 4; c++)
                {
                    b.LocalToWorld(sx[c] * b.Params.Width * 0.5 * 0.98, sz[c] * b.Params.Depth * 0.5 * 0.98, out double x, out double z);
                    corners++;
                    // At the building's own mid-height: a tower stands on its podium's roof, so over the same ground there can be
                    // two buildings, one above the other, and they do not overlap.
                    if (World.BuildingAtHeight(x, b.BaseHeight + b.Params.Height * 0.5, z) != b.Id) overlapping++;
                }
            }
            Assert.Less(overlapping / (double)corners, 0.001, $"{overlapping} of {corners} building corners are inside another building");
        }

        [Test]
        public void EveryBuildingsFrontDoorFacesAStreet()
        {
            int near = 0;
            foreach (BuildingSite b in World.Buildings)
            {
                // A point a few metres out from the door.
                b.LocalToWorld(0.0, b.Params.Depth * 0.5 + 6.0, out double x, out double z);
                double best = double.MaxValue;
                int cx = PlanWorld.ChunkOf(x), cy = PlanWorld.ChunkOf(z);
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        var ids = World.EdgesIn(cx + dx, cy + dy);
                        for (int i = 0; i < ids.Count; i++)
                        {
                            EdgeGeometry g = World.Geometry.Edges[ids[i]];
                            if (g == null) continue;
                            double d = PlanMath.DistanceToSegment(new Vec2(x, z), g.A, g.B, out _);
                            if (d < best) best = d;
                        }
                    }
                }
                if (best < 26.0) near++;
            }
            Assert.Greater(near / (double)World.Buildings.Length, 0.95, "buildings do not face the road they sit on");
        }

        [Test]
        public void TheSpawnIsAJunctionOnDryLand()
        {
            Assert.IsTrue(World.TryFindSpawn(out double x, out double z));
            Assert.IsFalse(World.Fields.IsWater(x, z));
            double nearest = double.MaxValue;
            foreach (RoadNode n in World.Data.Nodes)
            {
                nearest = Math.Min(nearest, (n.P.X - x) * (n.P.X - x) + (n.P.Y - z) * (n.P.Y - z));
            }
            Assert.Less(nearest, 1.0, "the spawn is not on a road node");
        }

        [Test]
        public void TheChunkIndexHoldsEveryBuildingExactlyOnce()
        {
            int total = 0;
            for (int cx = -60; cx <= 60; cx++)
            {
                for (int cy = -60; cy <= 60; cy++)
                {
                    foreach (int id in World.BuildingsOwnedBy(cx, cy))
                    {
                        BuildingSite b = World.Buildings[id];
                        Assert.AreEqual(cx, b.OwnerChunkX);
                        Assert.AreEqual(cy, b.OwnerChunkY);
                        total++;
                    }
                }
            }
            Assert.AreEqual(World.Buildings.Length, total);
        }

        [Test]
        public void BuildingsStayInsideTheRegion()
        {
            foreach (BuildingSite b in World.Buildings)
            {
                Assert.LessOrEqual(Math.Abs(b.CentreX), World.Data.Config.Half + 5.0);
                Assert.LessOrEqual(Math.Abs(b.CentreZ), World.Data.Config.Half + 5.0);
            }
        }

        [Test]
        public void ARoadRibbonIsNeverLongerThanItsRoad()
        {
            foreach (EdgeGeometry g in World.Geometry.Edges)
            {
                if (g == null) continue;
                Assert.GreaterOrEqual(g.TrimA, 0.0);
                Assert.GreaterOrEqual(g.TrimB, 0.0);
                Assert.LessOrEqual(g.TrimA + g.TrimB, g.Length * 0.97, $"road {g.Edge.Id} is trimmed to nothing");
            }
        }

        [Test]
        public void JunctionsHaveAPlateAroundThem()
        {
            int plated = 0;
            foreach (JunctionGeometry j in World.Geometry.Junctions)
            {
                if (j == null || j.Node.Edges.Count < 2) continue;
                Assert.IsNotNull(j.Plate, $"junction {j.Node.Id} has no plate");
                Assert.GreaterOrEqual(j.Plate.Length, 3);
                foreach (Vec2 p in j.Plate)
                {
                    Assert.Less((p - j.Node.P).Length, 80.0, "a plate corner flew off");
                }
                plated++;
            }
            Assert.Greater(plated, 100);
        }

        [Test]
        public void BlocksHaveAKerbAndAPavementOutlineOfTheSameShape()
        {
            int withRings = 0;
            foreach (PlanBlock b in World.Data.Blocks)
            {
                if (b.Inner == null) continue;
                Assert.AreEqual(b.Poly.Count, b.Inner.Count);
                Assert.AreEqual(b.Poly.Count, b.Outer.Count);
                withRings++;
            }
            Assert.Greater(withRings, 100);
        }

        [Test]
        public void TheGroundIsSmoothAndTheTrenchIsBelowTheCity()
        {
            // Smooth: two points a metre apart are never far apart in height.
            double worst = 0.0;
            for (double x = -600; x <= 600; x += 7.3)
            {
                for (double z = -600; z <= 600; z += 11.1)
                {
                    worst = Math.Max(worst, Math.Abs(World.GroundHeight(x, z) - World.GroundHeight(x + 1.0, z)));
                }
            }
            Assert.Less(worst, 1.5, "the ground has a cliff");

            // The trench: wherever a river runs the ground is lower than at its banks. (Only rivers are carved: a lake is flat ground the city
            // leaves empty, at whatever height its hills put it -- seed 38461927 had one at 27 m inside this square.)
            int checkedWet = 0;
            double band = World.Data.Config.RiverBand;
            for (double x = -1100; x <= 1100 && checkedWet < 20; x += 13.7)
            {
                for (double z = -1100; z <= 1100 && checkedWet < 20; z += 17.3)
                {
                    if (Math.Abs(World.Fields.RiverNoise(x, z)) < band)
                    {
                        Assert.Less(World.GroundHeight(x, z), 4.0, "the river bed is not below the city");
                        checkedWet++;
                    }
                }
            }
            Assert.Greater(checkedWet, 0, "no river anywhere in the test region");
        }
    }
}
