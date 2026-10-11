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
        private static readonly Color Acid = new Color(0.55f, 1.0f, 0.15f);

        /// <summary>How fast the drops fall, metres per second, and how long they may live (cloud to the deepest street).</summary>
        private const float DropSpeed = 30.0f;

        /// <summary>
        /// Where the drops start: well under the 70 m clouds, at about the height of a ten-storey roof. From the street it reads the same as
        /// rain from the clouds, and over a 60 m radius it keeps the number of drops alive (and colliding with the world) affordable.
        /// </summary>
        private const float RainFromHeight = 36.0f;

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
                AddClouds(root.transform, "Clouds", puffs, rate: 150.0f, size: new Vector2(34.0f, 56.0f), alpha: 0.85f, low: CloudDark, high: CloudSick,
                    height: AcidRain.CloudHeight);
                AddClouds(root.transform, "Underbelly", puffs, rate: 80.0f, size: new Vector2(18.0f, 32.0f), alpha: 0.55f, low: CloudSick, high: CloudPale,
                    height: AcidRain.CloudHeight - 4.0f);
                AddRain(root.transform, streak);

                var glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(root.transform, false);
                glowObject.transform.localPosition = Vector3.up * (AcidRain.CloudHeight * 0.5f);
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

        /// <summary>A flat disc of huge slow puffs at <paramref name="height"/>, the storm's ceiling.</summary>
        private static void AddClouds(Transform parent, string name, Material material, float rate, Vector2 size, float alpha, Color low, Color high,
            float height)
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
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.0f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(low.r, low.g, low.b, alpha), new Color(high.r, high.g, high.b, alpha));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2200;
            // Already a full sky the moment it is cast: the warning is the clouds darkening in, not the sky slowly filling.
            main.prewarm = false;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)(rate * 4.0f)) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = AcidRain.Radius;
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
            churn.strength = 1.2f;
            churn.frequency = 0.08f;
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

        /// <summary>The drops: thin green streaks falling from just under the clouds, killed by the first surface they hit.</summary>
        private static void AddRain(Transform parent, Material material)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * RainFromHeight;
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 2.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = (RainFromHeight + AcidRain.Depth) / DropSpeed;
            main.startSpeed = 0.0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(1.0f, 1.0f);
            main.startSizeZ = 1.0f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(Acid.r, Acid.g, Acid.b, 0.55f), new Color(0.8f, 1.0f, 0.4f, 0.75f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 9000;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 3000.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = AcidRain.Radius;
            shape.radiusThickness = 1.0f;
            shape.rotation = new Vector3(90.0f, 0.0f, 0.0f);

            ParticleSystem.VelocityOverLifetimeModule fall = particles.velocityOverLifetime;
            fall.enabled = true;
            fall.space = ParticleSystemSimulationSpace.World;
            fall.x = new ParticleSystem.MinMaxCurve(0.0f);
            fall.y = new ParticleSystem.MinMaxCurve(-DropSpeed);
            fall.z = new ParticleSystem.MinMaxCurve(0.0f);

            // A roof, a deck or the street ends a drop: the rain visibly does not get under cover, and the burn does not either.
            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.lifetimeLoss = 1.0f;
            collision.bounce = 0.0f;
            collision.radiusScale = 0.1f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;   // 30 m/s -> a 1.5 m streak
            renderer.lengthScale = 1.0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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
