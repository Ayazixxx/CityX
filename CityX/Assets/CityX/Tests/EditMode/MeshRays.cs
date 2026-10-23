using System.Collections.Generic;
using CityX.View;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Rays against a MeshBuffer's triangles (Moller-Trumbore, both faces), for tests that look at what a builder drew.</summary>
    internal static class MeshRays
    {
        /// <summary>
        /// The nearest triangle within <paramref name="reach"/> of <paramref name="from"/> along <paramref name="dir"/> (unit), in the buffer's own
        /// frame: how far, and its corners.
        /// </summary>
        public static bool Nearest(MeshBuffer buffer, Vector3 from, Vector3 dir, float reach, out float distance, out Vector3 ha, out Vector3 hb, out Vector3 hc)
        {
            distance = float.MaxValue;
            ha = hb = hc = Vector3.zero;
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            for (int k = 0; k + 2 < t.Count; k += 3)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-8f) continue;
                float inv = 1.0f / det;
                Vector3 s = from - a;
                float bu = Vector3.Dot(s, p) * inv;
                if (bu < 0.0f || bu > 1.0f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float bv = Vector3.Dot(dir, q) * inv;
                if (bv < 0.0f || bu + bv > 1.0f) continue;
                float d = Vector3.Dot(e2, q) * inv;
                if (d > 0.0f && d < reach && d < distance)
                {
                    distance = d;
                    ha = a;
                    hb = b;
                    hc = c;
                }
            }
            return distance < reach;
        }

        /// <summary>Whether anything of the buffer lies within <paramref name="reach"/> of <paramref name="from"/> along <paramref name="dir"/> (unit).</summary>
        public static bool Hits(MeshBuffer buffer, Vector3 from, Vector3 dir, float reach) => Nearest(buffer, from, dir, reach, out _, out _, out _, out _);

        /// <summary>Whether the segment <paramref name="p0"/>..<paramref name="p1"/> crosses anything of the buffer.</summary>
        public static bool Crosses(MeshBuffer buffer, Vector3 p0, Vector3 p1)
        {
            Vector3 d = p1 - p0;
            float l = d.magnitude;
            return l > 1e-6f && Hits(buffer, p0, d / l, l);
        }
    }
}
