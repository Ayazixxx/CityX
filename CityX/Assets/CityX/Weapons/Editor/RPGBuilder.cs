using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the RPG's prefab (owner, 2026-11-19: "make a basic rpg rocket launcher that is animated"; docs/RPG.md). The model is made from
    /// scratch in Blender after the RPG-7 (CityX/SourceArt/RPG/rpg_model.py): the body (tube, blast cone, wooden heat guards, grip, sights,
    /// optic), the loaded round (it flies when fired: FlyingRocket copies it), the hammer and the trigger. See FirearmBuilder for how a model
    /// becomes a prefab and FirearmProfile.RPG for how it fires. Its shot is the shotgun's recording, deep (the booster's thump).
    /// </summary>
    public static class RPGBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/RPG.prefab";
        private const string ModelPath = "Assets/CityX/Weapons/RPG.glb";
        private const string MeshFolder = "Assets/CityX/Weapons/RPGMeshes";

        /// <summary>The Blender materials' looks: worn grey steel, dark small parts, oiled wood, the optic's glass, the olive warhead and its motor.</summary>
        public static readonly Dictionary<string, FirearmBuilder.Finish> Finishes = new Dictionary<string, FirearmBuilder.Finish>
        {
            ["RPGSteel"] = new FirearmBuilder.Finish(new Color(0.11f, 0.115f, 0.1f), 0.7f, 0.45f),
            ["RPGDark"] = new FirearmBuilder.Finish(new Color(0.03f, 0.03f, 0.032f), 0.5f, 0.35f),
            ["RPGWood"] = new FirearmBuilder.Finish(new Color(0.33f, 0.17f, 0.07f), 0.0f, 0.35f),
            ["RPGGlass"] = new FirearmBuilder.Finish(new Color(0.05f, 0.08f, 0.1f), 0.9f, 0.9f),
            ["RPGWarhead"] = new FirearmBuilder.Finish(new Color(0.22f, 0.27f, 0.16f), 0.2f, 0.45f),
            ["RPGMotor"] = new FirearmBuilder.Finish(new Color(0.2f, 0.2f, 0.2f), 0.6f, 0.5f),
            ["RPGBrass"] = new FirearmBuilder.Finish(new Color(0.75f, 0.58f, 0.22f), 0.9f, 0.6f),
        };

        [MenuItem("CityX/Rebuild RPG")]
        public static GameObject Build()
        {
            return FirearmBuilder.Build(ModelPath, PrefabPath, MeshFolder, "RPG", "RPG", FirearmProfile.RPG(), Finishes, ShotgunBuilder.ShotSoundPath);
        }
    }
}
