namespace CityX.Character
{
    /// <summary>
    /// THE KNIFE'S NUMBERS (docs/KNIFE.md). Owner, 2026-10-29: "i think knives should do less damage on average, maybe have like 10% less
    /// reach and be around 30% faster swing speed". Fought with the one-handed sword's slashes (SoulsSwordCombat's knife set), quicker and
    /// lighter. What keeps it from simply beating the sword, from the research (Skyrim's daggers: speed 1.3, reach 0.7, stagger 0; Dark
    /// Souls 3: a straight sword trades through a dagger's lights): its lights do not stagger, so a sword's blow interrupts a knife's and
    /// never the other way round, and its guard is thin. Its reward is the backstab (<see cref="Backstab"/>).
    /// </summary>
    public static class KnifeRules
    {
        /// <summary>How much faster the whole swing runs than the one-handed sword's: wind-up, cut, recovery and the lunge.</summary>
        public const float SwingSpeed = 1.3f;

        /// <summary>The share of the sword's reach a knife's swing catches a body at.</summary>
        public const float ReachShare = 0.9f;

        /// <summary>A knife blow's base damage, the sword's being <see cref="CombatTuning.PunchDamage"/> (10).</summary>
        public const float BaseDamage = 7.0f;

        /// <summary>A dull knife's blow (the butter knife): a rounded blade that bruises more than it cuts, and cuts nothing off.</summary>
        public const float DullDamage = 4.0f;

        /// <summary>How many times the stamina a blow on a held guard costs with a knife: a thin blade stops little, and breaks sooner.</summary>
        public const float GuardStaminaShare = 1.6f;

        /// <summary>The knife's damage a second as a share of the one-handed sword's: less per blow, more blows (about 0.9).</summary>
        public static float DamagePerSecondShare => BaseDamage / CombatTuning.PunchDamage * SwingSpeed;
    }
}
