using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The poison gas ability: press 4 and the character blows out a cloud of purple gas down the crosshair. Whatever it touches is poisoned for ten
    /// seconds (see PoisonEffect and PoisonGasCloud); the caster never is. An innate ability like the fireball: nothing to equip.
    ///
    /// MULTIPLAYER FROM THE START (IReplicatedAbility): the owner's copy raises Fired as the cloud leaves; every other player's copy of the body
    /// shows a cosmetic cloud (the same look and drift, poisons nothing), the sound, and the raised-hand pose.
    /// </summary>
    public sealed class PoisonGasCaster : MonoBehaviour, IPoisonGasCaster, IReplicatedAbility
    {
        public const string Id = "poison-gas";

        string IReplicatedAbility.AbilityId => Id;

        /// <summary>Raised on the owner's copy as the cloud leaves the mouth. See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

        [Tooltip("Seconds between casts.")]
        public float CooldownSeconds = 6.0f;

        [Tooltip("How long the blowing-out pose holds.")]
        public float CastPoseSeconds = 0.6f;

        [Tooltip("How fast the cloud leaves, in metres per second (it slows to a drift almost at once).")]
        public float CloudSpeed = 11.0f;

        /// <summary>How far in front of the origin the cloud appears: past the caster's own body.</summary>
        public const float SpawnForwardOffset = 0.9f;

        public GameObject CloudPrefab;

        private float _cooldown;
        private float _castPoseRemaining;
        private PlayerVitals _vitals;
        private AudioSource _audio;

        public bool IsCasting => _castPoseRemaining > 0.0f;

        public float CastFraction => CastPoseSeconds > 0.0f ? Mathf.Clamp01(_castPoseRemaining / CastPoseSeconds) : 0.0f;

        public bool CanCast => _cooldown <= 0.0f && (_vitals == null || !_vitals.IsDead);

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, callable by hand where Awake never runs (the same idiom the other casters use).</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
        }

        public void Tick(float deltaTime)
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= deltaTime;
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
            _cooldown = CooldownSeconds;
            _castPoseRemaining = CastPoseSeconds;

            Vector3 forward = direction.normalized;
            Vector3 spawnAt = origin + forward * SpawnForwardOffset;
            int strength = _vitals != null ? _vitals.Strength : 0;
            PoisonGasCloud.Spawn(CloudPrefab, spawnAt, forward * CloudSpeed, strength, ignoreColliders);
            PlayHiss();
            Fired?.Invoke(new AbilityEvent(0, spawnAt, forward, CloudSpeed, CastPoseSeconds));
            return true;
        }

        /// <summary>
        /// Another player's cloud, shown on this copy of their body: the cloud itself (same look and drift, but it poisons nothing), the hiss at
        /// their mouth and the raised hand.
        /// </summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float speed = evt.A > 0.0f ? evt.A : CloudSpeed;
            PoisonGasCloud.Spawn(CloudPrefab, evt.Origin, evt.Direction.normalized * speed, 0, GetComponentsInChildren<Collider>(), cosmetic: true);
            RemoteAbilityPose.Show(gameObject, 0.0f, evt.B > 0.0f ? evt.B : CastPoseSeconds);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(PoisonGasSounds.Hiss, evt.Origin, 0.6f);
            }
        }

        private void PlayHiss()
        {
            if (_audio == null)
            {
                var holder = new GameObject("PoisonGasAudio");
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
