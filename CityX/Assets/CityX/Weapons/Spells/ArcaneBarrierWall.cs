using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ARCANE BARRIER'S WALL (docs/ARCANE_BARRIER.md), drawn in code: a square of magenta aura standing before its caster, see-through (a faint
    /// fill, additive, so the street shows through it tinted) with a bright rim, slow ripples drifting up its face, motes rising off its bottom
    /// edge and a soft light. A child of the caster's body (so it is the body's size: Grow and Shrink scale it with the body), its collider stops
    /// shots and bodies, and whatever strikes it resolves to the caster (GetComponentInParent), whose PlayerVitals hands the blow to the barrier
    /// (<see cref="IBarrier"/>). Struck, a ring spreads where it was hit and the face flares; weakened, it dims and flickers. Let go or broken it
    /// FADES AWAY where it stood (owner: "i dont want the arcane barrier to shatter make it fade away"): the collider goes at once, the light
    /// ebbs, the wall swells a little as it goes. The same on every machine.
    /// </summary>
    public sealed class ArcaneBarrierWall : MonoBehaviour
    {
        /// <summary>The shimmer drifting up the face, as a share of the face's strength.</summary>
        public const float ShimmerStrength = 0.45f;

        /// <summary>How dim the face gets as its health runs out (at none, this share of full).</summary>
        public const float WeakestStrength = 0.55f;

        /// <summary>How far a fading wall swells as it goes, as a share of its size.</summary>
        public const float FadeSwell = 0.06f;

        /// <summary>How long a struck ring spreads and fades, seconds, and how wide it ends, metres (on the built body).</summary>
        public const float RippleSeconds = 0.4f;

        public const float RippleSize = 1.2f;

        public const float LightIntensity = 0.7f;

        public const float LightRange = 5.0f;

        public const float HumVolume = 0.25f;

        private readonly List<(MeshRenderer ring, float age)> _ripples = new List<(MeshRenderer, float)>();
        private MaterialPropertyBlock _block;
        private MeshRenderer _face;
        private MeshRenderer _shimmer;
        private Light _light;
        private ParticleSystem _motes;
        private AudioSource _hum;
        private Vector3 _baseScale = Vector3.one;
        private float _bodyScale = 1.0f;
        private float _rise;
        private float _flash;
        private float _fadeSeconds;
        private float _fadeLeft;
        private float _seed;

        /// <summary>The wall's collider: off once it starts to fade.</summary>
        public BoxCollider Collider { get; private set; }

        /// <summary>How much of its health is left, 0..1 (its look follows it).</summary>
        public float HealthShare { get; private set; } = 1.0f;

        /// <summary>Fading away: let go or broken, it stops nothing any more.</summary>
        public bool Fading { get; private set; }

        /// <summary>How strongly its face is drawn now, 0..1: risen, its health, a flare, fading.</summary>
        public float Strength { get; private set; }

        /// <summary>Half its width and height in the world, metres.</summary>
        public float HalfWidth => ArcaneBarrierRules.Width * 0.5f * _bodyScale;

        public float HalfHeight => ArcaneBarrierRules.Height * 0.5f * _bodyScale;

        /// <summary>The way its face looks: away from the caster.</summary>
        public Vector3 Facing => transform.forward;

        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        /// <summary>Raises a wall before <paramref name="body"/>, a child of it.</summary>
        public static ArcaneBarrierWall Raise(Transform body)
        {
            var go = new GameObject("ArcaneBarrier");
            go.transform.SetParent(body, false);
            go.transform.localPosition = new Vector3(0.0f, ArcaneBarrierRules.Bottom + ArcaneBarrierRules.Height * 0.5f, ArcaneBarrierRules.Distance);
            go.transform.localRotation = Quaternion.identity;
            var wall = go.AddComponent<ArcaneBarrierWall>();
            wall.Build(body);
            return wall;
        }

        private void Build(Transform body)
        {
            _bodyScale = Mathf.Max(0.01f, body.lossyScale.y);
            _seed = Random.value * 100.0f;
            const float width = ArcaneBarrierRules.Width, height = ArcaneBarrierRules.Height;

            Collider = gameObject.AddComponent<BoxCollider>();
            Collider.size = new Vector3(width, height, ArcaneBarrierRules.Thickness);
            Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            // The caster stands right behind it: its own controller must not be pushed off by it.
            var controller = body.GetComponent<CharacterController>();
            if (controller != null)
            {
                Physics.IgnoreCollision(Collider, controller);
            }

            _face = ArcaneVisuals.Panel(transform, ArcaneVisuals.WallGlow, width, height, "Face");
            _shimmer = ArcaneVisuals.Panel(transform, ArcaneVisuals.ShimmerGlow, width * 0.96f, height * 0.96f, "Shimmer");
            _shimmer.transform.localPosition = new Vector3(0.0f, 0.0f, -0.01f);
            _light = ArcaneVisuals.Light(transform, 0.0f, LightRange * _bodyScale);
            _light.transform.localPosition = new Vector3(0.0f, 0.0f, 0.3f);

            _motes = ArcaneVisuals.Motes(transform, 16.0f, 0.1f, 0.25f, 0.08f, 1.4f);
            ParticleSystem.MainModule main = _motes.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = -0.05f;
            ParticleSystem.ShapeModule shape = _motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 0.05f, 0.05f);
            shape.position = new Vector3(0.0f, -height * 0.5f, 0.0f);

            if (Application.isPlaying)
            {
                _hum = gameObject.AddComponent<AudioSource>();
                _hum.clip = ArcaneBarrierSounds.Hum;
                _hum.loop = true;
                _hum.volume = HumVolume;
                _hum.spatialBlend = 1.0f;
                _hum.Play();
                AudioSource.PlayClipAtPoint(ArcaneBarrierSounds.Rise, transform.position, 0.8f);
            }
            Show(0.0f);
        }

        /// <summary>Something struck the wall at <paramref name="point"/>, leaving it <paramref name="healthShare"/> of its health: a ring there, the face flares.</summary>
        public void Struck(Vector3 point, float healthShare)
        {
            HealthShare = Mathf.Clamp01(healthShare);
            if (Fading)
            {
                return;
            }
            _flash = 1.0f;
            MeshRenderer ring = ArcaneVisuals.Panel(transform, ArcaneVisuals.RingGlow, 0.1f, 0.1f, "Ripple");
            Vector3 local = transform.InverseTransformPoint(point);
            const float halfWidth = ArcaneBarrierRules.Width * 0.5f, halfHeight = ArcaneBarrierRules.Height * 0.5f;
            ring.transform.localPosition = new Vector3(Mathf.Clamp(local.x, -halfWidth, halfWidth), Mathf.Clamp(local.y, -halfHeight, halfHeight), -0.02f);
            _ripples.Add((ring, 0.0f));
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcaneBarrierSounds.Struck, point, 0.6f);
            }
        }

        /// <summary>
        /// Lets the wall go over <paramref name="seconds"/>: it stops nothing from now, stays where it stood (no longer the body's), and fades out;
        /// <paramref name="broken"/>, it flares once as it gives out (a broken wall is already dim) and its motes are flung off. Never shatters.
        /// </summary>
        public void FadeAway(float seconds, bool broken)
        {
            if (Fading)
            {
                return;
            }
            Fading = true;
            Collider.enabled = false;
            transform.SetParent(null, true);
            _baseScale = transform.localScale / Mathf.Max(0.01f, transform.localScale.y) * _bodyScale;
            _fadeSeconds = Mathf.Max(0.01f, seconds);
            _fadeLeft = _fadeSeconds;
            if (broken)
            {
                _flash = 1.0f;
            }
            if (_motes != null)
            {
                ParticleSystem.EmissionModule emission = _motes.emission;
                emission.rateOverTime = 0.0f;
                if (broken)
                {
                    _motes.Emit(40);
                }
            }
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcaneBarrierSounds.Fade, transform.position, broken ? 0.9f : 0.5f);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _rise = Mathf.MoveTowards(_rise, 1.0f, deltaTime / ArcaneBarrierRules.RiseSeconds);
            _flash = Mathf.MoveTowards(_flash, 0.0f, deltaTime / 0.15f);
            float present = 1.0f;
            if (Fading)
            {
                _fadeLeft -= deltaTime;
                if (_fadeLeft <= 0.0f)
                {
                    Destroy(gameObject);
                    return;
                }
                present = _fadeLeft / _fadeSeconds;
            }
            float rise = _rise * _rise * (3.0f - 2.0f * _rise);
            float swell = Fading ? 1.0f + FadeSwell * (1.0f - present) : Mathf.Lerp(0.3f, 1.0f, rise);
            transform.localScale = _baseScale * swell;
            if (_hum != null)
            {
                _hum.volume = HumVolume * present;
            }

            for (int i = _ripples.Count - 1; i >= 0; i--)
            {
                (MeshRenderer ring, float age) = _ripples[i];
                age += deltaTime;
                if (ring == null || age >= RippleSeconds)
                {
                    if (ring != null)
                    {
                        Destroy(ring.gameObject);
                    }
                    _ripples.RemoveAt(i);
                    continue;
                }
                _ripples[i] = (ring, age);
                float t = age / RippleSeconds;
                float size = Mathf.Lerp(0.15f, RippleSize, 1.0f - (1.0f - t) * (1.0f - t));
                ring.transform.localScale = new Vector3(size, size, 1.0f);
                ArcaneVisuals.Fade(ring, ArcaneVisuals.Hot, (1.0f - t) * present, Block);
            }
            Show(present * rise);
        }

        /// <summary>Draws the face, its shimmer and its light at <paramref name="presence"/> (0..1: rising, fading).</summary>
        private void Show(float presence)
        {
            // Weakened, it dims toward WeakestStrength and flickers the more the less is left; fading, it goes evenly, never brightening.
            float weak = 1.0f - HealthShare;
            float flicker = Fading ? 1.0f : 1.0f - weak * 0.4f * Mathf.PerlinNoise(Time.time * 12.0f, _seed);
            Strength = Mathf.Clamp01(presence * (Mathf.Lerp(WeakestStrength, 1.0f, HealthShare) * flicker + 0.5f * _flash));
            ArcaneVisuals.Fade(_face, ArcaneVisuals.Magenta, Strength, Block);
            _shimmer.GetPropertyBlock(Block);
            Block.SetVector("_BaseMap_ST", new Vector4(1.5f, 1.5f, 0.0f, -Time.time * 0.12f));
            Block.SetColor("_BaseColor", ArcaneVisuals.Magenta * (Strength * ShimmerStrength));
            _shimmer.SetPropertyBlock(Block);
            _light.intensity = LightIntensity * Strength;
        }
    }
}
