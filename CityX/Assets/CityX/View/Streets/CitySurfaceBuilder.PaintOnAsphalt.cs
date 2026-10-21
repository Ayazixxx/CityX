using System.Collections.Generic;
using UnityEngine;

namespace CityX.View
{
    public static partial class CitySurfaceBuilder
    {
        /// <summary>How far paint laid on the asphalt stands off it: too little to see, enough to always draw over it (a 32-bit depth buffer).</summary>
        public const float PaintOnAsphaltLift = 0.01f;

        /// <summary>Asphalt this steep or steeper is a skirt down a road's edge, not a surface paint lies on.</summary>
        private const float PaintSurfaceMinUp = 0.6f;

        /// <summary>The grid the asphalt's triangles are found by, metres.</summary>
        private const float PaintGridCell = 4.0f;

        /// <summary>A cut piece's triangle smaller than this (twice its area, square metres: about a square centimetre) is left out.</summary>
        private const float SliverDoubleArea = 2e-4f;

        private struct AsphaltTriangle
        {
            public Vector2 A, B, C;
            public Vector3 Origin;
            public Vector3 Normal;
            public float MinX, MinZ, MaxX, MaxZ;

            /// <summary>The triangle's own plane, its height at (x, z).</summary>
            public float HeightAt(float x, float z) => Origin.y - (Normal.x * (x - Origin.x) + Normal.z * (z - Origin.z)) / Normal.y;
        }

        /// <summary>
        /// LAYS THE PAINT ON THE ASPHALT IT MARKS (owner, 2026-10-17: "the street decals are often loading in glitching into the terrain. i want
        /// them to be apart of the texture of the street not hovering above or glitching into the terrain"). A stripe was a flat quad from the road's
        /// height at its own four corners, 3 cm up, while the road under it is cut into pieces of its own, each flat: wherever the road bent under
        /// a stripe, part of the stripe sank into it and part floated (RoadPaintTests counted 14 % of the paint buried). Now every paint triangle
        /// added since <paramref name="fromTriangle"/> (whose vertices start at <paramref name="fromVertex"/>) is cut along the asphalt's own
        /// triangles under it, and each piece set on that triangle's plane, <see cref="PaintOnAsphaltLift"/> above it: it bends exactly where the
        /// road bends. Where two surfaces overlap, each gets its piece and the upper one's shows. Paint over no asphalt (a mall's lot) is kept as it
        /// was. Pure maths on the buffers, safe off the main thread.
        /// </summary>
        private static void LayPaintOnAsphalt(MeshBuffer paint, int fromTriangle, int fromVertex, MeshBuffer asphalt, Vector3 origin)
        {
            int paintCount = paint != null && asphalt != null ? paint.Triangles.Count - fromTriangle : 0;
            if (paintCount <= 0 || asphalt.IsEmpty)
            {
                return;
            }

            // The asphalt's upward faces, filed in a grid by where they lie.
            var surfaces = new List<AsphaltTriangle>(asphalt.Triangles.Count / 3);
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int t = 0; t < asphalt.Triangles.Count; t += 3)
            {
                Vector3 a = asphalt.Vertices[asphalt.Triangles[t]], b = asphalt.Vertices[asphalt.Triangles[t + 1]], c = asphalt.Vertices[asphalt.Triangles[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                float length = n.magnitude;
                if (length < 1e-8f)
                {
                    continue;
                }
                n /= length;
                if (n.y < 0.0f)
                {
                    n = -n;
                }
                if (n.y < PaintSurfaceMinUp)
                {
                    continue;
                }
                var tri = new AsphaltTriangle
                {
                    A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), C = new Vector2(c.x, c.z), Origin = a, Normal = n,
                    MinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), MaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)),
                    MinZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), MaxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z)),
                };
                surfaces.Add(tri);
                minX = Mathf.Min(minX, tri.MinX);
                minZ = Mathf.Min(minZ, tri.MinZ);
                maxX = Mathf.Max(maxX, tri.MaxX);
                maxZ = Mathf.Max(maxZ, tri.MaxZ);
            }
            if (surfaces.Count == 0)
            {
                return;
            }
            int columns = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / PaintGridCell));
            int rows = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / PaintGridCell));
            var cells = new List<int>[columns * rows];
            for (int i = 0; i < surfaces.Count; i++)
            {
                AsphaltTriangle tri = surfaces[i];
                int x0 = Cell(tri.MinX, minX, columns), x1 = Cell(tri.MaxX, minX, columns);
                int z0 = Cell(tri.MinZ, minZ, rows), z1 = Cell(tri.MaxZ, minZ, rows);
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int k = z * columns + x;
                        (cells[k] ?? (cells[k] = new List<int>(4))).Add(i);
                    }
                }
            }

            // The paint as it was laid, taken off the buffer to be laid again.
            var corners = new Vector3[paintCount];
            var uvs = new Vector2[paintCount];
            var normals = new Vector3[paintCount];
            var colours = new Color32[paintCount];
            for (int i = 0; i < paintCount; i++)
            {
                int v = paint.Triangles[fromTriangle + i];
                corners[i] = paint.Vertices[v];
                uvs[i] = paint.Uvs[v];
                normals[i] = paint.Normals[v];
                colours[i] = paint.Colors[v];
            }
            paint.Triangles.RemoveRange(fromTriangle, paintCount);
            int dropVertices = paint.Vertices.Count - fromVertex;
            paint.Vertices.RemoveRange(fromVertex, dropVertices);
            paint.Normals.RemoveRange(fromVertex, dropVertices);
            paint.Uvs.RemoveRange(fromVertex, dropVertices);
            paint.Colors.RemoveRange(fromVertex, dropVertices);

            Color32 look = paint.Look;
            var stamp = new int[surfaces.Count];
            var piece = new List<Vector2>(8);
            var scratch = new List<Vector2>(8);
            var uvOrigin = new Vector2(origin.x, origin.z);
            for (int t = 0; t + 2 < paintCount; t += 3)
            {
                Vector3 a = corners[t], b = corners[t + 1], c = corners[t + 2];
                float pMinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), pMaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float pMinZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), pMaxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                paint.Look = colours[t];
                bool laid = false;
                int pass = t / 3 + 1;
                for (int z = Cell(pMinZ, minZ, rows); z <= Cell(pMaxZ, minZ, rows); z++)
                {
                    for (int x = Cell(pMinX, minX, columns); x <= Cell(pMaxX, minX, columns); x++)
                    {
                        List<int> here = cells[z * columns + x];
                        if (here == null)
                        {
                            continue;
                        }
                        foreach (int s in here)
                        {
                            if (stamp[s] == pass)
                            {
                                continue;
                            }
                            stamp[s] = pass;
                            AsphaltTriangle tri = surfaces[s];
                            if (tri.MaxX < pMinX || tri.MinX > pMaxX || tri.MaxZ < pMinZ || tri.MinZ > pMaxZ)
                            {
                                continue;
                            }
                            piece.Clear();
                            piece.Add(new Vector2(a.x, a.z));
                            piece.Add(new Vector2(b.x, b.z));
                            piece.Add(new Vector2(c.x, c.z));
                            if (!ClipToTriangle(piece, scratch, tri.A, tri.B, tri.C))
                            {
                                continue;
                            }
                            laid = true;
                            Vector3 p0 = OnSurface(piece[0], tri);
                            for (int i = 1; i + 1 < piece.Count; i++)
                            {
                                Vector2 u = piece[i] - piece[0], w = piece[i + 1] - piece[0];
                                if (Mathf.Abs(u.x * w.y - u.y * w.x) < SliverDoubleArea)
                                {
                                    continue;   // a sliver from the cutting: nothing to see, and its winding would be rounding noise
                                }
                                paint.AddTriangle(p0, OnSurface(piece[i], tri), OnSurface(piece[i + 1], tri), tri.Normal,
                                    piece[0] + uvOrigin, piece[i] + uvOrigin, piece[i + 1] + uvOrigin);
                            }
                        }
                    }
                }
                if (!laid)
                {
                    paint.AddTriangle(a, b, c, normals[t], uvs[t], uvs[t + 1], uvs[t + 2]);
                }
            }
            paint.Look = look;
        }

        private static int Cell(float value, float min, int count) => Mathf.Clamp((int)((value - min) / PaintGridCell), 0, count - 1);

        private static Vector3 OnSurface(Vector2 p, AsphaltTriangle tri) => new Vector3(p.x, tri.HeightAt(p.x, p.y) + PaintOnAsphaltLift, p.y);

        /// <summary>
        /// Cuts the convex polygon <paramref name="poly"/> down to what lies inside triangle a-b-c (either way round), in place. False when less
        /// than a sliver is left.
        /// </summary>
        private static bool ClipToTriangle(List<Vector2> poly, List<Vector2> scratch, Vector2 a, Vector2 b, Vector2 c)
        {
            float winding = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            if (winding == 0.0f)
            {
                return false;
            }
            ClipToEdge(poly, scratch, a, b, winding);
            ClipToEdge(poly, scratch, b, c, winding);
            ClipToEdge(poly, scratch, c, a, winding);
            if (poly.Count < 3)
            {
                return false;
            }
            float area = 0.0f;
            for (int i = 1; i + 1 < poly.Count; i++)
            {
                Vector2 u = poly[i] - poly[0], v = poly[i + 1] - poly[0];
                area += Mathf.Abs(u.x * v.y - u.y * v.x) * 0.5f;
            }
            return area > 1e-5f;
        }

        /// <summary>Keeps the part of <paramref name="poly"/> on the inner side of edge p-q (the side the triangle is on, by <paramref name="winding"/>).</summary>
        private static void ClipToEdge(List<Vector2> poly, List<Vector2> scratch, Vector2 p, Vector2 q, float winding)
        {
            scratch.Clear();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 current = poly[i], next = poly[(i + 1) % n];
                float dc = Side(p, q, current) * winding, dn = Side(p, q, next) * winding;
                if (dc >= 0.0f)
                {
                    scratch.Add(current);
                }
                if ((dc >= 0.0f) != (dn >= 0.0f))
                {
                    scratch.Add(Vector2.Lerp(current, next, dc / (dc - dn)));
                }
            }
            poly.Clear();
            poly.AddRange(scratch);
        }

        private static float Side(Vector2 p, Vector2 q, Vector2 x) => (q.x - p.x) * (x.y - p.y) - (q.y - p.y) * (x.x - p.x);
    }
}
