using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the pump shotgun's prefab (owner, 2026-11-18: "make a shotgun with reload animations and parts to it that move like our ak47 and
    /// pistol"; docs/SHOTGUN.md). The model is made from scratch in Blender to a 12-gauge pump gun's real proportions
    /// (CityX/SourceArt/Shotgun/shotgun_model.py): the body, the pump (the forend with its action bars and the bolt in the port, slid back and
    /// home by the support hand after every shot), one shell (carried to the loading port by the reload) and the trigger. See FirearmBuilder for
    /// how a model becomes a prefab and FirearmProfile.Shotgun for how it fires.
    /// </summary>
    public static class ShotgunBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/Shotgun.prefab";
        private const string ModelPath = "Assets/CityX/Weapons/Shotgun.glb";
        private const string MeshFolder = "Assets/CityX/Weapons/ShotgunMeshes";

        /// <summary>The Blender materials' looks: parkerised steel, darker small parts, black polymer furniture, the shell's red hull and brass head.</summary>
        public static readonly Dictionary<string, FirearmBuilder.Finish> Finishes = new Dictionary<string, FirearmBuilder.Finish>
        {
            ["SGSteel"] = new FirearmBuilder.Finish(new Color(0.085f, 0.085f, 0.095f), 0.8f, 0.5f),
            ["SGDark"] = new FirearmBuilder.Finish(new Color(0.035f, 0.035f, 0.04f), 0.6f, 0.35f),
            ["SGPolymer"] = new FirearmBuilder.Finish(new Color(0.05f, 0.05f, 0.055f), 0.0f, 0.3f),
            ["SGShellHull"] = new FirearmBuilder.Finish(new Color(0.72f, 0.07f, 0.05f), 0.0f, 0.5f),
            ["SGShellBrass"] = new FirearmBuilder.Finish(new Color(0.78f, 0.58f, 0.24f), 0.9f, 0.6f),
        };

        [MenuItem("CityX/Rebuild Shotgun")]
        public static GameObject Build()
        {
            return FirearmBuilder.Build(ModelPath, PrefabPath, MeshFolder, "Shotgun", "Shotgun", FirearmProfile.Shotgun(), Finishes);
        }
    }
}
