using UnityEngine;

namespace CityX.Character
{
    /// <summary>The sword attacks: four with a one-handed sword, four with a two-handed one.</summary>
    public enum SwordStrikeKind
    {
        /// <summary>One-handed: a horizontal slash, right to left, at chest height (the owner's choice for the first attack).</summary>
        Slash1H,

        /// <summary>One-handed: the backhand that follows, a diagonal cut from the upper left down to the right.</summary>
        Cut1H,

        /// <summary>One-handed: a thrust, the hilt drawn to the hip and driven straight out with a step.</summary>
        Thrust1H,

        /// <summary>One-handed heavy: an overhead cut from behind the head, down the middle.</summary>
        Overhead1H,

        /// <summary>Two-handed: the classic diagonal, from over the right shoulder down to the left.</summary>
        Cut2H,

        /// <summary>Two-handed: the rising backhand that follows, from low on the left up to the right.</summary>
        Reverse2H,

        /// <summary>Two-handed: a flat sweep right to left at waist height, the whole body turning through it.</summary>
        Sweep2H,

        /// <summary>Two-handed heavy: the overhead slam, a long hold at the top then everything into the floor.</summary>
        Slam2H,
    }

    /// <summary>
    /// What the sword arm, the torso and the free hand are doing at one moment, in terms that do not depend on the rig: where the sword hand is
    /// relative to its shoulder (body axes, arm lengths), which way the blade points and which way its edge faces (body axes), how the torso is
    /// turned, leaning and tilted, how far the hips sink, and where the other hand is when it is not on the grip.
    /// </summary>
    public struct SwordFrame
    {
        /// <summary>Anything to apply at all.</summary>
        public bool Active;

        /// <summary>0 = the animation's own arm and torso, 1 = this pose.</summary>
        public float Weight;

        /// <summary>The sword (right) hand's offset from the right shoulder: x to the body's right, y up, z forward, in arm lengths (shoulder to wrist).</summary>
        public Vector3 Hand;

        /// <summary>The blade's direction, hilt to tip, in the body's axes (unit).</summary>
        public Vector3 Blade;

        /// <summary>The way the cutting edge faces, perpendicular to the blade, in the body's axes (unit).</summary>
        public Vector3 Edge;

        /// <summary>How far the sword arm's elbow is pushed out to the side rather than hanging down (0 to 1).</summary>
        public float ElbowOut;

        /// <summary>The torso's turn about the vertical, degrees, positive to the body's right.</summary>
        public float TwistDegrees;

        /// <summary>The torso's lean forward, degrees (negative leans back).</summary>
        public float LeanDegrees;

        /// <summary>The torso's sideways tilt, degrees, positive dropping the right shoulder.</summary>
        public float TiltDegrees;

        /// <summary>How far the hips sink toward the ground, metres: the legs bend to keep the feet planted.</summary>
        public float HipDrop;

        /// <summary>True when the free hand is on the grip too (the support hand follows the sword); false and it is placed by <see cref="FreeHand"/>.</summary>
        public bool TwoHanded;

        /// <summary>The free (left) hand's offset from the left shoulder, same units as <see cref="Hand"/>; only with a one-handed sword.</summary>
        public Vector3 FreeHand;

        public float FreeElbowOut;

        /// <summary>0 to 1: how far through the cut it is (1 at the contact), for effects.</summary>
        public float Reach;
    }

    /// <summary>
    /// PROCEDURAL SWORD ATTACKS. The Mixamo two-handed clips played over the upper body were stiff chops with the body standing still: no wind-up
    /// worth the name, the blade close to the chest, the same arc for a light and a heavy, nothing for a one-handed sword at all. Each attack here
    /// is a handful of keyframes of the SWORD (where the hand is, which way the blade points, which way the edge faces) and the torso, joined the
    /// way a good sword animation is built:
    ///
    ///  * a WIND-UP that eases into a short HOLD at the top (the anticipation; a third of the action, as the animation guides have it);
    ///  * a STRIKE that accelerates all the way into the contact, so the blade is at its fastest the moment it lands, along an ARC (a Bezier
    ///    through a control point), never a straight chord;
    ///  * a FOLLOW-THROUGH that overshoots past the target and decelerates, then a slower RECOVERY back to the ready stance;
    ///  * the TORSO winds up against the cut and unwinds through it, leans into it, and the HIPS drop into the heavy ones;
    ///  * the EDGE leads the motion, so the flat of the blade is never what arrives.
    ///
    /// Pure data and maths (no scene): the paths can be tested, and PlayerLocomotion only has to put the hand where a frame says, point the blade
    /// the way it says (two-bone IK on the arm, then the wrist), and turn the spine.
    /// </summary>
    public static class SwordStrikes
    {
        public const string Slash1HName = "sword1h_slash";
        public const string Cut1HName = "sword1h_cut";
        public const string Thrust1HName = "sword1h_thrust";
        public const string Overhead1HName = "sword1h_overhead";
        public const string Cut2HName = "sword2h_cut";
        public const string Reverse2HName = "sword2h_reverse";
        public const string Sweep2HName = "sword2h_sweep";
        public const string Slam2HName = "sword2h_slam";

        /// <summary>The held guard poses, played as states so another player's copy raises the same guard.</summary>
        public const string Guard1HName = "sword1h_guard";

        public const string Guard2HName = "sword2h_guard";

        /// <summary>The parry flourish: a held guard snapped forward for a moment when it catches a blow in its first instant.</summary>
        public const string ParryName = "sword_parry";

        public static readonly SwordStrikeKind[] All =
        {
            SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H, SwordStrikeKind.Thrust1H, SwordStrikeKind.Overhead1H,
            SwordStrikeKind.Cut2H, SwordStrikeKind.Reverse2H, SwordStrikeKind.Sweep2H, SwordStrikeKind.Slam2H,
        };

        public static bool TryGet(string animation, out SwordStrikeKind kind)
        {
            switch (animation)
            {
                case Slash1HName: kind = SwordStrikeKind.Slash1H; return true;
                case Cut1HName: kind = SwordStrikeKind.Cut1H; return true;
                case Thrust1HName: kind = SwordStrikeKind.Thrust1H; return true;
                case Overhead1HName: kind = SwordStrikeKind.Overhead1H; return true;
                case Cut2HName: kind = SwordStrikeKind.Cut2H; return true;
                case Reverse2HName: kind = SwordStrikeKind.Reverse2H; return true;
                case Sweep2HName: kind = SwordStrikeKind.Sweep2H; return true;
                case Slam2HName: kind = SwordStrikeKind.Slam2H; return true;
                default: kind = SwordStrikeKind.Slash1H; return false;
            }
        }

        /// <summary>True for a guard state name, with which hold it is.</summary>
        public static bool TryGetGuard(string animation, out bool oneHanded)
        {
            oneHanded = animation == Guard1HName;
            return animation == Guard1HName || animation == Guard2HName;
        }

        /// <summary>A strike, a guard or the parry: anything this plays instead of the Animator.</summary>
        public static bool Has(string animation) => TryGet(animation, out _) || TryGetGuard(animation, out _) || animation == ParryName;

        public static string NameOf(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.Slash1H: return Slash1HName;
                case SwordStrikeKind.Cut1H: return Cut1HName;
                case SwordStrikeKind.Thrust1H: return Thrust1HName;
                case SwordStrikeKind.Overhead1H: return Overhead1HName;
                case SwordStrikeKind.Cut2H: return Cut2HName;
                case SwordStrikeKind.Reverse2H: return Reverse2HName;
                case SwordStrikeKind.Sweep2H: return Sweep2HName;
                default: return Slam2HName;
            }
        }

        public static string GuardName(bool oneHanded) => oneHanded ? Guard1HName : Guard2HName;

        public static bool IsOneHanded(SwordStrikeKind kind)
        {
            return kind == SwordStrikeKind.Slash1H || kind == SwordStrikeKind.Cut1H
                || kind == SwordStrikeKind.Thrust1H || kind == SwordStrikeKind.Overhead1H;
        }

        public static bool IsHeavy(SwordStrikeKind kind) => kind == SwordStrikeKind.Overhead1H || kind == SwordStrikeKind.Slam2H;

        /// <summary>How long the whole attack takes, seconds: wind-up, hold, strike, follow-through and recovery.</summary>
        public static float Seconds(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.Slash1H: return 0.75f;
                case SwordStrikeKind.Cut1H: return 0.80f;
                case SwordStrikeKind.Thrust1H: return 0.70f;
                case SwordStrikeKind.Overhead1H: return 1.10f;
                case SwordStrikeKind.Cut2H: return 0.90f;
                case SwordStrikeKind.Reverse2H: return 0.90f;
                case SwordStrikeKind.Sweep2H: return 1.00f;
                default: return 1.50f;
            }
        }

        /// <summary>Where in the attack (0-1) the blade becomes dangerous: just as the strike leaves the hold.</summary>
        public static float ActiveStart(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.Slash1H: return 0.42f;
                case SwordStrikeKind.Cut1H: return 0.42f;
                case SwordStrikeKind.Thrust1H: return 0.40f;
                case SwordStrikeKind.Overhead1H: return 0.54f;
                case SwordStrikeKind.Cut2H: return 0.42f;
                case SwordStrikeKind.Reverse2H: return 0.42f;
                case SwordStrikeKind.Sweep2H: return 0.42f;
                default: return 0.56f;
            }
        }

        /// <summary>Where in the attack (0-1) the blade stops being dangerous: into the follow-through, before it has slowed.</summary>
        public static float ActiveEnd(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.Slash1H: return 0.66f;
                case SwordStrikeKind.Cut1H: return 0.66f;
                case SwordStrikeKind.Thrust1H: return 0.64f;
                case SwordStrikeKind.Overhead1H: return 0.76f;
                case SwordStrikeKind.Cut2H: return 0.68f;
                case SwordStrikeKind.Reverse2H: return 0.68f;
                case SwordStrikeKind.Sweep2H: return 0.70f;
                default: return 0.78f;
            }
        }

        /// <summary>The moment of contact (0-1): the end of the strike, where the blade is fastest and the reach is 1.</summary>
        public static float ContactFraction(SwordStrikeKind kind)
        {
            Key[] keys = KeysFor(kind);
            foreach (Key key in keys)
            {
                if (key.Reach >= 0.999f)
                {
                    return key.T;
                }
            }
            return 0.5f;
        }

        // ---- the ready stances and guards ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The stance the attacks start from and return to, held for a while after the last one. One-handed: the sword in front at waist height
        /// pointing at the enemy (a middle guard), the free hand raised across the body, the right shoulder drawn back a touch (a fencer's
        /// quarter turn). Two-handed: the sword up in front, both hands on the grip.
        /// </summary>
        public static SwordFrame Ready(bool oneHanded)
        {
            return oneHanded ? Frame(ReadyKey1H, 1.0f, false) : Frame(ReadyKey2H, 1.0f, true);
        }

        /// <summary>The held guard: one-handed, the blade across the body at chest height; two-handed, the blade up in front, edge out.</summary>
        public static SwordFrame Guard(bool oneHanded)
        {
            return oneHanded ? Frame(GuardKey1H, 1.0f, false) : Frame(GuardKey2H, 1.0f, true);
        }

        /// <summary>The free (left) hand's place in the one-handed stance: raised across the chest, elbow bent, a balance and a guard.</summary>
        public static readonly Vector3 FreeHandReady = new Vector3(0.22f, -0.05f, 0.45f);

        public const float FreeHandElbowOut = 0.35f;

        private static readonly Key ReadyKey1H = MakeKey(0.0f, new Vector3(0.22f, -0.45f, 0.52f), new Vector3(-0.12f, 0.25f, 0.96f), new Vector3(-1.0f, 0.0f, 0.2f),
            0.30f, 10.0f, 2.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);

        private static readonly Key ReadyKey2H = MakeKey(0.0f, new Vector3(0.08f, -0.20f, 0.50f), new Vector3(-0.12f, 0.90f, 0.42f), Vector3.forward,
            0.30f, 0.0f, 0.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);

        private static readonly Key GuardKey1H = MakeKey(0.0f, new Vector3(0.0f, -0.15f, 0.55f), new Vector3(-0.62f, 0.75f, 0.24f), Vector3.forward,
            0.50f, 15.0f, 3.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);

        private static readonly Key GuardKey2H = MakeKey(0.0f, new Vector3(0.0f, -0.02f, 0.52f), new Vector3(-0.20f, 0.92f, 0.33f), Vector3.forward,
            0.35f, 0.0f, 3.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);

        // ---- the keyframes -------------------------------------------------------------------------------------------------------------

        private enum Ease
        {
            /// <summary>Smoothstep: a wind-up (anticipation is never faster than the action), a recovery.</summary>
            Smooth,

            /// <summary>Fast off the mark, decelerating into the key: a follow-through losing its speed.</summary>
            SlowIn,

            /// <summary>Slow off the mark, accelerating hard into the key: the strike, fastest at the contact.</summary>
            Snap,

            /// <summary>Straight: a hold (the pose does not change).</summary>
            Linear,
        }

        private struct Key
        {
            public float T;
            public Vector3 Hand;
            public Vector3 Blade;
            public Vector3 Edge;
            public float ElbowOut;
            public float Twist;
            public float Lean;
            public float Tilt;
            public float Drop;
            public Ease Ease;
            public float Reach;
            public bool HasControl;
            public Vector3 Control;
            public bool HasVia;
            public Vector3 BladeVia;
        }

        /// <summary>A key with its blade made unit and its edge made perpendicular to the blade (the edge is given as the direction it should lead toward).</summary>
        private static Key MakeKey(float t, Vector3 hand, Vector3 blade, Vector3 edgeLead, float elbowOut, float twist, float lean, float tilt, float drop, Ease ease, float reach)
        {
            Vector3 b = blade.normalized;
            Vector3 e = edgeLead - b * Vector3.Dot(edgeLead, b);
            if (e.sqrMagnitude < 1e-6f)
            {
                e = Vector3.Cross(b, Vector3.up);
                if (e.sqrMagnitude < 1e-6f) e = Vector3.Cross(b, Vector3.forward);
            }
            return new Key
            {
                T = t, Hand = hand, Blade = b, Edge = e.normalized, ElbowOut = elbowOut, Twist = twist, Lean = lean, Tilt = tilt, Drop = drop, Ease = ease, Reach = reach,
            };
        }

        private static Key Arc(Key key, Vector3 control)
        {
            key.HasControl = true;
            key.Control = control;
            return key;
        }

        /// <summary>The blade passes near this direction halfway through the segment into this key: a flat sweep stays flat instead of flipping through the vertical (the shortest turn between two nearly opposite directions).</summary>
        private static Key Via(Key key, Vector3 bladeVia)
        {
            key.HasVia = true;
            key.BladeVia = bladeVia.normalized;
            return key;
        }

        private static Key Hold(Key key, float t)
        {
            key.T = t;
            key.Ease = Ease.Linear;
            return key;
        }

        private static Key At(Key key, float t)
        {
            key.T = t;
            return key;
        }

        private static readonly Key[][] Cache = new Key[8][];

        private static Key[] KeysFor(SwordStrikeKind kind)
        {
            int index = (int)kind;
            if (Cache[index] == null)
            {
                Cache[index] = BuildKeys(kind);
            }
            return Cache[index];
        }

        private static Key[] BuildKeys(SwordStrikeKind kind)
        {
            Key ready1 = ReadyKey1H;
            Key ready2 = ReadyKey2H;
            switch (kind)
            {
                case SwordStrikeKind.Slash1H:
                {
                    // Wound up back and out to the right at chest height with the blade trailing behind, torso turned with it; a flat arc across
                    // the front; the follow-through carries it on round to the left with the torso unwound past square.
                    Key windup = MakeKey(0.30f, new Vector3(0.62f, -0.30f, -0.25f), new Vector3(0.35f, 0.22f, -0.91f), new Vector3(-0.6f, 0.0f, 0.8f),
                        0.65f, 32.0f, -3.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);
                    Key contact = Via(Arc(MakeKey(0.60f, new Vector3(-0.05f, -0.38f, 0.90f), new Vector3(-0.50f, 0.0f, 0.87f), new Vector3(-0.87f, 0.0f, -0.5f),
                        0.30f, -30.0f, 7.0f, 3.0f, 0.02f, Ease.Snap, 1.0f), new Vector3(0.78f, -0.34f, 0.55f)), new Vector3(0.7f, 0.05f, 0.7f));
                    Key follow = MakeKey(0.74f, new Vector3(-0.48f, -0.32f, 0.50f), new Vector3(-0.93f, 0.06f, 0.36f), new Vector3(-0.36f, 0.0f, -0.93f),
                        0.40f, -45.0f, 5.0f, 4.0f, 0.02f, Ease.SlowIn, 0.6f);
                    return new[] { ready1, windup, Hold(windup, 0.40f), contact, follow, At(ready1, 1.0f) };
                }
                case SwordStrikeKind.Cut1H:
                {
                    // The backhand: the hand crosses to the left at chin height with the blade over the left shoulder (not in front of the face),
                    // then cuts diagonally down to the lower right.
                    Key windup = MakeKey(0.32f, new Vector3(-0.55f, 0.10f, 0.30f), new Vector3(-0.50f, 0.70f, -0.50f), new Vector3(0.75f, 0.3f, 0.3f),
                        0.60f, -28.0f, -3.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);
                    Key contact = Via(Arc(MakeKey(0.60f, new Vector3(0.40f, -0.22f, 0.84f), new Vector3(0.60f, -0.38f, 0.70f), new Vector3(0.7f, -0.7f, 0.0f),
                        0.40f, 28.0f, 9.0f, -4.0f, 0.0f, Ease.Snap, 1.0f), new Vector3(-0.10f, 0.30f, 0.75f)), new Vector3(0.1f, 0.45f, 0.88f));
                    Key follow = MakeKey(0.74f, new Vector3(0.66f, -0.45f, 0.42f), new Vector3(0.55f, -0.68f, 0.48f), new Vector3(0.3f, -0.8f, -0.5f),
                        0.50f, 38.0f, 6.0f, -3.0f, 0.0f, Ease.SlowIn, 0.6f);
                    return new[] { ready1, windup, Hold(windup, 0.40f), contact, follow, At(ready1, 1.0f) };
                }
                case SwordStrikeKind.Thrust1H:
                {
                    // Drawn back to the hip, point forward, then driven straight out with the body leaning in behind it.
                    Key windup = MakeKey(0.30f, new Vector3(0.38f, -0.40f, -0.08f), new Vector3(-0.08f, 0.12f, 0.99f), Vector3.left,
                        0.55f, 28.0f, -2.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);
                    Key contact = MakeKey(0.56f, new Vector3(0.05f, -0.15f, 1.00f), new Vector3(-0.03f, 0.04f, 1.0f), Vector3.left,
                        0.20f, -25.0f, 14.0f, 0.0f, 0.0f, Ease.Snap, 1.0f);
                    Key follow = MakeKey(0.66f, new Vector3(0.02f, -0.17f, 1.00f), new Vector3(-0.03f, 0.04f, 1.0f), Vector3.left,
                        0.20f, -25.0f, 14.0f, 0.0f, 0.0f, Ease.Linear, 0.9f);
                    return new[] { ready1, windup, Hold(windup, 0.38f), contact, follow, At(ready1, 1.0f) };
                }
                case SwordStrikeKind.Overhead1H:
                {
                    // Raised behind the head, a long hold leaning back, then over the top and down the middle with the hips sinking.
                    Key windup = MakeKey(0.36f, new Vector3(0.28f, 0.60f, -0.25f), new Vector3(0.15f, 0.55f, -0.82f), new Vector3(0.0f, 0.82f, 0.55f),
                        0.55f, 12.0f, -10.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);
                    Key contact = Via(Arc(MakeKey(0.68f, new Vector3(0.05f, 0.02f, 0.92f), new Vector3(-0.05f, -0.30f, 0.95f), new Vector3(0.0f, -1.0f, 0.3f),
                        0.20f, -10.0f, 18.0f, 0.0f, 0.10f, Ease.Snap, 1.0f), new Vector3(0.20f, 0.72f, 0.45f)), new Vector3(0.0f, 0.6f, 0.8f));
                    Key follow = MakeKey(0.80f, new Vector3(0.05f, -0.35f, 0.78f), new Vector3(-0.05f, -0.66f, 0.75f), new Vector3(0.0f, -0.75f, -0.66f),
                        0.30f, -12.0f, 22.0f, 0.0f, 0.14f, Ease.SlowIn, 0.6f);
                    return new[] { ready1, windup, Hold(windup, 0.52f), contact, follow, At(ready1, 1.0f) };
                }
                case SwordStrikeKind.Cut2H:
                {
                    // Both hands up over the right shoulder (kept near the body so the left hand can stay on the grip), the diagonal down to the
                    // left with the torso unwinding, the hips dropping a little under it.
                    Key windup = MakeKey(0.32f, new Vector3(0.12f, 0.46f, -0.08f), new Vector3(0.25f, 0.62f, -0.74f), new Vector3(-0.3f, 0.75f, 0.55f),
                        0.55f, 30.0f, -5.0f, -3.0f, 0.0f, Ease.Smooth, 0.0f);
                    Key contact = Via(Arc(MakeKey(0.62f, new Vector3(-0.18f, -0.08f, 0.80f), new Vector3(-0.50f, -0.28f, 0.82f), new Vector3(-0.6f, -0.7f, 0.2f),
                        0.30f, -32.0f, 12.0f, 5.0f, 0.06f, Ease.Snap, 1.0f), new Vector3(0.15f, 0.42f, 0.55f)), new Vector3(-0.1f, 0.5f, 0.86f));
                    Key follow = MakeKey(0.76f, new Vector3(-0.42f, -0.35f, 0.58f), new Vector3(-0.62f, -0.50f, 0.61f), new Vector3(-0.4f, -0.6f, -0.6f),
                        0.35f, -42.0f, 10.0f, 6.0f, 0.08f, Ease.SlowIn, 0.6f);
                    return new[] { ready2, windup, Hold(windup, 0.40f), contact, follow, At(ready2, 1.0f) };
                }
                case SwordStrikeKind.Reverse2H:
                {
                    // From low on the left, rising across to the upper right -- the return cut of the diagonal.
                    Key windup = MakeKey(0.32f, new Vector3(-0.26f, -0.36f, 0.24f), new Vector3(-0.55f, -0.48f, -0.68f), new Vector3(0.6f, 0.6f, 0.4f),
                        0.50f, -25.0f, 6.0f, 5.0f, 0.05f, Ease.Smooth, 0.0f);
                    Key contact = Via(Arc(MakeKey(0.62f, new Vector3(0.12f, 0.18f, 0.85f), new Vector3(0.40f, 0.45f, 0.80f), new Vector3(0.55f, 0.75f, -0.3f),
                        0.35f, 25.0f, 4.0f, -4.0f, 0.0f, Ease.Snap, 1.0f), new Vector3(-0.10f, -0.15f, 0.65f)), new Vector3(-0.1f, 0.0f, 1.0f));
                    Key follow = MakeKey(0.76f, new Vector3(0.25f, 0.48f, 0.30f), new Vector3(0.40f, 0.78f, -0.48f), new Vector3(0.3f, 0.5f, 0.8f),
                        0.50f, 35.0f, -3.0f, -5.0f, 0.0f, Ease.SlowIn, 0.6f);
                    return new[] { ready2, windup, Hold(windup, 0.40f), contact, follow, At(ready2, 1.0f) };
                }
                case SwordStrikeKind.Sweep2H:
                {
                    // The hilt drawn to the right hip, close in (the left hand stays on the grip), the blade trailing behind; then a flat sweep
                    // across the front, the whole torso turning through it.
                    Key windup = MakeKey(0.32f, new Vector3(0.02f, -0.32f, 0.02f), new Vector3(0.66f, 0.30f, -0.69f), new Vector3(-0.7f, 0.0f, 0.7f),
                        0.50f, 35.0f, -2.0f, 0.0f, 0.04f, Ease.Smooth, 0.0f);
                    Key contact = Via(Arc(MakeKey(0.62f, new Vector3(-0.05f, -0.24f, 0.78f), new Vector3(-0.55f, 0.0f, 0.84f), new Vector3(-0.84f, 0.0f, -0.55f),
                        0.30f, -35.0f, 8.0f, 4.0f, 0.08f, Ease.Snap, 1.0f), new Vector3(0.40f, -0.30f, 0.55f)), new Vector3(0.75f, 0.1f, 0.65f));
                    Key follow = MakeKey(0.78f, new Vector3(-0.35f, -0.22f, 0.45f), new Vector3(-0.95f, 0.12f, 0.28f), new Vector3(-0.28f, 0.0f, -0.95f),
                        0.40f, -55.0f, 6.0f, 5.0f, 0.06f, Ease.SlowIn, 0.6f);
                    return new[] { ready2, windup, Hold(windup, 0.40f), contact, follow, At(ready2, 1.0f) };
                }
                default:
                {
                    // The slam: high overhead leaning back, the longest hold of any attack, then down the middle with the hips sinking hard.
                    Key windup = MakeKey(0.34f, new Vector3(0.08f, 0.70f, -0.18f), new Vector3(0.05f, 0.40f, -0.92f), new Vector3(0.0f, 0.92f, 0.4f),
                        0.50f, 5.0f, -12.0f, 0.0f, 0.0f, Ease.Smooth, 0.0f);
                    Key contact = Via(Arc(MakeKey(0.70f, new Vector3(0.05f, 0.05f, 0.90f), new Vector3(0.0f, -0.36f, 0.93f), new Vector3(0.0f, -0.93f, -0.36f),
                        0.25f, -5.0f, 20.0f, 0.0f, 0.12f, Ease.Snap, 1.0f), new Vector3(0.10f, 0.68f, 0.50f)), new Vector3(0.0f, 0.6f, 0.8f));
                    Key follow = MakeKey(0.82f, new Vector3(0.05f, -0.38f, 0.74f), new Vector3(0.0f, -0.45f, 0.89f), new Vector3(0.0f, -0.89f, -0.45f),
                        0.30f, -5.0f, 24.0f, 0.0f, 0.14f, Ease.SlowIn, 0.6f);
                    return new[] { ready2, windup, Hold(windup, 0.54f), contact, follow, At(ready2, 1.0f) };
                }
            }
        }

        // ---- evaluation -----------------------------------------------------------------------------------------------------------------

        private static float Eased(Ease ease, float u)
        {
            u = Mathf.Clamp01(u);
            switch (ease)
            {
                case Ease.Smooth: return u * u * (3.0f - 2.0f * u);
                case Ease.SlowIn: return 1.0f - (1.0f - u) * (1.0f - u);
                case Ease.Snap: return u * u;
                default: return u;
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u)
        {
            float v = 1.0f - u;
            return v * v * a + 2.0f * v * u * c + u * u * b;
        }

        /// <summary>The pose at <paramref name="t01"/> (0 to 1) through an attack of the given kind.</summary>
        public static SwordFrame Evaluate(SwordStrikeKind kind, float t01)
        {
            t01 = Mathf.Clamp01(t01);
            Key[] keys = KeysFor(kind);
            bool twoHanded = !IsOneHanded(kind);

            int i = 0;
            while (i < keys.Length - 2 && t01 >= keys[i + 1].T)
            {
                i++;
            }
            Key a = keys[i];
            Key b = keys[i + 1];
            float span = Mathf.Max(b.T - a.T, 1e-4f);
            float u = Eased(b.Ease, (t01 - a.T) / span);

            var key = new Key
            {
                Hand = b.HasControl ? Bezier(a.Hand, b.Control, b.Hand, u) : Vector3.LerpUnclamped(a.Hand, b.Hand, u),
                Blade = b.HasVia ? Vector3.Slerp(Vector3.Slerp(a.Blade, b.BladeVia, u), Vector3.Slerp(b.BladeVia, b.Blade, u), u) : Vector3.Slerp(a.Blade, b.Blade, u),
                Edge = Vector3.Slerp(a.Edge, b.Edge, u),
                ElbowOut = Mathf.Lerp(a.ElbowOut, b.ElbowOut, u),
                Twist = Mathf.Lerp(a.Twist, b.Twist, u),
                Lean = Mathf.Lerp(a.Lean, b.Lean, u),
                Tilt = Mathf.Lerp(a.Tilt, b.Tilt, u),
                Drop = Mathf.Lerp(a.Drop, b.Drop, u),
                Reach = Mathf.Lerp(a.Reach, b.Reach, u),
            };
            return Frame(key, 1.0f, twoHanded);
        }

        private static SwordFrame Frame(Key key, float weight, bool twoHanded)
        {
            Vector3 blade = key.Blade.sqrMagnitude > 1e-6f ? key.Blade.normalized : Vector3.forward;
            Vector3 edge = key.Edge - blade * Vector3.Dot(key.Edge, blade);
            if (edge.sqrMagnitude < 1e-6f)
            {
                edge = Vector3.Cross(blade, Vector3.up);
            }
            return new SwordFrame
            {
                Active = true,
                Weight = weight,
                Hand = key.Hand,
                Blade = blade,
                Edge = edge.normalized,
                ElbowOut = key.ElbowOut,
                TwistDegrees = key.Twist,
                LeanDegrees = key.Lean,
                TiltDegrees = key.Tilt,
                HipDrop = key.Drop,
                TwoHanded = twoHanded,
                FreeHand = twoHanded ? Vector3.zero : FreeHandFor(key),
                FreeElbowOut = FreeHandElbowOut,
                Reach = key.Reach,
            };
        }

        /// <summary>The free hand through a one-handed attack: raised across the body, swinging a little the other way from the cut for balance.</summary>
        private static Vector3 FreeHandFor(Key key)
        {
            // Inward (positive x on the left hand) as the sword goes out to the right, and the other way; a touch higher while the torso leans.
            float counter = Mathf.Clamp(-key.Hand.x * 0.25f, -0.15f, 0.15f);
            return FreeHandReady + new Vector3(counter, key.Lean * 0.004f, -key.Reach * 0.12f);
        }

        /// <summary>
        /// Halfway (by <paramref name="t"/>) between two poses: positions and angles straight, the blade and edge turned, the hands' hold from
        /// <paramref name="b"/>. How one attack takes over from the pose the body is actually in (the last one's recovery, a guard).
        /// </summary>
        public static SwordFrame Lerp(in SwordFrame a, in SwordFrame b, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 blade = Vector3.Slerp(a.Blade, b.Blade, t);
            if (blade.sqrMagnitude < 1e-6f) blade = b.Blade;
            blade.Normalize();
            Vector3 edge = Vector3.Slerp(a.Edge, b.Edge, t);
            edge -= blade * Vector3.Dot(edge, blade);
            if (edge.sqrMagnitude < 1e-6f) edge = Vector3.Cross(blade, Vector3.up);
            return new SwordFrame
            {
                Active = true,
                Weight = Mathf.Lerp(a.Weight, b.Weight, t),
                Hand = Vector3.Lerp(a.Hand, b.Hand, t),
                Blade = blade,
                Edge = edge.normalized,
                ElbowOut = Mathf.Lerp(a.ElbowOut, b.ElbowOut, t),
                TwistDegrees = Mathf.Lerp(a.TwistDegrees, b.TwistDegrees, t),
                LeanDegrees = Mathf.Lerp(a.LeanDegrees, b.LeanDegrees, t),
                TiltDegrees = Mathf.Lerp(a.TiltDegrees, b.TiltDegrees, t),
                HipDrop = Mathf.Lerp(a.HipDrop, b.HipDrop, t),
                TwoHanded = b.TwoHanded,
                FreeHand = Vector3.Lerp(a.FreeHand, b.FreeHand, t),
                FreeElbowOut = Mathf.Lerp(a.FreeElbowOut, b.FreeElbowOut, t),
                Reach = Mathf.Lerp(a.Reach, b.Reach, t),
            };
        }

        /// <summary>
        /// The blade's tip relative to the right shoulder, in body metres, for a frame: the hand plus the blade, for checking reach and clearance
        /// (<paramref name="armLength"/> metres from the shoulder to the wrist, <paramref name="bladeLength"/> metres from the wrist to the tip).
        /// </summary>
        public static Vector3 TipOffset(in SwordFrame frame, float armLength, float bladeLength)
        {
            return frame.Hand * armLength + frame.Blade * bladeLength;
        }
    }
}
