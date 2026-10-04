using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The Bloodrage aura prefab: red embers and flame wisps rising off the whole body (a box of emission round the player, additive and soft), a
    /// second thicker low glow, and a red light. BloodrageAbility starts it with the ability and fades both the emission and the light out.
    /// </summary>
    public static class BloodrageAuraBuilder
    {
        private const string PrefabPath = "Assets/CityX/Character/BloodrageAura.prefab";
        private const string TexturePath = "Assets/CityX/Character/BloodrageAuraDot.png";
        private const string MaterialPath = "Assets/CityX/Character/BloodrageAura.mat";

        private static readonly Color Red = new Color(1.0f, 0.08f, 0.04f, 1.0f);

        [MenuItem("CityX/Rebuild Bloodrage Aura")]
        public static GameObject Build()
        {
            Texture2D dot = EnsureTexture();
            Material material = EnsureMaterial(dot);
            float height = BodyRig.StandingHeight;

            var root = new GameObject("BloodrageAura");
            try
            {
                AddParticles(root.transform, "Wisps", material, height, 70.0f, 1.0f, 0.5f, 1.0f, 1.1f);
                AddParticles(root.transform, "Haze", material, height, 22.0f, 1.4f, 1.2f, 2.0f, 0.35f);

                var lightObject = new GameObject("Glow");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = new Vector3(0.0f, height * 0.55f, 0.0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.12f, 0.05f);
                light.range = 9.0f;
                light.intensity = 3.2f;
                light.shadows = LightShadows.None;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: bloodrage aura written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddParticles(Transform parent, string name, Material material, float height, float rate, float lifetime,
            float sizeMin, float sizeMax, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0.0f, height * 0.5f, 0.0f);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 5.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(Red.r, 0.05f, 0.02f, alpha), new Color(1.0f, 0.3f, 0.1f, alpha));
            main.gravityModifier = -0.15f;   // rises
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // a trail of it hangs in the air behind a runner
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            // Off the surface of the body, all the way up it.
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, height * 0.95f, 0.6f);

            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1.0f, 0.5f, 0.3f), 0.0f), new GradientColorKey(new Color(0.9f, 0.05f, 0.02f), 0.5f), new GradientColorKey(new Color(0.4f, 0.0f, 0.0f), 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.15f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(new Keyframe(0.0f, 0.8f), new Keyframe(0.3f, 1.0f), new Keyframe(1.0f, 0.1f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Texture2D EnsureTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2.0f - 1.0f, dy = (y + 0.5f) / size * 2.0f - 1.0f;
                    float d = Mathf.Clamp01(1.0f - Mathf.Sqrt(dx * dx + dy * dy));
                    float a = d * d * (3.0f - 2.0f * d);
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material EnsureMaterial(Texture2D dot)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find("Legacy Shaders/Particles/Additive");
            material.SetTexture("_MainTex", dot);
            material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
