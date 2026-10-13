using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The character's own shape.
    ///
    /// A body extracted from a distance field fails in ways that are obvious
    /// on screen and invisible in code: holes where the tessellation
    /// disagreed with itself, a surface inside out, a head that is not
    /// actually round, fingers that merged into a mitten. Every one of those
    /// is a claim about geometry, and can be measured.
    /// </summary>
    public class FusedBodyTests
    {
        /// <summary>
        /// THE REAL SKELETON, not a stand-in. This used to be a made-up set of
        /// roughly human joint positions, on the grounds that the shape
        /// mathematics does not care where the bones came from -- which is true
        /// of the mathematics and false of the body. The proportions are most
        /// of what goes wrong: a shoulder that sits proud of the torso, an arm
        /// a third the thickness of the chest, a head sphere overhanging the
        /// shoulder line. A stand-in with none of those passed every test while
        /// the real body creased.
        /// </summary>
        private static Vector3 Bone(HumanBodyBones bone)
        {
            return BodyRig.ShapePositionOf(bone);
        }

        /// <summary>The wrist joint of the left arm: WristT of the way from the shoulder to fused_body.gd's hand target.</summary>
        private static Vector3 LeftWrist()
        {
            return Vector3.Lerp(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftHand), BodyRig.WristT);
        }

        /// <summary>A point exactly the arm's radius there off the elbow, straight out from the (hanging, diagonal) arm's axis: on the skin of an unbroken arm.</summary>
        private static Vector3 OnTheSkinAtTheElbow()
        {
            // The arm's cone runs from its ROOT (a little under the shoulder joint, FusedBodyShape.ArmRootBelowShoulder) to the wrist.
            Vector3 root = Bone(HumanBodyBones.LeftUpperArm) - Vector3.up * FusedBodyShape.ArmRootBelowShoulder;
            Vector3 elbow = Bone(HumanBodyBones.LeftLowerArm);
            Vector3 axis = (LeftWrist() - root).normalized;
            Vector3 outward = new Vector3(-axis.y, axis.x, 0.0f).normalized;
            Vector3 onAxis = root + axis * Vector3.Dot(elbow - root, axis);
            float t = Vector3.Distance(root, onAxis) / Vector3.Distance(root, LeftWrist());
            float radius = Mathf.Lerp(FusedBodyShape.ArmRadius, FusedBodyShape.WristRadius, t);
            return onAxis + outward * radius;
        }

        /// <summary>
        /// The authoring height, so the radius scale comes out at exactly one
        /// and the capsules are the numbers the body was drawn with.
        /// </summary>
        private const float Height = FusedBodyShape.ReferenceHeight;

        private static List<FusedBodyShape.Segment> MakeSegments()
        {
            return FusedBodyShape.Segments(Bone, Height);
        }

        private static List<FusedBodyShape.Segment> MakeShape()
        {
            return FusedBodyShape.LimbCapsules(Bone, Height);
        }

        /// <summary>
        /// The shape as the body's own grid sees it: without the hands, which are thinner than a body cell and are extracted on their own
        /// finer grid and stitched on (FusedBodyBuilder.StitchHand). On the body grid a finger is a lump with vertices off the field.
        /// </summary>
        private static List<FusedBodyShape.Segment> MakeShapeWithoutHands()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            shape.RemoveAll(s => s.Bone == HumanBodyBones.LeftHand || s.Bone == HumanBodyBones.RightHand);
            return shape;
        }

        /// <summary>One arm and its hand: what the fine hand grid extracts.</summary>
        private static List<FusedBodyShape.Segment> MakeLeftArmAndHand()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            shape.RemoveAll(s => s.Bone != HumanBodyBones.LeftHand && s.Bone != HumanBodyBones.LeftUpperArm);
            return shape;
        }

        private static int CountOf(List<FusedBodyShape.Segment> pieces, HumanBodyBones bone)
        {
            int n = 0;
            foreach (FusedBodyShape.Segment s in pieces)
            {
                if (s.Bone == bone) n++;
            }
            return n;
        }

        [Test]
        public void ThereIsATorsoAPelvisANeckFourLimbsHandsFeetAndNoHead()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();

            var bones = new HashSet<HumanBodyBones>();
            foreach (FusedBodyShape.Segment segment in segments)
            {
                bones.Add(segment.Bone);
            }

            // NO HEAD, WHICH IS THE POINT. fused_body.gd's own limb_defs() has
            // a torso, two arms, two legs and a neck and has never had a head
            // in it -- character.gd builds the skull separately, as its own
            // mesh on the head bone. Fusing it in here blended it into the
            // neck through the same smooth minimum as every joint and gave a
            // single continuous peanut with no neck at all.
            Assert.IsFalse(bones.Contains(HumanBodyBones.Head),
                "the head is back in the body's shape -- it belongs to HeadMesh");
            Assert.IsTrue(bones.Contains(HumanBodyBones.Neck), "no neck");
            Assert.IsTrue(bones.Contains(HumanBodyBones.Spine),
                "the spine drives no geometry -- the back will move as one rigid block");
            Assert.AreEqual(2, CountOf(segments, HumanBodyBones.Hips), "the hips own the lower torso and the pelvis bar");
            Assert.IsTrue(bones.Contains(HumanBodyBones.LeftHand), "no left hand");
            Assert.IsTrue(bones.Contains(HumanBodyBones.RightLowerLeg), "no right shin");
            // Feet now: a rounded box under each ankle (owner, 2026-10-13: "a basic foot with no toes").
            Assert.AreEqual(1, CountOf(segments, HumanBodyBones.RightFoot), "the right foot is not one piece");
            Assert.AreEqual(1, CountOf(segments, HumanBodyBones.LeftFoot), "the left foot is not one piece");
            // A hand: a palm, four fingers of two pieces each, a thumb of two.
            Assert.AreEqual(1 + 4 * 2 + 2, CountOf(segments, HumanBodyBones.LeftHand), "the left hand is not a palm, four fingers and a thumb");
            Assert.AreEqual(1 + 4 * 2 + 2, CountOf(segments, HumanBodyBones.RightHand));
        }

        /// <summary>
        /// The head is a SPHERE and stays the size it was drawn. This is the
        /// one proportion that was explicitly asked to survive, so it gets its
        /// own test rather than being implied by the others.
        /// </summary>
        [Test]
        public void TheHeadIsASphereOfTheOriginalSize()
        {
            Mesh head = HeadMesh.Build();
            try
            {
                Assert.Greater(head.vertexCount, 0, "the head built to nothing");

                foreach (Vector3 vertex in head.vertices)
                {
                    Assert.AreEqual(FusedBodyShape.HeadRadius, vertex.magnitude, 0.001f,
                        "the head is not a sphere of the size it was drawn at");
                }

                // Centred on its own origin, which is the head bone: the bone
                // is at the middle of the skull in this rig, so the mesh needs
                // no offset off it.
                Assert.AreEqual(Vector3.zero, head.bounds.center);
            }
            finally
            {
                Object.DestroyImmediate(head);
            }
        }

        /// <summary>
        /// Wound outward, and closed. character.gd records losing a triangle
        /// at each pole to a winding check that read a degenerate quad, which
        /// is a hole in the top of the skull.
        /// </summary>
        [Test]
        public void TheHeadIsClosedAndFacesOutward()
        {
            Mesh head = HeadMesh.Build();
            try
            {
                int[] triangles = head.triangles;
                Vector3[] vertices = head.vertices;
                Assert.AreEqual(0, triangles.Length % 3);

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];

                    Vector3 face = Vector3.Cross(b - a, c - a);
                    Vector3 centre = (a + b + c) / 3.0f;
                    Assert.Greater(Vector3.Dot(face.normalized, centre.normalized), 0.0f,
                        $"triangle {i / 3} of the head faces inward -- a hole in the skull");
                }

                // Every edge shared by exactly two triangles: a closed surface.
                var edges = new Dictionary<(Vector3Int, Vector3Int), int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Count(edges, vertices, triangles[i], triangles[i + 1]);
                    Count(edges, vertices, triangles[i + 1], triangles[i + 2]);
                    Count(edges, vertices, triangles[i + 2], triangles[i]);
                }
                foreach (KeyValuePair<(Vector3Int, Vector3Int), int> edge in edges)
                {
                    Assert.AreEqual(2, edge.Value,
                        $"the edge {edge.Key.Item1} to {edge.Key.Item2} is shared by "
                        + $"{edge.Value} triangles, not 2 -- the head is not closed");
                }
            }
            finally
            {
                Object.DestroyImmediate(head);
            }
        }

        /// <summary>
        /// Keyed by rounded POSITION, not vertex index: the head is triangle
        /// soup, so the two triangles either side of an edge own separate
        /// copies of its two corners.
        /// </summary>
        private static void Count(Dictionary<(Vector3Int, Vector3Int), int> edges,
            Vector3[] vertices, int from, int to)
        {
            Vector3Int a = Key(vertices[from]);
            Vector3Int b = Key(vertices[to]);
            (Vector3Int, Vector3Int) edge = Before(a, b) ? (a, b) : (b, a);
            edges[edge] = edges.TryGetValue(edge, out int seen) ? seen + 1 : 1;
        }

        /// <summary>
        /// A micron grid, kept as coordinates rather than hashed into an int -- two distinct edges colliding on a hash reads exactly like
        /// a hole, and chasing that is not what this test is for. A micron, not a tenth of a millimetre: merging two distinct vertices that
        /// close collapses a sliver triangle and leaves its neighbours' edges unpaired, a hole that is not there (the extraction keeps every
        /// crossing at least 0.2% of a cell off the grid corners, FusedBodyMesh.EdgeVertex).
        /// </summary>
        private static Vector3Int Key(Vector3 position)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x * 1000000.0f),
                Mathf.RoundToInt(position.y * 1000000.0f),
                Mathf.RoundToInt(position.z * 1000000.0f));
        }

        private static bool Before(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z < b.z;
        }

        /// <summary>
        /// Radii scale with the body. A character built at half size must be
        /// the same shape, not a normal body with tiny limbs.
        /// </summary>
        [Test]
        public void ThicknessFollowsTheBodySize()
        {
            List<FusedBodyShape.Segment> full = FusedBodyShape.Segments(Bone, Height);
            List<FusedBodyShape.Segment> half = FusedBodyShape.Segments(Bone, Height * 0.5f);

            for (int i = 0; i < full.Count; i++)
            {
                // An elbow's or knee's radius is read off the limb's cone there, and the cone's root moves with the scale: within a percent.
                Assert.AreEqual(full[i].Radius * 0.5f, half[i].Radius, full[i].Radius * 0.01f,
                    "a half-height character is not half as thick");
                Assert.AreEqual(full[i].RadiusB * 0.5f, half[i].RadiusB, full[i].RadiusB * 0.01f);
                Assert.AreEqual(full[i].Half * 0.5f, half[i].Half);
            }
        }

        [Test]
        public void TheSurfaceIsInsideTheBodyAndOutsideTheAir()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();

            Assert.Less(FusedBodyShape.Distance(shape, new Vector3(0.0f, 1.3f, 0.0f)), 0.0f,
                "the middle of the torso is not inside the body");
            Assert.Less(FusedBodyShape.Distance(shape, Bone(HumanBodyBones.Neck)), 0.0f,
                "the neck is not inside the body");
            Assert.Greater(FusedBodyShape.Distance(shape, Bone(HumanBodyBones.Head)), 0.0f,
                "the middle of the skull is inside the body -- the head is fused in again");
            Assert.Greater(FusedBodyShape.Distance(shape, new Vector3(0.0f, 5.0f, 0.0f)), 0.0f,
                "a point well above the character is inside it");
            Assert.Greater(FusedBodyShape.Distance(shape, new Vector3(3.0f, 1.3f, 0.0f)), 0.0f,
                "a point three metres to the side is inside the character");
        }

        /// <summary>
        /// The blend is what makes this a FUSED body rather than a pile of
        /// capsules. At a shoulder the surface must sit further out than
        /// either capsule alone would put it.
        /// </summary>
        [Test]
        public void JoinsAreBlendedRatherThanIntersecting()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            Vector3 shoulder = Bone(HumanBodyBones.LeftUpperArm);

            // Just outside both the arm and the torso, in the armpit crease between them (a little in toward the torso, below and in front of
            // the shoulder joint). A hard union would put this point outside the surface; the smooth one fills it in.
            var probe = shoulder + new Vector3(0.06f, -0.10f, 0.11f);

            float hardUnion = float.MaxValue;
            foreach (FusedBodyShape.Segment segment in shape)
            {
                hardUnion = Mathf.Min(hardUnion, segment.Distance(probe));
            }
            float blended = FusedBodyShape.Distance(shape, probe);

            Assert.Less(blended, hardUnion,
                "the blend is doing nothing -- the joins would be hard creases");
        }

        [Test]
        public void SmoothMinNeverExceedsAPlainMinimum()
        {
            // A smooth minimum only ever rounds DOWNWARD, which is what fills
            // a crease rather than carving one.
            for (float a = -1.0f; a <= 1.0f; a += 0.1f)
            {
                for (float b = -1.0f; b <= 1.0f; b += 0.1f)
                {
                    float smooth = FusedBodyShape.SmoothMin(a, b, 0.06f);
                    Assert.LessOrEqual(smooth, Mathf.Min(a, b) + 1e-5f,
                        $"smin({a:0.0}, {b:0.0}) came out above a plain min");
                }
            }
        }

        /// <summary>
        /// SmoothMin(d, d, k) is NOT d, it equals d - k * 0.25 (h lands exactly on 0.5 when the two inputs are equal). Harmless as long as a limb
        /// is one piece -- it bites once a limb is split into colinear pieces sharing an endpoint and those are unioned back together, which is
        /// exactly what LimbCapsules exists to avoid. Pinned here as its own fact about the formula.
        /// </summary>
        [Test]
        public void SmoothMinOfTwoEqualValuesDropsBelowEitherOfThem()
        {
            const float k = 0.06f;
            for (float d = -1.0f; d <= 1.0f; d += 0.1f)
            {
                float smooth = FusedBodyShape.SmoothMin(d, d, k);
                Assert.AreEqual(d - k * 0.25f, smooth, 1e-5f);
                Assert.Less(smooth, d, "two equal inputs should not smin() back to themselves");
            }
        }

        /// <summary>The shape list has one unbroken piece per arm and leg: shoulder to wrist, hip to ankle, no elbow, wrist or knee split.</summary>
        [Test]
        public void TheShapeHasOneUnbrokenPiecePerLimb()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            Assert.AreEqual(1, CountOf(shape, HumanBodyBones.LeftUpperArm), "the left arm is not one piece");
            Assert.AreEqual(1, CountOf(shape, HumanBodyBones.RightUpperLeg), "the right leg is not one piece");
            Assert.AreEqual(0, CountOf(shape, HumanBodyBones.LeftLowerArm), "the shape splits the arm at the elbow: it will bulge there");
            Assert.AreEqual(0, CountOf(shape, HumanBodyBones.RightLowerLeg), "the shape splits the leg at the knee: it will bulge there");
            Assert.AreEqual(1, CountOf(shape, HumanBodyBones.Neck));
            FusedBodyShape.Segment arm = shape.Find(s => s.Bone == HumanBodyBones.LeftUpperArm);
            Assert.Less(Vector3.Distance(arm.B, LeftWrist()), 1e-4f, "the arm's cone does not end at the wrist");
            Assert.AreEqual(FusedBodyShape.ArmRadius, arm.Radius, 1e-5f);
            Assert.AreEqual(FusedBodyShape.WristRadius, arm.RadiusB, 1e-5f, "the arm does not taper to the wrist");
        }

        /// <summary>A point exactly the arm's radius off the elbow sits on the skin of the unbroken arm: no seam bulge there.</summary>
        [Test]
        public void TheShapeHasNoBulgeAtWhatWouldHaveBeenAnElbowSeam()
        {
            Vector3 probe = OnTheSkinAtTheElbow();
            float distance = FusedBodyShape.Distance(MakeShape(), probe);
            Assert.AreEqual(0.0f, distance, 0.002f,
                "a point exactly the arm's radius off the elbow should sit on one unbroken arm's surface");
        }

        /// <summary>
        /// Documents the defect itself, on the per-bone list Segments() still (correctly) produces for skin weights: unioning the upper-arm and
        /// forearm pieces back together at their shared elbow pushes the surface outward by BlendRadius * 0.25 there. This is why the shape
        /// is built from LimbCapsules instead.
        /// </summary>
        [Test]
        public void ThePerBoneSegmentListStillBulgesAtAnElbowSeam()
        {
            Vector3 probe = OnTheSkinAtTheElbow();
            float distance = FusedBodyShape.Distance(MakeSegments(), probe);
            // Up to BlendRadius * 0.25 for two equal capsules; a little less here, since the two pieces are cones of slightly different taper.
            Assert.Less(distance, -0.004f, "the per-bone list should bulge outward at the elbow seam");
            Assert.Greater(distance, -FusedBodyShape.BlendRadius * 0.25f - 0.002f);
        }

        /// <summary>
        /// The extracted surface is closed. Exactly closed, no allowance: a boundary edge -- one used in only one direction -- is a hole, and a
        /// mismatched winding renders as a black speck.
        /// </summary>
        [Test]
        public void TheExtractedSurfaceIsClosed()
        {
            List<FusedBodyShape.Segment> shape = MakeShapeWithoutHands();
            Bounds bounds = FusedBodyShape.BoundsOf(shape);

            FusedBodyMesh.Extract(shape, bounds, FusedBodyShape.CellSize,
                out List<Vector3> vertices, out List<int> triangles);

            Assert.Greater(vertices.Count, 500, "the body extracted to almost nothing");
            Assert.AreEqual(0, triangles.Count % 3, "a partial triangle");
            Assert.AreEqual(0, UnmatchedEdges(triangles, out int total), $"of {total} edges some have no facing pair");
        }

        /// <summary>A hand on its own fine grid: closed, every vertex on the field, the fingers' vertices there at all.</summary>
        [Test]
        public void AHandExtractsCleanlyOnItsOwnFineGrid()
        {
            List<FusedBodyShape.Segment> armAndHand = MakeLeftArmAndHand();
            Bounds bounds = FusedBodyShape.BoundsOf(FusedBodyShape.HandPieces(armAndHand, HumanBodyBones.LeftHand));
            bounds.Expand(0.05f);
            FusedBodyMesh.Extract(armAndHand, bounds, FusedBodyShape.HandCellSize, out List<Vector3> vertices, out List<int> triangles);
            Assert.Greater(triangles.Count / 3, 4000, "the hand extracted to almost nothing");
            float worst = 0.0f;
            foreach (Vector3 vertex in vertices)
            {
                worst = Mathf.Max(worst, Mathf.Abs(FusedBodyShape.Distance(armAndHand, vertex)));
            }
            Assert.Less(worst, FusedBodyShape.HandCellSize * 0.5f, $"a hand vertex sits {worst:0.0000}m off the surface");
            // Open only where the arm leaves the box, nowhere on the hand itself.
            int open = UnmatchedEdges(triangles, out _);
            Assert.Less(open, 200, $"{open} open edges: the hand is torn, not just cut off at the forearm");
        }

        private static int UnmatchedEdges(List<int> triangles, out int total)
        {
            var edges = new HashSet<(int, int)>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                edges.Add((triangles[i], triangles[i + 1]));
                edges.Add((triangles[i + 1], triangles[i + 2]));
                edges.Add((triangles[i + 2], triangles[i]));
            }
            int unmatched = 0;
            foreach ((int a, int b) in edges)
            {
                if (!edges.Contains((b, a)))
                {
                    unmatched++;
                }
            }
            total = edges.Count;
            return unmatched;
        }

        /// <summary>
        /// Every vertex has to sit on the surface, not near it. A vertex placed
        /// by bad interpolation reads as a spike sticking out of the character.
        /// </summary>
        [Test]
        public void EveryVertexSitsOnTheSurface()
        {
            List<FusedBodyShape.Segment> shape = MakeShapeWithoutHands();
            Bounds bounds = FusedBodyShape.BoundsOf(shape);

            FusedBodyMesh.Extract(shape, bounds, FusedBodyShape.CellSize,
                out List<Vector3> vertices, out _);

            float worst = 0.0f;
            Vector3 where = Vector3.zero;
            foreach (Vector3 vertex in vertices)
            {
                float off = Mathf.Abs(FusedBodyShape.Distance(shape, vertex));
                if (off > worst)
                {
                    worst = off;
                    where = vertex;
                }
            }

            // Linear interpolation along an edge cannot be exact on a curved
            // field, so the tolerance is a fraction of a cell rather than zero.
            Assert.Less(worst, FusedBodyShape.CellSize * 0.5f,
                $"a vertex at {where} sits {worst:0.000}m off the surface");
        }

        /// <summary>
        /// Normals must point OUT. Inverted normals make the character render
        /// inside out -- lit from within, with the far side of the body drawn
        /// over the near side.
        /// </summary>
        [Test]
        public void NormalsPointAwayFromTheBody()
        {
            List<FusedBodyShape.Segment> shape = MakeShapeWithoutHands();
            Bounds bounds = FusedBodyShape.BoundsOf(shape);

            FusedBodyMesh.Extract(shape, bounds, FusedBodyShape.CellSize,
                out List<Vector3> vertices, out _);
            Vector3[] normals = FusedBodyMesh.Normals(shape, vertices, 0.004f);

            for (int i = 0; i < vertices.Count; i += 37)   // a spread sample
            {
                Vector3 outward = vertices[i] + normals[i] * 0.02f;
                Vector3 inward = vertices[i] - normals[i] * 0.02f;

                Assert.Greater(FusedBodyShape.Distance(shape, outward),
                    FusedBodyShape.Distance(shape, inward),
                    $"the normal at vertex {i} points into the body");
            }
        }

        // ---- the hands, feet and neck (owner, 2026-10-13) ----

        /// <summary>
        /// Four fingers and a thumb that are really separate: the field is solid along each finger and open between neighbouring fingers.
        /// Measured on the fine grid the hands are built on, the way the fingers must come out.
        /// </summary>
        [Test]
        public void EachHandHasFourSeparateFingersAndAThumb()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            foreach (HumanBodyBones hand in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                List<FusedBodyShape.Segment> pieces = FusedBodyShape.HandPieces(shape, hand);
                Assert.AreEqual(11, pieces.Count);
                // The eight finger pieces come in pairs (root to knuckle, knuckle to tip) after the palm; the thumb's two last.
                for (int f = 0; f < 4; f++)
                {
                    FusedBodyShape.Segment tipPiece = pieces[1 + f * 2 + 1];
                    Vector3 tip = tipPiece.B;
                    Assert.Less(FusedBodyShape.Distance(shape, tip), 0.0f, $"{hand} finger {f}'s tip is not solid");
                    if (f < 3)
                    {
                        // Halfway between this finger and the next, a third of the way out: air.
                        FusedBodyShape.Segment nextRoot = pieces[1 + (f + 1) * 2];
                        FusedBodyShape.Segment root = pieces[1 + f * 2];
                        Vector3 between = Vector3.Lerp(Vector3.Lerp(root.A, root.B, 0.7f), Vector3.Lerp(nextRoot.A, nextRoot.B, 0.7f), 0.5f);
                        Assert.Greater(FusedBodyShape.Distance(shape, between), 0.0f, $"{hand} fingers {f} and {f + 1} have merged into a mitten");
                    }
                }
                Vector3 thumbTip = pieces[10].B;
                Assert.Less(FusedBodyShape.Distance(shape, thumbTip), 0.0f, $"{hand}'s thumb tip is not solid");
                // The thumb stands away from the fingers: the point between the thumb's knuckle and the index finger's root is air.
                Vector3 web = Vector3.Lerp(pieces[9].B, pieces[1].A, 0.5f);
                Assert.Greater(FusedBodyShape.Distance(shape, web), 0.0f, $"{hand}'s thumb has merged into the palm");
            }
        }

        /// <summary>The fingers reach at least as far as fused_body.gd's old capsule hand did: the arms are no shorter.</summary>
        [Test]
        public void TheFingertipsReachAsFarAsTheOldHandDid()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            Vector3 shoulder = Bone(HumanBodyBones.LeftUpperArm);
            Vector3 target = Bone(HumanBodyBones.LeftHand);
            Vector3 along = (target - shoulder).normalized;
            float oldReach = Vector3.Distance(shoulder, target) + FusedBodyShape.ArmRadius;
            float reach = 0.0f;
            foreach (FusedBodyShape.Segment piece in FusedBodyShape.HandPieces(shape, HumanBodyBones.LeftHand))
            {
                reach = Mathf.Max(reach, Vector3.Dot(piece.B - shoulder, along) + piece.Radius);
            }
            Assert.GreaterOrEqual(reach, oldReach - 0.01f, $"the fingertips reach {reach:0.000} m from the shoulder, the old hand reached {oldReach:0.000}");
            Assert.Less(reach, oldReach + 0.04f, "the arm has grown");
        }

        /// <summary>
        /// A foot under each ankle: flat on the ground, reaching forward, no toes (one piece), and nothing of the body below the ground. The
        /// player is no taller: the crown is where it was.
        /// </summary>
        [Test]
        public void TheFeetStandFlatOnTheGroundAndReachForwardWithoutToes()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            foreach (HumanBodyBones foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Vector3 ankle = Bone(foot);
                // Solid well in front of the ankle, just off the ground; air above the toe and behind the heel.
                Assert.Less(FusedBodyShape.Distance(shape, ankle + new Vector3(0.0f, -0.09f, 0.17f)), 0.0f, $"{foot} does not reach forward");
                Assert.Greater(FusedBodyShape.Distance(shape, ankle + new Vector3(0.0f, 0.12f, 0.17f)), 0.0f, $"{foot} is a column, not a foot");
                Assert.Greater(FusedBodyShape.Distance(shape, ankle + new Vector3(0.0f, -0.09f, -0.16f)), 0.0f, $"{foot}'s heel sticks out behind");
                // Flat: the sole at ground level under the ankle and under the toe, within a few millimetres of each other.
                float underAnkle = FusedBodyShape.Distance(shape, new Vector3(ankle.x, 0.0f, ankle.z));
                float underToe = FusedBodyShape.Distance(shape, new Vector3(ankle.x, 0.0f, ankle.z + 0.15f));
                Assert.AreEqual(0.0f, underAnkle, 0.006f, $"{foot}'s sole is not on the ground under the ankle");
                Assert.AreEqual(0.0f, underToe, 0.006f, $"{foot}'s sole is not on the ground under the toe");
                Assert.Greater(FusedBodyShape.Distance(shape, new Vector3(ankle.x, -0.03f, ankle.z + 0.05f)), 0.0f, $"{foot} goes through the floor");
            }
            Assert.AreEqual(2.601f, BodyRig.StandingHeight, 0.0015f, "the player's height changed");
        }

        /// <summary>
        /// The foot's second pass (owner, 2026-10-13: "a tiny bit longer and make them more rounded off at the end, right now they kind of have a
        /// square shape with soft edges ... flat at the bottom"): the toe is a semicircle (its corners are air), the whole sole is flat right out to
        /// its side edge (no rounding under the foot, so it stands on the ground), and the toe reaches a little further than it did.
        /// </summary>
        [Test]
        public void TheToeIsRoundTheSoleIsFlatToItsEdgeAndTheFootIsALittleLonger()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            foreach (HumanBodyBones foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Vector3 ankle = Bone(foot);
                Vector3 sole = new Vector3(ankle.x, 0.0f, ankle.z);
                // Longer: the old foot ended 20 cm in front of the ankle; this one reaches past 21.5.
                Assert.Less(FusedBodyShape.Distance(shape, sole + new Vector3(0.0f, 0.05f, 0.215f)), 0.0f, $"{foot} is no longer than it was");
                // Round toe: a semicircle of the foot's half width, so the box's old corners are air while the centre line is still solid.
                float halfWidth = FusedBodyShape.FootToeRadius;
                Assert.Greater(FusedBodyShape.Distance(shape, sole + new Vector3(halfWidth * 0.85f, 0.05f, 0.19f)), 0.0f, $"{foot}'s toe still has corners");
                Assert.Less(FusedBodyShape.Distance(shape, sole + new Vector3(0.0f, 0.05f, 0.19f)), 0.0f, $"{foot}'s toe is missing");
                // Flat sole with a sharp edge: a centimetre above the ground at the very side of the foot is still foot (the old rounding hollowed this out).
                Assert.Less(FusedBodyShape.Distance(shape, sole + new Vector3(halfWidth * 0.95f, 0.01f, 0.12f)), 0.0f, $"{foot}'s sole is rounded under at the side");
                Assert.AreEqual(0.0f, FusedBodyShape.Distance(shape, sole + new Vector3(halfWidth * 0.9f, 0.0f, 0.12f)), 0.006f, $"{foot}'s sole does not reach the ground at its edge");
                // The top edge is still round: a little in from the side at the top is air.
                Assert.Greater(FusedBodyShape.Distance(shape, sole + new Vector3(halfWidth * 0.97f, FusedBodyShape.FootHeight - 0.005f, 0.12f)), 0.0f, $"{foot}'s top edge is a brick's");
            }
        }

        /// <summary>
        /// The head is not buried in the torso: above the shoulder line the body narrows to the neck, and the torso's top no longer rises
        /// into the skull. The head itself stays where it was.
        /// </summary>
        [Test]
        public void TheHeadSitsOnANeckNotInTheTorsosDome()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            Vector3 neck = Bone(HumanBodyBones.Neck);
            // The body's half-width just above the shoulder line, walking out from the centreline until the first air: the neck's, not the torso's.
            float width = 0.0f;
            for (float x = 0.0f; x < 0.4f; x += 0.005f)
            {
                if (FusedBodyShape.Distance(shape, new Vector3(x, neck.y + 0.06f, 0.0f)) >= 0.0f) break;
                width = x;
            }
            Assert.Less(width, FusedBodyShape.NeckRadius + FusedBodyShape.NeckBlend, $"just above the shoulder line the body is {2 * width:0.00} m wide: a dome, not a neck");
            Assert.Greater(width, FusedBodyShape.NeckRadius - 0.01f, "no neck at all");
            // Nothing of the body up at the middle of the head.
            Assert.Greater(FusedBodyShape.Distance(shape, new Vector3(0.0f, neck.y + 0.22f, 0.0f)), 0.0f, "the torso reaches up into the skull");
            Assert.AreEqual(BodyRig.ShoulderY + FusedBodyShape.NeckLength + FusedBodyShape.HeadRadius, BodyRig.HeadCentreY, 1e-5f, "the head moved");
        }

        /// <summary>No lump at the hips: the pelvis bar ends ARE the thigh tops, so the surface there is one smooth piece, nowhere two bulges a crease apart.</summary>
        [Test]
        public void TheThighsGrowOutOfThePelvisWithoutACrease()
        {
            List<FusedBodyShape.Segment> shape = MakeShape();
            Vector3 hip = Bone(HumanBodyBones.LeftUpperLeg);
            // The outline of the left side, from the waist down to mid thigh: walking out from the thigh's own axis until the first air (the
            // hanging arm is further out and is not the hip). Going down, the outline may only ever get narrower once it has started to; a
            // dip that widens again is a crease between two bulges -- the old thigh cap standing out of the torso.
            float Outline(float y)
            {
                float width = 0.0f;
                for (float x = -hip.x; x < 0.5f; x += 0.002f)
                {
                    if (FusedBodyShape.Distance(shape, new Vector3(-x, y, 0.0f)) >= 0.0f) break;
                    width = x;
                }
                return width;
            }
            // The outline widens from the waist into the hips and narrows again into the thigh: one swell. A dip that widens again is two lumps.
            float peak = 0.0f, lowSinceDip = float.MaxValue, worstBulge = 0.0f;
            bool dipped = false;
            var profile = new System.Text.StringBuilder();
            // And the surface turns smoothly along it: a hard crease (two pieces meeting unblended) flips the normal within a millimetre.
            float worstTurn = 0.0f;
            Vector3 previousNormal = Vector3.zero;
            for (float y = hip.y + 0.25f; y > hip.y - 0.25f; y -= 0.005f)
            {
                float width = Outline(y);
                profile.Append($" {y:0.000}:{width:0.000}");
                if (!dipped)
                {
                    if (width < peak - 0.002f) { dipped = true; lowSinceDip = width; }
                    else peak = Mathf.Max(peak, width);
                }
                else
                {
                    lowSinceDip = Mathf.Min(lowSinceDip, width);
                    worstBulge = Mathf.Max(worstBulge, width - lowSinceDip);
                }
                Vector3 p = new Vector3(-width, y, 0.0f);
                const float e = 0.002f;
                var normal = new Vector3(
                    FusedBodyShape.Distance(shape, p + Vector3.right * e) - FusedBodyShape.Distance(shape, p - Vector3.right * e),
                    FusedBodyShape.Distance(shape, p + Vector3.up * e) - FusedBodyShape.Distance(shape, p - Vector3.up * e),
                    FusedBodyShape.Distance(shape, p + Vector3.forward * e) - FusedBodyShape.Distance(shape, p - Vector3.forward * e)).normalized;
                if (previousNormal != Vector3.zero)
                {
                    worstTurn = Mathf.Max(worstTurn, Vector3.Angle(previousNormal, normal));
                }
                previousNormal = normal;
            }
            Assert.Less(worstBulge, 0.006f, $"the hip's outline narrows and bulges out again by {worstBulge * 1000:0} mm: a lump ({profile})");
            Assert.Less(worstTurn, 20.0f, $"the skin turns {worstTurn:0} degrees within 5 mm down the hip: a crease, not a fillet ({profile})");
            Assert.Less(Outline(hip.y) - Outline(hip.y - 0.25f), 0.08f, "the hips flare far wider than the thighs");
        }

        // ---- skin weights ----

        /// <summary>
        /// A JOINT MUST BE SHARED BETWEEN THE BONES IT JOINS. The skin was once weighted rigidly, one bone per vertex, and it tore: the blob the
        /// smooth union fills at a shoulder was handed entirely to whichever piece happened to be a millimetre nearer, so half the shoulder swung
        /// with the arm and the rest stayed with the chest.
        /// </summary>
        [Test]
        public void JointsAreSharedBetweenTheBonesTheyJoin()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            var bones = new HumanBodyBones[4];
            var weights = new float[4];

            // ON TOP OF THE SHOULDER, not at the joint centre. The centre is
            // buried well inside the arm; the skin that visibly creases
            // when an arm swings is the cap of the blob above it.
            Vector3 shoulder = Bone(HumanBodyBones.LeftUpperArm) + Vector3.up * 0.10f;
            int used = FusedBodyShape.WeightsAt(segments, shoulder, bones, weights);

            Assert.Greater(used, 1,
                "the shoulder is owned by a single bone -- it will tear when the arm swings");

            float heaviest = 0.0f;
            for (int i = 0; i < used; i++)
            {
                heaviest = Mathf.Max(heaviest, weights[i]);
            }
            // fused_body.gd's own narrow softmin: the top of the shoulder really is mostly the spine's. What is forbidden is a rigid joint.
            Assert.Less(heaviest, 0.9f,
                "one bone takes essentially all of the shoulder, which is rigid weighting wearing a blend's clothes");

            // And the elbow, between upper and lower arm.
            Vector3 elbow = Bone(HumanBodyBones.LeftLowerArm);
            used = FusedBodyShape.WeightsAt(segments, elbow, bones, weights);
            Assert.Greater(used, 1, "the elbow is owned by a single bone");
        }

        /// <summary>
        /// Weights must sum to one. Anything less darkens and shrinks a vertex
        /// toward the origin; anything more inflates it.
        /// </summary>
        [Test]
        public void EveryVertexIsFullyAccountedFor()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            List<FusedBodyShape.Segment> shape = MakeShape();
            Bounds bounds = FusedBodyShape.BoundsOf(shape);

            FusedBodyMesh.Extract(shape, bounds, FusedBodyShape.CellSize,
                out List<Vector3> vertices, out _);

            var bones = new HumanBodyBones[4];
            var weights = new float[4];

            for (int i = 0; i < vertices.Count; i += 13)
            {
                int used = FusedBodyShape.WeightsAt(segments, vertices[i], bones, weights);
                Assert.Greater(used, 0, $"vertex {i} is attached to nothing");

                float total = 0.0f;
                for (int slot = 0; slot < used; slot++)
                {
                    Assert.Greater(weights[slot], 0.0f, $"vertex {i} has a zero influence");
                    total += weights[slot];
                }
                Assert.AreEqual(1.0f, total, 0.001f, $"vertex {i} weights sum to {total:0.000}");
            }
        }

        /// <summary>
        /// Far from any joint, a vertex should still belong overwhelmingly to
        /// its own bone. A blend that smears everything is as wrong as no blend
        /// at all -- it makes a hand drag when a shoulder turns.
        /// </summary>
        [Test]
        public void AwayFromAJointOneBoneStillDominates()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            var bones = new HumanBodyBones[4];
            var weights = new float[4];

            Vector3 elbow = Bone(HumanBodyBones.LeftLowerArm);
            Vector3 midForearm = Vector3.Lerp(elbow, LeftWrist(), 0.5f);
            int used = FusedBodyShape.WeightsAt(segments, midForearm, bones, weights);

            float heaviest = 0.0f;
            HumanBodyBones owner = HumanBodyBones.Hips;
            for (int i = 0; i < used; i++)
            {
                if (weights[i] > heaviest)
                {
                    heaviest = weights[i];
                    owner = bones[i];
                }
            }

            Assert.AreEqual(HumanBodyBones.LeftLowerArm, owner,
                "the middle of the forearm is not owned by the forearm");
            Assert.Greater(heaviest, 0.6f,
                "the blend has smeared the forearm across too many bones");
        }

        /// <summary>A hand with fingers is a stiff piece: everything past the wrist is the hand bone's, so the fingers never bend with the forearm.</summary>
        [Test]
        public void TheHandIsStiffAndWhollyTheHandBones()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            List<FusedBodyShape.Segment> shape = MakeShape();
            var bones = new HumanBodyBones[4];
            var weights = new float[4];
            List<FusedBodyShape.Segment> pieces = FusedBodyShape.HandPieces(shape, HumanBodyBones.RightHand);
            // The tip piece of each finger (the pairs after the palm), then the thumb's, which starts nearer the wrist.
            foreach ((int index, float least) in new[] { (2, 0.95f), (4, 0.95f), (6, 0.95f), (8, 0.95f), (10, 0.85f) })
            {
                FusedBodyShape.Segment piece = pieces[index];
                Vector3 p = piece.B + (piece.B - piece.A).normalized * piece.Radius;   // on the very tip
                int used = FusedBodyShape.WeightsAt(segments, p, bones, weights);
                for (int i = 0; i < used; i++)
                {
                    if (bones[i] == HumanBodyBones.RightHand)
                    {
                        Assert.Greater(weights[i], least, $"a fingertip is only {weights[i]:P0} the hand's");
                    }
                }
            }
        }

        /// <summary>
        /// A vertex on the neck must be weighted to the neck, and one on a hand
        /// to that hand. Getting this wrong is what makes a head swing around
        /// when an arm moves.
        /// </summary>
        [Test]
        public void SkinWeightsFollowTheNearestLimb()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();

            // THE TOP OF THE BODY IS THE NECK COLUMN NOW, running from inside the chest up into the head.
            Assert.AreEqual(HumanBodyBones.Neck,
                FusedBodyShape.NearestBone(segments, Bone(HumanBodyBones.Neck) + Vector3.up * 0.08f),
                "the top of the body is not weighted to the neck");

            Assert.AreEqual(HumanBodyBones.LeftHand,
                FusedBodyShape.NearestBone(segments, Bone(HumanBodyBones.LeftHand)),
                "the left hand is not weighted to the left hand");

            Vector3 midShin = Vector3.Lerp(
                Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), 0.5f);
            Assert.AreEqual(HumanBodyBones.RightLowerLeg,
                FusedBodyShape.NearestBone(segments, midShin),
                "the right shin is not weighted to the right shin");

            Assert.AreEqual(HumanBodyBones.RightFoot,
                FusedBodyShape.NearestBone(segments, Bone(HumanBodyBones.RightFoot) + new Vector3(0.0f, -0.08f, 0.15f)),
                "the front of the right foot is not weighted to the right foot");
        }

        /// <summary>
        /// NEAR IS NOT THE SAME AS ATTACHED, and on this body in particular.
        /// The head is a 0.35m sphere centred one radius above the shoulder
        /// line, so its surface passes within a few centimetres of the top of
        /// each shoulder. Weighting purely by distance handed the head about a
        /// third of the shoulder cap, which means looking left pulls both
        /// shoulders round with it.
        /// </summary>
        [Test]
        public void TheHeadDoesNotOwnAnyOfTheShoulders()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            var bones = new HumanBodyBones[4];
            var weights = new float[4];

            Vector3 shoulder = Bone(HumanBodyBones.LeftUpperArm) + Vector3.up * 0.10f;
            int used = FusedBodyShape.WeightsAt(segments, shoulder, bones, weights);

            for (int i = 0; i < used; i++)
            {
                Assert.AreNotEqual(HumanBodyBones.Head, bones[i],
                    "the head drives part of the shoulder");
                Assert.AreNotEqual(HumanBodyBones.RightUpperArm, bones[i],
                    "the far shoulder drives part of this one");
            }
        }

        /// <summary>
        /// The connectivity rule itself, stated directly rather than only
        /// through its consequences.
        /// </summary>
        [Test]
        public void SkinIsSharedOnlyBetweenAttachedBones()
        {
            Assert.IsTrue(FusedBodyShape.AreConnected(
                HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm), "a shoulder");
            Assert.IsTrue(FusedBodyShape.AreConnected(
                HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), "a wrist");
            Assert.IsTrue(FusedBodyShape.AreConnected(
                HumanBodyBones.Hips, HumanBodyBones.LeftUpperArm), "a shoulder to the lower back");
            Assert.IsTrue(FusedBodyShape.AreConnected(
                HumanBodyBones.Hips, HumanBodyBones.Neck), "the collar");
            Assert.IsFalse(FusedBodyShape.AreConnected(
                HumanBodyBones.Hips, HumanBodyBones.Head),
                "the head is three joins from the hips and owns no skin anyway");
            Assert.IsFalse(FusedBodyShape.AreConnected(
                HumanBodyBones.Head, HumanBodyBones.LeftUpperArm), "head to shoulder");
            Assert.IsFalse(FusedBodyShape.AreConnected(
                HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm), "shoulder to shoulder");
            Assert.IsFalse(FusedBodyShape.AreConnected(
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg), "hip to hip");
        }

        // ---- the built mesh asset ----

        /// <summary>
        /// The saved body is ONE closed surface, hands included: no edge with a single triangle (a hole, or the fine hands not zipped onto the
        /// body), counted by position so the paint seams' duplicate vertices do not read as holes.
        /// </summary>
        [Test]
        public void TheSavedBodyIsOneClosedSurfaceHandsIncluded()
        {
            var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            Assert.IsNotNull(mesh);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var byPosition = new Dictionary<Vector3Int, int>();
            var canonical = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = Key(vertices[i]);
                if (!byPosition.TryGetValue(key, out int first))
                {
                    byPosition[key] = first = i;
                }
                canonical[i] = first;
            }
            var edges = new Dictionary<(int, int), int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = canonical[triangles[i]], b = canonical[triangles[i + 1]], c = canonical[triangles[i + 2]];
                foreach ((int p, int q) in new[] { (a, b), (b, c), (c, a) })
                {
                    (int, int) e = p < q ? (p, q) : (q, p);
                    edges[e] = edges.TryGetValue(e, out int n) ? n + 1 : 1;
                }
            }
            int open = 0;
            foreach (KeyValuePair<(int, int), int> edge in edges)
            {
                if (edge.Value == 1) open++;
            }
            Assert.AreEqual(0, open, $"{open} of {edges.Count} edges belong to a single triangle: a hole in the body");
            // Fingers resolved: well over the body grid's count of triangles for a hand.
            Assert.Greater(triangles.Length / 3, 30000, "the body is too coarse to carry fingers");
        }

        /// <summary>
        /// THE SKIN BENDS AT EVERY JOINT AND IS STIFF BETWEEN THEM (owner, 2026-10-13: one smooth body with smooth transitions; before that,
        /// 2026-10-10, twelve rigid tubes). Vertices shared between two bones exist at the shoulders, elbows, hips and knees; the fingers are
        /// wholly the hand's.
        /// </summary>
        [Test]
        public void TheSavedSkinBendsAtTheJointsAndIsStiffBetweenThem()
        {
            var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            Assert.IsNotNull(mesh);
            BoneWeight[] weights = mesh.boneWeights;
            Assert.Greater(weights.Length, 1000);
            Assert.GreaterOrEqual(mesh.bindposes.Length, 15, "torso, spine, neck, three per arm, three per leg");
            // Shared MEANINGFULLY: fused_body.gd's softmin gives nearly every vertex a few percent of a neighbouring bone (harmless); a joint
            // is where a second bone holds a real share.
            int shared = 0;
            foreach (BoneWeight w in weights)
            {
                if (w.weight0 < 0.9f) shared++;
            }
            // The elbows and knees hand over 2.5x wider than the other joints, so about half the vertices carry a real second share.
            Assert.Greater(shared, weights.Length / 50, "almost nothing is shared between two bones: the joints are rigid again");
            Assert.Less(shared, weights.Length * 6 / 10, "most of the skin is shared between bones: it will drag");
        }
    }
}
