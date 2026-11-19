using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE RPG'S ROCKET IN FLIGHT AND WHAT IT DOES (docs/RPG.md). After the RPG-7 (Wikipedia: the booster throws the round out at 115 m/s, the
    /// rocket motor lights about 10 m out and drives it to 295 m/s): here the round leaves slower than a bullet so it can be seen go, the motor
    /// lights a few metres out with a kick of flame and smoke and pushes it on to a top speed still slow enough to follow with the eye, it drops
    /// only a little, and it bursts on whatever it meets (the grenade's blast, Explosion), or of itself at the end of its range as the real
    /// fuze does at about 900 m. A body it strikes square takes a blow of its own as well. Behind the launcher the back-blast hurts and throws
    /// anyone standing close (the real danger area: 30 m, a 70-degree cone, the worst of it close in). The numbers are mine; game speeds after
    /// Call of Duty's and Battlefield's RPG-7, whose rockets can be watched all the way.
    /// </summary>
    public static class RocketRules
    {
        /// <summary>The speed it leaves the muzzle at, m/s.</summary>
        public const float LaunchSpeed = 38.0f;

        /// <summary>When the motor lights, seconds after the launch (a few metres out).</summary>
        public const float IgniteSeconds = 0.18f;

        /// <summary>How fast the motor speeds it up, m/s per second, until it reaches <see cref="TopSpeed"/>.</summary>
        public const float BoostAcceleration = 140.0f;

        public const float TopSpeed = 85.0f;

        /// <summary>The share of gravity it falls with: the fins and the motor keep it nearly straight.</summary>
        public const float GravityShare = 0.12f;

        /// <summary>How far it flies before it bursts of itself, metres.</summary>
        public const float Range = 260.0f;

        /// <summary>A body struck square: a blow of its own on top of the blast.</summary>
        public const float DirectHitDamage = 70.0f;

        /// <summary>The round's girth for what it strikes, metres (the warhead's).</summary>
        public const float Radius = 0.09f;

        /// <summary>The back-blast: how far behind the launcher it reaches, the half-angle of its cone, the most it hurts and how hard it throws.</summary>
        public const float BackblastMetres = 5.0f;

        public const float BackblastHalfAngleDegrees = 35.0f;

        public const float BackblastDamage = 35.0f;

        public const float BackblastThrowSpeed = 9.0f;

        /// <summary>The round's speed <paramref name="seconds"/> after the launch.</summary>
        public static float SpeedAt(float seconds) =>
            seconds < IgniteSeconds ? LaunchSpeed : Mathf.Min(TopSpeed, LaunchSpeed + BoostAcceleration * (seconds - IgniteSeconds));

        /// <summary>
        /// One step of flight: on along its way at its speed for the time, the little fall bending that way down step by step, as a rocket's
        /// nose follows its path.
        /// </summary>
        public static void Step(ref Vector3 position, ref Vector3 velocity, float secondsSinceLaunch, float deltaTime)
        {
            Vector3 heading = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            velocity = heading * SpeedAt(secondsSinceLaunch + deltaTime) + Physics.gravity * (GravityShare * deltaTime);
            position += velocity * deltaTime;
        }

        /// <summary>
        /// How much of the back-blast reaches a point <paramref name="behind"/> from the launcher's rear, the launcher pointing <paramref name="forward"/>:
        /// all of it at the rear, none past <see cref="BackblastMetres"/> or outside the cone behind.
        /// </summary>
        public static float BackblastShare(Vector3 behind, Vector3 forward)
        {
            float distance = behind.magnitude;
            if (distance > BackblastMetres)
            {
                return 0.0f;
            }
            if (distance > 0.3f && Vector3.Angle(behind, -forward) > BackblastHalfAngleDegrees)
            {
                return 0.0f;
            }
            return 1.0f - distance / BackblastMetres;
        }
    }
}
