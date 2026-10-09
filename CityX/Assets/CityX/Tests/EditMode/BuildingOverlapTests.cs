using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// REPORTED: some buildings generate as "two layers of building on top of each other": two sites occupying the same space, so one facade shows through or
    /// sits in front of the other and its windows cannot be broken. No two buildings may overlap in footprint over the same heights.
    /// </summary>
    public sealed class BuildingOverlapTests
    {
        private const int Seed = 38207461;

        private static Vector2[] Corners(BuildingSite s)
        {
            var c = new Vector2[4];
            double hx = s.Params.Width * 0.5, hz = s.Params.Depth * 0.5;
            double[,] l = { { -hx, -hz }, { hx, -hz }, { hx, hz }, { -hx, hz } };
            for (int i = 0; i < 4; i++)
            {
                s.LocalToWorld(l[i, 0], l[i, 1], out double wx, out double wz);
                c[i] = new Vector2((float)wx, (float)wz);
            }
            return c;
        }

        /// <summary>Penetration depth along the least-overlapping axis (SAT); 0 or less when the rectangles are apart.</summary>
        private static float Overlap(Vector2[] a, Vector2[] b)
        {
            float least = float.MaxValue;
            foreach (Vector2[] shape in new[] { a, b })
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 edge = shape[(i + 1) % 4] - shape[i];
                    Vector2 axis = new Vector2(-edge.y, edge.x).normalized;
                    Project(a, axis, out float a0, out float a1);
                    Project(b, axis, out float b0, out float b1);
                    float o = Mathf.Min(a1, b1) - Mathf.Max(a0, b0);
                    if (o <= 0.0f) return 0.0f;
                    least = Mathf.Min(least, o);
                }
            }
            return least;
        }

        private static void Project(Vector2[] p, Vector2 axis, out float min, out float max)
        {
            min = float.MaxValue; max = float.MinValue;
            foreach (Vector2 v in p)
            {
                float d = Vector2.Dot(v, axis);
                min = Mathf.Min(min, d); max = Mathf.Max(max, d);
            }
        }

        [Test]
        public void NoTwoBuildingsOccupyTheSameSpace()
        {
            var world = new CityWorld(Seed);
            var sites = new List<BuildingSite>();
            for (int i = 0; i < world.BuildingCount; i++)
            {
                BuildingSite s = world.Building(i);
                if (s.Exists) sites.Add(s);
            }
            var report = new System.Text.StringBuilder();
            int bad = 0;
            for (int i = 0; i < sites.Count; i++)
            {
                Vector2[] a = Corners(sites[i]);
                for (int j = i + 1; j < sites.Count; j++)
                {
                    BuildingSite sa = sites[i], sb = sites[j];
                    if ((sa.CentreX - sb.CentreX) * (sa.CentreX - sb.CentreX) + (sa.CentreZ - sb.CentreZ) * (sa.CentreZ - sb.CentreZ) > 160.0 * 160.0) continue;
                    float vertical = Mathf.Min(sa.Top, sb.Top) - Mathf.Max(sa.PodiumBase, sb.PodiumBase);
                    if (vertical < 1.0f) continue;
                    float depth = Overlap(a, Corners(sb));
                    if (depth > 0.3f)
                    {
                        bad++;
                        if (bad <= 12)
                        {
                            report.AppendLine($"  #{sa.Id} ({sa.Params.Width:F0}x{sa.Params.Depth:F0}, y {sa.PodiumBase:F0}-{sa.Top:F0}, at {sa.CentreX:F0},{sa.CentreZ:F0}) overlaps #{sb.Id} ({sb.Params.Width:F0}x{sb.Params.Depth:F0}, y {sb.PodiumBase:F0}-{sb.Top:F0}, at {sb.CentreX:F0},{sb.CentreZ:F0}) by {depth:F1} m, {vertical:F0} m tall");
                        }
                    }
                }
            }
            Debug.Log($"CityX building overlaps: {bad} of {sites.Count} buildings");
            Assert.AreEqual(0, bad, "overlapping buildings:\n" + report);
        }
    }
}
