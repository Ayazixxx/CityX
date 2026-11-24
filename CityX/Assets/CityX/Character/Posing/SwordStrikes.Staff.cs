using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BO STAFF'S ATTACKS AND HELD POSES (docs/BO_STAFF.md; owner, 2026-11-19, with a clip of a staff fighter: "make copy these movements as
    /// best you can", its opening spin "maybe that will be the heavy hold click attack but the rest looks pretty good"). The clip's fighter grips the
    /// pole near one end so the long end reaches, and swings it from the hips with the whole body turning: so does this one, both fists on its last
    /// half metre as on a bat (StaffRules), the two-handed blades' hold. The chain of three: a chop from over the right shoulder, a level backhand
    /// sweep at the middle (both aimed straight ahead: owner, 2026-11-22), and a rising blow from low on the right up past the left shoulder
    /// (owner, 2026-11-22: the lunging thrust that was third "just looks bad", cut); the heavy is the clip's opening spin, wound right round to the
    /// right, held, and let go flat round the front. No attack stabs (owner, 2026-11-23: "there should be no stabbing animation with the pole"): at a
    /// sprint the chain opens with the chop, as it does standing. Every contact's twist is the clip's own (SourceArt/SwordMoves). The pole is
    /// 1.85 m past the fist: the low blows' keys keep its far end off the ground.
    /// </summary>
    public static partial class SwordStrikes
    {
        public const string StaffChopName = "staff_chop";
        public const string StaffSweepName = "staff_sweep";
        public const string StaffRisingName = "staff_rising";
        public const string StaffSpinName = "staff_spin";

        /// <summary>A bo staff's attack.</summary>
        public static bool IsStaff(SwordStrikeKind kind) => kind >= SwordStrikeKind.StaffChop && kind <= SwordStrikeKind.StaffSpin;

        /// <summary>
        /// The chop's plane: from over the right shoulder down across the front to the lower left, holding the straight-ahead, so the blow lands on
        /// whoever is in front (owner, 2026-11-22: it "starts too far to the left its like its aimed at someone to the side"); 32 degrees off the
        /// level, shallow enough that the long pole carried on through does not go into the ground.
        /// </summary>
        public static readonly CutPlane Chop = new CutPlane(Vector3.forward, new Vector3(-0.85f, -0.53f, 0.0f));

        /// <summary>The rising blow's plane: from low ahead on the right, up through the front and on up past the left shoulder.</summary>
        public static readonly CutPlane Rising = new CutPlane(new Vector3(0.1f, 0.05f, 1.0f), new Vector3(-0.85f, 0.53f, 0.0f));

        // ---- the held poses ----
        // (Made when asked for, not as fields: a partial class's fields in another file may be set before ArmMetres, which Pose divides by.)

        /// <summary>Where a staff's attacks start from and go back to: low, side on, both fists at the right hip and the long end up at the enemy's head.</summary>
        private static Key StaffReadyKey => Pose(0.0f, Ease.Smooth, hand: new Vector3(0.16f, 1.34f, 0.46f), blade: new Vector3(-0.12f, 0.42f, 0.90f),
            edge: new Vector3(-0.84f, -0.44f, 0.32f), elbow: 0.25f, twist: 14.0f, hips: 8.0f, lean: 5.0f, tilt: 0.0f, drop: 0.10f, lead: -1.0f, depth: 0.40f);

        // Carried upright in both hands before the right shoulder, the fists where a two-handed blade's carry has them (clear of the belly), the long end
        // standing up past the head; at a run, steeper still. (Over the shoulder in one hand, the left arm crossed the chest to take the pole for each
        // attack and the pole swung through the head on its way back up after the high blows.)
        // (The fists stand off the belly by a puffy coat's thickness: owner, 2026-11-22, "the left wrist clips into the jacket in idle position".)
        private static Key StaffCarryKey => Pose(0.0f, Ease.Smooth, hand: new Vector3(0.14f, 1.32f, 0.56f), blade: new Vector3(0.20f, 0.90f, 0.38f),
            edge: new Vector3(-0.62f, -0.40f, 0.68f), elbow: 0.10f, twist: 0.0f, hips: 0.0f, lean: 0.0f, tilt: 0.0f, drop: 0.0f, lead: -1.0f, depth: 0.30f);

        private static Key StaffTrailKey => Pose(0.0f, Ease.Smooth, hand: new Vector3(0.18f, 1.42f, 0.42f), blade: new Vector3(0.15f, 0.95f, 0.27f),
            edge: new Vector3(-0.63f, -0.26f, 0.73f), elbow: 0.10f, twist: 0.0f, hips: 0.0f, lean: 0.0f, tilt: 0.0f, drop: 0.0f, lead: -1.0f, depth: 0.30f);

        // The pole across the front in both hands, from the right hip up past the left shoulder: a staff's block.
        private static Key StaffGuardKey => Pose(0.0f, Ease.Smooth, hand: new Vector3(0.36f, 1.32f, 0.34f), blade: new Vector3(-0.62f, 0.74f, 0.25f),
            edge: new Vector3(0.6f, 0.3f, 0.75f), elbow: 0.30f, twist: 12.0f, hips: 6.0f, lean: 4.0f, tilt: 0.0f, drop: 0.10f, lead: -1.0f, depth: 0.40f);

        /// <summary>THE STAFF'S CARRY: upright in both hands before the right shoulder; the legs are the walk's.</summary>
        public static SwordFrame StaffCarry(float run01)
        {
            float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(run01));
            SwordFrame frame = Lerp(Frame(StaffCarryKey, 1.0f, true), Frame(StaffTrailKey, 1.0f, true), t);
            frame.Footwork = 0.0f;
            frame.FreeHandWeight = 0.0f;
            frame.ForearmAlign = 0.0f;
            return frame;
        }

        /// <summary>The staff's held guard: the pole across the front in both hands.</summary>
        public static SwordFrame StaffGuard()
        {
            SwordFrame frame = Frame(StaffGuardKey, 1.0f, true);
            frame.ForearmAlign = 0.0f;
            return frame;
        }

        // ---- the attacks ----

        private static Key[] BuildStaffKeys(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.StaffChop:
                {
                    // THE CHOP: both fists up over the right shoulder, the pole back behind it, the body coiled and leaning back; then down through
                    // the front with a stride, the body square to the enemy and the pole pointing straight at them at the contact, carried on down to
                    // the lower left only a little way.
                    CutPlane cut = Chop;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.40f, 1.82f, -0.02f), cut, -127.5f, elbow: 0.60f, twist: 48.0f, hips: 22.0f,
                        lean: -8.0f, tilt: -5.0f, drop: 0.12f, lead: -1.0f, depth: 0.35f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.10f, 1.42f, 0.62f), cut, 0.0f, elbow: 0.30f, twist: 0.0f, hips: -10.0f,
                        lean: 14.0f, tilt: 3.0f, drop: 0.16f, lead: -1.0f, depth: 0.70f, reach: 1.0f), new Vector3(0.50f, 1.66f, 0.46f), 20.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(-0.34f, 1.36f, 0.38f), cut, 60.0f, elbow: 0.40f, twist: -30.0f, hips: -26.0f,
                        lean: 12.0f, tilt: 3.0f, drop: 0.16f, lead: -1.0f, depth: 0.65f, reach: 0.6f);
                    return Assemble(kind, StaffReadyKey, windup, contact, follow);
                }
                case SwordStrikeKind.StaffSweep:
                {
                    // THE SWEEP: a level backhand, from where the chop left the fists: the fists at the left hip and the pole round behind on the left;
                    // then the whole body turns round to the right and the pole goes flat across the front at the enemy's middle, pointing straight at
                    // them as it passes, carrying on round to the right, where the rising blow starts. (Owner, 2026-11-22: it was swept low at the shins,
                    // "it shouldnt be angled downward just make it horizontal". Each blow starts where the last ended, as the two-handed blades' chain
                    // does: swept the other way, the fists crossed the front between the blows and the left arm went through the chest.)
                    CutPlane cut = BackhandFlat;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(-0.34f, 1.68f, 0.32f), cut, -110.0f, elbow: 0.50f, twist: -45.0f, hips: -24.0f,
                        lean: 10.0f, tilt: -4.0f, drop: 0.26f, lead: -1.0f, depth: 0.45f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.10f, 1.46f, 0.60f), cut, 0.0f, elbow: 0.25f, twist: 0.0f, hips: 14.0f,
                        lean: 10.0f, tilt: -2.0f, drop: 0.16f, lead: -1.0f, depth: 0.65f, reach: 1.0f), new Vector3(-0.40f, 1.46f, 0.50f), -20.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(0.55f, 1.44f, 0.30f), cut, 100.0f, elbow: 0.40f, twist: 56.0f, hips: 42.0f,
                        lean: 8.0f, tilt: 0.0f, drop: 0.15f, lead: -1.0f, depth: 0.60f, reach: 0.6f);
                    return Assemble(kind, StaffReadyKey, windup, contact, follow);
                }
                case SwordStrikeKind.StaffRising:
                {
                    // THE RISING BLOW (the chain's last), from where the sweep left the fists: coiled low on the right, the pole's long end down by the
                    // ground ahead on that side (a low guard), held a beat; then it scoops up through the front and on up past the left shoulder,
                    // the body rising and leaning back behind it. (Wound on the left, the fists would cross the front between the blows and the left
                    // arm go through the chest.)
                    CutPlane cut = Rising;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.40f, 1.24f, 0.36f), cut, -55.0f, elbow: 0.35f, twist: 30.0f, hips: 18.0f,
                        lean: 8.0f, tilt: 6.0f, drop: 0.26f, lead: -1.0f, depth: 0.50f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.0f, 1.45f, 0.50f), cut, 0.0f, elbow: 0.30f, twist: -20.0f, hips: -28.0f,
                        lean: -2.0f, tilt: 6.0f, drop: 0.10f, lead: -1.0f, depth: 0.68f, reach: 1.0f), new Vector3(0.25f, 1.30f, 0.50f), 10.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(-0.36f, 1.78f, 0.40f), cut, 100.0f, elbow: 0.50f, twist: -46.0f, hips: -38.0f,
                        lean: -10.0f, tilt: 6.0f, drop: 0.06f, lead: -1.0f, depth: 0.55f, reach: 0.6f);
                    return Assemble(kind, StaffReadyKey, windup, contact, follow);
                }
                default:
                {
                    // THE SPIN (the heavy, the clip's opening blow): wound right round to the right and sunk, the pole out behind on the right, held
                    // there (the tell); then flat round the front at the chest with the whole body turning through it and on round to the left.
                    CutPlane cut = ForehandFlat;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.48f, 1.58f, -0.05f), cut, -150.0f, elbow: 0.55f, twist: 78.0f, hips: 40.0f,
                        lean: -2.0f, tilt: 4.0f, drop: 0.20f, lead: -1.0f, depth: 0.45f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.04f, 1.48f, 0.70f), cut, -5.0f, elbow: 0.25f, twist: -8.0f, hips: -24.0f,
                        lean: 18.0f, tilt: 2.0f, drop: 0.18f, lead: -1.0f, depth: 0.65f, reach: 1.0f), new Vector3(0.55f, 1.52f, 0.55f), 30.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(-0.30f, 1.44f, 0.48f), cut, 95.0f, elbow: 0.40f, twist: -40.0f, hips: -36.0f,
                        lean: 14.0f, tilt: 0.0f, drop: 0.16f, lead: -1.0f, depth: 0.60f, reach: 0.6f);
                    return Assemble(kind, StaffReadyKey, windup, contact, follow);
                }
            }
        }
    }
}
