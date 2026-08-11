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
    public sealed class PlayerVitals : MonoBehaviour
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

        /// <summary>
        /// player.gd SAFE_FALL_DISTANCE, which was two storeys expressed in the
        /// city's own floor height rather than a loose number. Kept derived for
        /// the same reason: change the floor height and this follows.
        /// </summary>
        public static float SafeFallDistance => World.CellLayout.NominalFloorMetres * 2.0f;

        /// <summary>player.gd FALL_DAMAGE_PER_METER, as a fraction of base health.</summary>
        public const float FallDamagePerMetreFraction = 0.08f;

        // --- stats ---

        [Header("Stats")]
        public int Vitality;

        public int Endurance;
        public int Strength;

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

        public event Action Died;
        public event Action<float> Damaged;

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
        }

        /// <summary>
        /// Advances stamina by one step. Returns whether sprinting is allowed
        /// to CONTINUE -- the caller asks for sprint, this decides.
        /// </summary>
        public bool TickStamina(bool wantsToSprint, float deltaTime)
        {
            bool sprinting = wantsToSprint && !_exhausted && _stamina > 0.0f;

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
            if (IsDead || amount <= 0.0f)
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
