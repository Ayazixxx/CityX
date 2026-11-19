namespace CityX.Weapons
{
    /// <summary>
    /// How one kind of boomerang flies and hits (docs/BOOMERANG.md): the Wooden Boomerang's numbers (<see cref="BoomerangRules"/>) or the Giant
    /// Boomerang's (<see cref="GiantBoomerangRules"/>).
    /// </summary>
    public readonly struct BoomerangProfile
    {
        public readonly float OutSpeed, OutSeconds, TurnAcceleration, BackTopSpeed, CatchMetres;

        /// <summary>Its reach to either side of its path for a body, and for a wall (what turns it back going out).</summary>
        public readonly float HitRadius, WallRadius;

        public readonly float HitAgainSeconds, SpinDegreesPerSecond;

        /// <summary>Going out it goes on through the people it may pass through (BoomerangBlows), and every hit throws the body down.</summary>
        public readonly bool Heavy;

        public BoomerangProfile(float outSpeed, float outSeconds, float turnAcceleration, float backTopSpeed, float catchMetres, float hitRadius, float wallRadius,
            float hitAgainSeconds, float spinDegreesPerSecond, bool heavy)
        {
            OutSpeed = outSpeed;
            OutSeconds = outSeconds;
            TurnAcceleration = turnAcceleration;
            BackTopSpeed = backTopSpeed;
            CatchMetres = catchMetres;
            HitRadius = hitRadius;
            WallRadius = wallRadius;
            HitAgainSeconds = hitAgainSeconds;
            SpinDegreesPerSecond = spinDegreesPerSecond;
            Heavy = heavy;
        }

        /// <summary>How far a throw goes when nothing stops it, metres.</summary>
        public float Range => OutSpeed * OutSeconds + OutSpeed * OutSpeed / (2.0f * TurnAcceleration);

        public static readonly BoomerangProfile Wooden = new BoomerangProfile(BoomerangRules.OutSpeed, BoomerangRules.OutSeconds, BoomerangRules.TurnAcceleration,
            BoomerangRules.BackTopSpeed, BoomerangRules.CatchMetres, BoomerangRules.HitRadius, BoomerangRules.HitRadius, BoomerangRules.HitAgainSeconds,
            BoomerangRules.SpinDegreesPerSecond, false);

        public static readonly BoomerangProfile Giant = new BoomerangProfile(GiantBoomerangRules.OutSpeed, GiantBoomerangRules.OutSeconds, GiantBoomerangRules.TurnAcceleration,
            GiantBoomerangRules.BackTopSpeed, GiantBoomerangRules.CatchMetres, GiantBoomerangRules.HitRadius, GiantBoomerangRules.WallRadius, GiantBoomerangRules.HitAgainSeconds,
            GiantBoomerangRules.SpinDegreesPerSecond, true);
    }

    /// <summary>
    /// The numbers of the Giant Boomerang (docs/BOOMERANG.md; owner, 2026-11-12: "make a giant version of the boomerang like the girl from inuyasha", thrown
    /// "heavy and slow": a wind-up, slower, about twice the wooden one's range, about three times its damage, knocks people over). All mine but those.
    /// </summary>
    public static class GiantBoomerangRules
    {
        public const float Damage = 30.0f;

        public const float OutSpeed = 18.0f;

        public const float OutSeconds = 1.1f;

        public const float TurnAcceleration = 32.0f;

        public const float BackTopSpeed = 24.0f;

        /// <summary>It is caught with its middle this near the catching hand: held near one end, its middle is a metre out from the fist (BoomerangThrower.GiantMiddleFromHand).</summary>
        public const float CatchMetres = 1.0f;

        /// <summary>A body this near its path is hit: about its half span less its thin tips.</summary>
        public const float HitRadius = 1.1f;

        /// <summary>A wall this near its path turns it back: its thickness, not its span, or the ground under it would.</summary>
        public const float WallRadius = 0.3f;

        public const float HitAgainSeconds = 0.5f;

        /// <summary>Two turns a second: heavy.</summary>
        public const float SpinDegreesPerSecond = 720.0f;

        /// <summary>A body it hits is thrown down limp this fast away from the thrower, and this fast up, m/s.</summary>
        public const float KnockAway = 6.0f;

        public const float KnockUp = 3.0f;
    }
}
