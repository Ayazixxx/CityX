using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The shape of a CITY X character, as a signed distance field. Ported from
    /// character/fused_body.gd, and grown since.
    ///
    /// This is what the game's people actually look like: smooth-blended
    /// capsules for the torso and limbs, hands with fingers, feet, a neck, and
    /// a sphere for a head (its own mesh, see HeadMesh). Not a humanoid mesh --
    /// the Mixamo mannequin was only ever a source of animation clips.
    ///
    /// AN SDF RATHER THAN ASSEMBLED PRIMITIVES. Separate capsules would meet at
    /// the shoulders and hips in hard intersecting seams. A smooth minimum
    /// fuses them into one continuous surface, which is the entire reason the
    /// Godot original is called a FUSED body, and it is why the shape has to be
    /// extracted rather than built.
    ///
    /// ONE SURFACE AGAIN (owner, 2026-10-13: "I want it to be a smooth body all
    /// throughout ... transitions between limbs smoother, there shouldnt be
    /// visible lines or lumps at the hips", pointing at Meccha Chameleon's
    /// figure). From 2026-10-10 the body was twelve separate tubes, one per
    /// bone, overlapping at the joints -- which showed a line at every elbow,
    /// wrist and knee and the thigh caps bulging out of the hips. Every part is
    /// fused into one field now, and the skin is weighted across each joint
    /// (<see cref="WeightsAt"/>) so it bends instead of coming apart.
    ///
    /// PROPORTIONS COME FROM THE SKELETON (BodyRig, fused_body.gd's own joint
    /// coordinates), THICKNESSES COME FROM HERE.
    /// </summary>
    public static class FusedBodyShape
    {
        /// <summary>
        /// The height the Godot radii below were authored against. Everything
        /// here is divided by it and multiplied by the real rig's height, so
        /// the character is the same SHAPE at any size.
        /// </summary>
        public const float ReferenceHeight = 2.2f;

        // --- fused_body.gd's own radii, in metres at ReferenceHeight ---

        public const float TorsoRadius = 0.26f;
        public const float ArmRadius = 0.09f;
        public const float LegRadius = 0.135f;

        /// <summary>
        /// The limbs taper, as limbs do: an arm is <see cref="ArmRadius"/> at the shoulder and this at the wrist, a leg <see cref="LegRadius"/> at the hip
        /// and <see cref="AnkleRadius"/> at the ankle. One round cone per limb, shoulder to wrist and hip to ankle, so there is no seam to bulge at an
        /// elbow or a knee (see <see cref="LimbCapsules"/>). Gentle on purpose: the character's own chunky look stays.
        /// </summary>
        public const float WristRadius = 0.07f;
        public const float AnkleRadius = 0.105f;

        /// <summary>fused_body.gd NECK_LEN. Where the head bone sits above the shoulder line (BodyRig.HeadCentreY): kept at its tiny value so the head does not move.</summary>
        public const float NeckLength = 0.001f;

        /// <summary>
        /// THE NECK IS A COLUMN NOW, not a bump buried in the torso (owner, 2026-10-13: "the head just clips into the torso"). The torso used to be a
        /// capsule up to the shoulder line, whose rounded top rose 0.26 above it and swallowed the lower quarter of the head sphere. The torso now
        /// stops <see cref="TorsoTopBelowShoulders"/> short of the shoulder line (its dome tops out level with the shoulders), and a neck this
        /// thick runs from inside the chest up into the head, flared into the shoulders by its own wider blend (<see cref="NeckBlend"/>), so the
        /// head sits in a collar instead of a dome. The head's own size and height are unchanged (BodyRig.HeadCentreY), so the player is no taller.
        /// </summary>
        public const float NeckRadius = 0.115f;
        public const float NeckColumnBelowShoulders = 0.12f;
        /// <summary>The column's axis ends this far above the shoulder line; its rounded top is then well inside the head sphere, which is as wide as the neck 2 cm above its bottom.</summary>
        public const float NeckColumnAboveShoulders = 0.04f;
        /// <summary>The torso capsule's dome then tops out at the shoulder line, under the head's underside.</summary>
        public const float TorsoTopBelowShoulders = 0.26f;
        public const float NeckBlend = 0.09f;

        /// <summary>
        /// A bar across the hips, nearly as thick as a thigh, ending a little inside each thigh's top: the legs grow out of a pelvis in one
        /// smooth surface instead of two thigh caps bulging out of the bottom of the torso tube ("lumps at the hips"). Its ends stop short of
        /// the hip joints so the bar is only ever near the inner side of a thigh top -- a bar ending exactly on the joint made every point of
        /// the thigh's top equally the pelvis's and the thigh's, and a swinging leg dragged half its own top along.
        /// </summary>
        public const float PelvisRadius = 0.12f;
        public const float PelvisEndsInsideHips = 0.05f;

        /// <summary>
        /// THE SHOULDERS ARE A LEVEL BAR, STRAIGHT ACROSS (owner, 2026-10-13: "i dont like how the shoulder are like angled upward like that it
        /// used to be straight across"): a bar as thick as an arm from one shoulder to the other, on the spine, its rounded ends the arms' own
        /// root balls, so the top of the body is one flat shoulder line with the arms hanging off its corners -- the look the body had before.
        /// It sits <see cref="ArmRootBelowShoulder"/> below the shoulder JOINTS, and so do the arms' roots: at the joints' own height its top
        /// would stand 9 cm up into the head sphere (the head bottom is a millimetre above the shoulder line, BodyRig.HeadCentreY), which was
        /// the "head clips into the torso". Lower, its top just meets the head's underside at the neck. A joint pivots about its own centre, so an
        /// arm root hung a little below it still turns cleanly; raised sideways the root swings in to sit on the bar's end.
        /// </summary>
        public const float ArmRootBelowShoulder = 0.06f;
        public const float ShoulderBarEndsInside = 0.03f;

        /// <summary>
        /// For the skin weights the bar ends this far inside the arm's root instead, so the root ball is the arm's (a bar ending on the ball
        /// made the ball's top half the spine's, and a raised arm pulled it into a cone) and only the fillet between them is shared.
        /// </summary>
        public const float ShoulderBarEndsInsideForWeights = 0.09f;

        /// <summary>
        /// character.gd's head_size is a 0.7 cube, so the sphere that fills it
        /// has this radius. Deliberately unchanged from the Godot build.
        /// </summary>
        public const float HeadRadius = 0.35f;

        /// <summary>
        /// fused_body.gd BLEND_K. How far either side of a join the surface
        /// rounds. Too small and the seams come back; too large and the whole
        /// figure melts into a blob.
        /// </summary>
        public const float BlendRadius = 0.06f;

        /// <summary>
        /// The extraction grid. 0.045 was fused_body.gd's CELL_SIZE; finer now that the joints are fillets a blend radius wide, which a 0.045 cell
        /// cut into one visible facet. Halving a cell quadruples the triangles; this is about twice fused_body.gd's count for the body.
        /// </summary>
        public const float CellSize = 0.03f;

        /// <summary>The hands are extracted on a grid this fine: a finger is under two cells of the body grid across, and the groove between two fingers one of these. See FusedBodyMesh.ExtractBody.</summary>
        public const float HandCellSize = 0.009f;

        // --- the hands (owner, 2026-10-13: "turn the bottom hand rounded off parts into actual hands with fingers", with a cartoon hand drawn: a
        // --- broad palm, four fingers, a thumb, a narrow wrist). A relaxed hand rather than the drawing's flat open one: the fingers curl a little
        // --- toward the palm, which reads as a hand from every angle and closes loosely round a held grip. All in metres at ReferenceHeight,
        // --- measured from the wrist joint along the arm.

        // FAT FINGERS ON A WIDE, FLAT PALM (owner, 2026-10-13: "the fingers needs to be a bit fatter the transition from hand size to finger
        // size is too big"): a finger is 5 cm thick on an 8.4 cm palm, four of them as wide as the palm, parted by grooves rather than gaps,
        // as in the owner's drawing.
        public const float PalmLength = 0.16f;
        public const float PalmHalfWidth = 0.11f;
        public const float PalmHalfThickness = 0.042f;
        public const float PalmRounding = 0.036f;
        public const float FingerRadius = 0.025f;
        public const float FingerSpacing = 0.058f;
        public static readonly float[] FingerLengths = { 0.088f, 0.098f, 0.09f, 0.072f };   // index, middle, ring, little
        public const float FingerCurlDegrees = 20.0f;
        public const float FingerTipCurlDegrees = 50.0f;
        public const float FingerKnuckleShare = 0.55f;
        public const float ThumbRadius = 0.027f;
        public const float ThumbLength = 0.06f;
        public const float ThumbTipLength = 0.052f;

        /// <summary>Fingers fuse to the palm and each other over a narrow band (webbing); the palm fuses to the wrist over a wider one.</summary>
        public const float FingerBlend = 0.008f;
        public const float PalmBlend = 0.04f;

        /// <summary>
        /// One digit of a hand: its name, its two bones on each hand (root to knuckle, knuckle to tip) and its joints in the HAND'S OWN FRAME --
        /// x down the arm to the fingertips from the wrist, y the back of the hand, z the thumb's side -- in metres at the reference height. The
        /// same table builds the shape's finger pieces (<see cref="AddHand"/>) and places the finger bones (BodyRig.Bones), so the skin's joints
        /// and the bones' joints cannot disagree.
        /// </summary>
        public readonly struct Digit
        {
            public readonly string Name;
            public readonly HumanBodyBones LeftProximal;
            public readonly HumanBodyBones LeftIntermediate;
            public readonly HumanBodyBones RightProximal;
            public readonly HumanBodyBones RightIntermediate;
            public readonly Vector3 Root;
            public readonly Vector3 Knuckle;
            public readonly Vector3 Tip;
            public readonly float Radius;

            public Digit(string name, HumanBodyBones leftProximal, HumanBodyBones rightProximal, Vector3 root, Vector3 knuckle, Vector3 tip, float radius)
            {
                Name = name;
                LeftProximal = leftProximal;
                LeftIntermediate = leftProximal + 1;
                RightProximal = rightProximal;
                RightIntermediate = rightProximal + 1;
                Root = root;
                Knuckle = knuckle;
                Tip = tip;
                Radius = radius;
            }
        }

        /// <summary>Index, middle, ring, little, then the thumb (the order the shape's hand pieces come in after the palm).</summary>
        public static readonly Digit[] Digits = BuildDigits();

        private static Digit[] BuildDigits()
        {
            var digits = new Digit[5];
            // The fingers from the knuckle line, each bent a little toward the palm (-y) at the knuckle and more at the tip; the index nearest the
            // thumb's side (+z), the little finger at the back.
            var curl1 = new Vector3(Mathf.Cos(FingerCurlDegrees * Mathf.Deg2Rad), -Mathf.Sin(FingerCurlDegrees * Mathf.Deg2Rad), 0.0f);
            var curl2 = new Vector3(Mathf.Cos(FingerTipCurlDegrees * Mathf.Deg2Rad), -Mathf.Sin(FingerTipCurlDegrees * Mathf.Deg2Rad), 0.0f);
            string[] names = { "index", "middle", "ring", "little" };
            HumanBodyBones[] left = { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal };
            HumanBodyBones[] right = { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal };
            for (int i = 0; i < 4; i++)
            {
                var root = new Vector3(PalmLength - 0.01f, 0.0f, (1.5f - i) * FingerSpacing);
                Vector3 knuckle = root + curl1 * (FingerLengths[i] * FingerKnuckleShare);
                Vector3 tip = knuckle + curl2 * (FingerLengths[i] * (1.0f - FingerKnuckleShare));
                digits[i] = new Digit(names[i], left[i], right[i], root, knuckle, tip, FingerRadius);
            }
            // The thumb from the side of the palm near the wrist, out past the index finger and folding across.
            var thumbRoot = new Vector3(0.045f, 0.0f, PalmHalfWidth - 0.01f);
            Vector3 thumbDir1 = new Vector3(0.45f, -0.25f, 0.8f).normalized;
            Vector3 thumbDir2 = new Vector3(0.6f, -0.6f, 0.45f).normalized;
            Vector3 thumbKnuckle = thumbRoot + thumbDir1 * ThumbLength;
            Vector3 thumbTip = thumbKnuckle + thumbDir2 * ThumbTipLength;
            digits[4] = new Digit("thumb", HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal, thumbRoot, thumbKnuckle, thumbTip, ThumbRadius);
            return digits;
        }

        /// <summary>True for the hand bone itself and for any finger bone of that hand: the pieces that make up the hand.</summary>
        public static bool BelongsToHand(HumanBodyBones bone, HumanBodyBones hand)
        {
            return bone == hand || (BodyRig.IsFinger(bone) && BodyRig.HandOf(bone) == hand);
        }

        // --- the feet (owner, 2026-10-13: "a basic foot with no toes", drawn as a sock: the shin turning a corner into a flat-soled, round-toed
        // --- foot; later the same day: "a tiny bit longer ... rounded off at the end, right now they kind of have a square shape with soft
        // --- edges ... flat at the bottom", for the foot IK that stands the sole on real ground). A STADIUM in plan (a semicircular toe and heel,
        // --- straight sides), extruded straight up from a FLAT sole with only its top edge rounded over, under the ankle, fused into the shin. The
        // --- player is no taller: the foot sits in the space the shin's rounded bottom used to take, with the ankle joint where the foot bone already was.

        /// <summary>Half the foot's width: the radius of the toe's and heel's semicircles (as wide as the shin's ankle, so no bulb sits above a narrower heel).</summary>
        public const float FootToeRadius = 0.10f;
        public const float FootHeelRadius = 0.10f;
        /// <summary>Where the toe's and heel's arcs are centred, forward of the ankle: the heel reaches 9 cm back, the toe 22.5 cm forward (was 20).</summary>
        public const float FootToeCentreForward = 0.125f;
        public const float FootHeelCentreForward = 0.01f;
        /// <summary>The foot's height, sole to top: the ankle joint sits a little under the top, inside it.</summary>
        public const float FootHeight = 0.15f;
        /// <summary>Only the TOP edge is rounded, by this much; the sole's edge is sharp, so the whole sole is flat on the ground.</summary>
        public const float FootTopRounding = 0.05f;
        public const float FootBlend = 0.045f;

        public enum SegmentKind
        {
            Capsule = 0,
            RoundBox = 1,
            /// <summary>A foot: a stadium in plan, flat-soled, round-topped (see <see cref="Segment.Foot"/>).</summary>
            Foot = 2,
        }

        /// <summary>
        /// One piece of the body, and which bone owns it: a capsule (a round cone when its two radii differ) or a rounded box (a palm, a foot).
        /// </summary>
        public readonly struct Segment
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            /// <summary>A capsule's radius at A; a box's edge rounding.</summary>
            public readonly float Radius;
            /// <summary>A capsule's radius at B: a round cone when it differs from <see cref="Radius"/>.</summary>
            public readonly float RadiusB;
            public readonly HumanBodyBones Bone;
            public readonly SegmentKind Kind;
            /// <summary>A box's half extents, rounding included.</summary>
            public readonly Vector3 Half;
            /// <summary>A box's orientation, and its inverse (kept, since the field is sampled millions of times).</summary>
            public readonly Quaternion Frame;
            public readonly Quaternion FrameInverse;
            /// <summary>How wide a band this piece rounds into the pieces before it in the shape (SmoothMin's k).</summary>
            public readonly float Blend;

            public Segment(Vector3 a, Vector3 b, float radius, HumanBodyBones bone)
                : this(a, b, radius, radius, bone, BlendRadius)
            {
            }

            public Segment(Vector3 a, Vector3 b, float radiusA, float radiusB, HumanBodyBones bone, float blend)
            {
                A = a;
                B = b;
                Radius = radiusA;
                RadiusB = radiusB;
                Bone = bone;
                Kind = SegmentKind.Capsule;
                Half = Vector3.zero;
                Frame = Quaternion.identity;
                FrameInverse = Quaternion.identity;
                Blend = blend;
            }

            private Segment(Vector3 centre, Quaternion frame, Vector3 half, float rounding, HumanBodyBones bone, float blend)
            {
                A = centre;
                B = centre;
                Radius = rounding;
                RadiusB = rounding;
                Bone = bone;
                Kind = SegmentKind.RoundBox;
                Half = half;
                Frame = frame;
                FrameInverse = Quaternion.Inverse(frame);
                Blend = blend;
            }

            /// <summary>A rounded box: <paramref name="half"/> extents include the <paramref name="rounding"/> of its edges.</summary>
            public static Segment Box(Vector3 centre, Quaternion frame, Vector3 half, float rounding, HumanBodyBones bone, float blend)
            {
                return new Segment(centre, frame, half, rounding, bone, blend);
            }

            /// <summary>
            /// A FOOT: in plan (the frame's x and z) a stadium from the heel's arc centre <paramref name="heel"/> (radius <paramref name="heelRadius"/>) to
            /// the toe's arc centre <paramref name="toe"/> (radius <paramref name="toeRadius"/>), both given AT SOLE LEVEL; extruded straight up the frame's y
            /// to <paramref name="height"/>, its top edge rounded by <paramref name="topRounding"/> and its sole left FLAT with a sharp edge, so the whole
            /// sole lies on the ground. The toe's end is a true semicircle, not a softened corner.
            /// </summary>
            public static Segment Foot(Vector3 heel, Vector3 toe, float heelRadius, float toeRadius, float height, float topRounding, Quaternion frame,
                HumanBodyBones bone, float blend)
            {
                return new Segment(heel, toe, heelRadius, toeRadius, height, topRounding, frame, bone, blend);
            }

            private Segment(Vector3 heel, Vector3 toe, float heelRadius, float toeRadius, float height, float topRounding, Quaternion frame,
                HumanBodyBones bone, float blend)
            {
                A = heel;
                B = toe;
                Radius = heelRadius;
                RadiusB = toeRadius;
                Bone = bone;
                Kind = SegmentKind.Foot;
                Half = new Vector3(topRounding, height, 0.0f);
                Frame = frame;
                FrameInverse = Quaternion.Inverse(frame);
                Blend = blend;
            }

            public bool IsBox => Kind == SegmentKind.RoundBox;

            public bool IsFoot => Kind == SegmentKind.Foot;

            /// <summary>How far from A and B the piece can reach: for a bounding box round it.</summary>
            public float Extent => IsBox ? Half.magnitude : IsFoot ? (B - A).magnitude + Mathf.Max(Radius, RadiusB) + Half.y : Mathf.Max(Radius, RadiusB);

            /// <summary>Distance from a point to this piece's surface: negative inside.</summary>
            public float Distance(Vector3 p)
            {
                if (Kind == SegmentKind.RoundBox)
                {
                    Vector3 q = FrameInverse * (p - A);
                    q = new Vector3(Mathf.Abs(q.x), Mathf.Abs(q.y), Mathf.Abs(q.z)) - (Half - Vector3.one * Radius);
                    Vector3 outside = Vector3.Max(q, Vector3.zero);
                    return outside.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0.0f) - Radius;
                }
                if (Kind == SegmentKind.Foot)
                {
                    return FootDistance(p);
                }

                Vector3 pa = p - A;
                Vector3 ba = B - A;
                float l2 = Vector3.Dot(ba, ba);
                // A zero-length capsule is a sphere, not a division by zero.
                if (l2 <= 1e-12f)
                {
                    return pa.magnitude - Radius;
                }
                float rr = Radius - RadiusB;
                if (Mathf.Abs(rr) < 1e-7f)
                {
                    float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / l2);
                    return (pa - ba * h).magnitude - Radius;
                }

                // A round cone (two spheres and the cone tangent to both): Inigo Quilez's exact distance, one square root.
                float a2 = l2 - rr * rr;
                float il2 = 1.0f / l2;
                float y = Vector3.Dot(pa, ba);
                float z = y - l2;
                Vector3 x2v = pa * l2 - ba * y;
                float x2 = Vector3.Dot(x2v, x2v);
                float y2 = y * y * l2;
                float z2 = z * z * l2;
                float k = Mathf.Sign(rr) * rr * rr * x2;
                if (Mathf.Sign(z) * a2 * z2 > k)
                {
                    return Mathf.Sqrt(x2 + z2) * il2 - RadiusB;
                }
                if (Mathf.Sign(y) * a2 * y2 < k)
                {
                    return Mathf.Sqrt(x2 + y2) * il2 - Radius;
                }
                return (Mathf.Sqrt(x2 * a2 * il2) + y * rr) * il2 - Radius;
            }

            /// <summary>A capsule's radius at a fraction along it.</summary>
            public float RadiusAt(float t) => Mathf.Lerp(Radius, RadiusB, Mathf.Clamp01(t));

            /// <summary>
            /// The foot's distance: the plan stadium (an uneven 2-D capsule, Inigo Quilez's exact distance) shrunk by the top rounding, extruded from far
            /// below the sole up to the top less the rounding, rounded back out by it, then CUT OFF at the sole plane -- so the top edge is round and the
            /// sole is a flat plane with a sharp edge. Exact outside and above the sole; inside, the cut makes it the distance to the nearest of the two.
            /// </summary>
            private float FootDistance(Vector3 p)
            {
                float rounding = Half.x;
                Vector3 q = FrameInverse * (p - A);
                Vector3 axis = FrameInverse * (B - A);
                float px = q.x, pz = q.z;             // the point in plan, from the heel's arc centre
                float dx = axis.x, dz = axis.z;       // heel to toe in plan
                float h = Mathf.Sqrt(dx * dx + dz * dz);
                float r1 = Radius - rounding;
                float r2 = RadiusB - rounding;
                float plan;
                if (h < 1e-5f)
                {
                    plan = Mathf.Sqrt(px * px + pz * pz) - Mathf.Max(r1, r2);
                }
                else
                {
                    float ux = dx / h, uz = dz / h;
                    float along = px * ux + pz * uz;
                    float perp = Mathf.Abs(px * uz - pz * ux);
                    float b = (r1 - r2) / h;
                    float a = Mathf.Sqrt(Mathf.Max(1.0f - b * b, 0.0f));
                    float k = -b * perp + a * along;
                    if (k < 0.0f)
                    {
                        plan = Mathf.Sqrt(perp * perp + along * along) - r1;
                    }
                    else if (k > a * h)
                    {
                        plan = Mathf.Sqrt(perp * perp + (along - h) * (along - h)) - r2;
                    }
                    else
                    {
                        plan = a * perp + b * along - r1;
                    }
                }
                // Extrude between far below the sole and the top (less the rounding), round, cut at the sole.
                float innerTop = Half.y - rounding;
                const float innerBottom = -1.0f;
                float cy = 0.5f * (innerTop + innerBottom);
                float hy = 0.5f * (innerTop - innerBottom);
                float wx = plan;
                float wy = Mathf.Abs(q.y - cy) - hy;
                float ox = Mathf.Max(wx, 0.0f);
                float oy = Mathf.Max(wy, 0.0f);
                float extruded = Mathf.Min(Mathf.Max(wx, wy), 0.0f) + Mathf.Sqrt(ox * ox + oy * oy);
                return Mathf.Max(extruded - rounding, -q.y);
            }
        }

        /// <summary>
        /// The body's pieces, ONE PER BONE, for deciding which bone owns each vertex (<see cref="WeightsAt"/>) and which paint row it lands in
        /// (<see cref="DominantBone"/>): the torso split at SPINE_T into the hips' half and the spine's, each arm split at the elbow and the
        /// wrist, each leg at the knee and the ankle, and the hands and feet their own bones'.
        ///
        /// NOT THE SHAPE. Two colinear pieces of one limb that share an endpoint push the fused surface outward by a quarter of the blend radius
        /// right at the seam (SmoothMin(d, d, k) is d - k/4), so the surface itself is extracted from <see cref="LimbCapsules"/>, where each limb
        /// is one unbroken piece. Both lists agree on every radius, so a point is on the skin of one exactly where it is on the skin of the other,
        /// bar that seam bulge.
        ///
        /// <paramref name="bonePosition"/> answers where a bone is, in whatever space the caller wants the mesh in. <paramref name="height"/> is
        /// that rig's standing height in the same space, and scales every radius.
        /// </summary>
        public static List<Segment> Segments(
            System.Func<HumanBodyBones, Vector3> bonePosition, float height)
        {
            float scale = height / ReferenceHeight;

            Vector3 hips = bonePosition(HumanBodyBones.Hips);
            Vector3 neck = bonePosition(HumanBodyBones.Neck);
            Vector3 torsoTop = neck - Vector3.up * (TorsoTopBelowShoulders * scale);
            Vector3 waist = Vector3.Lerp(hips, torsoTop, BodyRig.SpineT);

            var segments = new List<Segment>
            {
                // The torso: fused_body.gd's one thick capsule from the hip line toward the shoulder line, split at SPINE_T so the spine bone drives
                // the chest (one capsule owned entirely by the hips left the whole back rigid to the pelvis, see the Godot port's history).
                new Segment(hips, waist, TorsoRadius * scale, HumanBodyBones.Hips),
                new Segment(waist, torsoTop, TorsoRadius * scale, HumanBodyBones.Spine),
                Pelvis(bonePosition, scale),
                ShoulderBar(bonePosition, scale, ShoulderBarEndsInsideForWeights),
                NeckColumn(neck, scale),
            };

            AddArm(segments, bonePosition, scale, true, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            AddArm(segments, bonePosition, scale, true, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            AddLeg(segments, bonePosition, scale, true, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            AddLeg(segments, bonePosition, scale, true, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);

            // AND NO HEAD. fused_body.gd's own limb_defs() has never had one -- character.gd builds that separately, as its own sphere hung off the
            // head bone (see HeadMesh). Fused in here it swallowed the neck whole.
            return segments;
        }

        /// <summary>
        /// THE SHAPE: the pieces the surface is extracted and shaded from. One unbroken round cone per arm (shoulder to wrist) and per leg (hip to
        /// ankle), one capsule for the torso, the pelvis bar, the neck column, and each hand's and foot's own pieces -- no two colinear pieces
        /// sharing an endpoint, so no seam bulge (see <see cref="Segments"/>). fused_body.gd's own sdf_body() has the same rule: smin() only ever
        /// runs BETWEEN limbs, never within one.
        /// </summary>
        public static List<Segment> LimbCapsules(
            System.Func<HumanBodyBones, Vector3> bonePosition, float height)
        {
            float scale = height / ReferenceHeight;

            Vector3 hips = bonePosition(HumanBodyBones.Hips);
            Vector3 neck = bonePosition(HumanBodyBones.Neck);
            Vector3 torsoTop = neck - Vector3.up * (TorsoTopBelowShoulders * scale);

            var limbs = new List<Segment>
            {
                new Segment(hips, torsoTop, TorsoRadius * scale, HumanBodyBones.Hips),
                Pelvis(bonePosition, scale),
                ShoulderBar(bonePosition, scale, ShoulderBarEndsInside),
                NeckColumn(neck, scale),
            };

            AddArm(limbs, bonePosition, scale, false, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            AddArm(limbs, bonePosition, scale, false, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            AddLeg(limbs, bonePosition, scale, false, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            AddLeg(limbs, bonePosition, scale, false, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            return limbs;
        }

        /// <summary>The pieces of one hand alone (the palm and its fingers, nothing of the arm): what the fine hand grid is sized round. Empty if that bone has none.</summary>
        public static List<Segment> HandPieces(IReadOnlyList<Segment> shape, HumanBodyBones hand)
        {
            var pieces = new List<Segment>();
            foreach (Segment s in shape)
            {
                if (BelongsToHand(s.Bone, hand))
                {
                    pieces.Add(s);
                }
            }
            return pieces;
        }

        private static Segment Pelvis(System.Func<HumanBodyBones, Vector3> bonePosition, float scale)
        {
            Vector3 left = bonePosition(HumanBodyBones.LeftUpperLeg), right = bonePosition(HumanBodyBones.RightUpperLeg);
            Vector3 inward = (right - left).normalized * (PelvisEndsInsideHips * scale);
            return new Segment(left + inward, right - inward, PelvisRadius * scale, HumanBodyBones.Hips);
        }

        /// <summary>The level bar from one arm's root to the other's, ending <paramref name="endsInside"/> short of each.</summary>
        private static Segment ShoulderBar(System.Func<HumanBodyBones, Vector3> bonePosition, float scale, float endsInside)
        {
            Vector3 left = ArmRoot(bonePosition, HumanBodyBones.LeftUpperArm, scale);
            Vector3 right = ArmRoot(bonePosition, HumanBodyBones.RightUpperArm, scale);
            Vector3 inward = (right - left).normalized * (endsInside * scale);
            return new Segment(left + inward, right - inward, ArmRadius * scale, HumanBodyBones.Spine);
        }

        /// <summary>Where an arm's tube begins: <see cref="ArmRootBelowShoulder"/> under its shoulder joint.</summary>
        private static Vector3 ArmRoot(System.Func<HumanBodyBones, Vector3> bonePosition, HumanBodyBones shoulder, float scale)
        {
            return bonePosition(shoulder) - Vector3.up * (ArmRootBelowShoulder * scale);
        }

        private static Segment NeckColumn(Vector3 neck, float scale)
        {
            return new Segment(neck - Vector3.up * (NeckColumnBelowShoulders * scale), neck + Vector3.up * (NeckColumnAboveShoulders * scale),
                NeckRadius * scale, NeckRadius * scale, HumanBodyBones.Neck, NeckBlend * scale);
        }

        /// <summary>
        /// An arm: a round cone from its root (just under the shoulder joint, see <see cref="ArmRootBelowShoulder"/>) to the wrist (one piece, or
        /// split at the elbow for the weights), and the hand's pieces beyond it. The "hand" bone position is fused_body.gd's HAND target, the
        /// end of the old arm capsule; the wrist joint is WristT of the way there from the joint.
        /// </summary>
        private static void AddArm(List<Segment> into, System.Func<HumanBodyBones, Vector3> bonePosition, float scale, bool perBone,
            HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
        {
            Vector3 shoulder = bonePosition(upper);
            Vector3 root = ArmRoot(bonePosition, upper, scale);
            Vector3 handTarget = bonePosition(hand);
            Vector3 wrist = Vector3.Lerp(shoulder, handTarget, BodyRig.WristT);
            float rShoulder = ArmRadius * scale, rWrist = WristRadius * scale;
            if (perBone)
            {
                Vector3 elbow = bonePosition(lower);
                float elbowT = Vector3.Distance(root, elbow) / Mathf.Max(1e-6f, Vector3.Distance(root, wrist));
                float rElbow = Mathf.Lerp(rShoulder, rWrist, elbowT);
                into.Add(new Segment(root, elbow, rShoulder, rElbow, upper, BlendRadius * scale));
                into.Add(new Segment(elbow, wrist, rElbow, rWrist, lower, BlendRadius * scale));
            }
            else
            {
                into.Add(new Segment(root, wrist, rShoulder, rWrist, upper, BlendRadius * scale));
            }
            AddHand(into, shoulder, handTarget, wrist, scale, hand);
        }

        /// <summary>
        /// A hand at the end of an arm: a rounded-box palm, four fingers and a thumb, each finger two capsules bent a little toward the palm. The
        /// palm faces the body (the thumb forward), as a hanging hand's does; the hand bone owns all of it, so it moves as one piece.
        /// </summary>
        private static void AddHand(List<Segment> into, Vector3 shoulder, Vector3 handTarget, Vector3 wrist, float scale, HumanBodyBones hand)
        {
            Vector3 along = (handTarget - shoulder).normalized;              // down the arm, toward the fingertips
            Vector3 forward = Vector3.forward;                                // the thumb's side
            Vector3 towardBody = shoulder.x < 0.0f ? Vector3.right : Vector3.left;
            Vector3 palmNormal = (towardBody - along * Vector3.Dot(towardBody, along)).normalized;   // the way the fingers curl
            Quaternion frame = Quaternion.LookRotation(forward, along);

            Vector3 palmCentre = wrist + along * (PalmLength * 0.5f * scale);
            into.Add(Segment.Box(palmCentre, frame,
                new Vector3(PalmHalfThickness, PalmLength * 0.5f, PalmHalfWidth) * scale, PalmRounding * scale, hand, PalmBlend * scale));

            // The digits from the shared table (Digits), each piece on its OWN bone so the fingers can fold (HandGrip): the hand's frame is x
            // along the arm, y the back of the hand (away from the palm), z the thumb's side.
            Vector3 outward = -palmNormal;
            bool right = hand == HumanBodyBones.RightHand;
            foreach (Digit digit in Digits)
            {
                Vector3 root = wrist + along * (digit.Root.x * scale) + outward * (digit.Root.y * scale) + forward * (digit.Root.z * scale);
                Vector3 knuckle = wrist + along * (digit.Knuckle.x * scale) + outward * (digit.Knuckle.y * scale) + forward * (digit.Knuckle.z * scale);
                Vector3 tip = wrist + along * (digit.Tip.x * scale) + outward * (digit.Tip.y * scale) + forward * (digit.Tip.z * scale);
                float r = digit.Radius * scale;
                into.Add(new Segment(root, knuckle, r, r, right ? digit.RightProximal : digit.LeftProximal, FingerBlend * scale));
                into.Add(new Segment(knuckle, tip, r, r, right ? digit.RightIntermediate : digit.LeftIntermediate, FingerBlend * scale));
            }
        }

        /// <summary>A leg: a round cone from the hip to the ankle (one piece, or split at the knee for the weights), and the foot under it.</summary>
        private static void AddLeg(List<Segment> into, System.Func<HumanBodyBones, Vector3> bonePosition, float scale, bool perBone,
            HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot)
        {
            Vector3 hip = bonePosition(upper);
            Vector3 ankle = bonePosition(foot);
            float rHip = LegRadius * scale, rAnkle = AnkleRadius * scale;
            if (perBone)
            {
                Vector3 knee = bonePosition(lower);
                float kneeT = Vector3.Distance(hip, knee) / Mathf.Max(1e-6f, Vector3.Distance(hip, ankle));
                float rKnee = Mathf.Lerp(rHip, rAnkle, kneeT);
                into.Add(new Segment(hip, knee, rHip, rKnee, upper, BlendRadius * scale));
                into.Add(new Segment(knee, ankle, rKnee, rAnkle, lower, BlendRadius * scale));
            }
            else
            {
                into.Add(new Segment(hip, ankle, rHip, rAnkle, upper, BlendRadius * scale));
            }
            AddFoot(into, ankle, scale, foot);
        }

        /// <summary>
        /// A foot under the ankle: a flat-soled, round-toed stadium (<see cref="Segment.Foot"/>) with its sole on the ground (the ankle is
        /// <see cref="BodyRig.FootY"/> up), reaching forward from a short heel.
        /// </summary>
        private static void AddFoot(List<Segment> into, Vector3 ankle, float scale, HumanBodyBones foot)
        {
            Vector3 sole = new Vector3(ankle.x, ankle.y - BodyRig.FootY * scale, ankle.z);
            Vector3 heel = sole + Vector3.forward * (FootHeelCentreForward * scale);
            Vector3 toe = sole + Vector3.forward * (FootToeCentreForward * scale);
            into.Add(Segment.Foot(heel, toe, FootHeelRadius * scale, FootToeRadius * scale, FootHeight * scale, FootTopRounding * scale,
                Quaternion.identity, foot, FootBlend * scale));
        }

        // --- fused_body.gd's own stub-body constants (is_stub_point/stub_mesh) ---
        //
        // Which triangles survive when a full-length garment like the hooded
        // cloak swaps the whole body for "hands, feet and a collar only" so
        // there is no independently-moving limb left to poke through rigid
        // cloth.

        /// <summary>Fraction along the arm's cone (shoulder=0, wrist=1) where the visible forearm stub begins: fused_body.gd's 0.62 of the shoulder-to-fingertip arm.</summary>
        public const float StubHandFraction = 0.73f;

        /// <summary>Fraction along the leg (hip=0, ankle=1) where the visible foot stub begins.</summary>
        public const float StubFootFraction = 0.80f;

        /// <summary>How far down the chest the collar slice reaches, in metres at ReferenceHeight.</summary>
        public const float StubCollarY = 1.72f;

        /// <summary>How wide a slice of chest the collar keeps, in metres at ReferenceHeight.</summary>
        public const float StubCollarRadius = 0.32f;

        /// <summary>
        /// Whether a point (in the same rest-pose local space the body mesh
        /// itself is built in) survives into the stub body. Ported from
        /// fused_body.gd's own is_stub_point(): a point can sit near more
        /// than one limb (the shoulder belongs to both torso and arm), so
        /// this is a union -- any limb whose rule says "keep" keeps it. The
        /// hands and feet are kept whole.
        ///
        /// <paramref name="limbCapsules"/> must be the SAME list (and the
        /// same height/scale) the mesh being filtered was built from.
        /// </summary>
        public static bool IsStubPoint(Vector3 p, List<Segment> limbCapsules, float height)
        {
            float scale = height / ReferenceHeight;
            float collarY = StubCollarY * scale;
            float collarR = StubCollarRadius * scale;

            foreach (Segment limb in limbCapsules)
            {
                // BlendRadius's worth of slack because the smooth union rounds each joint outward past the raw piece -- without it the fused
                // surface right at a wrist reads as belonging to no limb at all and gets dropped.
                if (limb.Distance(p) > BlendRadius * scale)
                {
                    continue;
                }
                Vector3 ab = limb.B - limb.A;
                float denominator = Vector3.Dot(ab, ab);
                float t = denominator > 1e-9f
                    ? Mathf.Clamp01(Vector3.Dot(p - limb.A, ab) / denominator)
                    : 0.0f;

                switch (limb.Bone)
                {
                    case HumanBodyBones.Hips:
                    case HumanBodyBones.Neck:
                        if (p.y >= collarY && new Vector2(p.x, p.z).magnitude <= collarR)
                        {
                            return true;
                        }
                        break;
                    case HumanBodyBones.LeftUpperArm:
                    case HumanBodyBones.RightUpperArm:
                        if (t >= StubHandFraction)
                        {
                            return true;
                        }
                        break;
                    case HumanBodyBones.LeftHand:
                    case HumanBodyBones.RightHand:
                    case HumanBodyBones.LeftFoot:
                    case HumanBodyBones.RightFoot:
                        return true;
                    default:
                        if (t >= StubFootFraction)
                        {
                            return true;
                        }
                        break;
                }
            }
            return false;
        }

        /// <summary>
        /// The body's surface: a smooth union of every piece, each rounded in over its own blend band. Negative inside, positive outside, zero on
        /// the skin.
        /// </summary>
        public static float Distance(IReadOnlyList<Segment> segments, Vector3 p)
        {
            if (segments.Count == 0)
            {
                return float.MaxValue;
            }

            float d = segments[0].Distance(p);
            for (int i = 1; i < segments.Count; i++)
            {
                d = SmoothMin(d, segments[i].Distance(p), segments[i].Blend);
            }
            return d;
        }

        /// <summary>
        /// fused_body.gd's smin: a minimum that rounds off the corner where two
        /// surfaces meet, over a band of width <paramref name="k"/>.
        /// </summary>
        public static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1.0f - h);
        }

        /// <summary>
        /// Which bone a point on the skin belongs to: the nearest piece's.
        /// </summary>
        public static HumanBodyBones NearestBone(IReadOnlyList<Segment> segments, Vector3 p)
        {
            HumanBodyBones best = HumanBodyBones.Hips;
            float bestDistance = float.MaxValue;

            foreach (Segment segment in segments)
            {
                float d = segment.Distance(p);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = segment.Bone;
                }
            }
            return best;
        }

        /// <summary>How much wider than Godot's k the handover is across an elbow or a knee.</summary>
        public const float LimbHandoverWidening = 2.5f;

        /// <summary>
        /// The handover where a limb's root ball meets the torso (a shoulder, a hip), and at a wrist or an ankle: narrower than the shape's own
        /// blend. A root ball turns about its joint, so the ball's skin can be wholly the limb's and still never open a gap; only the fillet
        /// where it meets the torso needs sharing. With the band as wide as the fillet, a quarter of the ball's top was the torso's and a
        /// raised arm pulled it into a cone.
        /// </summary>
        public const float RootHandover = 0.02f;

        /// <summary>
        /// A knuckle hands over this narrowly: a finger's pieces are only 4 cm long, so the ordinary band would smear a fold across the whole finger
        /// (every fingertip was 13% its neighbour's). A crease at each knuckle is right for a cartoon hand closing round a grip.
        /// </summary>
        public const float FingerHandover = 0.01f;

        /// <summary>
        /// Whether two bones meet at an elbow or a knee: the one handover that is widened. A wrist or an ankle is NOT: a hand with fingers and a
        /// foot are stiff pieces that turn as a whole at a narrow joint, so they take Godot's own narrow band (<see cref="BlendRadius"/>).
        /// </summary>
        private static bool AcrossAnElbowOrKnee(HumanBodyBones a, HumanBodyBones b)
        {
            return (a == HumanBodyBones.LeftUpperArm && b == HumanBodyBones.LeftLowerArm) || (b == HumanBodyBones.LeftUpperArm && a == HumanBodyBones.LeftLowerArm)
                || (a == HumanBodyBones.RightUpperArm && b == HumanBodyBones.RightLowerArm) || (b == HumanBodyBones.RightUpperArm && a == HumanBodyBones.RightLowerArm)
                || (a == HumanBodyBones.LeftUpperLeg && b == HumanBodyBones.LeftLowerLeg) || (b == HumanBodyBones.LeftUpperLeg && a == HumanBodyBones.LeftLowerLeg)
                || (a == HumanBodyBones.RightUpperLeg && b == HumanBodyBones.RightLowerLeg) || (b == HumanBodyBones.RightUpperLeg && a == HumanBodyBones.RightLowerLeg);
        }

        /// <summary>
        /// The bones a vertex is attached to, and how much each one owns of it: fused_body.gd's own rule. Every piece contributes
        /// exp(-(distance - nearestDistance) / k), the contributions are summed per BONE, and the four heaviest bones are normalised.
        ///
        /// NARROW AT THE TORSO, WIDE AT AN ELBOW OR A KNEE, NARROW AGAIN AT A WRIST OR ANKLE. Godot's k (the width of the smooth union itself)
        /// keeps an armpit and a hip crisp -- a wider handover there is what made the torso drag and wrinkle when an arm swung. Across a knee or
        /// elbow bent a hundred degrees that narrow band leaves each bone dragging its own half of the joint and the outside of the bend swelling
        /// (measured on a wall climb: a shin's skin twice as far from its bone as at rest), so those two joints hand over
        /// <see cref="LimbHandoverWidening"/> times as widely. A hand or a foot is a stiff piece on a narrow joint.
        ///
        /// Fills <paramref name="bones"/> and <paramref name="weights"/>, which must each hold four, and returns how many were used. A bone whose
        /// share is under half a percent of the nearest's is dropped (Godot keeps it at a weight of 1e-5 or so, which changes nothing).
        /// </summary>
        public static int WeightsAt(IReadOnlyList<Segment> segments, Vector3 p,
            HumanBodyBones[] bones, float[] weights)
        {
            float nearest = float.MaxValue;
            var distances = new float[segments.Count];
            HumanBodyBones nearestBone = HumanBodyBones.Hips;
            for (int i = 0; i < segments.Count; i++)
            {
                distances[i] = segments[i].Distance(p);
                if (distances[i] < nearest)
                {
                    nearest = distances[i];
                    nearestBone = segments[i].Bone;
                }
            }

            // Summed per bone: the pieces of one bone (a torso half and the pelvis bar, a palm and its fingers) add their claims. ONLY BONES
            // JOINED TO THE NEAREST ONE (AreConnected): a hanging hand passes within a few centimetres of the hip, and the few percent of hand
            // that gave the hip's skin pulled it into a spike whenever the arms were raised.
            var boneShare = new List<(HumanBodyBones bone, float share)>();
            for (int i = 0; i < segments.Count; i++)
            {
                if (!AreConnected(nearestBone, segments[i].Bone))
                {
                    continue;
                }
                float k = AcrossAnElbowOrKnee(nearestBone, segments[i].Bone) ? BlendRadius * LimbHandoverWidening
                    : (nearestBone == segments[i].Bone ? BlendRadius
                    : (BodyRig.IsFinger(nearestBone) || BodyRig.IsFinger(segments[i].Bone)) ? FingerHandover : RootHandover);
                float share = Mathf.Exp(-(distances[i] - nearest) / k);
                int found = boneShare.FindIndex(entry => entry.bone == segments[i].Bone);
                if (found >= 0)
                {
                    boneShare[found] = (boneShare[found].bone, boneShare[found].share + share);
                }
                else
                {
                    boneShare.Add((segments[i].Bone, share));
                }
            }
            boneShare.Sort((a, b) => b.share.CompareTo(a.share));

            float heaviest = boneShare[0].share;
            int used = 0;
            float total = 0.0f;
            for (int i = 0; i < boneShare.Count && used < bones.Length; i++)
            {
                if (boneShare[i].share < heaviest * 0.005f)
                {
                    break;
                }
                bones[used] = boneShare[i].bone;
                weights[used] = boneShare[i].share;
                total += boneShare[i].share;
                used++;
            }
            for (int i = 0; i < used; i++)
            {
                weights[i] /= total;
            }
            return used;
        }

        /// <summary>
        /// Which bone owns a point, by fused_body.gd's own rule: sum
        /// exp(-(d - dmin) / BlendRadius) over every piece, grouped by bone,
        /// and take the heaviest.
        ///
        /// NOT SIMPLY THE NEAREST PIECE, and the difference is the point.
        /// Two pieces of the same bone -- a torso capsule and the pelvis bar,
        /// a palm and a finger -- ADD their claims here, so a point that is
        /// marginally nearest to a single thin piece can still be owned by
        /// the bone that surrounds it on both sides. Used to pick the atlas
        /// row, where being marginally wrong puts paint on a different limb.
        /// </summary>
        public static HumanBodyBones DominantBone(IReadOnlyList<Segment> segments, Vector3 p)
        {
            float nearest = float.MaxValue;
            foreach (Segment segment in segments)
            {
                nearest = Mathf.Min(nearest, segment.Distance(p));
            }

            HumanBodyBones best = HumanBodyBones.Hips;
            float bestWeight = -1.0f;

            foreach (Segment candidate in segments)
            {
                float weight = 0.0f;
                foreach (Segment segment in segments)
                {
                    if (segment.Bone != candidate.Bone)
                    {
                        continue;
                    }
                    weight += Mathf.Exp(-(segment.Distance(p) - nearest) / BlendRadius);
                }

                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = candidate.Bone;
                }
            }
            return best;
        }

        /// <summary>
        /// Whether two bones are close enough together in the skeleton to share
        /// skin.
        ///
        /// TWO GENERATIONS, not one. A hand and an upper arm are two apart and
        /// their pieces do overlap at a wrist; so are a head and the torso,
        /// with only the short neck between them. One generation would cut both
        /// of those and put a hard seam at the base of the skull.
        ///
        /// What it excludes is anything reached by going UP and then back down
        /// -- a head and a shoulder, or the two shoulders. Those are near each
        /// other without being attached to each other, and sharing weight
        /// between them is what makes a head turn drag the shoulders round.
        /// </summary>
        public static bool AreConnected(HumanBodyBones a, HumanBodyBones b)
        {
            return a == b || IsAncestor(a, b, 2) || IsAncestor(b, a, 2);
        }

        private static bool IsAncestor(HumanBodyBones ancestor, HumanBodyBones of, int generations)
        {
            HumanBodyBones walk = of;
            for (int i = 0; i < generations; i++)
            {
                walk = SkinParent(walk);
                if (walk == HumanBodyBones.LastBone)
                {
                    return false;
                }
                if (walk == ancestor)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The bone whose geometry a bone's geometry hangs off.
        ///
        /// NOT the skeleton's own parent chain, which is why it is written out
        /// here. The torso capsule belongs to the hips, so an arm's skin
        /// neighbour upward is the hips even though the arm bone is parented to
        /// the spine. LastBone means "no parent".
        /// </summary>
        private static HumanBodyBones SkinParent(HumanBodyBones bone)
        {
            if (BodyRig.IsFinger(bone))
            {
                return BodyRig.FingerParent(bone);   // a finger's root hangs off the hand, its tip off the root
            }
            switch (bone)
            {
                case HumanBodyBones.Spine: return HumanBodyBones.Hips;

                case HumanBodyBones.Neck: return HumanBodyBones.Spine;
                case HumanBodyBones.Head: return HumanBodyBones.Neck;

                case HumanBodyBones.LeftUpperArm: return HumanBodyBones.Spine;
                case HumanBodyBones.LeftLowerArm: return HumanBodyBones.LeftUpperArm;
                case HumanBodyBones.LeftHand: return HumanBodyBones.LeftLowerArm;

                case HumanBodyBones.RightUpperArm: return HumanBodyBones.Spine;
                case HumanBodyBones.RightLowerArm: return HumanBodyBones.RightUpperArm;
                case HumanBodyBones.RightHand: return HumanBodyBones.RightLowerArm;

                case HumanBodyBones.LeftUpperLeg: return HumanBodyBones.Hips;
                case HumanBodyBones.LeftLowerLeg: return HumanBodyBones.LeftUpperLeg;
                case HumanBodyBones.LeftFoot: return HumanBodyBones.LeftLowerLeg;

                case HumanBodyBones.RightUpperLeg: return HumanBodyBones.Hips;
                case HumanBodyBones.RightLowerLeg: return HumanBodyBones.RightUpperLeg;
                case HumanBodyBones.RightFoot: return HumanBodyBones.RightLowerLeg;

                default: return HumanBodyBones.LastBone;
            }
        }

        /// <summary>
        /// A box that certainly contains the whole of these pieces, with room for the blend to bulge outward past them.
        /// </summary>
        public static Bounds BoundsOf(IReadOnlyList<Segment> segments)
        {
            var bounds = new Bounds(segments[0].A, Vector3.zero);
            foreach (Segment segment in segments)
            {
                bounds.Encapsulate(new Bounds(segment.A, Vector3.one * segment.Extent * 2.0f));
                bounds.Encapsulate(new Bounds(segment.B, Vector3.one * segment.Extent * 2.0f));
            }
            bounds.Expand(BlendRadius * 4.0f);
            return bounds;
        }

        /// <summary>
        /// fused_body.gd's own hand-authored grid origin (BOUNDS_MIN). Not a bounding limit -- Unity's bounds must stay dynamically wider than
        /// this to fit whatever is passed in -- only a PHASE REFERENCE for PhaseAlignedOrigin below: two grids sampling the identical field at
        /// different phases place their vertices at different points along the surface, which showed as a different look at the neck and shoulders.
        /// </summary>
        private static readonly Vector3 GodotGridPhaseReference = new Vector3(-0.75f, -0.05f, -0.35f);

        /// <summary>
        /// Snaps <paramref name="boundsMin"/> DOWN (never up -- flooring only extends the sampled region, it can never clip it) to the nearest point
        /// that shares Godot's own grid phase on every axis.
        /// </summary>
        public static Vector3 PhaseAlignedOrigin(Vector3 boundsMin, float cellSize)
        {
            return new Vector3(
                SnapDownToPhase(boundsMin.x, GodotGridPhaseReference.x, cellSize),
                SnapDownToPhase(boundsMin.y, GodotGridPhaseReference.y, cellSize),
                SnapDownToPhase(boundsMin.z, GodotGridPhaseReference.z, cellSize));
        }

        private static float SnapDownToPhase(float value, float phase, float cellSize)
        {
            float steps = Mathf.Floor((value - phase) / cellSize);
            return phase + steps * cellSize;
        }
    }
}
