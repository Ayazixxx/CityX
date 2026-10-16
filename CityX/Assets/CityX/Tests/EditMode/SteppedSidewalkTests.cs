using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sidewalks that are steps (World/SteppedSidewalks; owner, 2026-10-16: "make more staircases throughout the city overall ... study how
    /// [San Francisco does] it"): every sidewalk whose kerb climbs 14 % or more is steps, and no other; its treads follow the slope, a riser
    /// apart, each within half a riser of the paving it replaces; nothing with a surface of its own crosses one; and its treads are built, with no
    /// paving left over them.
    /// </summary>
    public sealed class SteppedSidewalkTests
    {
        private const int Seed = 45678679;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static double Paving(Vec2 p) => World.Plan.GroundHeight(p.X, p.Y) + PlanBuildings.PavingRise;

        private static IEnumerable<(int block, int edge)> All()
        {
            List<PlanBlock> blocks = World.Plan.Data.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                if (blocks[b].Inner == null || blocks[b].Outer == null) continue;
                for (int e = 0; e < blocks[b].Inner.Count; e++)
                {
                    if (SteppedSidewalks.Is(World.Plan, b, e)) yield return (b, e);
                }
            }
        }

        [Test]
        public void SteepSidewalksAreStepsThatFollowTheSlope()
        {
            int count = 0, treadsAll = 0;
            double steepest = 0.0;
            foreach ((int b, int e) in All())
            {
                PlanBlock block = World.Plan.Data.Blocks[b];
                int f = (e + 1) % block.Inner.Count;
                Vec2 i0 = block.Inner[e], i1 = block.Inner[f], o0 = block.Outer[e], o1 = block.Outer[f];
                double length = (i1 - i0).Length;
                double grade = System.Math.Abs(World.Plan.GroundHeight(i1.X, i1.Y) - World.Plan.GroundHeight(i0.X, i0.Y)) / length;
                string what = $"block {b} edge {e} ({length:F0} m at {grade:P0})";
                Assert.That(grade, Is.GreaterThanOrEqualTo(SteppedSidewalks.Grade), $"{what}: steps on a gentle sidewalk");
                Assert.That(length, Is.GreaterThanOrEqualTo(SteppedSidewalks.MinLength), $"{what}: steps on a short sidewalk");
                Assert.IsFalse(PedestrianStreets.Along(World.Plan, block.Poly[e], block.Poly[f]), $"{what}: steps along a pedestrian street");
                List<(double t0, double t1, double top)> treads = SteppedSidewalks.Treads(World.Plan, block, e);
                Assert.AreEqual(0.0, treads[0].t0, 1e-9);
                Assert.AreEqual(1.0, treads[treads.Count - 1].t1, 1e-9);
                for (int i = 0; i < treads.Count; i++)
                {
                    (double t0, double t1, double top) = treads[i];
                    Assert.That(t1, Is.GreaterThan(t0), $"{what}: tread {i} has no going");
                    if (i > 0)
                    {
                        Assert.AreEqual(treads[i - 1].t1, t0, 1e-12, $"{what}: a gap before tread {i}");
                        Assert.AreEqual(SteppedSidewalks.Riser, System.Math.Abs(top - treads[i - 1].top), 1e-9, $"{what}: tread {i} is not a riser from the last");
                    }
                    double tm = (t0 + t1) * 0.5;
                    Vec2 middle = (i0 + (i1 - i0) * tm + o0 + (o1 - o0) * tm) * 0.5;
                    Assert.AreEqual(Paving(middle), top, SteppedSidewalks.Riser * 0.5 + 0.03, $"{what}: tread {i} strays from the paving");
                }
                count++;
                treadsAll += treads.Count;
                steepest = System.Math.Max(steepest, grade);
            }
            Debug.Log($"CityX stepped sidewalks: {count} in the region, {treadsAll} treads, the steepest {steepest:P0}");
            Assert.That(count, Is.GreaterThan(50), "too few stepped sidewalks");
        }

        [Test]
        public void EverySteepSidewalkIsStepsUnlessADriveOrARampCrossesIt()
        {
            int steep = 0, crossed = 0;
            var crossing = new List<Vec2[]>();
            foreach (BuildingSite site in World.Plan.Buildings)
            {
                if (!site.IsParkingGarage || !site.Exists) continue;
                foreach (GarageEntrance entrance in GarageEntrances.For(World.Plan, site))
                {
                    if (entrance.HasDriveway) crossing.Add(GarageEntrances.Driveway(site, entrance, 0.5f));
                }
            }
            foreach (KerbCut cut in World.Plan.Data.Expressways.KerbCuts) crossing.Add(cut.Poly);
            List<PlanBlock> blocks = World.Plan.Data.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                PlanBlock block = blocks[b];
                if (block.Inner == null || block.Outer == null) continue;
                for (int e = 0; e < block.Inner.Count; e++)
                {
                    int f = (e + 1) % block.Inner.Count;
                    Vec2 i0 = block.Inner[e], i1 = block.Inner[f];
                    double length = (i1 - i0).Length;
                    bool steepEnough = length >= SteppedSidewalks.MinLength && (block.Outer[f] - block.Outer[e]).Length >= 1.0
                        && System.Math.Abs(World.Plan.GroundHeight(i1.X, i1.Y) - World.Plan.GroundHeight(i0.X, i0.Y)) / length >= SteppedSidewalks.Grade
                        && !PedestrianStreets.Along(World.Plan, block.Poly[e], block.Poly[f]);
                    if (!steepEnough)
                    {
                        Assert.IsFalse(SteppedSidewalks.Is(World.Plan, b, e), $"block {b} edge {e}: steps where the sidewalk is not steep");
                        continue;
                    }
                    steep++;
                    Vec2[] strip = SteppedSidewalks.Strip(block, e);
                    bool isCrossed = false;
                    foreach (Vec2[] poly in crossing)
                    {
                        if (PlanMath.PolygonsIntersect(strip, poly)) isCrossed = true;
                    }
                    if (isCrossed) crossed++;
                    Assert.AreEqual(!isCrossed, SteppedSidewalks.Is(World.Plan, b, e),
                        $"block {b} edge {e}: {(isCrossed ? "steps where a drive or a ramp crosses" : "a steep sidewalk left flat")}");
                }
            }
            Debug.Log($"CityX stepped sidewalks: {steep} steep sidewalks, {crossed} of them crossed by a garage's drive or a ramp's kerb cut and left as they were");
            Assert.Greater(steep, 50);
        }

        [Test]
        public void AStepIsBuiltUnderEveryPointAndNoPavingStandsOverIt()
        {
            // The stepped sidewalk nearest the spawn: down its middle every half metre, a tread at its top, and no paving over it.
            World.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            (int block, int edge) best = (-1, -1);
            double bestDistance = double.MaxValue;
            foreach ((int b, int e) in All())
            {
                PlanBlock block = World.Plan.Data.Blocks[b];
                Vec2 mid = (block.Inner[e] + block.Inner[(e + 1) % block.Inner.Count]) * 0.5;
                double d = (mid - spawn).Length;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = (b, e);
                }
            }
            Assert.GreaterOrEqual(best.block, 0, "no stepped sidewalk");
            PlanBlock sidewalk = World.Plan.Data.Blocks[best.block];
            int n = sidewalk.Inner.Count, edge = best.edge, next = (edge + 1) % n;
            Vec2 i0 = sidewalk.Inner[edge], i1 = sidewalk.Inner[next], o0 = sidewalk.Outer[edge], o1 = sidewalk.Outer[next];
            List<(double t0, double t1, double top)> treads = SteppedSidewalks.Treads(World.Plan, sidewalk, edge);
            double length = (i1 - i0).Length;
            var built = new Dictionary<(int, int), (MeshBuffer ground, MeshBuffer pavement, Vector3 origin)>();
            int points = 0;
            foreach ((double t0, double t1, double top) in treads)
            {
                for (double t = t0 + 0.12 / length; t < t1 - 0.12 / length; t += 0.5 / length)
                {
                    Vec2 p = (i0 + (i1 - i0) * t + o0 + (o1 - o0) * t) * 0.5;
                    Assert.IsTrue(StairLaneTests.SurfaceNear(World, built, p, (float)top - 0.02f, (float)top + 0.02f, false),
                        $"block {best.block} edge {edge}: no tread at {top:F2} {t * length:F1} m along");
                    Assert.IsFalse(StairLaneTests.SurfaceNear(World, built, p, (float)top + 0.04f, (float)top + 3.0f, false),
                        $"block {best.block} edge {edge}: something stands over the tread {t * length:F1} m along");
                    points++;
                }
            }
            Debug.Log($"CityX stepped sidewalk: block {best.block} edge {edge}, {length:F0} m, {bestDistance:F0} m from the spawn, {treads.Count} treads, {points} points checked");
            Assert.Greater(points, 10);
        }
    }
}
