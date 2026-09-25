using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the glass shatter prefab from weapons/glass_shatter.tscn's
    /// own numbers -- a structural copy of BloodSplatterBuilder, with the
    /// two real differences: an omnidirectional burst (Godot's spread=180,
    /// vs. blood's 28-degree cone) and flat glass-tinted quads instead of
    /// spheres.
    /// </summary>
    public static class GlassShatterBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/GlassShatter.prefab";
        private const string MaterialPath = "Assets/CityX/Weapons/GlassShatter.mat";

        // --- straight from the .tscn ---
        private const int Particles = 16;              // amount
        private const float Lifetime = 0.4f;            // lifetime
        private const float SpeedMin = 1.5f;             // initial_velocity_min
        private const float SpeedMax = 3.5f;             // initial_velocity_max
        private const float ScaleMin = 0.5f;             // scale_min
        private const float ScaleMax = 1.3f;             // scale_max
        private const float ShardWidth = 0.05f;          // BoxMesh size.x
        private const float ShardHeight = 0.08f;         // BoxMesh size.y

        [MenuItem("CityX/Rebuild Glass Shatter")]
        public static GameObject Build()
        {
            var root = new GameObject("GlassShatter");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();

                ParticleSystem.MainModule main = particles.main;
                main.duration = Lifetime;
                main.loop = false;
                main.startLifetime = Lifetime;
                main.startSpeed = new ParticleSystem.MinMaxCurve(SpeedMin, SpeedMax);
                main.startSize = new ParticleSystem.MinMaxCurve(
                    ShardWidth * ScaleMin, ShardWidth * ScaleMax);
                main.startColor = new Color(0.85f, 0.92f, 0.9f, 1.0f);
                main.gravityModifier = 1.0f;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Particles;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0.0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, Particles) });

                // A real burst outward in every direction (spread=180 in
                // Godot), unlike blood's directional spray -- a bottle
                // shatters, it doesn't spurt.
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.01f;

                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.85f, 0.92f, 0.9f), 0.0f),
                        new GradientColorKey(new Color(0.75f, 0.85f, 0.82f), 0.6f),
                        new GradientColorKey(new Color(0.7f, 0.8f, 0.78f), 1.0f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 0.6f),
                        new GradientAlphaKey(0.0f, 1.0f),
                    });
                fade.color = new ParticleSystem.MinMaxGradient(gradient);

                var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.color = new Color(0.85f, 0.92f, 0.9f, 1.0f);
                AssetDatabase.CreateAsset(material, MaterialPath);

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                Mesh shardMesh = BuildShardMesh();
                AssetDatabase.AddObjectToAsset(shardMesh, material); // parked on the material asset so it isn't loose
                renderer.mesh = shardMesh;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                root.AddComponent<GlassShatter>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: glass shatter written to {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A flat quad shard -- Godot's BoxMesh size=(0.05, 0.08, 0.015), double-sided.</summary>
        private static Mesh BuildShardMesh()
        {
            var mesh = new Mesh { name = "GlassShard" };
            float hw = ShardWidth * 0.5f;
            float hh = ShardHeight * 0.5f;
            mesh.SetVertices(new[]
            {
                new Vector3(-hw, -hh, 0.0f), new Vector3(hw, -hh, 0.0f),
                new Vector3(hw, hh, 0.0f), new Vector3(-hw, hh, 0.0f),
            });
            mesh.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            // Both winding orders, so it reads from either side (Godot's own cull_mode=2).
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
