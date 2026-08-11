using System.Collections.Generic;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Accumulates triangles for one submesh.
    ///
    /// Flat-shaded throughout: every face gets its own four vertices rather than
    /// sharing corners. That quadruples the vertex count and is still the right
    /// call here -- shared corners would average normals across a 90 degree
    /// edge, so every building corner would read as a soft round bevel. The
    /// reference is hard concrete edges.
    ///
    /// UVs are in METRES, not normalised. A tiling texture applied later then
    /// reads at real-world scale on every surface automatically, whatever size
    /// the building is, instead of stretching to fit.
    /// </summary>
    public sealed class MeshBuffer
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();

        public bool IsEmpty => Triangles.Count == 0;

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Uvs.Clear();
            Triangles.Clear();
        }

        /// <summary>
        /// One quad, wound so its front face points along <paramref name="normal"/>.
        /// Corners must be given in order around the perimeter.
        /// </summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int start = Vertices.Count;

            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                Normals.Add(normal);
            }
            Uvs.Add(uvA);
            Uvs.Add(uvB);
            Uvs.Add(uvC);
            Uvs.Add(uvD);

            Triangles.Add(start);
            Triangles.Add(start + 1);
            Triangles.Add(start + 2);
            Triangles.Add(start);
            Triangles.Add(start + 2);
            Triangles.Add(start + 3);
        }

        /// <summary>
        /// An axis-aligned box. Bottom face is omitted -- everything in this
        /// city sits on ground or on another box, so it is never visible, and
        /// leaving it out is 2 triangles saved on every building in the world.
        /// </summary>
        public void AddBox(Vector3 min, Vector3 max, bool includeBottom = false)
        {
            float x0 = min.x, y0 = min.y, z0 = min.z;
            float x1 = max.x, y1 = max.y, z1 = max.z;
            float w = x1 - x0, h = y1 - y0, d = z1 - z0;

            // +Y
            AddQuad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1),
                    new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.up,
                    new Vector2(0, 0), new Vector2(0, d), new Vector2(w, d), new Vector2(w, 0));

            // -Z
            AddQuad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0),
                    new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.back,
                    new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));

            // +Z
            AddQuad(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1),
                    new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), Vector3.forward,
                    new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));

            // -X
            AddQuad(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0),
                    new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), Vector3.left,
                    new Vector2(0, 0), new Vector2(d, 0), new Vector2(d, h), new Vector2(0, h));

            // +X
            AddQuad(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1),
                    new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.right,
                    new Vector2(0, 0), new Vector2(d, 0), new Vector2(d, h), new Vector2(0, h));

            if (includeBottom)
            {
                AddQuad(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1),
                        new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), Vector3.down,
                        new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, d), new Vector2(0, d));
            }
        }
    }
}
