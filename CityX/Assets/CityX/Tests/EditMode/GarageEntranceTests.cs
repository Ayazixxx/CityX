using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A car park can be driven into (owner, 2026-10-16, with a screenshot of a garage: "there was physically no way for the car to drive into it,
    /// the road leads into a wall it doesnt turn into the garage"). Two faults: a dead-end road the blocks were traced without still ran on under
    /// the lots it was left out of, into the buildings (now retired, or the lots kept clear of it when a ramp lands on it); and a garage stood on
    /// a solid podium over the top of its lot's ground, up to 16 m over the street at its openings (now its lowest deck is at the street, with a
    /// short ramp where its other opening's paving is lower).
    /// </summary>
    public sealed class GarageEntranceTests
    {
        private const int Seed = 61256619;

        [Test]
        public void NoLiveRoadRunsUnderABuilding()
        {
            var world = new CityWorld(Seed);
            PlanWorld plan = world.Plan;
            int roads = 0, under = 0;
            var found = new List<string>();
            foreach (RoadEdge e in plan.Data.Edges)
            {
                if (!e.Alive) continue;
                EdgeGeometry g = plan.Geometry.Edges[e.Id];
                if (g == null) continue;
                roads++;
                for (double t = g.TrimA; t <= g.Length - g.TrimB; t += 1.0)
                {
                    foreach (double side in new[] { -0.8, 0.0, 0.8 })
                    {
                        Vec2 q = g.A + g.Dir * t + g.Dir.Perp * (g.HalfWidth * side);
                        int b = plan.BuildingAt(q.X, q.Y);
                        if (b < 0) continue;
                        under++;
                        if (found.Count < 8) found.Add($"road {e.Id} ({e.Class}, {g.Length:F0} m, {e.A.Edges.Count}/{e.B.Edges.Count} roads at its ends) under building {b} ({plan.Buildings[b].Params.Mass} '{plan.Buildings[b].Params.SpecialType}') at ({q.X:F1}, {q.Y:F1})");
                    }
                }
            }
            Debug.Log($"CityX roads under buildings: {roads} live roads, {under} points of their carriageways under a building");
            Assert.That(roads, Is.GreaterThan(1000));
            Assert.AreEqual(0, under, "a road runs under a building:\n" + string.Join("\n", found));
        }

        [Test]
        public void EveryGarageIsEnteredFromItsStreetAtTheStreetsLevel()
        {
            var world = new CityWorld(Seed);
            Assert.AreEqual(CitySurfaceBuilder.RoadLift, GarageEntrances.RoadLift, "the drives meet the road at its own height");
            int garages = 0, drives = 0, inside = 0, dug = 0, steep = 0;
            foreach (BuildingSite site in world.Plan.Buildings)
            {
                if (!site.Exists || !site.IsParkingGarage) continue;
                garages++;
                GarageEntrance[] two = GarageEntrances.For(world.Plan, site);
                Assert.AreEqual(2, two.Length);
                float deck = two[0].DeckY;
                Assert.AreEqual(deck, two[1].DeckY, "the two entrances meet one deck");
                Assert.LessOrEqual(deck, site.BaseHeight + 1e-4f, $"garage {site.Id}: its lowest deck stands over the plan's floor");
                if (deck < site.BaseHeight - 1.0f) dug++;
                float longest = Mathf.Max(GarageEntrances.ShortestRamp, site.Params.Depth - GarageEntrances.BackStrip);
                foreach (GarageEntrance e in two)
                {
                    if (e.HasRamp) inside++;
                    Assert.That(e.Length, Is.LessThanOrEqualTo(longest + 1e-3f), $"garage {site.Id}: the ramp runs on into the deck's own ramp");
                    Assert.That(e.SurfaceAt(Mathf.Max(e.RampEnd, 0.0f) + 0.01f), Is.EqualTo(deck).Within(1e-4f), $"garage {site.Id}: the drive does not reach the deck");
                    if (!e.HasDriveway) continue;
                    drives++;
                    // Out at the kerb: the road's own height there, and nothing of the carriageway between the kerb and the wall.
                    site.LocalToWorld((e.X0 + e.X1) * 0.5, site.Params.Depth * 0.5 + e.KerbOut, out double kx, out double kz);
                    float road = (float)(world.Plan.GroundHeight(kx, kz) + GarageEntrances.RoadLift);
                    Assert.That(e.SurfaceAt(-e.KerbOut), Is.EqualTo(road).Within(GarageEntrances.FlushRise + 1e-3f), $"garage {site.Id}: the drive does not meet the road at the kerb");
                    Assert.That(GarageEntrances.CarriagewayOut(world.Plan, site, (e.X0 + e.X1) * 0.5f), Is.EqualTo(e.KerbOut).Within(1e-4f));
                    // As gentle as a garage ramp (15 %) from the kerb to the deck, unless the garage is too shallow for it.
                    float run = e.RampEnd + e.KerbOut;
                    if (run > 1e-3f)
                    {
                        float grade = Mathf.Abs(deck - e.RoadY) / run;
                        if (e.RampEnd < longest - 1e-3f) Assert.That(grade, Is.LessThanOrEqualTo(GarageEntrances.Grade + 1e-3f), $"garage {site.Id}: a drive steeper than a garage ramp");
                        if (grade > 0.25f) steep++;
                    }
                }
            }
            Debug.Log($"CityX garages: {garages}, {drives} drives from the kerb, {inside} ramps running on inside, {dug} dug more than a metre into their hill, {steep} drives over 25 %");
            Assert.That(garages, Is.GreaterThan(10));
            Assert.That(drives, Is.GreaterThan(garages), "most entrances have a road in front to drive in from");
        }

        /// <summary>
        /// Where the road slopes along the frontage, a drive's mouth follows it and the drive untwists to level at the wall, which steepens it at a
        /// car's side: so it climbs that much less across the pavement (GarageEntrances.TwistAllowance) and the rest on the straight ramp inside,
        /// and no wheel of the car meets more than the grade (seed 19077151: a 12 % drive from a road rising 11 % across the opening was 16.5 %
        /// under the car's left wheels, and the car beached at its foot).
        /// </summary>
        [Test]
        public void NoDriveIsSteeperThanTheGradeAtACarsSide()
        {
            var world = new CityWorld(Seed);
            int drives = 0, eased = 0;
            float steepest = 0.0f;
            foreach (BuildingSite site in world.Plan.Buildings)
            {
                if (!site.Exists || !site.IsParkingGarage) continue;
                float front = site.Params.Depth * 0.5f, longest = Mathf.Max(GarageEntrances.ShortestRamp, site.Params.Depth - GarageEntrances.BackStrip);
                foreach (GarageEntrance e in GarageEntrances.For(world.Plan, site))
                {
                    if (!e.HasDriveway || Mathf.Abs(e.DeckY - e.RoadY) <= GarageEntrances.FlushRise) continue;
                    drives++;
                    site.LocalToWorld(e.X0, front + e.KerbOut, out double lx, out double lz);
                    site.LocalToWorld(e.X1, front + e.KerbOut, out double rx, out double rz);
                    float along = (float)((world.Plan.GroundHeight(rx, rz) - world.Plan.GroundHeight(lx, lz)) / (e.X1 - e.X0));
                    float twist = Mathf.Abs(along) * GarageEntrances.TwistAllowance / e.KerbOut;
                    // Across the pavement along the middle: to the wall, or to the deck where the drive reaches it out there.
                    float outside = e.RampEnd <= 0.0f ? Mathf.Abs(e.DeckY - e.RoadY) / (e.RampEnd + e.KerbOut) : Mathf.Abs(e.WallY - e.RoadY) / e.KerbOut;
                    if (twist > 1e-3f && outside < GarageEntrances.Grade - 1e-3f) eased++;
                    steepest = Mathf.Max(steepest, outside + twist);
                    string what = $"garage {site.Id}, the opening at {(e.X0 + e.X1) * 0.5f:F1}: {outside:P1} along the middle, the road {along:P1} along the frontage";
                    if (twist < GarageEntrances.Grade)
                    {
                        Assert.That(outside + twist, Is.LessThanOrEqualTo(GarageEntrances.Grade + 1e-3f), $"{what}: steeper than the grade at a car's side");
                    }
                    else
                    {
                        Assert.That(outside, Is.LessThanOrEqualTo(1e-3f), $"{what}: the road is too steep along the frontage for any climb out there");
                    }
                    if (e.HasRamp && e.RampEnd < longest - 1e-3f)
                    {
                        Assert.That(Mathf.Abs(e.DeckY - e.WallY) / e.RampEnd, Is.LessThanOrEqualTo(GarageEntrances.Grade + 1e-3f), $"{what}: the ramp inside is steeper than the grade");
                    }
                }
            }
            Debug.Log($"CityX garage drives at a car's side: {drives} climbing drives, {eased} eased across the pavement for a road sloping along the frontage; " +
                $"the steepest at a car's side {steepest:P1} (where the road allows)");
            Assert.That(drives, Is.GreaterThan(10));
        }

        [Test]
        public void TheGroundIsCutAwayUnderAGarageDugIntoItsHill()
        {
            var world = new CityWorld(Seed);
            var built = new Dictionary<(int, int), (MeshBuffer ground, MeshBuffer pavement, Vector3 origin)>();
            (MeshBuffer ground, MeshBuffer pavement, Vector3 origin) Chunk(int cx, int cy)
            {
                if (!built.TryGetValue((cx, cy), out var c))
                {
                    var origin = new Vector3(cx * CityLayout.ChunkSize, 0, cy * CityLayout.ChunkSize);
                    var ground = new MeshBuffer();
                    var pavement = new MeshBuffer();
                    CitySurfaceBuilder.Build(world, cx, cy, origin, ground, pavement, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), false);
                    c = (ground, pavement, origin);
                    built[(cx, cy)] = c;
                }
                return c;
            }

            // The garages near the spawn dug deepest into their hills.
            var dug = new List<BuildingSite>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                if (site.IsParkingGarage && GarageEntrances.For(world.Plan, site)[0].DeckY < site.BaseHeight - 0.5f) dug.Add(site);
                if (dug.Count >= 4) break;
            }
            Assert.That(dug.Count, Is.GreaterThan(0), "no garage near the spawn dug into its hill");
            foreach (BuildingSite site in dug)
            {
                float deck = GarageEntrances.For(world.Plan, site)[0].DeckY;
                double hw = site.Params.Width * 0.5 - 0.5, hd = site.Params.Depth * 0.5 - 0.5;
                int points = 0;
                for (int i = 0; i <= 6; i++)
                {
                    for (int j = 0; j <= 6; j++)
                    {
                        site.LocalToWorld(-hw + 2.0 * hw * i / 6.0, -hd + 2.0 * hd * j / 6.0, out double x, out double z);
                        var c = Chunk(PlanWorld.ChunkOf(x), PlanWorld.ChunkOf(z));
                        // Nothing of the ground over the lowest deck (it stood up through the decks) up to the plan's floor and a little over.
                        bool covered = Covers(c.ground, c.origin, x, z, deck + 0.05f, site.BaseHeight + 1.0f) || Covers(c.pavement, c.origin, x, z, deck + 0.05f, site.BaseHeight + 1.0f);
                        Assert.IsFalse(covered, $"garage {site.Id} ({site.Params.Width:F0}x{site.Params.Depth:F0}, deck {deck:F2}, plan floor {site.BaseHeight:F2}): the ground comes up through its lowest deck at ({x:F1}, {z:F1})");
                        points++;
                    }
                }
                // Across each drive: its own surface there, and nothing of the bank over it.
                int drivePoints = 0;
                foreach (GarageEntrance e in GarageEntrances.For(world.Plan, site))
                {
                    if (!e.HasDriveway) continue;
                    for (float s = -e.KerbOut + 0.3f; s <= -0.3f; s += 0.7f)
                    {
                        foreach (float x in new[] { e.X0 + 0.4f, (e.X0 + e.X1) * 0.5f, e.X1 - 0.4f })
                        {
                            site.LocalToWorld(x, site.Params.Depth * 0.5 - s, out double wx, out double wz);
                            var c = Chunk(PlanWorld.ChunkOf(wx), PlanWorld.ChunkOf(wz));
                            float drive = CitySurfaceBuilder.DriveHeight(world.Plan, site, e, x, s);   // as drawn: out at the road its mouth meets the road as drawn
                            Assert.IsTrue(Covers(c.pavement, c.origin, wx, wz, drive - 0.03f, drive + 0.03f), $"garage {site.Id}: no drive surface {-s:F1} m out in front of the opening at {x:F1}");
                            bool bank = Covers(c.ground, c.origin, wx, wz, drive + 0.05f, drive + 4.0f) || Covers(c.pavement, c.origin, wx, wz, drive + 0.05f, drive + 4.0f);
                            Assert.IsFalse(bank, $"garage {site.Id}: the bank stands over the drive {-s:F1} m out in front of the opening at {x:F1} (drive {drive:F2})");
                            drivePoints++;
                        }
                    }
                }
                Debug.Log($"CityX garage {site.Id}: lowest deck {deck:F2}, {site.BaseHeight - deck:F2} m under the plan's floor; {points} points clear inside, {drivePoints} along its drives");
            }
        }

        /// <summary>
        /// Where a drive meets the road there is no step, all across the opening (seed 19077151: on a street sloping along the frontage the mouth,
        /// level across, stood a step over the road at one side and the car stopped at it; and where a road's 5 m piece spans a crease in the hill,
        /// the road as drawn stood up to 0.8 m off a mouth that followed the ground). Every garage's drives, built: every half metre across the
        /// mouth, the road's surface just outside the kerb line against the drive's just inside it.
        /// </summary>
        [Test]
        public void EachDrivesMouthIsFlushWithTheRoadAcrossTheOpening()
        {
            var world = new CityWorld(Seed);
            var built = new Dictionary<(int, int), (MeshBuffer ground, MeshBuffer pavement, Vector3 origin)>();
            (MeshBuffer ground, MeshBuffer pavement, Vector3 origin) Chunk(double x, double z)
            {
                int cx = PlanWorld.ChunkOf(x), cy = PlanWorld.ChunkOf(z);
                if (!built.TryGetValue((cx, cy), out var c))
                {
                    var origin = new Vector3(cx * CityLayout.ChunkSize, 0, cy * CityLayout.ChunkSize);
                    var ground = new MeshBuffer();
                    var pavement = new MeshBuffer();
                    CitySurfaceBuilder.Build(world, cx, cy, origin, ground, pavement, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), false);
                    c = (ground, pavement, origin);
                    built[(cx, cy)] = c;
                }
                return c;
            }

            int drives = 0, points = 0;
            float lip = 0.0f, gap = 0.0f;   // the drive over the road (an edge the car's nose meets: a 2 cm step stops it), the road over the drive
            string lipAt = "none", gapAt = "none";
            foreach (BuildingSite site in world.Plan.Buildings)
            {
                if (!site.Exists || !site.IsParkingGarage) continue;
                float front = site.Params.Depth * 0.5f;
                foreach (GarageEntrance e in GarageEntrances.For(world.Plan, site))
                {
                    if (!e.HasDriveway) continue;
                    drives++;
                    for (float x = e.X0 + 0.25f; x <= e.X1 - 0.25f + 1e-3f; x += 0.5f)
                    {
                        site.LocalToWorld(x, front + e.KerbOut - 0.03, out double ix, out double iz);
                        site.LocalToWorld(x, front + e.KerbOut + 0.03, out double ox, out double oz);
                        var inside = Chunk(ix, iz);
                        var outside = Chunk(ox, oz);
                        // The drive (the pavement and the ground are cut away under it), and the road (the ground is cut away past the kerb line).
                        float drive = HighestAt(inside.pavement, inside.origin, ix, iz, e.RoadY - 1.5f, e.RoadY + 1.5f);
                        float road = HighestAt(outside.ground, outside.origin, ox, oz, e.RoadY - 1.5f, e.RoadY + 1.5f);
                        if (float.IsNaN(drive) || float.IsNaN(road)) continue;   // the road's edge is further out here (it runs off square to the front)
                        points++;
                        string at = $"garage {site.Id}, the opening at {(e.X0 + e.X1) * 0.5f:F1}, {x:F2} across: the drive {drive:F3}, the road {road:F3}";
                        if (drive - road > lip)
                        {
                            lip = drive - road;
                            lipAt = at;
                        }
                        if (road - drive > gap)
                        {
                            gap = road - drive;
                            gapAt = at;
                        }
                    }
                }
            }
            Debug.Log($"CityX garage drive mouths: {drives} drives, {points} points across their mouths; the drive at most {lip:F3} m over the road ({lipAt}), " +
                $"the road at most {gap:F3} m over the drive ({gapAt})");
            Assert.That(drives, Is.GreaterThan(10));
            Assert.That(points, Is.GreaterThan(drives * 4), "too few points where both the drive and the road were found");
            Assert.That(lip, Is.LessThan(0.01f), $"a drive's edge stands over the road: {lipAt}");
            Assert.That(gap, Is.LessThan(0.05f), $"a drive sinks under the road's edge: {gapAt}");
        }

        /// <summary>The highest upward-facing triangle of a buffer over a point (in plan) between two heights; NaN where there is none.</summary>
        private static float HighestAt(MeshBuffer buffer, Vector3 origin, double wx, double wz, float yLow, float yHigh)
        {
            float x = (float)(wx - origin.x), z = (float)(wz - origin.z);
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            float best = float.NaN;
            for (int k = 0; k + 2 < t.Count; k += 3)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.y <= 0.3f * n.magnitude) continue;
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float wa = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                float wb = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                if (wa < -1e-4f || wb < -1e-4f || wa + wb > 1.0001f) continue;
                float y = wa * a.y + wb * b.y + (1.0f - wa - wb) * c.y + origin.y;
                if (y >= yLow && y <= yHigh && (float.IsNaN(best) || y > best)) best = y;
            }
            return best;
        }

        /// <summary>
        /// Nothing stands in the lane straight in from a drive on the lowest deck, from inside the front wall to the aisle before the ramp along the
        /// back wall (seed 19077151: the car drove in and met a column of the deck's grid; the columns now keep out of each drive's lane,
        /// CityMeshBuilder.GarageEntranceLaneHalf). The garages near the spawn, each built alone in its own frame: no triangle of them in a box as wide
        /// as the car and a margin, from just over the drive's surface to over the car's roof.
        /// </summary>
        [Test]
        public void TheLaneInFromEachDriveIsClearOnTheLowestDeck()
        {
            var world = new CityWorld(Seed);
            const float halfLane = 2.0f;   // Vehicles/Car is 3.4 m wide at the player's scale; the nearest columns stand 2.3 m from the lane's middle
            const float carHeight = 2.3f;
            int garages = 0, lanes = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                if (!site.IsParkingGarage) continue;
                GarageEntrance[] two = GarageEntrances.For(world.Plan, site);
                if (!two[0].HasDriveway && !two[1].HasDriveway) continue;
                MeshBuffer[] built = SiteAlone(world, site);
                float front = site.Params.Depth * 0.5f;
                float back = -front + 0.6f + CityMeshBuilder.GarageRampWidth + 0.5f;   // the aisle in front of the ramp up along the back wall
                foreach (GarageEntrance e in two)
                {
                    if (!e.HasDriveway) continue;
                    float mid = (e.X0 + e.X1) * 0.5f;
                    // In 1 m pieces from inside the front wall to the back aisle, each from just over the drive's surface there.
                    var pieces = new List<(Vector3 min, Vector3 max)>();
                    for (float z = front - 0.5f; z > back; z -= 1.0f)
                    {
                        float zb = Mathf.Max(back, z - 1.0f);
                        float floor = Mathf.Max(e.SurfaceAt(front - z), e.SurfaceAt(front - zb));
                        pieces.Add((new Vector3(mid - halfLane, floor + 0.1f, zb), new Vector3(mid + halfLane, floor + carHeight, z)));
                    }
                    foreach (MeshBuffer b in built)
                    {
                        string hit = FirstIn(b, pieces);
                        Assert.IsNull(hit, $"garage {site.Id} (front at {front:F1}): something stands in the lane in from the drive at {mid:F1}: {hit}");
                    }
                    lanes++;
                }
                if (++garages >= 6) break;
            }
            Debug.Log($"CityX garage lanes: {lanes} lanes of {garages} garages near the spawn clear from the front wall to the back aisle");
            Assert.That(garages, Is.GreaterThan(0), "no garage with a drive near the spawn");
        }

        /// <summary>A building alone, near, in its builder's unturned frame (x along the frontage, +z out of the front, y absolute).</summary>
        private static MeshBuffer[] SiteAlone(CityWorld world, BuildingSite site)
        {
            var buffers = new MeshBuffer[4];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ);
            CityMeshBuilder.BuildSite(world, site, origin, buffers[0], buffers[1], buffers[2], buffers[3], null, null, null, null, false);
            foreach (MeshBuffer b in buffers)
            {
                for (int i = 0; i < b.Vertices.Count; i++)
                {
                    Vector3 v = b.Vertices[i];
                    site.WorldToLocal(v.x + site.CentreX, v.z + site.CentreZ, out double lx, out double lz);
                    b.Vertices[i] = new Vector3((float)lx, v.y, (float)lz);
                }
            }
            return buffers;
        }

        /// <summary>The first triangle of a buffer with any of it inside one of the boxes, as its corners and the box; null when none is.</summary>
        private static string FirstIn(MeshBuffer buffer, List<(Vector3 min, Vector3 max)> boxes)
        {
            Vector3 lo = boxes[0].min, hi = boxes[0].max;
            foreach ((Vector3 min, Vector3 max) in boxes)
            {
                lo = Vector3.Min(lo, min);
                hi = Vector3.Max(hi, max);
            }
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            var poly = new List<Vector3>(9);
            var next = new List<Vector3>(9);
            for (int k = 0; k + 2 < t.Count; k += 3)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                Vector3 tlo = Vector3.Min(a, Vector3.Min(b, c)), thi = Vector3.Max(a, Vector3.Max(b, c));
                if (thi.x <= lo.x || tlo.x >= hi.x || thi.y <= lo.y || tlo.y >= hi.y || thi.z <= lo.z || tlo.z >= hi.z) continue;
                foreach ((Vector3 min, Vector3 max) in boxes)
                {
                    if (thi.x <= min.x || tlo.x >= max.x || thi.y <= min.y || tlo.y >= max.y || thi.z <= min.z || tlo.z >= max.z) continue;
                    poly.Clear();
                    poly.Add(a);
                    poly.Add(b);
                    poly.Add(c);
                    for (int axis = 0; axis < 3 && poly.Count > 0; axis++)
                    {
                        Clip(poly, next, axis, min[axis], 1.0f);
                        Clip(next, poly, axis, max[axis], -1.0f);
                    }
                    if (poly.Count >= 3)
                    {
                        return $"a triangle ({a.x:F2}, {a.y:F2}, {a.z:F2}) ({b.x:F2}, {b.y:F2}, {b.z:F2}) ({c.x:F2}, {c.y:F2}, {c.z:F2}) in the box z {min.z:F1}..{max.z:F1}, y {min.y:F2}..{max.y:F2}";
                    }
                }
            }
            return null;
        }

        /// <summary>The part of a polygon on one side of a plane across an axis: <paramref name="sign"/> +1 keeps what is at or over it, -1 at or under.</summary>
        private static void Clip(List<Vector3> from, List<Vector3> to, int axis, float plane, float sign)
        {
            to.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                Vector3 p = from[i], q = from[(i + 1) % from.Count];
                float dp = (p[axis] - plane) * sign, dq = (q[axis] - plane) * sign;
                if (dp >= 0.0f) to.Add(p);
                if ((dp >= 0.0f) != (dq >= 0.0f)) to.Add(Vector3.Lerp(p, q, dp / (dp - dq)));
            }
        }

        /// <summary>Whether an upward-facing triangle of a buffer lies over a point (in plan) between two heights.</summary>
        private static bool Covers(MeshBuffer buffer, Vector3 origin, double wx, double wz, float yLow, float yHigh)
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
