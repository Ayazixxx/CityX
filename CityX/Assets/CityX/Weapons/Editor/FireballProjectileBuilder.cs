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

        /// <summary>
        /// The trail streaming off the ball -- EXPLICIT ASK, to look like
        /// the jetpack/flamethrower's own fire rather than its own
        /// hand-rolled lookalike: "make the fireball trail... the same as
        /// the jetpack and flamethrower fire effect... turn it into the
        /// flamethrower fire effect that one looks better." Reuses
        /// FlamethrowerBuilder's own outer/core flame construction exactly
        /// the way JetpackBuilder.BuildFlame already does (both builders
        /// live in this same namespace), rather than re-deriving a second,
        /// visually different fire -- the old version here was an
        /// untextured, non-additive, Lit-shader cone with no noise/flicker,
        /// which is the whole reason it read as a marble's dust trail
        /// rather than fire.
        /// </summary>
        private static void BuildTrail(Transform parent)
        {
            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(parent, false);
            // A 180-degree YAW, not a pitch -- WRONG UNTIL THIS FIX,
            // reported directly as "the fireball trail is aimed upward."
            // FlamethrowerBuilder's Cone shape (see BuildOuterFlame/
            // BuildCoreFlame) emits along local +Z unrotated -- confirmed
            // by FlamethrowerBuilder's own "Nozzle" wrapper, which needs
            // NO rotation at all because the gun's muzzle already faces
            // +Z. The old -90-degree-pitch value here was carried over
            // unchanged from this trail's original hand-rolled
            // ParticleSystem and happened to point local +Z to local +Y
            // (up) -- invisible on the old faint, non-additive trail, but
            // glaring on this additive one. Instantiate orients the whole
            // prefab with +Z toward the launch direction (see
            // FireballProjectile.Spawn's own Quaternion.LookRotation), so
            // the fix is a straight 180-degree turn: +Z becomes -Z,
            // pointing the flame straight out the back.
            trailObject.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);

            Texture2D particleTexture = FlamethrowerBuilder.BuildParticleTexture();
            Material flameMaterial = FlamethrowerBuilder.BuildFlameMaterial(particleTexture);

            ParticleSystem outer = FlamethrowerBuilder.BuildOuterFlame(trailObject.transform, flameMaterial);
            FlamethrowerBuilder.BuildCoreFlame(outer.transform, flameMaterial);

            // Unlike the jetpack/flamethrower, which toggle their flame on
            // input and so build it stopped-and-cleared, a flying
            // fireball's trail streams continuously from the moment it
            // spawns to the moment it's destroyed on impact -- both layers
            // need to play immediately.
            foreach (ParticleSystem particles in outer.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.playOnAwake = true;
                particles.Play();
            }
        }
    }
}
