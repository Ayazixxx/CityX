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

        /// <summary>The owner's recordings (2026-11-19: "replace the shotgun blast noise with shotgunblast.mp3 and replace the shotgun cocking sound with the other one").</summary>
        public const string ShotSoundPath = "Assets/CityX/Weapons/Audio/shotgun_blast.mp3";

        public const string RackSoundPath = "Assets/CityX/Weapons/Audio/shotgun_pump.mp3";

        /// <summary>One shell going in (owner, the same day: "here is the shell insertion noise"): its click 0.38 s in, where the reload seats the shell.</summary>
        public const string ShellSoundPath = "Assets/CityX/Weapons/Audio/shotgun_shell.mp3";

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
            FirearmProfile profile = FirearmProfile.Shotgun();
            profile.RackSound = AssetDatabase.LoadAssetAtPath<AudioClip>(RackSoundPath);
            if (profile.RackSound == null)
            {
                Debug.LogWarning($"CityX: no pump recording at {RackSoundPath} -- the shotgun falls back to the synthesised rack.");
            }
            profile.ShellSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ShellSoundPath);
            if (profile.ShellSound == null)
            {
                Debug.LogWarning($"CityX: no shell recording at {ShellSoundPath} -- the shotgun falls back to the synthesised click.");
            }
            return FirearmBuilder.Build(ModelPath, PrefabPath, MeshFolder, "Shotgun", "Shotgun", profile, Finishes, ShotSoundPath);
        }
    }
}
