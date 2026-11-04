using UnityEngine;

namespace CityX.Character
{
    public enum FistKind
    {
        RightStraight,
        LeftStraight,
        Overhand,
        Uppercut,
    }

    /// <summary>
    /// What the arms, torso and stance are doing at one moment of an unarmed strike, in terms that do not depend on the rig: where each fist is relative
    /// to ITS shoulder (in the body's own axes, in units of the arm's length), how strongly to hold it there, how far the torso is turned and leaning, how
    /// far the hips sink, and which foot the stance has forward.
    /// </summary>
    public struct FistFrame
    {
        /// <summary>Anything to apply at all.</summary>
        public bool Active;

        /// <summary>Each arm: 0 = the animation's own arm, 1 = the IK target below.</summary>
        public float RightWeight, LeftWeight;

        /// <summary>Right-handed offsets from the shoulder: x to the body's right, y up, z forward, in arm lengths.</summary>
        public Vector3 RightHand, LeftHand;

        /// <summary>How much the elbow is pushed out to the side rather than down (0 straight punches, 1 a swing across).</summary>
        public float RightElbowOut, LeftElbowOut;

        /// <summary>The torso's turn about the vertical, degrees, positive to the body's right.</summary>
        public float TwistDegrees;

        /// <summary>The torso's lean forward, degrees (negative leans back).</summary>
        public float LeanDegrees;

        /// <summary>How far the hips are sunk, metres (the legs bend: the stance's crouch, the lean into a blow).</summary>
        public float HipDrop;

        /// <summary>0 to 1: how much the feet stand in the stance and step to it (SwordFootwork) rather than follow the walk clip.</summary>
        public float Footwork;

        /// <summary>The stance: which foot is forward (-1 left, 1 right, 0 square) and how far apart front to back, leg lengths.</summary>
        public float StanceLead, StanceDepth;

        /// <summary>0 to 1: how far through the blow's FORWARD reach it is (for effects).</summary>
        public float Extension;
    }

    /// <summary>
    /// PROCEDURAL FIST STRIKES, a brawler's, not a boxer's (owner, 2026-11-03: "make the fists animations like dark souls unarmed punching"; the research
    /// in docs/MELEE_COMBOS.md). What Souls' bare hands look like, and what this is built on:
    ///
    ///  * NO GUARD. The fighter stands as he always stands, his arms the walk cycle's own, a little crouched (the READY stance); the fists do not come up to the
    ///    face between blows. The first pass's boxing guard went with the owner's ask.
    ///  * the lights are ALTERNATING STRAIGHT PUNCHES, right then left, each a committed lunge: the fist drawn back to the hip, a step forward with the
    ///    other foot as it goes out, the torso leaning well into it, the arm straight at full reach at the contact, a hold there, and a slow pull back
    ///    down to the side. The two look alike mirrored; that is the point.
    ///  * the heavy is the OVERHAND: the fist wound back and high over the shoulder for a long moment (the tell), then brought down and across with
    ///    the whole body; it knocks the target down. Held again in its window: the UPPERCUT, from a dip, driven up under the chin.
    ///  * WEIGHT AND FEET carry it: the stance's feet are planted and one steps (SwordFootwork, as the sword's), the hips sink into the lean.
    ///  * the arm reaches FULL EXTENSION at the moment of contact (the hand goes where the blow lands), after a short anticipation and a fast extension
    ///    that accelerates into the target.
    ///
    /// Pure data and maths (no scene), so the paths can be tested: contact is at <see cref="ContactFraction"/> of the swing, the fist is at full reach
    /// then, and the arm starts and ends the walk cycle's.
    /// </summary>
    public static class FistStrikes
    {
        public const string RightStraightName = "punch_right";
        public const string LeftStraightName = "punch_left";
        public const string OverhandName = "punch_heavy";
        public const string UppercutName = "punch_uppercut";

        public static bool TryGet(string animation, out FistKind kind)
        {
            switch (animation)
            {
                case RightStraightName: kind = FistKind.RightStraight; return true;
                case LeftStraightName: kind = FistKind.LeftStraight; return true;
                case OverhandName: kind = FistKind.Overhand; return true;
                case UppercutName: kind = FistKind.Uppercut; return true;
                default: kind = FistKind.RightStraight; return false;
            }
        }

        public static bool Has(string animation) => TryGet(animation, out _);

        public static string NameOf(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.RightStraight: return RightStraightName;
                case FistKind.LeftStraight: return LeftStraightName;
                case FistKind.Overhand: return OverhandName;
                default: return UppercutName;
            }
        }

        /// <summary>The right hand throws everything but the left straight.</summary>
        public static bool UsesRightHand(FistKind kind) => kind != FistKind.LeftStraight;

        /// <summary>How long the whole blow takes, seconds: anticipation, strike, hold and recovery. Each is a commitment, not a snap.</summary>
        public static float Seconds(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.RightStraight: return 0.50f;
                case FistKind.LeftStraight: return 0.50f;
                case FistKind.Overhand: return 0.80f;
                default: return 0.60f;
            }
        }

        /// <summary>Where in the blow it LANDS, as a fraction of its length (the fist is at full reach then).</summary>
        public static float ContactFraction(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.RightStraight: return 0.46f;
                case FistKind.LeftStraight: return 0.46f;
                case FistKind.Overhand: return 0.60f;
                default: return 0.50f;
            }
        }

        /// <summary>The share of walking pace the body keeps while throwing it (the legs go on under the blow).</summary>
        public static float MoveFraction(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.RightStraight: return 0.5f;
                case FistKind.LeftStraight: return 0.5f;
                case FistKind.Overhand: return 0.3f;
                default: return 0.35f;
            }
        }

        /// <summary>How hard the body steps INTO the blow, as a share of walking pace, while its fist is going out (zero after the contact).</summary>
        public static float LungeFraction(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.RightStraight: return 0.9f;
                case FistKind.LeftStraight: return 0.9f;
                case FistKind.Overhand: return 1.0f;
                default: return 0.7f;
            }
        }

        /// <summary>The hips sink this far in the ready stance, metres: the crouch that lets the feet stand apart (PlayerLocomotion.FootReach).</summary>
        public const float ReadyHipDrop = 0.07f;

        /// <summary>The feet stand apart front to back by this many leg lengths when a punch has one foot forward.</summary>
        public const float StanceDepth = 0.5f;

        /// <summary>
        /// Where the fist is drawn to as a blow recovers, in arm lengths from its shoulder, while the arm is handed back to the walk cycle: in front of the
        /// belly with the elbow bent, so the arm comes back as a pull and does not swing down straight like a lever.
        /// </summary>
        private static Vector3 PullBackOffset(bool rightHand)
        {
            return Side(rightHand, 0.0f, -0.62f, 0.34f);
        }

        // The key times of a blow, as fractions of it: the end of the anticipation, the contact (full reach), the end of the hold on contact.
        private static void Phases(FistKind kind, out float anticipationEnd, out float contact, out float holdEnd)
        {
            contact = ContactFraction(kind);
            switch (kind)
            {
                case FistKind.RightStraight: anticipationEnd = 0.22f; holdEnd = 0.60f; break;
                case FistKind.LeftStraight: anticipationEnd = 0.22f; holdEnd = 0.60f; break;
                case FistKind.Overhand: anticipationEnd = 0.45f; holdEnd = 0.72f; break;   // the long wind-up is the tell
                default: anticipationEnd = 0.26f; holdEnd = 0.64f; break;
            }
        }

        /// <summary>Right-handed local offset (x = to the body's right) for a hand of the given side: x is mirrored for the left hand.</summary>
        private static Vector3 Side(bool rightHand, float inward, float y, float z)
        {
            // "inward" is toward the middle of the body: negative x for the right hand, positive x for the left.
            return new Vector3(rightHand ? -inward : inward, y, z);
        }

        /// <summary>The windup and peak of each blow (inward, up, forward) for the hand that throws it, the torso's turn and lean, and how far the hips sink.</summary>
        private static void Keys(FistKind kind, out Vector3 windup, out Vector3 control, out Vector3 peak, out float twist, out float windupTwist,
            out float lean, out float windupLean, out float elbowOut, out float hipDrop, out float windupHipDrop)
        {
            bool right = UsesRightHand(kind);
            switch (kind)
            {
                case FistKind.RightStraight:
                case FistKind.LeftStraight:
                    windup = Side(right, 0.10f, -0.42f, -0.14f);   // drawn back to the hip, the elbow behind the body
                    control = Side(right, 0.14f, -0.10f, 0.56f);
                    peak = Side(right, 0.18f, 0.04f, 1.02f);       // straight out at chest height, the arm locked
                    twist = 38.0f; windupTwist = 14.0f; lean = 16.0f; windupLean = -4.0f; elbowOut = 0.1f;
                    hipDrop = 0.11f; windupHipDrop = 0.08f;
                    break;
                case FistKind.Overhand:
                    windup = Side(right, -0.14f, 0.84f, -0.30f);   // wound back and high over the shoulder
                    control = Side(right, 0.02f, 0.92f, 0.52f);
                    peak = Side(right, 0.30f, -0.40f, 0.86f);      // brought down and across, landing below the shoulder
                    twist = 44.0f; windupTwist = 22.0f; lean = 24.0f; windupLean = -14.0f; elbowOut = 0.5f;
                    hipDrop = 0.15f; windupHipDrop = 0.05f;
                    break;
                default:   // the uppercut: from a dip, driven up under the chin
                    windup = Side(right, 0.18f, -0.68f, 0.30f);
                    control = Side(right, 0.14f, -0.10f, 0.56f);
                    peak = Side(right, 0.12f, 0.30f, 0.62f);       // up to the chin (a quarter arm above the shoulder), the elbow bent
                    twist = 24.0f; windupTwist = 8.0f; lean = -6.0f; windupLean = 14.0f; elbowOut = 0.35f;
                    hipDrop = 0.04f; windupHipDrop = 0.17f;
                    break;
            }
        }

        /// <summary>Which foot the stance has forward for a blow: the OTHER foot from the fist (the hips turn into it over the front foot).</summary>
        public static float StanceLeadOf(FistKind kind) => UsesRightHand(kind) ? -1.0f : 1.0f;

        private static float EaseInOut(float u) => u * u * (3.0f - 2.0f * u);

        /// <summary>Accelerates into the target: slow off the mark, fastest just before it arrives.</summary>
        private static float EaseIn(float u) => u * u * (1.6f - 0.6f * u);

        /// <summary>Fast away, settling slowly: the pull back down to the side.</summary>
        private static float EaseOut(float u) => 1.0f - (1.0f - u) * (1.0f - u);

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u)
        {
            float v = 1.0f - u;
            return v * v * a + 2.0f * v * u * c + u * u * b;
        }

        /// <summary>
        /// The pose at <paramref name="t01"/> (0 to 1) through a blow of the given kind. Only the throwing arm is posed, and only while it throws: it is
        /// taken from the walk cycle over the wind-up and given back over the pull-back. The other arm is never touched.
        /// </summary>
        public static FistFrame Evaluate(FistKind kind, float t01)
        {
            t01 = Mathf.Clamp01(t01);
            Phases(kind, out float anticipationEnd, out float contact, out float holdEnd);
            Keys(kind, out Vector3 windup, out Vector3 control, out Vector3 peak, out float twist, out float windupTwist,
                out float lean, out float windupLean, out float elbowOut, out float hipDrop, out float windupHipDrop);
            bool right = UsesRightHand(kind);

            Vector3 hand;
            float armWeight = 1.0f;
            float twistNow;
            float leanNow;
            float dropNow;
            float extension;
            if (t01 < anticipationEnd)
            {
                float u = EaseInOut(t01 / anticipationEnd);
                hand = windup;
                armWeight = u;
                twistNow = -windupTwist * u;     // wound up AGAINST the blow
                leanNow = windupLean * u;
                dropNow = Mathf.Lerp(ReadyHipDrop, windupHipDrop, u);
                extension = 0.0f;
            }
            else if (t01 < contact)
            {
                float u = EaseIn((t01 - anticipationEnd) / (contact - anticipationEnd));
                hand = Bezier(windup, control, peak, u);
                twistNow = Mathf.Lerp(-windupTwist, twist, u);
                leanNow = Mathf.Lerp(windupLean, lean, u);
                dropNow = Mathf.Lerp(windupHipDrop, hipDrop, u);
                extension = u;
            }
            else if (t01 < holdEnd)
            {
                float u = (t01 - contact) / (holdEnd - contact);
                hand = peak + (peak - control).normalized * (0.03f * (1.0f - u));   // a hair of follow-through, settling
                twistNow = twist;
                leanNow = lean;
                dropNow = hipDrop;
                extension = 1.0f;
            }
            else
            {
                float through = (t01 - holdEnd) / (1.0f - holdEnd);
                float u = EaseOut(through);
                hand = Vector3.Lerp(peak, PullBackOffset(right), u);
                armWeight = 1.0f - EaseInOut(through);
                twistNow = Mathf.Lerp(twist, 0.0f, u);
                leanNow = Mathf.Lerp(lean, 0.0f, u);
                dropNow = Mathf.Lerp(hipDrop, ReadyHipDrop, u);
                extension = 1.0f - u;
            }

            // A right-handed blow turns the torso to the LEFT (negative yaw); a left-handed one to the right.
            float sign = right ? -1.0f : 1.0f;
            var frame = new FistFrame
            {
                Active = true,
                TwistDegrees = sign * twistNow,
                LeanDegrees = leanNow,
                HipDrop = dropNow,
                Footwork = 1.0f,
                StanceLead = StanceLeadOf(kind),
                StanceDepth = StanceDepth,
                Extension = extension,
            };

            if (right)
            {
                frame.RightWeight = armWeight;
                frame.RightHand = hand;
                frame.RightElbowOut = elbowOut;
            }
            else
            {
                frame.LeftWeight = armWeight;
                frame.LeftHand = hand;
                frame.LeftElbowOut = elbowOut;
            }
            return frame;
        }

        /// <summary>
        /// The ready stance between blows: a little crouched with the feet square. The arms are not posed at all, they stay the walk cycle's: placing the
        /// hands here put them on the hips, elbows out, which is the pose the owner rejected on 2026-11-03.
        /// </summary>
        public static FistFrame Ready()
        {
            return new FistFrame
            {
                Active = true,
                HipDrop = ReadyHipDrop,
                Footwork = 1.0f,
                StanceLead = 0.0f,
                StanceDepth = 0.0f,
            };
        }

        /// <summary>
        /// A pose part way from <paramref name="from"/> to <paramref name="to"/> (<paramref name="share"/> 0 to 1): the next punch taking over from one still
        /// recovering, whose arm and torso would otherwise snap. The stance is <paramref name="to"/>'s at once: the feet step to it in their own time.
        /// </summary>
        public static FistFrame Blend(in FistFrame from, in FistFrame to, float share)
        {
            share = Mathf.Clamp01(share);
            FistFrame frame = to;
            frame.TwistDegrees = Mathf.Lerp(from.TwistDegrees, to.TwistDegrees, share);
            frame.LeanDegrees = Mathf.Lerp(from.LeanDegrees, to.LeanDegrees, share);
            frame.HipDrop = Mathf.Lerp(from.HipDrop, to.HipDrop, share);
            BlendArm(from.RightWeight, from.RightHand, from.RightElbowOut, to.RightWeight, to.RightHand, to.RightElbowOut, share,
                out frame.RightWeight, out frame.RightHand, out frame.RightElbowOut);
            BlendArm(from.LeftWeight, from.LeftHand, from.LeftElbowOut, to.LeftWeight, to.LeftHand, to.LeftElbowOut, share,
                out frame.LeftWeight, out frame.LeftHand, out frame.LeftElbowOut);
            return frame;
        }

        private static void BlendArm(float fromWeight, Vector3 fromHand, float fromElbow, float toWeight, Vector3 toHand, float toElbow, float share,
            out float weight, out Vector3 hand, out float elbow)
        {
            weight = Mathf.Lerp(fromWeight, toWeight, share);
            // An arm one pose does not use has no place in that pose: it keeps the other pose's, or the hand would be pulled to the shoulder as it fades.
            float place = fromWeight <= 0.0f ? 1.0f : toWeight <= 0.0f ? 0.0f : share;
            hand = Vector3.Lerp(fromHand, toHand, place);
            elbow = Mathf.Lerp(fromElbow, toElbow, place);
        }
    }
}
