using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// What flies and glows round Glacier Spikes' crystals (docs/GLACIER_SPIKES.md), drawn in code: ice chips thrown up where each crystal
    /// breaks the ground, mist rolling and glitter rising behind the front, frost left on the ground along the band, and at the far end a
    /// burst of mist and glitter, a ring racing out over the floor, a flash of cold light and a shake. The light travels with the front and
    /// then stays at the end, fading as the field sinks. The reference's colours (achrefelouafi's LinearAbiltyCastingThreeJS, MIT).
    /// </summary>
    public sealed class GlacierSpikesEffects : MonoBehaviour
    {
        public static readonly Color Ice = new Color(0.541f, 0.855f, 1.0f, 1.0f);
        public static readonly Color Rim = new Color(0.949f, 0.996f, 1.0f, 1.0f);
        public static readonly Color Frost = new Color(0.941f, 0.976f, 1.0f, 1.0f);
        public static readonly Color FrostEdge = new Color(0.475f, 0.714f, 0.867f, 1.0f);
        public static readonly Color Shock = new Color(0.373f, 0.816f, 1.0f, 1.0f);
        public static readonly Color Glow = new Color(0.498f, 0.831f, 1.0f, 1.0f);

        /// <summary>Chips thrown where a crystal breaks the ground (rubble throws one), and the chance of a puff of mist with them.</summary>
        public const int ChipsPerBreach = 3;
        public const float BreachMistChance = 0.25f;

        /// <summary>No mist where a crystal breaks the ground this close to the line's start, metres: it would fill the caster's own view.</summary>
        public const float MistClearOfCaster = 3.0f;

        /// <summary>Mist puffs and glitter specks a second thrown up along the front as it runs.</summary>
        public const float FrontMistPerSecond = 40.0f;
        public const float FrontGlitterPerSecond = 110.0f;

        /// <summary>How thick a puff of mist is: a breath of it along the line, a little more in the burst.</summary>
        public const float MistAlpha = 0.16f;
        public const float ImpactMistAlpha = 0.24f;

        /// <summary>The burst at the far end.</summary>
        public const int ImpactMist = 60;
        public const int ImpactGlitter = 100;

        /// <summary>The light: its flash at the end, what it holds while the field stands, and its reach, metres.</summary>
        public const float FlashIntensity = 5.0f;
        public const float HoldIntensity = 1.2f;
        public const float LightRange = 14.0f;
        public const float FlashSeconds = 0.35f;

        /// <summary>The ring: how far it races out (a share of the impact cluster's radius) and in how long.</summary>
        public const float RingReachShare = 2.2f;
        public const float RingSeconds = 0.55f;

        /// <summary>The shake: felt this far from the end, metres, this hard at it, for this long.</summary>
        public const float ShakeReach = 25.0f;
        public const float ShakeMetres = 0.22f;
        public const float ShakeSeconds = 0.45f;

        /// <summary>How far above the ground the frost lies, metres.</summary>
        public const float FrostLift = 0.03f;

        private static Material _chipMaterial;
        private static Material _mistMaterial;
        private static Material _glitterMaterial;
        private static Material _frostMaterial;
        private static Material _ringMaterial;
        private static Material _patchMaterial;

        private GlacierSpikesField _field;
        private Material _template;
        private ParticleSystem _chips;
        private ParticleSystem _mist;
        private ParticleSystem _glitter;
        private GlacierGroundStrip _frost;
        private Transform _ring;
        private MeshRenderer _ringRenderer;
        private Transform _patch;
        private MeshRenderer _patchRenderer;
        private Light _light;
        private MaterialPropertyBlock _tint;
        private float _front;
        private float _sinceImpact = -1.0f;
        private float _mistDebt;
        private float _glitterDebt;
        private float _shade = 1.0f;

        public bool HasImpacted => _sinceImpact >= 0.0f;

        public float LightIntensity => _light != null && _light.enabled ? _light.intensity : 0.0f;

        public void Begin(GlacierSpikesField field, IGlacierGround ground, Material template)
        {
            _field = field;
            _shade = SceneShade();
            _template = template;
            _chips = Particles("Chips", ChipMaterial(), 0.05f, 0.16f, 0.6f, 0.45f, 0.95f, 1.6f, 600, true);
            _mist = Particles("Mist", MistMaterial(), 0.7f, 1.6f, 1.8f, 1.0f, 2.0f, -0.05f, 400, false);
            _glitter = Particles("Glitter", GlitterMaterial(), 0.05f, 0.11f, 0.5f, 0.6f, 1.4f, -0.1f, 400, false);

            _frost = new GlacierGroundStrip("GlacierFrost");
            _frost.Lay(field.Path, ground, FrostLift, 0.0f, false);
            var frost = new GameObject("Frost");
            frost.transform.SetParent(transform, false);
            frost.AddComponent<MeshFilter>().sharedMesh = _frost.Mesh;
            Flat(frost, FrostMaterial());

            _ring = Quad("Ring", RingMaterial(), out _ringRenderer);
            _patch = Quad("Patch", PatchMaterial(), out _patchRenderer);
            Vector3 end = field.Cast.End;
            end.y = field.Path.HeightAt(field.Cast.Length);
            _ring.position = end + Vector3.up * (FrostLift * 2.0f);
            _patch.position = end + Vector3.up * FrostLift;
            float patch = GlacierSpikesRules.ImpactRadius * 2.6f;
            _patch.localScale = new Vector3(patch, 1.0f, patch);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(transform, false);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Glow;
            _light.range = LightRange;
            _light.shadows = LightShadows.None;
            _light.intensity = HoldIntensity;
            Step(0.0f, 0.0f, 0.0f);
        }

        /// <summary>A crystal <paramref name="width"/> wide broke the ground at <paramref name="at"/>: chips of ice thrown up and out, sometimes mist.</summary>
        public void Breach(Vector3 at, float width, bool rubble)
        {
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            int chips = rubble ? 1 : ChipsPerBreach;
            for (int i = 0; i < chips; i++)
            {
                Vector2 round = Random.insideUnitCircle;
                emit.position = at + new Vector3(round.x, 0.05f, round.y) * (width * 0.5f);
                emit.velocity = new Vector3(round.x * 2.5f, Random.Range(3.5f, 7.0f), round.y * 2.5f);
                emit.startColor = Shaded(Ice, 1.0f);
                _chips.Emit(emit, 1);
            }
            Vector3 fromStart = at - _field.Cast.Start;
            fromStart.y = 0.0f;
            if (fromStart.sqrMagnitude > MistClearOfCaster * MistClearOfCaster && Random.value < BreachMistChance)
            {
                Puffs(at, width * 0.6f, 2, 0.6f, MistAlpha);
            }
        }

        /// <summary>The front reached the end: the burst, the ring, the flash, the shake and the frost patch.</summary>
        public void Impact()
        {
            _sinceImpact = 0.0f;
            Vector3 end = _ring.position;
            Puffs(end, GlacierSpikesRules.ImpactRadius, ImpactMist, 1.6f, ImpactMistAlpha);
            Glitter(end, GlacierSpikesRules.ImpactRadius, ImpactGlitter, 2.0f);
            ThirdPersonCamera.ShakeNear(end, ShakeReach, ShakeMetres, ShakeSeconds);
        }

        /// <summary>One frame: the front at <paramref name="front"/> metres, the field <paramref name="sunk"/> (0 to 1) back into the ground.</summary>
        public void Step(float front, float sunk, float deltaTime)
        {
            bool running = !HasImpacted;
            _front = front;
            GlacierSpikesCast cast = _field.Cast;
            Vector3 frontPoint = cast.Start + cast.Direction * front;
            frontPoint.y = _field.Path.HeightAt(front);
            float standing = 1.0f - sunk;

            if (running && deltaTime > 0.0f)
            {
                float half = GlacierSpikesRules.HalfWidth(cast.Length > 0.0f ? front / cast.Length : 0.0f);
                _mistDebt += FrontMistPerSecond * deltaTime;
                _glitterDebt += FrontGlitterPerSecond * deltaTime;
                int mist = Mathf.FloorToInt(_mistDebt);
                int glitter = Mathf.FloorToInt(_glitterDebt);
                _mistDebt -= mist;
                _glitterDebt -= glitter;
                if (front > MistClearOfCaster)
                {
                    Puffs(frontPoint, half, mist, 0.5f, MistAlpha);
                }
                Glitter(frontPoint, half, glitter, 1.0f);
            }
            else if (sunk < 0.6f && deltaTime > 0.0f)
            {
                _glitterDebt += FrontGlitterPerSecond * 0.15f * deltaTime;
                int glitter = Mathf.FloorToInt(_glitterDebt);
                _glitterDebt -= glitter;
                float along = Random.value;
                Vector3 at = cast.PointAt(along);
                at.y = _field.Path.HeightAt(along * cast.Length);
                Glitter(at, GlacierSpikesRules.HalfWidth(along), glitter, 0.8f);
            }

            // Faded in away from the caster (it lay round their feet in their own view) and darker still at night: frost lies, it does not shine.
            _frost.Paint(Shaded(FrostEdge, 0.5f) * _shade, Shaded(Frost, 0.45f) * _shade, front, 1.5f, MistClearOfCaster, standing);

            if (HasImpacted)
            {
                _sinceImpact += deltaTime;
            }
            float ring = HasImpacted ? Mathf.Clamp01(_sinceImpact / RingSeconds) : 0.0f;
            bool ringShown = HasImpacted && ring < 1.0f;
            _ringRenderer.enabled = ringShown;
            if (ringShown)
            {
                float reach = GlacierSpikesRules.ImpactRadius * RingReachShare * (1.0f - Mathf.Pow(1.0f - ring, 3.0f)) * 2.0f + 0.5f;
                _ring.localScale = new Vector3(reach, 1.0f, reach);
                Tint(_ringRenderer, Shock * Mathf.Pow(1.0f - ring, 1.5f));
            }
            float patch = HasImpacted ? Mathf.Clamp01(_sinceImpact / 0.3f) * standing : 0.0f;
            _patchRenderer.enabled = patch > 0.0f;
            if (patch > 0.0f)
            {
                Color frost = Shaded(Frost, 0.55f);
                frost.a *= patch;
                Tint(_patchRenderer, frost);
            }

            float flash = HasImpacted ? Mathf.Clamp01(1.0f - _sinceImpact / FlashSeconds) : 0.0f;
            _light.transform.position = (HasImpacted ? _ring.position : frontPoint) + Vector3.up * 1.5f;
            _light.intensity = (HoldIntensity + (FlashIntensity - HoldIntensity) * flash * flash) * standing;
            _light.enabled = _light.intensity > 0.01f;
        }

        /// <summary>Mist puffs round <paramref name="at"/>, within <paramref name="spread"/> metres, rising slowly.</summary>
        private void Puffs(Vector3 at, float spread, int count, float lift, float alpha)
        {
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector2 round = Random.insideUnitCircle * spread;
                emit.position = at + new Vector3(round.x, Random.Range(0.1f, 0.6f), round.y);
                emit.velocity = new Vector3(round.x * 0.25f, Random.Range(0.1f, lift), round.y * 0.25f);
                emit.startColor = Shaded(Frost, alpha);
                _mist.Emit(emit, 1);
            }
        }

        /// <summary>Glitter round <paramref name="at"/>, within <paramref name="spread"/> metres, rising up to <paramref name="rise"/> metres a second.</summary>
        private void Glitter(Vector3 at, float spread, int count, float rise)
        {
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector2 round = Random.insideUnitCircle * spread;
                emit.position = at + new Vector3(round.x, Random.Range(0.2f, 2.5f), round.y);
                emit.velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.3f, rise), Random.Range(-0.2f, 0.2f));
                emit.startColor = Rim * Mathf.Lerp(0.35f, 1.0f, _shade);
                _glitter.Emit(emit, 1);
            }
        }

        /// <summary>
        /// How lit the street is (the sky's ambient, the sun's or the moon's light): mist and chips are no lamps, and drawn unlit at full white
        /// they would glow in the dark. Never quite nothing: the ice's own glow lights them.
        /// </summary>
        private static float SceneShade()
        {
            float ambient = RenderSettings.ambientEquatorColor.maxColorComponent * 2.5f;
            return Mathf.Clamp(ambient, 0.25f, 1.0f);
        }

        private Color Shaded(Color colour, float alpha)
        {
            Color lit = Color.Lerp(Ice * 0.6f, colour, _shade) * _shade;
            lit.a = alpha;
            return lit;
        }

        private void Tint(Renderer renderer, Color colour)
        {
            _tint ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_tint);
            _tint.SetColor("_BaseColor", colour);
            renderer.SetPropertyBlock(_tint);
        }

        private ParticleSystem Particles(string name, Material material, float leastSize, float mostSize, float grow, float shortest, float longest,
            float gravity, int most, bool stretched)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(shortest, longest);
            main.startSpeed = 0.0f;
            main.startSize = new ParticleSystem.MinMaxCurve(leastSize, mostSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, 2.0f * Mathf.PI);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = most;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, grow));
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.1f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 2.0f;
            }
            particles.Play();
            return particles;
        }

        private Transform Quad(string name, Material material, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = GlowShapes.FlatQuad;
            renderer = Flat(go, material);
            renderer.enabled = false;
            return go.transform;
        }

        private static MeshRenderer Flat(GameObject go, Material material)
        {
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        // Made from the template (soft particles on) when there is one, so mist fades where it meets the street instead of ending in a line.
        private Material ChipMaterial() => _chipMaterial != null ? _chipMaterial : (_chipMaterial = SoftGlow.Make("GlacierChip", Color.white, false, _template));

        private Material MistMaterial() => _mistMaterial != null ? _mistMaterial : (_mistMaterial = SoftGlow.Make("GlacierMist", Color.white, false, _template));

        private Material GlitterMaterial() =>
            _glitterMaterial != null ? _glitterMaterial : (_glitterMaterial = SoftGlow.Make("GlacierGlitter", Color.white, true, _template));

        private static Material FrostMaterial() => _frostMaterial != null ? _frostMaterial : (_frostMaterial = SoftGlow.Make("GlacierFrost", Color.white, false));

        private Material PatchMaterial() => _patchMaterial != null ? _patchMaterial : (_patchMaterial = SoftGlow.Make("GlacierFrostPatch", Frost, false, _template));

        /// <summary>A bright ring at the rim of its square, glowing a little inward: the shockwave on the floor.</summary>
        private static Material RingMaterial()
        {
            if (_ringMaterial != null)
            {
                return _ringMaterial;
            }
            Texture2D ring = GlowShapes.Paint("GlacierRing", 128, TextureWrapMode.Clamp, (u, v) =>
            {
                float d = new Vector2(u * 2.0f - 1.0f, v * 2.0f - 1.0f).magnitude / 0.9f;
                float band = (d - 0.92f) / 0.06f;
                float rim = Mathf.Exp(-band * band);
                float inner = d < 0.92f ? 0.18f * d * d : 0.0f;
                return d > 1.0f ? 0.0f : rim + inner;
            });
            _ringMaterial = GlowShapes.Painted("GlacierShock", Shock, true, ring);
            return _ringMaterial;
        }
    }
}
