using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the heat press (docs/HEAT_PRESS.md; owner, 2026-11-24): its work table, the base, the drawer and the lid on its pivot, each a mesh
    /// asset painted with <see cref="HeatPressShape.Colours"/>, colliders on what a body bumps into, and <see cref="HeatPress"/> to move them.
    /// Built with the scene.
    /// </summary>
    public static class HeatPressBuilder
    {
        public const string Folder = "Assets/CityX/Wearables/HeatPress";
        public const string PrefabPath = Folder + "/HeatPress.prefab";

        [MenuItem("CityX/Rebuild Heat Press")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Wearables", "HeatPress");
            }
            Material[] finishes = Finishes();
            var root = new GameObject("HeatPress");
            try
            {
                Part(root.transform, "Table", Save(HeatPressShape.Table()), finishes);
                var press = new GameObject("Press").transform;
                press.SetParent(root.transform, false);
                press.localPosition = HeatPressShape.OnTheTable;
                Part(press, "Base", Save(HeatPressShape.Base()), finishes);
                Transform drawer = Part(press, "Drawer", Save(HeatPressShape.Drawer()), finishes);
                var pivot = new GameObject("LidPivot").transform;
                pivot.SetParent(press, false);
                pivot.localPosition = HeatPressShape.Pivot;
                Transform lid = Part(pivot, "Lid", Save(HeatPressShape.Lid()), finishes);
                lid.localPosition = -HeatPressShape.Pivot;

                // What a body bumps into: the table, and the press standing on it.
                var table = root.AddComponent<BoxCollider>();
                table.center = new Vector3(0.0f, HeatPressShape.TableHeight * 0.5f, 0.0f);
                table.size = new Vector3(HeatPressShape.TableHalf.x * 2.0f, HeatPressShape.TableHeight, HeatPressShape.TableHalf.y * 2.0f);
                var body = press.gameObject.AddComponent<BoxCollider>();
                body.center = new Vector3(0.0f, 0.2f, -0.06f);
                body.size = new Vector3(0.62f, 0.4f, 0.84f);

                var heatPress = press.gameObject.AddComponent<HeatPress>();
                heatPress.LidPivot = pivot;
                heatPress.Drawer = drawer;
                press.gameObject.AddComponent<HeatPressUse>();   // "[E] Print a shirt" 

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material[] Finishes()
        {
            string[] names = System.Enum.GetNames(typeof(HeatPressShape.Finish));
            var materials = new Material[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                materials[i] = HeldModelBaker.LitMaterial($"{Folder}/HeatPress{names[i]}.mat", HeatPressShape.Colours[i], 0.0f, HeatPressShape.Smoothness[i]);
            }
            return materials;
        }

        private static Mesh Save(Mesh made)
        {
            string path = $"{Folder}/{made.name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(made, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(made, path);
            return made;
        }

        private static Transform Part(Transform parent, string name, Mesh mesh, Material[] finishes)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = finishes;
            return go.transform;
        }
    }
}
