using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The sword's crescent wave (a Getsuga Tensho style slash): press 7 holding the sword, the sword swings, and a crescent of light leaves the
    /// blade down the crosshair, like the fireball spell but a different animation and look. PlayerInputReader only offers the key while the
    /// sword is in hand.
    /// </summary>
    public sealed class SwordWaveCaster : MonoBehaviour, ISwordWaveCaster
    {
        public float CooldownSeconds = 1.2f;

        /// <summary>How long after the swing starts the crescent leaves the blade: the middle of the slash.</summary>
        public float ReleaseDelaySeconds = 0.18f;

        public const float WaveSpeed = 34.0f;

        public const float SpawnForwardOffset = 0.9f;

        public GameObject WavePrefab;

        private float _cooldown;
        private float _releaseIn = -1.0f;
        private float _chestHeight;
        private Vector3 _direction;
        private Collider[] _ignore;
        private PlayerVitals _vitals;

        public bool CanCast => _cooldown <= 0.0f && _releaseIn < 0.0f && (_vitals == null || !_vitals.IsDead);

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
        }

        public void Tick(float deltaTime)
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= deltaTime;
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

        public bool TryCast(float chestHeight, Vector3 direction, Collider[] ignoreColliders)
        {
            if (!CanCast || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            _cooldown = CooldownSeconds;
            _releaseIn = ReleaseDelaySeconds;
            _chestHeight = chestHeight;
            _direction = direction.normalized;
            _ignore = ignoreColliders;
            return true;
        }

        private void Release()
        {
            int strength = _vitals != null ? _vitals.Strength : 0;
            // From where the body is when the blade comes through, along the direction the crosshair had when the key was pressed.
            Vector3 origin = transform.position + Vector3.up * _chestHeight;
            SwordWaveProjectile.Spawn(WavePrefab, origin + _direction * SpawnForwardOffset, _direction * WaveSpeed, strength, _ignore);
        }
    }
}
