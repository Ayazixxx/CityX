using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the blood splatter prefab from weapons/blood_splatter.tscn's own
    /// numbers.
    ///
    /// A BUILDER RATHER THAN A HAND-AUTHORED PREFAB, matching how every other
    /// asset in this project is made: the numbers stay greppable next to the
    /// Godot ones they came from, and rebuilding is how you check the port
    /// rather than clicking through an inspector comparing values.
    /// </summary>
    public static class BloodSplatterBuilder
    {
        private const string PrefabPath = "Assets/CityX/Character/BloodSplatter.prefab";
        private const string MaterialPath = "Assets/CityX/Character/BloodSplatter.mat";

        // --- straight from the .tscn ---
        private const int Droplets = 14;                 // amount
        private const float Lifetime = 0.4f;             // lifetime
        private const float SpreadDegrees = 28.0f;       // spread
        private const float SpeedMin = 1.2f;             // initial_velocity_min
        private const float SpeedMax = 2.8f;             // initial_velocity_max
        private const float ScaleMin = 0.6f;             // scale_min
        private const float ScaleMax = 1.4f;             // scale_max
        private const float DropletDiameter = 0.06f;     // SphereMesh height, radius 0.03

        [MenuItem("CityX/Rebuild Blood Splatter")]
        public static GameObject Build()
        {
            var root = new GameObject("BloodSplatter");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();

                ParticleSystem.MainModule main = particles.main;
                main.duration = Lifetime;
                main.loop = false;
                main.startLifetime = Lifetime;
                main.startSpeed = new ParticleSystem.MinMaxCurve(SpeedMin, SpeedMax);
                main.startSize = new ParticleSystem.MinMaxCurve(
                    DropletDiameter * ScaleMin, DropletDiameter * ScaleMax);
                main.startColor = new Color(0.55f, 0.02f, 0.02f, 1.0f);
                // Godot's ParticleProcessMaterial pulls -9.8 explicitly.
                // Unity's own gravity is -9.81, so a modifier of 1 is the same
                // arc to within a hundredth of a metre per second squared.
                main.gravityModifier = 1.0f;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Droplets;

                // one_shot with explosiveness 1.0: the whole amount at once,
                // then nothing. A steady rate would read as a wound bleeding
                // rather than as a blow landing.
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0.0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, Droplets) });

                // direction (0,0,1) with spread 28 -- a cone about local +Z,
                // which is why Spawn aims +Z along the hit normal.
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = SpreadDegrees;
                shape.radius = 0.01f;

                // The colour ramp: full red, holding to 60% of life, then
                // fading its alpha out entirely by the end.
                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.55f, 0.02f, 0.02f), 0.0f),
                        new GradientColorKey(new Color(0.5f, 0.02f, 0.02f), 0.6f),
                        new GradientColorKey(new Color(0.4f, 0.02f, 0.02f), 1.0f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 0.6f),
                        new GradientAlphaKey(0.0f, 1.0f),
                    });
                fade.color = new ParticleSystem.MinMaxGradient(gradient);

                // Unshaded in Godot (shading_mode = 0). Blood at this size is
                // three pixels; lighting it costs more than it says.
                var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.color = new Color(0.5f, 0.02f, 0.02f, 1.0f);
                AssetDatabase.CreateAsset(material, MaterialPath);

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = material;
                // Droplets this small never cast a readable shadow and there
                // can be a lot of them at once.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                root.AddComponent<BloodSplatter>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: blood splatter written to {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
