using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The stairways through steep blocks (World/Plan/StairLanes; owner, 2026-10-16: "make more staircases throughout the city overall ... san
    /// francisco has a lot of randoms staircases that go up hills"): steep blocks get one straight across from the lot line on the low side to the
    /// lot line on the high side; its steps follow the ground and are steps a body climbs, with landings; the lots and buildings keep off it; and
    /// its treads are built where it says, with no ground or paving left over them.
    /// </summary>
    public sealed class StairLaneTests
    {
        private const int Seed = CitySeed.Default;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static double Paving(Vec2 p) => World.Plan.GroundHeight(p.X, p.Y) + PlanBuildings.PavingRise;

        private static StairLane NearestToSpawn(out double distance)
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            StairLane lane = null;
            distance = double.MaxValue;
            foreach (StairLane l in World.Plan.Data.StairLanes)
            {
                double d = ((l.A + l.B) * 0.5 - new Vec2(sx, sz)).Length;
                if (d < distance)
                {
                    distance = d;
                    lane = l;
                }
            }
            return lane;
        }

        [Test]
        public void SteepBlocksGetStairwaysThatClimbStraightAcrossThemInSteps()
        {
            List<StairLane> lanes = World.Plan.Data.StairLanes;
            double drops = 0.0, lengths = 0.0;
            int steps = 0, landings = 0;
            foreach (StairLane lane in lanes)
            {
                PlanBlock block = World.Plan.Data.Blocks[lane.Block];
                string what = $"stairway {lane.Id} (block {lane.Block}, {lane.Length:F0} m, {lane.Drop:F1} m up)";
                Assert.AreEqual(lane.Id, block.StairLane, $"{what}: its block does not know it");
                Assert.Less(RingDistance(block.Outer, lane.A), 0.05, $"{what}: its low end is not on the lot line");
                Assert.Less(RingDistance(block.Outer, lane.B), 0.05, $"{what}: its high end is not on the lot line");
                for (double s = 0.5; s < lane.Length - 0.5; s += 2.0)
                {
                    foreach (double across in new[] { -lane.Width * 0.5, lane.Width * 0.5 })
                    {
                        Assert.IsTrue(PlanMath.PointInPolygon(lane.A + lane.Dir * s + lane.Dir.Perp * across, block.Outer), $"{what}: its side leaves its block {s:F1} m along");
                    }
                }
                Assert.That(Paving(lane.B) - Paving(lane.A), Is.GreaterThanOrEqualTo(StairLanes.MinDrop - 1e-6), $"{what}: it does not climb enough");
                Assert.That(lane.Drop / lane.Length, Is.InRange(StairLanes.MinGrade - 1e-9, StairLanes.MaxGrade + 1e-9), $"{what}: its grade");
                List<StairTread> treads = StairLanes.Profile(lane, World.Plan.Fields);
                Assert.AreEqual(0.0, treads[0].S0, 1e-9);
                Assert.AreEqual(lane.Length, treads[treads.Count - 1].S1, 1e-6, $"{what}: the treads do not reach its end");
                Assert.AreEqual(Paving(lane.A), treads[0].Top, 1e-6, $"{what}: it does not start on the low pavement");
                Assert.AreEqual(Paving(lane.B), treads[treads.Count - 1].Top, StairLanes.Riser * 0.75 + 1e-6, $"{what}: it does not end on the high pavement");
                int up = 0;
                for (int i = 1; i < treads.Count; i++)
                {
                    Assert.AreEqual(treads[i - 1].S1, treads[i].S0, 1e-9, $"{what}: a gap between treads {i - 1} and {i}");
                    Assert.That(treads[i].S1 - treads[i].S0, Is.GreaterThan(0.0), $"{what}: tread {i} has no going");
                    double rise = treads[i].Top - treads[i - 1].Top;
                    Assert.That(System.Math.Abs(rise), Is.LessThanOrEqualTo(StairLanes.Riser + 1e-9), $"{what}: a step of {rise:F2} m");
                    Vec2 under = lane.A + lane.Dir * ((treads[i].S0 + treads[i].S1) * 0.5);
                    Assert.That(System.Math.Abs(Paving(under) - treads[i].Top), Is.LessThanOrEqualTo(StairLanes.MaxStray + 1e-6), $"{what}: tread {i} strays from the paving");
                    if (rise > 1e-9)
                    {
                        up++;
                        steps++;
                        Assert.That(up, Is.LessThanOrEqualTo(StairLanes.FlightMax), $"{what}: more than a flight's steps with no landing");
                    }
                    else
                    {
                        if (treads[i].S1 - treads[i].S0 >= StairLanes.Landing - 1e-6) landings++;
                        up = 0;
                    }
                }
                drops += lane.Drop;
                lengths += lane.Length;
            }
            Debug.Log($"CityX stairways: {lanes.Count} in the region, {steps} steps up in all, {landings} landings, mean {lengths / Mathf.Max(1, lanes.Count):F0} m long and " +
                $"{drops / Mathf.Max(1, lanes.Count):F1} m up");
            Assert.That(lanes.Count, Is.GreaterThan(20), "too few stairways");
            Assert.That(landings, Is.GreaterThan(0), "no stairway has a landing");
        }

        [Test]
        public void NoLotOrBuildingStandsOnAStairway()
        {
            var corridors = new Dictionary<int, Vec2[]>();
            foreach (StairLane lane in World.Plan.Data.StairLanes) corridors[lane.Block] = StairLanes.Corridor(lane, StairLanes.LotClear - 0.05);
            int lots = 0;
            foreach (PlanLot lot in World.Plan.Data.Lots)
            {
                if (!corridors.TryGetValue(lot.BlockIndex, out Vec2[] corridor)) continue;
                lots++;
                Assert.IsFalse(PlanMath.PolygonsIntersect(lot.Lot, corridor), $"lot {lot.Id} reaches into its block's stairway");
            }
            int points = 0;
            foreach (StairLane lane in World.Plan.Data.StairLanes)
            {
                for (double s = 0.3; s < lane.Length - 0.3; s += 0.5)
                {
                    foreach (double across in new[] { -lane.Width * 0.5 + 0.1, 0.0, lane.Width * 0.5 - 0.1 })
                    {
                        Vec2 p = lane.A + lane.Dir * s + lane.Dir.Perp * across;
                        int b = World.Plan.BuildingAt(p.X, p.Y);
                        Assert.AreEqual(-1, b, $"stairway {lane.Id}: building {b} stands on it {s:F1} m along");
                        points++;
                    }
                }
            }
            Debug.Log($"CityX stairways: {lots} lots beside them kept clear, {points} points along them clear of every building");
            Assert.Greater(lots, 20);
            Assert.Greater(points, 1000);
        }

        [Test]
        public void AStairwaysTreadsAreBuiltAndNoGroundStandsOverThem()
        {
            // The stairway nearest the spawn: across it every metre, a tread under each point at its top, and nothing of the ground over it.
            StairLane lane = NearestToSpawn(out double best);
            Assert.IsNotNull(lane, "no stairway");
            List<StairTread> treads = StairLanes.Profile(lane, World.Plan.Fields);
            var built = new Dictionary<(int, int), (MeshBuffer ground, MeshBuffer pavement, Vector3 origin)>();
            int checkedPoints = 0;
            float riser = (float)StairLanes.Riser;
            for (double s = 0.5; s < lane.Length - 0.5; s += 1.0)
            {
                foreach (double across in new[] { -1.2, 1.2 })   // either side of the handrail down the middle, clear of the planters
                {
                    Vec2 p = lane.A + lane.Dir * s + lane.Dir.Perp * across;
                    float top = (float)CitySurfaceBuilder.TreadTopAt(treads, s);
                    // At a riser the tread either side may be the one under the point.
                    Assert.IsTrue(SurfaceNear(World, built, p, top - riser - 0.03f, top + riser + 0.03f, false), $"stairway {lane.Id}: no tread {s:F1} m along at {top:F2}");
                    Assert.IsFalse(SurfaceNear(World, built, p, top + riser + 0.05f, top + 6.0f, true), $"stairway {lane.Id}: something of the ground stands over its tread {s:F1} m along");
                    checkedPoints++;
                }
            }
            Debug.Log($"CityX stairway {lane.Id}: {lane.Length:F0} m, {lane.Drop:F1} m up, {best:F0} m from the spawn: {checkedPoints} points checked");
            Assert.Greater(checkedPoints, 20);
        }

        /// <summary>
        /// Whether a surface of the city's ground (the paving buffer, with the terrain and every tread; and the roads' with
        /// <paramref name="roads"/>) lies over a point between two heights, in the chunk the point is in or one beside it: a tread is built by the
        /// chunk its middle is in, which need not be the point's. Chunks are built once into <paramref name="built"/>.
        /// </summary>
        internal static bool SurfaceNear(CityWorld world, Dictionary<(int, int), (MeshBuffer ground, MeshBuffer pavement, Vector3 origin)> built, Vec2 p,
            float yLow, float yHigh, bool roads)
        {
            int px = PlanWorld.ChunkOf(p.X), py = PlanWorld.ChunkOf(p.Y);
            for (int cy = py - 1; cy <= py + 1; cy++)
            {
                for (int cx = px - 1; cx <= px + 1; cx++)
                {
                    if (!built.TryGetValue((cx, cy), out var c))
                    {
                        var origin = new Vector3(cx * (float)PlanWorld.ChunkSize, 0, cy * (float)PlanWorld.ChunkSize);
                        var ground = new MeshBuffer();
                        var pavement = new MeshBuffer();
                        CitySurfaceBuilder.Build(world, cx, cy, origin, ground, pavement, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), false);
                        c = (ground, pavement, origin);
                        built[(cx, cy)] = c;
                    }
                    if (Covers(c.pavement, c.origin, p.X, p.Y, yLow, yHigh) || (roads && Covers(c.ground, c.origin, p.X, p.Y, yLow, yHigh))) return true;
                }
            }
            return false;
        }

        private static double RingDistance(List<Vec2> ring, Vec2 p)
        {
            double best = double.MaxValue;
            for (int i = 0; i < ring.Count; i++)
            {
                best = System.Math.Min(best, PlanMath.DistanceToSegment(p, ring[i], ring[(i + 1) % ring.Count], out _));
            }
            return best;
        }

        /// <summary>Whether an upward-facing triangle of a buffer lies over a point (in plan) between two heights.</summary>
        internal static bool Covers(MeshBuffer buffer, Vector3 origin, double wx, double wz, float yLow, float yHigh)
        {
            float x = (float)(wx - origin.x), z = (float)(wz - origin.z);
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            for (int k = 0; k + 2 < t.Count; k += 3)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                if (Vector3.Cross(b - a, c - a).y <= 1e-6f) continue;
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float wa = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                float wb = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                if (wa < -1e-4f || wb < -1e-4f || wa + wb > 1.0001f) continue;
                float y = wa * a.y + wb * b.y + (1.0f - wa - wb) * c.y + origin.y;
                if (y >= yLow && y <= yHigh) return true;
            }
            return false;
        }
    }
}
