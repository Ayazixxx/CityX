using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The ground keeps out of the basements (owner, 2026-10-16, with a screenshot looking down a lobby's opening to its basement: "the basement is
    /// only 1 foot high"). The terrain and the block's paving ran across every building's footprint, so they passed through each basement a foot or
    /// so under the lobby floor: the stairs down went three steps and met the street's paving. Now nothing of the ground is left over a basement,
    /// and the paving just outside its walls is whole.
    /// </summary>
    public sealed class BasementGroundTests
    {
        private const int Seed = 46283197;

        [Test]
        public void NoGroundOrPavingIsLeftInsideABasementAndThePavingOutsideIsWhole()
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
            bool Covered(double x, double z, float yLow = float.NegativeInfinity, float yHigh = float.PositiveInfinity)
            {
                var c = Chunk(PlanWorld.ChunkOf(x), PlanWorld.ChunkOf(z));
                return Covers(c.ground, c.origin, x, z, yLow, yHigh) || Covers(c.pavement, c.origin, x, z, yLow, yHigh);
            }

            var basements = new List<BuildingSite>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 12))
            {
                if (CitySurfaceBuilder.HasBasementUnder(site)) basements.Add(site);
            }
            Assert.That(basements.Count, Is.GreaterThan(5), "too few buildings with a basement near the spawn");
            int checkedOutside = 0, onRoads = 0;
            foreach (BuildingSite site in basements)
            {
                double hw = site.Params.Width * 0.5, hd = site.Params.Depth * 0.5;
                // Inside: nothing of the ground in the basement, anywhere across it, between its floor a storey down and the lobby floor (on a steep
                // slope the ground under the downhill end lies deeper than the basement, under its floor, out of the way).
                float lobby = site.BaseHeight, floor = lobby - BuildingInterior.StoreyHeight(site.Params.Height);
                for (int i = 0; i <= 4; i++)
                {
                    for (int j = 0; j <= 4; j++)
                    {
                        double lx = (i / 4.0 - 0.5) * (2.0 * hw - 0.8), lz = (j / 4.0 - 0.5) * (2.0 * hd - 0.8);
                        site.LocalToWorld(lx, lz, out double x, out double z);
                        if (!Covered(x, z, floor - 0.05f, lobby - 0.05f))
                        {
                            continue;
                        }
                        if (OnARoad(world, x, z))
                        {
                            // A separate plan fault, not the ground under the basement: on seed 46283197 a block stands across the end of a dead-end
                            // street (building 2520 over road 3542). Counted here, fixed in the plan on its own.
                            onRoads++;
                            continue;
                        }
                        // What it is: the ground buffer (roads, junctions) or the pavement (terrain, paving, kerbs), and any road or kerb cut near.
                        var c = Chunk(PlanWorld.ChunkOf(x), PlanWorld.ChunkOf(z));
                        string which = Covers(c.ground, c.origin, x, z, floor - 0.05f, lobby - 0.05f) ? "the roads' buffer" : "the pavement buffer";
                        string roads = "";
                        foreach (int e in world.Plan.EdgesIn(PlanWorld.ChunkOf(x), PlanWorld.ChunkOf(z)))
                        {
                            EdgeGeometry g = world.Plan.Geometry.Edges[e];
                            if (g == null) continue;
                            double d = PlanMath.DistanceToSegment(new Vec2(x, z), g.A, g.B, out _);
                            if (d < g.HalfWidth + 3.0) roads += $" road {e} ({g.Edge.Class}{(g.Edge.Bridge ? ", bridge" : "")}) {d:F1} m off its middle, half width {g.HalfWidth:F1};";
                        }
                        int other = world.Plan.BuildingAt(x, z);
                        Assert.Fail($"building {site.Id} ({site.Params.Width:F0}x{site.Params.Depth:F0}, {site.Params.Mass}, lot zone {site.Zone}): something in " +
                            $"{which} runs through its basement at ({lx:F1}, {lz:F1}), {site.BaseHeight - world.Plan.GroundHeight(x, z):F2} m under the lobby " +
                            $"floor; the building found there is {other};{roads}");
                    }
                }
                // Outside: half a metre out from the middle of each wall the paving is whole (unless another basement is there).
                foreach ((double lx, double lz) in new[] { (0.0, -hd - 0.5), (0.0, hd + 0.5), (-hw - 0.5, 0.0), (hw + 0.5, 0.0) })
                {
                    site.LocalToWorld(lx, lz, out double x, out double z);
                    if (InAnyFootprint(world, x, z) || world.Plan.Fields.IsWater(x, z)) continue;
                    Assert.IsTrue(Covered(x, z), $"building {site.Id}: a hole in the ground outside its wall at ({lx:F1}, {lz:F1})");
                    checkedOutside++;
                }
            }
            Debug.Log($"[BasementGround] {basements.Count} buildings with a basement near the spawn: none has ground left inside, {checkedOutside} points outside whole; " +
                $"{onRoads} points inside a footprint lie on a street (a block placed across one)");
            Assert.That(checkedOutside, Is.GreaterThan(basements.Count));
        }

        /// <summary>Whether a point lies on a street's carriageway (within its half width of its middle).</summary>
        private static bool OnARoad(CityWorld world, double x, double z)
        {
            foreach (int e in world.Plan.EdgesIn(PlanWorld.ChunkOf(x), PlanWorld.ChunkOf(z)))
            {
                EdgeGeometry g = world.Plan.Geometry.Edges[e];
                if (g != null && PlanMath.DistanceToSegment(new Vec2(x, z), g.A, g.B, out _) <= g.HalfWidth)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether any building's footprint (its walls) covers a point: the ground under a building is the building's business.</summary>
        private static bool InAnyFootprint(CityWorld world, double x, double z)
        {
            return world.Plan.BuildingAt(x, z) >= 0;
        }

        /// <summary>Whether an upward-facing triangle of a buffer lies over a point (in plan).</summary>
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
