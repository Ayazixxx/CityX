using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// AN EMPTY CAN DROPPED AFTER A DRINK (docs/PHIZZ_CAN.md): it falls and rolls where it was let go, lies about for a while, then shrinks away. Litter,
    /// on every machine its own: nothing is sent, nothing is hurt by it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DroppedCan : MonoBehaviour
    {
        /// <summary>How long it lies there, then how long it takes to shrink away, seconds.</summary>
        public const float LieSeconds = 30.0f;
        public const float ShrinkSeconds = 1.0f;

        private float _age;
        private Vector3 _size;

        private void Awake()
        {
            _size = transform.localScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age < LieSeconds)
            {
                return;
            }
            float left = 1.0f - (_age - LieSeconds) / ShrinkSeconds;
            if (left <= 0.0f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = _size * left;
        }
    }
}
