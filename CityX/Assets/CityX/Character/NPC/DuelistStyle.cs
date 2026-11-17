namespace CityX.Character
{
    /// <summary>Which way a duelist fights (<see cref="DuelistStyle"/>): set on the enemy's brain by its builder.</summary>
    public enum DuelistStyleKind
    {
        /// <summary>The swordsman's: a measured duel behind a guard.</summary>
        Swordsman,

        /// <summary>Crazy Guy's: the kitchen knife's.</summary>
        Knife,
    }

    /// <summary>
    /// HOW A DUELIST FIGHTS: the numbers <see cref="DuelistMind"/> fights by. The swordsman's are the mind's own constants, as they have always been.
    ///
    /// THE KNIFE'S (owner, 2026-11-16, of Crazy Guy: "make him wield a kitchen knife and utilize its advantages"; docs/KNIFE.md): the knife is quick
    /// and light, a little short in reach, its slashes stagger no one, its guard is thin, and its backstab is the biggest of any weapon (3x,
    /// Backstab.KnifeMultiplier). So he fights close and fast: in tight, flurries of two to four quick cuts with short pauses, hardly ever a guard
    /// (dashing instead, more often), taking the target's back when he has a clear shot at it (<see cref="FlanksForBackstab"/>; owner, 2026-11-17:
    /// "only goes for that when he has a clear shot"), cutting the moment he is behind it, and when a slow blow comes, dashing round it rather than
    /// away (<see cref="DashesRound"/>). Now and then the icepick stab (its heavy).
    /// </summary>
    public sealed class DuelistStyle
    {
        public float EngageMetres;
        public float StrikeMetres;
        public float SprintBeyondMetres;
        public float CircleMin;
        public float CircleMax;
        public float AttackCooldownMin;
        public float AttackCooldownMax;
        public float BackOffSeconds;
        public float PressSeconds;
        public float GuardStanceChance;
        public float DodgeChance;
        public float HeavyChance;
        public float IntoGuardChance;
        public float DashWhenHitChance;
        public float DashOutChance;
        public float DashInPerSecond;

        /// <summary>A chain is one cut and this many more, chosen between the two (both inclusive).</summary>
        public int ExtraCutsMin;

        public int ExtraCutsMax;

        /// <summary>
        /// With a clear shot at the target's back (already round past its side, <see cref="DuelistMind.ClearShotDegrees"/>) he steps round to it,
        /// and he cuts the moment he is behind it (a backstab). In front of it he fights it face on.
        /// </summary>
        public bool FlanksForBackstab;

        /// <summary>Out of a slow blow he dashes round the target, toward its back, instead of away.</summary>
        public bool DashesRound;

        /// <summary>
        /// His pause over, he steps in to cut instead of circling until the target comes into reach (the swordsman waits for it: a duel; a knife
        /// presses).
        /// </summary>
        public bool StepsInToCut;

        public static readonly DuelistStyle Swordsman = new DuelistStyle
        {
            EngageMetres = DuelistMind.EngageMetres,
            StrikeMetres = DuelistMind.StrikeMetres,
            SprintBeyondMetres = DuelistMind.SprintBeyondMetres,
            CircleMin = DuelistMind.CircleMin,
            CircleMax = DuelistMind.CircleMax,
            AttackCooldownMin = DuelistMind.AttackCooldownMin,
            AttackCooldownMax = DuelistMind.AttackCooldownMax,
            BackOffSeconds = DuelistMind.BackOffSeconds,
            PressSeconds = DuelistMind.PressSeconds,
            GuardStanceChance = DuelistMind.GuardStanceChance,
            DodgeChance = DuelistMind.DodgeChance,
            HeavyChance = DuelistMind.HeavyChance,
            IntoGuardChance = DuelistMind.IntoGuardChance,
            DashWhenHitChance = DuelistMind.DashWhenHitChance,
            DashOutChance = DuelistMind.DashOutChance,
            DashInPerSecond = DuelistMind.DashInPerSecond,
            ExtraCutsMin = 0,
            ExtraCutsMax = 1,
        };

        public static readonly DuelistStyle Knife = new DuelistStyle
        {
            EngageMetres = 2.6f,
            StrikeMetres = DuelistMind.StrikeMetres * KnifeRules.ReachShare,
            SprintBeyondMetres = 5.0f,
            CircleMin = 0.6f,
            CircleMax = 1.4f,
            AttackCooldownMin = 0.35f,
            AttackCooldownMax = 0.9f,
            BackOffSeconds = 0.4f,
            PressSeconds = DuelistMind.PressSeconds / KnifeRules.SwingSpeed,
            GuardStanceChance = 0.05f,
            DodgeChance = 0.7f,
            HeavyChance = 0.2f,
            IntoGuardChance = 0.1f,
            DashWhenHitChance = 0.5f,
            DashOutChance = 0.6f,
            DashInPerSecond = 0.8f,
            ExtraCutsMin = 1,
            ExtraCutsMax = 3,
            FlanksForBackstab = true,
            DashesRound = true,
            StepsInToCut = true,
        };

        public static DuelistStyle For(DuelistStyleKind kind) => kind == DuelistStyleKind.Knife ? Knife : Swordsman;
    }
}
