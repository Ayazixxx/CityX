using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// POISON BREATH: press 4 and the character SCREAMS (the Scream animation, on the upper body so the legs keep working) and a cloud of purple gas
    /// is breathed out of their MOUTH, aimed at the crosshair. Whatever it touches is poisoned for ten seconds (see PoisonEffect and PoisonGasCloud); the
    /// caster never is. An innate ability like the fireball: nothing to equip.
    ///
    /// THE TIMING comes from the clip (2.8 s, played at <see cref="AnimationSpeed"/>): the head tilts back to breathe in, then snaps forward and down
    /// about 1.05 s in (of clip time). The gas leaves then, from the mouth (found from the head bone: the way the head faces, a little below its
    /// centre), and is aimed at where the crosshair is AT THAT MOMENT.
    ///
    /// MULTIPLAYER FROM THE START (IReplicatedAbility): the scream is replicated with the rest of the animations (the same timed-state sync the
    /// sword swings use), and the owner's copy raises Fired as the gas leaves the mouth, so every other player's copy shows a cosmetic cloud (the same
    /// look and drift, poisons nothing) and the hiss, in time with the scream they are already watching.
    /// </summary>
    public sealed class PoisonBreathCaster : MonoBehaviour, IPoisonBreathCaster, IReplicatedAbility
    {
        public const string Id = "poison-breath";

        /// <summary>The Animator state (on the torso+head layer) that plays the scream.</summary>
        public const string AnimationState = "PoisonBreath";

        /// <summary>The same scream on the arms' own layer.</summary>
        public const string ArmsAnimationState = "PoisonBreathArms";

        string IReplicatedAbility.AbilityId => Id;

        /// <summary>Raised on the owner's copy as the gas leaves the mouth. See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

        [Tooltip("Seconds between casts, counted from the press.")]
        public float CooldownSeconds = 6.0f;

        [Tooltip("How fast the scream plays (1 is the clip's own pace: 2.8 s). Timing only: the animator state itself is baked at 1.5.")]
        public float AnimationSpeed = 1.5f;

        [Tooltip("The scream clip's own length in seconds, at speed 1.")]
        public float ClipSeconds = 2.8f;

        [Tooltip("Seconds into the scream, at speed 1, when the head snaps forward and the gas leaves (measured from the clip).")]
        public float ReleaseClipSeconds = 1.05f;

        [Tooltip("How fast the cloud leaves, in metres per second (it slows to a drift almost at once).")]
        public float CloudSpeed = 11.0f;

        [Tooltip("How far in front of the face the mouth is, and how far below the head bone's centre, in metres (the head bone's own frame).")]
        public Vector3 MouthOffset = new Vector3(0.0f, -0.03f, 0.16f);

        /// <summary>How far past the mouth the cloud appears, so it does not start inside the head.</summary>
        public const float SpawnForwardOffset = 0.3f;

        public GameObject CloudPrefab;

        private float _cooldown;
        private float _castRemaining;
        private float _releaseIn = -1.0f;
        private Vector3 _fallbackOrigin;
        private Vector3 _fallbackDirection = Vector3.forward;
        private Vector3 _aimPoint;
        private bool _hasAimPoint;
        private Collider[] _ignore;
        private PlayerVitals _vitals;
        private PlayerAnimationDriver _driver;
        private Animator _animator;
        private AudioSource _audio;

        /// <summary>True from the press until the scream is over.</summary>
        public bool IsCasting => _castRemaining > 0.0f;

        /// <summary>The scream is its own animation, so there is no raised-arm pose to hold.</summary>
        public float CastFraction => 0.0f;

        public bool CanCast => _cooldown <= 0.0f && _releaseIn < 0.0f && (_vitals == null || !_vitals.IsDead);

        /// <summary>Seconds from the press to the gas leaving the mouth.</summary>
        public float ReleaseSeconds => ReleaseClipSeconds / Mathf.Max(0.1f, AnimationSpeed);

        /// <summary>Seconds the whole scream takes at this speed.</summary>
        public float ScreamSeconds => ClipSeconds / Mathf.Max(0.1f, AnimationSpeed);

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, callable by hand where Awake never runs (the same idiom the other casters use).</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _driver = GetComponent<PlayerAnimationDriver>();
            _animator = GetComponentInChildren<Animator>();
        }

        public void Tick(float deltaTime)
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= deltaTime;
            }
            if (_castRemaining > 0.0f)
            {
                _castRemaining -= deltaTime;
            }
            if (_releaseIn >= 0.0f)
            {
                _releaseIn -= deltaTime;
                if (_releaseIn < 0.0f)
                {
                    Release();
                }
            }
        }

        public bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders)
        {
            if (!CanCast || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            // Cooldown Reduction shortens it (2026-10-14).
            _cooldown = CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);
            _fallbackOrigin = origin;
            _fallbackDirection = direction.normalized;
            _ignore = ignoreColliders;
            _hasAimPoint = false;

            if (_driver != null && _driver.HasState(AnimationState))
            {
                // Cast Speed screams it faster (2026-10-14): the clip, the wind-up before the gas leaves and the whole cast, alike. The speed
                // goes to other players with the scream (PlayLocalState announces it).
                float castSpeed = PlayerVitals.SpeedOf(_vitals, StatId.CastSpeed);
                _driver.PlayLocalState(AnimationState, AnimationSpeed * castSpeed, 0.0f);
                // The arms do the scream too, on their own layer, unless something else (a sword swing) is using them.
                if (_driver.HasState(ArmsAnimationState))
                {
                    _driver.PlayLocalState(ArmsAnimationState, AnimationSpeed * castSpeed, 0.0f);
                }
                _castRemaining = ScreamSeconds / castSpeed;
                _releaseIn = ReleaseSeconds / castSpeed;
            }
            else
            {
                // No scream to play (a body with no animator, or a controller without the state): the breath just leaves.
                _castRemaining = 0.0f;
                Release();
            }
            return true;
        }

        /// <summary>A stagger cuts the scream short before the gas leaves (Stability, 2026-10-14): no gas, and the cast is over.</summary>
        public void Interrupt()
        {
            if (_releaseIn >= 0.0f)
            {
                _releaseIn = -1.0f;
                _castRemaining = 0.0f;
            }
        }

        public void Aim(Vector3 aimPoint)
        {
            if (_releaseIn >= 0.0f)
            {
                _aimPoint = aimPoint;
                _hasAimPoint = true;
            }
        }

        /// <summary>Where the mouth is right now, in world space: the head bone, moved out along the way the head faces. Falls back to <paramref name="fallback"/>.</summary>
        public Vector3 MouthPosition(Vector3 fallback)
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
            Transform head = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            return head != null ? head.position + head.rotation * MouthOffset : fallback;
        }

        private void Release()
        {
            _releaseIn = -1.0f;
            Vector3 mouth = MouthPosition(_fallbackOrigin);
            Vector3 direction = _hasAimPoint && (_aimPoint - mouth).sqrMagnitude > 0.01f ? (_aimPoint - mouth).normalized : _fallbackDirection;
            Vector3 spawnAt = mouth + direction * SpawnForwardOffset;
            // A spell: Spell Power makes the poison stronger (2% a point, 2026-10-14).
            float spellPower = PlayerVitals.SpellPowerOf(_vitals);
            PoisonGasCloud.Spawn(CloudPrefab, spawnAt, direction * CloudSpeed, spellPower, _ignore);
            PlayHiss();
            Fired?.Invoke(new AbilityEvent(0, spawnAt, direction, CloudSpeed));
        }

        /// <summary>
        /// Another player's breath, shown on this copy of their body: the cloud itself (same look and drift, but it poisons nothing) and the hiss
        /// at their mouth. Their scream arrives with the rest of their animations.
        /// </summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float speed = evt.A > 0.0f ? evt.A : CloudSpeed;
            PoisonGasCloud.Spawn(CloudPrefab, evt.Origin, evt.Direction.normalized * speed, 0, GetComponentsInChildren<Collider>(), cosmetic: true);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(PoisonGasSounds.Hiss, evt.Origin, 0.6f);
            }
        }

        private void PlayHiss()
        {
            if (_audio == null)
            {
                var holder = new GameObject("PoisonBreathAudio");
                holder.transform.SetParent(transform, false);
                _audio = holder.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0.0f;   // the sound of THIS body, heard from inside it
                _audio.volume = 0.6f;
            }
            _audio.PlayOneShot(PoisonGasSounds.Hiss);
        }
    }
}
