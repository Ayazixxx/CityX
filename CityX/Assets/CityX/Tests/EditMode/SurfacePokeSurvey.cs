using System.Collections.Generic;
using System.Text;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: where the wrong surface shows (owner, 2026-10-16, with screenshots: "on areas where the terrain slopes there are often times
    /// different colored textures poking through"). Builds the ground of the chunks round the spawn at the game's own scale and, every half metre,
    /// finds the surface on top and what it covers: a point on a carriageway should show asphalt (setts on a pedestrian street), a point on a
    /// block's paving should show paving. Counts each wrong pair (earth over asphalt, paving over asphalt, earth over paving, ...) with the worst
    /// spots and what they are (a road, a junction's plate, the pavement ring, a block's paving). Explicit.
    /// </summary>
    [Explicit("a survey, not a test")]
    public sealed class SurfacePokeSurvey
    {
        private const int Seed = 23481876;
        private const int Radius = 6;
        private const float Step = 0.5f;

        private struct Tri
        {
            public Vector3 A, B, C;
            public int Slice;
            public Color32 Look;
        }

        [Test]
        public void SurveyTheSurfacesShowingWhereTheyShouldNot()
        {
            CityScale saved = CityScale.Active;
            CityScale.Active = CityScale.Default;
            try
            {
                var world = new CityWorld(Seed);
                PlanWorld plan = world.Plan;
                plan.TryFindSpawn(out double sx, out double sz);
                int scx = PlanWorld.ChunkOf(sx), scy = PlanWorld.ChunkOf(sz);
                // The chunks round the spawn, and the hilliest road chunks within 40 of it (the spawn's own ground is nearly flat).
                var near = new List<(int, int)>();
                for (int cy = scy - Radius; cy <= scy + Radius; cy++)
                {
                    for (int cx = scx - Radius; cx <= scx + Radius; cx++) near.Add((cx, cy));
                }
                var hilly = new List<(int, int, double)>();
                for (int cy = scy - 40; cy <= scy + 40; cy++)
                {
                    for (int cx = scx - 40; cx <= scx + 40; cx++)
                    {
                        if (plan.EdgesIn(cx, cy).Count == 0) continue;
                        hilly.Add((cx, cy, Slope(plan, (cx + 0.5) * CityLayout.ChunkSize, (cy + 0.5) * CityLayout.ChunkSize)));
                    }
                }
                hilly.Sort((a, b) => b.Item3.CompareTo(a.Item3));
                var steep = new List<(int, int)>();
                for (int i = 0; i < 160 && i < hilly.Count; i++) steep.Add((hilly[i].Item1, hilly[i].Item2));
                Debug.Log($"CityX surface poke survey: the hilliest road chunk slopes {hilly[0].Item3:F0} %, the 160th {hilly[System.Math.Min(159, hilly.Count - 1)].Item3:F0} %");
                Survey(world, near, false, "round the spawn");
                Survey(world, steep, false, "the 160 hilliest road chunks");
                Survey(world, steep, true, "the 160 hilliest road chunks, as seen from afar");
            }
            finally
            {
                CityScale.Active = saved;
            }
        }

        private static void Survey(CityWorld world, List<(int, int)> chunks, bool far, string which)
        {
            PlanWorld plan = world.Plan;
            var pairs = new Dictionary<string, int>();
            var worst = new Dictionary<string, (float depth, string where)>();
            int points = 0, wrong = 0;
            foreach ((int cx, int cy) in chunks)
            {
                {
                    var origin = new Vector3(cx * CityLayout.ChunkSize, 0, cy * CityLayout.ChunkSize);
                    var ground = new MeshBuffer();
                    var pavement = new MeshBuffer();
                    CitySurfaceBuilder.Build(world, cx, cy, origin, ground, pavement, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), far);
                    // Upward faces only, bucketed by the metre they reach into.
                    var buckets = new Dictionary<(int, int), List<Tri>>();
                    foreach (MeshBuffer m in new[] { ground, pavement })
                    {
                        for (int k = 0; k + 2 < m.Triangles.Count; k += 3)
                        {
                            int ia = m.Triangles[k], ib = m.Triangles[k + 1], ic = m.Triangles[k + 2];
                            Vector3 a = m.Vertices[ia], b = m.Vertices[ib], c = m.Vertices[ic];
                            Vector3 n = Vector3.Cross(b - a, c - a);
                            if (n.sqrMagnitude < 1e-10f || Mathf.Abs(n.normalized.y) < 0.35f) continue;   // walls, kerb faces
                            SurfaceLook.Unpack(m.Colors[ia], out _, out int slice, out _);
                            var t = new Tri { A = a, B = b, C = c, Slice = slice, Look = m.Colors[ia] };
                            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                            int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z))), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
                            for (int bz = z0; bz <= z1; bz++)
                            {
                                for (int bx = x0; bx <= x1; bx++)
                                {
                                    if (!buckets.TryGetValue((bx, bz), out List<Tri> list)) buckets[(bx, bz)] = list = new List<Tri>();
                                    list.Add(t);
                                }
                            }
                        }
                    }
                    for (float lz = Step * 0.5f; lz < CityLayout.ChunkSize; lz += Step)
                    {
                        for (float lx = Step * 0.5f; lx < CityLayout.ChunkSize; lx += Step)
                        {
                            if (!buckets.TryGetValue((Mathf.FloorToInt(lx), Mathf.FloorToInt(lz)), out List<Tri> list)) continue;
                            // Every surface over the point: the top one, and the highest of each other kind under it.
                            float topY = float.MinValue;
                            int topSlice = -1;
                            var under = new List<(float y, int slice)>();
                            var looks = new List<(float y, Color32 look, int slice)>();
                            foreach (Tri t in list)
                            {
                                if (!Height(t, lx, lz, out float y)) continue;
                                under.Add((y, t.Slice));
                                looks.Add((y, t.Look, t.Slice));
                                if (y > topY)
                                {
                                    topY = y;
                                    topSlice = t.Slice;
                                }
                            }
                            if (topSlice < 0) continue;
                            points++;
                            double wx = lx + origin.x, wz = lz + origin.z;
                            string expected = Expected(plan, wx, wz, out string what);
                            // Two different looks within 3 cm of each other at the top: they fight, each showing through the other.
                            Color32 topLook = default;
                            foreach ((float y, Color32 look, int slice) in looks)
                            {
                                if (y == topY) topLook = look;
                            }
                            foreach ((float y, Color32 look, int slice) in looks)
                            {
                                if (topY - y > 0.03f || (look.r == topLook.r && look.g == topLook.g && look.b == topLook.b && look.a == topLook.a)) continue;
                                string fight = $"{Name(topSlice)} fighting {Name(slice)} within 3 cm ({(what.Length > 0 ? what : "off the roads and blocks")})";
                                pairs[fight] = pairs.TryGetValue(fight, out int f0) ? f0 + 1 : 1;
                                if (!worst.ContainsKey(fight)) worst[fight] = (topY - y, $"({wx:F1}, {wz:F1}) ground {plan.GroundHeight(wx, wz):F2}, slope {Slope(plan, wx, wz):F0} %");
                                break;
                            }
                            if (expected == null) continue;
                            int want = expected == "asphalt" ? SurfaceLook.Asphalt : SurfaceLook.Paving;
                            if (topSlice == want || (want == SurfaceLook.Asphalt && topSlice == SurfaceLook.Setts) || (want == SurfaceLook.Paving && topSlice == SurfaceLook.Setts)) continue;
                            // The wanted surface is there but under the top one: the top one pokes through it.
                            float wantedY = float.MinValue;
                            foreach ((float y, int slice) in under)
                            {
                                if (slice == want && y > wantedY) wantedY = y;
                            }
                            if (wantedY == float.MinValue) continue;   // nothing of the wanted kind here at all: another question
                            wrong++;
                            string pair = $"{Name(topSlice)} over {Name(want)} ({what})";
                            pairs[pair] = pairs.TryGetValue(pair, out int n0) ? n0 + 1 : 1;
                            float depth = topY - wantedY;
                            if (!worst.TryGetValue(pair, out var w) || depth > w.depth)
                            {
                                worst[pair] = (depth, $"({wx:F1}, {wz:F1}) ground {plan.GroundHeight(wx, wz):F2}, slope {Slope(plan, wx, wz):F0} %");
                            }
                        }
                    }
                }
            }
            var report = new StringBuilder();
            report.AppendLine($"CityX surface poke survey: seed {Seed}, game scale, {which} ({chunks.Count} chunks, {(far ? "far" : "near")}), {points} points every {Step} m; {wrong} show the wrong surface ({100.0 * wrong / Mathf.Max(1, points):F2} %)");
            var keys = new List<string>(pairs.Keys);
            keys.Sort((a, b) => pairs[b].CompareTo(pairs[a]));
            foreach (string k in keys)
            {
                report.AppendLine($"  {pairs[k],7} x {k}: worst {worst[k].depth:F3} m over it at {worst[k].where}");
            }
            Debug.Log(report.ToString());
        }

        private static bool Height(Tri t, float x, float z, out float y)
        {
            Vector3 a = t.A, b = t.B, c = t.C;
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            y = 0.0f;
            if (Mathf.Abs(d) < 1e-9f) return false;
            float wa = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
            float wb = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
            if (wa < -1e-4f || wb < -1e-4f || wa + wb > 1.0001f) return false;
            y = wa * a.y + wb * b.y + (1.0f - wa - wb) * c.y;
            return true;
        }

        /// <summary>What should show at a point: asphalt on a carriageway (or a junction's plate), paving on a block's paved ground; null elsewhere.</summary>
        private static string Expected(PlanWorld plan, double x, double z, out string what)
        {
            var p = new Vec2(x, z);
            int cx = PlanWorld.ChunkOf(x), cy = PlanWorld.ChunkOf(z);
            what = "";
            foreach (int e in plan.EdgesIn(cx, cy))
            {
                EdgeGeometry g = plan.Geometry.Edges[e];
                if (g == null || g.Edge.Bridge) continue;
                double d = PlanMath.DistanceToSegment(p, g.A, g.B, out double u);
                double along = u * g.Length;
                if (d <= g.HalfWidth - 0.3 && along >= g.TrimA + 0.3 && along <= g.Length - g.TrimB - 0.3)
                {
                    what = "a road";
                    return PedestrianStreets.Is(plan, g.Edge) ? null : "asphalt";
                }
            }
            foreach (int n in plan.JunctionsIn(cx, cy))
            {
                JunctionGeometry j = plan.Geometry.Junctions[n];
                if (j?.Plate != null && PlanMath.PointInPolygon(p, j.Plate))
                {
                    what = "a junction's plate";
                    return "asphalt";
                }
            }
            if (plan.BuildingAt(x, z, 0.5) >= 0) return null;   // under a building: its business
            foreach (int b in plan.BlocksIn(cx, cy))
            {
                PlanBlock block = plan.Data.Blocks[b];
                if (block.Inner == null || !PlanMath.PointInPolygon(p, block.Inner)) continue;
                if (block.Outer != null && !PlanMath.PointInPolygon(p, block.Outer))
                {
                    what = "the pavement ring";
                    return "paving";
                }
                if (block.Paved)
                {
                    what = "a block's paving";
                    return "paving";
                }
            }
            return null;
        }

        private static double Slope(PlanWorld plan, double x, double z)
        {
            double dx = plan.GroundHeight(x + 1.0, z) - plan.GroundHeight(x - 1.0, z);
            double dz = plan.GroundHeight(x, z + 1.0) - plan.GroundHeight(x, z - 1.0);
            return System.Math.Sqrt(dx * dx + dz * dz) * 50.0;
        }

        private static string Name(int slice) =>
            slice == SurfaceLook.Paving ? "paving" : slice == SurfaceLook.Asphalt ? "asphalt" : slice == SurfaceLook.Earth ? "earth" : slice == SurfaceLook.Setts ? "setts" : $"wall material {slice}";
    }
}
