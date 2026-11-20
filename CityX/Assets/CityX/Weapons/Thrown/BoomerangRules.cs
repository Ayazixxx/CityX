namespace CityX.Weapons
{
    /// <summary>
    /// The numbers of the Wooden Boomerang (docs/BOOMERANG.md; owner, 2026-11-05: "make a boomerang weapon that functions exactly like the terraria
    /// boomerang but in 3d"). They are Terraria's own, its tiles and ticks turned into metres and seconds: a Terraria player stands 3 tiles tall
    /// and ours 2.6 m, so a tile is about 0.87 m.
    /// </summary>
    public static class BoomerangRules
    {
        /// <summary>Terraria's 10, of a player's 100 there as here.</summary>
        public const float Damage = 10.0f;

        /// <summary>Velocity 6.5 pixels a tick was 20 m/s; 2 more (owner, 2026-11-20, after trying them faster: "increase them all by +2").</summary>
        public const float OutSpeed = 22.0f;

        /// <summary>It flies straight this long, then turns for home whatever it has or has not met.</summary>
        public const float OutSeconds = 0.5f;

        /// <summary>How hard it steers for home once it has turned, m/s per second: enough to end a throw at about <see cref="Range"/>.</summary>
        public const float TurnAcceleration = 100.0f;

        /// <summary>Its top speed coming back: 46 mph (29 m/s), and 2 more (owner, 2026-11-20, after trying them faster: "increase them all by +2").</summary>
        public const float BackTopSpeed = 31.0f;

        /// <summary>How far a throw goes when nothing stops it: 13.4 m (Terraria's 13.6 tiles, 12 m, until it was made faster).</summary>
        public const float Range = OutSpeed * OutSeconds + OutSpeed * OutSpeed / (2.0f * TurnAcceleration);

        /// <summary>It is caught this near the thrower's chest.</summary>
        public const float CatchMetres = 1.2f;

        /// <summary>Use time 20 ticks: a throw this soon after the last one is refused, even if it is already back.</summary>
        public const float ThrowEverySeconds = 1.0f / 3.0f;

        /// <summary>The same body cannot be hit again by one throw for this long, so one pass is one hit.</summary>
        public const float HitAgainSeconds = 0.33f;

        /// <summary>Its reach to either side of its path, metres: about half its span.</summary>
        public const float HitRadius = 0.3f;

        /// <summary>Gone after a minute, or this far from the thrower (187.5 tiles), if it never gets home.</summary>
        public const float LifeSeconds = 60.0f;

        public const float LostMetres = 163.0f;

        /// <summary>About four turns a second.</summary>
        public const float SpinDegreesPerSecond = 1375.0f;
    }
}
