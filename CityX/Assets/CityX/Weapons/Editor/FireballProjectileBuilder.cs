using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the fireball projectile -- a glowing sphere with a short
    /// trailing ember stream, procedural (matching BottleBuilder/
    /// JetpackBuilder/SwordBuilder's own reasoning: no glTF importer
    /// exists in this project for a hand-authored mesh, and a fireball is
    /// a sphere anyway, which needs no authored mesh at all). No Godot
    /// source to port numbers from -- see FireballCaster.cs's own doc
    /// comment.
    /// </summary>
    public static class FireballProjectileBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/FireballProjectile.prefab";

        /// <summary>"LARGE", from the explicit ask -- noticeably bigger than a thrown bottle or a bullet.</summary>
        private const float Radius = 0.16f;

        // Biased well down on green from a first attempt at (1.0, 0.7,
        // 0.15) -- rendered and confirmed that read as pale yellow rather
        // than fire, since red and green sat too close together.
        private static readonly Color CoreColour = new Color(1.0f, 0.45f, 0.05f);

        [MenuItem("CityX/Rebuild Fireball Projectile")]
        public static GameObject Build()
        {
            var root = new GameObject("FireballProjectile");
            try
            {
                // A SCALED PRIMITIVE, NOT A HAND-COPIED MESH -- see
                // JetpackBuilder's own class comment for why: a hand-copied
                // Mesh produced zero-bounds, invisible renderers here
                // before, and scaling a Unity primitive sidesteps whatever
                // that was entirely.
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Visual";
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * (Radius * 2.0f);

                Material material = CharacterSkin.Ensure("FireballCore", CoreColour);
                // EMISSIVE, not lit -- a fireball is a light source, not a
                // surface reflecting one; without this it reads as an
                // orange ball sitting in shadow rather than something on
                // fire.
                material.EnableKeyword("_EMISSION");
                // 1.4x, not higher -- confirmed directly by rendering it at
                // 2.5x first: with no HDR/bloom pipeline in a plain camera
                // capture, that clipped the orange straight to a pale
                // near-white disc instead of reading as fire.
                material.SetColor("_EmissionColor", CoreColour * 1.4f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetFloat("_Metallic", 0.0f);
                material.SetFloat("_Smoothness", 0.2f);
                visual.GetComponent<MeshRenderer>().sharedMaterial = material;

                BuildTrail(root.transform);

                var collider = root.AddComponent<SphereCollider>();
                collider.radius = Radius;

                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.useGravity = false; // a spell flies true, it doesn't arc like a thrown bottle
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var fireball = root.AddComponent<FireballProjectile>();
                fireball.BurstPrefab = FireBurstBuilder.Build();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: fireball projectile written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A short stream of embers trailing the ball, so it reads as flying fire rather than a thrown orange marble.</summary>
        private static void BuildTrail(Transform parent)
        {
            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(parent, false);
            // Backward, along -Z -- Instantiate orients the whole prefab
            // with +Z toward the launch direction (see
            // FireballProjectile.Spawn's own Quaternion.LookRotation), so
            // the trail has to point the opposite way to stream out behind.
            trailObject.transform.localRotation = Quaternion.Euler(-90.0f, 0.0f, 0.0f);

            ParticleSystem particles = trailObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.3f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(Radius * 0.6f, Radius * 1.1f);
            main.startColor = CoreColour;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 60.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10.0f;
            shape.radius = Radius * 0.5f;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(CoreColour, 0.0f),
                    new GradientColorKey(new Color(0.3f, 0.1f, 0.05f), 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f),
                });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = trailObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = CharacterSkin.Ensure("FireballTrailParticle", CoreColour);
            renderer.sharedMaterial.SetFloat("_Surface", 1.0f); // transparent, matching an emissive trail rather than a lit solid
            renderer.sharedMaterial.SetFloat("_Metallic", 0.0f);
            renderer.sharedMaterial.SetFloat("_Smoothness", 0.0f);
        }
    }
}
