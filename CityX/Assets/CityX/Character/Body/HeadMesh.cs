using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The head: its own sphere, sitting on the neck, not part of the body.
    /// Ported from character.gd's _generate_sphere_verts / _sphere_point.
    ///
    /// NOT FUSED INTO THE BODY, WHICH THIS PORT GOT WRONG ONCE ALREADY.
    /// fused_body.gd's own limb_defs() has a torso, two arms, two legs and a
    /// neck in it, and no head at all -- the head has never been part of that
    /// shape. Blending it in gave a single continuous peanut with no neck,
    /// and it took the head's paint row with it. Keeping it separate is also
    /// what lets the head turn freely: it slides through the torso instead of
    /// dragging a shared skin around with it.
    ///
    /// EQUIRECTANGULAR, ACROSS THE WHOLE ROW. A sphere has no discrete faces,
    /// so unlike a box it is not split into per-face columns: longitude runs
    /// the full width of its atlas row and latitude runs the height.
    /// </summary>
    public static class HeadMesh
    {
        /// <summary>
        /// character.gd's own LAT_SEGMENTS was 8 -- fine with Godot's own
        /// lighting, but reported here as visibly faceted creases across
        /// the head, both in the surface shading and the silhouette
        /// (smooth per-vertex normals fix the shading gradient WITHIN a
        /// triangle, never the polygon edges themselves, and at 18cm a
        /// panel on a 70cm-diameter sphere those edges are real). Raised
        /// well past parity since the ask was "make it smooth", not "match
        /// Godot's own faceting" -- the paint UV mapping (u = lon/
        /// LongitudeSegments, v scaled the same way) is proportional, so
        /// this is a pure resolution increase with nothing else to retune.
        /// </summary>
        public const int LatitudeSegments = 24;

        /// <summary>See LatitudeSegments -- character.gd's own LON_SEGMENTS was 12.</summary>
        public const int LongitudeSegments = 36;

        /// <summary>
        /// Where the head's centre sits relative to the head BONE.
        ///
        /// Godot hangs the head off a pivot at the neck's own top end and
        /// offsets the mesh up by half its height, so it sits ON the neck
        /// rather than orbiting in place when it tilts. This rig's head bone
        /// is already at the middle of the skull -- Unity's humanoid rig wants
        /// it there -- and the two land the head in the same place: Godot's
        /// centre is 1.901 + 0.35, and this one is 1.9 + 0.35.
        /// </summary>
        public static readonly Vector3 CentreOnBone = Vector3.zero;

        public static Mesh Build()
        {
            return Build(FusedBodyShape.HeadRadius);
        }

        public static Mesh Build(float radius)
        {
            int row = PaintAtlas.RowOf(HumanBodyBones.Head);
            float v0 = (float)row / PaintAtlas.PartCount + PaintAtlas.RowMargin;
            float v1 = (float)(row + 1) / PaintAtlas.PartCount - PaintAtlas.RowMargin;

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int lat = 0; lat < LatitudeSegments; lat++)
            {
                for (int lon = 0; lon < LongitudeSegments; lon++)
                {
                    Corner c00 = At(lat, lon, radius, v0, v1);
                    Corner c01 = At(lat, lon + 1, radius, v0, v1);
                    Corner c11 = At(lat + 1, lon + 1, radius, v0, v1);
                    Corner c10 = At(lat + 1, lon, radius, v0, v1);

                    Emit(positions, normals, uvs, triangles, c00, c01, c11);
                    Emit(positions, normals, uvs, triangles, c00, c11, c10);
                }
            }

            var mesh = new Mesh { name = "CityXHead" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private readonly struct Corner
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector2 Uv;

            public Corner(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                Uv = uv;
            }
        }

        private static Corner At(int lat, int lon, float radius, float v0, float v1)
        {
            float theta = lat / (float)LatitudeSegments * Mathf.PI;
            float phi = lon / (float)LongitudeSegments * Mathf.PI * 2.0f;
            float sinTheta = Mathf.Sin(theta);

            var position = new Vector3(
                radius * sinTheta * Mathf.Cos(phi),
                radius * Mathf.Cos(theta),
                radius * sinTheta * Mathf.Sin(phi));

            Vector3 normal = position.sqrMagnitude > 0.0001f
                ? position.normalized
                : Vector3.up;

            float u = lon / (float)LongitudeSegments;
            float vLocal = lat / (float)LatitudeSegments;
            return new Corner(position, normal, new Vector2(u, v0 + vLocal * (v1 - v0)));
        }

        /// <summary>
        /// One triangle, wound so it faces outward.
        ///
        /// PER TRIANGLE, NOT PER QUAD, which is character.gd's own hard-won
        /// note: at the poles two of a quad's corners collapse onto the same
        /// point, so deciding the winding from that quad's first triangle
        /// reads a zero-area cross product and silently skips the check --
        /// leaving the other, real triangle facing inward. That is a hole at
        /// the top of the head.
        /// </summary>
        private static void Emit(List<Vector3> positions, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Corner a, Corner b, Corner c)
        {
            Vector3 geometric = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            if (geometric.sqrMagnitude <= 1e-7f)
            {
                return;   // a degenerate pole wedge: nothing to draw
            }

            Vector3 outward = ((a.Normal + b.Normal + c.Normal) / 3.0f).normalized;
            bool flip = Vector3.Dot(geometric.normalized, outward) < 0.0f;

            int first = positions.Count;
            Add(positions, normals, uvs, a);
            Add(positions, normals, uvs, flip ? c : b);
            Add(positions, normals, uvs, flip ? b : c);

            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        private static void Add(List<Vector3> positions, List<Vector3> normals,
            List<Vector2> uvs, Corner corner)
        {
            positions.Add(corner.Position);
            normals.Add(corner.Normal);
            uvs.Add(corner.Uv);
        }
    }
}
