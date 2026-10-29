using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOW THE FIRE LOOKS (docs/FIRE.md; owner: "make the fire look really good"): four particle systems shared by every burning cell and
    /// every burning body, fed by hand each frame -- flipbook flames standing upright (vertical billboards of the sheet in
    /// <see cref="FireSprites"/>), a soft orange glow on the ground under them, embers that rise, drift and fall, and slow dark smoke above
    /// -- and a handful of flickering point lights parked over the hottest clusters. All additive and soft but the smoke, which darkens.
    /// Cells far from the camera emit less, and past <see cref="FarMetres"/> nothing (a flame a pixel high is not worth a particle).
    /// </summary>
    public sealed class FireLook
    {
        public const int MaxLights = 6;
        public const float LightRange = 9.0f;
        public const float LightIntensity = 3.2f;
        public const float LightHeight = 1.4f;
        public const float LightsEverySeconds = 0.2f;
        public const float ClusterMetres = 5.0f;

        /// <summary>Emission halves past this distance from the camera and stops past the far one.</summary>
        public const float NearMetres = 60.0f;

        public const float FarMetres = 130.0f;

        /// <summary>Particles a second for one cell at full heat.</summary>
        public const float FlamesPerSecond = 13.0f;

        public const float GlowPerSecond = 6.0f;
        public const float EmbersPerSecond = 5.0f;
        public const float SmokePerSecond = 2.5f;

        public static readonly Color LightColour = new Color(1.0f, 0.55f, 0.22f);
        public static readonly Color GlowColour = new Color(1.0f, 0.42f, 0.1f);
        public static readonly Color EmberColour = new Color(1.0f, 0.78f, 0.35f);
        public static readonly Color SmokeColour = new Color(0.07f, 0.07f, 0.07f);

        /// <summary>How many flames get a hot core tongue at their base, and its colour.</summary>
        public const float CoreShare = 0.6f;

        public static readonly Color CoreColour = new Color(1.0f, 0.95f, 0.75f);

        private sealed class Debts
        {
            public float Flame, Glow, Ember, Smoke;
        }

        private readonly ParticleSystem _flames;
        private readonly ParticleSystem _glow;
        private readonly ParticleSystem _embers;
        private readonly ParticleSystem _smoke;
        private readonly Light[] _lights = new Light[MaxLights];
        private readonly float[] _lightStrength = new float[MaxLights];
        private readonly Dictionary<FireCell, Debts> _debts = new Dictionary<FireCell, Debts>();
        private readonly List<FireCell> _gone = new List<FireCell>();
        private readonly Dictionary<(int, int, int), (Vector3 sum, float heat)> _clusters = new Dictionary<(int, int, int), (Vector3, float)>();
        private float _lightsDue;
        private float _flicker;

        public FireLook(Transform parent)
        {
            // The flames are premultiplied, not additive: additive tongues washed out pink against the bright sky (rendered 2026-10-29); the
            // glow and the embers add their light, the smoke darkens.
            _flames = Make(parent, "Flames", FireSprites.Particles("FireFlames", FireSprites.Flames(), ParticleBlend.Premultiplied, 2), 2500, ParticleSystemRenderMode.VerticalBillboard);
            _glow = Make(parent, "Glow", FireSprites.Particles("FireGlow", FireSprites.Dot(), ParticleBlend.Additive, 1), 800, ParticleSystemRenderMode.Billboard);
            _embers = Make(parent, "Embers", FireSprites.Particles("FireEmbers", FireSprites.Dot(), ParticleBlend.Additive, 3), 1500, ParticleSystemRenderMode.Billboard);
            _smoke = Make(parent, "Smoke", FireSprites.Particles("FireSmoke", FireSprites.Dot(), ParticleBlend.Alpha, 0), 1200, ParticleSystemRenderMode.Billboard);
            StyleFlames(_flames);
            StyleGlow(_glow);
            StyleEmbers(_embers);
            StyleSmoke(_smoke);
            for (int i = 0; i < MaxLights; i++)
            {
                var go = new GameObject("FireLight" + i);
                go.transform.SetParent(parent, false);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LightColour;
                light.range = LightRange;
                light.intensity = 0.0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                _lights[i] = light;
            }
        }

        /// <summary>One frame of every burning cell's fire, seen from <paramref name="eye"/>.</summary>
        public void Tick(IReadOnlyList<FireCell> burning, float deltaTime, Vector3 eye)
        {
            _flicker += deltaTime;
            for (int i = 0; i < burning.Count; i++)
            {
                FireCell cell = burning[i];
                float distance = Vector3.Distance(cell.Centre, eye);
                if (distance > FarMetres)
                {
                    continue;
                }
                float share = distance > NearMetres ? 0.5f : 1.0f;
                if (!_debts.TryGetValue(cell, out Debts debts))
                {
                    debts = new Debts();
                    _debts[cell] = debts;
                }
                float heat = Mathf.Max(0.05f, cell.Heat);
                EmitFlames(cell.Centre, heat, 1.0f, ref debts.Flame, FlamesPerSecond * heat * share * deltaTime);
                debts.Glow += GlowPerSecond * heat * share * deltaTime;
                while (debts.Glow >= 1.0f)
                {
                    debts.Glow -= 1.0f;
                    EmitGlow(cell.Centre, heat);
                }
                debts.Ember += EmbersPerSecond * heat * share * deltaTime;
                while (debts.Ember >= 1.0f)
                {
                    debts.Ember -= 1.0f;
                    EmitEmber(cell.Centre + Random.insideUnitSphere * 0.3f + Vector3.up * 0.4f, 1.0f);
                }
                debts.Smoke += SmokePerSecond * (0.4f + 0.6f * heat) * share * deltaTime;
                while (debts.Smoke >= 1.0f)
                {
                    debts.Smoke -= 1.0f;
                    EmitSmoke(cell.Centre + Vector3.up * (0.9f + 0.5f * heat), heat, 1.0f);
                }
            }
            Forget(burning);
            _lightsDue -= deltaTime;
            if (_lightsDue <= 0.0f)
            {
                _lightsDue = LightsEverySeconds;
                PlaceLights(burning);
            }
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(_flicker * 9.0f, 0.3f);
            for (int i = 0; i < MaxLights; i++)
            {
                _lights[i].intensity = LightIntensity * _lightStrength[i] * flicker;
            }
        }

        /// <summary>A burning body's fire at its chest: flames, a few embers, smoke.</summary>
        public void Burn(Vector3 chest, float strength, float deltaTime)
        {
            float debt = 0.0f;
            // Up and down the torso, not one spot: a body on fire is wrapped in it.
            EmitFlames(chest + Vector3.up * Random.Range(-0.9f, 0.5f), strength, 0.75f, ref debt, FlamesPerSecond * 1.8f * strength * deltaTime, 0.3f);
            if (Random.value < EmbersPerSecond * strength * deltaTime)
            {
                EmitEmber(chest + Random.insideUnitSphere * 0.35f, 0.8f);
            }
            if (Random.value < SmokePerSecond * strength * deltaTime)
            {
                EmitSmoke(chest + Vector3.up * 0.6f, strength, 0.6f);
            }
        }

        private void EmitFlames(Vector3 at, float heat, float size, ref float debt, float count, float spread = 0.3f)
        {
            debt += count;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            while (debt >= 1.0f)
            {
                debt -= 1.0f;
                Vector2 disc = Random.insideUnitCircle * spread;
                emit.position = at + new Vector3(disc.x, 0.05f + Random.value * 0.2f * heat, disc.y);
                emit.velocity = Vector3.up * (Random.Range(0.9f, 1.6f) * (0.6f + 0.4f * heat)) + new Vector3(Random.Range(-0.15f, 0.15f), 0.0f, Random.Range(-0.15f, 0.15f));
                emit.startSize = Random.Range(0.75f, 1.15f) * (0.55f + 0.6f * heat) * size;
                emit.startLifetime = Random.Range(0.55f, 0.85f);
                emit.startColor = Color.white;
                _flames.Emit(emit, 1);
                if (Random.value < CoreShare)
                {
                    // A hot core: a smaller, brighter, shorter-lived tongue at the base, where a fire is hottest.
                    emit.startSize *= 0.5f;
                    emit.startLifetime *= 0.6f;
                    emit.startColor = CoreColour;
                    _flames.Emit(emit, 1);
                }
            }
        }

        private void EmitGlow(Vector3 at, float heat)
        {
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = at + Vector3.up * 0.25f,
                velocity = Vector3.zero,
                startSize = Random.Range(1.6f, 2.2f) * (0.5f + 0.5f * heat),
                startLifetime = 0.5f,
                startColor = new Color(GlowColour.r, GlowColour.g, GlowColour.b, 0.5f * heat),
            };
            _glow.Emit(emit, 1);
        }

        private void EmitEmber(Vector3 at, float strength)
        {
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = at,
                velocity = Vector3.up * Random.Range(1.2f, 2.6f) * strength + new Vector3(Random.Range(-0.5f, 0.5f), 0.0f, Random.Range(-0.5f, 0.5f)),
                startSize = Random.Range(0.035f, 0.07f),
                startLifetime = Random.Range(0.9f, 2.0f),
                startColor = EmberColour,
            };
            _embers.Emit(emit, 1);
        }

        private void EmitSmoke(Vector3 at, float heat, float size)
        {
            Vector2 disc = Random.insideUnitCircle * 0.25f;
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = at + new Vector3(disc.x, 0.0f, disc.y),
                velocity = Vector3.up * Random.Range(0.7f, 1.2f) + new Vector3(Random.Range(-0.2f, 0.2f), 0.0f, Random.Range(-0.2f, 0.2f)),
                startSize = Random.Range(0.55f, 0.85f) * (0.7f + 0.5f * heat) * size,
                startLifetime = Random.Range(2.2f, 3.4f),
                startColor = new Color(SmokeColour.r, SmokeColour.g, SmokeColour.b, 0.3f),
            };
            _smoke.Emit(emit, 1);
        }

        private void Forget(IReadOnlyList<FireCell> burning)
        {
            if (_debts.Count <= burning.Count)
            {
                return;
            }
            _gone.Clear();
            foreach (FireCell cell in _debts.Keys)
            {
                if (!cell.Burning)
                {
                    _gone.Add(cell);
                }
            }
            foreach (FireCell cell in _gone)
            {
                _debts.Remove(cell);
            }
        }

        /// <summary>The lights go over the hottest clusters of cells (blocks of <see cref="ClusterMetres"/>), bright by how much burns there.</summary>
        private void PlaceLights(IReadOnlyList<FireCell> burning)
        {
            _clusters.Clear();
            foreach (FireCell cell in burning)
            {
                var key = (Mathf.FloorToInt(cell.Centre.x / ClusterMetres), Mathf.FloorToInt(cell.Centre.y / 4.0f), Mathf.FloorToInt(cell.Centre.z / ClusterMetres));
                _clusters.TryGetValue(key, out (Vector3 sum, float heat) cluster);
                cluster.sum += cell.Centre * cell.Heat;
                cluster.heat += cell.Heat;
                _clusters[key] = cluster;
            }
            for (int i = 0; i < MaxLights; i++)
            {
                _lightStrength[i] = 0.0f;
                _lights[i].enabled = false;
            }
            int placed = 0;
            while (placed < MaxLights && _clusters.Count > 0)
            {
                (int, int, int) hottest = default;
                float most = 0.0f;
                foreach (KeyValuePair<(int, int, int), (Vector3 sum, float heat)> entry in _clusters)
                {
                    if (entry.Value.heat > most)
                    {
                        most = entry.Value.heat;
                        hottest = entry.Key;
                    }
                }
                if (most <= 0.0f)
                {
                    break;
                }
                (Vector3 sum, float heat) picked = _clusters[hottest];
                _clusters.Remove(hottest);
                Light light = _lights[placed];
                light.transform.position = picked.sum / picked.heat + Vector3.up * LightHeight;
                light.range = LightRange * Mathf.Lerp(0.6f, 1.3f, Mathf.Clamp01(picked.heat / 12.0f));
                light.enabled = true;
                _lightStrength[placed] = Mathf.Clamp01(picked.heat / 4.0f);
                placed++;
            }
        }

        public void Clear()
        {
            _flames.Clear(true);
            _glow.Clear(true);
            _embers.Clear(true);
            _smoke.Clear(true);
            _debts.Clear();
            for (int i = 0; i < MaxLights; i++)
            {
                _lights[i].enabled = false;
                _lightStrength[i] = 0.0f;
            }
        }

        private static ParticleSystem Make(Transform parent, string name, Material material, int maxParticles, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startLifetime = 1.0f;
            main.startSpeed = 0.0f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;   // fed by hand
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return particles;
        }

        private static void StyleFlames(ParticleSystem particles)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = FireSprites.Tiles;
            sheet.numTilesY = FireSprites.Tiles;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f));
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0.0f, FireSprites.Frames);
            sheet.cycleCount = 1;

            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 0.5f),
                    new GradientColorKey(new Color(1.0f, 0.55f, 0.3f), 0.8f), new GradientColorKey(new Color(0.45f, 0.08f, 0.02f), 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.12f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0.0f, 1.0f),
                });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(new Keyframe(0.0f, 0.55f), new Keyframe(0.3f, 1.0f), new Keyframe(0.7f, 1.15f), new Keyframe(1.0f, 0.8f)));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.6f;
            noise.damping = true;
        }

        private static void StyleGlow(ParticleSystem particles)
        {
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.4f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 0.85f, 1.0f, 1.1f));
        }

        private static void StyleEmbers(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = 0.18f;
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(1.0f, 0.5f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.6f, 0.1f, 0.0f), 1.0f),
                },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.4f));
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.8f;
            noise.damping = true;
        }

        private static void StyleSmoke(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = -0.02f;
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.15f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 1.0f, 1.0f, 2.8f));
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;
        }
    }
}
