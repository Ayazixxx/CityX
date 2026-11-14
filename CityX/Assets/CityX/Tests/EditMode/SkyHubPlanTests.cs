using System;
using CityX.Character;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Where the sky hub stands (Plan.SkyHubPlan, docs/SKY_HUB.md; owner, 2026-11-02): the island as high as the clouds, the stair round its
    /// circumference with steps a body climbs, its foot on a flat lot inside one block, off every road; no building, hill, expressway or footbridge
    /// in the stair's way; the spawn and Old Guy's house outside its ring; and every machine finding the same.
    /// </summary>
    public sealed class SkyHubPlanTests
    {
        private const int Seed = CitySeed.Default;

        private CityScale _scaleBefore;

        private static PlanWorld Plan => PlanWorld.For(Seed);

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own city
            Footbridges.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            CityScale.Active = _scaleBefore;
            Footbridges.Reset();
        }

        [Test]
        public void TheIslandIsAsHighAsTheClouds()
        {
            Assert.AreEqual(CloudLayer.HeightMetres, SkyHubPlan.IslandHeight, 0.01);
            Assert.IsTrue(Plan.SkyHub.Found, "no land near the spawn holds the hub's landing");
            Assert.AreEqual(Plan.SkyHub.FootHeight + CloudLayer.HeightMetres, Plan.SkyHub.IslandTop, 0.01);
        }

        [Test]
        public void TheStairGoesRoundTheIslandsCircumferenceWithStepsABodyClimbs()
        {
            Assert.AreEqual(SkyHubPlan.IslandRadius + SkyHubPlan.StairGap, SkyHubPlan.StairInnerRadius, 1e-6, "the stair is not at the island's edge");
            Assert.AreEqual(SkyHubPlan.IslandHeight, SkyHubPlan.StepCount * SkyHubPlan.Riser, 1e-6, "the steps do not reach the island");
            Assert.AreEqual(SkyHubPlan.IslandHeight, SkyHubPlan.StairLength * SkyHubPlan.Rise, 1e-6);
            Assert.Greater(SkyHubPlan.StairTurns, 1.0, "the stair does not go all the way round");
            Assert.LessOrEqual(SkyHubPlan.Riser, 0.25, "taller than the steps a body climbs elsewhere");
            double degrees = Math.Atan(SkyHubPlan.Rise) * 180.0 / Math.PI;
            Assert.LessOrEqual(degrees, 33.0, "steeper than the city's stairs (BuildingStairs)");

            SkyHubPlan hub = Plan.SkyHub;
            Vec2 top = hub.PlaceAt(SkyHubPlan.StairLength, -SkyHubPlan.StairWidth * 0.5);
            Assert.AreEqual(SkyHubPlan.StairInnerRadius, (top - new Vec2(hub.CentreX, hub.CentreZ)).Length, 1e-6, "the stair's top is not at the island's edge");
            Assert.AreEqual(hub.IslandTop, hub.HeightAt(SkyHubPlan.StairLength), 1e-6, "the stair's top is not level with the island");
        }

        [Test]
        public void TheLandingIsFlatAndOffTheRoads()
        {
            SkyHubPlan hub = Plan.SkyHub;
            PlanBlock block = Plan.Data.Blocks[hub.Block];
            double high = double.MinValue, low = double.MaxValue;
            const double hair = 0.05;   // the lot may end exactly on a lot line
            for (double x = -SkyHubPlan.LandingLength + hair; x <= SkyHubPlan.LowRun - hair; x += 1.0)
            {
                for (double z = -SkyHubPlan.LandingHalfWidth + hair; z <= SkyHubPlan.LandingHalfWidth - hair; z += 1.0)
                {
                    Vec2 p = hub.LandingPoint(x, z);
                    Assert.IsTrue(PlanMath.PointInPolygon(p, block.Outer), $"({x:F2}, {z:F2}) of the landing is off its block: on a road");
                    Assert.IsFalse(Plan.Fields.IsWater(p.X, p.Y), "the landing is in water");
                    Assert.IsFalse(Plan.Data.Expressways.Covers(p, 0.0), "an expressway is over the landing");
                    Assert.IsFalse(Plan.TutorialHouse.Contains(p.X, p.Y), "the landing is on Old Guy's lot");
                    double h = Plan.Fields.Height(p.X, p.Y);
                    high = Math.Max(high, h);
                    low = Math.Min(low, h);
                }
            }
            Assert.LessOrEqual(high - low, SkyHubPlan.MaxGroundSpread + 0.1, "the ground under the landing is not flat");
            Assert.GreaterOrEqual(hub.FootHeight, high, "the ground comes up through the landing");
            Assert.Less(hub.FootHeight - high, 0.6, "the landing stands too far off the ground to walk onto");
        }

        [Test]
        public void NoBuildingStandsOnTheLandingOrInTheStairsWay()
        {
            SkyHubPlan hub = Plan.SkyHub;
            foreach (BuildingSite b in Plan.Buildings)
            {
                Assert.IsFalse(hub.Overlaps(b), $"building {b.Id} is in the way of the landing or the stair");
                b.WorldToLocal(hub.PlaceAt(0.0).X, hub.PlaceAt(0.0).Y, out double fx, out double fz);
                Assert.IsFalse(Math.Abs(fx) <= b.Params.Width * 0.5 && Math.Abs(fz) <= b.Params.Depth * 0.5, $"building {b.Id} stands on the stair's foot");
            }
            // Walked along the stair from the top of the low run: over every building under it, the underside is clear of the roof.
            var world = new CityWorld(Seed);
            for (double s = SkyHubPlan.LowRun; s < 2.0 * Math.PI * SkyHubPlan.StairMidRadius; s += 2.0)
            {
                foreach (double across in new[] { -SkyHubPlan.StairWidth * 0.5, 0.0, SkyHubPlan.StairWidth * 0.5 })
                {
                    Vec2 p = hub.PlaceAt(s, across);
                    if (world.TryBuildingAt(p.X, p.Y, out BuildingSite under))
                    {
                        Assert.Greater(hub.HeightAt(s) - SkyHubPlan.DeckThickness, under.Top + SkyHubPlan.ClearOverRoofs - 1e-3,
                            $"the stair at {s:F0} m passes through building {under.Id}, whose top is at {under.Top:F1}");
                    }
                }
            }
        }

        [Test]
        public void PastTheLandingTheStairIsClearOfTheGroundAndTheExpressways()
        {
            SkyHubPlan hub = Plan.SkyHub;
            for (double s = SkyHubPlan.LowRun; s * SkyHubPlan.Rise < 500.0; s += 2.0)
            {
                foreach (double across in new[] { -SkyHubPlan.StairWidth * 0.5, 0.0, SkyHubPlan.StairWidth * 0.5 })
                {
                    Vec2 p = hub.PlaceAt(s, across);
                    double clear = hub.HeightAt(s) - SkyHubPlan.DeckThickness - Plan.Fields.Height(p.X, p.Y);
                    Assert.GreaterOrEqual(clear, SkyHubPlan.ClearOverGround - 0.5, $"the stair at {s:F0} m is {clear:F1} m over the ground: a road or a hill runs into it");
                    if (s < SkyHubPlan.ClearOfExpressways)
                    {
                        Assert.IsFalse(Plan.Data.Expressways.Covers(p, 0.0), $"an expressway runs under the stair at {s:F0} m, {hub.HeightAt(s) - hub.FootHeight:F0} m up");
                    }
                }
            }
        }

        [Test]
        public void TheSpawnAndOldGuysHouseAreOutsideTheRing()
        {
            SkyHubPlan hub = Plan.SkyHub;
            var centre = new Vec2(hub.CentreX, hub.CentreZ);
            Assert.IsTrue(Plan.TryFindSpawn(out double sx, out double sz));
            double clear = SkyHubPlan.StairOuterRadius + SkyHubPlan.ClearOfTheFall - 1e-3;
            Assert.GreaterOrEqual((new Vec2(sx, sz) - centre).Length, clear, "the spawn is under the island or the stair: the fall from the sky would hit them");
            foreach (Vec2 corner in Plan.TutorialHouse.Corners())
            {
                Assert.GreaterOrEqual((corner - centre).Length, clear, "Old Guy's lot is under the island or the stair");
            }
            Assert.Less((hub.PlaceAt(0.0) - new Vec2(sx, sz)).Length, SkyHubPlan.SearchRadius, "the landing is far from the spawn");
        }

        [Test]
        public void EveryMachineFindsTheSameHub()
        {
            PlanConfig config = PlanConfig.For(CityScale.Active);
            SkyHubPlan a = PlanWorld.Create(Seed, config).SkyHub, b = PlanWorld.Create(Seed, config).SkyHub;
            Assert.AreEqual(a.CentreX, b.CentreX);
            Assert.AreEqual(a.CentreZ, b.CentreZ);
            Assert.AreEqual(a.StartAngle, b.StartAngle);
            Assert.AreEqual(a.Direction, b.Direction);
            Assert.AreEqual(a.FootHeight, b.FootHeight);
        }

        [Test]
        public void StairOverKnowsTheLowestPassAndOnlyTheRing()
        {
            SkyHubPlan hub = Plan.SkyHub;
            Assert.IsFalse(hub.StairOver(hub.CentreX, hub.CentreZ, out _, out _), "the island's middle is not under the stair");
            Vec2 near = hub.PlaceAt(100.0);
            Assert.IsTrue(hub.StairOver(near.X, near.Y, out double height, out double s));
            Assert.AreEqual(100.0, s, 1e-6);
            Assert.AreEqual(hub.HeightAt(100.0), height, 1e-6);
            // A place the stair passes twice answers for the lower pass.
            Vec2 again = hub.PlaceAt(100.0 + 2.0 * Math.PI * SkyHubPlan.StairMidRadius);
            Assert.IsTrue(hub.StairOver(again.X, again.Y, out double lower, out _));
            Assert.AreEqual(hub.HeightAt(100.0), lower, 1e-3);
        }
    }
}
