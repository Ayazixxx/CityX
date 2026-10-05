using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The poison gas assets. THE LOOK follows what the research says makes particle smoke read as smoke rather than as confetti: many LARGE, SOFT
    /// puffs with a fractal-noise silhouette (not a perfect disc), four different puff shapes in one flipbook texture picked at random, each turning
    /// slowly, growing as it ages and fading in and out (alpha rises fast, falls slowly), pushed about by the noise module so the cloud churns,
    /// simulated in world space so it hangs where it was blown instead of following the mouth, plus a thin layer of small brighter wisps and a
    /// faint purple light. Alpha-blended (smoke occludes; additive would read as fire or magic).
    /// </summary>
    public static class PoisonGasBuilder
    {
        private const string CloudPrefabPath = "Assets/CityX/Weapons/PoisonGasCloud.prefab";
        private const string VictimPrefabPath = "Assets/CityX/Weapons/PoisonVictimFx.prefab";
        private const string TexturePath = "Assets/CityX/Weapons/PoisonGasPuffs.png";
        private const string MaterialPath = "Assets/CityX/Weapons/PoisonGas.mat";

        private static readonly Color Purple = new Color(0.55f, 0.14f, 0.85f);
        private static readonly Color DeepPurple = new Color(0.30f, 0.04f, 0.50f);
        private static readonly Color Magenta = new Color(0.85f, 0.25f, 0.95f);

        [MenuItem("CityX/Rebuild Poison Gas")]
        public static GameObject Build()
        {
            Material material = EnsureMaterial(EnsureTexture());
            GameObject victimFx = BuildVictimFx(material);

            var root = new GameObject("PoisonGasCloud");
            try
            {
                AddSmoke(root.transform, "Gas", material, rate: 70.0f, lifetime: new Vector2(3.2f, 4.4f), size: new Vector2(1.9f, 3.5f),
                    speed: new Vector2(0.15f, 0.7f), alpha: 0.55f, low: DeepPurple, high: Purple, maxParticles: 450, noise: 0.55f);
                AddSmoke(root.transform, "Wisps", material, rate: 38.0f, lifetime: new Vector2(1.4f, 2.4f), size: new Vector2(0.7f, 1.4f),
                    speed: new Vector2(0.4f, 1.3f), alpha: 0.4f, low: Purple, high: Magenta, maxParticles: 200, noise: 0.9f);

                var lightObject = new GameObject("Glow");
                lightObject.transform.SetParent(root.transform, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.6f, 0.2f, 1.0f);
                light.range = 7.0f;
                light.intensity = 1.8f;
                light.shadows = LightShadows.None;

                var cloud = root.AddComponent<PoisonGasCloud>();
                cloud.VictimFxPrefab = victimFx;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CloudPrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddSmoke(Transform parent, string name, Material material, float rate, Vector2 lifetime, Vector2 size, Vector2 speed,
            float alpha, Color low, Color high, int maxParticles, float noise)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 4.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(low.r, low.g, low.b, alpha), new Color(high.r, high.g, high.b, alpha));
            main.gravityModifier = -0.03f;   // the faintest rise
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;   // the cloud resizes this as it swells

            // Alpha rises fast and falls slowly: a puff appears out of the haze and thins away.
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.8f, 0.7f, 0.9f), 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.14f), new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = gradient;

            ParticleSystem.SizeOverLifetimeModule growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(new Keyframe(0.0f, 0.65f), new Keyframe(0.5f, 1.0f), new Keyframe(1.0f, 1.3f)));

            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);

            // The churn: slow, smooth turbulence (low frequency, high quality) so the bank rolls instead of puffing out evenly.
            ParticleSystem.NoiseModule turbulence = particles.noise;
            turbulence.enabled = true;
            turbulence.strength = noise;
            turbulence.frequency = 0.4f;
            turbulence.scrollSpeed = 0.25f;
            turbulence.damping = true;
            turbulence.quality = ParticleSystemNoiseQuality.High;

            // Four different puffs in one flipbook, a random one each: no two neighbours look stamped from the same mould.
            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0.0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0.0f, 4.0f);
            sheet.cycleCount = 0;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>A few small purple bubbles rising round a poisoned body's chest.</summary>
        private static GameObject BuildVictimFx(Material material)
        {
            var root = new GameObject("PoisonVictimFx");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = particles.main;
                main.duration = 2.0f;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(Purple.r, Purple.g, Purple.b, 0.6f), new Color(Magenta.r, Magenta.g, Magenta.b, 0.6f));
                main.gravityModifier = -0.25f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 14.0f;
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.4f;

                ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
                colour.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                    new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) });
                colour.color = gradient;

                ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0.0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0.0f, 4.0f);

                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, VictimPrefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- the puff texture: four fractal-noise clouds in a 2 x 2 flipbook ----

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0f;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3.0f - 2.0f * fx);
            fy = fy * fy * (3.0f - 2.0f * fy);
            float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), fx);
            float b = Mathf.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float Fbm(float x, float y, int seed)
        {
            float sum = 0.0f, amplitude = 0.5f, frequency = 1.0f;
            for (int octave = 0; octave < 5; octave++)
            {
                sum += ValueNoise(x * frequency, y * frequency, seed + octave * 17) * amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }
            return sum;
        }

        private static Texture2D EnsureTexture()
        {
            const int cell = 128, size = cell * 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int frame = 0; frame < 4; frame++)
            {
                int ox = (frame % 2) * cell, oy = (frame / 2) * cell;
                for (int y = 0; y < cell; y++)
                {
                    for (int x = 0; x < cell; x++)
                    {
                        float u = (x + 0.5f) / cell * 2.0f - 1.0f, v = (y + 0.5f) / cell * 2.0f - 1.0f;
                        float radial = Mathf.Clamp01(1.0f - Mathf.Sqrt(u * u + v * v));
                        radial = radial * radial * (3.0f - 2.0f * radial);
                        // The silhouette is the radial falloff bitten into by noise: ragged, billowing edges and thin patches inside.
                        float cloud = Fbm(u * 1.7f + 3.0f * frame, v * 1.7f - 2.0f * frame, 7 + frame * 31);
                        float alpha = Mathf.Clamp01((radial * 1.5f - 0.35f) + (cloud - 0.45f) * 1.3f * radial);
                        alpha = alpha * alpha * (3.0f - 2.0f * alpha);
                        // A little shading in the colour too: lighter on the upper left, darker underneath, so a puff has a body.
                        float shade = Mathf.Clamp01(0.82f + (u * -0.12f + v * 0.16f) + (cloud - 0.5f) * 0.3f);
                        pixels[(oy + y) * size + ox + x] = new Color(shade, shade, shade, alpha);
                    }
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material EnsureMaterial(Texture2D puffs)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            material.SetTexture("_MainTex", puffs);
            // This shader draws 2 x tint x vertex colour x texture, so a half-grey tint is a neutral one.
            material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
