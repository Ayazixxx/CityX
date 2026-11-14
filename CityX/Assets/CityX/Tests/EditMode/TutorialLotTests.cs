using System;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// Old Guy's house in the generated city (Plan.TutorialLot, docs/OLD_GUYS_HOUSE.md): on the game's world it stands on the land nearest the
    /// spawn, its whole lot inside one block's lot line with its front on a street, on flat dry ground, and it is the building closest to where the
    /// player lands (its driveway), with no generated building or footbridge on its lot.
    /// </summary>
    public sealed class TutorialLotTests
    {
        private const int Seed = CitySeed.Default;

        private CityScale _scaleBefore;

        private static PlanWorld Plan => PlanWorld.For(Seed);

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own city: the classic one's blocks are too small for the house
            Footbridges.Reset();   // the bridges worked out for the other scale's buildings
        }

        [TearDown]
        public void TearDown()
        {
            CityScale.Active = _scaleBefore;
            Footbridges.Reset();
        }

        [Test]
        public void TheHouseStandsOnLandBesideTheSpawn()
        {
            TutorialLot lot = Plan.TutorialHouse;
            Assert.IsTrue(lot.Found, "no land near the spawn holds the house");
            Assert.IsTrue(Plan.TryFindSpawn(out double sx, out double sz));
            Assert.Less((lot.FrontMiddle - new Vec2(sx, sz)).Length, 60.0, "the house is not beside the spawn");
        }

        [Test]
        public void WhereThePlayerLandsItIsTheNearestBuilding()
        {
            TutorialLot lot = Plan.TutorialHouse;
            lot.ToWorld(House.Landing.x, House.Landing.z, out double px, out double pz);
            double house = Gap(House.Landing.x, House.Landing.z, TutorialLot.HouseX0, TutorialLot.HouseX1, TutorialLot.HouseZ0, TutorialLot.HouseZ1);
            Assert.IsTrue(lot.Contains(px, pz), "the player does not land on the house's lot");
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (b.IsOpenGround) continue;
                b.WorldToLocal(px, pz, out double bx, out double bz);
                double other = Gap(bx, bz, -b.Params.Width * 0.5, b.Params.Width * 0.5, -b.Params.Depth * 0.5, b.Params.Depth * 0.5);
                Assert.Greater(other, house, $"building {b.Id} is nearer where the player lands ({other:F1} m) than Old Guy's house ({house:F1} m)");
            }
        }

        [Test]
        public void TheLotIsInsideItsBlockWithItsFrontOnTheStreet()
        {
            TutorialLot lot = Plan.TutorialHouse;
            PlanBlock block = Plan.Data.Blocks[lot.Block];
            foreach (Vec2 corner in lot.Corners(-0.03))
            {
                Assert.IsTrue(PlanMath.PointInPolygon(corner, block.Outer), $"the lot's corner {corner.X:F1}, {corner.Y:F1} is outside its block's lot line");
            }
            double toLotLine = double.MaxValue;
            for (int i = 0, n = block.Outer.Count; i < n; i++)
            {
                toLotLine = Math.Min(toLotLine, PlanMath.DistanceToSegment(lot.FrontMiddle, block.Outer[i], block.Outer[(i + 1) % n], out _));
            }
            Assert.Less(toLotLine, TutorialLot.FrontInset + 0.01, "the lot's front is not on the street's lot line");
            Assert.IsFalse(block.Plaza || block.Mall || block.Encloses || block.StairLane >= 0, "the house took a plaza, a mall, an island's block or a stairway's");
        }

        [Test]
        public void TheGroundUnderTheLotIsFlatDryAndOpen()
        {
            TutorialLot lot = Plan.TutorialHouse;
            double high = double.MinValue, low = double.MaxValue;
            for (double x = TutorialLot.LotX0; x <= TutorialLot.LotX1; x += 1.0)
            {
                for (double z = TutorialLot.LotZ0; z <= TutorialLot.LotZ1; z += 1.0)
                {
                    lot.ToWorld(x, z, out double wx, out double wz);
                    Assert.IsFalse(Plan.Fields.IsWater(wx, wz), $"water under the lot at {x:F0}, {z:F0}");
                    Assert.IsFalse(Plan.Data.Expressways.Covers(new Vec2(wx, wz), 0.0), $"an expressway over the lot at {x:F0}, {z:F0}");
                    double h = Plan.GroundHeight(wx, wz);
                    high = Math.Max(high, h);
                    low = Math.Min(low, h);
                }
            }
            // Sampled finer than the search did: a little over its bound is the ground between its samples.
            Assert.Less(high - low, TutorialLot.MaxGroundSpread + 0.1, "the ground under the lot is not flat");
            Assert.GreaterOrEqual(lot.FloorHeight, high + PlanBuildings.PavingRise, "the floor is below the paving round the house");
            Assert.LessOrEqual(lot.LowestGround, low + 0.05, "the footing does not reach the lowest ground");
        }

        [Test]
        public void NoGeneratedBuildingOrFootbridgeIsOnTheLot()
        {
            var world = new CityWorld(Seed);
            TutorialLot lot = world.Plan.TutorialHouse;
            Vec2 middle = lot.FrontMiddle;
            for (int i = 0; i < world.Plan.Buildings.Length; i++)
            {
                BuildingSite b = world.Plan.Buildings[i];
                Assert.AreEqual(i, b.Id, "a building's id is not its place in the city's list");
                Assert.IsFalse(lot.Overlaps(b), $"building {b.Id} stands on Old Guy's lot");
                if ((new Vec2(b.CentreX, b.CentreZ) - middle).Length > 200.0) continue;
                foreach (Footbridge bridge in Footbridges.For(world, b))
                {
                    for (float t = 0.0f; t <= 1.0f; t += 0.02f)
                    {
                        UnityEngine.Vector3 p = UnityEngine.Vector3.Lerp(bridge.StartWorld, bridge.EndWorld, t);
                        Assert.IsFalse(world.Plan.OnTutorialLot(p.x, p.z), $"the footbridge from building {bridge.SiteA} to {bridge.SiteB} crosses Old Guy's lot");
                    }
                }
            }
        }

        [Test]
        public void EveryMachinePutsItInTheSamePlace()
        {
            PlanWorld a = PlanWorld.Create(Seed, PlanWorld.DefaultConfig());
            TutorialLot lot = Plan.TutorialHouse;
            Assert.AreEqual(lot.OriginX, a.TutorialHouse.OriginX, 0.0);
            Assert.AreEqual(lot.OriginZ, a.TutorialHouse.OriginZ, 0.0);
            Assert.AreEqual(lot.AxisX, a.TutorialHouse.AxisX, 0.0);
            Assert.AreEqual(lot.FloorHeight, a.TutorialHouse.FloorHeight, 0.0);
            Assert.AreEqual(Plan.Buildings.Length, a.Buildings.Length);
        }

        [Test]
        public void TheHousesPlanFitsTheLandTheCityKeeps()
        {
            Assert.AreEqual(TutorialLot.LotX0, House.Lot.X0, 1e-4);
            Assert.AreEqual(TutorialLot.LotZ0, House.Lot.Z0, 1e-4);
            Assert.AreEqual(TutorialLot.LotX1, House.Lot.X1, 1e-4);
            Assert.AreEqual(TutorialLot.LotZ1, House.Lot.Z1, 1e-4);
            Assert.AreEqual(TutorialLot.HouseHeight, House.RoofHighY, 1e-4);
            foreach (House.Room room in House.Rooms)
            {
                foreach (House.Area area in room.Floors)
                {
                    Assert.IsTrue(House.Lot.Contains(area.X0, area.Z0) && House.Lot.Contains(area.X1, area.Z1), $"{room.Name} reaches off the lot");
                }
            }
            foreach (House.Block block in House.Blocks)
            {
                Assert.IsTrue(House.Lot.Contains(block.Min.x, block.Min.z) && House.Lot.Contains(block.Max.x, block.Max.z), "the porch or a footing leaves the lot");
            }
            foreach (House.Step step in House.OutdoorSteps())
            {
                Assert.IsTrue(House.Lot.Contains(step.Tread.X0, step.Tread.Z0) && House.Lot.Contains(step.Tread.X1, step.Tread.Z1), "the outdoor steps leave the lot");
            }
            Assert.IsTrue(House.Lot.Contains(-House.RoofOverhang, House.Depth + House.RoofOverhang), "the roof's eaves leave the lot");
        }

        [Test]
        public void ATurnedLotMapsToTheWorldAndBack()
        {
            var lot = new TutorialLot(100.0, -40.0, 0.6, 0.8, 5.0, 4.5, 0);
            lot.ToWorld(3.0, 7.0, out double wx, out double wz);
            lot.ToLocal(wx, wz, out double x, out double z);
            Assert.AreEqual(3.0, x, 1e-9);
            Assert.AreEqual(7.0, z, 1e-9);
            // +z, away from the street, is +x turned a quarter counter-clockwise.
            lot.ToWorld(0.0, 1.0, out double zx, out double zz);
            Assert.AreEqual(-0.8, zx - 100.0, 1e-9);
            Assert.AreEqual(0.6, zz + 40.0, 1e-9);
            Assert.IsTrue(lot.Contains(wx, wz));
            lot.ToWorld(TutorialLot.LotX1 + 1.0, 0.0, out double ox, out double oz);
            Assert.IsFalse(lot.Contains(ox, oz));
            Assert.IsTrue(lot.Contains(ox, oz, 1.5));
        }

        private static double Gap(double x, double z, double x0, double x1, double z0, double z1)
        {
            double dx = Math.Max(0.0, Math.Max(x0 - x, x - x1)), dz = Math.Max(0.0, Math.Max(z0 - z, z - z1));
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
