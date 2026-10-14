using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the AK-47 prefab (owner, 2026-10-14: "add an ak47 with reload animation too ... remake it yourself so we dont have to credit
    /// them"). The model is made from scratch in Blender to the rifle's real proportions (CityX/SourceArt/AK47/ak47_model.py): the body with
    /// its wooden stock, grip and handguards, the curved magazine that rocks out and in about its front lug, the bolt's charging handle on the
    /// right that runs back on every shot and is pulled in an empty reload, and the trigger. The free reference file the owner sent was only
    /// looked at (its shape and the order of its reload), never used. See FirearmBuilder for how a model becomes a prefab and FirearmProfile.AK47
    /// for how it fires.
    /// </summary>
    public static class AK47Builder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/AK47.prefab";
        private const string ModelPath = "Assets/CityX/Weapons/AK47.glb";
        private const string MeshFolder = "Assets/CityX/Weapons/AK47Meshes";

        /// <summary>The Blender materials' looks: blued steel, darker small parts, the red-brown laminated wood, the steel magazine.</summary>
        public static readonly Dictionary<string, FirearmBuilder.Finish> Finishes = new Dictionary<string, FirearmBuilder.Finish>
        {
            ["AKSteel"] = new FirearmBuilder.Finish(new Color(0.10f, 0.10f, 0.11f), 0.8f, 0.5f),
            ["AKDarkSteel"] = new FirearmBuilder.Finish(new Color(0.06f, 0.06f, 0.065f), 0.7f, 0.4f),
            ["AKWood"] = new FirearmBuilder.Finish(new Color(0.42f, 0.2f, 0.08f), 0.0f, 0.45f),
            ["AKMagazine"] = new FirearmBuilder.Finish(new Color(0.12f, 0.12f, 0.13f), 0.75f, 0.45f),
        };

        [MenuItem("CityX/Rebuild AK-47")]
        public static GameObject Build()
        {
            return FirearmBuilder.Build(ModelPath, PrefabPath, MeshFolder, "AK47", "AK47", FirearmProfile.AK47(), Finishes);
        }
    }
}
