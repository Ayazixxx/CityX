using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The ground never shows through a road (owner, 2026-10-16, with a night screenshot of a sloping street: "on areas where the terrain slopes there
    /// are often times different colored textures poking through"). A road lays its own surface, straight across each 5 m piece, while the
    /// terrain under it was sampled every 4 m: on a curving hillside the earth stood up through the asphalt (by as much as 0.74 m on 3,636 of the
    /// half-metre points in the 160 hilliest road chunks near the spawn) and fought it where the two lay within a few centimetres (about 1,500 more).
    /// The terrain is cut away under every road and junction now, with a skirt down each road's edge (CitySurfaceBuilder.RoadCuts, RoadSkirt).
    /// The hilliest road chunks near the spawn at the game's own scale, near and far: on every point of a carriageway, nothing of the earth at or
    /// over the asphalt. (SurfacePokeSurvey, Explicit, maps the rest.)
    /// </summary>
    public sealed class RoadGroundTests
    {
        private const int Seed = 80469103;
        private const int Chunks = 40;
        private const float Step = 0.5f;

        private struct Tri
        {
            public Vector3 A, B, C;
            public int Slice;
        }

        [Test]
        public void TheGroundNeverShowsThroughARoadOnAHillside()
        {
            CityScale saved = CityScale.Active;
            CityScale.Active = CityScale.Default;
            try
            {
                var world = new CityWorld(Seed);
                PlanWorld plan = world.Plan;
                plan.TryFindSpawn(out double sx, out double sz);
                int scx = PlanWorld.ChunkOf(sx), scy = PlanWorld.ChunkOf(sz);
                var hilly = new List<(int, int, double)>();
                for (int cy = scy - 30; cy <= scy + 30; cy++)
                {
                    for (int cx = scx - 30; cx <= scx + 30; cx++)
                    {
                        if (plan.EdgesIn(cx, cy).Count == 0) continue;
                        double x = (cx + 0.5) * CityLayout.ChunkSize, z = (cy + 0.5) * CityLayout.ChunkSize;
                        double dx = plan.GroundHeight(x + 1.0, z) - plan.GroundHeight(x - 1.0, z), dz = plan.GroundHeight(x, z + 1.0) - plan.GroundHeight(x, z - 1.0);
                        hilly.Add((cx, cy, dx * dx + dz * dz));
                    }
                }
                hilly.Sort((a, b) => b.Item3.CompareTo(a.Item3));
                int roadPoints = 0;
                foreach (bool far in new[] { false, true })
                {
                    for (int i = 0; i < Chunks && i < hilly.Count; i++)
                    {
                        roadPoints += CheckChunk(world, hilly[i].Item1, hilly[i].Item2, far);
                    }
                }
                Debug.Log($"CityX road ground: {roadPoints} points on carriageways in the {Chunks} hilliest road chunks near the spawn, near and far: the earth over none");
                Assert.Greater(roadPoints, 10000, "too few road points looked at");
            }
            finally
            {
                CityScale.Active = saved;
            }
        }

        private static int CheckChunk(CityWorld world, int cx, int cy, bool far)
        {
            PlanWorld plan = world.Plan;
            var origin = new Vector3(cx * CityLayout.ChunkSize, 0, cy * CityLayout.ChunkSize);
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            CitySurfaceBuilder.Build(world, cx, cy, origin, ground, pavement, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), far);
            var buckets = new Dictionary<(int, int), List<Tri>>();
            foreach (MeshBuffer m in new[] { ground, pavement })
            {
                for (int k = 0; k + 2 < m.Triangles.Count; k += 3)
                {
                    int ia = m.Triangles[k];
                    Vector3 a = m.Vertices[ia], b = m.Vertices[m.Triangles[k + 1]], c = m.Vertices[m.Triangles[k + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-10f || Mathf.Abs(n.normalized.y) < 0.35f) continue;
                    SurfaceLook.Unpack(m.Colors[ia], out _, out int slice, out _);
                    var t = new Tri { A = a, B = b, C = c, Slice = slice };
                    for (int bz = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z))); bz <= Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z))); bz++)
                    {
                        for (int bx = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))); bx <= Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))); bx++)
                        {
                            if (!buckets.TryGetValue((bx, bz), out List<Tri> list)) buckets[(bx, bz)] = list = new List<Tri>();
                            list.Add(t);
                        }
                    }
                }
            }
            int points = 0;
            for (float lz = Step * 0.5f; lz < CityLayout.ChunkSize; lz += Step)
            {
                for (float lx = Step * 0.5f; lx < CityLayout.ChunkSize; lx += Step)
                {
                    double wx = lx + origin.x, wz = lz + origin.z;
                    if (!OnACarriageway(plan, wx, wz, out int edge)) continue;
                    if (!buckets.TryGetValue((Mathf.FloorToInt(lx), Mathf.FloorToInt(lz)), out List<Tri> list)) continue;
                    float asphalt = float.MinValue, earth = float.MinValue;
                    foreach (Tri t in list)
                    {
                        if (!Height(t, lx, lz, out float y)) continue;
                        if (t.Slice == SurfaceLook.Asphalt || t.Slice == SurfaceLook.Setts) asphalt = Mathf.Max(asphalt, y);
                        if (t.Slice == SurfaceLook.Earth) earth = Mathf.Max(earth, y);
                    }
                    if (asphalt == float.MinValue) continue;   // under an expressway's ramp foot or the like: not this test's
                    points++;
                    Assert.Less(earth, asphalt - 0.03f, $"the earth stands {earth - asphalt:F3} m against the asphalt of road {edge} at ({wx:F1}, {wz:F1}), " +
                        $"ground {plan.GroundHeight(wx, wz):F2}, {(far ? "far" : "near")} chunk ({cx}, {cy})");
                }
            }
            return points;
        }

        /// <summary>Whether a point is on a road's carriageway, clear of its edges and ends by 0.3 m (not a bridge: its deck is over the ground).</summary>
        private static bool OnACarriageway(PlanWorld plan, double x, double z, out int edge)
        {
            var p = new Vec2(x, z);
            foreach (int e in plan.EdgesIn(PlanWorld.ChunkOf(x), PlanWorld.ChunkOf(z)))
            {
                EdgeGeometry g = plan.Geometry.Edges[e];
                if (g == null || g.Edge.Bridge) continue;
                double d = PlanMath.DistanceToSegment(p, g.A, g.B, out double u);
                double along = u * g.Length;
                if (d <= g.HalfWidth - 0.3 && along >= g.TrimA + 0.3 && along <= g.Length - g.TrimB - 0.3)
                {
                    edge = e;
                    return true;
                }
            }
            edge = -1;
            return false;
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
    }
}
