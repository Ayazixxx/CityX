using System.Collections.Generic;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The broken-bottle silhouette: glass_bottle.gd's own procedural
    /// jagged-rim shape, ported. Pure mesh math, no Editor dependency, same
    /// shape as HeadMesh -- a body builds the vertex/triangle lists, a
    /// caller in Editor code wraps it in a real Mesh asset.
    /// </summary>
    public static class BottleMesh
    {
        private const int Sides = 8;
        private const float BodyRadiusFraction = 0.32f;
        private const float BrokenHeightFraction = 0.65f;

        /// <summary>
        /// A tapered cylinder body cut short, ending in a ring of
        /// fixed-height jagged shards rather than a smooth rim -- matching
        /// glass_bottle.gd's own non-random shard_heights list, so the
        /// broken shape is stable rather than reshuffling on every rebuild.
        /// </summary>
        private static readonly float[] ShardHeights = { 0.55f, 0.20f, 0.45f, 0.10f, 0.50f, 0.25f, 0.40f, 0.15f };

        /// <summary>
        /// The whole, unbroken bottle: a tapered body, a shoulder, a
        /// narrower neck, and a rim -- built procedurally rather than from
        /// bottle_model.glb, because no glTF importer is installed in this
        /// project (every .glb here, not just this one, sits on Unity's
        /// inert DefaultImporter and has never actually loaded as
        /// geometry -- flagged separately, not something to silently fix
        /// by adding a package dependency mid-feature).
        /// </summary>
        public static Mesh BuildIntact(float bodyHeight, float shoulderHeight, float neckHeight,
            float rimHeight, float bodyRadius, float neckRadius)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float bodyTopY = bodyHeight;
            float shoulderTopY = bodyTopY + shoulderHeight;
            float neckTopY = shoulderTopY + neckHeight;
            float rimTopY = neckTopY + rimHeight;

            Vector3[] baseRing = Ring(Sides, bodyRadius, 0.0f);
            Vector3[] bodyTopRing = Ring(Sides, bodyRadius, bodyTopY);
            Vector3[] shoulderTopRing = Ring(Sides, neckRadius, shoulderTopY);
            Vector3[] neckTopRing = Ring(Sides, neckRadius, neckTopY);
            Vector3[] rimTopRing = Ring(Sides, neckRadius * 1.15f, rimTopY);

            EmitBand(positions, normals, uvs, triangles, baseRing, bodyTopRing);
            EmitBand(positions, normals, uvs, triangles, bodyTopRing, shoulderTopRing);
            EmitBand(positions, normals, uvs, triangles, shoulderTopRing, neckTopRing);
            EmitBand(positions, normals, uvs, triangles, neckTopRing, rimTopRing);

            Vector3 baseCentre = Vector3.zero;
            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                Emit(positions, normals, uvs, triangles, baseCentre, baseRing[next], baseRing[i]);
            }
            Vector3 rimCentre = new Vector3(0.0f, rimTopY, 0.0f);
            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                Emit(positions, normals, uvs, triangles, rimCentre, rimTopRing[i], rimTopRing[next]);
            }

            var mesh = new Mesh { name = "IntactBottle" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] Ring(int sides, float radius, float y)
        {
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2.0f;
                ring[i] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            }
            return ring;
        }

        private static void EmitBand(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3[] lower, Vector3[] upper)
        {
            int sides = lower.Length;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                EmitQuad(positions, normals, uvs, triangles, lower[i], lower[next], upper[next], upper[i]);
            }
        }

        public static Mesh BuildBroken(float intactHeight)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float radius = intactHeight * BodyRadiusFraction;
            float bodyHeight = intactHeight * BrokenHeightFraction;
            float shardSpan = intactHeight * BrokenHeightFraction * 0.5f;

            var basePoints = new Vector3[Sides];
            var rimPoints = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float angle = i / (float)Sides * Mathf.PI * 2.0f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                basePoints[i] = new Vector3(x, 0.0f, z);
                rimPoints[i] = new Vector3(x, bodyHeight + ShardHeights[i % ShardHeights.Length] * shardSpan, z);
            }

            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                EmitQuad(positions, normals, uvs, triangles,
                    basePoints[i], basePoints[next], rimPoints[next], rimPoints[i]);
            }

            // A flat base cap so the broken bottle doesn't read as hollow
            // from below when it's lying on the ground pre-pickup.
            Vector3 baseCentre = Vector3.zero;
            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                Emit(positions, normals, uvs, triangles, baseCentre, basePoints[next], basePoints[i]);
            }

            var mesh = new Mesh { name = "BrokenBottle" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EmitQuad(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Emit(positions, normals, uvs, triangles, a, b, c);
            Emit(positions, normals, uvs, triangles, a, c, d);
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
