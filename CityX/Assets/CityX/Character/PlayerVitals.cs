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
    public sealed class PlayerVitals : MonoBehaviour, IDamageable
    {
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

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (IsDead || IsInvulnerable)
            {
                return HitResponse.Ignored;
            }

            bool blocked = IsBlocking;
            TakeDamage(amount, blocked);

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
            return blocked ? HitResponse.Blocked : HitResponse.Landed;
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
        }

        private void Awake()
        {
            _health = MaxHealth;
            _stamina = MaxStamina;
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
            else
            {
                _stamina = Mathf.Min(MaxStamina, _stamina + StaminaRegenPerSecond * deltaTime);
                if (_exhausted && _stamina >= MaxStamina * ExhaustedRecoverFraction)
                {
                    _exhausted = false;
                }
            }

            return sprinting;
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
    }
}
