using UnityEngine;

namespace CityX.Character
{
    public enum FistKind
    {
        Jab,
        Cross,
        Hook,
        Uppercut,
        Haymaker,
    }

    /// <summary>
    /// What the arms and torso are doing at one moment of an unarmed strike, in terms that do not depend on the rig: where each fist is relative to ITS
    /// shoulder (in the body's own axes, in units of the arm's length), how strongly to hold it there, and how far the torso is turned and leaning.
    /// </summary>
    public struct FistFrame
    {
        /// <summary>Anything to apply at all.</summary>
        public bool Active;

        /// <summary>Each arm: 0 = the animation's own arm, 1 = the IK target below.</summary>
        public float RightWeight, LeftWeight;

        /// <summary>Right-handed offsets from the shoulder: x to the body's right, y up, z forward, in arm lengths.</summary>
        public Vector3 RightHand, LeftHand;

        /// <summary>How much the elbow is pushed out to the side rather than down (0 straight punches, 1 a hook).</summary>
        public float RightElbowOut, LeftElbowOut;

        /// <summary>The torso's turn about the vertical, degrees, positive to the body's right.</summary>
        public float TwistDegrees;

        /// <summary>The torso's lean forward, degrees (negative leans back).</summary>
        public float LeanDegrees;

        /// <summary>0 to 1: how far through the blow's FORWARD reach it is (for effects).</summary>
        public float Extension;
    }

    /// <summary>
    /// PROCEDURAL FIST STRIKES. The old punches were two weak Mixamo jab clips played over the WHOLE body: the feet froze for the clip, the arms barely
    /// left the chest on this rig (short arms on a tall body), and every punch was the same punch. Third-person fighting games (Sifu, the Arkham and
    /// Yakuza games, Street Fighter's 3D cousins, Fortnite's melee) all get their feel from the same few things, which this is built on:
    ///
    ///  * a GUARD stance the fists return to, so the fighter looks ready and every blow starts and ends in the same place;
    ///  * a short ANTICIPATION (the fist drawn back), a fast EXTENSION that accelerates into the target, a brief HOLD on contact, a slower RECOVERY;
    ///  * different punches along different PATHS: a straight jab, a cross with the hips behind it, a hook that sweeps in an arc, an uppercut that
    ///    rises, a haymaker that arches over the top;
    ///  * the TORSO turns and leans into each blow (the arm alone is a puppet's), against a small counter-turn in the anticipation;
    ///  * the arm reaches FULL EXTENSION at the moment of contact (the hand goes where the blow lands), not a fixed short arc.
    ///
    /// Pure data and maths (no scene), so the paths can be tested: contact is at <see cref="ContactFraction"/> of the swing, the fist is at full reach
    /// then, and the pose starts and ends in the guard.
    /// </summary>
    public static class FistStrikes
    {
        public const string JabName = "punch_jab";
        public const string CrossName = "punch_cross";
        public const string HookName = "punch_hook";
        public const string UppercutName = "punch_uppercut";
        public const string HaymakerName = "punch_heavy";

        public static bool TryGet(string animation, out FistKind kind)
        {
            switch (animation)
            {
                case JabName: kind = FistKind.Jab; return true;
                case CrossName: kind = FistKind.Cross; return true;
                case HookName: kind = FistKind.Hook; return true;
                case UppercutName: kind = FistKind.Uppercut; return true;
                case HaymakerName: kind = FistKind.Haymaker; return true;
                default: kind = FistKind.Jab; return false;
            }
        }

        public static bool Has(string animation) => TryGet(animation, out _);

        public static string NameOf(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.Jab: return JabName;
                case FistKind.Cross: return CrossName;
                case FistKind.Hook: return HookName;
                case FistKind.Uppercut: return UppercutName;
                default: return HaymakerName;
            }
        }

        /// <summary>The right hand throws it (the cross, the uppercut, the haymaker); the jab and the hook are the lead, left, hand.</summary>
        public static bool UsesRightHand(FistKind kind) => kind == FistKind.Cross || kind == FistKind.Uppercut || kind == FistKind.Haymaker;

        /// <summary>How long the whole blow takes, seconds: anticipation, strike, hold and recovery.</summary>
        public static float Seconds(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.Jab: return 0.30f;
                case FistKind.Cross: return 0.36f;
                case FistKind.Hook: return 0.42f;
                case FistKind.Uppercut: return 0.42f;
                default: return 0.70f;
            }
        }

        /// <summary>Where in the blow it LANDS, as a fraction of its length (the fist is at full reach then).</summary>
        public static float ContactFraction(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.Jab: return 0.38f;
                case FistKind.Cross: return 0.42f;
                case FistKind.Hook: return 0.45f;
                case FistKind.Uppercut: return 0.45f;
                default: return 0.62f;
            }
        }

        /// <summary>The share of walking pace the body keeps while throwing it (the legs go on walking under the blow).</summary>
        public static float MoveFraction(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.Jab: return 0.8f;
                case FistKind.Cross: return 0.75f;
                case FistKind.Hook: return 0.7f;
                case FistKind.Uppercut: return 0.65f;
                default: return 0.5f;
            }
        }

        /// <summary>How hard the body steps INTO the blow, as a share of walking pace, while its fist is going out (zero after the contact).</summary>
        public static float LungeFraction(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.Jab: return 0.55f;
                case FistKind.Cross: return 0.8f;
                case FistKind.Hook: return 0.6f;
                case FistKind.Uppercut: return 0.5f;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// Where each fist rests in the guard, in the same units: the lead (left) hand a little further out than the rear. These place the WRIST; the hand now reaches a
        /// further 0.16 m past it (the arm is the Godot character's full length, see BodyRig.ShapePositionOf), so the guard sits lower than it did, at chin height:
        /// with the old height the fists covered the eyes of anyone looking at the fighter from the front.
        /// </summary>
        public static Vector3 GuardOffset(bool rightHand)
        {
            return rightHand ? new Vector3(-0.16f, 0.10f, 0.52f) : new Vector3(0.16f, 0.10f, 0.64f);
        }

        // The key times of a blow, as fractions of it: the end of the anticipation, the contact (full reach), the end of the hold on contact.
        private static void Phases(FistKind kind, out float anticipationEnd, out float contact, out float holdEnd)
        {
            contact = ContactFraction(kind);
            switch (kind)
            {
                case FistKind.Jab: anticipationEnd = 0.12f; holdEnd = 0.46f; break;
                case FistKind.Cross: anticipationEnd = 0.20f; holdEnd = 0.52f; break;
                case FistKind.Hook: anticipationEnd = 0.22f; holdEnd = 0.54f; break;
                case FistKind.Uppercut: anticipationEnd = 0.22f; holdEnd = 0.54f; break;
                default: anticipationEnd = 0.42f; holdEnd = 0.70f; break;
            }
        }

        /// <summary>Right-handed local offset (x = to the body's right) for a hand of the given side: x is mirrored for the left hand.</summary>
        private static Vector3 Side(bool rightHand, float inward, float y, float z)
        {
            // "inward" is toward the middle of the body: negative x for the right hand, positive x for the left.
            return new Vector3(rightHand ? -inward : inward, y, z);
        }

        // The windup and peak of each blow (inward, up, forward) for the hand that throws it; arcs add their own control.
        private static void Keys(FistKind kind, out Vector3 windup, out Vector3 control, out Vector3 peak, out float twist, out float windupTwist, out float lean, out float windupLean, out float elbowOut)
        {
            bool right = UsesRightHand(kind);
            switch (kind)
            {
                case FistKind.Jab:
                    windup = Side(right, 0.22f, 0.26f, 0.42f);
                    control = Side(right, 0.28f, 0.22f, 0.70f);
                    peak = Side(right, 0.20f, 0.13f, 1.02f);
                    twist = 12.0f; windupTwist = 4.0f; lean = 7.0f; windupLean = -2.0f; elbowOut = 0.0f;
                    break;
                case FistKind.Cross:
                    windup = Side(right, 0.40f, 0.22f, 0.12f);
                    control = Side(right, 0.30f, 0.20f, 0.60f);
                    peak = Side(right, 0.16f, 0.12f, 1.04f);
                    twist = 34.0f; windupTwist = 12.0f; lean = 11.0f; windupLean = -3.0f; elbowOut = 0.0f;
                    break;
                case FistKind.Hook:
                    windup = Side(right, -0.80f, 0.20f, 0.10f);   // wide out to the side, a little back
                    control = Side(right, -0.30f, 0.16f, 0.82f);
                    peak = Side(right, 0.52f, 0.12f, 0.70f);       // across the front, elbow bent
                    twist = 40.0f; windupTwist = 16.0f; lean = 7.0f; windupLean = -2.0f; elbowOut = 1.0f;
                    break;
                case FistKind.Uppercut:
                    windup = Side(right, 0.20f, -0.62f, 0.34f);    // dropped low
                    control = Side(right, 0.14f, -0.05f, 0.52f);
                    peak = Side(right, 0.12f, 0.64f, 0.66f);       // driven up under the chin
                    twist = 22.0f; windupTwist = 8.0f; lean = -6.0f; windupLean = 12.0f; elbowOut = 0.35f;
                    break;
                default:   // the haymaker: back and high, over the top
                    windup = Side(right, -0.30f, 0.72f, -0.36f);
                    control = Side(right, 0.05f, 0.95f, 0.50f);
                    peak = Side(right, 0.14f, -0.06f, 1.06f);
                    twist = 46.0f; windupTwist = 30.0f; lean = 16.0f; windupLean = -12.0f; elbowOut = 0.6f;
                    break;
            }
        }

        private static float EaseInOut(float u) => u * u * (3.0f - 2.0f * u);

        /// <summary>Accelerates into the target: slow off the mark, fastest just before it arrives.</summary>
        private static float EaseIn(float u) => u * u * (1.6f - 0.6f * u);

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u)
        {
            float v = 1.0f - u;
            return v * v * a + 2.0f * v * u * c + u * u * b;
        }

        /// <summary>
        /// The pose at <paramref name="t01"/> (0 to 1) through a blow of the given kind. <paramref name="guard"/> is how much the OTHER arm (and
        /// both arms outside a blow) is held in the guard stance.
        /// </summary>
        public static FistFrame Evaluate(FistKind kind, float t01, float guard)
        {
            t01 = Mathf.Clamp01(t01);
            Phases(kind, out float anticipationEnd, out float contact, out float holdEnd);
            Keys(kind, out Vector3 windup, out Vector3 control, out Vector3 peak, out float twist, out float windupTwist,
                out float lean, out float windupLean, out float elbowOut);
            bool right = UsesRightHand(kind);
            Vector3 guardPose = GuardOffset(right);

            Vector3 hand;
            float twistNow;
            float leanNow;
            float extension;
            if (t01 < anticipationEnd)
            {
                float u = EaseInOut(t01 / anticipationEnd);
                hand = Vector3.Lerp(guardPose, windup, u);
                twistNow = -windupTwist * u;     // wound up AGAINST the blow
                leanNow = windupLean * u;
                extension = 0.0f;
            }
            else if (t01 < contact)
            {
                float u = EaseIn((t01 - anticipationEnd) / (contact - anticipationEnd));
                hand = Bezier(windup, control, peak, u);
                twistNow = Mathf.Lerp(-windupTwist, twist, u);
                leanNow = Mathf.Lerp(windupLean, lean, u);
                extension = u;
            }
            else if (t01 < holdEnd)
            {
                float u = (t01 - contact) / (holdEnd - contact);
                hand = peak + (peak - control).normalized * (0.03f * (1.0f - u));   // a hair of follow-through, settling
                twistNow = twist;
                leanNow = lean;
                extension = 1.0f;
            }
            else
            {
                float u = EaseInOut((t01 - holdEnd) / (1.0f - holdEnd));
                hand = Vector3.Lerp(peak, guardPose, u);
                twistNow = Mathf.Lerp(twist, 0.0f, u);
                leanNow = Mathf.Lerp(lean, 0.0f, u);
                extension = 1.0f - u;
            }

            // A right-handed blow turns the torso to the LEFT (negative yaw); a left-handed one to the right.
            float sign = right ? -1.0f : 1.0f;
            var frame = new FistFrame
            {
                Active = true,
                TwistDegrees = sign * twistNow,
                LeanDegrees = leanNow,
                Extension = extension,
            };

            float guardWeight = Mathf.Clamp01(guard);
            Vector3 otherGuard = GuardOffset(!right);
            if (right)
            {
                frame.RightWeight = 1.0f;
                frame.RightHand = hand;
                frame.RightElbowOut = elbowOut;
                frame.LeftWeight = Mathf.Max(guardWeight, 0.85f);
                frame.LeftHand = otherGuard;
            }
            else
            {
                frame.LeftWeight = 1.0f;
                frame.LeftHand = hand;
                frame.LeftElbowOut = elbowOut;
                frame.RightWeight = Mathf.Max(guardWeight, 0.85f);
                frame.RightHand = otherGuard;
            }
            return frame;
        }

        /// <summary>Just the guard stance, held at the given strength (0 = the animation's own arms).</summary>
        public static FistFrame Guard(float weight, float twistDegrees = 0.0f)
        {
            weight = Mathf.Clamp01(weight);
            return new FistFrame
            {
                Active = weight > 0.001f,
                RightWeight = weight,
                LeftWeight = weight,
                RightHand = GuardOffset(true),
                LeftHand = GuardOffset(false),
                TwistDegrees = twistDegrees,
            };
        }
    }
}
