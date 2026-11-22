using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the ammunition on the ground (owner, 2026-11-21: "make a basic ammunition model for each type of gun that is an in game right now",
    /// "look at what ammunition models for those guns look like in other games as a guide"). Games give each calibre its own small box, told apart
    /// at a glance by colour and shape, and Resident Evil's handgun box shows its rounds; so: a cardboard box of brass rounds for the pistol, an
    /// olive ammo can for the rifle, a box of red shells for the shotgun, and the RPG's own rocket laid on the ground. Low-effort stand-ins drawn
    /// from simple shapes, at the game's 1.3x of real size, until the owner's models.
    /// </summary>
    public static class AmmoBuilder
    {
        public const string Folder = "Assets/CityX/Weapons/Ammo";
        public const string PistolAmmoPath = Folder + "/PistolAmmo.prefab";
        public const string RifleAmmoPath = Folder + "/RifleAmmo.prefab";
        public const string ShotgunShellsPath = Folder + "/ShotgunShells.prefab";
        public const string RpgRocketsPath = Folder + "/RpgRockets.prefab";

        /// <summary>Rounds in one box: a pistol's two magazines, an AK's two, a dozen shells, a pair of rockets.</summary>
        public const int PistolRounds = 30;
        public const int RifleRounds = 60;
        public const int ShotgunShellCount = 12;
        public const int RocketCount = 2;

        private const float Scale = 1.3f;

        private static readonly Color Cardboard = new Color(0.36f, 0.30f, 0.20f);
        private static readonly Color Olive = new Color(0.22f, 0.26f, 0.15f);
        private static readonly Color Brass = new Color(0.78f, 0.60f, 0.24f);
        private static readonly Color Copper = new Color(0.62f, 0.33f, 0.18f);
        private static readonly Color ShellRed = new Color(0.62f, 0.07f, 0.06f);
        private static readonly Color Dark = new Color(0.06f, 0.06f, 0.06f);

        // The colour marking each kind, as games colour-code their ammunition: pistol yellow, rifle orange, shotgun red.
        private static readonly Color PistolMark = new Color(0.95f, 0.78f, 0.12f);
        private static readonly Color RifleMark = new Color(0.95f, 0.45f, 0.08f);
        private static readonly Color ShotgunMark = new Color(0.85f, 0.12f, 0.10f);

        [MenuItem("CityX/Rebuild Ammunition")]
        public static void BuildAll()
        {
            BuildPistol();
            BuildRifle();
            BuildShotgun();
            BuildRockets();
        }

        /// <summary>A small cardboard box, a yellow band round it, its rounds standing in rows with their copper tips out of the open top.</summary>
        public static GameObject BuildPistol()
        {
            var root = Root("PistolAmmo");
            Vector3 box = new Vector3(0.12f, 0.05f, 0.08f) * Scale;
            Cube(root, "Box", Vector3.up * box.y * 0.5f, box, Mat("AmmoCardboard", Cardboard, 0.0f, 0.2f));
            Cube(root, "Band", Vector3.up * box.y * 0.5f, new Vector3(box.x * 1.01f, box.y * 0.35f, box.z * 1.01f), Mat("AmmoPistolMark", PistolMark, 0.0f, 0.3f));
            Material brass = Mat("AmmoBrass", Brass, 0.9f, 0.6f), copper = Mat("AmmoCopper", Copper, 0.8f, 0.55f);
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    var at = new Vector3((x - 2) * 0.021f * Scale, box.y, (z - 1) * 0.021f * Scale);
                    Cylinder(root, "Case", at + Vector3.up * 0.004f * Scale, 0.0095f * Scale, 0.008f * Scale, brass);
                    Sphere(root, "Tip", at + Vector3.up * 0.011f * Scale, 0.009f * Scale, copper);
                }
            }
            return Save(root, PistolAmmoPath, ItemRegistry.PistolAmmo, PistolRounds, "Pistol");
        }

        /// <summary>An olive ammo can: its lid's lip, a carrying handle on top and an orange band across its side.</summary>
        public static GameObject BuildRifle()
        {
            var root = Root("RifleAmmo");
            Vector3 can = new Vector3(0.28f, 0.18f, 0.10f) * Scale;
            Material olive = Mat("AmmoCanOlive", Olive, 0.4f, 0.45f), dark = Mat("AmmoDark", Dark, 0.5f, 0.35f);
            Cube(root, "Can", Vector3.up * can.y * 0.5f, can, olive);
            Cube(root, "Lid", Vector3.up * (can.y + 0.008f * Scale), new Vector3(can.x * 1.04f, 0.016f * Scale, can.z * 1.08f), olive);
            Cube(root, "Latch", new Vector3(can.x * 0.5f + 0.006f * Scale, can.y * 0.82f, 0.0f), new Vector3(0.012f, 0.05f, 0.05f) * Scale, dark);
            float top = can.y + 0.016f * Scale;
            Cube(root, "HandlePostL", new Vector3(-0.05f * Scale, top + 0.012f * Scale, 0.0f), new Vector3(0.01f, 0.024f, 0.012f) * Scale, dark);
            Cube(root, "HandlePostR", new Vector3(0.05f * Scale, top + 0.012f * Scale, 0.0f), new Vector3(0.01f, 0.024f, 0.012f) * Scale, dark);
            Cube(root, "Handle", new Vector3(0.0f, top + 0.026f * Scale, 0.0f), new Vector3(0.11f, 0.01f, 0.016f) * Scale, dark);
            Cube(root, "Band", new Vector3(0.0f, can.y * 0.45f, 0.0f), new Vector3(can.x * 1.01f, can.y * 0.16f, can.z * 1.02f), Mat("AmmoRifleMark", RifleMark, 0.0f, 0.3f));
            return Save(root, RifleAmmoPath, ItemRegistry.RifleAmmo, RifleRounds, "AK-47");
        }

        /// <summary>A box with a red band, two rows of red shells standing in its open top, and one lying loose beside it, its brass head showing.</summary>
        public static GameObject BuildShotgun()
        {
            var root = Root("ShotgunShells");
            Vector3 box = new Vector3(0.13f, 0.055f, 0.07f) * Scale;
            Cube(root, "Box", Vector3.up * box.y * 0.5f, box, Mat("AmmoCardboard", Cardboard, 0.0f, 0.2f));
            Cube(root, "Band", Vector3.up * box.y * 0.5f, new Vector3(box.x * 1.01f, box.y * 0.35f, box.z * 1.01f), Mat("AmmoShotgunMark", ShotgunMark, 0.0f, 0.3f));
            Material red = Mat("AmmoShellRed", ShellRed, 0.0f, 0.45f), brass = Mat("AmmoBrass", Brass, 0.9f, 0.6f);
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    var at = new Vector3((x - 2) * 0.025f * Scale, box.y, (z - 0.5f) * 0.03f * Scale);
                    Cylinder(root, "Shell", at + Vector3.up * 0.012f * Scale, 0.011f * Scale, 0.024f * Scale, red);
                }
            }
            // One loose shell on its side: the red hull and the brass head say "shell" from any side.
            var loose = new Vector3(0.0f, 0.011f * Scale, box.z * 0.5f + 0.03f * Scale);
            Transform hull = Cylinder(root, "LooseHull", loose, 0.011f * Scale, 0.05f * Scale, red);
            hull.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
            Transform head = Cylinder(root, "LooseHead", loose + Vector3.right * 0.03f * Scale, 0.0115f * Scale, 0.012f * Scale, brass);
            head.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
            return Save(root, ShotgunShellsPath, ItemRegistry.ShotgunShells, ShotgunShellCount, "Pump Shotgun");
        }

        /// <summary>Two of the RPG's own rockets (its "Rocket" part, warhead and motor), lying side by side.</summary>
        public static GameObject BuildRockets()
        {
            var root = Root("RpgRockets");
            var rpg = AssetDatabase.LoadAssetAtPath<GameObject>(RPGBuilder.PrefabPath);
            Transform rocket = rpg != null ? CityX.Core.TransformSearch.FindDeep(rpg.transform, "Rocket") : null;
            MeshFilter filter = rocket != null ? rocket.GetComponent<MeshFilter>() : null;
            MeshRenderer renderer = rocket != null ? rocket.GetComponent<MeshRenderer>() : null;
            if (filter == null || renderer == null)
            {
                Debug.LogError("CityX: the RPG prefab has no Rocket part to lay down as ammunition -- rebuild the RPG first.");
                Object.DestroyImmediate(root);
                return null;
            }
            Bounds bounds = filter.sharedMesh.bounds;
            // Laid flat along x: the rocket's longest side is its length.
            Vector3 size = bounds.size;
            Quaternion flat = size.y >= size.x && size.y >= size.z ? Quaternion.Euler(0.0f, 0.0f, 90.0f)
                : size.z >= size.x ? Quaternion.Euler(0.0f, 90.0f, 0.0f) : Quaternion.identity;
            float girth = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            for (int i = 0; i < RocketCount; i++)
            {
                var part = new GameObject("Rocket" + i).transform;
                part.SetParent(root.transform, false);
                part.localRotation = flat;
                part.localPosition = new Vector3(0.0f, girth * 0.5f, (i - 0.5f) * girth * 1.6f) - flat * bounds.center;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            return Save(root, RpgRocketsPath, ItemRegistry.RpgRockets, RocketCount, "RPG");
        }

        private static GameObject Root(string name)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Ammo");
            }
            return new GameObject(name);
        }

        private static GameObject Save(GameObject root, string path, string itemId, int rounds, string forGuns)
        {
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = AmmoBox.PickupRange;
            root.AddComponent<AmmoBox>().Fill(itemId, rounds, forGuns);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return saved;
        }

        private static Material Mat(string name, Color colour, float metallic, float smoothness) =>
            HeldModelBaker.LitMaterial($"{Folder}/{name}.mat", colour, metallic, smoothness);

        private static Transform Cube(GameObject root, string name, Vector3 at, Vector3 size, Material material) =>
            PrimitiveParts.Box(root.transform, name, at, Quaternion.identity, size, material);

        private static Transform Cylinder(GameObject root, string name, Vector3 at, float radius, float height, Material material) =>
            PrimitiveParts.Cylinder(root.transform, name, at, Quaternion.identity, radius, height, material);

        private static Transform Sphere(GameObject root, string name, Vector3 at, float radius, Material material) =>
            PrimitiveParts.Ball(root.transform, name, at, radius, material);
    }
}
