using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The shapes gore is built from (docs/DISMEMBERMENT.md), in code and closed all round: a turned shape round an axis (a bone), a tube along a
    /// path (a rib, a gut, a jaw), a lumpy ball (an organ, a piece of meat), a ball split into bands by height (an eye). Each takes the submesh
    /// its triangles go in, so one mesh can carry bone and marrow, or white, iris and pupil. Pure, so tests can measure them.
    /// </summary>
    public static class GoreMeshes
    {
        /// <summary>A mesh being put together: corners, and triangles in submeshes.</summary>
        public sealed class Builder
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int>[] Submeshes;

            public Builder(int submeshes)
            {
                Submeshes = new List<int>[Mathf.Max(1, submeshes)];
                for (int i = 0; i < Submeshes.Length; i++)
                {
                    Submeshes[i] = new List<int>();
                }
            }

            public int Add(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Positions.Add(position);
                Normals.Add(normal);
                Uvs.Add(uv);
                return Positions.Count - 1;
            }

            public void Triangle(int submesh, int a, int b, int c)
            {
                List<int> into = Submeshes[Mathf.Clamp(submesh, 0, Submeshes.Length - 1)];
                into.Add(a);
                into.Add(b);
                into.Add(c);
            }

            /// <summary>Every corner moved so the middle of what is built sits at the origin (where a thrown piece turns about).</summary>
            public void Recentre()
            {
                if (Positions.Count == 0)
                {
                    return;
                }
                Vector3 min = Positions[0], max = Positions[0];
                foreach (Vector3 p in Positions)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                Vector3 middle = (min + max) * 0.5f;
                for (int i = 0; i < Positions.Count; i++)
                {
                    Positions[i] -= middle;
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.indexFormat = Positions.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(Positions);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, Uvs);
                mesh.subMeshCount = Submeshes.Length;
                for (int i = 0; i < Submeshes.Length; i++)
                {
                    mesh.SetTriangles(Submeshes[i], i, false);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// A shape turned round the Y axis through <paramref name="rings"/> (height, radius) from bottom to top, <paramref name="sides"/>
        /// round, closed by a flat cap at each end (into <paramref name="bottomCap"/> and <paramref name="topCap"/>'s submeshes). A ring's
        /// radius is pushed in and out by <paramref name="jag"/> (0 to 1) round it, for a broken end.
        /// </summary>
        public static void Lathe(Builder into, int submesh, IList<(float y, float r, float jag)> rings, int sides, int bottomCap,
            int topCap, System.Random random)
        {
            var rows = new List<int[]>();
            var points = new List<Vector3[]>();
            foreach ((float y, float r, float jag) in rings)
            {
                var ring = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2.0f / sides;
                    float lift = jag > 0.0f ? (float)(random.NextDouble() * 2.0 - 1.0) * jag * r : 0.0f;
                    ring[i] = new Vector3(Mathf.Cos(angle) * r, y + lift, Mathf.Sin(angle) * r);
                }
                points.Add(ring);
            }
            for (int k = 0; k < points.Count; k++)
            {
                var row = new int[sides];
                for (int i = 0; i < sides; i++)
                {
                    Vector3 p = points[k][i];
                    Vector3 below = points[Mathf.Max(0, k - 1)][i], above = points[Mathf.Min(points.Count - 1, k + 1)][i];
                    Vector3 along = above - below;
                    Vector3 outward = new Vector3(p.x, 0.0f, p.z);
                    Vector3 normal = outward.sqrMagnitude > 1e-12f ? (outward.normalized - along.normalized * Vector3.Dot(outward.normalized, along.normalized)) : Vector3.up;
                    row[i] = into.Add(p, normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up, new Vector2(i / (float)sides, k / (float)points.Count));
                }
                rows.Add(row);
            }
            for (int k = 0; k + 1 < rows.Count; k++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    into.Triangle(submesh, rows[k][i], rows[k + 1][i], rows[k + 1][j]);
                    into.Triangle(submesh, rows[k][i], rows[k + 1][j], rows[k][j]);
                }
            }
            Cap(into, bottomCap, points[0], down: true);
            Cap(into, topCap, points[points.Count - 1], down: false);
        }

        /// <summary>A flat fan over a ring, facing down or up, with its own corners (a hard edge round it).</summary>
        private static void Cap(Builder into, int submesh, Vector3[] ring, bool down)
        {
            Vector3 middle = Vector3.zero;
            foreach (Vector3 p in ring)
            {
                middle += p;
            }
            middle /= ring.Length;
            Vector3 normal = down ? Vector3.down : Vector3.up;
            int centre = into.Add(middle, normal, new Vector2(0.5f, 0.5f));
            var corners = new int[ring.Length];
            for (int i = 0; i < ring.Length; i++)
            {
                corners[i] = into.Add(ring[i], normal, Vector2.zero);
            }
            for (int i = 0; i < ring.Length; i++)
            {
                int j = (i + 1) % ring.Length;
                if (down)
                {
                    into.Triangle(submesh, centre, corners[i], corners[j]);
                }
                else
                {
                    into.Triangle(submesh, centre, corners[j], corners[i]);
                }
            }
        }

        /// <summary>
        /// A tube <paramref name="radius"/> thick along <paramref name="path"/> (<paramref name="taper"/>: its radius at the far end over the
        /// near), <paramref name="sides"/> round, its ends closed.
        /// </summary>
        public static void Tube(Builder into, int submesh, IList<Vector3> path, float radius, float taper, int sides)
        {
            int count = path.Count;
            if (count < 2)
            {
                return;
            }
            var rings = new Vector3[count][];
            Vector3 previousSide = Vector3.zero;
            for (int k = 0; k < count; k++)
            {
                Vector3 along = (path[Mathf.Min(count - 1, k + 1)] - path[Mathf.Max(0, k - 1)]).normalized;
                Vector3 side = k == 0 ? Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                    : (previousSide - along * Vector3.Dot(previousSide, along)).normalized;
                previousSide = side;
                Vector3 up = Vector3.Cross(side, along);
                float r = radius * Mathf.Lerp(1.0f, taper, k / (float)(count - 1));
                rings[k] = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2.0f / sides;
                    rings[k][i] = path[k] + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * r;
                }
            }
            var rows = new int[count][];
            for (int k = 0; k < count; k++)
            {
                rows[k] = new int[sides];
                for (int i = 0; i < sides; i++)
                {
                    Vector3 normal = (rings[k][i] - path[k]).normalized;
                    rows[k][i] = into.Add(rings[k][i], normal, new Vector2(i / (float)sides, k / (float)count));
                }
            }
            for (int k = 0; k + 1 < count; k++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    into.Triangle(submesh, rows[k][i], rows[k + 1][j], rows[k][j]);
                    into.Triangle(submesh, rows[k][i], rows[k + 1][i], rows[k + 1][j]);
                }
            }
            EndCap(into, submesh, rings[0], path[0], -(path[1] - path[0]).normalized);
            EndCap(into, submesh, rings[count - 1], path[count - 1], (path[count - 1] - path[count - 2]).normalized);
        }

        private static void EndCap(Builder into, int submesh, Vector3[] ring, Vector3 middle, Vector3 facing)
        {
            int centre = into.Add(middle, facing, new Vector2(0.5f, 0.5f));
            var corners = new int[ring.Length];
            for (int i = 0; i < ring.Length; i++)
            {
                corners[i] = into.Add(ring[i], facing, Vector2.zero);
            }
            for (int i = 0; i < ring.Length; i++)
            {
                int j = (i + 1) % ring.Length;
                Vector3 normal = Vector3.Cross(ring[i] - middle, ring[j] - middle);
                if (Vector3.Dot(normal, facing) >= 0.0f)
                {
                    into.Triangle(submesh, centre, corners[i], corners[j]);
                }
                else
                {
                    into.Triangle(submesh, centre, corners[j], corners[i]);
                }
            }
        }

        /// <summary>
        /// A ball of <paramref name="size"/> (its half-widths), its surface pushed in and out by noise of <paramref name="bumps"/> (0 smooth) at
        /// <paramref name="frequency"/>, placed at <paramref name="at"/>. Each triangle goes in the submesh <paramref name="band"/> picks from
        /// the unit direction of its middle (null: all in <paramref name="submesh"/>).
        /// </summary>
        public static void Ball(Builder into, int submesh, Vector3 at, Vector3 size, float bumps, float frequency, float seed,
            int rings = 8, int sides = 12, System.Func<Vector3, int> band = null)
        {
            var rows = new int[rings + 1][];
            var unit = new Vector3[rings + 1][];
            for (int k = 0; k <= rings; k++)
            {
                float polar = Mathf.PI * k / rings;
                rows[k] = new int[sides + 1];
                unit[k] = new Vector3[sides + 1];
                for (int i = 0; i <= sides; i++)
                {
                    float azimuth = Mathf.PI * 2.0f * (i % sides) / sides;
                    Vector3 d = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    if (k == 0 || k == rings)
                    {
                        d = new Vector3(0.0f, d.y > 0.0f ? 1.0f : -1.0f, 0.0f);
                    }
                    float push = 1.0f + bumps * (Mathf.PerlinNoise(d.x * frequency + seed, d.y * frequency + d.z * frequency * 0.7f + seed * 1.3f) - 0.5f) * 2.0f;
                    Vector3 p = Vector3.Scale(d, size) * push;
                    unit[k][i] = d;
                    rows[k][i] = into.Add(at + p, new Vector3(d.x / Mathf.Max(size.x, 1e-6f), d.y / Mathf.Max(size.y, 1e-6f), d.z / Mathf.Max(size.z, 1e-6f)).normalized,
                        new Vector2(i / (float)sides, k / (float)rings));
                }
            }
            for (int k = 0; k < rings; k++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int a = rows[k][i], b = rows[k][i + 1], c = rows[k + 1][i + 1], d = rows[k + 1][i];
                    int sub = band != null ? band((unit[k][i] + unit[k][i + 1] + unit[k + 1][i + 1] + unit[k + 1][i]).normalized) : submesh;
                    if (k > 0)
                    {
                        into.Triangle(sub, a, b, d);
                    }
                    if (k < rings - 1)
                    {
                        into.Triangle(sub, b, c, d);
                    }
                }
            }
        }

        /// <summary>A box of <paramref name="size"/> at <paramref name="at"/>, turned by <paramref name="turn"/>, with hard edges.</summary>
        public static void Box(Builder into, int submesh, Vector3 at, Vector3 size, Quaternion turn)
        {
            Vector3 h = size * 0.5f;
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in faces)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 centre = Vector3.Scale(n, h);
                Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                Vector3 normal = turn * n;
                int a = into.Add(at + turn * (centre - du - dv), normal, Vector2.zero);
                int b = into.Add(at + turn * (centre - du + dv), normal, Vector2.up);
                int c = into.Add(at + turn * (centre + du + dv), normal, Vector2.one);
                int d = into.Add(at + turn * (centre + du - dv), normal, Vector2.right);
                into.Triangle(submesh, a, c, b);
                into.Triangle(submesh, a, d, c);
            }
        }
    }
}
