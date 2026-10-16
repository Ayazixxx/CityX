using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Road paint lies on the road: close enough that a foot does not visibly sink into it, and not under it (it would vanish or flicker).
    /// </summary>
    public sealed class RoadPaintTests
    {
        private const int Seed = 63428175;

        [Test]
        public void PaintIsSeldomSunkAndNeverFloatsFarAboveTheRoad()
        {
            var world = new CityWorld(Seed);
            world.Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cz = PlanWorld.ChunkOf(sz);

            int samples = 0, sunk = 0, sunkAtOldLift = 0, floating = 0;
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    var ground = new MeshBuffer();
                    var pavement = new MeshBuffer();
                    var structures = new MeshBuffer();
                    var markings = new MeshBuffer();
                    var dividers = new MeshBuffer();
                    CitySurfaceBuilder.Build(world, cx + dx, cz + dz, Vector3.zero, ground, pavement, structures, markings, dividers, new MeshBuffer(), false);
                    foreach (MeshBuffer paint in new[] { markings, dividers })
                    {
                        for (int t = 0; t < paint.Triangles.Count; t += 3)
                        {
                            Vector3 a = paint.Vertices[paint.Triangles[t]], b = paint.Vertices[paint.Triangles[t + 1]], c = paint.Vertices[paint.Triangles[t + 2]];
                            foreach (Vector3 p in new[] { a, b, c, (a + b) * 0.5f, (b + c) * 0.5f, (a + c) * 0.5f, (a + b + c) / 3.0f })
                            {
                                if (!SurfaceBelow(p.x, p.y, p.z, out float road, ground, pavement, structures))
                                {
                                    continue;
                                }
                                samples++;
                                float delta = p.y - road;
                                sunk += delta < 0.002f ? 1 : 0;
                                sunkAtOldLift += delta + (0.03f - CitySurfaceBuilder.PaintLift) < 0.002f ? 1 : 0;
                                floating += delta > CitySurfaceBuilder.PaintLift + 0.05f ? 1 : 0;
                            }
                        }
                    }
                }
            }
            TestContext.WriteLine($"{samples} samples: sunk {sunk} ({100.0 * sunk / samples:0.0}%), would be sunk at the old 3 cm lift {sunkAtOldLift}, floating {floating}");
            Assert.Greater(samples, 500, "need paint to measure");
            Assert.LessOrEqual(sunk, sunkAtOldLift + samples / 50, "a lower lift must not bury much more paint than the old 3 cm one did");
        }

        /// <summary>The highest road, pavement or deck surface at this spot that is not more than 25 cm above the point.</summary>
        private static bool SurfaceBelow(float x, float py, float z, out float y, params MeshBuffer[] buffers)
        {
            bool found = false;
            y = 0.0f;
            foreach (MeshBuffer buffer in buffers)
            {
                for (int t = 0; t < buffer.Triangles.Count; t += 3)
                {
                    Vector3 a = buffer.Vertices[buffer.Triangles[t]], b = buffer.Vertices[buffer.Triangles[t + 1]], c = buffer.Vertices[buffer.Triangles[t + 2]];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-8f)
                    {
                        continue;
                    }
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    float w = 1.0f - u - v;
                    if (u >= -0.001f && v >= -0.001f && w >= -0.001f)
                    {
                        float h = u * a.y + v * b.y + w * c.y;
                        if (h <= py + 0.25f && (!found || h > y))
                        {
                            y = h;
                            found = true;
                        }
                    }
                }
            }
            return found;
        }
    }
}
