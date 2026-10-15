using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The Acid Rain storm prefab: a high ceiling of big, slow-churning storm-grey cloud (the poison gas's own soft puff flipbook, tinted), green rain
    /// streaks falling out of it that die on the first thing they hit (a roof stops them, as it stops the burn), a faint sickly glow, and the city's
    /// own rain sound, lower and heavier. Sized from AcidRain's constants, so the look and the damage cover the same circle.
    /// </summary>
    public static class AcidRainBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/AcidRainStorm.prefab";
        private const string StreakTexturePath = "Assets/CityX/Weapons/AcidRainStreak.png";
        private const string StreakMaterialPath = "Assets/CityX/Weapons/AcidRainStreak.mat";
        private const string PuffMaterialPath = "Assets/CityX/Weapons/PoisonGas.mat";
        private const string RainSoundPath = "Assets/CityX/UI/Audio/rain_noises.mp3";

        private static readonly Color CloudDark = new Color(0.16f, 0.17f, 0.19f);
        private static readonly Color CloudSick = new Color(0.34f, 0.35f, 0.37f);
        private static readonly Color CloudPale = new Color(0.50f, 0.51f, 0.52f);

        private const string SplashTexturePath = "Assets/CityX/Weapons/AcidRainSplash.png";
        private const string SplashMaterialPath = "Assets/CityX/Weapons/AcidRainSplash.mat";

        /// <summary>
        /// The cloud puffs' sizes, metres. They are 1.5 km up (AcidRain.CloudHeight) and must look as big from the street as the old 70 m ones
        /// did, so they are some twenty times bigger -- and so few are needed to cover the sky that the deck is far cheaper to draw than before.
        /// </summary>
        private static readonly Vector2 CloudPuffSize = new Vector2(550.0f, 850.0f);

        private static readonly Vector2 UnderbellyPuffSize = new Vector2(350.0f, 600.0f);

        [MenuItem("CityX/Rebuild Acid Rain")]
        public static GameObject Build()
        {
            Material puffs = AssetDatabase.LoadAssetAtPath<Material>(PuffMaterialPath);
            if (puffs == null)
            {
                PoisonGasBuilder.Build();
                puffs = AssetDatabase.LoadAssetAtPath<Material>(PuffMaterialPath);
            }
            Material streak = EnsureStreakMaterial();

            var root = new GameObject("AcidRainStorm");
            try
            {
                AddClouds(root.transform, "Clouds", puffs, rate: 6.0f, burst: 40, size: CloudPuffSize, alpha: 0.85f, low: CloudDark, high: CloudSick,
                    height: AcidRain.CloudHeight, radius: AcidRain.CloudRadius);
                AddClouds(root.transform, "Underbelly", puffs, rate: 4.0f, burst: 24, size: UnderbellyPuffSize, alpha: 0.55f, low: CloudSick,
                    high: CloudPale, height: AcidRain.CloudHeight - 60.0f, radius: AcidRain.CloudRadius * 0.8f);
                AddRain(root.transform, streak);
                AddSplashes(root.transform, EnsureSplashMaterial());

                var glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(root.transform, false);
                glowObject.transform.localPosition = Vector3.up * AcidRain.GlowHeight;
                Light glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.62f, 0.85f, 0.45f);   // only a faint sickly cast now the clouds are grey: the acid is the rain
                glow.range = AcidRain.Radius * 1.8f;
                glow.intensity = 0.9f;
                glow.shadows = LightShadows.None;

                var soundObject = new GameObject("Sound");
                soundObject.transform.SetParent(root.transform, false);
                AudioSource sound = soundObject.AddComponent<AudioSource>();
                sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RainSoundPath);
                sound.loop = true;
                sound.playOnAwake = false;
                sound.spatialBlend = 1.0f;
                sound.rolloffMode = AudioRolloffMode.Linear;
                sound.minDistance = AcidRain.Radius * 0.5f;
                sound.maxDistance = AcidRain.Radius * 2.5f;
                sound.volume = 0.9f;
                sound.pitch = 0.8f;   // lower and heavier than ordinary rain

                root.AddComponent<AcidRainStorm>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A flat disc of huge slow puffs at <paramref name="height"/>, <paramref name="radius"/> across, the storm's ceiling: <paramref name="burst"/>
        /// at once (a full sky the moment it is cast) and <paramref name="rate"/> a second after.
        /// </summary>
        private static void AddClouds(Transform parent, string name, Material material, float rate, int burst, Vector2 size, float alpha, Color low,
            Color high, float height, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 6.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6.0f, 9.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.0f, 10.0f);   // a slow drift, at 1.5 km
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(low.r, low.g, low.b, alpha), new Color(high.r, high.g, high.b, alpha));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            // Already a full sky the moment it is cast: the warning is the clouds darkening in, not the sky slowly filling.
            main.prewarm = false;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)burst) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 1.0f;
            shape.rotation = new Vector3(90.0f, 0.0f, 0.0f);   // lie flat

            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(1.0f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = gradient;

            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            ParticleSystem.NoiseModule churn = particles.noise;
            churn.enabled = true;
            churn.strength = 20.0f;   // the churn of a cloud a kilometre and a half away: big, slow
            churn.frequency = 0.004f;
            churn.scrollSpeed = 0.15f;
            churn.quality = ParticleSystemNoiseQuality.Medium;

            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0.0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0.0f, 4.0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 4.0f;   // big puffs stay big up close
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The drops: thin green streaks. Nothing is emitted by rate or shape: AcidRainStorm drops each one by script from just above the viewer,
        /// with the lifetime that ends it on the first thing below it (a roof, the street), as the city rain does -- the particle system's own
        /// collision let a quarter of them through the ground at 30 m/s.
        /// </summary>
        private static ParticleSystem AddRain(Transform parent, Material material)
        {
            var go = new GameObject(AcidRainStorm.RainName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * AcidRain.RainFromHeight;   // moved up above a flier at run time (AcidRainStorm)
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 2.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = AcidRain.MaxDropFall / AcidRain.DropSpeed;   // each drop's own is set as it is dropped
            main.startSpeed = 0.0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(1.0f, 1.0f);
            main.startSizeZ = 1.0f;
            main.startColor = new ParticleSystem.MinMaxGradient(AcidRain.DropGreen, AcidRain.DropPale);   // each drop's own is set as it is dropped
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 9000;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;   // 30 m/s -> a 1.5 m streak
            renderer.lengthScale = 1.0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        /// <summary>
        /// GREEN SPLASHES WHERE THE DROPS LAND (owner, 2026-10-15: "when you do /rain there are these droplet effects that appear on the floor
        /// that look good but they arent there during acid rain make a green version ... the same green as the acid rain droplets in the air").
        /// The city rain's splash, made the way that one is (a few tiny dots thrown up and fading in 0.3 s): AcidRainStorm throws them where and
        /// when each drop near the viewer lands, in that drop's own colour.
        /// </summary>
        private static void AddSplashes(Transform parent, Material material)
        {
            var go = new GameObject(AcidRainStorm.SplashesName);
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1.0f;
            main.startLifetime = 0.3f;
            main.startSpeed = 0.0f;
            // A crown a few centimetres across, as the city rain's (RainEffect.BuildSplashes).
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = Color.white;   // each splash's own is its drop's
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The splash material: URP's unlit particle shader, see-through, WHITE, so each splash's drop colour is the colour; a small soft dot
        /// drawn here (16 x 16), as the city rain's own splash dot is.
        /// </summary>
        private static Material EnsureSplashMaterial()
        {
            Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashTexturePath);
            if (dot == null)
            {
                const int size = 16;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2.0f - 1.0f;
                        float dy = (y + 0.5f) / size * 2.0f - 1.0f;
                        float fall = Mathf.Clamp01(1.0f - (dx * dx + dy * dy));
                        texture.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, fall * fall));
                    }
                }
                texture.Apply();
                System.IO.File.WriteAllBytes(SplashTexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SplashTexturePath, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(SplashTexturePath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                dot = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashTexturePath);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(SplashMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, SplashMaterialPath);
            }
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            material.SetTexture("_BaseMap", dot);
            material.mainTexture = dot;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A small soft streak: bright down the middle, fading to the sides and the ends.</summary>
        private static Material EnsureStreakMaterial()
        {
            const int width = 8, height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float across = 1.0f - Mathf.Abs((x + 0.5f) / width * 2.0f - 1.0f);
                    float along = Mathf.Sin((y + 0.5f) / height * Mathf.PI);
                    texture.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp01(across * across * along)));
                }
            }
            texture.Apply();
            System.IO.File.WriteAllBytes(StreakTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(StreakTexturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(StreakTexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(StreakMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, StreakMaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(StreakTexturePath));
            material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
