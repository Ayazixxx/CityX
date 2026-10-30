using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The frag grenade's numbers (docs/GRENADE.md). The fuse, the bounce and the blast are the Godot game's (weapons/thrown_grenade.gd,
    /// weapons/explosion.gd) with the reach scaled to this city's 1.3 times life size.
    /// </summary>
    public static class GrenadeRules
    {
        /// <summary>The fuse, seconds from the throw (the Godot game's 2.4: you can bank one round a corner, and it will not wait for you).</summary>
        public const float FuseSeconds = 2.4f;

        /// <summary>How hard it is thrown, metres a second, and how far up from the aim (a lob, not a fastball), degrees.</summary>
        public const float ThrowSpeed = 17.0f;

        public const float ThrowLiftDegrees = 8.0f;

        /// <summary>The grenade: its weight, kilograms, and how it bounces and rolls (bouncy enough to skip off a wall, not back at you).</summary>
        public const float Mass = 0.45f;

        public const float Bounce = 0.35f;
        public const float Friction = 0.8f;

        /// <summary>The blast's reach, metres, the damage at its middle (a kill there: 100 health, the Godot game's 90 lifted past it), and the share left at its rim.</summary>
        public const float Radius = 7.5f;

        public const float Damage = 110.0f;
        public const float EdgeShare = 0.25f;

        /// <summary>How hard it throws what it does not kill outright away and up, metres a second at its middle.</summary>
        public const float ThrowAwaySpeed = 11.0f;

        public const float ThrowUpSpeed = 6.0f;

        /// <summary>How far its push reaches past the damage, as a share of the reach (loose things, bottles, ragdolls).</summary>
        public const float PushReachShare = 1.4f;

        /// <summary>The fuse light's blink: slow at the throw, frantic at the end, a second.</summary>
        public const float BlinkSlowestHz = 2.0f;

        public const float BlinkFastestHz = 14.0f;

        /// <summary>How much of its damage a body <paramref name="distance"/> from the middle takes: all of it there, the edge share at the rim, none past it.</summary>
        public static float DamageShare(float distance)
        {
            if (distance > Radius)
            {
                return 0.0f;
            }
            return Mathf.Lerp(1.0f, EdgeShare, Mathf.Clamp01(distance / Radius));
        }

        /// <summary>How fast the fuse light blinks with <paramref name="left"/> of the fuse to burn, a second.</summary>
        public static float BlinkHz(float left)
        {
            float burned = 1.0f - Mathf.Clamp01(left / FuseSeconds);
            return Mathf.Lerp(BlinkSlowestHz, BlinkFastestHz, burned);
        }

        /// <summary>The throw's velocity along <paramref name="aim"/> (the camera's facing), lifted a little.</summary>
        public static Vector3 ThrowVelocity(Vector3 aim)
        {
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, aim);
            if (side.sqrMagnitude < 1e-6f)
            {
                return aim * ThrowSpeed;
            }
            return Quaternion.AngleAxis(-ThrowLiftDegrees, side.normalized) * aim * ThrowSpeed;
        }
    }
}
