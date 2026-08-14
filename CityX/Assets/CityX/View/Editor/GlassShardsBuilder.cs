using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Builds the glass shard burst from weapons/glass_shatter.tscn's own
    /// numbers, the same way the blood splatter is built: the values stay
    /// greppable next to the Godot ones they came from, and rebuilding is how
    /// the port gets checked rather than comparing inspector fields by eye.
    ///
    /// OMNIDIRECTIONAL, unlike the blood. Blood sprays away from the surface it
    /// came off; a pane of glass giving way bursts every way at once, which is
    /// why Godot's version needs no orientation at its spawn site and this one
    /// is a sphere rather than a cone.
    /// </summary>
    public static class GlassShardsBuilder
    {
        private const string PrefabPath = "Assets/CityX/View/GlassShards.prefab";
        private const string MaterialPath = "Assets/CityX/View/Materials/GlassShards.mat";

        // --- straight from the .tscn ---
        private const int Shards = 16;                   // amount
        private const float Lifetime = 0.4f;             // lifetime
        private const float SpreadDegrees = 180.0f;      // spread
        private const float SpeedMin = 1.5f;             // initial_velocity_min
        private const float SpeedMax = 3.5f;             // initial_velocity_max
        private const float ScaleMin = 0.5f;             // scale_min
        private const float ScaleMax = 1.3f;             // scale_max
        private const float ShardSize = 0.05f;

        private static readonly Color Glass = new Color(0.85f, 0.92f, 0.90f, 1.0f);

        [MenuItem("CityX/Rebuild Glass Shards")]
        public static GameObject Build()
        {
            var root = new GameObject("GlassShards");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();

                ParticleSystem.MainModule main = particles.main;
                main.duration = Lifetime;
                main.loop = false;
                main.startLifetime = Lifetime;
                main.startSpeed = new ParticleSystem.MinMaxCurve(SpeedMin, SpeedMax);
                main.startSize = new ParticleSystem.MinMaxCurve(
                    ShardSize * ScaleMin, ShardSize * ScaleMax);
                main.startColor = Glass;
                main.gravityModifier = 1.0f;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Shards;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0.0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, Shards) });

                // Spread 180 in Godot is the whole sphere, so this is a sphere
                // rather than a cone opened very wide -- the same shape, and
                // without a direction nothing has to aim it.
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                // The colour ramp: pale glass fading its alpha out entirely by
                // the end, so shards vanish rather than settling on the road.
                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Glass, 0.0f),
                        new GradientColorKey(new Color(0.75f, 0.85f, 0.82f), 0.5f),
                        new GradientColorKey(new Color(0.70f, 0.80f, 0.78f), 1.0f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 0.5f),
                        new GradientAlphaKey(0.0f, 1.0f),
                    });
                fade.color = new ParticleSystem.MinMaxGradient(gradient);

                var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.color = Glass;
                AssetDatabase.CreateAsset(material, MaterialPath);

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: glass shards written to {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
