using UnityEngine;

namespace CityX.Character
{
    /// <summary>A stump spurting blood along its own up: a spray every <see cref="SpurtSeconds"/>, weaker each time, for <see cref="BleedSeconds"/>.</summary>
    public sealed class StumpBleed : MonoBehaviour
    {
        public const float BleedSeconds = 3.5f;
        public const float SpurtSeconds = 0.28f;

        private float _age;
        private float _nextSpurt;

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= BleedSeconds)
            {
                enabled = false;
                return;
            }
            if (_age < _nextSpurt)
            {
                return;
            }
            _nextSpurt = _age + SpurtSeconds;
            BloodBurst.Spawn(transform.position, transform.up, Mathf.Lerp(1.0f, 0.3f, _age / BleedSeconds));
        }
    }
}
