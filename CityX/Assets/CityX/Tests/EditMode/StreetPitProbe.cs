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
    /// Not a test: a survey for holes in the street (owner, 2026-10-16, with a screenshot of "a room that goes downward into the street"). Builds the
    /// real chunks round the spawn at the game's own scale and looks down on them: every half metre, the highest upward-facing surface of the
    /// chunk's meshes; where that is more than <see cref="Depth"/> under the ground (or there is nothing at all), the street has a hole in it.
    /// Prints each hole's extent, depth and the buildings about it. Explicit.
    /// </summary>
    [Explicit("a survey of the generated city; prints, asserts nothing")]
    public sealed class StreetPitProbe
    {
        private const int Seed = 48398181;

        /// <summary>A spot is a hole when the top surface over it is this far under the ground, metres.</summary>
        private const float Depth = 0.8f;

        private const float Cell = 0.5f;

        [Test]
        public void FindHolesInTheStreetRoundTheSpawn([Values("varied", "classic")] string scaleName, [Values(14)] int radius)
        {
            CityScale saved = CityScale.Active;
            Assert.IsTrue(CityScale.TryParse(scaleName, out CityScale scale));
            CityScale.Active = scale;
            try
            {
                var world = new CityWorld(Seed);
                world.Plan.TryFindSpawn(out double sx, out double sz);
                int scx = PlanWorld.ChunkOf(sx), scy = PlanWorld.ChunkOf(sz);
                var report = new StringBuilder();
                int holes = 0;
                for (int cy = scy - radius; cy <= scy + radius; cy++)
                {
                    for (int cx = scx - radius; cx <= scx + radius; cx++)
                    {
                        holes += Survey(world, cx, cy, report);
                    }
                }
                Debug.Log($"[StreetPit] {scaleName}: {holes} holes within {radius} chunks of the spawn ({sx:F0}, {sz:F0})\n{report}");
            }
            finally
            {
                CityScale.Active = saved;
            }
        }

        private static int Survey(CityWorld world, int cx, int cy, StringBuilder report)
        {
            var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
            var ground = new MeshBuffer();
            var concrete = new MeshBuffer();
            var structures = new MeshBuffer();
            var lit = new MeshBuffer();
            var dark = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, cx, cy, origin, ground, concrete, structures, lit, dark);

            int n = Mathf.RoundToInt(CityLayout.ChunkSize / Cell);
            var top = new float[n, n];
            var source = new int[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) top[i, j] = float.NegativeInfinity;
            MeshBuffer[] buffers = { ground, concrete, structures };
            for (int b = 0; b < buffers.Length; b++)
            {
                Rasterize(buffers[b], b, top, source, n);
            }

            // The holes: cells whose top is well under the ground there (not water), grouped into connected patches.
            var hole = new bool[n, n];
            int any = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double wx = origin.x + (i + 0.5) * Cell, wz = origin.z + (j + 0.5) * Cell;
                    if (world.Plan.Fields.IsWater(wx, wz)) continue;
                    double g = world.Plan.GroundHeight(wx, wz);
                    if (top[i, j] < g - Depth)
                    {
                        hole[i, j] = true;
                        any++;
                    }
                }
            }
            if (any == 0) return 0;
            var seen = new bool[n, n];
            int patches = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (!hole[i, j] || seen[i, j]) continue;
                    // Flood the patch.
                    var stack = new Stack<(int, int)>();
                    stack.Push((i, j));
                    seen[i, j] = true;
                    int count = 0, voids = 0, i0 = i, i1 = i, j0 = j, j1 = j;
                    float deepest = 0.0f, shallowest = float.MaxValue;
                    var sources = new int[3];
                    while (stack.Count > 0)
                    {
                        (int a, int c) = stack.Pop();
                        count++;
                        i0 = Mathf.Min(i0, a); i1 = Mathf.Max(i1, a); j0 = Mathf.Min(j0, c); j1 = Mathf.Max(j1, c);
                        double wx = origin.x + (a + 0.5) * Cell, wz = origin.z + (c + 0.5) * Cell;
                        double g = world.Plan.GroundHeight(wx, wz);
                        if (float.IsNegativeInfinity(top[a, c])) voids++;
                        else
                        {
                            float depth = (float)(g - top[a, c]);
                            deepest = Mathf.Max(deepest, depth);
                            shallowest = Mathf.Min(shallowest, depth);
                            sources[source[a, c]]++;
                        }
                        foreach ((int da, int dc) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int na = a + da, nc = c + dc;
                            if (na < 0 || nc < 0 || na >= n || nc >= n || seen[na, nc] || !hole[na, nc]) continue;
                            seen[na, nc] = true;
                            stack.Push((na, nc));
                        }
                    }
                    if (count < 4) continue;   // a speck (a seam between two meshes): not a room
                    patches++;
                    double mx = origin.x + (i0 + i1 + 1) * 0.5 * Cell, mz = origin.z + (j0 + j1 + 1) * 0.5 * Cell;
                    report.AppendLine($"chunk ({cx},{cy}): a hole of {count * Cell * Cell:F1} m2 at ({mx:F1}, {mz:F1}), {(i1 - i0 + 1) * Cell:F1} x {(j1 - j0 + 1) * Cell:F1} m, " +
                        $"{(voids > 0 ? voids + " cells with nothing under them, " : "")}floor {shallowest:F2}-{deepest:F2} m under the ground " +
                        $"(top from ground {sources[0]}, concrete {sources[1]}, structures {sources[2]})");
                    foreach (BuildingSite s in Near(world, mx, mz, 30.0))
                    {
                        s.WorldToLocal(mx, mz, out double lx, out double lz);
                        report.AppendLine($"    building {s.Id}: {s.Params.Width:F1}x{s.Params.Depth:F1}x{s.Params.Height:F1} {s.Params.Mass} special '{s.Params.SpecialType}' " +
                            $"basement {s.Params.HasBasement} base {s.BaseHeight:F2} podium {s.PodiumBase:F2} door {s.DoorGroundHeight:F2} onRoof {s.OnRoof} wing {s.Wing} shell {s.Shell} " +
                            $"sides {s.Sides}, the hole's middle at ({lx:F1}, {lz:F1}) in its frame");
                    }
                }
            }
            return patches;
        }

        private static IEnumerable<BuildingSite> Near(CityWorld world, double x, double z, double reach)
        {
            int c0x = PlanWorld.ChunkOf(x - reach), c1x = PlanWorld.ChunkOf(x + reach);
            int c0z = PlanWorld.ChunkOf(z - reach), c1z = PlanWorld.ChunkOf(z + reach);
            for (int cy = c0z; cy <= c1z; cy++)
            {
                for (int cx = c0x; cx <= c1x; cx++)
                {
                    foreach (int id in world.BuildingsOwnedBy(cx, cy))
                    {
                        BuildingSite s = world.Building(id);
                        if (!s.Exists) continue;
                        s.WorldToLocal(x, z, out double lx, out double lz);
                        if (System.Math.Abs(lx) <= s.Params.Width * 0.5 + 6.0 && System.Math.Abs(lz) <= s.Params.Depth * 0.5 + 6.0)
                        {
                            yield return s;
                        }
                    }
                }
            }
        }

        /// <summary>Every upward-facing triangle of a buffer onto the grid: the highest surface over each cell's middle.</summary>
        private static void Rasterize(MeshBuffer buffer, int index, float[,] top, int[,] source, int n)
        {
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            for (int k = 0; k + 2 < t.Count; k += 3)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.y <= 1e-6f || normal.normalized.y < 0.3f) continue;   // only what can be stood on, looked down at
                float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                int i0 = Mathf.Max(0, Mathf.CeilToInt(minX / Cell - 0.5f)), i1 = Mathf.Min(n - 1, Mathf.FloorToInt(maxX / Cell - 0.5f));
                int j0 = Mathf.Max(0, Mathf.CeilToInt(minZ / Cell - 0.5f)), j1 = Mathf.Min(n - 1, Mathf.FloorToInt(maxZ / Cell - 0.5f));
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f) continue;
                for (int i = i0; i <= i1; i++)
                {
                    float x = (i + 0.5f) * Cell;
                    for (int j = j0; j <= j1; j++)
                    {
                        float z = (j + 0.5f) * Cell;
                        float wa = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                        float wb = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                        float wc = 1.0f - wa - wb;
                        if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                        float y = wa * a.y + wb * b.y + wc * c.y;
                        if (y > top[i, j])
                        {
                            top[i, j] = y;
                            source[i, j] = index;
                        }
                    }
                }
            }
        }
    }
}
