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

        /// <summary>
        /// The reload's own recordings (owner, 2026-10-14: "replace the ak47 reload sound with this. speed it up so its 2.8 seconds when magazine
        /// is empty and 2.17 seconds with rounds left, but try to maintain the pitch"): one recording, sped up to each reload's exact length
        /// without changing its pitch by CityX/SourceArt/AK47/stretch_reload.py (WSOLA, in Blender's Python).
        /// </summary>
        public const string ReloadSoundPath = "Assets/CityX/Weapons/Audio/ak47_reload_tactical.ogg";

        public const string EmptyReloadSoundPath = "Assets/CityX/Weapons/Audio/ak47_reload_empty.ogg";

        /// <summary>
        /// The Blender materials' looks: blued steel, painted more than polished (a very metallic surface shows almost none of its own colour
        /// under the cel light: nearly black, the whole rifle was one dark shape and its parts did not read against each other), darker small
        /// parts and the barrel, the bolt carrier's bare steel, the red-brown wood with a darker cut for the grip's chequering and the
        /// handguard's groove, the steel magazine, a round's brass.
        /// </summary>
        public static readonly Dictionary<string, FirearmBuilder.Finish> Finishes = new Dictionary<string, FirearmBuilder.Finish>
        {
            ["AKSteel"] = new FirearmBuilder.Finish(new Color(0.24f, 0.255f, 0.29f), 0.35f, 0.45f),
            ["AKDarkSteel"] = new FirearmBuilder.Finish(new Color(0.07f, 0.07f, 0.08f), 0.5f, 0.4f),
            ["AKBrightSteel"] = new FirearmBuilder.Finish(new Color(0.60f, 0.61f, 0.63f), 0.6f, 0.6f),
            ["AKDarkWood"] = new FirearmBuilder.Finish(new Color(0.27f, 0.12f, 0.05f), 0.0f, 0.35f),
            ["AKWood"] = new FirearmBuilder.Finish(new Color(0.47f, 0.22f, 0.08f), 0.0f, 0.45f),
            ["AKMagazine"] = new FirearmBuilder.Finish(new Color(0.31f, 0.32f, 0.35f), 0.35f, 0.45f),
            ["AKBrass"] = new FirearmBuilder.Finish(new Color(0.78f, 0.6f, 0.22f), 0.7f, 0.6f),
        };

        [MenuItem("CityX/Rebuild AK-47")]
        public static GameObject Build()
        {
            FirearmProfile profile = FirearmProfile.AK47();
            profile.ReloadSound = LoadClip(ReloadSoundPath);
            profile.EmptyReloadSound = LoadClip(EmptyReloadSoundPath);
            return FirearmBuilder.Build(ModelPath, PrefabPath, MeshFolder, "AK47", "AK47", profile, Finishes);
        }

        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"CityX: no reload recording at {path} -- the AK-47 falls back to the synthesised reload clicks.");
            }
            return clip;
        }
    }
}
