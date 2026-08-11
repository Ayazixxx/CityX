using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The shape of a CITY X character, as a signed distance field. Ported from
    /// character/fused_body.gd.
    ///
    /// This is what the game's people actually look like: smooth-blended
    /// capsules for the torso and limbs, and a sphere for a head. Not a
    /// humanoid mesh -- the Mixamo mannequin currently standing in is a
    /// placeholder that happens to come with the animations.
    ///
    /// AN SDF RATHER THAN ASSEMBLED PRIMITIVES. Six separate capsules would
    /// meet at the shoulders and hips in hard intersecting seams. A smooth
    /// minimum fuses them into one continuous surface, which is the entire
    /// reason the Godot original is called a FUSED body, and it is why the
    /// shape has to be extracted rather than built.
    ///
    /// PROPORTIONS COME FROM THE SKELETON, THICKNESSES COME FROM HERE. The
    /// limb lengths are read off whatever humanoid rig is passed in, so the
    /// body has real human proportions and the Mixamo clips fit it; the radii,
    /// the blend and the head are the Godot build's own numbers. That is the
    /// split asked for: human arms and legs, head sphere unchanged.
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
        public const float NeckRadius = 0.10f;

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
        /// fused_body.gd CELL_SIZE. The extraction grid. Halving it quadruples
        /// the build time and does not visibly improve a shape made of
        /// capsules; doubling it starts faceting the head.
        /// </summary>
        public const float CellSize = 0.045f;

        /// <summary>One capsule of the body, and which bone owns it.</summary>
        public readonly struct Segment
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly float Radius;
            public readonly HumanBodyBones Bone;

            public Segment(Vector3 a, Vector3 b, float radius, HumanBodyBones bone)
            {
                A = a;
                B = b;
                Radius = radius;
                Bone = bone;
            }

            /// <summary>Distance from a point to this capsule's surface.</summary>
            public float Distance(Vector3 p)
            {
                Vector3 pa = p - A;
                Vector3 ba = B - A;
                float denominator = Vector3.Dot(ba, ba);
                // A zero-length capsule is a sphere, not a division by zero.
                float h = denominator <= 1e-12f
                    ? 0.0f
                    : Mathf.Clamp01(Vector3.Dot(pa, ba) / denominator);
                return (pa - ba * h).magnitude - Radius;
            }
        }

        /// <summary>
        /// Builds the body's capsules from a humanoid rig's bind pose.
        ///
        /// <paramref name="bonePosition"/> answers where a bone is, in whatever
        /// space the caller wants the mesh in. <paramref name="height"/> is that
        /// rig's standing height in the same space, and scales every radius.
        /// </summary>
        public static List<Segment> Segments(
            System.Func<HumanBodyBones, Vector3> bonePosition, float height)
        {
            float scale = height / ReferenceHeight;

            Vector3 hips = bonePosition(HumanBodyBones.Hips);
            Vector3 chest = bonePosition(HumanBodyBones.Chest);
            Vector3 head = bonePosition(HumanBodyBones.Head);

            var segments = new List<Segment>
            {
                // The torso runs hips to chest as one thick capsule, the way
                // the original does -- not one per spine bone, which would
                // bulge at every joint.
                new Segment(hips, chest, TorsoRadius * scale, HumanBodyBones.Hips),
                new Segment(chest, head, NeckRadius * scale, HumanBodyBones.Neck),

                // The head sphere is a zero-length capsule, which is exactly a
                // sphere, so it fuses into the neck through the same blend as
                // everything else rather than being a separate object stuck on
                // top with a visible join.
                new Segment(head, head, HeadRadius * scale, HumanBodyBones.Head),
            };

            // Arms carry a hand: fused_body.gd splits each arm three ways at
            // WRIST_T, which is 0.7 of the way from elbow to fingertip.
            AddLimb(segments, bonePosition, ArmRadius * scale, HandFraction,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            AddLimb(segments, bonePosition, ArmRadius * scale, HandFraction,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);

            // Legs do NOT. The original has no foot bone at all -- the shin
            // capsule's own rounded end is the foot. Adding one would put a
            // sphere where the ankle is and make the character look club-footed.
            AddLimb(segments, bonePosition, LegRadius * scale, 1.0f,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            AddLimb(segments, bonePosition, LegRadius * scale, 1.0f,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);

            return segments;
        }

        /// <summary>
        /// fused_body.gd WRIST_T, re-expressed along the elbow-to-hand span
        /// rather than the whole arm: its 0.5/0.85/1.0 split becomes 0.7 of
        /// the forearm segment.
        /// </summary>
        public const float HandFraction = 0.7f;

        private static void AddLimb(List<Segment> segments,
            System.Func<HumanBodyBones, Vector3> bonePosition, float radius, float endFraction,
            HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end)
        {
            Vector3 a = bonePosition(upper);
            Vector3 b = bonePosition(lower);
            Vector3 c = bonePosition(end);

            // Capsules sharing endpoints, each owned by its own bone. Colinear
            // same-radius capsules that share an end union to exactly the shape
            // of one long capsule, so splitting here changes where the skin
            // weights fall and nothing at all about the silhouette.
            segments.Add(new Segment(a, b, radius, upper));

            if (endFraction >= 1.0f)
            {
                segments.Add(new Segment(b, c, radius, lower));
                return;
            }

            Vector3 wrist = Vector3.Lerp(b, c, endFraction);
            segments.Add(new Segment(b, wrist, radius, lower));
            segments.Add(new Segment(wrist, c, radius, end));
        }

        /// <summary>
        /// The body's surface: a smooth union of every capsule. Negative
        /// inside, positive outside, zero on the skin.
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
                d = SmoothMin(d, segments[i].Distance(p), BlendRadius);
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
        /// Which bone a point on the skin belongs to: the nearest capsule's.
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

        /// <summary>
        /// How wide the handover between bones is, as a multiple of the LOCAL
        /// limb's radius.
        ///
        /// RELATIVE TO THE LIMB, NOT A FIXED DISTANCE. A fixed width was tried
        /// first and has no good value: 0.24m is about right across a shoulder
        /// and catastrophic on a forearm, where it reaches past both the elbow
        /// and the wrist at once.
        ///
        /// UNDER ONE RADIUS, which took measuring to find. At 1.5 radii the
        /// middle of a forearm was still only 57% its own -- because a forearm
        /// is under three radii long, so a handover a radius and a half wide
        /// arrives from both ends at once and meets in the middle. The
        /// neighbouring capsules' rounded caps already overlap it heavily
        /// before any falloff is applied. Three quarters of a radius is wide
        /// enough that a joint is genuinely shared and narrow enough that the
        /// middle of a limb belongs to itself.
        /// </summary>
        public const float WeightFalloffRadii = 0.75f;

        /// <summary>
        /// The bones a vertex is attached to, and how much each one owns of it.
        ///
        /// BLENDED, NOT NEAREST-ONLY -- and this was got wrong first time. The
        /// original assigns each vertex wholly to its closest capsule, which is
        /// what fused_body.gd does, so that is what was ported. It tears. The
        /// torso capsule is thick and the arm capsule is thin, and the smooth
        /// union between them fills a large blob at the shoulder; giving every
        /// point of that blob entirely to whichever capsule happens to be a
        /// millimetre nearer means half a shoulder swings with the arm and the
        /// other half stays with the chest. The seam between them stretches
        /// like a sheet.
        ///
        /// Sharing a vertex between every capsule within <see cref="WeightFalloff"/>
        /// of the nearest one makes that handover gradual, which is what a
        /// shoulder actually does.
        ///
        /// Fills <paramref name="bones"/> and <paramref name="weights"/>, which
        /// must each hold four, and returns how many were used. Four because
        /// that is what a Unity BoneWeight carries -- past the fourth the
        /// smallest influences are dropped and the rest renormalised.
        /// </summary>
        public static int WeightsAt(IReadOnlyList<Segment> segments, Vector3 p,
            HumanBodyBones[] bones, float[] weights)
        {
            float nearest = float.MaxValue;
            float nearestRadius = 0.0f;
            foreach (Segment segment in segments)
            {
                float d = segment.Distance(p);
                if (d < nearest)
                {
                    nearest = d;
                    nearestRadius = segment.Radius;
                }
            }

            // The handover is as wide as the limb this point sits on is thick,
            // so a forearm hands over across a few centimetres and a torso
            // across a hand's breadth.
            float falloff = Mathf.Max(nearestRadius * WeightFalloffRadii, 1e-4f);

            int used = 0;
            foreach (Segment segment in segments)
            {
                // Linear falloff from the nearest capsule outward. A capsule
                // exactly at the nearest distance takes a full share; one a
                // whole falloff further away takes none.
                float share = 1.0f - (segment.Distance(p) - nearest) / falloff;
                if (share <= 0.0f)
                {
                    continue;
                }

                // Two capsules of the same limb share a bone at the elbow and
                // the knee; their shares add rather than competing for a slot.
                int existing = -1;
                for (int i = 0; i < used; i++)
                {
                    if (bones[i] == segment.Bone)
                    {
                        existing = i;
                        break;
                    }
                }
                if (existing >= 0)
                {
                    weights[existing] = Mathf.Max(weights[existing], share);
                    continue;
                }

                if (used < bones.Length)
                {
                    bones[used] = segment.Bone;
                    weights[used] = share;
                    used++;
                    continue;
                }

                // Full: displace the weakest, if this one beats it.
                int weakest = 0;
                for (int i = 1; i < used; i++)
                {
                    if (weights[i] < weights[weakest])
                    {
                        weakest = i;
                    }
                }
                if (share > weights[weakest])
                {
                    bones[weakest] = segment.Bone;
                    weights[weakest] = share;
                }
            }

            if (used == 0)
            {
                bones[0] = NearestBone(segments, p);
                weights[0] = 1.0f;
                return 1;
            }

            float total = 0.0f;
            for (int i = 0; i < used; i++)
            {
                total += weights[i];
            }
            for (int i = 0; i < used; i++)
            {
                weights[i] /= total;
            }
            return used;
        }

        /// <summary>
        /// A box that certainly contains the whole body, with room for the
        /// blend to bulge outward past the raw capsules.
        /// </summary>
        public static Bounds BoundsOf(IReadOnlyList<Segment> segments)
        {
            var bounds = new Bounds(segments[0].A, Vector3.zero);
            foreach (Segment segment in segments)
            {
                bounds.Encapsulate(new Bounds(segment.A, Vector3.one * segment.Radius * 2.0f));
                bounds.Encapsulate(new Bounds(segment.B, Vector3.one * segment.Radius * 2.0f));
            }
            bounds.Expand(BlendRadius * 4.0f);
            return bounds;
        }
    }
}
