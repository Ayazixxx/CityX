using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A flash of magenta where an orb strikes or the ball lands (or, in another palette, a spell's own light: the Magic Wand's speck's white): a
    /// hot spot swelling and fading, a ring of sparks thrown out, a light that falls off on a cube (a detonation, not a dimmer, as FireBurst's),
    /// and a sound. Standing on its own in the world, gone when spent.
    /// </summary>
    public sealed class ArcaneBurst : MonoBehaviour
    {
        public const float FlashSeconds = 0.3f;
        public const float LifetimeSeconds = 1.0f;

        private MeshRenderer _hot;
        private MeshRenderer _glow;
        private Light _light;
        private GlowPalette _palette;
        private float _size;
        private float _lightIntensity;
        private float _age;
        private MaterialPropertyBlock _block;

        /// <summary>Made on first use: a property block may not be made while the component is (a field initializer).</summary>
        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        /// <summary>
        /// A burst <paramref name="size"/> metres across at <paramref name="position"/> (no sound when <paramref name="sound"/> is null), in
        /// <paramref name="palette"/>'s light (the arcane by default).
        /// </summary>
        public static ArcaneBurst Spawn(Vector3 position, float size, AudioClip sound, float volume = 1.0f, GlowPalette palette = null)
        {
            palette ??= ArcaneVisuals.Arcane;
            var go = new GameObject("ArcaneBurst");
            go.transform.position = position;
            var burst = go.AddComponent<ArcaneBurst>();
            burst._size = size;
            burst._palette = palette;
            burst._glow = ArcaneVisuals.Sprite(go.transform, palette.Glow, size * 0.4f);
            burst._hot = ArcaneVisuals.Sprite(go.transform, palette.Hot, size * 0.2f, "Hot");
            ParticleSystem sparks = ArcaneVisuals.Motes(go.transform, 0.0f, size * 0.1f, size * 4.0f, size * 0.08f, 0.5f, palette);
            sparks.Emit(Mathf.Clamp(Mathf.RoundToInt(size * 18.0f), 8, 40));
            burst._lightIntensity = 5.0f;
            burst._light = ArcaneVisuals.Light(go.transform, burst._lightIntensity, size * 5.0f, palette.Colour);
            if (sound != null && Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(sound, position, volume);
            }
            return burst;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / FlashSeconds);
            float fade = Mathf.Pow(1.0f - t, 3.0f);
            if (_glow != null)
            {
                _glow.transform.localScale = Vector3.one * (_size * (0.4f + 0.9f * t));
                ArcaneVisuals.Fade(_glow, _palette.Colour, 1.0f - t, Block);
            }
            if (_hot != null)
            {
                _hot.transform.localScale = Vector3.one * (_size * (0.2f + 0.5f * t));
                ArcaneVisuals.Fade(_hot, _palette.HotColour, fade, Block);
            }
            if (_light != null)
            {
                _light.intensity = _lightIntensity * fade;
                _light.enabled = t < 1.0f;
            }
            if (_age >= LifetimeSeconds)
            {
                Destroy(gameObject);
            }
        }
    }
}
