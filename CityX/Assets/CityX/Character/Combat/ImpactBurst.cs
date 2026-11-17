using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// Something hard striking a surface (owner, 2026-11-17, of the boomerangs: "when thrown against a wall or the floor or any type of object there
    /// is some sort of bang noise and some impact fx"): a white flash, a ring snapping out flat on the surface, chips of it thrown out and falling,
    /// and a puff of grey dust that drifts off it. Laid on the surface whichever way it faces (a wall, the floor, a car's side); <c>size</c> scales
    /// the lot (1 a thrown boomerang, more the giant one). Silent: the caller has its own sound. Gone in <see cref="LifeSeconds"/>.
    /// </summary>
    public sealed class ImpactBurst : MonoBehaviour
    {
        public const float LifeSeconds = 1.6f;

        /// <summary>How long the ring takes to snap out to its rim, and how wide it gets at size 1, metres.</summary>
        public const float RingSeconds = 0.22f;

        public const float RingMetres = 1.1f;

        /// <summary>Chips and dust puffs thrown at size 1.</summary>
        public const int Chips = 14;

        public const int Puffs = 16;

        /// <summary>How far round from straight out of the surface the chips and dust are thrown, degrees: a cone.</summary>
        public const float SpreadDegrees = 65.0f;

        /// <summary>Grey dust and darker chips, so they read against a pale wall and a dark street.</summary>
        public static readonly Color Dust = new Color(0.55f, 0.53f, 0.50f, 0.9f);

        public static readonly Color Chip = new Color(0.30f, 0.28f, 0.26f, 1.0f);

        private static readonly Color RingColour = new Color(1.0f, 0.97f, 0.9f, 0.85f);

        /// <summary>How many bursts have been shown, and the last one's place and facing (for tests).</summary>
        public static int Shown { get; private set; }

        public static Vector3 LastPoint { get; private set; }

        public static Vector3 LastNormal { get; private set; }

        private static Material _ringMaterial;
        private static Material _dustMaterial;
        private static Material _chipMaterial;

        private Transform _ring;
        private MeshRenderer _ringRenderer;
        private MaterialPropertyBlock _tint;
        private float _size;
        private float _age;

        /// <summary>A burst at <paramref name="point"/> on a surface facing <paramref name="normal"/>, <paramref name="size"/> times a boomerang's.</summary>
        public static void At(Vector3 point, Vector3 normal, float size)
        {
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            Shown++;
            LastPoint = point;
            LastNormal = normal;
            if (!Application.isPlaying)
            {
                return;
            }
            var go = new GameObject("ImpactBurst");
            go.transform.SetPositionAndRotation(point + normal * 0.02f, Quaternion.FromToRotation(Vector3.up, normal));
            go.AddComponent<ImpactBurst>().Build(normal, Mathf.Max(0.2f, size));
            AttackGlint.Flash(point + normal * 0.05f);
        }

        /// <summary>
        /// A way out of a surface facing <paramref name="normal"/>, within <paramref name="spreadDegrees"/> of straight out: <paramref name="round"/>
        /// (0 to 1) is how far round the normal, <paramref name="out01"/> (0 to 1) how far from it toward the cone's edge (evenly over the cone's face).
        /// </summary>
        public static Vector3 Scatter(Vector3 normal, float spreadDegrees, float round, float out01)
        {
            normal = normal.normalized;
            Vector3 side = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 other = Vector3.Cross(normal, side);
            float tilt = Mathf.Sqrt(Mathf.Clamp01(out01)) * spreadDegrees * Mathf.Deg2Rad;
            float turn = round * Mathf.PI * 2.0f;
            Vector3 across = side * Mathf.Cos(turn) + other * Mathf.Sin(turn);
            return (normal * Mathf.Cos(tilt) + across * Mathf.Sin(tilt)).normalized;
        }

        private void Build(Vector3 normal, float size)
        {
            _size = size;
            _ring = new GameObject("Ring").transform;
            _ring.SetParent(transform, false);
            _ring.gameObject.AddComponent<MeshFilter>().sharedMesh = GlowShapes.FlatQuad;
            _ringRenderer = _ring.gameObject.AddComponent<MeshRenderer>();
            _ringRenderer.sharedMaterial = RingMaterial();
            _ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;

            Vector3 point = transform.position;
            ParticleSystem chips = Particles("Chips", ChipMaterial(), 1.0f, false);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0, n = Mathf.RoundToInt(Chips * size); i < n; i++)
            {
                emit.position = point;
                emit.velocity = Scatter(normal, SpreadDegrees, Random.value, Random.value) * Random.Range(3.0f, 7.5f) * Mathf.Sqrt(size);
                emit.startSize = Random.Range(0.035f, 0.08f) * size;
                emit.startLifetime = Random.Range(0.45f, 0.9f);
                emit.rotation = Random.Range(0.0f, 360.0f);
                chips.Emit(emit, 1);
            }
            ParticleSystem dust = Particles("Dust", DustMaterial(), -0.02f, true);
            for (int i = 0, n = Mathf.RoundToInt(Puffs * size); i < n; i++)
            {
                emit.position = point;
                emit.velocity = Scatter(normal, SpreadDegrees, Random.value, Random.value) * Random.Range(0.8f, 2.6f) * size;
                emit.startSize = Random.Range(0.3f, 0.6f) * size;
                emit.startLifetime = Random.Range(0.7f, 1.4f);
                emit.rotation = Random.Range(0.0f, 360.0f);
                dust.Emit(emit, 1);
            }
            Update();
        }

        /// <summary>Particles thrown once: chips that fall (gravity 1) and shrink, or dust that slows, swells and thins away.</summary>
        private ParticleSystem Particles(string name, Material material, float gravity, bool swells)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.0f, swells ? AnimationCurve.Linear(0.0f, 0.5f, 1.0f, 1.8f) : AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.4f));
            if (swells)
            {
                ParticleSystem.VelocityOverLifetimeModule drag = particles.velocityOverLifetime;
                drag.enabled = true;
                drag.speedModifier = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 1.0f, 0.5f, 0.05f));
                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                var alpha = new Gradient();
                alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                    new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.05f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) });
                fade.color = alpha;
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return particles;
        }

        private MaterialPropertyBlock Tint => _tint ??= new MaterialPropertyBlock();

        private void Update()
        {
            _age += Time.deltaTime;
            float out01 = Mathf.Clamp01(_age / RingSeconds);
            float across = RingMetres * _size * (1.0f - (1.0f - out01) * (1.0f - out01));   // snaps out, easing to its rim
            _ring.localScale = new Vector3(across, 1.0f, across);
            _ringRenderer.GetPropertyBlock(Tint);
            Tint.SetColor("_BaseColor", new Color(RingColour.r, RingColour.g, RingColour.b, RingColour.a * (1.0f - out01)));
            _ringRenderer.SetPropertyBlock(Tint);
            _ringRenderer.enabled = out01 < 1.0f;
            if (_age >= LifeSeconds)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>A thin bright band round a disc, nothing inside or at the square's edge; lit, so it reads by night too.</summary>
        private static Material RingMaterial()
        {
            if (_ringMaterial == null)
            {
                Texture2D ring = GlowShapes.Paint("ImpactRing", 128, TextureWrapMode.Clamp, (u, v) =>
                {
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2.0f;
                    float band = (r - 0.82f) / 0.07f;
                    return Mathf.Exp(-band * band);
                });
                _ringMaterial = GlowShapes.Painted("ImpactRing", RingColour, true, ring);
            }
            return _ringMaterial;
        }

        private static Material DustMaterial() => _dustMaterial != null ? _dustMaterial : (_dustMaterial = SoftGlow.Make("ImpactDust", Dust, false));

        private static Material ChipMaterial() => _chipMaterial != null ? _chipMaterial : (_chipMaterial = SoftGlow.Make("ImpactChip", Chip, false));
    }
}
