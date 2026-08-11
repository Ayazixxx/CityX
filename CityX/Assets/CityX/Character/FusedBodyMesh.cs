using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Turns <see cref="FusedBodyShape"/>'s distance field into an actual
    /// surface, by marching tetrahedra.
    ///
    /// TETRAHEDRA RATHER THAN CUBES, which is what the Godot original used and
    /// for the same reason: marching cubes needs a 256-entry lookup table that
    /// nobody can read, review or debug, and has ambiguous cases that produce
    /// holes. A cube split into six tetrahedra has four cases in total -- no
    /// corners inside, one, two, or three -- and every one of them is obvious.
    /// It costs more triangles, which for a character built once at import
    /// time is not a cost at all.
    ///
    /// Deliberately in the runtime assembly rather than an editor one: it is
    /// pure geometry, and it is the part most worth testing.
    /// </summary>
    public static class FusedBodyMesh
    {
        /// <summary>
        /// The six tetrahedra a unit cube splits into. Every one shares the
        /// cube's main diagonal (corner 0 to corner 7), which is what makes the
        /// decomposition consistent between neighbouring cubes -- if they
        /// disagreed, the surfaces would not meet and the body would be full of
        /// cracks.
        /// </summary>
        private static readonly int[][] Tetrahedra =
        {
            new[] { 0, 5, 1, 7 },
            new[] { 0, 1, 3, 7 },
            new[] { 0, 3, 2, 7 },
            new[] { 0, 2, 6, 7 },
            new[] { 0, 6, 4, 7 },
            new[] { 0, 4, 5, 7 },
        };

        /// <summary>The eight corners of a cube, in the order the table above indexes.</summary>
        private static readonly Vector3Int[] Corners =
        {
            new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1),
            new Vector3Int(0, 1, 1), new Vector3Int(1, 1, 1),
        };

        /// <summary>
        /// Extracts the surface. Returns positions and triangle indices with
        /// duplicate vertices welded, so the mesh is watertight and its normals
        /// can be smoothed across faces.
        /// </summary>
        public static void Extract(IReadOnlyList<FusedBodyShape.Segment> segments,
            Bounds bounds, float cellSize,
            out List<Vector3> vertices, out List<int> triangles)
        {
            vertices = new List<Vector3>();
            triangles = new List<int>();

            var index = new Dictionary<(Vector3Int, Vector3Int), int>();

            int countX = Mathf.CeilToInt(bounds.size.x / cellSize);
            int countY = Mathf.CeilToInt(bounds.size.y / cellSize);
            int countZ = Mathf.CeilToInt(bounds.size.z / cellSize);
            Vector3 origin = bounds.min;

            // The field is sampled on a plane at a time and the previous plane
            // is kept, so every corner is evaluated ONCE rather than eight
            // times. The distance function walks every capsule, so this is the
            // difference between a build that takes a moment and one that takes
            // most of a minute.
            int planeStride = (countY + 1);
            var previous = new float[planeStride * (countZ + 1)];
            var current = new float[planeStride * (countZ + 1)];

            SamplePlane(segments, origin, cellSize, 0, countY, countZ, previous);

            var cubeValues = new float[8];
            var cubePoints = new Vector3[8];
            var cubeKeys = new Vector3Int[8];

            for (int x = 0; x < countX; x++)
            {
                SamplePlane(segments, origin, cellSize, x + 1, countY, countZ, current);

                for (int y = 0; y < countY; y++)
                {
                    for (int z = 0; z < countZ; z++)
                    {
                        for (int c = 0; c < 8; c++)
                        {
                            Vector3Int corner = Corners[c];
                            var key = new Vector3Int(x + corner.x, y + corner.y, z + corner.z);
                            cubeKeys[c] = key;
                            cubePoints[c] = origin + (Vector3)key * cellSize;
                            float[] plane = corner.x == 0 ? previous : current;
                            cubeValues[c] = plane[(y + corner.y) * (countZ + 1) + (z + corner.z)];
                        }

                        foreach (int[] tet in Tetrahedra)
                        {
                            EmitTetrahedron(tet, cubeValues, cubePoints, cubeKeys,
                                vertices, triangles, index);
                        }
                    }
                }

                (previous, current) = (current, previous);
            }
        }

        private static void SamplePlane(IReadOnlyList<FusedBodyShape.Segment> segments,
            Vector3 origin, float cellSize, int x, int countY, int countZ, float[] into)
        {
            for (int y = 0; y <= countY; y++)
            {
                for (int z = 0; z <= countZ; z++)
                {
                    var p = origin + new Vector3(x, y, z) * cellSize;
                    into[y * (countZ + 1) + z] = FusedBodyShape.Distance(segments, p);
                }
            }
        }

        private static void EmitTetrahedron(int[] tet, float[] values, Vector3[] points,
            Vector3Int[] keys, List<Vector3> vertices, List<int> triangles,
            Dictionary<(Vector3Int, Vector3Int), int> index)
        {
            // "Inside" is negative, since that is what a signed distance means.
            int mask = 0;
            for (int i = 0; i < 4; i++)
            {
                if (values[tet[i]] < 0.0f)
                {
                    mask |= 1 << i;
                }
            }

            if (mask == 0 || mask == 0b1111)
            {
                return;   // wholly outside or wholly inside: no surface here
            }

            // The four corners sorted into inside and outside. Which is which
            // is all the case analysis this needs.
            var inside = new List<int>(4);
            var outside = new List<int>(4);
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0) inside.Add(tet[i]);
                else outside.Add(tet[i]);
            }

            // WHICH WAY IS OUT, FOR THIS TETRAHEDRON, from its own corners.
            // The surface separates the inside corners from the outside ones,
            // so the line between the two groups' centres crosses it going
            // outward -- always, for any planar cut of a tetrahedron.
            //
            // This replaces a pass that turned each finished triangle to agree
            // with the field's gradient at its own centre. That sounds safer
            // and is not: it decides each face ALONE, so two faces sharing an
            // edge could disagree, and one disagreeing face renders as a black
            // speck, because what you are seeing is its unlit back. About 1.8%
            // of edges were mismatched that way. Here neighbouring tetrahedra
            // classify their shared corners identically, so they cannot
            // disagree.
            Vector3 outward = Centre(points, outside) - Centre(points, inside);

            if (inside.Count == 1 || outside.Count == 1)
            {
                // A single corner cut off by the surface: one triangle across
                // the three edges leaving it.
                int lone = inside.Count == 1 ? inside[0] : outside[0];
                List<int> others = inside.Count == 1 ? outside : inside;

                int a = EdgeVertex(lone, others[0], values, points, keys, vertices, index);
                int b = EdgeVertex(lone, others[1], values, points, keys, vertices, index);
                int c = EdgeVertex(lone, others[2], values, points, keys, vertices, index);

                AddTriangle(triangles, vertices, a, b, c, outward);
            }
            else
            {
                // Two in, two out: the surface cuts the tet into a quad. The
                // order round it matters -- a and b share inside[0], b and c
                // share outside[1], c and d share inside[1], d and a share
                // outside[0] -- or the "quad" is a bowtie.
                int a = EdgeVertex(inside[0], outside[0], values, points, keys, vertices, index);
                int b = EdgeVertex(inside[0], outside[1], values, points, keys, vertices, index);
                int c = EdgeVertex(inside[1], outside[1], values, points, keys, vertices, index);
                int d = EdgeVertex(inside[1], outside[0], values, points, keys, vertices, index);

                AddTriangle(triangles, vertices, a, b, c, outward);
                AddTriangle(triangles, vertices, a, c, d, outward);
            }
        }

        private static Vector3 Centre(Vector3[] points, List<int> corners)
        {
            var sum = Vector3.zero;
            foreach (int corner in corners)
            {
                sum += points[corner];
            }
            return sum / corners.Count;
        }

        /// <summary>
        /// The point on an edge where the surface crosses zero, welded so both
        /// tetrahedra sharing that edge use the same vertex.
        ///
        /// KEYED ON THE PAIR OF CORNERS, exactly. Two earlier keys were wrong
        /// in ways worth recording. Rounding the float position produces cracks,
        /// because two tetrahedra can compute the same crossing to slightly
        /// different floats and land in different buckets. And adding the two
        /// corner coordinates -- which is unique for the axis-aligned edges of
        /// a cube -- COLLIDES on a tetrahedron's diagonals: the edge from
        /// (0,0,0) to (1,0,1) and the edge from (1,0,0) to (0,0,1) both sum to
        /// (1,0,1), so two unrelated edges shared one vertex and tore the
        /// surface open along every diagonal. The ordered pair cannot collide.
        /// </summary>
        private static int EdgeVertex(int cornerA, int cornerB, float[] values, Vector3[] points,
            Vector3Int[] keys, List<Vector3> vertices,
            Dictionary<(Vector3Int, Vector3Int), int> index)
        {
            Vector3Int ka = keys[cornerA];
            Vector3Int kb = keys[cornerB];

            // Ordered, so an edge gets the same key whichever end asks.
            bool swap = ka.x > kb.x
                || (ka.x == kb.x && ka.y > kb.y)
                || (ka.x == kb.x && ka.y == kb.y && ka.z > kb.z);
            if (swap)
            {
                (cornerA, cornerB) = (cornerB, cornerA);
                (ka, kb) = (kb, ka);
            }

            var edgeKey = (ka, kb);
            if (index.TryGetValue(edgeKey, out int existing))
            {
                return existing;
            }

            float va = values[cornerA];
            float vb = values[cornerB];
            float t = Mathf.Approximately(va, vb) ? 0.5f : va / (va - vb);
            t = Mathf.Clamp01(t);

            vertices.Add(Vector3.Lerp(points[cornerA], points[cornerB], t));
            index[edgeKey] = vertices.Count - 1;
            return vertices.Count - 1;
        }

        /// <summary>
        /// Adds a triangle wound so its face points along
        /// <paramref name="outward"/>.
        /// </summary>
        private static void AddTriangle(List<int> triangles, List<Vector3> vertices,
            int a, int b, int c, Vector3 outward)
        {
            if (a == b || b == c || a == c)
            {
                return;   // degenerate: the surface passed exactly through a corner
            }

            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(face, outward) < 0.0f)
            {
                (b, c) = (c, b);
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>
        /// Surface normals from the distance field itself, by central
        /// difference, rather than averaged from triangle faces.
        ///
        /// The field knows the true normal everywhere; face averaging only
        /// knows what the tessellation happened to produce, and on a blended
        /// join that shows up as faceting exactly where the surface is
        /// smoothest.
        /// </summary>
        public static Vector3[] Normals(IReadOnlyList<FusedBodyShape.Segment> segments,
            IReadOnlyList<Vector3> vertices, float epsilon)
        {
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float dx = FusedBodyShape.Distance(segments, p + Vector3.right * epsilon)
                           - FusedBodyShape.Distance(segments, p - Vector3.right * epsilon);
                float dy = FusedBodyShape.Distance(segments, p + Vector3.up * epsilon)
                           - FusedBodyShape.Distance(segments, p - Vector3.up * epsilon);
                float dz = FusedBodyShape.Distance(segments, p + Vector3.forward * epsilon)
                           - FusedBodyShape.Distance(segments, p - Vector3.forward * epsilon);

                var normal = new Vector3(dx, dy, dz);
                normals[i] = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
            }
            return normals;
        }
    }
}
