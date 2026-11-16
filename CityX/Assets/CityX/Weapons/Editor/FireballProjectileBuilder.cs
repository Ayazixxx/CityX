using CityX.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the fireball projectile (docs/FIRE.md, "The fire spells"): a ball of molten fire -- Unity's sphere on the CityX/FireballCore
    /// shader, its seams heating as it flies -- with its own light. Its wake, embers and smoke are the fire's shared look, left as it flies
    /// (FireballProjectile, FireScene.Wake), not particles of its own. No Godot source to port numbers from (FireballCaster's doc comment).
    /// </summary>
    public static class FireballProjectileBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/FireballProjectile.prefab";
        private const string CoreMaterialPath = "Assets/CityX/Weapons/FireballCore.mat";
        public const string CoreShaderName = "CityX/FireballCore";

        /// <summary>"LARGE", from the explicit ask -- noticeably bigger than a thrown bottle or a bullet.</summary>
        private const float Radius = 0.16f;

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
                var core = visual.GetComponent<MeshRenderer>();
                core.sharedMaterial = CoreMaterial();
                core.shadowCastingMode = ShadowCastingMode.Off;   // a light, not a thing in its light
                core.receiveShadows = false;

                // Its glow: a real light, so it lights what it flies past at night (FireballProjectile.GlowColour; flickered at run time).
                var glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(root.transform, false);
                Light glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = FireballProjectile.GlowColour;
                glow.intensity = FireballProjectile.GlowIntensity;
                glow.range = FireballProjectile.GlowRange;
                glow.shadows = LightShadows.None;

                var collider = root.AddComponent<SphereCollider>();
                collider.radius = Radius;

                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.useGravity = false; // a spell flies true, it doesn't arc like a thrown bottle
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var fireball = root.AddComponent<FireballProjectile>();
                fireball.Core = core;
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

        /// <summary>The core's material, an asset so a build keeps its shader; each ball's heat and seams go on it per ball (a property block).</summary>
        private static Material CoreMaterial()
        {
            Shader shader = Shader.Find(CoreShaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, CoreMaterialPath);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
