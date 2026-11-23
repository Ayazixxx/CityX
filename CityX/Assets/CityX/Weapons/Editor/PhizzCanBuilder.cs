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

        /// <summary>The can's own PHIZZ label: the bottle's with BANANA SODA drawn smaller inside it, so printed 1.1 times bigger the splash grows and the wording stays its size (owner: "leave the banana soda the same size and make the phizz logo 1.1x bigger"): SourceArt/Phizz/phizz_label.ps1 -WordingScale 0.909.</summary>
        public const string LabelPath = Folder + "/PhizzCanLabel.png";

        private const int Sides = 24;

        /// <summary>A ring at least this often (metres) up the curved foot and neck; the body is straight and needs none between its ends.</summary>
        private const float RingEvery = 0.004f;
        private const float StraightRingEvery = 0.2f;

        /// <summary>The label, printed once ahead and once behind: its heights on the body and how far round it goes (owner: "make the phizz part a bit bigger", then the PHIZZ splash 1.1 times again; its clear margins run onto the neck and foot, printed nothing there).</summary>
        private const float PrintFrom = 0.0085f;
        private const float PrintTo = 0.1086f;
        private const float PrintArcDegrees = 175.0f;
        private const float PrintProud = 0.0003f;
        private const int PrintColumns = 18;
        private const int PrintRows = 12;

        private static readonly Color Gold = new Color(0.86f, 0.66f, 0.16f);
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
            Material gold = HeldModelBaker.LitMaterial($"{Folder}/phizz_can_body.mat", Gold, Metallic, GoldSmoothness);
            Material silver = HeldModelBaker.LitMaterial($"{Folder}/phizz_can_ends.mat", Silver, Metallic, SilverSmoothness);
            Material tab = HeldModelBaker.LitMaterial($"{Folder}/phizz_can_tab.mat", TabSilver, Metallic, TabSmoothness);
            Material print = BottleBuilder.PrintMaterial($"{Folder}/phizz_can_print.mat", LabelPath);

            var root = new GameObject("phizz_can");
            try
            {
                Part(root.transform, "Body", BottleMesh.Turned(CanMesh.Body, Sides, StraightRingEvery), gold);
                Part(root.transform, "Foot", BottleMesh.Turned(CanMesh.Foot, Sides, RingEvery), silver);
                Part(root.transform, "Top", BottleMesh.Turned(CanMesh.Top, Sides, RingEvery), silver);
                Part(root.transform, "Rivet", BottleMesh.Turned(CanMesh.Rivet, 12, RingEvery * 0.5f), silver);
                Part(root.transform, "Tab", CanMesh.Tab(), tab);
                Part(root.transform, "Opening", CanMesh.Opening(), silver);
                Part(root.transform, "Print", BottleMesh.Joined(
                    BottleMesh.Printed(CanMesh.Outline, PrintFrom, PrintTo, 90.0f, PrintArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery),
                    BottleMesh.Printed(CanMesh.Outline, PrintFrom, PrintTo, 270.0f, PrintArcDegrees, PrintProud, PrintColumns, PrintRows, RingEvery)), print);

                var body = root.AddComponent<CapsuleCollider>();
                body.direction = 1;
                body.radius = CanMesh.Radius * BottleShape.LifeScale;
                body.height = CanMesh.Height * BottleShape.LifeScale;
                body.center = new Vector3(0.0f, body.height * 0.5f, 0.0f);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: PHIZZ can written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A part of the can at the game's size, saved as its own mesh.</summary>
        private static void Part(Transform parent, string name, BottleMesh.Model model, Material material)
        {
            var vertices = new Vector3[model.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = model.Vertices[i] * BottleShape.LifeScale;
            }
            Mesh mesh = HeldModelBaker.Save($"{Folder}/phizz_can_{name.ToLowerInvariant()}.asset", $"phizz_can_{name.ToLowerInvariant()}", vertices,
                model.Normals.ToArray(), model.Triangles.ToArray(), model.HasUvs ? model.Uvs.ToArray() : null);
            HeldModelBaker.Part(parent, name, mesh, material);
        }
    }
}
