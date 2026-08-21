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
        ///
        /// The head is NOT one of the capsules below -- see Segments. This
        /// lives here because it is the body's own scale that decides it.
        /// </summary>
        public const float HeadRadius = 0.35f;

        /// <summary>
        /// fused_body.gd NECK_LEN. Very nearly zero on purpose: the neck is a
        /// rounded bump where the head meets the shoulders, not a column.
        /// </summary>
        public const float NeckLength = 0.001f;

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
            Vector3 neck = bonePosition(HumanBodyBones.Neck);

            var segments = new List<Segment>
            {
                // HIPS TO NECK AS ONE THICK CAPSULE, which is fused_body.gd's
                // own torso: a single capsule from the hip line to the shoulder
                // line. Not one per spine bone, which would bulge at every
                // joint -- and not hips-to-chest, which is what this used while
                // it was fitting itself to an imported rig that has a chest.
                // THE TORSO IS TWO BONES, NOT ONE. fused_body.gd splits it at
                // SPINE_T -- hips for the lower half, spine for the upper --
                // and this port collapsed it into a single capsule owned
                // entirely by the hips. The Spine bone then drove no geometry
                // at all, so the whole back was rigid to the pelvis while the
                // head and arms still hung off a spine chain the mesh ignored.
                // Mixamo puts most of its upper-body motion in the spine, so
                // every clip read as the back lurching in one block with the
                // head disconnected from it.
                //
                // Two colinear capsules of the same radius sharing an endpoint
                // union to exactly the shape of one, so this changes where the
                // weights fall and nothing whatever about the silhouette.
                new Segment(hips, Vector3.Lerp(hips, neck, BodyRig.SpineT),
                    TorsoRadius * scale, HumanBodyBones.Hips),
                new Segment(Vector3.Lerp(hips, neck, BodyRig.SpineT), neck,
                    TorsoRadius * scale, HumanBodyBones.Spine),

                // A BUMP AT THE SHOULDER LINE, not a column up to the skull.
                // fused_body.gd's neck limb is NECK_LEN = 0.001 long: a 0.10
                // sphere sitting on the torso, and nothing more.
                new Segment(neck, neck + Vector3.up * (NeckLength * scale),
                    NeckRadius * scale, HumanBodyBones.Neck),
            };

            // AND NO HEAD. fused_body.gd's own limb_defs() has a torso, two
            // arms, two legs and a neck in it and has never had a head --
            // character.gd builds that separately, as its own sphere hung off
            // the head bone (see HeadMesh). Adding it here fused it into the
            // neck through the same smooth blend as every joint, and since the
            // head sphere's radius reaches from the skull all the way back
            // down to the shoulder line, it swallowed the neck whole and gave
            // one continuous peanut with no neck in it at all.

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
        /// One capsule per LIMB -- torso (hips straight to neck), each arm
        /// (shoulder straight to hand), each leg (hip straight to foot), and
        /// the neck. Exactly what fused_body.gd's own limb_defs()/sdf_body()
        /// draws, for exactly the reason its own limb_defs() comment gives:
        /// "two colinear, same-radius, endpoint-sharing capsules union to an
        /// identical SDF as one capsule spanning both."
        ///
        /// THIS IS THE SHAPE. Segments() above exists for skin weights, and
        /// splits a limb into per-BONE pieces (an elbow, a wrist, a knee, the
        /// spine) -- correct for deciding which bone owns a vertex, wrong for
        /// deciding where the SURFACE sits. Unioning those per-bone pieces
        /// back together with a second round of SmoothMin, as Distance(...)
        /// does for whatever list it is handed, does not reproduce "one
        /// capsule spanning both": SmoothMin(d, d, k) is NOT d, it equals
        /// d - k * 0.25, so every internal seam between two colinear
        /// same-radius capsules pushes the surface outward by a quarter of
        /// the blend radius. fused_body.gd's sdf_capsule() is only ever
        /// called once per limb and smin() only ever runs BETWEEN limbs (an
        /// arm against the torso, a leg against the torso), never within
        /// one, so it structurally cannot hit this. Extract() and Normals()
        /// must be built from this list; Unwrap() and WeightsAt() must keep
        /// using Segments() -- see FusedBodyBuilder.Build's own comment.
        /// </summary>
        public static List<Segment> LimbCapsules(
            System.Func<HumanBodyBones, Vector3> bonePosition, float height)
        {
            float scale = height / ReferenceHeight;

            Vector3 hips = bonePosition(HumanBodyBones.Hips);
            Vector3 neck = bonePosition(HumanBodyBones.Neck);

            return new List<Segment>
            {
                new Segment(hips, neck, TorsoRadius * scale, HumanBodyBones.Hips),
                new Segment(neck, neck + Vector3.up * (NeckLength * scale),
                    NeckRadius * scale, HumanBodyBones.Neck),

                new Segment(bonePosition(HumanBodyBones.LeftUpperArm), bonePosition(HumanBodyBones.LeftHand),
                    ArmRadius * scale, HumanBodyBones.LeftUpperArm),
                new Segment(bonePosition(HumanBodyBones.RightUpperArm), bonePosition(HumanBodyBones.RightHand),
                    ArmRadius * scale, HumanBodyBones.RightUpperArm),

                new Segment(bonePosition(HumanBodyBones.LeftUpperLeg), bonePosition(HumanBodyBones.LeftFoot),
                    LegRadius * scale, HumanBodyBones.LeftUpperLeg),
                new Segment(bonePosition(HumanBodyBones.RightUpperLeg), bonePosition(HumanBodyBones.RightFoot),
                    LegRadius * scale, HumanBodyBones.RightUpperLeg),
            };
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
        /// How wide the handover between two bones is, as a multiple of the
        /// THICKER of the two limbs meeting there.
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
        public const float WeightFalloffRadii = 1.0f;

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
            HumanBodyBones nearestBone = HumanBodyBones.Hips;
            foreach (Segment segment in segments)
            {
                float d = segment.Distance(p);
                if (d < nearest)
                {
                    nearest = d;
                    nearestRadius = segment.Radius;
                    nearestBone = segment.Bone;
                }
            }

            int used = 0;
            foreach (Segment segment in segments)
            {
                // ONLY BETWEEN BONES THAT ARE ACTUALLY CONNECTED. Without this
                // the head sphere, which is big and sits directly above the
                // shoulder line, takes a third of the top of each shoulder --
                // so turning the head drags the shoulders with it. Distance
                // alone cannot tell "these two pieces of skin meet at a joint"
                // apart from "these two pieces of skin happen to be near each
                // other".
                if (!AreConnected(nearestBone, segment.Bone))
                {
                    continue;
                }

                // THE HANDOVER IS AS WIDE AS THE THICKER OF THE TWO LIMBS
                // MEETING HERE, and using only the nearer one's thickness was
                // wrong in the exact place it mattered most. At a CITY X
                // shoulder the arm is 0.09 thick and the torso 0.26, and the
                // joint sits proud of the torso, so the arm always wins
                // "nearest" -- which handed the whole shoulder over across
                // 34mm. The blob the smooth union fills there is several times
                // that across, so most of it was rigidly weighted and creased
                // when the arm swung. Against the torso's own thickness the
                // handover is about 200mm, which is the size of the thing
                // actually being blended.
                //
                // Taken per PAIR rather than once for the whole point, so it
                // stays continuous: where two capsules are equidistant the
                // falloff cancels out of the share entirely.
                float falloff = Mathf.Max(
                    Mathf.Max(nearestRadius, segment.Radius) * WeightFalloffRadii, 1e-4f);

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
        /// Which bone owns a point, by fused_body.gd's own rule: sum
        /// exp(-(d - dmin) / BlendRadius) over every segment, grouped by bone,
        /// and take the heaviest.
        ///
        /// NOT SIMPLY THE NEAREST SEGMENT, and the difference is the point.
        /// Two segments of the same bone -- a torso capsule and the neck stub,
        /// a thigh and its shin -- ADD their claims here, so a point that is
        /// marginally nearest to a single thin segment can still be owned by
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
        /// their capsules do overlap at a wrist; so are a head and the torso,
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
            switch (bone)
            {
                // THE UPPER TORSO IS THE SPINE'S NOW, so everything that meets
                // the body at the shoulder line hangs off the spine rather
                // than the hips. Splitting the torso without moving these left
                // the shoulder owned by a single bone -- the arm and the
                // geometry it grows out of were no longer neighbours, so no
                // weight was shared across the join and it tore the moment an
                // arm swung. The legs stay on the hips: they meet the LOWER
                // half, which is still the hips' own capsule.
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
