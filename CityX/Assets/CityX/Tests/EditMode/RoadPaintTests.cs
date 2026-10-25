using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Road paint lies ON the asphalt it marks, as part of the street: wherever it shows, it is the same few millimetres over the road, never
    /// sunk into it and never floating over it, however the road bends under it (owner, 2026-10-17: "i want them to be apart of the texture of
    /// the street not hovering above or glitching into the terrain"). Measured as the eye sees it, over the 7 x 7 chunks round the spawn: at
    /// every corner, edge middle and centre of every paint triangle, the topmost paint there against the topmost surface.
    /// </summary>
    public sealed class RoadPaintTests
    {
        private const int Seed = 23481876;

        [Test]
        public void PaintLiesFlatOnTheAsphaltWhereverItShows()
        {
            var world = new CityWorld(Seed);
            world.Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cz = PlanWorld.ChunkOf(sz);
            float lift = CitySurfaceBuilder.PaintOnAsphaltLift;

            int samples = 0, hidden = 0, floating = 0, low = 0, flush = 0;
            float worst = 0.0f;
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
                            Vector3 centre = (a + b + c) / 3.0f;
                            foreach (Vector3 corner in new[] { a, b, c, (a + b) * 0.5f, (b + c) * 0.5f, (a + c) * 0.5f, centre })
                            {
                                // A hair in from the triangle's edge, so the point is inside the asphalt triangle the piece lies on.
                                Vector3 p = Vector3.Lerp(corner, centre, 0.02f);
                                if (!SurfaceBelow(p.x, p.y, p.z, out _, ground))
                                {
                                    continue;   // not road paint (a car park's, a mall's)
                                }
                                samples++;
                                SurfaceBelow(p.x, p.y, p.z, out float topPaint, markings, dividers);
                                SurfaceBelow(p.x, topPaint, p.z, out float top, ground, pavement, structures);
                                float over = topPaint - top;
                                if (over < -0.002f)
                                {
                                    hidden++;   // under a kerb or a surface over the road: not seen
                                    continue;
                                }
                                worst = Mathf.Max(worst, Mathf.Abs(over - lift));
                                if (over > lift + 0.003f) floating++;
                                else if (over < lift - 0.003f) low++;
                                else flush++;
                            }
                        }
                    }
                }
            }
            TestContext.WriteLine($"{samples} samples: flush {flush}, low {low}, floating {floating}, hidden {hidden}; worst {worst * 100.0f:0.00} cm off");
            Assert.Greater(samples, 500, "need paint to measure");
            Assert.AreEqual(0, floating, $"paint floats over the road (worst {worst * 100.0f:0.0} cm off its lift)");
            Assert.LessOrEqual(low, samples / 200, "paint sits in the road rather than on it");
            Assert.LessOrEqual(hidden, samples / 100, "more than one point in a hundred of the paint is hidden under something");
            Assert.GreaterOrEqual(flush, samples * 98 / 100, "the paint is not laid flush on the road");
        }

        /// <summary>The highest surface of <paramref name="buffers"/> at this spot that is not more than 25 cm above the point.</summary>
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
