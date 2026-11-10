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
    /// The stance the feet keep under an unarmed fight, between blows and under them: how far the hips sink and how the feet stand
    /// (PlayerLocomotion steps them to it with SwordFootwork, as the sword's). The blows themselves are clips (<see cref="FistStrikes.Motion"/>).
    /// </summary>
    public struct FistFrame
    {
        /// <summary>Anything to apply at all.</summary>
        public bool Active;

        /// <summary>How far the hips are sunk, metres (the legs bend: the stance's crouch).</summary>
        public float HipDrop;

        /// <summary>0 to 1: how much the feet stand in the stance and step to it (SwordFootwork) rather than follow the walk clip.</summary>
        public float Footwork;

        /// <summary>The stance: which foot is forward (-1 left, 1 right, 0 square) and how far apart front to back, leg lengths.</summary>
        public float StanceLead, StanceDepth;
    }

    /// <summary>
    /// THE UNARMED STRIKES, a brawler's, not a boxer's (owner, 2026-11-03: "make the fists animations like dark souls unarmed punching"; the
    /// research in docs/MELEE_COMBOS.md): alternating straight punches, right then left, each a committed lunge; the overhand on a hold, wound
    /// back high for a long moment (the tell) and brought down with the whole body; the uppercut after it, from a dip.
    ///
    /// Each is a FULL-BODY CLIP made in Blender (CityX/SourceArt/FistCombo, docs/FIST_COMBAT.md; owner, 2026-11-09: "improve upon the fist
    /// fighting (mostly the animations)", and of the blows posed in code on the arms alone, 2026-11-03: "there needs to be some type of full
    /// body animation/movement just the arms doesnt look right"): the stride, the weight going back and then over the front foot, the turn
    /// from the hips up, the fist at full reach on the frame it lands, the other arm working against it. FistPoseDriver plays them.
    ///
    /// What is here is what the game and the clips must agree on: each blow's length, the moment it lands, and how far and when the body is
    /// CARRIED into it (the clips' feet are planted on the ground the body is carried over: fist_pose.py's `carry`, keep the two in step).
    /// </summary>
    public static class FistStrikes
    {
        public const string RightStraightName = "punch_right";
        public const string LeftStraightName = "punch_left";
        public const string OverhandName = "punch_heavy";
        public const string UppercutName = "punch_uppercut";

        /// <summary>The clips, in Resources: one segment a blow, named as the blow is (<see cref="NameOf"/>).</summary>
        public const string MotionPath = "Fists/FistMotion";

        public static BakedMotion Motion => BakedMotion.Load(MotionPath);

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

        /// <summary>
        /// How far the body is carried into the blow, metres: the stride. The body is rooted otherwise (a blow is a commitment), and the
        /// clip's rear foot stays where it stood while the body goes this far past it.
        /// </summary>
        public static float CarryMetres(FistKind kind)
        {
            switch (kind)
            {
                case FistKind.RightStraight: return 0.45f;
                case FistKind.LeftStraight: return 0.45f;
                case FistKind.Overhand: return 0.50f;
                default: return 0.30f;
            }
        }

        /// <summary>Between which seconds of the blow the body is carried: from the lead foot leaving the ground to the blow landing.</summary>
        private static void CarryWindow(FistKind kind, out float start, out float end)
        {
            end = Seconds(kind) * ContactFraction(kind);
            switch (kind)
            {
                case FistKind.RightStraight: start = 0.05f; break;
                case FistKind.LeftStraight: start = 0.05f; break;
                case FistKind.Overhand: start = 0.30f; break;   // the long wind-up stands still: the tell
                default: start = 0.10f; break;
            }
        }

        /// <summary>How far the body has been carried into the blow <paramref name="seconds"/> into it, metres: eased off the mark and into the landing.</summary>
        public static float Carried(FistKind kind, float seconds)
        {
            CarryWindow(kind, out float start, out float end);
            return CarryMetres(kind) * Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(start, end, seconds));
        }

        /// <summary>The hips sink this far in the ready stance, metres: the crouch that lets the feet stand apart (PlayerLocomotion.FootReach).</summary>
        public const float ReadyHipDrop = 0.07f;

        /// <summary>
        /// The ready stance, between blows and under them: a little crouched with the feet square, where every clip starts and ends. The arms
        /// are not posed at all, they stay the walk cycle's: placing the hands here put them on the hips, elbows out, which is the pose the
        /// owner rejected on 2026-11-03.
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
    }
}
