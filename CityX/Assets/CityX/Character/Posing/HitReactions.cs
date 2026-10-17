using UnityEngine;

namespace CityX.Character
{
    /// <summary>Which way a blow came from, in the body's own terms.</summary>
    public enum HitDirection
    {
        Front,
        Back,
        Left,
        Right,
    }

    /// <summary>The torso and head's recoil from a blow at one moment: leans and tilts in degrees, body axes.</summary>
    public struct FlinchFrame
    {
        public bool Active;

        /// <summary>0 = nothing, 1 = this recoil in full.</summary>
        public float Weight;

        /// <summary>The torso's lean, degrees, positive forward (a blow from behind), negative back (a blow from the front).</summary>
        public float LeanDegrees;

        /// <summary>The torso's sideways tip, degrees, positive toward the body's right (a blow from the left).</summary>
        public float TiltRightDegrees;

        /// <summary>The head's own extra snap the same way, degrees (it whips further than the torso).</summary>
        public float HeadDegrees;
    }

    /// <summary>
    /// WHAT BEING HIT LOOKS LIKE. Nothing on the body reacted to a blow before: the hitstun took the controls away and the body stood there.
    /// A flinch is a quick recoil away from the blow -- the torso and head thrown the other way in a few frames, held an instant (the victim's
    /// own hit-stop), then recovering -- and a STAGGER (a blow that broke the poise, or any blow outside an attack's hyper-armour) is the same
    /// thing bigger and longer. Four directions, so a hit from behind folds the body forward and one from the side tips it over.
    ///
    /// Played by name through PlayerAnimationDriver like a punch or a sword cut, so another player's copy flinches the same way. Pure data.
    /// </summary>
    public static class HitReactions
    {
        private static readonly string[] FlinchNames = { "hit_front", "hit_back", "hit_left", "hit_right" };
        private static readonly string[] StaggerNames = { "stagger_front", "stagger_back", "stagger_left", "stagger_right" };

        public static string NameFor(HitDirection direction, bool stagger)
        {
            return (stagger ? StaggerNames : FlinchNames)[(int)direction];
        }

        public static bool TryGet(string animation, out HitDirection direction, out bool stagger)
        {
            for (int i = 0; i < 4; i++)
            {
                if (animation == FlinchNames[i])
                {
                    direction = (HitDirection)i;
                    stagger = false;
                    return true;
                }
                if (animation == StaggerNames[i])
                {
                    direction = (HitDirection)i;
                    stagger = true;
                    return true;
                }
            }
            direction = HitDirection.Front;
            stagger = false;
            return false;
        }

        public static bool Has(string animation) => TryGet(animation, out _, out _);

        /// <summary>Which way a blow from <paramref name="fromLocal"/> (the attacker's position in the body's own space) came from.</summary>
        public static HitDirection DirectionOf(Vector3 fromLocal)
        {
            if (Mathf.Abs(fromLocal.z) >= Mathf.Abs(fromLocal.x))
            {
                return fromLocal.z >= 0.0f ? HitDirection.Front : HitDirection.Back;
            }
            return fromLocal.x >= 0.0f ? HitDirection.Right : HitDirection.Left;
        }

        /// <summary>How long the reaction lasts, seconds.</summary>
        public static float Seconds(bool stagger) => stagger ? 0.55f : 0.30f;

        /// <summary>How far the torso is thrown, degrees.</summary>
        public static float Amplitude(bool stagger) => stagger ? 26.0f : 14.0f;

        /// <summary>The head whips this many times further than the torso.</summary>
        public const float HeadShare = 1.6f;

        /// <summary>The recoil arrives over this share of the reaction, holds to <see cref="HoldEnd"/>, and recovers over the rest.</summary>
        public const float SnapEnd = 0.15f;

        public const float HoldEnd = 0.35f;

        /// <summary>The recoil at <paramref name="t01"/> (0 to 1) through the reaction.</summary>
        public static FlinchFrame Evaluate(HitDirection direction, bool stagger, float t01)
        {
            t01 = Mathf.Clamp01(t01);
            float amount;
            if (t01 < SnapEnd)
            {
                float u = t01 / SnapEnd;
                amount = 1.0f - (1.0f - u) * (1.0f - u);   // thrown: fast, decelerating into the hold
            }
            else if (t01 < HoldEnd)
            {
                amount = 1.0f;
            }
            else
            {
                float u = (t01 - HoldEnd) / (1.0f - HoldEnd);
                amount = 1.0f - u * u * (3.0f - 2.0f * u);   // recovering: smooth
            }
            float degrees = Amplitude(stagger) * amount;
            var frame = new FlinchFrame { Active = true, Weight = 1.0f, HeadDegrees = degrees * (HeadShare - 1.0f) };
            switch (direction)
            {
                case HitDirection.Front: frame.LeanDegrees = -degrees; break;
                case HitDirection.Back: frame.LeanDegrees = degrees; break;
                case HitDirection.Left: frame.TiltRightDegrees = degrees; break;
                default: frame.TiltRightDegrees = -degrees; break;
            }
            return frame;
        }
    }
}
