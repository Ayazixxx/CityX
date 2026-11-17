using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE BASEBALL BAT'S SIZE, set by the owner in the editor (2026-11-16: "let me adjust the baseball bat size in unity"): the Baseball Bat Size slider on the
    /// WeaponGrips asset, also on the bat's own Inspector. The model grows or shrinks about the bottom hand, so the hands stay on the handle and the knob stays
    /// just under the bottom fist, and a swing's reach follows the barrel's end. Read every frame, so dragging the slider while the game runs shows at once, on
    /// the bat in hand, on the ground and in other players' hands alike (every copy is this prefab and reads the same asset).
    /// </summary>
    public sealed class BatSize : MonoBehaviour
    {
        [SerializeField] private Transform _model;

        /// <summary>How far the barrel's end stands above the root at the size the bat was built at (BaseballBatBuilder).</summary>
        [SerializeField] private float _builtReach;

        private SwordWeapon _weapon;
        private float _applied = float.NaN;

        public Transform Model { get => _model; set => _model = value; }

        public float BuiltReach { get => _builtReach; set => _builtReach = value; }

        /// <summary>Where the model grows from, in the bat root's space: the bottom hand on the handle.</summary>
        public static Vector3 Pivot => new Vector3(0.0f, -SwordWeapon.DefaultSupportGripDown, 0.0f);

        /// <summary>How far the barrel's end stands above the root at <paramref name="size"/>.</summary>
        public static float ReachAt(float builtReach, float size) => Pivot.y + (builtReach - Pivot.y) * size;

        private void Awake()
        {
            Apply();
        }

        private void Update()
        {
            if (WeaponGripSettings.Current.BaseballBatSize != _applied)
            {
                Apply();
            }
        }

        /// <summary>The model and the reach at the size the settings say now.</summary>
        public void Apply()
        {
            float size = Mathf.Clamp(WeaponGripSettings.Current.BaseballBatSize, WeaponGripSettings.SmallestSize, WeaponGripSettings.LargestSize);
            _applied = WeaponGripSettings.Current.BaseballBatSize;
            if (_model != null)
            {
                _model.localScale = Vector3.one * size;
                _model.localPosition = Pivot * (1.0f - size);
            }
            if (_weapon == null)
            {
                _weapon = GetComponent<SwordWeapon>();
            }
            if (_weapon != null)
            {
                _weapon.BladeLength = ReachAt(_builtReach, size);
            }
        }
    }
}
