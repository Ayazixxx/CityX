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
    /// Unarmed was carried over from Godot's combo_data.gd. Sword is a REAL
    /// PORT now, not an invented replacement -- an earlier version of this
    /// file deliberately built a different shape here (this exact chain
    /// architecture instead of Godot's own separate two-hit
    /// _sword_combo_ready/_sword_combo_timer flag pair in character.gd),
    /// reasoning that the flag pair had a real, twice-rediscovered bug class
    /// (a stuck flag from some new call site -- jump, dash, crouch --
    /// forgetting to clear it, see tests/test_sword_hit_interrupt_check.gd
    /// and tests/test_sword_action_interrupt_check.gd). That reasoning was
    /// sound but the numbers it landed on were not checked against the real
    /// ones -- confirmed directly by reading character.gd/player.gd: the
    /// combo window is 5 seconds (SWORD_COMBO_WINDOW_SECONDS), not 0.9, and
    /// sword_attack2 deals the SAME damage as sword_attack (both just
    /// PUNCH_DAMAGE, no per-swing scaling) rather than hitting 1.4x harder.
    /// This keeps the timer-based chain architecture (still correct that
    /// IsSwinging as a countdown cannot get stuck the way a flag can), but
    /// the numbers themselves now match Godot's real values exactly.
    /// </summary>
    public static class ComboChain
    {
        public const string Unarmed = "unarmed";

        /// <summary>
        /// Two stages -- sword_attack then sword_attack2 -- matching
        /// character.gd's own punch()/get_last_attack_combo_info(): a
        /// second swing within SWORD_COMBO_WINDOW_SECONDS of the first
        /// plays sword_attack2 and ends the combo (finisher); a third
        /// click after that restarts at sword_attack. Both swings deal
        /// IDENTICAL damage (damage_scale 1.0 on both -- there is no
        /// "finisher hits harder" for sword the way unarmed's own chain
        /// has). sword_attack2's hitstun_scale is 0 not because Godot's own
        /// hitstun_scale is 0 (it's actually 1.0, same as sword_attack) but
        /// because player.gd's take_damage() checks `if is_finisher: ...
        /// elif hitstun_seconds > 0: ...` -- is_finisher always wins, so a
        /// real sword_attack2 hit never actually applies hitstun at all, it
        /// ragdolls instead (see MeleeAttacker's own ApplyHit contract: zero
        /// hitstun is this codebase's own signal for "ragdoll instead").
        /// The OBSERVABLE outcome matches exactly; the numeric hitstun_scale
        /// value along the way does not need to, since Godot's own value is
        /// unreachable in play.
        /// </summary>
        public const string Sword = "sword";

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
                [Sword] = new[]
                {
                    // SWORD_COMBO_WINDOW_SECONDS in character.gd -- a real 5
                    // seconds, not the unarmed chain's own 0.9s window.
                    new ComboStage("sword_attack", 1.0f, 1.0f, 5.0f, false),
                    new ComboStage("sword_attack2", 1.0f, 0.0f, 0.0f, true),
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

        /// <summary>
        /// How long a successful block leaves the ATTACKER unable to move or
        /// throw another punch. This is the actual reward for timing a block:
        /// without it, blocking would be a worse dodge -- still taking a
        /// quarter of the hit -- rather than an opening.
        /// </summary>
        public const float BlockStunSeconds = 1.0f;

        /// <summary>player.gd POST_FINISHER_INVULN_SECONDS</summary>
        public const float PostFinisherInvulnerabilitySeconds = 1.0f;

        /// <summary>
        /// The haymaker thrown by holding the punch button rather than tapping
        /// it. player.gd's HEAVY_PUNCH_DAMAGE, which is 1.5x a jab.
        ///
        /// NOT A MEMBER OF ANY CHAIN, which is the point. It hits harder and
        /// takes longer, and it opens no follow-up window -- so throwing one is
        /// a decision to commit rather than a free upgrade to whichever jab was
        /// coming next.
        ///
        /// HITSTUN SCALE IS 0, NOT 1.5 -- an earlier version of this file had
        /// this at 1.5 (matching the damage scale), which was never actually
        /// checked against player.gd: `_punch()` seeds `var hitstun := 0.0`
        /// and only overwrites it from get_last_attack_combo_info() when
        /// `not is_heavy` -- a heavy/haymaker punch keeps that zero default
        /// unconditionally, real damage but no hitstun at all, same shape the
        /// unarmed chain's own finisher already uses here for the opposite
        /// reason (ragdoll instead). Confirmed by reading player.gd directly.
        /// </summary>
        public static readonly ComboStage HeavyPunch =
            new ComboStage("punch_heavy", 1.5f, 0.0f, 0.0f, false);

        /// <summary>
        /// The sword's own held-attack variant, played from
        /// 2hsw_chargedattack.fbx. Godot's sword_heavy_attack runs through
        /// the exact same `_punch(character, is_heavy=true)` path as the
        /// unarmed haymaker -- there is no separate sword-heavy damage
        /// constant, it is HEAVY_PUNCH_DAMAGE (1.5x PUNCH_DAMAGE) same as
        /// HeavyPunch above, with the same zero hitstun for the same reason.
        /// An earlier version of this file had 1.6x damage and full hitstun,
        /// neither of which is real -- confirmed by reading player.gd
        /// directly, same as HeavyPunch's own correction.
        /// </summary>
        public static readonly ComboStage SwordHeavyAttack =
            new ComboStage("sword_charged_attack", 1.5f, 0.0f, 0.0f, false);

        /// <summary>player.gd STRENGTH_DAMAGE_PERCENT_PER_POINT</summary>
        public const float StrengthDamagePerPoint = 0.02f;

        /// <summary>The shared strength-scaling chokepoint every damage source runs through.</summary>
        public static float DamageFor(float baseDamage, int strength)
        {
            return baseDamage * (1.0f + strength * StrengthDamagePerPoint);
        }

        /// <summary>Damage a stage deals at a given Strength.</summary>
        public static float DamageFor(ComboStage stage, int strength)
        {
            return DamageFor(PunchDamage * stage.DamageScale, strength);
        }

        /// <summary>How long a stage stuns its target.</summary>
        public static float HitstunFor(ComboStage stage)
        {
            return HitstunSeconds * stage.HitstunScale;
        }
    }
}
