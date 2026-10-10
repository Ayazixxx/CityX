using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Where paint lives on the body, and how a point on the skin turns into a
    /// pixel in the paint texture.
    ///
    /// PORTED FROM fused_body.gd's build_uncached AND character.gd's atlas
    /// constants, INCLUDING THE FIXES. Every unobvious number in here was
    /// arrived at by something going visibly wrong in the Godot build --
    /// brushes stamping diamonds, strokes near an armpit appearing at the hip,
    /// paint smearing at the collar. They are recorded as reasons rather than
    /// carried across as magic, because the next person to "simplify" this will
    /// otherwise reintroduce all of them.
    ///
    /// THE LAYOUT: one ROW per body part, six columns wide. The rows are a
    /// cylindrical unwrap -- u is the angle around the limb, v runs along it.
    /// The head is the exception and is box-mapped into the six columns of its
    /// own row, which is what the columns are for.
    /// </summary>
    public static class PaintAtlas
    {
        /// <summary>character.gd ATLAS_CELL. One cell is one box face.</summary>
        public const int Cell = 64;

        /// <summary>character.gd FACE_COUNT: +X,-X,+Y,-Y,+Z,-Z for a box map.</summary>
        public const int FaceCount = 6;

        /// <summary>
        /// character.gd PART_NAMES, in order. The index into this IS the atlas
        /// row, so the order is part of the save format: reordering it
        /// repaints every existing character's arm onto their leg.
        /// </summary>
        public static readonly HumanBodyBones[] Parts =
        {
            HumanBodyBones.Head,
            HumanBodyBones.Hips,
            HumanBodyBones.Neck,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
        };

        public static int PartCount => Parts.Length;

        /// <summary>
        /// THE ATLAS IS FINER THAN GODOT'S: twice the texels around, four times along (owner, 2026-10-10, the creator's brush "feels very
        /// choppy"). At Godot's 64 per row a torso texel was about 4 mm around and 16 mm up the body, so any slanting line came out as stairs
        /// whatever the brush did; now it is about 2 by 4 mm, and the face about 5 by 3. The UVs are fractions of the atlas and do not change;
        /// only the texel count does. Paint saved at Godot's size (<see cref="LegacyWidth"/> by <see cref="LegacyHeight"/>) still loads,
        /// scaled up (BodyPaint.Decode, BodyPaint.Adopt).
        /// </summary>
        public const int ColumnDetail = 2;

        public const int RowDetail = 4;

        public static int Width => Cell * FaceCount * ColumnDetail;

        /// <summary>How many texels tall one part's row is.</summary>
        public static int RowHeight => Cell * RowDetail;

        public static int Height => RowHeight * PartCount;

        /// <summary>Godot's atlas, the size of every design made there and every save made before 2026-10-10.</summary>
        public static int LegacyWidth => Cell * FaceCount;

        public static int LegacyHeight => Cell * PartCount;

        /// <summary>
        /// fused_body.gd PAINT_BONE_FOR. A bone that is a colinear sub-split of
        /// another's limb -- a forearm of an upper arm, a shin of a thigh, the
        /// spine of the hips -- shares that parent's row and UV axis instead of
        /// getting its own, so paint stays continuous across the elbow and the
        /// knee rather than seaming there.
        /// </summary>
        public static HumanBodyBones PaintBoneFor(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Spine: return HumanBodyBones.Hips;

                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.LeftHand: return HumanBodyBones.LeftUpperArm;

                case HumanBodyBones.RightLowerArm:
                case HumanBodyBones.RightHand: return HumanBodyBones.RightUpperArm;

                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.LeftFoot: return HumanBodyBones.LeftUpperLeg;

                case HumanBodyBones.RightLowerLeg:
                case HumanBodyBones.RightFoot: return HumanBodyBones.RightUpperLeg;

                default: return bone;
            }
        }

        /// <summary>Which atlas row a paint bone owns, or -1 if it owns none.</summary>
        public static int RowOf(HumanBodyBones paintBone)
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                if (Parts[i] == paintBone)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// fused_body.gd _uv_axis: the WHOLE LIMB's axis, spanning any internal
        /// bone split. Deliberately not the individual bone's segment -- an arm
        /// unwraps as one piece from shoulder to fingertip, so a stroke drawn
        /// over the elbow stays one stroke.
        /// </summary>
        public static void AxisOf(HumanBodyBones paintBone, out Vector3 a, out Vector3 b)
        {
            switch (paintBone)
            {
                // THE HEAD HAD NO CASE HERE AND FELL THROUGH TO THE DEFAULT,
                // which handed back the world Y axis from the origin. The head
                // sits between y=1.9 and y=2.6, so every point on it came out
                // with rawT between 1.9 and 2.6 -- all of it past the end of a
                // one-unit axis, all of it therefore in the cap-overflow
                // branch, which asymptotes. The entire head collapsed into the
                // top 0.03% of its own row: about a hundredth of one pixel of
                // v. Painting it stamped a full-height stripe every time,
                // which is what "the brush is all widespread on the face"
                // looks like from the front.
                //
                // Godot never hit this because its head is not part of the
                // fused body at all -- it is a separate mesh with its own box
                // map, and fused_body.gd's own PAINT_BONES has no head in it.
                // Fusing the head in was this port's own decision, and it left
                // the head's unwrap behind.
                //
                // The sphere, end to end. A surface point at angle n has
                // rawT = (n.y + 1) / 2, so it spans exactly 0 to 1 with the
                // poles at the ends and nothing overflowing -- except the
                // blend where it melts into the neck, which bulges slightly
                // past the bottom and is exactly what the cap slice is for.
                case HumanBodyBones.Head:
                {
                    Vector3 centre = BodyRig.PositionOf(HumanBodyBones.Head);
                    Vector3 half = Vector3.up * FusedBodyShape.HeadRadius;
                    a = centre - half;
                    b = centre + half;
                    return;
                }

                case HumanBodyBones.Hips:
                    a = BodyRig.PositionOf(HumanBodyBones.Hips);
                    b = BodyRig.PositionOf(HumanBodyBones.Neck);
                    return;

                case HumanBodyBones.Neck:
                    a = BodyRig.PositionOf(HumanBodyBones.Neck);
                    b = BodyRig.PositionOf(HumanBodyBones.Head);
                    return;

                case HumanBodyBones.LeftUpperArm:
                    a = BodyRig.ShapePositionOf(HumanBodyBones.LeftUpperArm);
                    b = BodyRig.ShapePositionOf(HumanBodyBones.LeftHand);
                    return;

                case HumanBodyBones.RightUpperArm:
                    a = BodyRig.ShapePositionOf(HumanBodyBones.RightUpperArm);
                    b = BodyRig.ShapePositionOf(HumanBodyBones.RightHand);
                    return;

                case HumanBodyBones.LeftUpperLeg:
                    a = BodyRig.PositionOf(HumanBodyBones.LeftUpperLeg);
                    b = BodyRig.PositionOf(HumanBodyBones.LeftFoot);
                    return;

                case HumanBodyBones.RightUpperLeg:
                    a = BodyRig.PositionOf(HumanBodyBones.RightUpperLeg);
                    b = BodyRig.PositionOf(HumanBodyBones.RightFoot);
                    return;

                default:
                    // COMPLAINS RATHER THAN GUESSING. The silent world-Y
                    // fallback that used to live here is the whole reason the
                    // head shipped unpaintable: a part with no axis got a
                    // plausible-looking one instead of an error, and every
                    // check downstream passed because the numbers were finite.
                    // fused_body.gd's own _uv_axis pushes an error here too.
                    Debug.LogError($"CityX: no UV axis for paint bone {paintBone} -- " +
                                   "anything skinned to it cannot be painted.");
                    a = Vector3.zero;
                    b = Vector3.up;
                    return;
            }
        }

        private static Vector3 FingertipOf(float side)
        {
            return new Vector3(
                side * (BodyRig.ShoulderX + BodyRig.ArmLength), BodyRig.ShoulderY, 0.0f);
        }

        /// <summary>
        /// How much of each end of a row is reserved for a capsule's rounded
        /// CAP, and how quickly the overflow asymptotes into it.
        ///
        /// A capsule's caps genuinely extend past its a/b endpoints, and
        /// hard-clamping t to [0,1] collapsed every vertex out there onto one
        /// V coordinate. At the torso's top cap -- the collar, a natural place
        /// to paint -- that produced literally zero-area triangles, which the
        /// brush's own anisotropic correction cannot solve a tangent from, so
        /// it fell back to a generic shape; and the triangles beside them were
        /// crushed, because a whole cap's worth of surface was fighting into
        /// the last sliver before the clamp. Asymptoting into a reserved slice
        /// instead -- never quite reaching it, so no new degeneracy appears at
        /// the far end either -- spreads distinct 3D points across distinct V.
        /// </summary>
        public const float CapFraction = 0.2f;

        /// <summary>
        /// The head's share, which is much smaller, because the head is a
        /// SPHERE AND ITS AXIS ALREADY RUNS POLE TO POLE.
        ///
        /// Every other part is a capsule whose surface genuinely extends a
        /// whole radius past each end of its axis, and a fifth of the row is
        /// what it takes to spread that out. The head's axis is the sphere's
        /// own diameter, so its surface reaches exactly the ends and no
        /// further -- reserving another fifth at each end is reserving room
        /// for an overflow that cannot happen, and it cost the face half its
        /// vertical resolution.
        ///
        /// Not zero, though: the blend that melts the head into the neck
        /// bulges a little below the bottom pole, and with no slice at all
        /// every vertex out there would land on one v -- exactly the collapse
        /// the cap slice exists to prevent, just at a smaller scale.
        /// </summary>
        public const float HeadCapFraction = 0.05f;

        /// <summary>Which of the two a row uses.</summary>
        public static float CapFractionFor(int row)
        {
            return row == RowOf(HumanBodyBones.Head) ? HeadCapFraction : CapFraction;
        }

        public const float CapOverflowScale = 0.22f;

        /// <summary>
        /// Padding in from each row's true edges, in UV.
        ///
        /// t=0 and t=1 would otherwise land EXACTLY on row/PartCount, and
        /// adjacent rows share that boundary by construction -- so rounding at
        /// that knife-edge can push a vertex into its neighbour's row. That is
        /// what made paint on the bottom of the head come out at the crotch.
        /// </summary>
        public static float RowMargin => 2.0f / LegacyHeight;   // two of Godot's texels: a fraction of the atlas, so the UVs are unchanged

        /// <summary>
        /// The UV for one point on the skin, given the limb it belongs to.
        ///
        /// <paramref name="row"/> is the atlas row of that limb's paint bone.
        /// </summary>
        public static Vector2 Coordinate(Vector3 point, Vector3 axisA, Vector3 axisB, int row)
        {
            Vector3 ab = axisB - axisA;
            float rawT = Vector3.Dot(point - axisA, ab) / Mathf.Max(ab.sqrMagnitude, 1e-7f);

            // The angle only ever needs the nearest point ON the segment, so it
            // clamps regardless of how the length axis handles overflow.
            float clampedT = Mathf.Clamp01(rawT);
            Vector3 radial = point - (axisA + ab * clampedT);

            Frame(ab, out Vector3 right, out Vector3 forward);
            float theta = Mathf.Atan2(Vector3.Dot(radial, forward), Vector3.Dot(radial, right));
            float u = theta / (Mathf.PI * 2.0f) + 0.5f;

            float cap = CapFractionFor(row);

            float t;
            if (rawT > 1.0f)
            {
                float overflow = 1.0f - Mathf.Exp(-(rawT - 1.0f) / CapOverflowScale);
                t = (1.0f - cap) + overflow * cap;
            }
            else if (rawT < 0.0f)
            {
                float overflow = 1.0f - Mathf.Exp(rawT / CapOverflowScale);
                t = cap * (1.0f - overflow);
            }
            else
            {
                t = cap + rawT * (1.0f - 2.0f * cap);
            }

            float v0 = (float)row / PartCount + RowMargin;
            float v1 = (float)(row + 1) / PartCount - RowMargin;
            return new Vector2(u, v0 + t * (v1 - v0));
        }

        /// <summary>
        /// Two perpendicular directions across a limb, to measure its angle
        /// against.
        ///
        /// THE ONE PLACE THIS DELIBERATELY DIFFERS FROM fused_body.gd, and it
        /// had to. Godot measures the angle as atan2(radial.z, radial.x)
        /// outright, which works there because its bind pose rests with the
        /// arms DOWN -- every limb runs roughly along Y, so the radial
        /// direction lies in XZ and those two components span it.
        ///
        /// This rig binds in a T-POSE, because Unity's humanoid retargeting
        /// measures a rig against one. The arms therefore run along X, radial.x
        /// is always about zero on them, and atan2 collapses to plus or minus a
        /// right angle -- which folded each arm's whole circumference onto two
        /// u values. Measured: 19% of the body's triangles came out degenerate
        /// in UV, which is a brush stamping diamonds down both arms.
        ///
        /// Building the frame from the limb's own direction is the same idea
        /// expressed so it does not depend on which way the limb happens to
        /// point, and it agrees with Godot exactly on a vertical limb.
        /// </summary>
        public static void Frame(Vector3 along, out Vector3 right, out Vector3 forward)
        {
            Vector3 direction = along.normalized;

            // World X, with anything along the limb removed. On a vertical limb
            // that leaves X untouched, which is what keeps the torso's unwrap
            // identical to the Godot build's.
            right = Vector3.right - direction * Vector3.Dot(Vector3.right, direction);
            if (right.sqrMagnitude < 1e-6f)
            {
                // The limb runs along X itself -- an arm in the T-pose. Any
                // perpendicular will do as long as it is chosen consistently.
                right = Vector3.forward - direction * Vector3.Dot(Vector3.forward, direction);
            }
            right.Normalize();

            // Cross(right, direction), in that order, so a vertical limb gives
            // forward = +Z and the angle comes out as atan2(z, x) exactly.
            forward = Vector3.Cross(right, direction);
        }

        /// <summary>
        /// Unwraps one triangle's u values across the theta seam.
        ///
        /// u wraps from 1 back to 0 all the way round a limb. A triangle
        /// straddling that wrap has corners at, say, 0.99 and 0.01, and the
        /// hardware interpolates the whole way BACKWARDS through 0.5 -- the
        /// opposite side of the limb. That is what made paint near an armpit,
        /// which sits near the arm's own wrap seam, scatter along a trail of
        /// unrelated places in the same row.
        ///
        /// Safe only because this mesh is triangle soup: every triangle owns
        /// its three vertices outright, so treating 0.01 as 1.01 for this one
        /// triangle cannot disturb a neighbour. Painting wraps it back into
        /// [0,1) afterwards.
        /// </summary>
        public static void FixThetaSeam(ref Vector2 a, ref Vector2 b, ref Vector2 c)
        {
            float max = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float min = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            if (max - min <= 0.5f)
            {
                return;   // an ordinary spread, not a seam crossing
            }

            if (a.x < 0.5f) a.x += 1.0f;
            if (b.x < 0.5f) b.x += 1.0f;
            if (c.x < 0.5f) c.x += 1.0f;
        }
    }
}
