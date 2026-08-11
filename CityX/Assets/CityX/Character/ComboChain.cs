using System.Collections.Generic;

namespace CityX.Character
{
    /// <summary>One swing in an attack chain.</summary>
    public readonly struct ComboStage
    {
        /// <summary>Animation state name.</summary>
        public readonly string Animation;

        /// <summary>Multiplier on the base punch damage, not a literal figure.</summary>
        public readonly float DamageScale;

        /// <summary>Multiplier on the base hitstun duration.</summary>
        public readonly float HitstunScale;

        /// <summary>
        /// Seconds after this stage during which another attack advances the
        /// chain rather than restarting it.
        /// </summary>
        public readonly float WindowSeconds;

        /// <summary>
        /// Last swing of the chain. An explicit flag rather than "is it the
        /// last index", so a chain could end early without renumbering.
        /// </summary>
        public readonly bool IsFinisher;

        public ComboStage(string animation, float damageScale, float hitstunScale,
            float windowSeconds, bool isFinisher)
        {
            Animation = animation;
            DamageScale = damageScale;
            HitstunScale = hitstunScale;
            WindowSeconds = windowSeconds;
            IsFinisher = isFinisher;
        }
    }

    /// <summary>
    /// The attack chains, ported from the Godot build's combo_data.gd.
    ///
    /// A shared table so the animation side and the damage side do not
    /// re-derive each other's numbers -- the exact reason it was split out in
    /// the original.
    ///
    /// SCALES, NOT LITERALS. Damage and hitstun are multipliers against
    /// <see cref="CombatTuning"/>'s base figures rather than copied numbers,
    /// matching the original's "derive, don't duplicate" convention. Retuning a
    /// punch then moves the whole chain with it instead of leaving four
    /// stages quietly disagreeing.
    ///
    /// Only unarmed is carried over. Sword had its own separate two-hit
    /// mechanic in Godot and a known bug (task #292, a swing that never
    /// released its in-progress flag); porting the bug would be silly, and
    /// porting the fix is a decision for when swords exist here at all.
    /// </summary>
    public static class ComboChain
    {
        public const string Unarmed = "unarmed";

        private static readonly Dictionary<string, ComboStage[]> Chains =
            new Dictionary<string, ComboStage[]>
            {
                [Unarmed] = new[]
                {
                    new ComboStage("punch_right", 1.0f, 1.0f, 0.9f, false),
                    new ComboStage("punch_left", 1.0f, 1.0f, 0.9f, false),
                    new ComboStage("punch_right", 1.0f, 1.0f, 0.9f, false),
                    new ComboStage("punch_left", 1.0f, 1.1f, 0.9f, false),
                    // The finisher's hitstun scale is zero on purpose: it
                    // ragdolls the target instead, so hitstun would be a second
                    // competing reaction to the same hit.
                    new ComboStage("punch_heavy", 1.5f, 0.0f, 0.0f, true),
                },
            };

        public static ComboStage[] For(string moveset)
        {
            return Chains.TryGetValue(moveset, out ComboStage[] chain)
                ? chain
                : System.Array.Empty<ComboStage>();
        }

        public static bool Has(string moveset) => Chains.ContainsKey(moveset);
    }

    /// <summary>
    /// Base combat figures, ported from player.gd. Everything else is derived
    /// from these.
    /// </summary>
    public static class CombatTuning
    {
        /// <summary>player.gd PUNCH_DAMAGE</summary>
        public const float PunchDamage = 10.0f;

        /// <summary>player.gd HITSTUN_SECONDS</summary>
        public const float HitstunSeconds = 0.5f;

        /// <summary>player.gd BLOCK_DAMAGE_MULTIPLIER -- 75% is absorbed.</summary>
        public const float BlockDamageMultiplier = 0.25f;

        /// <summary>player.gd POST_FINISHER_INVULN_SECONDS</summary>
        public const float PostFinisherInvulnerabilitySeconds = 1.0f;

        /// <summary>player.gd STRENGTH_DAMAGE_PERCENT_PER_POINT</summary>
        public const float StrengthDamagePerPoint = 0.02f;

        /// <summary>Damage a stage deals at a given Strength.</summary>
        public static float DamageFor(ComboStage stage, int strength)
        {
            return PunchDamage * stage.DamageScale * (1.0f + strength * StrengthDamagePerPoint);
        }

        /// <summary>How long a stage stuns its target.</summary>
        public static float HitstunFor(ComboStage stage)
        {
            return HitstunSeconds * stage.HitstunScale;
        }
    }
}
