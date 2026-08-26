using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the flame-burst prefab a fireball spawns on impact -- same
    /// structural shape as GlassShatterBuilder (a one-shot ParticleSystem
    /// burst plus a self-cleaning FireBurst component), with a fire
    /// palette, a bigger/longer burst befitting "a LARGE fireball", and a
    /// decaying point light on top for the detonation flash weapons/
    /// explosion.gd's own OmniLight3D gave the reference for (see
    /// FireBurst.Update's own doc comment on why that decay is cubic).
    /// </summary>
    public static class FireBurstBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/FireBurst.prefab";
        private const string MaterialPath = "Assets/CityX/Weapons/FireBurstParticle.mat";

        private const int Particles = 40;
        private const float Lifetime = 0.5f;
        private const float SpeedMin = 2.0f;
        private const float SpeedMax = 5.0f;
        private const float SizeMin = 0.15f;
        private const float SizeMax = 0.35f;

        private static readonly Color CoreColour = new Color(1.0f, 0.85f, 0.3f);
        private static readonly Color EdgeColour = new Color(0.9f, 0.25f, 0.05f);

        [MenuItem("CityX/Rebuild Fire Burst")]
        public static GameObject Build()
        {
            var root = new GameObject("FireBurst");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();

                ParticleSystem.MainModule main = particles.main;
                main.duration = Lifetime;
                main.loop = false;
                main.startLifetime = Lifetime;
                main.startSpeed = new ParticleSystem.MinMaxCurve(SpeedMin, SpeedMax);
                main.startSize = new ParticleSystem.MinMaxCurve(SizeMin, SizeMax);
                main.startColor = CoreColour;
                main.gravityModifier = -0.15f; // embers drift up, not down
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Particles;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0.0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, Particles) });

                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(CoreColour, 0.0f),
                        new GradientColorKey(EdgeColour, 0.4f),
                        new GradientColorKey(new Color(0.2f, 0.05f, 0.02f), 1.0f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(0.8f, 0.5f),
                        new GradientAlphaKey(0.0f, 1.0f),
                    });
                fade.color = new ParticleSystem.MinMaxGradient(gradient);

                ParticleSystem.SizeOverLifetimeModule shrink = particles.sizeOverLifetime;
                shrink.enabled = true;
                shrink.size = new ParticleSystem.MinMaxCurve(1.0f,
                    AnimationCurve.EaseInOut(0.0f, 1.0f, 1.0f, 0.2f));

                var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.color = Color.white;
                AssetDatabase.CreateAsset(material, MaterialPath);

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // THE DETONATION FLASH -- decayed by FireBurst.Update, not
                // here; this only sets the peak brightness it decays from.
                var flashObject = new GameObject("Flash");
                flashObject.transform.SetParent(root.transform, false);
                Light flash = flashObject.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.color = new Color(1.0f, 0.6f, 0.25f);
                flash.intensity = 8.0f;
                flash.range = 6.0f;
                flash.shadows = LightShadows.None;

                root.AddComponent<FireBurst>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: fire burst written to {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
