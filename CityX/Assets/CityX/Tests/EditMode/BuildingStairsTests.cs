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
        private const int Seed = 83920571;
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
                if (site.Shell)
                {
                    continue;   // sealed sculpture: built without stairs (BuildingShapeTests)
                }
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                // Covered by a sealed block on it, or a pitched roof nobody stands on (RoofShape).
                Assert.AreEqual((site.RoofSealed || RoofShape.For(site).Pitched) && !layout.SealedTop, layout.RoofCovered, $"#{site.Id}");
                for (int f = 0; f < layout.FloorCount; f++)
                {
                    if (f == layout.FloorCount - 1 && layout.NoRoofStair)
                    {
                        Assert.IsNull(layout.Floors[f], "a sealed ceiling, or a roof under a sealed block, has nothing above it to climb to");
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

        /// <summary>
        /// THE PLAYER FITS: at every step, a body (2.6 m tall, a capsule half a metre wide) has headroom under the slab above, under the
        /// stair of the next floor, and a little to spare for the step it is about to climb. "My head is hitting the roof in this stairwell
        /// and it doesn.t let me go up."
        /// </summary>
        private static float WorstHeadroom(StairPlan p, StairPlan next, out string where)
        {
            const float radius = 0.5f;
            float worst = float.MaxValue;
            where = "";
            for (int i = 0; i < p.Treads.Length; i++)
            {
                Rect body = Rect.MinMaxRect(p.Treads[i].center.x - radius, p.Treads[i].center.y - radius, p.Treads[i].center.x + radius, p.Treads[i].center.y + radius);
                float stand = p.Tops[i];
                for (int j = 0; j < p.Treads.Length; j++)
                {
                    if (Hits(body, p.Treads[j]))
                    {
                        stand = Mathf.Max(stand, p.Tops[j]);
                    }
                }
                float ceiling = float.MaxValue;
                bool insideHole = body.xMin >= p.Hole.xMin - 0.01f && body.xMax <= p.Hole.xMax + 0.01f && body.yMin >= p.Hole.yMin - 0.01f && body.yMax <= p.Hole.yMax + 0.01f;
                if (!insideHole && stand < p.Rise - 1.0f)   // the last metre is the step up onto the floor itself
                {
                    ceiling = p.Rise - BuildingInterior.SlabThickness;
                }
                if (next != null)
                {
                    for (int j = 0; j < next.Treads.Length; j++)
                    {
                        if (Hits(body, next.Treads[j]))
                        {
                            ceiling = Mathf.Min(ceiling, p.Rise + Mathf.Max(0.0f, next.Tops[j] - BuildingStairs.Soffit));
                        }
                    }
                }
                float room = ceiling - stand;
                if (room < worst)
                {
                    worst = room;
                    where = $"{p.Kind} x{p.Flights} step {i} of {p.Treads.Length}, rise {p.Rise:0.0}, ceiling {ceiling:0.00}, standing {stand:0.00}";
                }
            }
            return worst;
        }

        [Test]
        public void ABodyAlwaysFitsOnEveryStairWithRoomForTheNextStep()
        {
            int checkedStairs = 0, tight = 0;
            string first = "";
            foreach (BuildingSite site in Buildings(3))
            {
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                for (int f = -1; f < layout.FloorCount; f++)
                {
                    StairPlan p = f < 0 ? layout.Basement : layout.Floors[f];
                    if (p == null)
                    {
                        continue;
                    }
                    StairPlan next = f + 1 < layout.FloorCount ? layout.Floors[f + 1] : null;
                    float room = WorstHeadroom(p, next, out string where);
                    checkedStairs++;
                    if (room < 3.0f)
                    {
                        tight++;
                        if (first.Length == 0)
                        {
                            first = $"building {site.Id} floor {f} core {layout.Core}: {room:0.00} m at {where}";
                        }
                    }
                }
            }
            Assert.Greater(checkedStairs, 3000);
            Assert.AreEqual(0, tight, $"{tight} of {checkedStairs} stairs squeeze a body; first: {first}");
        }

        // ---- the way through ----

        [Test]
        public void YouCanWalkFromWhereTheLastStairLeavesYouToTheNextOneAndFromTheFrontDoor()
        {
            int floorsChecked = 0, blocked = 0;
            var firsts = new List<string>();
            foreach (BuildingSite site in Buildings(7))
            {
                // The room the stairs are planned in: the building, or for a round one the square in its middle (BuildingStairs.PlanFor), whose front
                // edge the lobby reaches from the door across open floor.
                float halfW = site.Params.Width * 0.5f, halfD = site.Params.Depth * 0.5f;
                if (site.Sides >= 3)
                {
                    halfW = halfD = RoundFootprint.Of(site).InnerHalf + BuildingInterior.WallThickness + 0.05f;
                }
                float hx = halfW - BuildingInterior.WallThickness - 0.05f, hz = halfD - BuildingInterior.WallThickness - 0.05f;
                if (hx > 60.0f || hz > 60.0f)
                {
                    continue;   // a grid for a 200 m room is a lot of cells; the small and middling ones carry the same cases
                }
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                for (int f = 0; f < layout.FloorCount; f++)
                {
                    StairPlan plan = layout.Floors[f];
                    if (plan == null)
                    {
                        continue;
                    }
                    StairPlan arriving = f == 0 ? layout.Basement : layout.Floors[f - 1];
                    var starts = new List<Vector2>();
                    if (f == 0 || arriving == null)
                    {
                        starts.Add(new Vector2(0.0f, hz - 1.0f));   // in at the front door
                    }
                    if (arriving != null)
                    {
                        starts.Add(arriving.ExitPoint);   // off the last stair
                    }
                    List<Rect> solids = BuildingStairs.FloorSolids(plan, arriving, f, halfW, halfD);
                    floorsChecked++;
                    Vector2 stuckFrom = default;
                    bool stuck = false;
                    foreach (Vector2 start in starts)
                    {
                        if (!stuck && BuildingStairs.WalkPath(solids, hx, hz, start, plan.EntryPoint) == null)
                        {
                            stuck = true;
                            stuckFrom = start;
                        }
                    }
                    if (stuck)
                    {
                        blocked++;
                        if (firsts.Count < 6)
                        {
                            firsts.Add($"building {site.Id} floor {f}: {plan.Kind} x{plan.Flights}, core {layout.Core}, arriving {(arriving == null ? "from the door" : arriving.Kind.ToString())}, from {stuckFrom} to {plan.EntryPoint}, room {hx * 2:0}x{hz * 2:0}");
                        }
                    }
                }
            }
            TestContext.WriteLine($"{blocked} of {floorsChecked} floors have no walkable way to the stair");
            foreach (string line in firsts)
            {
                TestContext.WriteLine(line);
            }
            Assert.Greater(floorsChecked, 1000);
            Assert.AreEqual(0, blocked, $"{blocked} of {floorsChecked} floors: you cannot walk to the stair. First: {(firsts.Count > 0 ? firsts[0] : "")}");
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
