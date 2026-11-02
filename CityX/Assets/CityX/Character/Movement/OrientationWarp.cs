using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ORIENTATION WARPING (docs/MOVEMENT.md; Unreal's Lyra does the same): moving at an angle to where the body faces, the Animator is shown only
    /// the nearest of the four straight ways (forward, a side-step, back), and the hips and legs are turned the rest of the way toward the real
    /// travel while the chest stays put. A diagonal blended from a forward walk and a side-step tangled the legs ("diagonal walking doesnt look
    /// right", owner, 2026-11-01). The shown way only changes once the travel is <see cref="SwitchDegrees"/> off it, so a diagonal does not
    /// flicker between two.
    /// </summary>
    public sealed class OrientationWarp
    {
        /// <summary>The shown way holds until the travel is this far off it, degrees (past the 45 between two ways, so it does not flicker).</summary>
        public const float SwitchDegrees = 55.0f;

        /// <summary>The hips turn at most this far, degrees.</summary>
        public const float MaxDegrees = 60.0f;

        /// <summary>Below this speed, metres a second, nothing is warped (standing, starting).</summary>
        public const float MinSpeed = 0.5f;

        /// <summary>How fast the warp follows, per second (an easing rate, not degrees).</summary>
        public const float FollowPerSecond = 12.0f;

        private int _way;

        /// <summary>The body's local travel (x across, y along) turned onto the nearest of the four ways, its speed kept.</summary>
        public Vector2 Shown(Vector2 local)
        {
            float speed = local.magnitude;
            if (speed < MinSpeed)
            {
                return local;
            }
            float angle = AngleOf(local);
            if (Mathf.Abs(Mathf.DeltaAngle(_way * 90.0f, angle)) > SwitchDegrees)
            {
                _way = ((Mathf.RoundToInt(angle / 90.0f) % 4) + 4) % 4;
            }
            float shown = _way * 90.0f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(shown), Mathf.Cos(shown)) * speed;
        }

        /// <summary>How far the hips turn, degrees, for a body shown walking <paramref name="shown"/> while it really travels <paramref name="travel"/>.</summary>
        public static float DegreesFor(Vector2 shown, Vector2 travel)
        {
            if (shown.magnitude < MinSpeed || travel.magnitude < MinSpeed)
            {
                return 0.0f;
            }
            return Mathf.Clamp(Mathf.DeltaAngle(AngleOf(shown), AngleOf(travel)), -MaxDegrees, MaxDegrees);
        }

        /// <summary>The warp eased from <paramref name="current"/> toward <paramref name="target"/>.</summary>
        public static float Follow(float current, float target, float deltaTime) =>
            Mathf.Lerp(current, target, 1.0f - Mathf.Exp(-FollowPerSecond * Mathf.Max(0.0f, deltaTime)));

        /// <summary>Degrees clockwise from forward, seen from above: right is 90, back 180.</summary>
        private static float AngleOf(Vector2 local) => Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
    }
}
