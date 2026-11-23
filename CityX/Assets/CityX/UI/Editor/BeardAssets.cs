using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.UIEditor
{
    /// <summary>
    /// The beards' meshes (docs/BEARDS.md): each model SourceArt/Beards/beard_build.py writes into Character/Beards (Beard_&lt;name&gt;.glb) baked
    /// into a plain mesh in Resources (Character/Resources/Beards/&lt;name&gt;.asset), its node's own transform applied (HairAssets.Bake), which
    /// CharacterHair loads by name. Rebaked whole every time: a beard is a few hundred to a few thousand triangles.
    /// </summary>
    public static class BeardAssets
    {
        public const string SourceFolder = "Assets/CityX/Character/Beards";
        public const string BakedFolder = "Assets/CityX/Character/Resources/Beards";

        public static string SourcePath(string beard) => SourceFolder + "/Beard_" + beard + ".glb";

        public static string BakedPath(string beard) => BakedFolder + "/" + beard + ".asset";

        [MenuItem("CityX/Bake Beards")]
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder(BakedFolder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Character/Resources", "Beards");
            }
            foreach (string beard in CharacterHair.ValidBeards)
            {
                if (beard == CharacterHair.None)
                {
                    continue;
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath(beard));
                MeshFilter filter = model != null ? model.GetComponentInChildren<MeshFilter>(true) : null;
                if (filter == null || filter.sharedMesh == null)
                {
                    Debug.LogWarning($"CityX: {SourcePath(beard)} is missing or has no mesh -- run SourceArt/Beards/beard_build.py --export.");
                    continue;
                }
                Mesh baked = HairAssets.Bake(filter.sharedMesh, filter.transform.localToWorldMatrix);
                baked.name = "Beard_" + beard;
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(BakedPath(beard));
                if (existing != null)
                {
                    EditorUtility.CopySerialized(baked, existing);
                    Object.DestroyImmediate(baked);
                }
                else
                {
                    AssetDatabase.CreateAsset(baked, BakedPath(beard));
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
