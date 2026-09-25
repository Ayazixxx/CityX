using System.Collections.Generic;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A handful of angular, jagged shard shapes for a shattered window --
    /// small triangular PRISMS with a little real thickness, not flat unlit
    /// cards. A billboard sprite reads fine from an establishing shot but
    /// vanishes edge-on and never catches light as it tumbles; a real solid
    /// fragment does both, which is the whole difference between a sparkle
    /// effect and something that reads as broken GLASS the way the user's
    /// own Garry's Mod/Half-Life reference does.
    ///
    /// HAND-AUTHORED SILHOUETTES, NOT RANDOMISED AT RUNTIME -- the same
    /// reason BottleMesh's own ShardHeights are a fixed array: a rebuild
    /// reproduces the exact same shapes rather than reshuffling them.
    /// </summary>
    public static class GlassShardMesh
    {
        public const int ShapeCount = 6;

        // Each shape is one small, deliberately irregular triangle in local
        // XY, roughly centred on the origin so a shard scales and tumbles
        // about its own rough middle rather than one far corner.
        private static readonly Vector2[][] Shapes =
        {
            new[] { new Vector2(-0.50f, -0.35f), new Vector2(0.60f, -0.20f), new Vector2(-0.10f, 0.55f) },
            new[] { new Vector2(-0.40f, -0.50f), new Vector2(0.45f, -0.45f), new Vector2(0.15f, 0.40f) },
            new[] { new Vector2(-0.55f, 0.10f), new Vector2(0.20f, -0.50f), new Vector2(0.45f, 0.35f) },
            new[] { new Vector2(-0.20f, -0.50f), new Vector2(0.55f, 0.05f), new Vector2(-0.35f, 0.45f) },
            new[] { new Vector2(-0.50f, -0.15f), new Vector2(0.10f, -0.55f), new Vector2(0.40f, 0.50f) },
            new[] { new Vector2(-0.30f, -0.45f), new Vector2(0.55f, -0.10f), new Vector2(-0.15f, 0.50f) },
        };

        /// <summary>
        /// One shard, `size` metres across its own longest span and
        /// `thickness` metres deep -- a real prism: two triangular caps and
        /// three rectangular edges, every face doubled front-and-back so a
        /// thin fragment never disappears when the camera catches it edge-on
        /// or from whichever side happens to be "inside" by this method's
        /// own arbitrary winding (the same edge-on blind spot this project's
        /// own particle work has hit before, avoided here for good instead
        /// of hand-deriving Unity's winding convention for six one-off
        /// shapes).
        /// </summary>
        public static Mesh Build(int shapeIndex, float size, float thickness)
        {
            Vector2[] shape = Shapes[((shapeIndex % Shapes.Length) + Shapes.Length) % Shapes.Length];
            float half = thickness * 0.5f;

            Vector3 a = new Vector3(shape[0].x * size, shape[0].y * size, -half);
            Vector3 b = new Vector3(shape[1].x * size, shape[1].y * size, -half);
            Vector3 c = new Vector3(shape[2].x * size, shape[2].y * size, -half);
            Vector3 a2 = new Vector3(shape[0].x * size, shape[0].y * size, half);
            Vector3 b2 = new Vector3(shape[1].x * size, shape[1].y * size, half);
            Vector3 c2 = new Vector3(shape[2].x * size, shape[2].y * size, half);

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            EmitDoubleSided(positions, normals, uvs, triangles, a, b, c);
            EmitDoubleSided(positions, normals, uvs, triangles, a2, c2, b2);

            EmitQuadDoubleSided(positions, normals, uvs, triangles, a, b, b2, a2);
            EmitQuadDoubleSided(positions, normals, uvs, triangles, b, c, c2, b2);
            EmitQuadDoubleSided(positions, normals, uvs, triangles, c, a, a2, c2);

            var mesh = new Mesh { name = $"GlassShard{shapeIndex}" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EmitQuadDoubleSided(List<Vector3> positions, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            EmitDoubleSided(positions, normals, uvs, triangles, a, b, c);
            EmitDoubleSided(positions, normals, uvs, triangles, a, c, d);
        }

        private static void EmitDoubleSided(List<Vector3> positions, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            Emit(positions, normals, uvs, triangles, a, b, c);
            Emit(positions, normals, uvs, triangles, a, c, b);
        }

        private static void Emit(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int first = positions.Count;
            positions.Add(a);
            positions.Add(b);
            positions.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.right);
            uvs.Add(Vector2.up);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }
    }
}
