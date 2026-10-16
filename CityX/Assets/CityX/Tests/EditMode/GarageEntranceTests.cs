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
        private const int Seed = 63428175;

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
                            float drive = e.SurfaceAt(s);
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
