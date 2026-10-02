using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The stairs of every building: never steeper than a person can comfortably climb, always somewhere to stand at each end, never
    /// through the ceiling or into a wall, and a real mix of kinds.
    /// </summary>
    public sealed class BuildingStairsTests
    {
        private const int Seed = 61472093;
        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static IEnumerable<BuildingSite> Buildings(int stride)
        {
            for (int id = 0; id < World.BuildingCount; id += stride)
            {
                BuildingSite site = World.Building(id);
                if (site.Params.HasBuilding)
                {
                    yield return site;
                }
            }
        }

        private static bool Hits(Rect a, Rect b) => a.xMin < b.xMax - 0.01f && a.xMax > b.xMin + 0.01f && a.yMin < b.yMax - 0.01f && a.yMax > b.yMin + 0.01f;

        private static IEnumerable<StairPlan> AllPlans(BuildingStairs.Layout layout)
        {
            if (layout.Basement != null)
            {
                yield return layout.Basement;
            }
            foreach (StairPlan p in layout.Floors)
            {
                if (p != null)
                {
                    yield return p;
                }
            }
        }

        [Test]
        public void NoStairIsSteeperThanAWalkableSlope()
        {
            int flights = 0;
            float steepest = 0.0f;
            foreach (BuildingSite site in Buildings(7))
            {
                foreach (StairPlan p in AllPlans(BuildingStairs.PlanFor(site)))
                {
                    flights++;
                    steepest = Mathf.Max(steepest, p.AngleDegrees);
                    Assert.LessOrEqual(p.AngleDegrees, BuildingStairs.MaxAngleDegrees, $"building {site.Id}: a {p.Kind} stair at {p.AngleDegrees:0.0} degrees");
                }
            }
            Assert.Greater(flights, 1000, "need a real sample");
            TestContext.WriteLine($"{flights} stairs, steepest {steepest:0.0} degrees");
        }

        [Test]
        public void EveryFittedOutFloorHasAStairAndASealedTopHasNone()
        {
            int missing = 0, total = 0;
            foreach (BuildingSite site in Buildings(5))
            {
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                for (int f = 0; f < layout.FloorCount; f++)
                {
                    if (f == layout.FloorCount - 1 && layout.SealedTop)
                    {
                        Assert.IsNull(layout.Floors[f], "a sealed ceiling has nothing above it to climb to");
                        continue;
                    }
                    total++;
                    missing += layout.Floors[f] == null ? 1 : 0;
                }
            }
            Assert.Greater(total, 5000);
            Assert.LessOrEqual(missing, total * 0.002, $"{missing} of {total} floors found no stair that fits");
        }

        [Test]
        public void EveryStairStaysInsideItsRoomAndLeavesRoomAtBothEnds()
        {
            foreach (BuildingSite site in Buildings(9))
            {
                float hx = site.Params.Width * 0.5f - BuildingInterior.WallThickness;
                float hz = site.Params.Depth * 0.5f - BuildingInterior.WallThickness;
                foreach (StairPlan p in AllPlans(BuildingStairs.PlanFor(site)))
                {
                    Assert.GreaterOrEqual(p.Bounds.xMin, -hx, $"building {site.Id} {p.Kind}");
                    Assert.LessOrEqual(p.Bounds.xMax, hx, $"building {site.Id} {p.Kind}");
                    Assert.GreaterOrEqual(p.Bounds.yMin, -hz, $"building {site.Id} {p.Kind}");
                    Assert.LessOrEqual(p.Bounds.yMax, hz, $"building {site.Id} {p.Kind}");
                    Assert.GreaterOrEqual(Mathf.Max(p.Entry.width, p.Entry.height), BuildingStairs.Width - 0.01f);
                    Assert.IsFalse(Hits(p.Exit, p.Hole), $"building {site.Id} {p.Kind}: the cell you step off onto is part of the opening");
                    foreach (Rect tread in p.Treads)
                    {
                        Assert.IsFalse(Hits(tread, p.Exit) && p.Kind != StairKind.Wrap, "a step on the exit cell");
                    }
                }
            }
        }

        [Test]
        public void TheOpeningIsWhereTheCeilingIsCloseAndTheEndCellIsNot()
        {
            foreach (BuildingSite site in Buildings(11))
            {
                foreach (StairPlan p in AllPlans(BuildingStairs.PlanFor(site)))
                {
                    for (int i = 0; i < p.Treads.Length; i++)
                    {
                        if (p.Tops[i] > p.Rise - BuildingStairs.HeadClear)
                        {
                            Assert.IsTrue(p.Hole.Contains(p.Treads[i].center), $"building {site.Id} {p.Kind}: a step within head height of the ceiling is under solid slab");
                        }
                    }
                }
            }
        }

        [Test]
        public void ConsecutiveFloorsLineUpSoNobodyClimbsIntoASolidFloor()
        {
            foreach (BuildingSite site in Buildings(13))
            {
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                StairPlan previous = layout.Basement;
                for (int f = 0; f < layout.FloorCount; f++)
                {
                    StairPlan p = layout.Floors[f];
                    if (p != null && previous != null)
                    {
                        Assert.IsFalse(Hits(p.Entry, previous.Hole), $"building {site.Id} floor {f}: {p.Kind} entry {p.Entry} over the opening {previous.Hole} below ({previous.Kind}, core {layout.Core})");
                    }
                    previous = p ?? previous;
                }
            }
        }

        [Test]
        public void ManyKindsOfStairAppearAndSomeBuildingsHaveACore()
        {
            var kinds = new Dictionary<StairKind, int>();
            var flights = new Dictionary<int, int>();
            int cores = 0, buildings = 0;
            foreach (BuildingSite site in Buildings(3))
            {
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                buildings++;
                cores += layout.Core ? 1 : 0;
                foreach (StairPlan p in AllPlans(layout))
                {
                    kinds[p.Kind] = (kinds.TryGetValue(p.Kind, out int n) ? n : 0) + 1;
                    if (p.Kind == StairKind.Switchback)
                    {
                        flights[p.Flights] = (flights.TryGetValue(p.Flights, out int m) ? m : 0) + 1;
                    }
                }
            }
            foreach (KeyValuePair<StairKind, int> kv in kinds)
            {
                TestContext.WriteLine($"{kv.Key}: {kv.Value}");
            }
            TestContext.WriteLine($"cores: {cores} of {buildings} buildings");
            foreach (StairKind kind in new[] { StairKind.Straight, StairKind.Landing, StairKind.L, StairKind.Switchback, StairKind.Wrap })
            {
                Assert.Greater(kinds.TryGetValue(kind, out int count) ? count : 0, 30, $"{kind} stairs should be a regular sight");
            }
            Assert.Greater(cores, buildings * 0.08, "stair cores");
            Assert.Less(cores, buildings * 0.7, "but not everywhere");
        }

        [Test]
        public void ATallBuildingIsFittedOutToTheCapAndSealedAbove()
        {
            int sealedCount = 0;
            foreach (BuildingSite site in Buildings(2))
            {
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                float height = site.Params.Height;
                if (layout.SealedTop)
                {
                    sealedCount++;
                    Assert.AreEqual(BuildingInterior.MaxFloors, layout.FloorCount);
                    Assert.AreEqual(layout.FloorCount * layout.Storey, BuildingInterior.FloorTop(layout.FloorCount - 1, layout.FloorCount, height, layout.Storey), 0.001f);
                }
                else
                {
                    Assert.Less(layout.RiseOf(layout.FloorCount - 1, height), layout.Storey * 2.0f, "the top floor swallows less than a storey");
                }
            }
            Assert.Greater(sealedCount, 100, "tall buildings exist");
        }

        [Test]
        public void ACoreIsTheSameShaftOnEveryFloor()
        {
            int cores = 0;
            foreach (BuildingSite site in Buildings(5))
            {
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                if (!layout.Core)
                {
                    continue;
                }
                cores++;
                StairPlan first = null;
                foreach (StairPlan p in AllPlans(layout))
                {
                    Assert.IsTrue(p.Enclosed);
                    first = first ?? p;
                    Assert.AreEqual(first.Bounds, p.Bounds, "one shaft, floor after floor");
                }
            }
            Assert.Greater(cores, 20);
        }
    }
}
