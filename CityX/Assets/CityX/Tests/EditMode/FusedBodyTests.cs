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
    /// actually round. Every one of those is a claim about geometry, and can
    /// be measured.
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
            return BodyRig.PositionOf(bone);
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

        [Test]
        public void ThereIsATorsoAHeadAndFourLimbs()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();

            // torso, neck, head, three capsules per arm, two per leg.
            Assert.AreEqual(3 + 3 + 3 + 2 + 2, segments.Count);

            var bones = new HashSet<HumanBodyBones>();
            foreach (FusedBodyShape.Segment segment in segments)
            {
                bones.Add(segment.Bone);
            }
            Assert.IsTrue(bones.Contains(HumanBodyBones.Head), "no head");
            Assert.IsTrue(bones.Contains(HumanBodyBones.LeftHand), "no left hand");
            Assert.IsTrue(bones.Contains(HumanBodyBones.RightLowerLeg), "no right shin");

            // Deliberately absent: the shin capsule's own rounded end is the
            // foot, exactly as in the original.
            Assert.IsFalse(bones.Contains(HumanBodyBones.RightFoot),
                "a foot capsule would make the character club-footed");
        }

        /// <summary>
        /// The head is a SPHERE and stays the size it was drawn. This is the
        /// one proportion that was explicitly asked to survive the move to
        /// human limb lengths, so it gets its own test rather than being
        /// implied by the others.
        /// </summary>
        [Test]
        public void TheHeadIsASphereOfTheOriginalSize()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();

            FusedBodyShape.Segment head = segments.Find(s => s.Bone == HumanBodyBones.Head);
            Assert.AreEqual(head.A, head.B, "the head is a capsule, not a sphere");
            Assert.AreEqual(FusedBodyShape.HeadRadius, head.Radius, 0.001f,
                "the head is not the size it was drawn at");
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
                Assert.AreEqual(full[i].Radius * 0.5f, half[i].Radius, 0.0001f,
                    "a half-height character is not half as thick");
            }
        }

        [Test]
        public void TheSurfaceIsInsideTheBodyAndOutsideTheAir()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();

            Assert.Less(FusedBodyShape.Distance(segments, new Vector3(0.0f, 1.3f, 0.0f)), 0.0f,
                "the middle of the torso is not inside the body");
            Assert.Less(FusedBodyShape.Distance(segments, Bone(HumanBodyBones.Head)), 0.0f,
                "the middle of the head is not inside the body");
            Assert.Greater(FusedBodyShape.Distance(segments, new Vector3(0.0f, 5.0f, 0.0f)), 0.0f,
                "a point well above the character is inside it");
            Assert.Greater(FusedBodyShape.Distance(segments, new Vector3(3.0f, 1.3f, 0.0f)), 0.0f,
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
            List<FusedBodyShape.Segment> segments = MakeSegments();
            Vector3 shoulder = Bone(HumanBodyBones.LeftUpperArm);

            // Just outside both the arm and the torso, in the crease between
            // them. A hard union would put this point outside the surface; the
            // smooth one fills it in.
            var probe = shoulder + new Vector3(0.0f, 0.12f, 0.12f);

            float hardUnion = float.MaxValue;
            foreach (FusedBodyShape.Segment segment in segments)
            {
                hardUnion = Mathf.Min(hardUnion, segment.Distance(probe));
            }
            float blended = FusedBodyShape.Distance(segments, probe);

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
        /// The extracted surface is closed. Exactly closed, no allowance.
        ///
        /// THIS USED TO ALLOW 1.8% AND CALLED THAT A REAL DEFECT IN THE
        /// TESSELLATION -- slivers along the tetrahedron diagonals where two
        /// cells were said to have disagreed. That diagnosis was wrong. The
        /// tetrahedra agree perfectly; every cube face is cut by the same
        /// diagonal from both sides. What was mismatched was the WINDING,
        /// because a final pass turned each triangle to face the field's
        /// gradient one triangle at a time, and two faces sharing an edge could
        /// come out disagreeing. Nothing was ever missing, so nothing needed
        /// tolerating.
        ///
        /// Worth stating because "it looks solid at playing distance" was
        /// offered as the reason not to chase it, and the visible symptom --
        /// black specks on the skin, which are single back-facing triangles --
        /// was there the whole time.
        /// </summary>
        [Test]
        public void TheExtractedSurfaceIsClosed()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, FusedBodyShape.CellSize,
                out List<Vector3> vertices, out List<int> triangles);

            Assert.Greater(vertices.Count, 500, "the body extracted to almost nothing");
            Assert.AreEqual(0, triangles.Count % 3, "a partial triangle");

            // In a closed surface every edge is shared by exactly two
            // triangles, once in each direction. A boundary edge -- one used
            // in only one direction -- is a hole.
            var edges = new HashSet<(int, int)>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                AddEdge(edges, triangles[i], triangles[i + 1]);
                AddEdge(edges, triangles[i + 1], triangles[i + 2]);
                AddEdge(edges, triangles[i + 2], triangles[i]);
            }

            int unmatched = 0;
            foreach ((int a, int b) in edges)
            {
                if (!edges.Contains((b, a)))
                {
                    unmatched++;
                }
            }

            float share = (float)unmatched / edges.Count;
            Assert.AreEqual(0, unmatched,
                $"{unmatched} of {edges.Count} edges have no facing pair ({share:P2})");
        }

        private static void AddEdge(HashSet<(int, int)> edges, int a, int b)
        {
            edges.Add((a, b));
        }

        /// <summary>
        /// Every vertex has to sit on the surface, not near it. A vertex placed
        /// by bad interpolation reads as a spike sticking out of the character.
        /// </summary>
        [Test]
        public void EveryVertexSitsOnTheSurface()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, FusedBodyShape.CellSize,
                out List<Vector3> vertices, out _);

            float worst = 0.0f;
            foreach (Vector3 vertex in vertices)
            {
                worst = Mathf.Max(worst, Mathf.Abs(FusedBodyShape.Distance(segments, vertex)));
            }

            // Linear interpolation along an edge cannot be exact on a curved
            // field, so the tolerance is a fraction of a cell rather than zero.
            Assert.Less(worst, FusedBodyShape.CellSize * 0.5f,
                $"a vertex sits {worst:0.000}m off the surface");
        }

        /// <summary>
        /// Normals must point OUT. Inverted normals make the character render
        /// inside out -- lit from within, with the far side of the body drawn
        /// over the near side.
        /// </summary>
        [Test]
        public void NormalsPointAwayFromTheBody()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, FusedBodyShape.CellSize,
                out List<Vector3> vertices, out _);
            Vector3[] normals = FusedBodyMesh.Normals(
                segments, vertices, FusedBodyShape.CellSize * 0.5f);

            for (int i = 0; i < vertices.Count; i += 37)   // a spread sample
            {
                Vector3 outward = vertices[i] + normals[i] * 0.05f;
                Vector3 inward = vertices[i] - normals[i] * 0.05f;

                Assert.Greater(FusedBodyShape.Distance(segments, outward),
                    FusedBodyShape.Distance(segments, inward),
                    $"the normal at vertex {i} points into the body");
            }
        }

        /// <summary>
        /// A JOINT MUST BE SHARED BETWEEN THE BONES IT JOINS. This is the test
        /// that was missing, and its absence cost a visible bug: the skin was
        /// weighted rigidly, one bone per vertex, faithfully copying the
        /// original -- and it tore. The blob the smooth union fills at a
        /// shoulder was handed entirely to whichever capsule happened to be a
        /// millimetre nearer, so half the shoulder swung with the arm and the
        /// rest stayed with the chest, stretching the seam between them.
        ///
        /// "Every vertex is attached to something" was true the whole time. The
        /// property that actually matters is that no single bone owns a joint.
        /// </summary>
        [Test]
        public void JointsAreSharedBetweenTheBonesTheyJoin()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();
            var bones = new HumanBodyBones[4];
            var weights = new float[4];

            // ON TOP OF THE SHOULDER, not at the joint centre. The centre is
            // buried well inside the arm capsule; the skin that visibly creases
            // when an arm swings is the cap of the blob above it, and that is
            // what has to be shared.
            Vector3 shoulder = Bone(HumanBodyBones.LeftUpperArm) + Vector3.up * 0.10f;
            int used = FusedBodyShape.WeightsAt(segments, shoulder, bones, weights);

            Assert.Greater(used, 1,
                "the shoulder is owned by a single bone -- it will tear when the arm swings");

            float heaviest = 0.0f;
            for (int i = 0; i < used; i++)
            {
                heaviest = Mathf.Max(heaviest, weights[i]);
            }
            Assert.Less(heaviest, 0.8f,
                "one bone takes essentially all of the shoulder, which is rigid weighting " +
                "wearing a blend's clothes");

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
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, FusedBodyShape.CellSize,
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

            // THE EXACT MIDPOINT, which is the only point on a forearm that is
            // genuinely away from a joint. This probe was at 0.35 first and
            // reported 57% -- but a forearm is only about two and a half radii
            // long, so 0.35 along it is still inside the elbow's handover and
            // SHOULD be shared. Testing there measured the elbow and called it
            // the forearm.
            Vector3 midForearm = Vector3.Lerp(
                Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand), 0.5f);

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

        /// <summary>
        /// A vertex on the head must be weighted to the head, and one on a hand
        /// to that hand. Getting this wrong is what makes a head swing around
        /// when an arm moves.
        /// </summary>
        [Test]
        public void SkinWeightsFollowTheNearestLimb()
        {
            List<FusedBodyShape.Segment> segments = MakeSegments();

            Assert.AreEqual(HumanBodyBones.Head,
                FusedBodyShape.NearestBone(segments, Bone(HumanBodyBones.Head) + Vector3.up * 0.3f),
                "the top of the head is not weighted to the head");

            Assert.AreEqual(HumanBodyBones.LeftHand,
                FusedBodyShape.NearestBone(segments, Bone(HumanBodyBones.LeftHand)),
                "the left hand is not weighted to the left hand");

            // MID-shin, not the knee. The knee is the exact point where the
            // thigh and shin capsules meet, so both are equidistant there and
            // whichever wins is arbitrary -- asserting on it tests the order of
            // a list rather than the shape.
            Vector3 midShin = Vector3.Lerp(
                Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), 0.5f);
            Assert.AreEqual(HumanBodyBones.RightLowerLeg,
                FusedBodyShape.NearestBone(segments, midShin),
                "the right shin is not weighted to the right shin");
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
                HumanBodyBones.Hips, HumanBodyBones.LeftUpperArm), "a shoulder");
            Assert.IsTrue(FusedBodyShape.AreConnected(
                HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), "a wrist");

            // Two apart, and they do have to share: the neck between the head
            // and the torso is short enough that the head sphere and the torso
            // capsule overlap directly.
            Assert.IsTrue(FusedBodyShape.AreConnected(
                HumanBodyBones.Hips, HumanBodyBones.Head), "the base of the skull");

            // Reached by going up and back down again. Near, not attached.
            Assert.IsFalse(FusedBodyShape.AreConnected(
                HumanBodyBones.Head, HumanBodyBones.LeftUpperArm), "head to shoulder");
            Assert.IsFalse(FusedBodyShape.AreConnected(
                HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm), "shoulder to shoulder");
            Assert.IsFalse(FusedBodyShape.AreConnected(
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg), "hip to hip");
        }
    }
}
