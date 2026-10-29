using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The giant's numbers (docs/GIANT.md): its size is the Grow spell's; everything about its leap and slam is in its own lengths.</summary>
    public static class GiantRules
    {
        /// <summary>The Grow spell's size (BodySize.GrowScale): ten times the player, 26 m tall.</summary>
        public const float Scale = CityX.Character.BodySize.GrowScale;

        /// <summary>Its health, before stats: a fight, not a speed bump (twelve times a player's).</summary>
        public const float MaxHealth = 1200.0f;

        /// <summary>The Max Health stat points that make it so: 10 health a point on top of a body's own 100.</summary>
        public const int MaxHealthPoints = (int)((MaxHealth - CityX.Character.PlayerVitals.BaseMaxHealth) / 10.0f);

        /// <summary>What killing it pays, XP.</summary>
        public const int XpReward = 400;

        /// <summary>How far it notices a player, metres.</summary>
        public const float NoticeMetres = 160.0f;

        /// <summary>It leaps when the player is this close, metres (about its own height), and stops walking closer than this.</summary>
        public const float LeapFromMetres = 26.0f;

        public const float StandOffMetres = 6.0f;

        /// <summary>The tell: how long it crouches before the leap, seconds. Long: a slow wind-up a player can see and run from.</summary>
        public const float CrouchSeconds = 1.1f;

        /// <summary>How hard it keeps walking at the target in the air (the leap carries it), and the most it may be in the air.</summary>
        public const float AirMoveShare = 0.6f;

        public const float MaxAirSeconds = 8.0f;

        /// <summary>How long it stands after a slam, and from one slam to the next, seconds.</summary>
        public const float RecoverSeconds = 1.4f;

        public const float SlamCooldownSeconds = 5.0f;

        /// <summary>The slam's reach round where it lands, metres, the damage at its middle (half at its rim), and the throw.</summary>
        public const float SlamRadius = 18.0f;

        public const float SlamDamage = 40.0f;
        public const float SlamThrowSpeed = 9.0f;
        public const float SlamThrowUpSpeed = 6.0f;

        /// <summary>How much of the slam's damage a body at <paramref name="distance"/> takes: all at the middle, half at the rim, none past it.</summary>
        public static float SlamShare(float distance)
        {
            if (distance > SlamRadius)
            {
                return 0.0f;
            }
            return Mathf.Lerp(1.0f, 0.5f, Mathf.Clamp01(distance / SlamRadius));
        }
    }
}
