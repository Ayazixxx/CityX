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
        /// Last swing of the chain: it closes it, and its recovery cannot be cancelled into the next (the fighter is open there). An explicit flag
        /// rather than "is it the last index", so a chain could end early without renumbering.
        /// </summary>
        public readonly bool IsFinisher;

        /// <summary>A landed hit knocks a player down (the finisher ragdoll) instead of stunning: its hitstun scale is zero.</summary>
        public readonly bool KnocksDown;

        public ComboStage(string animation, float damageScale, float hitstunScale,
            float windowSeconds, bool isFinisher, bool knocksDown = false)
        {
            Animation = animation;
            DamageScale = damageScale;
            HitstunScale = hitstunScale;
            WindowSeconds = windowSeconds;
            IsFinisher = isFinisher;
            KnocksDown = knocksDown;
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
                    // SOULS' TWO-HIT STRING (docs/MELEE_COMBOS.md; owner, 2026-11-03: "the fists ... are too op"): a right straight and a left
                    // straight, each half a sword blow, then the fighter must recover (the left closes the chain and cannot be cancelled). The
                    // first's stun outlasts the gap to the second, so two hits are guaranteed; the third is not.
                    new ComboStage(FistStrikes.RightStraightName, 0.5f, 0.65f, 0.9f, false),
                    new ComboStage(FistStrikes.LeftStraightName, 0.5f, 0.5f, 0.0f, true),
                },
                [Sword] = new[]
                {
                    // SWORD_COMBO_WINDOW_SECONDS in character.gd -- a real 5
                    // seconds, not the unarmed chain's own 0.9s window.
                    new ComboStage("sword_attack", 1.0f, 1.0f, 5.0f, false),
                    // The finisher's hitstun scale is zero on purpose: it
                    // ragdolls the target instead, so hitstun would be a second
                    // competing reaction to the same hit.
                    new ComboStage("sword_attack2", 1.0f, 0.0f, 0.0f, true, knocksDown: true),
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
        /// <summary>player.gd PUNCH_DAMAGE: the base every melee figure scales from (a sword blow's; a bare straight is half of it, 2026-11-03).</summary>
        public const float PunchDamage = 10.0f;

        /// <summary>
        /// The one-handed sword's (the combat sword's) blow: as much damage a second as the kitchen knife's at its 1.3 times the swings, 11.7
        /// (owner, 2026-11-15: "raise the sword damage to fill that 17% damage per second gap"). Other blades and the blunt weapons keep PunchDamage.
        /// </summary>
        public const float OneHandedSwordDamage = KnifeRules.BaseDamage * KnifeRules.SwingSpeed;

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
        /// The overhand thrown by holding the punch button rather than tapping it: the fists' big blow, a whole sword blow's damage, and it
        /// KNOCKS THE TARGET DOWN (no hitstun on top: the ragdoll is the reaction).
        ///
        /// NOT A MEMBER OF THE CHAIN, which is the point. It takes longest to throw (the long wind-up is the tell), and it starts the chain over
        /// rather than advancing it, so throwing one is a decision to commit rather than a free upgrade to whichever straight was coming next.
        /// Its window is for one thing only: the button HELD again inside it follows with <see cref="UppercutPunch"/>.
        /// </summary>
        public static readonly ComboStage HeavyPunch =
            new ComboStage(FistStrikes.OverhandName, 1.0f, 0.0f, 0.9f, false, knocksDown: true);

        /// <summary>The uppercut: the held follow-up to the overhand, driven up from a dip. Hits a little less, stuns longest, opens nothing.</summary>
        public static readonly ComboStage UppercutPunch =
            new ComboStage(FistStrikes.UppercutName, 0.8f, 0.8f, 0.0f, false);

        /// <summary>
        /// STAMINA (docs/MELEE_COMBOS.md): no stamina, no punch, as with the sword. A straight costs half a sword light; the overhand as much as a
        /// sword's light; recovery pauses as after a sword swing.
        /// </summary>
        public const float PunchStaminaCost = 8.0f;

        public const float HeavyPunchStaminaCost = 16.0f;

        public const float UppercutStaminaCost = 12.0f;

        public const float PunchRegenDelaySeconds = 0.6f;

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
        public static float DamageFor(float baseDamage, float strength)
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
