using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// The Healing Aura's circle, drawn in code (docs/HEALING.md; owner: "make the healing aura green"): a ring of green light on the ground
    /// round the caster, a faint glow filling it and an inner ring of little marks turning slowly, green motes rising out of it, and a soft green
    /// light; it follows the caster, laid on the ground under them, grows and fades in and out. The same on another player's copy.
    /// </summary>
    public sealed class HealingAuraCircle : MonoBehaviour
    {
        public static readonly Color Green = new Color(0.32f, 1.0f, 0.45f);

        /// <summary>How fast the inner ring turns, degrees a second, and how long the circle takes to grow in and to fade.</summary>
        public const float TurnDegreesPerSecond = 25.0f;

        public const float FadeInSeconds = 0.4f;
        public const float FadeOutSeconds = 0.6f;

        /// <summary>Motes a second rising out of the circle, and the light's strength.</summary>
        public const float MotesPerSecond = 30.0f;

        public const float LightIntensity = 1.6f;

        /// <summary>The circle lies this far above the ground it is laid on, metres, and looks for ground this far below the caster's feet.</summary>
        public const float Lift = 0.04f;

        public const float GroundSearch = 3.0f;

        /// <summary>The rim's radius as a share of its square's half-width: the rest is room for its glow to fade out, so the square's edges never show.</summary>
        public const float RimShare = 0.86f;

        private static Material _ringMaterial;
        private static Material _innerMaterial;
        private static Material _moteMaterial;
        private static readonly RaycastHit[] GroundBuffer = new RaycastHit[8];

        private Transform _body;
        private Transform _rings;
        private Transform _inner;
        private MeshRenderer _ringRenderer;
        private MeshRenderer _innerRenderer;
        private ParticleSystem _motes;
        private Light _light;
        private MaterialPropertyBlock _block;
        private float _strength;
        private bool _on;

        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        /// <summary>0 to 1, how far grown in it is (it outlasts the spell by its fade).</summary>
        public float Strength => _strength;

        public bool IsShowing => _on || _strength > 0.0f;

        /// <summary>A circle round <paramref name="body"/>, hidden until <see cref="Show"/>.</summary>
        public static HealingAuraCircle Make(Transform body)
        {
            var go = new GameObject("HealingAura");
            var circle = go.AddComponent<HealingAuraCircle>();
            circle.Build(body);
            return circle;
        }

        private void Build(Transform body)
        {
            _body = body;
            // The rings are sized by their own transform (the circle's width); the motes and the light, under the unscaled root, are not.
            _rings = new GameObject("Rings").transform;
            _rings.SetParent(transform, false);
            Flat("Ring", RingMaterial(), out _ringRenderer);
            _inner = Flat("Inner", InnerMaterial(), out _innerRenderer);
            _motes = Motes();
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 1.0f;
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Green;
            _light.shadows = LightShadows.None;
            Apply();
        }

        /// <summary>Grows in (or fades out).</summary>
        public void Show(bool on)
        {
            _on = on;
        }

        private void LateUpdate()
        {
            if (_body == null)
            {
                Destroy(gameObject);
                return;
            }
            float rate = _on ? 1.0f / FadeInSeconds : 1.0f / FadeOutSeconds;
            _strength = Mathf.MoveTowards(_strength, _on ? 1.0f : 0.0f, rate * Time.deltaTime);
            Follow();
            _inner.Rotate(Vector3.up, TurnDegreesPerSecond * Time.deltaTime, Space.World);
            if (_on && _strength > 0.0f)
            {
                _motes.Emit(Mathf.Max(0, Mathf.RoundToInt(MotesPerSecond * Time.deltaTime + Random.value - 0.5f)));
            }
            Apply();
        }

        /// <summary>Under the caster, laid on the ground below their feet (or at their feet in the air), the size of the circle at their scale.</summary>
        private void Follow()
        {
            float scale = Mathf.Max(0.01f, _body.lossyScale.y);
            Vector3 feet = _body.position;
            Vector3 at = feet;
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * 0.5f * scale, Vector3.down, GroundBuffer, (GroundSearch + 0.5f) * scale, ~0,
                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = GroundBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(_body) || hit.distance >= best)
                {
                    continue;
                }
                best = hit.distance;
                at = hit.point;
            }
            transform.SetPositionAndRotation(at + Vector3.up * Lift, Quaternion.identity);
            float radius = HealingAuraRules.Radius * scale;
            float grown = radius * 2.0f / RimShare * Mathf.SmoothStep(0.6f, 1.0f, _strength);
            _rings.localScale = new Vector3(grown, grown, grown);
            ParticleSystem.ShapeModule shape = _motes.shape;
            shape.radius = radius * 0.95f;
            _light.range = radius * 1.6f;
        }

        private void Apply()
        {
            bool visible = _strength > 0.0f;
            _ringRenderer.enabled = visible;
            _innerRenderer.enabled = visible;
            _light.enabled = visible;
            _light.intensity = LightIntensity * _strength;
            Tint(_ringRenderer, _strength);
            Tint(_innerRenderer, _strength);
        }

        private void Tint(Renderer renderer, float strength)
        {
            renderer.GetPropertyBlock(Block);
            Block.SetColor("_BaseColor", Green * strength);
            renderer.SetPropertyBlock(Block);
        }

        /// <summary>A unit quad lying flat (facing up), one of the rings.</summary>
        private Transform Flat(string name, Material material, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_rings, false);
            go.AddComponent<MeshFilter>().sharedMesh = GlowShapes.FlatQuad;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        private ParticleSystem Motes()
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(transform, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.0f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(Green, new Color(0.85f, 1.0f, 0.75f));
            main.gravityModifier = -0.12f;   // they rise
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 128;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = HealingAuraRules.Radius;
            shape.rotation = new Vector3(-90.0f, 0.0f, 0.0f);
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MoteMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static Material RingMaterial() => _ringMaterial != null ? _ringMaterial : (_ringMaterial = Painted("HealingAuraRing", RingAlpha));

        private static Material InnerMaterial() => _innerMaterial != null ? _innerMaterial : (_innerMaterial = Painted("HealingAuraInner", InnerAlpha));

        private static Material MoteMaterial() => _moteMaterial != null ? _moteMaterial : (_moteMaterial = SoftGlow.Make("HealingAuraMote", Green, true));

        /// <summary>The rim: a bright soft ring at the circle's edge, a glow falling inward from it, and a faint fill; nothing past the rim's glow.</summary>
        public static float RingAlpha(float u, float v)
        {
            float r = RadiusAt(u, v);
            float rim = (r - 1.0f) / 0.04f;
            float inside = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, (r - 0.98f) / 0.04f);
            float glow = Mathf.Exp(-(1.0f - r) / 0.13f);
            return Mathf.Clamp01(Mathf.Exp(-rim * rim) + inside * (0.45f * glow + 0.08f));
        }

        /// <summary>The inner ring: a thin ring at three fifths of the radius with eight little marks on it, so its turning shows.</summary>
        public static float InnerAlpha(float u, float v)
        {
            float x = u - 0.5f, y = v - 0.5f;
            float r = RadiusAt(u, v);
            float ring = (r - 0.6f) / 0.018f;
            float angle = Mathf.Atan2(y, x);
            float marks = Mathf.Pow(Mathf.Max(0.0f, Mathf.Cos(angle * 8.0f)), 24.0f);
            float near = (r - 0.66f) / 0.03f;
            return Mathf.Clamp01(0.7f * Mathf.Exp(-ring * ring) + marks * Mathf.Exp(-near * near));
        }

        /// <summary>How far out (u, v) is, 1 at the rim.</summary>
        private static float RadiusAt(float u, float v)
        {
            return Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2.0f / RimShare;
        }

        private static Material Painted(string name, System.Func<float, float, float> alpha)
        {
            return GlowShapes.Painted(name, Green, true, GlowShapes.Paint(name, 256, TextureWrapMode.Clamp, alpha));
        }
    }
}
