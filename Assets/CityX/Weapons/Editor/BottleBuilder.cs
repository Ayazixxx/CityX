using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the glass bottle and thrown-bottle prefabs, from
    /// weapons/glass_bottle.gd's and thrown_bottle.gd's own numbers.
    ///
    /// BOTH VISUALS ARE PROCEDURAL, NOT bottle_model.glb. No glTF importer
    /// is installed in this project -- every .glb here (not just this one)
    /// sits on Unity's inert DefaultImporter and has never actually loaded
    /// as geometry. Adding a package dependency to fix that is a call for
    /// the user to make, not something to do silently mid-feature, so the
    /// intact bottle is built with BottleMesh.BuildIntact the same way the
    /// broken one already is.
    ///
    /// VisualRoot APPLIES BottleScale, THE ROOT NEVER DOES -- matching the
    /// Godot original's own shape: BOTTLE_SCALE (2.4x) lives entirely on
    /// the visual child transform, because the root's own position/
    /// rotation is the live grip anchor while equipped.
    /// </summary>
    public static class BottleBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/GlassBottle.prefab";
        private const string ThrownPrefabPath = "Assets/CityX/Weapons/ThrownBottle.prefab";

        // --- glass_bottle.gd's own unscaled proportions, in metres ---
        private const float BodyHeight = 0.15f;
        private const float ShoulderHeight = 0.02f;
        private const float NeckHeight = 0.045f;
        private const float RimHeight = 0.015f;
        private const float BodyRadius = 0.032f;
        private const float NeckRadius = 0.011f;
        private const float IntactHeight = BodyHeight + ShoulderHeight + NeckHeight + RimHeight;

        [MenuItem("CityX/Rebuild Glass Bottle")]
        public static GameObject Build()
        {
            var root = new GameObject("GlassBottle");
            try
            {
                var visualRoot = new GameObject("VisualRoot");
                visualRoot.transform.SetParent(root.transform, false);
                visualRoot.transform.localScale = Vector3.one * BottleWeapon.BottleScale;

                GameObject intact = BuildIntactVisual(visualRoot.transform);
                GameObject broken = BuildBrokenVisual(visualRoot.transform);
                broken.SetActive(false);

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = BodyRadius * BottleWeapon.BottleScale;
                capsule.height = IntactHeight * BottleWeapon.BottleScale;
                capsule.center = new Vector3(0.0f, capsule.height * 0.5f, 0.0f);
                capsule.material = new PhysicsMaterial("BottleGlassPhysics")
                {
                    bounciness = BottleWeapon.BottleBounce,
                    dynamicFriction = BottleWeapon.BottleFriction,
                    staticFriction = BottleWeapon.BottleFriction,
                };

                var body = root.AddComponent<Rigidbody>();
                body.mass = BottleWeapon.BottleMass;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = BottleWeapon.InteractRange;

                var bottle = root.AddComponent<BottleWeapon>();
                bottle.VisualRoot = visualRoot.transform;
                bottle.IntactVisual = intact;
                bottle.BrokenVisual = broken;
                bottle.ThrownBottlePrefab = BuildThrownBottle();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: glass bottle written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildIntactVisual(Transform parent)
        {
            var visual = new GameObject("Intact");
            visual.transform.SetParent(parent, false);

            var filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = EnsureMeshAsset(
                BottleMesh.BuildIntact(BodyHeight, ShoulderHeight, NeckHeight, RimHeight, BodyRadius, NeckRadius),
                "Assets/CityX/Weapons/IntactBottleMesh.asset");

            Material glass = CharacterSkin.Ensure("BottleGlass", new Color(0.34f, 0.22f, 0.08f, 0.55f));
            glass.SetFloat("_Smoothness", 0.85f);
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glass;
            return visual;
        }

        private static GameObject BuildBrokenVisual(Transform parent)
        {
            var broken = new GameObject("Broken");
            broken.transform.SetParent(parent, false);

            var filter = broken.AddComponent<MeshFilter>();
            filter.sharedMesh = EnsureMeshAsset(
                BottleMesh.BuildBroken(IntactHeight), "Assets/CityX/Weapons/BrokenBottleMesh.asset");

            Material dullGlass = CharacterSkin.Ensure("BottleGlassBroken", new Color(0.22f, 0.19f, 0.16f, 0.85f));
            dullGlass.SetFloat("_Smoothness", 0.35f);
            var renderer = broken.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = dullGlass;
            return broken;
        }

        /// <summary>
        /// A Mesh built with `new Mesh()` lives only in memory -- assigning
        /// it to a MeshFilter and saving the PREFAB does not persist it, so
        /// the reference resolves to nothing the next time the project
        /// loads. Saved as its own small asset instead, matching how every
        /// other generated mesh in this project (FusedBody's own included)
        /// is made to survive a reload.
        /// </summary>
        private static Mesh EnsureMeshAsset(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject BuildThrownBottle()
        {
            var root = new GameObject("ThrownBottle");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * BottleWeapon.BottleScale;
                var filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = EnsureMeshAsset(
                    BottleMesh.BuildIntact(BodyHeight, ShoulderHeight, NeckHeight, RimHeight, BodyRadius, NeckRadius),
                    "Assets/CityX/Weapons/ThrownBottleMesh.asset");
                Material glass = CharacterSkin.Ensure("BottleGlass", new Color(0.34f, 0.22f, 0.08f, 0.55f));
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = glass;

                var body = root.AddComponent<Rigidbody>();
                body.mass = BottleWeapon.BottleMass;
                body.useGravity = true;

                var collider = root.AddComponent<SphereCollider>();
                collider.radius = BodyRadius * BottleWeapon.BottleScale * 1.2f;

                var thrown = root.AddComponent<ThrownBottle>();
                thrown.ShatterPrefab = GlassShatterBuilder.Build();
                thrown.ImpactCharacterSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/CityX/Weapons/Audio/glass_hit_enemy.mp3");
                thrown.ImpactTerrainSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/CityX/Weapons/Audio/glass_hit_terrain.mp3");

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ThrownPrefabPath);
                Debug.Log($"CityX: thrown bottle written to {ThrownPrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
