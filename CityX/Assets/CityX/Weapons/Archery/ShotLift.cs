using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A WEAK SHOT AIMED UP ONTO THE CROSSHAIR (owner, 2026-11-16, of the slingshot: "when you hold click it does a good job of aiming at your
    /// crosshair but is there a way we can make it better at going towards the crosshair during less charges shots"). A drawn shot flies flat
    /// for its range, then falls on along the same line (ArrowProjectile); a light pull's flat range is short, so what the crosshair was on past
    /// it was undershot. This tilts the shot up just enough that it comes down on that point, as a thrower lobs, within what the pull can reach;
    /// out of reach the shot goes as aimed.
    /// </summary>
    public static class ShotLift
    {
        /// <summary>How far down the crosshair's line the point to land on is looked for, metres.</summary>
        public const float LookReach = 60.0f;

        /// <summary>The most the shot is tilted up, degrees (and down, for a point the straight line would pass over).</summary>
        public const float MostLiftDegrees = 35.0f;

        public const float MostDropDegrees = 10.0f;

        private const float ScanDegrees = 1.0f;
        private const int Steps = 16;

        /// <summary>
        /// Which way to loose from <paramref name="from"/> so that, flat for <paramref name="flatRange"/> and then falling on along the same line
        /// at <paramref name="fallSpeed"/>, the shot comes down on <paramref name="target"/>: the least tilt from <paramref name="direction"/>
        /// that gets there. False when none within the bounds does.
        /// </summary>
        public static bool Solve(Vector3 from, Vector3 direction, float flatRange, float fallSpeed, Vector3 target, out Vector3 lifted)
        {
            lifted = direction;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude < 1e-6f || fallSpeed <= 0.0f)
            {
                return false;
            }
            right.Normalize();
            if (!Miss(from, direction, right, -MostDropDegrees, flatRange, fallSpeed, target, out float first) || first > 0.0f)
            {
                return false;
            }
            // Scanned up for the first tilt that no longer falls short (the low lob, not the high one), then narrowed down.
            float low = -MostDropDegrees, high = float.NaN;
            for (float degrees = low + ScanDegrees; degrees <= MostLiftDegrees + 1e-3f; degrees += ScanDegrees)
            {
                if (Miss(from, direction, right, degrees, flatRange, fallSpeed, target, out float miss) && miss >= 0.0f)
                {
                    high = degrees;
                    break;
                }
                low = degrees;
            }
            if (float.IsNaN(high))
            {
                return false;
            }
            for (int i = 0; i < Steps; i++)
            {
                float middle = 0.5f * (low + high);
                if (Miss(from, direction, right, middle, flatRange, fallSpeed, target, out float miss) && miss >= 0.0f)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }
            lifted = Tilted(direction, right, high);
            return true;
        }

        private static Vector3 Tilted(Vector3 direction, Vector3 right, float degrees) => Quaternion.AngleAxis(-degrees, right) * direction;

        /// <summary>How far above <paramref name="target"/> the shot tilted <paramref name="degrees"/> up passes over it (below: negative).</summary>
        private static bool Miss(Vector3 from, Vector3 direction, Vector3 right, float degrees, float flatRange, float fallSpeed, Vector3 target,
            out float miss)
        {
            miss = 0.0f;
            Vector3 way = Tilted(direction, right, degrees);
            Vector3 flatEnd = from + way * flatRange;
            Vector3 velocity = way * fallSpeed;
            Vector3 across = new Vector3(velocity.x, 0.0f, velocity.z);
            Vector3 toTarget = new Vector3(target.x - flatEnd.x, 0.0f, target.z - flatEnd.z);
            if (across.sqrMagnitude < 1e-6f || Vector3.Dot(across, toTarget) <= 0.0f)
            {
                return false;
            }
            float seconds = toTarget.magnitude / across.magnitude;
            miss = flatEnd.y + velocity.y * seconds - 0.5f * ArcheryRules.Gravity * seconds * seconds - target.y;
            return true;
        }
    }
}
