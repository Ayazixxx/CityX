namespace CityX.Character
{
    /// <summary>The Fire Enhancement's numbers (docs/FIRE_ENHANCEMENT.md).</summary>
    public static class FireEnhancementRules
    {
        /// <summary>What a cast costs, mana, and how long the fire waits on the weapon for its attack before it goes out, seconds.</summary>
        public const float ManaCost = 15.0f;

        public const float ArmedSeconds = 30.0f;

        /// <summary>From one cast to the next, seconds. It does not stack: cast while the fire still waits, nothing happens.</summary>
        public const float CooldownSeconds = 1.0f;

        /// <summary>The fire each blow of the enhanced attack adds, before Spell Power, and how long its target then burns, seconds.</summary>
        public const float FireDamage = 15.0f;

        public const float BurnSeconds = 5.0f;

        /// <summary>
        /// The enhanced swing burns until the body stops swinging (a held heavy stays enhanced to its blow), at least this long (the swing
        /// may only show as started the frame after), and never longer than the most, seconds.
        /// </summary>
        public const float SwingGraceSeconds = 0.15f;

        public const float MaxSwingSeconds = 5.0f;
    }
}
