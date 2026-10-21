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
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Triangles = new List<int>();

        /// <summary>
        /// The look stamped on every vertex added from now on (see <see cref="SurfaceLook"/>). The default is the look a mesh with no
        /// vertex colours at all gets, so a buffer nobody styles renders exactly as it always did.
        /// </summary>
        public Color32 Look = SurfaceLook.Default;

        /// <summary>Added to every UV written from now on, so two buildings of the same size do not wear the same brick pattern.</summary>
        public Vector2 UvOffset;

        public bool IsEmpty => Triangles.Count == 0;

        // ---- an optional frame, so a building built in its own unturned frame lands turned in the chunk ----

        private bool _hasFrame;
        private float _frameCos = 1.0f;
        private float _frameSin;
        private Vector3 _frameOffset;

        /// <summary>
        /// Everything added from now until <see cref="ClearFrame"/> is turned and moved into the chunk: a point (x, y, z) in the
        /// building's own frame is written as (x*axisX - z*axisZ, y, x*axisZ + z*axisX) + offset, and normals are turned with it.
        /// A pure turn about the vertical, so winding is untouched. This is how the whole of the building generator -- which
        /// thinks in axis-aligned boxes -- builds a building that stands at an angle.
        /// </summary>
        public void SetFrame(Vector3 offset, double axisX, double axisZ)
        {
            _hasFrame = true;
            _frameCos = (float)axisX;
            _frameSin = (float)axisZ;
            _frameOffset = offset;
        }

        public void ClearFrame()
        {
            _hasFrame = false;
            _frameCos = 1.0f;
            _frameSin = 0.0f;
            _frameOffset = Vector3.zero;
        }

        /// <summary>A point in the current frame as a chunk-local point (the identity when no frame is set).</summary>
        public Vector3 FramePoint(Vector3 v)
        {
            if (!_hasFrame) return v;
            return new Vector3(v.x * _frameCos - v.z * _frameSin + _frameOffset.x, v.y + _frameOffset.y,
                v.x * _frameSin + v.z * _frameCos + _frameOffset.z);
        }

        /// <summary>A direction in the current frame as a chunk-local direction.</summary>
        public Vector3 FrameDirection(Vector3 v)
        {
            if (!_hasFrame) return v;
            return new Vector3(v.x * _frameCos - v.z * _frameSin, v.y, v.x * _frameSin + v.z * _frameCos);
        }

        /// <summary>One triangle, wound so its front face points along <paramref name="normal"/>.</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Vector2 uvA, Vector2 uvB, Vector2 uvC)
        {
            a = FramePoint(a);
            b = FramePoint(b);
            c = FramePoint(c);
            normal = FrameDirection(normal);
            int start = Vertices.Count;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
            Uvs.Add(uvA + UvOffset);
            Uvs.Add(uvB + UvOffset);
            Uvs.Add(uvC + UvOffset);
            Colors.Add(Look);
            Colors.Add(Look);
            Colors.Add(Look);
            AddWoundTriangle(start, start + 1, start + 2, a, b, c, normal);
        }

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Uvs.Clear();
            Colors.Clear();
            Triangles.Clear();
        }

        /// <summary>
        /// Back to exactly what a new buffer is -- empty, the default look, no UV offset, no frame -- but its lists keep the room they grew
        /// to: the streamer builds chunk after chunk in the same buffers (2026-10-14), and growing a block of towers' lists from nothing
        /// every time was most of the garbage streaming made.
        /// </summary>
        public void Reset()
        {
            Clear();
            Look = SurfaceLook.Default;
            UvOffset = Vector2.zero;
            ClearFrame();
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
            if (_hasFrame)
            {
                a = FramePoint(a);
                b = FramePoint(b);
                c = FramePoint(c);
                d = FramePoint(d);
                normal = FrameDirection(normal);
            }
            int start = Vertices.Count;

            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                Normals.Add(normal);
            }
            Uvs.Add(uvA + UvOffset);
            Uvs.Add(uvB + UvOffset);
            Uvs.Add(uvC + UvOffset);
            Uvs.Add(uvD + UvOffset);
            for (int i = 0; i < 4; i++)
            {
                Colors.Add(Look);
            }

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
