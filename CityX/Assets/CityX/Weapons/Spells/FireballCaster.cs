using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The fireball spell: press 1, raise the right hand, launch a large
    /// fireball that bursts into flame on impact. No Godot equivalent to
    /// port -- an explicit new-mechanic ask, same footing as Jetpack.cs. The
    /// raised-hand visual itself lives in PlayerLocomotion.SetCastPose/
    /// ApplyCastingPose, driven by this class's own CastFraction -- a
    /// dedicated procedural pose, not a reuse of the gun's own aim-in
    /// animation the way an earlier version of this file's own wiring did.
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
    public sealed partial class FireballCaster : MonoBehaviour, IFireballCaster, IReplicatedAbility
    {
        public const string Id = "fireball";

        string IReplicatedAbility.AbilityId => Id;

        /// <summary>Raised on the owner's copy when a fireball leaves the hand: where, and which way, and how fast. See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

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
        /// What a cast pays: the Fireball Staff's plain shot, the same ball. It paid nothing at first, never meant (owner, 2026-10-30: "make
        /// fireball cost some mana that was unintended"; GAME_DESIGN.md: a spell pays mana).
        /// </summary>
        public const float SpellManaCost = 10.0f;

        /// <summary>
        /// How far in front of the spawn point the fireball actually
        /// appears -- far enough that it does not spawn overlapping the
        /// caster's own collider and immediately register a self-hit.
        /// </summary>
        public const float SpawnForwardOffset = 0.6f;

        public GameObject FireballPrefab;

        [Tooltip("fireball_cast.mp3 -- plays once per successful cast. Set directly by CityMassingSceneBuilder, since this component has no separate item builder of its own.")]
        public AudioSource CastAudio;

        private float _cooldownRemaining;
        private float _castPoseRemaining;
        private PlayerVitals _vitals;

        public bool IsCasting => _castPoseRemaining > 0.0f;

        public float CastFraction =>
            CastPoseSeconds > 0.0f ? Mathf.Clamp01(_castPoseRemaining / CastPoseSeconds) : 0.0f;

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
            _locomotion = GetComponent<PlayerLocomotion>();
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
            if (!CanCast || direction.sqrMagnitude < 0.0001f || (_vitals != null && !_vitals.TrySpendMana(SpellManaCost)))
            {
                return false;
            }

            // Cooldown Reduction shortens it (2026-10-14).
            _cooldownRemaining = CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);
            _castPoseRemaining = CastPoseSeconds;

            // PlayOneShot, not Play() -- matching PistolWeapon.ShotPlayer's
            // own reasoning: Play() restarts the SAME playback, so a cast
            // fired again before the clip finishes would cut itself off.
            // Never actually reachable at this spell's own 1.5s cooldown
            // against a short cast clip, but costs nothing to get right the
            // same way regardless.
            if (CastAudio != null && CastAudio.clip != null)
            {
                CastAudio.PlayOneShot(CastAudio.clip);
            }

            // A spell: Spell Power makes it hurt more (2% a point), and the caster's Armor Penetration goes with it (2026-10-14).
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            Vector3 forward = direction.normalized;
            FireballProjectile.Spawn(
                FireballPrefab, origin + forward * SpawnForwardOffset,
                forward * FireballSpeed, spellPower, ignoreColliders, penetration: penetration, striker: gameObject);
            Fired?.Invoke(new AbilityEvent(0, origin + forward * SpawnForwardOffset, forward, FireballSpeed));
            return true;
        }

        /// <summary>
        /// Another player's fireball, shown on this copy of their body: the raised hand (or, from the Fireball Staff, the staff thrust out), the
        /// cast sound at the hand and the fireball itself flying, as big as theirs (bursting where it lands, like the real one), but hurting
        /// nothing and starting no cooldown; and the staff's charge glowing while they hold it.
        /// </summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind == StaffChargeStarted || evt.Kind == StaffChargeStopped)
            {
                PlayRemoteCharge(evt.Kind == StaffChargeStarted);
                return;
            }
            if (evt.Direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector3 forward = evt.Direction.normalized;
            float speed = evt.A > 0.0f ? evt.A : FireballSpeed;
            float size = evt.B > 0.0f ? Mathf.Clamp(evt.B, 1.0f, StaffMostSize) : 1.0f;
            FireballProjectile.Spawn(FireballPrefab, evt.Origin, forward * speed, 0, GetComponentsInChildren<Collider>(), cosmetic: true, size: size);
            if (evt.N == FromStaff)
            {
                PlayRemoteStaffShot();
            }
            else
            {
                RemoteAbilityPose.Show(gameObject, 0.0f, CastPoseSeconds);
            }
            if (Application.isPlaying && CastAudio != null && CastAudio.clip != null)
            {
                AudioSource.PlayClipAtPoint(CastAudio.clip, evt.Origin);
            }
        }
    }
}
