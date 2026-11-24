using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the full PHIZZ bottle (docs/PHIZZ_CAN.md): the soda bottle's own glass, label and gold cap (BottleShape, BottleMesh: the same as the weapon's),
    /// standing on its base at the origin as a drink is held, its glass showing the drink in it below a level (_CITYX_LIQUID, FullBottle); and the cap as it
    /// falls when pulled off.
    /// </summary>
    public static class FullBottleBuilder
    {
        public const string Folder = PhizzCanBuilder.Folder;
        public const string PrefabPath = Folder + "/phizz_bottle.prefab";
        public const string CapPrefabPath = Folder + "/phizz_bottle_cap.prefab";

        /// <summary>
        /// The cap coming off: the owner's bottle-opening sound with its silence before and after cut off (owner, 2026-11-23). The can keeps its own
        /// (PhizzCanBuilder.OpenSoundPath).
        /// </summary>
        public const string OpenSoundPath = "Assets/CityX/Weapons/Audio/soda_bottle_open.ogg";

        private const int Sides = 20;
        private const float RingEvery = 0.012f;
        private const int PrintColumns = 18;
        private const int PrintRows = 12;
        private const float PrintProud = 0.0004f;
        private const float GlassSmoothness = 0.88f;
        private const float CapMetallic = 0.5f;
        private const float CapSmoothness = 0.6f;
        private const float CapMass = 0.02f;

        [MenuItem("CityX/Rebuild PHIZZ Bottle")]
        public static void BuildFromMenu() => Build();

        public static GameObject Build()
        {
            BottleShape shape = BottleShape.For(GlassBottleKinds.Soda);
            Material glass = HeldModelBaker.LitMaterial($"{Folder}/phizz_bottle_glass.mat", shape.Glass, 0.0f, GlassSmoothness);
            glass.EnableKeyword("_CITYX_LIQUID");
            EditorUtility.SetDirty(glass);
            Material print = BottleBuilder.PrintMaterial($"{Folder}/phizz_bottle_print.mat", shape.DecalPath);
            Material cap = HeldModelBaker.LitMaterial($"{Folder}/phizz_bottle_cap.mat", shape.CapColour, CapMetallic, CapSmoothness);
            BottleMesh.Model capModel = BottleMesh.Turned(shape.Cap, Sides, RingEvery);

            GameObject fallingCap = SaveCap(shape, capModel, cap);
            GameObject thrown = SaveThrown(shape, print, cap, capModel);
            var root = new GameObject("phizz_bottle");
            try
            {
                Renderer glassRenderer = Part(root.transform, shape, "Glass", BottleMesh.Turned(shape.Profile, Sides, RingEvery), glass).GetComponent<Renderer>();
                Part(root.transform, shape, "Print", BottleMesh.Joined(
                    BottleMesh.Printed(shape.Profile, shape.DecalFrom, shape.DecalTo, 90.0f, shape.DecalArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery * 0.5f),
                    BottleMesh.Printed(shape.Profile, shape.DecalFrom, shape.DecalTo, 270.0f, shape.DecalArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery * 0.5f)), print);
                Transform capPart = Part(root.transform, shape, "Cap", capModel, cap);

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = shape.BodyRadius * shape.Scale;
                capsule.height = shape.Height * shape.Scale;
                capsule.center = new Vector3(0.0f, capsule.height * 0.5f, 0.0f);
                root.AddComponent<Rigidbody>().mass = FullBottle.BottleMass;
                var pickup = root.AddComponent<SphereCollider>();
                pickup.isTrigger = true;
                pickup.radius = HeldDrink.InteractRange;

                var bottle = root.AddComponent<FullBottle>();
                bottle.Glass = new[] { glassRenderer };
                bottle.Cap = capPart.gameObject;
                bottle.CapPrefab = fallingCap;
                bottle.ThrownPrefab = thrown;
                bottle.ShatterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottleBuilder.ShatterPrefabPath(GlassBottleKinds.Soda));
                bottle.OpenSound = AssetDatabase.LoadAssetAtPath<AudioClip>(OpenSoundPath);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: full PHIZZ bottle written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The bottle thrown full: its glass the drink's yellow all through (no level: it is full), shut; where it lands it bursts into the soda bottle's own pieces
        /// (made about its neck: ShatterAt turns this standing frame into the held one they are in) and splashes yellow.
        /// </summary>
        private static GameObject SaveThrown(BottleShape shape, Material print, Material cap, BottleMesh.Model capModel)
        {
            Material full = HeldModelBaker.LitMaterial($"{Folder}/phizz_bottle_glass_full.mat", FullBottle.LiquidColour, 0.0f, GlassSmoothness);
            var root = new GameObject("phizz_bottle_thrown");
            try
            {
                Part(root.transform, shape, "Glass", BottleMesh.Turned(shape.Profile, Sides, RingEvery), full);
                Part(root.transform, shape, "Print", BottleMesh.Joined(
                    BottleMesh.Printed(shape.Profile, shape.DecalFrom, shape.DecalTo, 90.0f, shape.DecalArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery * 0.5f),
                    BottleMesh.Printed(shape.Profile, shape.DecalFrom, shape.DecalTo, 270.0f, shape.DecalArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery * 0.5f)), print);
                Part(root.transform, shape, "Cap", capModel, cap);
                var pieces = new GameObject("ShatterAt").transform;
                pieces.SetParent(root.transform, false);
                pieces.localPosition = new Vector3(0.0f, shape.GripHeight * shape.Scale, 0.0f);
                pieces.localRotation = Quaternion.Euler(180.0f, 0.0f, 0.0f);
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = shape.BodyRadius * shape.Scale;
                capsule.height = shape.Height * shape.Scale;
                capsule.center = new Vector3(0.0f, capsule.height * 0.5f, 0.0f);
                root.AddComponent<Rigidbody>().mass = FullBottle.BottleMass;
                var flying = root.AddComponent<ThrownBottle>();
                flying.ShatterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottleBuilder.ShatterPrefabPath(GlassBottleKinds.Soda));
                flying.ShatterAt = pieces;
                flying.Splashes = true;
                flying.SplashColour = FullBottle.LiquidColour;
                flying.Stains = true;
                flying.ImpactCharacterSound = AssetDatabase.LoadAssetAtPath<AudioClip>(BottleBuilder.ImpactCharacterSoundPath);
                flying.ImpactTerrainSound = AssetDatabase.LoadAssetAtPath<AudioClip>(BottleBuilder.ImpactTerrainSoundPath);
                return PrefabUtility.SaveAsPrefabAsset(root, FullBottle.ThrownPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The cap pulled off: a small body that falls, lies about and goes.</summary>
        private static GameObject SaveCap(BottleShape shape, BottleMesh.Model capModel, Material material)
        {
            var root = new GameObject("phizz_bottle_cap");
            try
            {
                Part(root.transform, shape, "Cap", capModel, material);
                var collider = root.AddComponent<SphereCollider>();
                collider.radius = shape.NeckRadius * 1.3f * shape.Scale;
                collider.center = new Vector3(0.0f, shape.Height * shape.Scale, 0.0f);
                root.AddComponent<Rigidbody>().mass = CapMass;
                root.AddComponent<DroppedCan>();
                return PrefabUtility.SaveAsPrefabAsset(root, CapPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A part of the bottle at the soda bottle's size, saved as its own mesh.</summary>
        private static Transform Part(Transform parent, BottleShape shape, string name, BottleMesh.Model model, Material material)
        {
            var vertices = new Vector3[model.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = model.Vertices[i] * shape.Scale;
            }
            string asset = $"phizz_bottle_{name.ToLowerInvariant()}";
            Mesh mesh = HeldModelBaker.Save($"{Folder}/{asset}.asset", asset, vertices, model.Normals.ToArray(), model.Triangles.ToArray(),
                model.HasUvs ? model.Uvs.ToArray() : null);
            return HeldModelBaker.Part(parent, name, mesh, material);
        }
    }
}
