using System;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Health, stamina, fall damage and death, ported from player.gd.
    ///
    /// PLAIN C# LOGIC WITH A THIN MonoBehaviour AROUND IT. Everything that
    /// decides a number lives in methods that take a delta and return a result,
    /// so the rules can be tested without a scene, a physics step or a frame.
    /// The Godot original had all of this tangled into _physics_process, which
    /// is why it could only ever be checked by playing the game.
    ///
    /// The stat scaling is carried over intact: health and stamina both grow
    /// with their stat point, so a levelled character is genuinely tougher
    /// rather than just hitting harder.
    /// </summary>
    public sealed class PlayerVitals : MonoBehaviour, IDamageable, IPoisonable
    {
        /// <summary>Set on a body that other players can hit (see NetworkedDamage): another player's copy forwards hits to the real body instead of applying them.</summary>
        public IRemoteDamageRelay Relay;

        /// <summary>Poison eats health quietly: no flinch, no block, no i-frames (see IPoisonable).</summary>
        public void TakePoison(float amount)
        {
            if (Relay != null && amount > 0.0f && Relay.TryRelayPoison(amount))
            {
                return;
            }
            if (IsDead || amount <= 0.0f)
            {
                return;
            }
            // Poison is a spell's (the gas, the acid): magic resistance stops its share.
            TakeDamage(amount * (1.0f - ResistanceTo(DamageKind.Magic, 0.0f)), false);
        }

        // --- tuning, ported from player.gd ---

        public const float BaseMaxHealth = 100.0f;
        public const float BaseMaxStamina = 130.0f;
        public const float StaminaDrainPerSecond = 20.0f;
        public const float StaminaRegenPerSecond = 15.6f;

        /// <summary>
        /// MANA (owner, 2026-10-14: a mana bar on the HUD; "make it so the mana bar doesnt appear until you unlock a spell. but i guess for now
        /// just have it on screen from the beginning"). The pool spells will draw on (GAME_DESIGN.md: a spell costs mana, and Mana appears with
        /// your first spell). What each spell costs is still to be decided (owner: "we will figure out how much mana each spell costs later"),
        /// so nothing spends it yet.
        /// </summary>
        public const float BaseMaxMana = 100.0f;
        public const float ManaRegenPerSecond = 4.0f;

        /// <summary>
        /// player.gd STAMINA_EXHAUSTED_RECOVER_FRACTION. Once stamina bottoms
        /// out you cannot sprint again until this much has come back -- without
        /// it, sprint stutters on and off one frame at a time at zero.
        /// </summary>
        public const float ExhaustedRecoverFraction = 0.2f;

        /// <summary>What a point of Max Health and of Max Stamina adds (StatCatalog's own numbers; kept here for what already read them).</summary>
        public const float HealthPerStatPoint = 10.0f;
        public const float StaminaPerStatPoint = 10.0f;

        /// <summary>
        /// HEALTH COMES BACK ON ITS OWN (2026-10-14, for the Health Regen stat: the game had no regeneration at all): this many points a
        /// second, plus Health Regen's, once <see cref="HealthRegenDelaySeconds"/> have passed without a blow.
        /// </summary>
        public const float BaseHealthRegenPerSecond = 1.0f;

        public const float HealthRegenDelaySeconds = 5.0f;

        /// <summary>player.gd STAT_POINTS_PER_LEVEL -- granted on every level-up.</summary>
        public const int StatPointsPerLevel = 5;

        /// <summary>player.gd SKILL_POINT_EVERY_N_LEVELS -- one skill point on every second level reached (2, 4, 6...).</summary>
        public const int SkillPointEveryNLevels = 2;

        /// <summary>player.gd XP_PER_LEVEL_BASE -- next level costs this times the level being left.</summary>
        public const int XpPerLevelBase = 100;

        /// <summary>
        /// player.gd SAFE_FALL_DISTANCE: TWO STOREYS, expressed in the city's
        /// own floor height rather than as a loose number, so changing the
        /// floor height carries this with it.
        ///
        /// A storey is StoreyHeightRows window rows, not one -- and this used
        /// to read NominalFloorMetres * 2, which is two ROWS. That is 6.4m
        /// against Godot's 12.8m, so every drop between one and two storeys
        /// hurt here and was free there. The comment already said "two
        /// storeys"; only the arithmetic disagreed.
        /// </summary>
        public static float SafeFallDistance =>
            World.CellLayout.NominalFloorMetres * World.BuildingInterior.StoreyHeightRows * 2.0f;

        /// <summary>player.gd FALL_DAMAGE_PER_METER, as a fraction of base health.</summary>
        public const float FallDamagePerMetreFraction = 0.08f;

        /// <summary>
        /// How much of SafeFallDistance's own margin a horizontal crash
        /// still gets, out of 1.0 -- deliberately less than a straight
        /// drop's full two storeys. A fall's first two storeys are safe
        /// because legs bend on landing and absorb it; ramming into an
        /// immovable wall sideways has no equivalent give, the torso takes
        /// it directly, so the same body should not get the same margin
        /// both ways. CONCRETE BUG, found by
        /// JetpackTests.TheRealTopDiveSpeedIsActuallyFastEnoughToTriggerCrashDamage:
        /// reusing SafeFallDistance itself gave a threshold of 15.85 m/s
        /// against a jetpack dive that only ever reaches 15.0 m/s (an
        /// EXPLICIT ASK -- see PlayerLocomotion.JetpackDiveSpeedMultiplier's
        /// own doc comment -- so the speed side is not this fix's to
        /// change), meaning the single fastest way to hit a wall with this
        /// jetpack could never clear the bar. This is what "the jetpack
        /// sideways fall damage thing still hasnt workedt" actually was.
        /// </summary>
        public const float CrashSafeDistanceFraction = 0.5f;

        /// <summary>ApplyCrashDamage's own safe threshold -- see CrashSafeDistanceFraction's own doc comment for why it differs from SafeFallDistance.</summary>
        public static float SafeCrashDistance => SafeFallDistance * CrashSafeDistanceFraction;

        // --- stats ---

        [Header("Stats")]
        /// <summary>Shown on the HUD badge; raised by XP (AddXp).</summary>
        public int Level = 1;

        /// <summary>
        /// THE POINTS IN EVERY STAT (owner, 2026-10-14: StatCatalog, twenty-four of them). Everything a stat changes asks this sheet: the
        /// ceilings and regeneration here, the blows taken here (armour, magic resistance, block chance, stability), and the blows, shots,
        /// spells and steps the body makes elsewhere.
        /// </summary>
        public StatSheet Stats = new StatSheet();

        /// <summary>The old Vitality: Max Health's points.</summary>
        public int Vitality
        {
            get => Stats[StatId.MaxHealth];
            set => Stats[StatId.MaxHealth] = value;
        }

        /// <summary>The old Endurance: Max Stamina's points.</summary>
        public int Endurance
        {
            get => Stats[StatId.MaxStamina];
            set => Stats[StatId.MaxStamina] = value;
        }

        /// <summary>The old Strength: Melee Damage's points (2% a point, as Strength always was).</summary>
        public int Strength
        {
            get => Stats[StatId.MeleeDamage];
            set => Stats[StatId.MeleeDamage] = value;
        }

        /// <summary>Earned but not yet spent on the stats.</summary>
        public int StatPoints;

        /// <summary>Earned but not yet spent on the skill trees (PlayerSkills.TryLearn). A separate pool from the stat points.</summary>
        public int SkillPoints;

        /// <summary>Progress toward the next level. Reset (not cleared) on every level-up.</summary>
        public int Experience;

        /// <summary>player.gd xp_to_next_level -- a pure function of Level, so it can never drift out of sync with it.</summary>
        public int ExperienceToNextLevel => XpPerLevelBase * Level;

        // --- state ---

        [Header("State")]
        [SerializeField] private float _health = BaseMaxHealth;

        [SerializeField] private float _stamina = BaseMaxStamina;

        [SerializeField] private float _mana = BaseMaxMana;

        [Tooltip("Whether this player has mana yet: the HUD shows the mana bar only when true. Mana is meant to appear with the first spell " +
                 "unlocked (GAME_DESIGN.md); until spells are unlocked by the skill tree, everyone has it from the start.")]
        public bool ManaUnlocked = true;

        private bool _exhausted;
        private float _highestAirborneY;
        private bool _wasAirborne;

        public float MaxHealth => _networkMaxHealth > 0.0f ? _networkMaxHealth : BaseMaxHealth + Stats.Effect(StatId.MaxHealth);
        public float MaxStamina => BaseMaxStamina + Stats.Effect(StatId.MaxStamina);

        public float Health => _health;
        public float Stamina => _stamina;
        public float MaxMana => BaseMaxMana + Stats.Effect(StatId.MaxMana);
        public float Mana => _mana;
        public bool IsDead => _health <= 0.0f;

        /// <summary>
        /// True while stamina has bottomed out and has not recovered enough to
        /// sprint again.
        /// </summary>
        public bool IsExhausted => _exhausted;

        /// <summary>
        /// Seconds left locked out of acting after being hit. Lives here rather
        /// than in the attacker because it is a condition of the body being
        /// hit, and several things need to ask about it.
        /// </summary>
        public float HitstunRemaining { get; private set; }

        public bool IsStunned => HitstunRemaining > 0.0f;

        // ---- POISE (2026-10-13, from the soulslike research: Dark Souls 3 turns poise on only inside attacks with hyper-armour) ----

        /// <summary>The poise pool: an attack with hyper-armour keeps going under blows until their poise damage has emptied it.</summary>
        public const float MaxPoise = 60.0f;

        /// <summary>The pool refills at this rate once <see cref="PoiseRegenDelaySeconds"/> have passed since the last blow.</summary>
        public const float PoiseRegenPerSecond = 20.0f;

        public const float PoiseRegenDelaySeconds = 1.5f;

        public float Poise { get; private set; } = MaxPoise;

        /// <summary>Set by the combat while an attack with hyper-armour is in its wind-up or active frames: blows then only stagger once the poise is spent.</summary>
        [NonSerialized]
        public bool HyperArmour;

        private float _poiseRegenDelay;
        private float _pendingPoise = -1.0f;

        /// <summary>The poise damage of the NEXT blow to land (an attack says it just before the hit). A blow that never says has none: it staggers as any blow did.</summary>
        public void TakePoise(float damage)
        {
            _pendingPoise = Mathf.Max(0.0f, damage);
        }

        /// <summary>Raised when a blow empties the poise pool mid-attack: the attack is broken.</summary>
        public event Action PoiseBroken;

        /// <summary>The pool's size: <see cref="MaxPoise"/>, deeper with Stability (60% stability, 1.6 times as deep).</summary>
        public float PoiseCapacity => MaxPoise * Stats.Increase(StatId.Stability);

        // ---- WHAT THE STATS DO TO A BLOW (2026-10-14) ----

        /// <summary>
        /// A number from 0 to 1 for every chance a blow rolls (block chance, stability). Random; a test puts a fixed one in to know what it gets.
        /// </summary>
        [NonSerialized]
        public Func<float> Roll = () => UnityEngine.Random.value;

        /// <summary>
        /// Raised when a block stops a blow outright (Block Chance): the blow's origin and kind. Nothing gets through, no stamina is spent, and a
        /// bullet glances off. What shows it (a spark, the sound) listens here.
        /// </summary>
        public event Action<Vector3, DamageKind> Deflected;

        /// <summary>
        /// Raised when a blow staggers this body (puts it in hitstun). What was under way and should be cut short by being hit -- a spell's
        /// wind-up, a charging spin -- listens here. A blow Stability shrugs off raises nothing.
        /// </summary>
        public event Action Staggered;

        /// <summary>What a cooldown is multiplied by: Cooldown Reduction's share off (a body with no vitals: 1).</summary>
        public static float CooldownFactorOf(PlayerVitals vitals) => vitals != null ? vitals.Stats.Decrease(StatId.CooldownReduction) : 1.0f;

        /// <summary>How much faster a stat makes something go: 1 plus its share (a body with no vitals: 1). Melee Speed, Cast Speed, Fire Rate...</summary>
        public static float SpeedOf(PlayerVitals vitals, StatId stat) => vitals != null ? vitals.Stats.Increase(stat) : 1.0f;

        /// <summary>The whole chance to stop a blow outright while blocking: everyone's <see cref="StatCatalog.BaseBlockChance"/> plus Block Chance.</summary>
        public float BlockChance => StatCatalog.BaseBlockChance + Stats.Effect(StatId.BlockChance);

        /// <summary>The share of a blow of this kind that armour (physical, bullets) or magic resistance (spells) stops, after the attacker's penetration.</summary>
        public float ResistanceTo(DamageKind kind, float penetration)
        {
            float rating = kind == DamageKind.Magic ? Stats.Effect(StatId.MagicResistance) : Stats.Effect(StatId.Armor);
            return StatCatalog.RatingReduction(rating * (1.0f - Mathf.Clamp01(penetration)));
        }

        // ---- THE PARRY: a held guard that catches a blow in its first instant ----

        /// <summary>Seconds left of the guard's perfect window (set when the guard comes up, SoulsSwordCombat.PerfectGuardSeconds).</summary>
        public float PerfectGuardRemaining { get; private set; }

        public void BeginPerfectGuard(float seconds)
        {
            PerfectGuardRemaining = Mathf.Max(PerfectGuardRemaining, seconds);
        }

        /// <summary>Raised with the blow's origin when the guard parries it: no damage, no stamina, and the attacker is the one left open.</summary>
        public event Action<Vector3> Parried;

        public bool IsAlive => !IsDead;

        public event Action Died;

        /// <summary>
        /// Raised by <see cref="Revive"/>. Paired with Died so that anything
        /// showing a death state has a matching signal to clear it, rather than
        /// polling IsDead and guessing when the transition happened.
        /// </summary>
        public event Action Revived;

        public event Action<float> Damaged;

        /// <summary>Raised with the world position the blow came from.</summary>
        public event Action<Vector3> Hit;

        /// <summary>
        /// How long this body still cannot be hit. Set by a dash.
        ///
        /// EXACTLY THE BURST, not the animation's follow-through -- the dash
        /// clip keeps playing a recovery pose for a while after the movement
        /// has ended, and Godot is explicit that what dodges the blow is going
        /// through it, not standing in the aftermath of having done so.
        /// </summary>
        public float InvulnerableRemaining { get; private set; }

        public bool IsInvulnerable => InvulnerableRemaining > 0.0f;

        public void GrantInvulnerability(float seconds)
        {
            InvulnerableRemaining = Mathf.Max(InvulnerableRemaining, seconds);
        }

        /// <summary>
        /// How long a block stays up once thrown. Exactly as long as the clip
        /// plays, and NOT extendable by holding the button -- Godot is explicit
        /// that you cannot hold a block, you have to time it. Holding
        /// right-click past this does nothing at all.
        /// </summary>
        public const float BlockWindowSeconds = 0.7f;

        /// <summary>
        /// Development cheats, ported from Godot's /infinitehealth and
        /// /infinitesprint. Plain fields rather than anything cleverer: they
        /// exist to let somebody stand in front of a thing and study it without
        /// the game killing them, and nothing else should ever read them.
        /// </summary>
        public bool InfiniteHealth;

        public bool InfiniteStamina;

        public float BlockRemaining { get; private set; }

        public bool IsBlocking => BlockRemaining > 0.0f;

        /// <summary>Throws a block. Re-throwing mid-block restarts the window.</summary>
        public void StartBlock()
        {
            BlockRemaining = BlockWindowSeconds;
        }

        /// <summary>
        /// Locks this body out of acting, without damaging it. Used to punish
        /// an attacker whose punch was blocked -- the damage already went to
        /// the defender, and this is what happens to whoever threw it.
        /// </summary>
        public void Stun(float seconds)
        {
            HitstunRemaining = Mathf.Max(HitstunRemaining, seconds);
        }

        /// <summary>
        /// player.gd FINISHER_RAGDOLL_SECONDS -- how long a landed combo
        /// finisher (the last swing of a chain, unarmed or sword) keeps the
        /// target limp before it stands back up on its own.
        /// </summary>
        public const float FinisherRagdollSeconds = 1.5f;

        /// <summary>player.gd POST_FINISHER_INVULN_SECONDS -- granted once the body stands back up, not during the knockdown itself.</summary>
        public const float PostFinisherInvulnerabilitySeconds = 1.0f;

        public const float FinisherKnockbackSpeed = 9.0f;
        public const float FinisherKnockbackUpSpeed = 4.0f;

        private Ragdoll _ragdoll;
        private float _finisherRagdollRemaining;

        /// <summary>
        /// Called by MeleeAttacker when a landed hit's own ComboStage is
        /// IsFinisher -- see take_damage()'s own `if is_finisher:
        /// _begin_finisher_ragdoll(attacker)` in player.gd. Deliberately
        /// NOT threaded through IDamageable.ApplyHit's own hitstunSeconds
        /// parameter (e.g. as a sentinel value): that parameter cannot
        /// distinguish "this hit has no hitstun because it's a committed
        /// heavy attack, which never ragdolls" (see HeavyPunch/
        /// SwordHeavyAttack's own hitstun_scale=0) from "this hit has no
        /// hitstun because it's a chain finisher, which ragdolls instead"
        /// -- both would arrive as hitstunSeconds==0 with nothing else to
        /// tell them apart, so MeleeAttacker calls this directly instead,
        /// since it already knows _currentStage.IsFinisher.
        ///
        /// A NO-OP WITHOUT A REAL Ragdoll COMPONENT, same tolerance
        /// Civilian.ApplyHit's own `if (_ragdoll != null)` already
        /// establishes -- not every IDamageable-carrying body needs to
        /// support this.
        /// </summary>
        public void TriggerFinisherKnockdown(Vector3 awayDirection)
        {
            if (_ragdoll == null || IsDead)
            {
                return;
            }
            Vector3 flat = awayDirection;
            flat.y = 0.0f;
            Vector3 direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.back;
            _ragdoll.Activate(direction * FinisherKnockbackSpeed + Vector3.up * FinisherKnockbackUpSpeed);
            _finisherRagdollRemaining = FinisherRagdollSeconds;
        }

        /// <summary>
        /// EXPLICIT ASK: "if the amount of damage you take doesnt kill you,
        /// you still ragdoll when you crash into something when using the
        /// jetpack." Same knockback shape as TriggerFinisherKnockdown
        /// (reuses its own speed constants -- a hard impact reads the same
        /// whichever system caused it), but deliberately does NOT arm
        /// FinisherRagdollSeconds' own timed auto-recovery: a jetpack crash
        /// instead uses Ragdoll's own manual "press space once resting
        /// StandUpRestSeconds" recovery (see Ragdoll.CanStandUp/
        /// BeginStandingUp) -- EXPLICIT ASK, "press space to stand up once
        /// your on the floor for 2.5 seconds."
        /// </summary>
        public void TriggerCrashKnockdown(Vector3 awayDirection)
        {
            if (_ragdoll == null || IsDead)
            {
                return;
            }
            Vector3 flat = awayDirection;
            flat.y = 0.0f;
            Vector3 direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.back;
            _ragdoll.Activate(direction * FinisherKnockbackSpeed + Vector3.up * FinisherKnockbackUpSpeed);
        }

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            float poiseDamage = _pendingPoise;
            _pendingPoise = -1.0f;

            // Another player's copy: the hit is sent to the real body's owner, who decides what it does (block, parry, poise, stun, health).
            if (Relay != null && Relay.TryRelayHit(amount, fromPosition, hitstunSeconds, poiseDamage))
            {
                return HitResponse.Landed;
            }

            if (IsDead || IsInvulnerable)
            {
                return HitResponse.Ignored;
            }

            // What the blow is (DamageContext, said by whoever struck it, or carried here from another player's machine by NetworkedDamage).
            DamageKind kind = DamageContext.Kind;
            float penetration = DamageContext.Penetration;
            bool blocked = IsBlocking;

            // THE PARRY: a soulslike guard that caught the blow in its first instant stops it dead -- no damage, no stamina -- and answers
            // Blocked, which leaves the ATTACKER staggered (SoulsSwordCombat.BlockedRecoilSeconds). The window closes on the first blow.
            if (blocked && BlockDrainsStamina && PerfectGuardRemaining > 0.0f)
            {
                PerfectGuardRemaining = 0.0f;
                Parried?.Invoke(fromPosition);
                return HitResponse.Blocked;
            }

            // BLOCK CHANCE (2026-10-14): a block can stop a blow outright, a bullet included (owner: "if you hold block you deflect enemy
            // bullets ... it starts at a lower chance and lets you upgrade it") -- everyone's BaseBlockChance plus the stat, cut by the
            // attacker's Armor Penetration. Nothing gets through and no stamina is spent; the attacker hears what any block answers.
            if (blocked && Roll() < BlockChance * (1.0f - penetration))
            {
                _healthRegenDelay = HealthRegenDelaySeconds;   // still a fight
                Deflected?.Invoke(fromPosition, kind);
                Hit?.Invoke(fromPosition);
                return BlockDrainsStamina ? HitResponse.Guarded : HitResponse.Blocked;
            }

            // ARMOUR AND MAGIC RESISTANCE (2026-10-14): a share of the blow is stopped -- armour stops blows and bullets, magic resistance
            // spells -- rating / (rating + 100), never all of it, less the share the attacker's penetration ignores.
            amount *= 1.0f - ResistanceTo(kind, penetration);

            TakeDamage(amount, blocked);

            // SOULS GUARD: a held block pays for what it stops in stamina, and
            // a hit it cannot afford breaks the guard -- you eat the reduced
            // damage anyway and are staggered wide open. Only when the sword
            // combat has switched this on; the classic timed block is untouched.
            // Stability makes the guard cheaper to hold (harder to break).
            if (blocked && BlockDrainsStamina && !InfiniteStamina)
            {
                float drain = amount * BlockStaminaPerDamage * (1.0f - 0.5f * Stats.Effect(StatId.Stability));
                if (drain >= _stamina)
                {
                    _stamina = 0.0f;
                    _exhausted = true;
                    BlockRemaining = 0.0f;
                    HitstunRemaining = Mathf.Max(HitstunRemaining, GuardBreakStunSeconds);
                    DelayStaminaRegen(GuardBreakStunSeconds);
                    GuardBroken?.Invoke();
                    Hit?.Invoke(fromPosition);
                    return HitResponse.Landed;
                }
                _stamina -= drain;
                DelayStaminaRegen(0.5f);
            }

            // POISE: the blow's poise damage drains the pool. Inside an attack with hyper-armour the body only staggers once the pool is
            // spent (and the pool refills for the next time); outside one, every blow staggers, as it always did -- Dark Souls 3's rule.
            bool staggers = true;
            if (poiseDamage >= 0.0f)
            {
                Poise = Mathf.Max(0.0f, Poise - poiseDamage);
                _poiseRegenDelay = PoiseRegenDelaySeconds;
                if (HyperArmour)
                {
                    staggers = Poise <= 0.0f;
                    if (staggers)
                    {
                        Poise = PoiseCapacity;
                        PoiseBroken?.Invoke();
                    }
                }
            }

            // STABILITY (2026-10-14): a blow that would stagger is shrugged off with its chance -- no hitstun, and nothing under way is cut
            // short. The damage still lands.
            if (!blocked && staggers && hitstunSeconds > 0.0f && Roll() < Stats.Effect(StatId.Stability))
            {
                staggers = false;
            }

            // A BLOCKED HIT DOES NOT STUN THE DEFENDER. Eating the hitstun as
            // well as a quarter of the damage would make blocking a worse dodge
            // rather than a punish -- and the punish is the whole point of
            // timing one.
            if (!blocked && staggers)
            {
                // Longest wins rather than newest. A weak jab landing during a
                // heavy stun should not cut it short.
                HitstunRemaining = Mathf.Max(HitstunRemaining, hitstunSeconds);
                if (hitstunSeconds > 0.0f)
                {
                    Staggered?.Invoke();
                }
            }

            Hit?.Invoke(fromPosition);
            if (!blocked)
            {
                return HitResponse.Landed;
            }
            return BlockDrainsStamina ? HitResponse.Guarded : HitResponse.Blocked;
        }

        /// <summary>Spends mana on a spell: false, and nothing spent, when there is not enough.</summary>
        public bool TrySpendMana(float amount)
        {
            if (amount < 0.0f || _mana < amount)
            {
                return false;
            }
            _mana -= amount;
            return true;
        }

        /// <summary>Mana comes back over time, but not while dead. Call once per frame (PlayerInputReader does).</summary>
        public void TickMana(float deltaTime)
        {
            if (!IsDead && _mana < MaxMana)
            {
                _mana = Mathf.Min(MaxMana, _mana + ManaRegenPerSecond * Stats.Increase(StatId.ManaRegen) * deltaTime);
            }
        }

        /// <summary>Counts hitstun and i-frames down. Call once per frame.</summary>
        public void TickHitstun(float deltaTime)
        {
            if (HitstunRemaining > 0.0f)
            {
                HitstunRemaining = Mathf.Max(0.0f, HitstunRemaining - deltaTime);
            }
            if (InvulnerableRemaining > 0.0f)
            {
                InvulnerableRemaining = Mathf.Max(0.0f, InvulnerableRemaining - deltaTime);
            }
            if (BlockRemaining > 0.0f)
            {
                BlockRemaining = Mathf.Max(0.0f, BlockRemaining - deltaTime);
            }
            if (PerfectGuardRemaining > 0.0f)
            {
                PerfectGuardRemaining = Mathf.Max(0.0f, PerfectGuardRemaining - deltaTime);
            }
            // The pool refills once the delay has run out, counting whatever of this frame is left after it.
            float poiseTime = deltaTime;
            if (_poiseRegenDelay > 0.0f)
            {
                float waited = Mathf.Min(_poiseRegenDelay, poiseTime);
                _poiseRegenDelay -= waited;
                poiseTime -= waited;
            }
            if (_poiseRegenDelay <= 0.0f && poiseTime > 0.0f && Poise < PoiseCapacity)
            {
                Poise = Mathf.Min(PoiseCapacity, Poise + PoiseRegenPerSecond * poiseTime);
            }
            if (_finisherRagdollRemaining > 0.0f)
            {
                _finisherRagdollRemaining -= deltaTime;
                if (_finisherRagdollRemaining <= 0.0f)
                {
                    _finisherRagdollRemaining = 0.0f;
                    // Gets up with the stand-up animation, from wherever the body is lying (no rest wait: the knockdown's own timer was it).
                    // A rig with no animator has nothing to animate and just stands.
                    if (_ragdoll != null && _ragdoll.Animator != null && _ragdoll.IsLimp)
                    {
                        _ragdoll.BeginStandingUp(force: true);
                    }
                    else
                    {
                        _ragdoll?.Deactivate();
                    }
                    GrantInvulnerability(PostFinisherInvulnerabilitySeconds);
                }
            }
        }

        private void Awake()
        {
            _health = MaxHealth;
            _stamina = MaxStamina;
            _mana = MaxMana;
            _ragdoll = GetComponent<Ragdoll>();
        }

        /// <summary>Restores everything, for respawn.</summary>
        public void Revive()
        {
            _health = MaxHealth;
            _stamina = MaxStamina;
            _mana = MaxMana;
            _exhausted = false;
            _wasAirborne = false;
            HitstunRemaining = 0.0f;
            Poise = PoiseCapacity;
            HyperArmour = false;
            PerfectGuardRemaining = 0.0f;
            _pendingPoise = -1.0f;
            Revived?.Invoke();
        }

        /// <summary>
        /// Another player's copy: shows the owner's health (so a copy that has died ignores further hits, and anything that reads it sees the truth).
        /// Raises nothing and costs nothing; only the owner's own vitals ever decide damage.
        /// </summary>
        public void SetHealthFromNetwork(float health, float maxHealth = -1.0f)
        {
            // The owner's ceiling comes with it (their Max Health stat is theirs, not whatever this machine's copy holds).
            if (maxHealth > 0.0f && !float.IsNaN(maxHealth))
            {
                _networkMaxHealth = maxHealth;
            }
            _health = Mathf.Clamp(float.IsNaN(health) ? _health : health, 0.0f, MaxHealth);
        }

        /// <summary>Another player's copy: the owner's Max Health, as last shared (0 until then, and always on the owner's own body).</summary>
        private float _networkMaxHealth;

        /// <summary>
        /// Grants XP, ported from player.gd's own add_xp(). Loops rather than
        /// a single if so one big grant can trigger several level-ups at
        /// once. Every second level also brings a skill point, as Godot's
        /// does (SkillPointEveryNLevels), now that there is a skill tree to
        /// spend it on (PlayerSkills).
        /// </summary>
        public void AddXp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            Experience += amount;
            while (Experience >= ExperienceToNextLevel)
            {
                Experience -= ExperienceToNextLevel;
                Level += 1;
                StatPoints += StatPointsPerLevel;
                if (Level % SkillPointEveryNLevels == 0)
                {
                    SkillPoints += 1;
                }
            }
        }

        /// <summary>
        /// Spends one available point on Vitality/Endurance/Strength. Ported
        /// from player.gd's own spend_stat_point(), scoped to the 3 stats
        /// this port has -- Mana/Speed do not exist here. Returns false, a
        /// no-op, if there is nothing to spend or the name is not
        /// recognised, the same "denied action does nothing" convention
        /// every other gated action in this project already uses.
        ///
        /// RAISES CURRENT HEALTH/STAMINA BY THE SAME AMOUNT THE MAXIMUM JUST
        /// ROSE, exactly like player.gd's own _recompute_stats() -- spending
        /// a point is an immediate, real boost, not something you have to go
        /// rest to benefit from, but it can only ever raise current health/
        /// stamina, never lower them, since a max here can only ever go up.
        /// </summary>
        public bool SpendStatPoint(string statName)
        {
            StatId id;
            switch (statName)
            {
                case "vitality": id = StatId.MaxHealth; break;
                case "endurance": id = StatId.MaxStamina; break;
                case "strength": id = StatId.MeleeDamage; break;
                default:
                    StatDefinition stat = StatCatalog.FindByKey(statName);
                    if (stat == null)
                    {
                        return false;
                    }
                    id = stat.Id;
                    break;
            }
            return SpendStatPoints(id, 1) == 1;
        }

        /// <summary>
        /// Spends up to <paramref name="count"/> points on a stat (the Stats page's "+", five with Shift) and says how many it spent. A stat that
        /// raises a ceiling raises the bar with it at once, the way spending always has: current health, stamina and mana go up by as much as
        /// their maximums.
        /// </summary>
        public int SpendStatPoints(StatId id, int count)
        {
            int spend = Mathf.Min(count, StatPoints);
            if (spend <= 0)
            {
                return 0;
            }

            float oldMaxHealth = MaxHealth;
            float oldMaxStamina = MaxStamina;
            float oldMaxMana = MaxMana;

            Stats[id] += spend;
            StatPoints -= spend;

            _health += MaxHealth - oldMaxHealth;
            _stamina += MaxStamina - oldMaxStamina;
            _mana += MaxMana - oldMaxMana;
            return spend;
        }

        private float _healthRegenDelay;

        /// <summary>
        /// Health comes back on its own (2026-10-14): <see cref="BaseHealthRegenPerSecond"/> plus Health Regen, once
        /// <see cref="HealthRegenDelaySeconds"/> have passed since the last blow. Not while dead. Call once per frame (PlayerInputReader does).
        /// </summary>
        public void TickHealthRegen(float deltaTime)
        {
            if (IsDead || deltaTime <= 0.0f)
            {
                return;
            }
            if (_healthRegenDelay > 0.0f)
            {
                _healthRegenDelay -= deltaTime;
                return;
            }
            if (_health < MaxHealth)
            {
                float rate = BaseHealthRegenPerSecond + Stats.Effect(StatId.HealthRegen);
                _health = Mathf.Min(MaxHealth, _health + rate * deltaTime);
            }
        }

        /// <summary>
        /// Advances stamina by one step. Returns whether sprinting is allowed
        /// to CONTINUE -- the caller asks for sprint, this decides.
        /// </summary>
        public bool TickStamina(bool wantsToSprint, float deltaTime)
        {
            bool sprinting = wantsToSprint && !_exhausted && _stamina > 0.0f;

            if (InfiniteStamina)
            {
                _stamina = MaxStamina;
                _exhausted = false;
                return wantsToSprint;
            }

            if (sprinting)
            {
                _stamina = Mathf.Max(0.0f, _stamina - StaminaDrainPerSecond * deltaTime);
                if (_stamina <= 0.0f)
                {
                    _exhausted = true;
                    sprinting = false;
                }
            }
            else if (StaminaRegenDelayRemaining > 0.0f)
            {
                // SOULS-STYLE: spending stamina on a combat action pauses
                // recovery briefly, so chaining actions back to back is
                // what drains the bar rather than a free trickle refill.
                StaminaRegenDelayRemaining = Mathf.Max(0.0f, StaminaRegenDelayRemaining - deltaTime);
            }
            else
            {
                float regen = StaminaRegenPerSecondOverride > 0.0f
                    ? StaminaRegenPerSecondOverride
                    : StaminaRegenPerSecond;
                regen *= Stats.Increase(StatId.StaminaRegen);
                _stamina = Mathf.Min(MaxStamina, _stamina + regen * deltaTime);
                if (_exhausted && _stamina >= MaxStamina * ExhaustedRecoverFraction)
                {
                    _exhausted = false;
                }
            }

            return sprinting;
        }

        /// <summary>
        /// Seconds after a combat spend before stamina starts coming back.
        /// Zero unless something called <see cref="TrySpendCombatStamina"/> or
        /// <see cref="DelayStaminaRegen"/>, so plain sprinting behaves exactly
        /// as it always has.
        /// </summary>
        public float StaminaRegenDelayRemaining { get; private set; }

        /// <summary>
        /// When above zero, replaces StaminaRegenPerSecond. The sword combat
        /// sets this to Dark Souls' 45/s while a sword is equipped and clears
        /// it again on unequip; zero means "use the normal rate".
        /// </summary>
        public float StaminaRegenPerSecondOverride;

        /// <summary>Holds stamina recovery off for at least this long.</summary>
        public void DelayStaminaRegen(float seconds)
        {
            StaminaRegenDelayRemaining = Mathf.Max(StaminaRegenDelayRemaining, seconds);
        }

        /// <summary>
        /// Spends stamina the SOULS way: an action is allowed as long as ANY
        /// stamina is left, even if the cost is bigger than what remains --
        /// the bar just bottoms out (and exhausts), which is the risk of
        /// swinging on empty. Refused only when already at zero/exhausted.
        /// TrySpendStamina, by contrast, refuses whenever the cost exceeds the
        /// bar, which is right for a dash but wrong for "one more swing".
        /// </summary>
        public bool TrySpendCombatStamina(float cost, float regenDelaySeconds)
        {
            if (InfiniteStamina)
            {
                return true;
            }
            if (_exhausted || _stamina <= 0.0f)
            {
                return false;
            }
            _stamina = Mathf.Max(0.0f, _stamina - cost);
            if (_stamina <= 0.0f)
            {
                _exhausted = true;
            }
            DelayStaminaRegen(regenDelaySeconds);
            return true;
        }

        /// <summary>
        /// While true, a hit that lands on a raised block drains stamina
        /// instead of only being reduced -- Souls' guard. Set by the sword
        /// combat while the block button is held.
        /// </summary>
        public bool BlockDrainsStamina;

        /// <summary>Stamina lost per point of damage absorbed by a held block.</summary>
        public float BlockStaminaPerDamage = 1.0f;

        /// <summary>How long a GUARD BREAK (block hit with too little stamina to hold) leaves you staggered.</summary>
        public float GuardBreakStunSeconds = 1.0f;

        /// <summary>Raised when a block is broken by a hit it could not afford.</summary>
        public event Action GuardBroken;

        /// <summary>Drops a raised block immediately (a held block is released, not timed out).</summary>
        public void EndBlock()
        {
            BlockRemaining = 0.0f;
        }

        /// <summary>Spends stamina if there is enough. Returns whether it was paid.</summary>
        public bool TrySpendStamina(float cost)
        {
            if (_exhausted || _stamina < cost)
            {
                return false;
            }
            _stamina -= cost;
            if (_stamina <= 0.0f)
            {
                _stamina = 0.0f;
                _exhausted = true;
            }
            return true;
        }

        /// <summary>
        /// Applies damage. <paramref name="blocking"/> lets most of it through
        /// at a quarter strength rather than negating it, matching the original.
        /// </summary>
        public void TakeDamage(float amount, bool blocking = false)
        {
            if (IsDead || amount <= 0.0f || InfiniteHealth)
            {
                return;
            }

            float applied = blocking ? amount * CombatTuning.BlockDamageMultiplier : amount;
            _health = Mathf.Max(0.0f, _health - applied);
            _healthRegenDelay = HealthRegenDelaySeconds;
            Damaged?.Invoke(applied);

            if (_health <= 0.0f)
            {
                Died?.Invoke();
            }
        }

        /// <summary>
        /// Called every frame a jetpack is actively thrusting -- continuously
        /// refreshes TickFallDamage's own peak-height reference to wherever
        /// the player currently is, so letting go afterward only ever
        /// measures the genuine unpowered fall from THAT point, never the
        /// whole powered climb. No jetpack exists to port this from; this is
        /// the direct mechanical reading of the explicit ask ("it negates
        /// fall damage from the last height it was on").
        /// </summary>
        public void ResetFallTracking(float currentY)
        {
            _wasAirborne = true;
            _highestAirborneY = currentY;
        }

        private bool _fallForgiven;
        private float _fallForgivenAboveY;

        /// <summary>
        /// A SUPER JUMP DOES NOT HURT ITS OWN LANDING (2026-10-14): a full
        /// charge rises about 15 m, above the safe fall, so every leap would
        /// cost a fifth of the player's health. The rise of the jump about to
        /// leave <paramref name="takeOffY"/> is forgiven: the next landing
        /// counts the fall from no higher than the take-off, so a leap back
        /// down to the same street is free and one off a roof still costs
        /// what stepping off that roof would.
        /// </summary>
        public void ForgiveFallAbove(float takeOffY)
        {
            _fallForgiven = true;
            _fallForgivenAboveY = takeOffY;
        }

        /// <summary>
        /// Watches height to work out fall damage, and applies it on landing.
        /// Returns the damage dealt, which is zero for a safe landing.
        ///
        /// TRACKS THE PEAK RATHER THAN READING VELOCITY. Landing speed alone
        /// cannot tell a long drop from a short one on a bouncy frame, and the
        /// Godot original had the same peak-tracking for the same reason.
        /// </summary>
        public float TickFallDamage(bool grounded, float currentY)
        {
            if (!grounded)
            {
                if (!_wasAirborne)
                {
                    _wasAirborne = true;
                    _highestAirborneY = currentY;
                }
                else
                {
                    _highestAirborneY = Mathf.Max(_highestAirborneY, currentY);
                }
                return 0.0f;
            }

            if (!_wasAirborne)
            {
                return 0.0f;
            }
            _wasAirborne = false;

            float top = _highestAirborneY;
            if (_fallForgiven)
            {
                top = Mathf.Min(top, _fallForgivenAboveY);
                _fallForgiven = false;
            }
            float fallen = top - currentY;
            if (fallen <= SafeFallDistance)
            {
                return 0.0f;
            }

            float damage = (fallen - SafeFallDistance) * (BaseMaxHealth * FallDamagePerMetreFraction);
            TakeDamage(damage);
            return damage;
        }

        /// <summary>
        /// Fall damage's own formula, applied to a jetpack crash into a wall
        /// instead of a drop -- EXPLICIT ASK: "horizontal fall damage when
        /// using the jetpack and you crash into a wall... calculated same
        /// as fall damage but instead horizontal."
        ///
        /// A fall's own damage is a function of the DISTANCE dropped, but a
        /// wall crash has no distance -- only a speed at the instant of
        /// impact -- so this converts that speed into the equivalent
        /// free-fall distance gravity would need to reach it (d = v^2 / 2g,
        /// the same physics a fall's own landing speed is derived from in
        /// reverse), then reuses TickFallDamage's own FallDamagePerMetreFraction
        /// against THAT distance -- one damage RATE, read either way, rather
        /// than a second unrelated formula with its own tuning to keep in
        /// step. The safe THRESHOLD itself is SafeCrashDistance, not
        /// SafeFallDistance directly -- see SafeCrashDistance's own doc
        /// comment for why a sideways slam does not get the same margin a
        /// two-storey drop onto your own feet does.
        /// </summary>
        public float ApplyCrashDamage(float horizontalSpeed)
        {
            float gravity = Physics.gravity.magnitude;
            if (gravity <= 0.0f)
            {
                return 0.0f;
            }
            float equivalentFallDistance = (horizontalSpeed * horizontalSpeed) / (2.0f * gravity);
            if (equivalentFallDistance <= SafeCrashDistance)
            {
                return 0.0f;
            }

            float damage = (equivalentFallDistance - SafeCrashDistance) * (BaseMaxHealth * FallDamagePerMetreFraction);
            TakeDamage(damage);
            return damage;
        }
    }
}
