using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the PHIZZ can (docs/PHIZZ_CAN.md): <see cref="CanMesh"/>'s outline turned and coloured as the owner asked (a golden body, the
    /// top and bottom bare silver "like soda cans usually are"), the PHIZZ label printed on it front and back, a pull tab on the lid. A model
    /// only, standing on its base at the origin, the bottles' size: drinking it is Fable's to build.
    /// </summary>
    public static class PhizzCanBuilder
    {
        public const string Folder = "Assets/CityX/Weapons/Cans";
        public const string PrefabPath = Folder + "/phizz_can.prefab";
        public const string EmptyPrefabPath = Folder + "/phizz_can_empty.prefab";

        /// <summary>The tab cracking the lid (the owner's sound).</summary>
        public const string OpenSoundPath = "Assets/CityX/Weapons/Audio/soda_can_open.mp3";

        /// <summary>The can's own PHIZZ label: the owner's splash with no margin and their BANANA SODA drawn smaller inside it, so that printed half way round the splash is about 1.2 times the bottle's and the wording the size it is on the bottle (owner: "make the phizz logo 1.1x bigger", "leave the banana soda the same size", "make the logo 1.1x bigger"): SourceArt/Phizz/phizz_label.ps1 -WordingScale 0.834 -Margin 0.</summary>
        public const string LabelPath = Folder + "/PhizzCanLabel.png";

        private const int Sides = 24;

        /// <summary>A ring at least this often (metres) up the curved foot and neck; the body is straight and needs none between its ends.</summary>
        private const float RingEvery = 0.004f;
        private const float StraightRingEvery = 0.2f;

        /// <summary>The label, printed once ahead and once behind, each half way round (the splash's drips meet at the sides), as tall as it is wide round the can: its heights and its arc (owner: "make the phizz part a bit bigger", then the splash 1.1 times, twice; its clear top and bottom run onto the neck and foot, printed nothing there).</summary>
        private const float PrintFrom = 0.00665f;
        private const float PrintTo = 0.11035f;
        private const float PrintArcDegrees = 180.0f;
        private const float PrintProud = 0.0003f;
        private const int PrintColumns = 18;
        private const int PrintRows = 12;

        /// <summary>A yellow gold, lighter and less metal than the bottle caps' (owner: "make the can a tad yellow its a bit too dark").</summary>
        private static readonly Color Gold = new Color(0.95f, 0.79f, 0.22f);
        private const float GoldMetallic = 0.3f;
        private static readonly Color Silver = new Color(0.78f, 0.79f, 0.81f);
        private const float Metallic = 0.5f;
        private const float GoldSmoothness = 0.7f;
        private const float SilverSmoothness = 0.75f;

        /// <summary>The tab a duller, darker metal than the lid: the same silver, flat on the lid, it could not be told from it.</summary>
        private static readonly Color TabSilver = new Color(0.56f, 0.57f, 0.60f);
        private const float TabSmoothness = 0.5f;

        [MenuItem("CityX/Rebuild PHIZZ Can")]
        public static void BuildFromMenu() => Build();

        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Cans");
            }
            Material gold = HeldModelBaker.LitMaterial($"{Folder}/phizz_can_body.mat", Gold, GoldMetallic, GoldSmoothness);
            Material silver = HeldModelBaker.LitMaterial($"{Folder}/phizz_can_ends.mat", Silver, Metallic, SilverSmoothness);
            Material tab = HeldModelBaker.LitMaterial($"{Folder}/phizz_can_tab.mat", TabSilver, Metallic, TabSmoothness);
            Material print = BottleBuilder.PrintMaterial($"{Folder}/phizz_can_print.mat", LabelPath);

            Material hole = HeldModelBaker.LitMaterial($"{Folder}/phizz_can_hole.mat", Color.black, 0.0f, 0.0f);
            var look = new Look { Gold = gold, Silver = silver, Tab = tab, Print = print, Hole = hole };

            // The empty can first: the full one drops it.
            GameObject empty = SaveCan("phizz_can_empty", EmptyPrefabPath, look, root =>
            {
                root.transform.Find("TabHinge").localRotation = Quaternion.AngleAxis(PhizzCan.TabOpenDegrees, Vector3.forward);
                root.transform.Find("Hole").gameObject.SetActive(true);
                root.AddComponent<DroppedCan>();
            });

            GameObject saved = SaveCan("phizz_can", PrefabPath, look, root =>
            {
                var pickup = root.AddComponent<SphereCollider>();
                pickup.isTrigger = true;
                pickup.radius = PhizzCan.InteractRange;
                var can = root.AddComponent<PhizzCan>();
                can.TabHinge = root.transform.Find("TabHinge");
                can.Hole = root.transform.Find("Hole").gameObject;
                can.EmptyPrefab = empty;
                can.OpenSound = AssetDatabase.LoadAssetAtPath<AudioClip>(OpenSoundPath);
            });
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: PHIZZ can written to {PrefabPath}");
            return saved;
        }

        private sealed class Look
        {
            public Material Gold, Silver, Tab, Print, Hole;
        }

        /// <summary>The can's model (the tab on a hinge at its rivet, the hole hidden), a body and a capsule, finished by <paramref name="finish"/> and saved.</summary>
        private static GameObject SaveCan(string name, string path, Look look, System.Action<GameObject> finish)
        {
            var root = new GameObject(name);
            try
            {
                Part(root.transform, "Body", BottleMesh.Turned(CanMesh.Body, Sides, StraightRingEvery), look.Gold);
                Part(root.transform, "Foot", BottleMesh.Turned(CanMesh.Foot, Sides, RingEvery), look.Silver);
                Part(root.transform, "Top", BottleMesh.Turned(CanMesh.Top, Sides, RingEvery), look.Silver);
                Part(root.transform, "Rivet", BottleMesh.Turned(CanMesh.Rivet, 12, RingEvery * 0.5f), look.Silver);
                Part(root.transform, "Opening", CanMesh.Opening(), look.Silver);
                Part(root.transform, "Hole", CanMesh.Hole(), look.Hole).gameObject.SetActive(false);
                Part(root.transform, "Print", BottleMesh.Joined(
                    BottleMesh.Printed(CanMesh.Outline, PrintFrom, PrintTo, 90.0f, PrintArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery),
                    BottleMesh.Printed(CanMesh.Outline, PrintFrom, PrintTo, 270.0f, PrintArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery)), look.Print);
                // The tab turns about its rivet (along the can's Z) to lift its free end.
                var hinge = new GameObject("TabHinge");
                hinge.transform.SetParent(root.transform, false);
                hinge.transform.localPosition = new Vector3(0.0f, CanMesh.LidHeight * BottleShape.LifeScale, 0.0f);
                Part(hinge.transform, "Tab", CanMesh.Tab(), look.Tab).localPosition = -hinge.transform.localPosition;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = CanMesh.Radius * BottleShape.LifeScale;
                capsule.height = CanMesh.Height * BottleShape.LifeScale;
                capsule.center = new Vector3(0.0f, capsule.height * 0.5f, 0.0f);
                root.AddComponent<Rigidbody>().mass = PhizzCan.CanMass;
                finish(root);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A part of the can at the game's size, saved as its own mesh.</summary>
        private static Transform Part(Transform parent, string name, BottleMesh.Model model, Material material)
        {
            var vertices = new Vector3[model.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = model.Vertices[i] * BottleShape.LifeScale;
            }
            Mesh mesh = HeldModelBaker.Save($"{Folder}/phizz_can_{name.ToLowerInvariant()}.asset", $"phizz_can_{name.ToLowerInvariant()}", vertices,
                model.Normals.ToArray(), model.Triangles.ToArray(), model.HasUvs ? model.Uvs.ToArray() : null);
            return HeldModelBaker.Part(parent, name, mesh, material);
        }
    }
}
