using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Rebuilds every character asset, in the order they depend on each other.
    ///
    /// THE ORDER IS NOT INCIDENTAL. The civilian reuses the player's animator
    /// controller, so the player has to exist first; and both build their body
    /// from <see cref="CityX.Character.BodyRig"/>, so a change to the body's
    /// proportions has to reach both or the crowd and the player end up at
    /// different scales in the same street.
    ///
    /// Exists as one entry point mainly so it can be run headlessly:
    ///
    ///     Unity.exe -batchmode -quit -projectPath CityX \
    ///         -executeMethod CityX.CharacterEditor.CharacterRebuild.RebuildAll
    ///
    /// which is the only way to rebuild these without opening the editor, and
    /// the editor holds an exclusive lock on Library/ so the two cannot both
    /// happen.
    /// </summary>
    public static class CharacterRebuild
    {
        [MenuItem("CityX/Rebuild Everything Character")]
        public static void RebuildAll()
        {
            bool ok = true;

            if (PlayerRigBuilder.Build() == null)
            {
                Debug.LogError("CityX: the player rig failed to build.");
                ok = false;
            }
            else if (CivilianBuilder.Build() == null)
            {
                Debug.LogError("CityX: the civilian failed to build.");
                ok = false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (Application.isBatchMode)
            {
                // Named loudly and exited non-zero, because a batch run that
                // logs an error and exits 0 reads as a success in every script
                // that calls it.
                Debug.Log(ok
                    ? "CityX: character rebuild finished."
                    : "CityX: character rebuild FAILED.");
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
