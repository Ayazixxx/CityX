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
            TakeDamage(amount, false);
        }

        // --- tuning, ported from player.gd ---

        public const float BaseMaxHealth = 100.0f;
        public const float BaseMaxStamina = 130.0f;
        public const float StaminaDrainPerSecond = 20.0f;
        public const float StaminaRegenPerSecond = 15.6f;

        /// <summary>
        /// player.gd STAMINA_EXHAUSTED_RECOVER_FRACTION. Once stamina bottoms
        /// out you cannot sprint again until this much has come back -- without
        /// it, sprint stutters on and off one frame at a time at zero.
        /// </summary>
        public const float ExhaustedRecoverFraction = 0.2f;

        public const float HealthPerStatPoint = 10.0f;
        public const float StaminaPerStatPoint = 10.0f;

        /// <summary>player.gd STAT_POINTS_PER_LEVEL -- granted on every level-up.</summary>
        public const int StatPointsPerLevel = 5;

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
        /// <summary>
        /// Shown on the HUD badge. Nothing raises it yet -- XP and the skill
        /// tree are not ported -- but it lives here rather than being a
        /// hardcoded "1" in the HUD, so that when progression does land there
        /// is one number to set and no display quietly left behind.
        /// </summary>
        public int Level = 1;

        public int Vitality;

        public int Endurance;
        public int Strength;

        /// <summary>Earned but not yet spent on Vitality/Endurance/Strength.</summary>
        public int StatPoints;

        /// <summary>Progress toward the next level. Reset (not cleared) on every level-up.</summary>
        public int Experience;

        /// <summary>player.gd xp_to_next_level -- a pure function of Level, so it can never drift out of sync with it.</summary>
        public int ExperienceToNextLevel => XpPerLevelBase * Level;

        // --- state ---

        [Header("State")]
        [SerializeField] private float _health = BaseMaxHealth;

        [SerializeField] private float _stamina = BaseMaxStamina;

        private bool _exhausted;
        private float _highestAirborneY;
        private bool _wasAirborne;

        public float MaxHealth => BaseMaxHealth + Vitality * HealthPerStatPoint;
        public float MaxStamina => BaseMaxStamina + Endurance * StaminaPerStatPoint;

        public float Health => _health;
        public float Stamina => _stamina;
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
            // Another player's copy: the hit is sent to the real body's owner, who decides what it does (block, stun, health).
            if (Relay != null && Relay.TryRelayHit(amount, fromPosition, hitstunSeconds))
            {
                return HitResponse.Landed;
            }

            if (IsDead || IsInvulnerable)
            {
                return HitResponse.Ignored;
            }

            bool blocked = IsBlocking;
            TakeDamage(amount, blocked);

            // SOULS GUARD: a held block pays for what it stops in stamina, and
            // a hit it cannot afford breaks the guard -- you eat the reduced
            // damage anyway and are staggered wide open. Only when the sword
            // combat has switched this on; the classic timed block is untouched.
            if (blocked && BlockDrainsStamina && !InfiniteStamina)
            {
                float drain = amount * BlockStaminaPerDamage;
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

            // A BLOCKED HIT DOES NOT STUN THE DEFENDER. Eating the hitstun as
            // well as a quarter of the damage would make blocking a worse dodge
            // rather than a punish -- and the punish is the whole point of
            // timing one.
            if (!blocked)
            {
                // Longest wins rather than newest. A weak jab landing during a
                // heavy stun should not cut it short.
                HitstunRemaining = Mathf.Max(HitstunRemaining, hitstunSeconds);
            }

            Hit?.Invoke(fromPosition);
            if (!blocked)
            {
                return HitResponse.Landed;
            }
            return BlockDrainsStamina ? HitResponse.Guarded : HitResponse.Blocked;
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
            _ragdoll = GetComponent<Ragdoll>();
        }

        /// <summary>Restores everything, for respawn.</summary>
        public void Revive()
        {
            _health = MaxHealth;
            _stamina = MaxStamina;
            _exhausted = false;
            _wasAirborne = false;
            HitstunRemaining = 0.0f;
            Revived?.Invoke();
        }

        /// <summary>
        /// Another player's copy: shows the owner's health (so a copy that has died ignores further hits, and anything that reads it sees the truth).
        /// Raises nothing and costs nothing; only the owner's own vitals ever decide damage.
        /// </summary>
        public void SetHealthFromNetwork(float health)
        {
            _health = Mathf.Clamp(float.IsNaN(health) ? _health : health, 0.0f, MaxHealth);
        }

        /// <summary>
        /// Grants XP, ported from player.gd's own add_xp(). Loops rather than
        /// a single if so one big grant can trigger several level-ups at
        /// once. Godot also awards a skill point every few levels here, but
        /// there is no skill tree on either side to spend it on (see
        /// PlayerSubmenuView's own doc comment), so that half is left out
        /// rather than silently building toward a system that does not exist.
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
            if (StatPoints <= 0)
            {
                return false;
            }

            float oldMaxHealth = MaxHealth;
            float oldMaxStamina = MaxStamina;

            switch (statName)
            {
                case "vitality": Vitality += 1; break;
                case "endurance": Endurance += 1; break;
                case "strength": Strength += 1; break;
                default: return false;
            }
            StatPoints -= 1;

            _health += MaxHealth - oldMaxHealth;
            _stamina += MaxStamina - oldMaxStamina;
            return true;
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

            float fallen = _highestAirborneY - currentY;
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
