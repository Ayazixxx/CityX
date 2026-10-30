using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// The ground heaving: a ring of dust racing out over the ground, dust rolling out low behind it and slowing, and a cloud of it heaving up
    /// in the middle, grey-brown and thick so it reads against the street. A giant's landing (GiantSlam) and an explosion (docs/GRENADE.md)
    /// throw it up; it is gone in <see cref="LifeSeconds"/>.
    /// </summary>
    public sealed class DustBurst : MonoBehaviour
    {
        /// <summary>How long the ring takes to reach its rim, and how long the dust lasts, seconds.</summary>
        public const float RingSeconds = 0.8f;

        public const float LifeSeconds = 3.0f;

        /// <summary>Puffs of dust rolling out along the ground, and heaving up in the middle, for a whole burst.</summary>
        public const int RollingPuffs = 56;

        public const int HeavingPuffs = 16;

        /// <summary>Grey-brown and thick, darker than the street so it reads against it.</summary>
        public static readonly Color Dust = new Color(0.47f, 0.42f, 0.35f, 0.85f);

        private static Material _ringMaterial;
        private static Material _dustMaterial;

        private Transform _ring;
        private MeshRenderer _ringRenderer;
        private MaterialPropertyBlock _tint;
        private float _radius;
        private float _age;

        private MaterialPropertyBlock Tint => _tint ??= new MaterialPropertyBlock();

        /// <summary>Its pictures made ahead (a few milliseconds the first burst would otherwise stop the game for).</summary>
        public static void Prewarm()
        {
            RingMaterial();
            DustMaterial();
        }

        /// <summary>
        /// Dust thrown up at <paramref name="feet"/> out to <paramref name="radius"/>, <paramref name="amount"/> of a whole burst's puffs (a
        /// smaller blast throws fewer), under <paramref name="parent"/> when given.
        /// </summary>
        public static DustBurst Roll(Vector3 feet, float radius, float amount, Transform parent = null)
        {
            var go = new GameObject("DustBurst");
            go.transform.SetParent(parent, true);
            go.transform.position = feet + Vector3.up * 0.05f;
            var burst = go.AddComponent<DustBurst>();
            burst.Build(radius, amount);
            return burst;
        }

        private void Build(float radius, float amount)
        {
            _radius = radius;
            _ring = new GameObject("Ring").transform;
            _ring.SetParent(transform, false);
            _ring.gameObject.AddComponent<MeshFilter>().sharedMesh = GlowShapes.FlatQuad;
            _ringRenderer = _ring.gameObject.AddComponent<MeshRenderer>();
            _ringRenderer.sharedMaterial = RingMaterial();
            _ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;
            Puffs(radius, Mathf.Max(0.05f, amount));
            Update();
        }

        /// <summary>Dust rolling out low along the ground all round, slowing as it goes, and a cloud of it heaving up in the middle.</summary>
        private void Puffs(float radius, float amount)
        {
            ParticleSystem particles = DustSystem();
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };   // white: the material is the dust's colour
            Vector3 feet = transform.position;
            int rolling = Mathf.RoundToInt(RollingPuffs * amount);
            for (int i = 0; i < rolling; i++)
            {
                float angle = Random.value * Mathf.PI * 2.0f;
                var outward = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
                emit.position = feet + outward * radius * Random.Range(0.05f, 0.2f) + Vector3.up * radius * 0.05f;
                emit.velocity = outward * radius * Random.Range(0.9f, 1.6f) + Vector3.up * radius * Random.Range(0.02f, 0.12f);
                emit.startSize = radius * Random.Range(0.22f, 0.4f);
                emit.startLifetime = Random.Range(1.4f, 2.6f);
                emit.rotation = Random.Range(0.0f, 360.0f);
                particles.Emit(emit, 1);
            }
            int heaving = Mathf.RoundToInt(HeavingPuffs * amount);
            for (int i = 0; i < heaving; i++)
            {
                Vector2 disc = Random.insideUnitCircle * radius * 0.15f;
                emit.position = feet + new Vector3(disc.x, radius * 0.05f, disc.y);
                emit.velocity = Vector3.up * radius * Random.Range(0.25f, 0.6f) + new Vector3(disc.x, 0.0f, disc.y);
                emit.startSize = radius * Random.Range(0.3f, 0.5f);
                emit.startLifetime = Random.Range(1.8f, 2.8f);
                emit.rotation = Random.Range(0.0f, 360.0f);
                particles.Emit(emit, 1);
            }
        }

        /// <summary>The dust's particles: soft puffs that slow to a drift, swell and thin away.</summary>
        private ParticleSystem DustSystem()
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.gravityModifier = -0.01f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule drag = particles.velocityOverLifetime;
            drag.enabled = true;
            drag.speedModifier = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 1.0f, 0.6f, 0.04f));
            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 0.6f, 1.0f, 1.8f));
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.06f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = DustMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return particles;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float out01 = Mathf.Clamp01(_age / RingSeconds);
            float across = _radius * 2.0f * Mathf.Sqrt(out01);
            _ring.localScale = new Vector3(across, 1.0f, across);
            _ringRenderer.GetPropertyBlock(Tint);
            Tint.SetColor("_BaseColor", new Color(Dust.r, Dust.g, Dust.b, Dust.a * (1.0f - out01)));
            _ringRenderer.SetPropertyBlock(Tint);
            _ringRenderer.enabled = out01 < 1.0f;
            if (_age >= LifeSeconds)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// A thick soft band of dust round a flat disc, thin inside it, blended (dust is not light); nothing at the square's edge, which would
        /// show as a cut.
        /// </summary>
        private static Material RingMaterial()
        {
            if (_ringMaterial == null)
            {
                Texture2D ring = GlowShapes.Paint("DustRing", 128, TextureWrapMode.Clamp, (u, v) =>
                {
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2.0f;
                    float band = (r - 0.78f) / 0.1f;
                    float inside = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, (r - 0.7f) / 0.15f);
                    return Mathf.Exp(-band * band) + 0.25f * r * inside;
                });
                _ringMaterial = GlowShapes.Painted("DustRing", Dust, false, ring);
            }
            return _ringMaterial;
        }

        private static Material DustMaterial() => _dustMaterial != null ? _dustMaterial : (_dustMaterial = SoftGlow.Make("Dust", Dust, false));
    }
}
