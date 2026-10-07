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
    /// PROPORTIONS COME FROM THE SKELETON, THICKNESSES COME FROM HERE.
    ///
    /// STALE AS OF 2026-09-20, CORRECTED HERE: this used to mean "read limb
    /// lengths off whatever humanoid rig is passed in, so the body has real
    /// human/Mixamo proportions" -- that was BodyRig's own documented FIRST
    /// ATTEMPT, abandoned because it broke both the silhouette and the skin
    /// (see BodyRig.cs's own "WHY NOT JUST USE THE MIXAMO SKELETON"). The
    /// caller today (BodyRigBuilder.Segments/LimbCapsules) passes
    /// BodyRig.PositionOf, and BodyRig's own joint coordinates
    /// (HipY/ShoulderY/ShoulderX/HandX/HandY/...) ARE fused_body.gd's exact
    /// numbers -- so "the skeleton" this reads from is already
    /// Godot-proportioned, not human-proportioned. Confirmed directly after
    /// a Blender reference export ("give me the model") was built by baking
    /// a live SkinnedMeshRenderer bent through hand-tuned bone rotations
    /// instead of reusing this pipeline: it measured 21% short on arm reach
    /// and visibly pinched at the shoulder ("armpit gaps") purely from that
    /// approximation, not from any real proportions difference -- see
    /// PlayerBodyReferenceExporter.cs's own doc comment.
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

        // --- fused_body.gd's own stub-body constants (is_stub_point/stub_mesh) ---
        //
        // Which triangles survive when a full-length garment like the hooded
        // cloak swaps the whole body for "hands, feet and a collar only" so
        // there is no independently-moving limb left to poke through rigid
        // cloth. See fused_body.gd's own STUB_* doc comment for how each
        // number was arrived at -- these are its exact values, not re-tuned
        // for this port.

        /// <summary>Fraction along the arm (shoulder=0, hand=1) where the visible hand/forearm stub begins.</summary>
        public const float StubHandFraction = 0.62f;

        /// <summary>Fraction along the leg (hip=0, foot=1) where the visible foot stub begins.</summary>
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
        /// this is a union -- any limb whose rule says "keep" keeps it.
        ///
        /// <paramref name="limbCapsules"/> must be the SAME list (and the
        /// same height/scale) the mesh being filtered was built from --
        /// StubCollarY/StubCollarRadius are absolute metres at
        /// ReferenceHeight, scaled here exactly the way LimbCapsules scales
        /// its own radii, so a taller or shorter rig's collar slice still
        /// lines up with that rig's own shoulder line.
        /// </summary>
        public static bool IsStubPoint(Vector3 p, List<Segment> limbCapsules, float height)
        {
            float scale = height / ReferenceHeight;
            float collarY = StubCollarY * scale;
            float collarR = StubCollarRadius * scale;

            foreach (Segment limb in limbCapsules)
            {
                Vector3 ab = limb.B - limb.A;
                float denominator = Vector3.Dot(ab, ab);
                float t = denominator > 1e-9f
                    ? Mathf.Clamp01(Vector3.Dot(p - limb.A, ab) / denominator)
                    : 0.0f;
                // BlendRadius's worth of slack because the smooth union
                // rounds each joint outward past the raw capsule -- without
                // it the fused surface right at a wrist reads as belonging
                // to no limb at all and gets dropped.
                if (Vector3.Distance(p, limb.A + ab * t) > limb.Radius + BlendRadius)
                {
                    continue;
                }

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
        /// The bones a vertex is attached to, and how much each one owns of it: fused_body.gd's own rule, exactly. Every capsule contributes
        /// exp(-(distance - nearestDistance) / BlendRadius), the contributions are summed per BONE, and the four heaviest bones are normalised.
        ///
        /// THIS REPLACED A WIDER, SMOOTHER HANDOVER, and the replacement is the point. The first port gave every capsule within three quarters of the thicker limb's
        /// radius (about 0.2 m at a shoulder) a share, on the reasoning that Godot's nearest-capsule rule "tears". Godot's rule is not nearest-only (it is this softmin,
        /// the same k as the surface blend), and a 0.2 m handover is what made the torso drag and wrinkle around the armpit whenever an arm swung: half the chest
        /// followed the arm. With Godot's own narrow handover the armpit stays crisp, as in the Godot character, and the joint still deforms because the blend is
        /// the width of the smooth union itself.
        ///
        /// Fills <paramref name="bones"/> and <paramref name="weights"/>, which must each hold four, and returns how many were used. A bone whose share is under
        /// half a percent of the nearest's is dropped (Godot keeps it at a weight of 1e-5 or so, which changes nothing).
        /// </summary>
        public static int WeightsAt(IReadOnlyList<Segment> segments, Vector3 p,
            HumanBodyBones[] bones, float[] weights)
        {
            float nearest = float.MaxValue;
            var distances = new float[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                distances[i] = segments[i].Distance(p);
                nearest = Mathf.Min(nearest, distances[i]);
            }

            // Summed per bone: two capsules of one bone (a torso half and the neck, a thigh) add their claims.
            var boneShare = new List<(HumanBodyBones bone, float share)>();
            for (int i = 0; i < segments.Count; i++)
            {
                float share = Mathf.Exp(-(distances[i] - nearest) / BlendRadius);
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

        /// <summary>
        /// fused_body.gd's own hand-authored grid origin (BOUNDS_MIN in
        /// fused_body.gd, confirmed by reading it directly: Vector3(-0.75,
        /// -0.05, -0.35)). NOT reused as an actual bounding limit -- Unity's
        /// own bounds must stay dynamically wider than this to also fit the
        /// T-pose Mixamo retargeting needs (see BoundsOf's own callers,
        /// which build segments from BodyRig.Bones(), a T-pose layout) --
        /// only as a PHASE REFERENCE for PhaseAlignedOrigin below.
        ///
        /// EXPLICIT REPORT: "the shoulders on godot look way better they
        /// arent like morphed into torso" / "the head... sunken into the
        /// torso." Every constant governing the SDF field itself (BlendRadius/
        /// fused_body.gd's BLEND_K, CellSize/CELL_SIZE, TorsoRadius, every
        /// bone position) was re-checked directly against fused_body.gd this
        /// session and confirmed identical -- the underlying shape MATH is
        /// not the difference. What differs is WHERE the marching-tetrahedra
        /// grid starts sampling that identical field: Extract()'s own origin
        /// is just bounds.min, unsnapped, and Unity's bounds.min is whatever
        /// the CURRENT rig's T-pose happens to measure to -- an essentially
        /// arbitrary float, in general agreeing with Godot's own fixed
        /// BOUNDS_MIN at NONE of the three axes' phase (value mod CellSize).
        /// Two grids sampling the identical SDF at different phases place
        /// their marching-tetrahedra vertices at different points along the
        /// isosurface -- most visible exactly where this report points,
        /// blend junctions with real curvature (the neck/shoulder area),
        /// least visible on a limb's own straight run. Confirmed this is not
        /// the previously-found-and-fixed "double smin" shoulder defect
        /// (fused_body.gd's own history, see LimbCapsules' doc comment) --
        /// this project's own LimbCapsules already builds the torso as ONE
        /// capsule, matching fused_body.gd's current (already-fixed) shape
        /// exactly.
        /// </summary>
        private static readonly Vector3 GodotGridPhaseReference = new Vector3(-0.75f, -0.05f, -0.35f);

        /// <summary>
        /// Snaps <paramref name="boundsMin"/> DOWN (never up -- flooring
        /// only extends the sampled region, it can never clip it) to the
        /// nearest point that shares Godot's own grid phase on every axis,
        /// so this engine's marching-tetrahedra grid samples the SDF field
        /// at the SAME relative offsets Godot's grid does, despite starting
        /// from a different, dynamically-measured bounding box. Assumes the
        /// reference body scale (1.0, matching fused_body.gd's own units
        /// directly) -- the one case this actually needs to match Godot
        /// exactly; a scaled character (Civilian at a different height,
        /// say) has no Godot reference to phase-align against in the first
        /// place.
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
