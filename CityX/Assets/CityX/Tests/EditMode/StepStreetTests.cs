using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The step streets (World/StepStreets; owner, 2026-10-16: "san francisco has a lot of randoms staircases that go up hills"): every ordinary
    /// street too steep to drive (21 % or more junction to junction) is steps from plate to plate unless an expressway ramp lands on it, and no
    /// other road is; its treads follow the road a riser apart with landings; it is built as steps with bollards across both ends and no
    /// asphalt; and no garage or car park opens onto one.
    /// </summary>
    public sealed class StepStreetTests
    {
        private const int Seed = 45678679;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static IEnumerable<EdgeGeometry> StepStreetsOf(PlanWorld plan)
        {
            foreach (EdgeGeometry g in plan.Geometry.Edges)
            {
                if (g != null && StepStreets.Is(plan, g.Edge)) yield return g;
            }
        }

        [Test]
        public void EverySteepStreetIsStepsAndNoOtherRoadIs()
        {
            int steps = 0, steepRoads = 0;
            foreach (RoadEdge edge in World.Plan.Data.Edges)
            {
                if (!edge.Alive) continue;
                bool steep = StepStreets.Steep(World.Plan.Fields, edge.A.P, edge.B.P, edge.Class) && !edge.Bridge;
                bool isSteps = StepStreets.Is(World.Plan, edge);
                if (isSteps)
                {
                    Assert.IsTrue(steep, $"road {edge.Id} ({edge.Class}) is steps but not a steep street");
                    Assert.IsFalse(PedestrianStreets.Is(World.Plan, edge), $"road {edge.Id} is both steps and a pedestrian street");
                    steps++;
                }
                else if (steep)
                {
                    steepRoads++;   // an expressway ramp or exit lands on it, or it is steeper than any flight climbs
                }
            }
            Debug.Log($"CityX step streets: {steps} in the region, {steepRoads} steep streets kept as roads (a ramp lands on them, or no flight climbs them)");
            Assert.That(steps, Is.GreaterThan(10), "too few step streets");
        }

        [Test]
        public void AStepStreetsTreadsGoFromPlateToPlateInSteps()
        {
            int streets = 0, stepsUp = 0;
            foreach (EdgeGeometry g in StepStreetsOf(World.Plan))
            {
                List<StairTread> treads = StepStreets.Profile(World.Plan, g, out bool fromB);
                double t0 = g.TrimA, t1 = g.Length - g.TrimB, length = t1 - t0;
                Vec2 low = g.A + g.Dir * (fromB ? t1 : t0), high = g.A + g.Dir * (fromB ? t0 : t1);
                string what = $"step street {g.Edge.Id} ({length:F0} m)";
                Assert.AreEqual(0.0, treads[0].S0, 1e-9);
                Assert.AreEqual(length, treads[treads.Count - 1].S1, 1e-6, $"{what}: the treads stop short");
                Assert.AreEqual(World.Plan.GroundHeight(low.X, low.Y) + PlanGeometry.RoadLift, treads[0].Top, 1e-6, $"{what}: it does not start on the road");
                Assert.AreEqual(World.Plan.GroundHeight(high.X, high.Y) + PlanGeometry.RoadLift, treads[treads.Count - 1].Top, StairLanes.Riser * 2.0,
                    $"{what}: it does not end at the road's height at its high end");
                int up = 0;
                for (int i = 1; i < treads.Count; i++)
                {
                    Assert.AreEqual(treads[i - 1].S1, treads[i].S0, 1e-9, $"{what}: a gap before tread {i}");
                    double rise = treads[i].Top - treads[i - 1].Top;
                    Assert.That(System.Math.Abs(rise), Is.LessThanOrEqualTo(StairLanes.Riser + 1e-9), $"{what}: a step of {rise:F2} m");
                    Vec2 under = low + (high - low).Normalized * ((treads[i].S0 + treads[i].S1) * 0.5);
                    double road = World.Plan.GroundHeight(under.X, under.Y) + PlanGeometry.RoadLift;
                    Assert.That(System.Math.Abs(road - treads[i].Top), Is.LessThanOrEqualTo(StepStreets.MaxStray + 1e-6), $"{what}: tread {i} strays from the road");
                    if (rise > 1e-9)
                    {
                        up++;
                        stepsUp++;
                        Assert.That(up, Is.LessThanOrEqualTo(StairLanes.FlightMax), $"{what}: a flight with no landing");
                    }
                    else
                    {
                        up = 0;
                    }
                }
                streets++;
            }
            Debug.Log($"CityX step streets: {streets} profiled, {stepsUp} steps up in all");
            Assert.Greater(streets, 10);
        }

        [Test]
        public void AStepStreetIsBuiltAsStepsWithBollardsAndNoAsphalt()
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            EdgeGeometry street = null;
            double best = double.MaxValue;
            foreach (EdgeGeometry g in StepStreetsOf(World.Plan))
            {
                double d = ((g.A + g.B) * 0.5 - spawn).Length;
                if (d < best)
                {
                    best = d;
                    street = g;
                }
            }
            Assert.IsNotNull(street, "no step street");
            List<StairTread> treads = StepStreets.Profile(World.Plan, street, out bool fromB);
            double t0 = street.TrimA, t1 = street.Length - street.TrimB, length = t1 - t0;
            Vec2 dir = fromB ? -street.Dir : street.Dir, low = street.A + street.Dir * (fromB ? t1 : t0);
            var built = new Dictionary<(int, int), (MeshBuffer ground, MeshBuffer pavement, MeshBuffer structures, Vector3 origin)>();
            (MeshBuffer ground, MeshBuffer pavement, MeshBuffer structures, Vector3 origin) Chunk(int cx, int cy)
            {
                if (!built.TryGetValue((cx, cy), out var c))
                {
                    var origin = new Vector3(cx * (float)PlanWorld.ChunkSize, 0, cy * (float)PlanWorld.ChunkSize);
                    c = (new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), origin);
                    CitySurfaceBuilder.Build(World, cx, cy, origin, c.ground, c.pavement, c.structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), false);
                    built[(cx, cy)] = c;
                }
                return c;
            }
            bool Near(Vec2 p, float y0, float y1, int which)
            {
                int px = PlanWorld.ChunkOf(p.X), py = PlanWorld.ChunkOf(p.Y);
                for (int cy = py - 1; cy <= py + 1; cy++)
                {
                    for (int cx = px - 1; cx <= px + 1; cx++)
                    {
                        var c = Chunk(cx, cy);
                        MeshBuffer buffer = which == 0 ? c.ground : which == 1 ? c.pavement : c.structures;
                        if (StairLaneTests.Covers(buffer, c.origin, p.X, p.Y, y0, y1)) return true;
                    }
                }
                return false;
            }
            float riser = (float)StairLanes.Riser;
            int points = 0;
            for (double s = StepStreets.EndPad + 0.5; s < length - StepStreets.EndPad - 0.5; s += 1.0)
            {
                foreach (double across in new[] { -street.HalfWidth + 0.6, street.HalfWidth - 0.6 })
                {
                    Vec2 p = low + dir * s + dir.Perp * across;
                    float top = (float)CitySurfaceBuilder.TreadTopAt(treads, s);
                    Assert.IsTrue(Near(p, top - riser - 0.03f, top + riser + 0.03f, 1), $"step street {street.Edge.Id}: no tread {s:F1} m along at {top:F2}");
                    double road = World.Plan.GroundHeight(p.X, p.Y) + PlanGeometry.RoadLift;
                    Assert.IsFalse(Near(p, (float)road - 0.02f, (float)road + 0.02f, 0) && System.Math.Abs(road - top) > 0.05,
                        $"step street {street.Edge.Id}: asphalt left on it {s:F1} m along");
                    points++;
                }
            }
            // The bollards: something standing over each end's pad between knee and waist height, in the street's middle third.
            int bollards = 0;
            foreach (double s in new[] { StepStreets.BollardInset, length - StepStreets.BollardInset })
            {
                float top = (float)CitySurfaceBuilder.TreadTopAt(treads, s);
                for (double w = -street.HalfWidth + StepStreets.BollardSpacing * 0.5; w <= street.HalfWidth - StepStreets.BollardSpacing * 0.5 + 1e-6; w += StepStreets.BollardSpacing)
                {
                    if (Near(low + dir * s + dir.Perp * w, top + 0.4f, top + 1.6f, 2)) bollards++;
                }
            }
            Debug.Log($"CityX step street {street.Edge.Id}: {length:F0} m, {treads[treads.Count - 1].Top - treads[0].Top:F1} m up, {best:F0} m from the spawn: " +
                $"{points} points checked, {bollards} bollards");
            Assert.Greater(points, 10);
            Assert.That(bollards, Is.GreaterThanOrEqualTo(4), "no bollards across the ends");
        }

        [Test]
        public void NoGarageOrCarParkOpensOntoAStepStreet()
        {
            int parking = 0;
            foreach (BuildingSite site in World.Plan.Buildings)
            {
                if (!site.Exists || (!site.IsParkingGarage && !site.IsOpenGround)) continue;
                parking++;
                // The road in front of its front wall.
                site.LocalToWorld(0.0, site.Params.Depth * 0.5 + 4.0, out double fx, out double fz);
                var front = new Vec2(fx, fz);
                RoadEdge nearest = null;
                double best = double.MaxValue;
                for (int cy = PlanWorld.ChunkOf(fz) - 2; cy <= PlanWorld.ChunkOf(fz) + 2; cy++)
                {
                    for (int cx = PlanWorld.ChunkOf(fx) - 2; cx <= PlanWorld.ChunkOf(fx) + 2; cx++)
                    {
                        foreach (int id in World.Plan.EdgesIn(cx, cy))
                        {
                            RoadEdge e = World.Plan.Data.Edges[id];
                            double d = PlanMath.DistanceToSegment(front, e.A.P, e.B.P, out _);
                            if (d < best)
                            {
                                best = d;
                                nearest = e;
                            }
                        }
                    }
                }
                if (nearest == null) continue;
                Assert.IsFalse(StepStreets.Is(World.Plan, nearest), $"{(site.IsParkingGarage ? "garage" : "car park")} {site.Id} opens onto step street {nearest.Id}");
            }
            Assert.Greater(parking, 5);
        }
    }
}
