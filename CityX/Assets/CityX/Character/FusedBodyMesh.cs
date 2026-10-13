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
            // Kept off the corners: a crossing within a hair of a corner makes a sliver triangle microns across, which a check welding
            // vertices by position merges into a hole that is not there. The surface moves by under 0.2% of a cell.
            t = Mathf.Clamp(t, 0.002f, 0.998f);

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

            // A sliver (two corners a hair apart) is kept: dropping it unpairs its neighbours' edges, a hole that is not there.
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
        /// Re-emits an extracted surface as TRIANGLE SOUP with paint UVs: every
        /// triangle gets its own three vertices, shared with nothing.
        ///
        /// UNWELDED ON PURPOSE, which is the whole reason this exists as a
        /// separate pass. The seam unwrap that stops a triangle straddling the
        /// wrap from interpolating backwards round the far side of a limb works
        /// by nudging one corner's u from 0.01 to 1.01 -- and that is only safe
        /// if no other triangle shares that corner. Welded, the nudge would
        /// drag every neighbouring triangle's mapping with it.
        ///
        /// THE ATLAS ROW IS DECIDED ONCE PER TRIANGLE, from its centroid, never
        /// per vertex. Every blend junction -- the armpit, the hip -- has a
        /// knife-edge where two bones' claims are nearly tied, and a small
        /// triangle straddling it could have one corner won by the torso and
        /// another by the arm, putting its corners in two different rows. The
        /// hardware interpolates UV across the triangle, so its interior would
        /// sweep through every row in between and sample whatever was painted
        /// there. In the Godot build that showed up as a stroke near an armpit
        /// appearing as a thin line along the hip.
        /// </summary>
        public static void Unwrap(IReadOnlyList<FusedBodyShape.Segment> segments,
            IReadOnlyList<Vector3> weldedVertices, IReadOnlyList<int> weldedTriangles,
            out List<Vector3> vertices, out List<int> triangles, out List<Vector2> uvs)
        {
            vertices = new List<Vector3>(weldedTriangles.Count);
            triangles = new List<int>(weldedTriangles.Count);
            uvs = new List<Vector2>(weldedTriangles.Count);

            for (int i = 0; i < weldedTriangles.Count; i += 3)
            {
                Vector3 a = weldedVertices[weldedTriangles[i]];
                Vector3 b = weldedVertices[weldedTriangles[i + 1]];
                Vector3 c = weldedVertices[weldedTriangles[i + 2]];

                Vector3 centroid = (a + b + c) / 3.0f;
                HumanBodyBones dominant = FusedBodyShape.DominantBone(segments, centroid);
                HumanBodyBones paintBone = PaintAtlas.PaintBoneFor(dominant);
                int row = PaintAtlas.RowOf(paintBone);
                if (row < 0)
                {
                    // A bone with no row of its own means the paint bone table
                    // and the atlas have drifted apart. Painting there would
                    // land somewhere arbitrary, so it goes to the hips rather
                    // than off the end of the texture.
                    row = PaintAtlas.RowOf(HumanBodyBones.Hips);
                    paintBone = HumanBodyBones.Hips;
                }

                PaintAtlas.AxisOf(paintBone, out Vector3 axisA, out Vector3 axisB);

                Vector2 ua = PaintAtlas.Coordinate(a, axisA, axisB, row);
                Vector2 ub = PaintAtlas.Coordinate(b, axisA, axisB, row);
                Vector2 uc = PaintAtlas.Coordinate(c, axisA, axisB, row);
                PaintAtlas.FixThetaSeam(ref ua, ref ub, ref uc);

                triangles.Add(vertices.Count);
                vertices.Add(a);
                uvs.Add(ua);

                triangles.Add(vertices.Count);
                vertices.Add(b);
                uvs.Add(ub);

                triangles.Add(vertices.Count);
                vertices.Add(c);
                uvs.Add(uc);
            }
        }

        /// <summary>The hand's fine grid is cut and zipped onto the body this far before the wrist joint, along the forearm, where the field is purely the forearm's.</summary>
        public const float WristCutBack = 0.045f;

        /// <summary>The grids are shifted by this off any round number, so no grid corner lands exactly on the surface (see ExtractBody).</summary>
        public const float GridShift = 0.000137f;

        /// <summary>Only what lies within this of the forearm's line is cut (the plane is infinite and would take the feet off too): the thumb reaches 0.21 off the line, the nearest other part below the cut (the hip) is 0.30 off it. Nothing may straddle it, or the cut edge frays.</summary>
        public const float WristCutRadius = 0.25f;

        /// <summary>
        /// The whole body as one closed surface with the hands extracted finer and stitched on. One grid cannot be both: fingers need a cell
        /// a third the size of the body's, and a body on that grid is a third of a million triangles. So the body is extracted on
        /// <paramref name="cellSize"/>, each hand (with a stretch of forearm) on <paramref name="handCellSize"/> from the same field, both are
        /// cut on one plane across the forearm (<see cref="ClipByPlane"/>) and the two cut edges are zipped (<see cref="Zip"/>): both trace the
        /// same cross-section of the same field, so the seam is a hair wide and the field's own normals shade straight across it. Welded.
        /// </summary>
        public static void ExtractBody(IReadOnlyList<FusedBodyShape.Segment> shape, System.Func<HumanBodyBones, Vector3> bonePosition,
            float cellSize, float handCellSize, out List<Vector3> vertices, out List<int> triangles, System.Action<string> warn)
        {
            Bounds bounds = FusedBodyShape.BoundsOf(shape);
            // Phase-aligned to Godot's own grid -- see PhaseAlignedOrigin. Only the MIN corner moves, downward, so the region only ever grows.
            // Then shifted by a hair: on Godot's exact grid the torso's radius lands on grid corners (0.26 at x = 0.24, z = 0.10), and a
            // corner exactly on the surface makes slivers of triangles microns across, which read as holes to anything welding by position.
            bounds.SetMinMax(FusedBodyShape.PhaseAlignedOrigin(bounds.min, cellSize) + Vector3.one * GridShift, bounds.max);
            Extract(shape, bounds, cellSize, out vertices, out triangles);
            if (vertices.Count == 0)
            {
                return;
            }
            foreach (HumanBodyBones hand in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                StitchHand(shape, bonePosition, hand, handCellSize, ref vertices, ref triangles, warn);
            }
        }

        /// <summary>
        /// Replaces one hand of the body's coarse surface with the same hand extracted on the fine grid: both are cut on a plane across the
        /// forearm <see cref="WristCutBack"/> before the wrist, the body keeping the shoulder side and the fine piece the hand side, and the
        /// two cut edges are zipped. The fine field is just this arm and this hand; nothing else comes within its grid.
        /// </summary>
        private static void StitchHand(IReadOnlyList<FusedBodyShape.Segment> shape, System.Func<HumanBodyBones, Vector3> bonePosition, HumanBodyBones hand,
            float handCellSize, ref List<Vector3> vertices, ref List<int> triangles, System.Action<string> warn)
        {
            HumanBodyBones upper = hand == HumanBodyBones.LeftHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            Vector3 shoulder = bonePosition(upper);
            Vector3 handTarget = bonePosition(hand);
            Vector3 wrist = Vector3.Lerp(shoulder, handTarget, BodyRig.WristT);
            Vector3 along = (handTarget - shoulder).normalized;
            Vector3 cut = wrist - along * WristCutBack;

            var local = new List<FusedBodyShape.Segment>();
            foreach (FusedBodyShape.Segment s in shape)
            {
                if (s.Bone == upper || s.Bone == hand)
                {
                    local.Add(s);
                }
            }
            List<FusedBodyShape.Segment> pieces = FusedBodyShape.HandPieces(shape, hand);
            if (pieces.Count == 0 || local.Count == pieces.Count)
            {
                warn?.Invoke($"CityX: no {hand} or its arm in the shape -- the body keeps its coarse hand.");
                return;
            }
            Bounds bounds = FusedBodyShape.BoundsOf(pieces);
            bounds.Encapsulate(new Bounds(wrist, Vector3.one * 0.24f));
            bounds.Encapsulate(new Bounds(cut - along * 0.03f, Vector3.one * 0.24f));
            bounds.Expand(0.02f);
            bounds.SetMinMax(bounds.min + Vector3.one * GridShift, bounds.max);
            Extract(local, bounds, handCellSize, out List<Vector3> fineVertices, out List<int> fineTriangles);
            if (fineVertices.Count == 0)
            {
                warn?.Invoke($"CityX: the {hand} extracted to nothing on the fine grid.");
                return;
            }

            ClipByPlane(fineVertices, fineTriangles, cut, along, true, float.MaxValue,
                out List<Vector3> handVertices, out List<int> handTriangles, out List<List<int>> handLoops);
            ClipByPlane(vertices, triangles, cut, along, false, WristCutRadius,
                out List<Vector3> bodyVertices, out List<int> bodyTriangles, out List<List<int>> bodyLoops);
            if (handLoops.Count != 1 || bodyLoops.Count != 1)
            {
                var where = new System.Text.StringBuilder();
                foreach (List<int> loop in bodyLoops) where.Append($" body {loop.Count} at {Centre(bodyVertices, loop)}");
                foreach (List<int> loop in handLoops) where.Append($" hand {loop.Count} at {Centre(handVertices, loop)}");
                warn?.Invoke($"CityX: the {hand} cut left {bodyLoops.Count} edge(s) on the body and {handLoops.Count} on the hand -- zipping the nearest pairs ({where}).");
            }

            int offset = bodyVertices.Count;
            bodyVertices.AddRange(handVertices);
            foreach (int t in handTriangles)
            {
                bodyTriangles.Add(offset + t);
            }
            var unpaired = new List<List<int>>();
            foreach (List<int> loop in handLoops)
            {
                var shifted = new List<int>(loop.Count);
                foreach (int i in loop)
                {
                    shifted.Add(offset + i);
                }
                unpaired.Add(shifted);
            }
            foreach (List<int> bodyLoop in bodyLoops)
            {
                Vector3 centre = Centre(bodyVertices, bodyLoop);
                int nearest = -1;
                float best = float.MaxValue;
                for (int i = 0; i < unpaired.Count; i++)
                {
                    float d = (Centre(bodyVertices, unpaired[i]) - centre).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = i;
                    }
                }
                if (nearest < 0)
                {
                    warn?.Invoke($"CityX: the {hand}'s cut on the body has no edge on the hand to zip to -- a hole at the wrist.");
                    continue;
                }
                Zip(bodyVertices, bodyLoop, unpaired[nearest], along, bodyTriangles);
                unpaired.RemoveAt(nearest);
            }
            vertices = bodyVertices;
            triangles = bodyTriangles;
        }

        private static Vector3 Centre(IReadOnlyList<Vector3> vertices, List<int> loop)
        {
            Vector3 sum = Vector3.zero;
            foreach (int i in loop)
            {
                sum += vertices[i];
            }
            return sum / Mathf.Max(1, loop.Count);
        }

        /// <summary>
        /// Shares every vertex that two triangles can share: the same position AND the same paint UV. Unwrap's triangle soup is only
        /// needed where it does something -- at the theta seam of a limb's row, and where one row meets another -- and everywhere else it
        /// tripled the vertex count for nothing. A vertex on a seam keeps its own copy; its neighbours across the seam keep theirs.
        /// </summary>
        public static void Weld(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
            out List<Vector3> outVertices, out List<int> outTriangles, out List<Vector2> outUvs)
        {
            outVertices = new List<Vector3>(vertices.Count / 3);
            outTriangles = new List<int>(triangles.Count);
            outUvs = new List<Vector2>(vertices.Count / 3);
            var index = new Dictionary<(Vector3Int, Vector2Int), int>();
            var remap = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                Vector2 uv = uvs[i];
                var key = (new Vector3Int(Mathf.RoundToInt(p.x * 10000.0f), Mathf.RoundToInt(p.y * 10000.0f), Mathf.RoundToInt(p.z * 10000.0f)),
                    new Vector2Int(Mathf.RoundToInt(uv.x * 100000.0f), Mathf.RoundToInt(uv.y * 100000.0f)));
                if (!index.TryGetValue(key, out int shared))
                {
                    shared = outVertices.Count;
                    index[key] = shared;
                    outVertices.Add(p);
                    outUvs.Add(uv);
                }
                remap[i] = shared;
            }
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = remap[triangles[i]], b = remap[triangles[i + 1]], c = remap[triangles[i + 2]];
                if (a == b || b == c || a == c)
                {
                    continue;
                }
                outTriangles.Add(a);
                outTriangles.Add(b);
                outTriangles.Add(c);
            }
        }

        /// <summary>
        /// Cuts a welded, closed mesh on a plane through <paramref name="point"/> with normal <paramref name="normal"/>, keeping the side the
        /// normal points to (or the other, when <paramref name="keepPositive"/> is false), but only within <paramref name="radius"/> of the
        /// line through the point along the normal: the plane is infinite and a wrist's cut would otherwise take the feet off too, while
        /// everything of the forearm and hand lies along that line. Returns the kept mesh, still welded, and the loops of new vertices along
        /// the cut, each in the winding of the kept triangles.
        ///
        /// THIS IS WHAT LETS THE HANDS BE FINER THAN THE BODY. One grid cannot be both: fingers need a cell a third the size of the body's, and
        /// a body on that grid is a third of a million triangles. So the body and each hand are extracted from the same field on their own grids,
        /// both are cut on a plane across the forearm, and the two cut edges are zipped together (<see cref="Zip"/>). Both edges trace the same
        /// cross-section of the same field, so the seam between them is a hair wide and the field's own normals shade straight across it.
        /// </summary>
        public static void ClipByPlane(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, Vector3 point, Vector3 normal,
            bool keepPositive, float radius, out List<Vector3> outVertices, out List<int> outTriangles, out List<List<int>> loops)
        {
            // Nudged off every grid vertex: a vertex exactly on the plane is neither side, and an edge to it a zero-length sliver.
            point += normal * 0.00037f;
            float sign = keepPositive ? 1.0f : -1.0f;
            float radius2 = radius * radius;
            var side = new float[vertices.Count];   // positive: kept
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 offset = vertices[i] - point;
                float alongLine = Vector3.Dot(offset, normal);
                float s = sign * alongLine;
                if (s < 0.0f && (offset - normal * alongLine).sqrMagnitude > radius2)
                {
                    s = 1.0f;   // the wrong side of the plane but nowhere near the line of the cut: kept whole
                }
                side[i] = s;
            }

            // Local lists, since a local function may not touch an out parameter.
            var kept = new List<Vector3>(vertices.Count);
            var keptTriangles = new List<int>(triangles.Count);
            var remap = new int[vertices.Count];
            for (int i = 0; i < remap.Length; i++)
            {
                remap[i] = -1;
            }
            var onPlane = new Dictionary<(int, int), int>();
            var next = new Dictionary<int, int>();   // boundary edges: from -> to, in the kept winding

            int Keep(int i)
            {
                if (remap[i] < 0)
                {
                    remap[i] = kept.Count;
                    kept.Add(vertices[i]);
                }
                return remap[i];
            }

            int Cross(int keptEnd, int cutEnd)
            {
                (int lo, int hi) = keptEnd < cutEnd ? (keptEnd, cutEnd) : (cutEnd, keptEnd);
                if (onPlane.TryGetValue((lo, hi), out int existing))
                {
                    return existing;
                }
                float t = side[keptEnd] / (side[keptEnd] - side[cutEnd]);
                int created = kept.Count;
                kept.Add(Vector3.Lerp(vertices[keptEnd], vertices[cutEnd], t));
                onPlane[(lo, hi)] = created;
                return created;
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                bool ka = side[a] >= 0.0f, kb = side[b] >= 0.0f, kc = side[c] >= 0.0f;
                int keptCount = (ka ? 1 : 0) + (kb ? 1 : 0) + (kc ? 1 : 0);
                if (keptCount == 3)
                {
                    keptTriangles.Add(Keep(a));
                    keptTriangles.Add(Keep(b));
                    keptTriangles.Add(Keep(c));
                    continue;
                }
                if (keptCount == 0)
                {
                    continue;
                }
                // Rotate so the odd one out is first: one kept (a kept, b and c cut) or one cut (a cut, b and c kept).
                for (int r = 0; r < 3; r++)
                {
                    bool aOdd = keptCount == 1 ? ka : !ka;
                    if (aOdd)
                    {
                        break;
                    }
                    (a, b, c) = (b, c, a);
                    (ka, kb, kc) = (kb, kc, ka);
                }
                if (keptCount == 1)
                {
                    int ab = Cross(a, b), ac = Cross(a, c);
                    keptTriangles.Add(Keep(a));
                    keptTriangles.Add(ab);
                    keptTriangles.Add(ac);
                    next[ab] = ac;
                }
                else
                {
                    // a cut, b and c kept: the quad b, c, ca, ba.
                    int ba = Cross(b, a), ca = Cross(c, a);
                    int kb2 = Keep(b), kc2 = Keep(c);
                    keptTriangles.Add(kb2);
                    keptTriangles.Add(kc2);
                    keptTriangles.Add(ca);
                    keptTriangles.Add(kb2);
                    keptTriangles.Add(ca);
                    keptTriangles.Add(ba);
                    next[ca] = ba;
                }
            }

            outVertices = kept;
            outTriangles = keptTriangles;
            loops = new List<List<int>>();
            var visited = new HashSet<int>();
            foreach (int start in next.Keys)
            {
                if (visited.Contains(start))
                {
                    continue;
                }
                var loop = new List<int>();
                int walk = start;
                while (!visited.Contains(walk) && next.TryGetValue(walk, out int to))
                {
                    visited.Add(walk);
                    loop.Add(walk);
                    walk = to;
                }
                if (loop.Count >= 3)
                {
                    loops.Add(loop);
                }
            }
        }

        /// <summary>
        /// Triangulates the band between two closed loops of vertices lying in one plane (see <see cref="ClipByPlane"/>): walks both round
        /// together, always taking the shorter bridge. Both loops are first turned to run the same way round <paramref name="normal"/>. Every
        /// band triangle is emitted twice, facing each way: the band is a ring of slivers a hair wide lying flat in the cut plane, so neither
        /// side is its "outside", and a culled sliver would be a crack.
        /// </summary>
        public static void Zip(IReadOnlyList<Vector3> vertices, List<int> loopA, List<int> loopB, Vector3 normal, List<int> triangles)
        {
            if (loopA.Count < 3 || loopB.Count < 3)
            {
                return;
            }
            Orient(vertices, loopA, normal);
            Orient(vertices, loopB, normal);

            int n = loopA.Count, m = loopB.Count;
            int j0 = 0;
            float best = float.MaxValue;
            for (int j = 0; j < m; j++)
            {
                float d = (vertices[loopB[j]] - vertices[loopA[0]]).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    j0 = j;
                }
            }

            int ai = 0, bj = 0;
            while (ai < n || bj < m)
            {
                int a = loopA[ai % n], aNext = loopA[(ai + 1) % n];
                int b = loopB[(j0 + bj) % m], bNext = loopB[(j0 + bj + 1) % m];
                bool advanceA;
                if (ai >= n)
                {
                    advanceA = false;
                }
                else if (bj >= m)
                {
                    advanceA = true;
                }
                else
                {
                    advanceA = (vertices[aNext] - vertices[b]).sqrMagnitude <= (vertices[a] - vertices[bNext]).sqrMagnitude;
                }
                int p = a, q = advanceA ? aNext : bNext, r = b;
                if (p != q && q != r && p != r)
                {
                    triangles.Add(p); triangles.Add(q); triangles.Add(r);
                    triangles.Add(p); triangles.Add(r); triangles.Add(q);
                }
                if (advanceA) ai++; else bj++;
            }
        }

        /// <summary>Reverses a loop if it runs clockwise seen along <paramref name="normal"/>.</summary>
        private static void Orient(IReadOnlyList<Vector3> vertices, List<int> loop, Vector3 normal)
        {
            Vector3 centre = Vector3.zero;
            foreach (int i in loop)
            {
                centre += vertices[i];
            }
            centre /= loop.Count;
            float turn = 0.0f;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 a = vertices[loop[i]] - centre, b = vertices[loop[(i + 1) % loop.Count]] - centre;
                turn += Vector3.Dot(Vector3.Cross(a, b), normal);
            }
            if (turn < 0.0f)
            {
                loop.Reverse();
            }
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
