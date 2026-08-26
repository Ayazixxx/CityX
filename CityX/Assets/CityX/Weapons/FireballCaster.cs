using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The fireball spell: press 1, aim the hand forward, launch a large
    /// fireball that bursts into flame on impact. No Godot equivalent to
    /// port -- an explicit new-mechanic ask, same footing as Jetpack.cs.
    ///
    /// AN INNATE ABILITY, NOT AN EQUIPPABLE ITEM -- unlike a gun or a sword,
    /// nothing has to be picked up or equipped first; the explicit ask
    /// ("when you press 1") describes a hotbar-style spell always available
    /// to the player. Lives in CityX.Weapons rather than CityX.Character
    /// (unlike MeleeAttacker/PlayerVitals, which ARE always-present player
    /// components) specifically so it can reference FireballProjectile
    /// directly -- see IFireballCaster's own doc comment for how
    /// PlayerInputReader still reaches this without Character depending on
    /// Weapons.
    ///
    /// EVERY RULE IS A FUNCTION OF A DELTA, matching MeleeAttacker's own
    /// class comment and for the same reason -- Tick counts the cooldown
    /// and the cast pose down every frame rather than using a coroutine or
    /// Invoke, so nothing here can get stuck the way Godot's own
    /// _sword_swing_in_progress flag repeatedly did (see ComboChain.Sword's
    /// own doc comment).
    /// </summary>
    public sealed class FireballCaster : MonoBehaviour, IFireballCaster
    {
        [Tooltip("Seconds between casts.")]
        public float CooldownSeconds = 1.5f;

        /// <summary>
        /// How long the aim-hand-forward pose holds after a cast -- purely
        /// cosmetic follow-through, not a windup: the fireball launches the
        /// instant TryCast succeeds, so a shorter or longer value here
        /// changes nothing about when damage happens.
        /// </summary>
        public float CastPoseSeconds = 0.4f;

        public const float FireballSpeed = 20.0f;

        /// <summary>
        /// How far in front of the spawn point the fireball actually
        /// appears -- far enough that it does not spawn overlapping the
        /// caster's own collider and immediately register a self-hit.
        /// </summary>
        public const float SpawnForwardOffset = 0.6f;

        public GameObject FireballPrefab;

        private float _cooldownRemaining;
        private float _castPoseRemaining;
        private PlayerVitals _vitals;

        public bool IsCasting => _castPoseRemaining > 0.0f;

        public bool CanCast => _cooldownRemaining <= 0.0f && (_vitals == null || !_vitals.IsDead);

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Public and idempotent, callable by hand in EditMode tests where
        /// Awake never runs -- the same idiom PlayerLocomotion.Bind/
        /// GrappleSwing.Bind/PlayerClimbing.Bind already use for the exact
        /// same reason.
        /// </summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownRemaining > 0.0f)
            {
                _cooldownRemaining -= deltaTime;
            }
            if (_castPoseRemaining > 0.0f)
            {
                _castPoseRemaining -= deltaTime;
            }
        }

        public bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders)
        {
            if (!CanCast || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            _cooldownRemaining = CooldownSeconds;
            _castPoseRemaining = CastPoseSeconds;

            int strength = _vitals != null ? _vitals.Strength : 0;
            Vector3 forward = direction.normalized;
            FireballProjectile.Spawn(
                FireballPrefab, origin + forward * SpawnForwardOffset,
                forward * FireballSpeed, strength, ignoreColliders);
            return true;
        }
    }
}
