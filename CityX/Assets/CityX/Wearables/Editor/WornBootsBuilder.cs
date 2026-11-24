using CityX.Items;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Worn Boots (docs/WORN_BOOTS.md; owner, 2026-11-23: "can you make a shoe model ingame ... looks good with the jeans. use the sneakers
    /// we built as a base for sizing but these can be a bit bulkier", "just make a boot a homeless person would wear"): a second pair of shoes, worn
    /// as the sneakers are (each rigid on its foot, the feet and the covered ankles not drawn). The model and its paint are made by
    /// SourceArt/WornBoots/worn_boots_build.py already standing on the body's feet at their worn size, so nothing is grown or moved here.
    /// </summary>
    public static class WornBootsBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/WornBoots.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/WornBoots.glb";
        public const string LeftMeshPath = "Assets/CityX/Wearables/WornBootsLeftMesh.asset";
        public const string RightMeshPath = "Assets/CityX/Wearables/WornBootsRightMesh.asset";

        /// <summary>Old suede: no shine at all.</summary>
        private const float Smoothness = 0.08f;

        [MenuItem("CityX/Rebuild Worn Boots")]
        public static GameObject Build() =>
            WhiteSneakerBuilder.SavePair("WornBoots", PrefabPath, ItemRegistry.WornBoots, SourcePath, LeftMeshPath, RightMeshPath, "WornBoots",
                point => point, Smoothness);
    }
}
