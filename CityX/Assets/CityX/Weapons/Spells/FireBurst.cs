using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A fireball's burst where it lands (docs/FIRE.md, "The fire spells"), the way the reference's meteor detonates (achrefelouafi's
    /// LinearAbiltyCastingThreeJS, MIT), at a fireball's size: a flash of light falling off on a cubic (weapons/explosion.gd's reasoning: a cubic
    /// fall reads as a detonation, a linear one as a light being dimmed), the fire thrown out of it with sparks, embers, cinders and smoke
    /// (FireScene.Burst), a black scorch on what it hit with molten cracks cooling in it (FireMarks.ScorchOn), and the camera of a player near
    /// it nudged. A charged ball's is bigger in all of it. Everyone sees it: a copy of another player's fireball bursts the same way.
    /// </summary>
    public sealed class FireBurst : MonoBehaviour
    {
        /// <summary>How long the flash takes to fade out, in seconds.</summary>
        public const float FlashSeconds = 0.3f;

        private Light _flash;
        private float _flashStartIntensity;
        private float _flashElapsed;

        /// <summary>A burst at <paramref name="position"/> on a surface facing <paramref name="normal"/>; a charged fireball's is <paramref name="scale"/> times as big.</summary>
        public static FireBurst Spawn(GameObject prefab, Vector3 position, Vector3 normal, float scale = 1.0f)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector3 away = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            GameObject instance = Instantiate(prefab, position, Quaternion.LookRotation(away));
            if (!Mathf.Approximately(scale, 1.0f))
            {
                instance.transform.localScale = Vector3.one * scale;
                foreach (Light light in instance.GetComponentsInChildren<Light>(true))
                {
                    light.range *= scale;
                }
            }
            FireScene.Burst(position, away, scale);
            FireMarks.ScorchOn(position, away, SpellFire.ScorchMetres(scale), true);
            ThirdPersonCamera.ShakeNear(position, SpellFire.ShakeReach(scale), SpellFire.ShakeMetres(scale), SpellFire.ShakeSeconds);
            return instance.GetComponent<FireBurst>();
        }

        private void Awake()
        {
            _flash = GetComponentInChildren<Light>();
            _flashStartIntensity = _flash != null ? _flash.intensity : 0.0f;
        }

        private void Update()
        {
            _flashElapsed += Time.deltaTime;
            if (_flash != null)
            {
                float t = Mathf.Clamp01(_flashElapsed / FlashSeconds);
                _flash.intensity = _flashStartIntensity * Mathf.Pow(1.0f - t, 3.0f);
            }
            // Gone with its flash: the fire it threw out burns on in the fire's own look, which is not this object's.
            if (_flashElapsed >= FlashSeconds)
            {
                Destroy(gameObject);
            }
        }
    }
}
