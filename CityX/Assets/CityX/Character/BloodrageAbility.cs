using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// BLOODRAGE (key 8): for 7.5 seconds the body moves 25% faster and every melee blow (fists, a sword, a thrown-away bottle's swing) hits 25%
    /// harder, its walking, running and jumping animations are replaced by the Bloodrage ones, and a red aura rises off it. When the time is up
    /// the animations go back and the aura fades away.
    ///
    /// AN INNATE ABILITY like the fireball: nothing has to be equipped. The rules are numbers other systems READ (<see cref="SpeedFactor"/> by
    /// PlayerLocomotion, <see cref="DamageFactor"/> by MeleeAttacker and SoulsSwordCombat), so nothing here reaches into them.
    ///
    /// THE ANIMATIONS ARE SWAPPED WITH AN OVERRIDE CONTROLLER made once, at Awake, over the player's own: from then on the Animator always runs
    /// the override, which at rest is identical to the base. Bloodrage only changes which clip stands in for walking, running and jumping (one
    /// ApplyOverrides call, which keeps the animator in the state it was in), and ending it puts them back. Every other state (the sword's,
    /// the gun's, climbing) is untouched.
    /// </summary>
    public sealed class BloodrageAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "bloodrage";

        string IReplicatedAbility.AbilityId => Id;

        /// <summary>Raised on the owner's copy when Bloodrage starts (A is how long it lasts). See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

        public const float DurationSeconds = 7.5f;
        public const float SpeedMultiplier = 1.25f;
        public const float DamageMultiplier = 1.25f;

        /// <summary>How long the aura takes to build up when it starts, and to die away when it ends.</summary>
        public const float AuraFadeInSeconds = 0.35f;
        public const float AuraFadeOutSeconds = 1.4f;

        [Tooltip("The clips Bloodrage replaces (the very clips the player's animator controller uses for walking, running and jumping), and what replaces each, same order.")]
        public AnimationClip[] OriginalClips = new AnimationClip[0];

        public AnimationClip[] BloodrageClips = new AnimationClip[0];

        [Tooltip("The red aura: a particle system and a light, started when Bloodrage does and faded when it ends.")]
        public GameObject AuraPrefab;

        private Animator _animator;
        private AnimatorOverrideController _override;
        private PlayerVitals _vitals;
        private float _remaining;
        private float _auraIntensity;
        private GameObject _aura;
        private ParticleSystem[] _auraParticles = new ParticleSystem[0];
        private float[] _auraBaseRates = new float[0];
        private Light[] _auraLights = new Light[0];
        private float[] _auraBaseLightIntensity = new float[0];

        public bool IsActive => _remaining > 0.0f;

        public float Remaining => Mathf.Max(0.0f, _remaining);

        /// <summary>1 + the speed bonus while Bloodrage lasts.</summary>
        public float SpeedFactor => IsActive ? SpeedMultiplier : 1.0f;

        /// <summary>1 + the melee damage bonus while Bloodrage lasts.</summary>
        public float DamageFactor => IsActive ? DamageMultiplier : 1.0f;

        /// <summary>0 to 1: how strong the aura is right now (it outlasts the effect by its fade).</summary>
        public float AuraIntensity => _auraIntensity;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent so an EditMode test can call it where Awake never runs.</summary>
        public void Bind()
        {
            if (_vitals == null)
            {
                _vitals = GetComponent<PlayerVitals>();
            }
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
            if (_override == null && _animator != null && _animator.runtimeAnimatorController != null
                && !(_animator.runtimeAnimatorController is AnimatorOverrideController))
            {
                _override = new AnimatorOverrideController(_animator.runtimeAnimatorController) { name = "BloodrageOverride" };
                _animator.runtimeAnimatorController = _override;
            }
        }

        public bool CanActivate => !IsActive && (_vitals == null || !_vitals.IsDead);

        /// <summary>Starts Bloodrage. False if it is already running or the body is dead.</summary>
        public bool TryActivate()
        {
            if (!CanActivate)
            {
                return false;
            }
            Bind();
            _remaining = DurationSeconds;
            SwapAnimations(true);
            StartAura();
            Fired?.Invoke(new AbilityEvent(0, transform.position, Vector3.zero, DurationSeconds));
            return true;
        }

        /// <summary>
        /// Another player's Bloodrage, shown on this copy of their body: the Bloodrage walk, run and jump and the red aura for as long as theirs
        /// lasts (this component's own Update counts it down and puts everything back, as on the owner). Changes no speed or damage here:
        /// those are the owner's, read by the owner's movement and melee.
        /// </summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (IsActive)
            {
                return;
            }
            Bind();
            float seconds = evt.A > 0.0f ? Mathf.Min(evt.A, 60.0f) : DurationSeconds;
            _remaining = seconds;
            SwapAnimations(true);
            StartAura();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Counts the effect down and eases the aura; public so a test can step it without frames.</summary>
        public void Tick(float deltaTime)
        {
            if (_remaining > 0.0f)
            {
                _remaining -= deltaTime;
                if (_remaining <= 0.0f)
                {
                    _remaining = 0.0f;
                    SwapAnimations(false);
                }
            }

            float target = IsActive ? 1.0f : 0.0f;
            float rate = IsActive ? 1.0f / AuraFadeInSeconds : 1.0f / AuraFadeOutSeconds;
            _auraIntensity = Mathf.MoveTowards(_auraIntensity, target, rate * deltaTime);
            ApplyAura();

            if (_aura != null && !IsActive && _auraIntensity <= 0.0f)
            {
                Destroy(_aura);
                _aura = null;
                _auraParticles = new ParticleSystem[0];
                _auraLights = new Light[0];
            }
        }

        private void SwapAnimations(bool on)
        {
            if (_override == null)
            {
                return;
            }
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            _override.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip original = pairs[i].Key;
                int index = System.Array.IndexOf(OriginalClips, original);
                if (index >= 0 && index < BloodrageClips.Length && BloodrageClips[index] != null)
                {
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, on ? BloodrageClips[index] : null);
                }
            }
            _override.ApplyOverrides(pairs);
        }

        private void StartAura()
        {
            if (AuraPrefab == null)
            {
                return;
            }
            if (_aura == null)
            {
                _aura = Instantiate(AuraPrefab, transform);
                _aura.transform.localPosition = Vector3.zero;
                _aura.transform.localRotation = Quaternion.identity;
                _auraParticles = _aura.GetComponentsInChildren<ParticleSystem>();
                _auraBaseRates = new float[_auraParticles.Length];
                for (int i = 0; i < _auraParticles.Length; i++)
                {
                    _auraBaseRates[i] = _auraParticles[i].emission.rateOverTime.constant;
                }
                _auraLights = _aura.GetComponentsInChildren<Light>();
                _auraBaseLightIntensity = new float[_auraLights.Length];
                for (int i = 0; i < _auraLights.Length; i++)
                {
                    _auraBaseLightIntensity[i] = _auraLights[i].intensity;
                }
            }
            foreach (ParticleSystem particles in _auraParticles)
            {
                if (!particles.isPlaying)
                {
                    particles.Play();
                }
            }
        }

        private void ApplyAura()
        {
            for (int i = 0; i < _auraParticles.Length; i++)
            {
                if (_auraParticles[i] == null)
                {
                    continue;
                }
                ParticleSystem.EmissionModule emission = _auraParticles[i].emission;
                emission.rateOverTime = _auraBaseRates[i] * _auraIntensity;
            }
            for (int i = 0; i < _auraLights.Length; i++)
            {
                if (_auraLights[i] != null)
                {
                    _auraLights[i].intensity = _auraBaseLightIntensity[i] * _auraIntensity;
                }
            }
        }
    }
}
