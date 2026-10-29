using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOW THE FIRE LOOKS (docs/FIRE.md; owner, 2026-10-29: "make it look like real life fire like gta or red dead"): four particle systems
    /// shared by every burning cell and every burning body, fed by hand each frame. The flames and the smoke are quads the CityX/Fire shader
    /// draws every pixel of -- a flame shape bent and eaten by noise rising through it, coloured by a black-body ramp, born fast and burning
    /// away from its tips as it ages (the shader reads each particle's own random number and age through custom vertex streams); flames stand
    /// upright (vertical billboards), smoke faces the camera. Under them an additive orange glow on the ground, and embers that rise, drift and
    /// fall, bright enough for the game's bloom. A handful of flickering point lights park over the hottest clusters. Cells far from the camera
    /// emit less, and past <see cref="FarMetres"/> nothing (a flame a pixel high is not worth a particle).
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
        public const float FlamesPerSecond = 11.0f;

        public const float GlowPerSecond = 6.0f;
        public const float EmbersPerSecond = 6.0f;
        public const float SmokePerSecond = 2.0f;

        /// <summary>How many flames get a hot core tongue at their base (smaller, brighter, quicker), and how much brighter.</summary>
        public const float CoreShare = 0.5f;

        public const float CoreBrightness = 1.3f;

        /// <summary>The embers' material brightness: past 1 so the bloom catches them, as it does the flames' cores.</summary>
        public const float EmberBrightness = 1.8f;

        /// <summary>
        /// A burning weapon's flames (<see cref="Lick"/>): so many a second for each metre of it, their size against a burning cell's, how far
        /// off its line they start, and the least length counted (a hand burns as a short blade does).
        /// </summary>
        public const float LickFlamesPerMetre = 100.0f;

        public const float LickSize = 0.3f;
        public const float LickSpread = 0.03f;
        public const float LickShortestMetres = 0.3f;

        /// <summary>The longest a weapon's flame lives, seconds, and the share of its flames that also leave a glow.</summary>
        public const float LickSeconds = 0.35f;

        public const float LickGlowShare = 0.4f;

        public static readonly Color LightColour = new Color(1.0f, 0.55f, 0.22f);
        public static readonly Color GlowColour = new Color(1.0f, 0.42f, 0.1f);
        public static readonly Color EmberColour = new Color(1.0f, 0.78f, 0.35f);

        /// <summary>The smoke's own grey once it has risen out of the fire's light (the shader lights it orange while it is young).</summary>
        public static readonly Color SmokeColour = new Color(0.1f, 0.1f, 0.1f, 0.6f);

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
        private float _lickDebt;

        public FireLook(Transform parent)
        {
            _flames = Make(parent, "Flames", FireSprites.Flames(), 2500, ParticleSystemRenderMode.VerticalBillboard, true);
            _glow = Make(parent, "Glow", FireSprites.Particles("FireGlow", FireSprites.Dot(), ParticleBlend.Additive, 1), 800, ParticleSystemRenderMode.Billboard, false);
            _embers = Make(parent, "Embers", FireSprites.Particles("FireEmbers", FireSprites.Dot(), ParticleBlend.Additive, 3, EmberBrightness), 1500, ParticleSystemRenderMode.Billboard, false);
            _smoke = Make(parent, "Smoke", FireSprites.Smoke(), 1200, ParticleSystemRenderMode.Billboard, true);
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
                    EmitSmoke(cell.Centre + Vector3.up * (1.1f + 0.5f * heat), heat, 1.0f);
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

        /// <summary>
        /// A burning weapon's fire: small tongues licking up from random points along <paramref name="from"/> to <paramref name="to"/>,
        /// <see cref="LickFlamesPerMetre"/> of it a second, and now and then an ember; at <paramref name="scale"/> a body's size. Short-lived and
        /// slow to rise, unlike a burning cell's (a second and a metre up): they hug the weapon, and a swing leaves a brief trail of them.
        /// </summary>
        public void Lick(Vector3 from, Vector3 to, float strength, float scale, float deltaTime)
        {
            scale = Mathf.Max(0.01f, scale);
            float metres = Mathf.Max(LickShortestMetres, Vector3.Distance(from, to) / scale);
            _lickDebt += LickFlamesPerMetre * metres * strength * deltaTime;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            while (_lickDebt >= 1.0f)
            {
                _lickDebt -= 1.0f;
                emit.position = Vector3.Lerp(from, to, Random.value) + Random.insideUnitSphere * LickSpread * scale;
                emit.velocity = (Vector3.up * Random.Range(0.25f, 0.5f) + Random.insideUnitSphere * 0.08f) * scale;
                emit.startSize = Random.Range(0.75f, 1.25f) * LickSize * scale;
                emit.startLifetime = Random.Range(LickSeconds * 0.6f, LickSeconds);
                emit.startColor = new Color(1.0f, 1.0f, 1.0f, strength);
                _flames.Emit(emit, 1);
                if (Random.value < CoreShare)
                {
                    emit.startSize *= 0.55f;
                    emit.startColor = new Color(CoreBrightness, CoreBrightness * 0.97f, CoreBrightness * 0.9f, strength);
                    _flames.Emit(emit, 1);
                }
                if (Random.value < LickGlowShare)
                {
                    // A soft orange glow along it, so its flames read as one burning blade and not a row of little fires.
                    emit.velocity = Vector3.zero;
                    emit.startSize = LickSize * 1.6f * scale;
                    emit.startColor = new Color(GlowColour.r, GlowColour.g, GlowColour.b, 0.35f * strength);
                    _glow.Emit(emit, 1);
                }
            }
            if (Random.value < EmbersPerSecond * strength * deltaTime)
            {
                EmitEmber(Vector3.Lerp(from, to, Random.value), 0.6f);
            }
        }

        /// <summary>A burning body's fire: flames up and down the torso, a few embers, smoke.</summary>
        public void Burn(Vector3 chest, float strength, float deltaTime)
        {
            float debt = 0.0f;
            EmitFlames(chest + Vector3.up * Random.Range(-0.9f, 0.5f), strength, 0.7f, ref debt, FlamesPerSecond * 1.8f * strength * deltaTime, 0.3f);
            if (Random.value < EmbersPerSecond * strength * deltaTime)
            {
                EmitEmber(chest + Random.insideUnitSphere * 0.35f, 0.8f);
            }
            if (Random.value < SmokePerSecond * strength * deltaTime)
            {
                EmitSmoke(chest + Vector3.up * 0.7f, strength, 0.6f);
            }
        }

        /// <summary>
        /// Flames at <paramref name="at"/>: quads the shader draws a tongue on, each with the cell's <paramref name="heat"/> as its strength
        /// (the vertex alpha), standing a little over the ground so the soft fade does not eat their base.
        /// </summary>
        private void EmitFlames(Vector3 at, float heat, float size, ref float debt, float count, float spread = 0.3f)
        {
            debt += count;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            while (debt >= 1.0f)
            {
                debt -= 1.0f;
                Vector2 disc = Random.insideUnitCircle * spread;
                emit.position = at + new Vector3(disc.x, 0.08f + Random.value * 0.2f * heat, disc.y);
                emit.velocity = Vector3.up * (Random.Range(0.7f, 1.3f) * (0.6f + 0.4f * heat)) + new Vector3(Random.Range(-0.12f, 0.12f), 0.0f, Random.Range(-0.12f, 0.12f));
                emit.startSize = Random.Range(1.1f, 1.6f) * (0.55f + 0.6f * heat) * size;
                emit.startLifetime = Random.Range(0.6f, 1.0f);
                emit.startColor = new Color(1.0f, 1.0f, 1.0f, heat);
                _flames.Emit(emit, 1);
                if (Random.value < CoreShare)
                {
                    // A hot core: a smaller, brighter, quicker tongue at the base, where a fire is hottest.
                    emit.startSize *= 0.55f;
                    emit.startLifetime *= 0.6f;
                    emit.startColor = new Color(CoreBrightness, CoreBrightness * 0.97f, CoreBrightness * 0.9f, heat);
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
                startSize = Random.Range(0.03f, 0.06f),
                startLifetime = Random.Range(0.9f, 2.2f),
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
                startSize = Random.Range(0.8f, 1.2f) * (0.7f + 0.5f * heat) * size,
                startLifetime = Random.Range(2.5f, 4.0f),
                startColor = SmokeColour,
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

        /// <summary>
        /// One shared particle system, fed by hand. The fire shader's layers hand each particle its uv, its own random number and its age
        /// (<paramref name="fireStreams"/>: the vertex streams the shader reads as TEXCOORD0 xyzw).
        /// </summary>
        private static ParticleSystem Make(Transform parent, string name, Material material, int maxParticles, ParticleSystemRenderMode mode, bool fireStreams)
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
            if (fireStreams)
            {
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
                    ParticleSystemVertexStream.StableRandomX, ParticleSystemVertexStream.AgePercent,
                });
            }
            return particles;
        }

        /// <summary>The shader draws and fades each flame; the particle only grows a little as it rises and wavers in the noise.</summary>
        private static void StyleFlames(ParticleSystem particles)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(new Keyframe(0.0f, 0.7f), new Keyframe(0.3f, 1.0f), new Keyframe(0.7f, 1.15f), new Keyframe(1.0f, 1.05f)));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
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

        /// <summary>The shader fades and lights each puff; the particle grows as it rises and drifts in the noise.</summary>
        private static void StyleSmoke(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = -0.02f;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 1.0f, 1.0f, 2.6f));
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;
        }
    }
}
