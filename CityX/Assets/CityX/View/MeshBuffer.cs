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
        /// Corners must be given in order around the perimeter, but EITHER WAY
        /// ROUND -- the winding is corrected here rather than trusted.
        ///
        /// IT USED TO BE TRUSTED, AND FIVE OF ADDBOX'S SIX FACES WERE BACKWARDS.
        /// Only the roof was right, so every wall, kerb and walkway deck in the
        /// city was rasterised inside out. That is close to invisible on a
        /// closed box -- you see the far wall's inner surface, which fills the
        /// same silhouette -- but it lights each face by a normal pointing away
        /// from the camera, and it puts the far wall's depth in the buffer, so
        /// anything INSIDE a building draws in front of the building. The
        /// convention was settled by rendering a quad each way round with
        /// back-face culling on: the visible one is the one whose
        /// Cross(b - a, c - a) points at the camera.
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

            // EACH TRIANGLE CHECKED AND FIXED ON ITS OWN, not one shared
            // correction assumed to cover both. The quad used to correct
            // winding once (swapping b and d) and trust that the same swap
            // fixed BOTH triangles -- true for an ordinary planar
            // quadrilateral, but false for a skewed one (four corners that
            // are not coplanar, e.g. a sloped-street bevel segment whose
            // near and far edges sit at different heights): the two
            // triangles can disagree about which way is "out," and a single
            // swap can straighten one while leaving the other backwards
            // (caught by EveryFaceIsWoundTheWayItsNormalClaims). Reversing
            // just the INDEX order of whichever triangle needs it -- rather
            // than swapping vertex DATA the other triangle also reads from
            // -- fixes either or both independently without touching the
            // vertices or UVs at all.
            AddWoundTriangle(start, start + 1, start + 2, a, b, c, normal);
            AddWoundTriangle(start, start + 2, start + 3, a, c, d, normal);
        }

        private void AddWoundTriangle(int ia, int ib, int ic, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0.0f)
            {
                (ia, ib) = (ib, ia);
            }
            Triangles.Add(ia);
            Triangles.Add(ib);
            Triangles.Add(ic);
        }

        /// <summary>
        /// An axis-aligned box. Bottom face is omitted -- everything in this
        /// city sits on ground or on another box, so it is never visible, and
        /// leaving it out is 2 triangles saved on every building in the world.
        /// </summary>
        public void AddBox(Vector3 min, Vector3 max, bool includeBottom = false)
        {
            AddBox(min, max, this, includeBottom);
        }

        /// <summary>
        /// A box whose TOP face goes into a different buffer.
        ///
        /// Buildings need this: a roof and a wall are not the same surface, and
        /// putting the window texture on the top of every building is glaring
        /// the moment you get above roof height.
        /// </summary>
        public void AddBox(Vector3 min, Vector3 max, MeshBuffer topBuffer, bool includeBottom = false)
        {
            float x0 = min.x, y0 = min.y, z0 = min.z;
            float x1 = max.x, y1 = max.y, z1 = max.z;
            float w = x1 - x0, h = y1 - y0, d = z1 - z0;

            // +Y
            topBuffer.AddQuad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1),
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
