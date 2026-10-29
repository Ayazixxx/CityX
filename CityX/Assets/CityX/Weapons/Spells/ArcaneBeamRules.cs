namespace CityX.Weapons
{
    /// <summary>
    /// The Arcane Beam Staff's numbers (docs/STAFFS.md), with no scene: a straight beam along the crosshair that burns the first thing it
    /// meets, a tick at a time, paid in mana while held.
    /// </summary>
    public static class ArcaneBeamRules
    {
        /// <summary>How far the beam reaches, metres (the water jet's 16, the flame staff's 9: a beam is the long reach of the staffs).</summary>
        public const float Range = 24.0f;

        /// <summary>How thick the beam is for what it meets, metres (its radius).</summary>
        public const float HitRadius = 0.15f;

        /// <summary>Damage a second of being held on something, before Spell Power: one target, so less than the flame staff's 36 for a cone.</summary>
        public const float DamagePerSecond = 25.0f;

        public const float TickSeconds = 0.2f;

        public const float DamagePerTick = DamagePerSecond * TickSeconds;

        /// <summary>The mana it drinks a second (mana comes back at 4 a second, so a full bar burns about 16 s).</summary>
        public const float ManaPerSecond = 10.0f;

        /// <summary>How often the beam's direction is sent to other players, seconds.</summary>
        public const float AimSendSeconds = 0.1f;
    }
}
