using System;
using System.IO;
using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// What the pause menu's settings panel remembers between sessions.
    /// Ported from ui/settings_panel.gd's own ConfigFile -- this project
    /// already has a save format for something a player would be upset to
    /// lose (see <see cref="CharacterStore"/>), so settings reuse the exact
    /// same shape rather than reaching for a different mechanism.
    /// </summary>
    [Serializable]
    public sealed class GameSettingsData
    {
        public float MasterVolume = 1.0f;
        public bool Fullscreen = true;

        /// <summary>
        /// An index into CityX.UI.DisplaySettings.Options. Kept as a bare int
        /// here rather than referencing that list directly -- Core sits below
        /// UI in the assembly graph, and a settings file has to be readable
        /// even before that list exists to check it against.
        /// </summary>
        public int ResolutionIndex = 3; // must match CityX.UI.DisplaySettings.DefaultIndex
    }

    /// <summary>
    /// Where saved settings live, and the only thing that reads or writes
    /// them. Same atomic-write-then-replace shape as <see cref="CharacterStore"/>,
    /// for the same reason: a crash mid-save should cost at most the latest
    /// change, never leave a truncated file behind.
    /// </summary>
    public static class GameSettings
    {
        public const string FileName = "settings.json";

        /// <summary>Overridable so tests write to a scratch directory instead of the real one.</summary>
        public static string DirectoryOverride;

        public static string Directory =>
            string.IsNullOrEmpty(DirectoryOverride)
                ? Application.persistentDataPath
                : DirectoryOverride;

        public static string Path_ => System.IO.Path.Combine(Directory, FileName);

        /// <summary>Never throws and never returns null. A missing or corrupt file is just the defaults.</summary>
        public static GameSettingsData Load()
        {
            string path = Path_;
            if (!File.Exists(path))
            {
                return new GameSettingsData();
            }

            try
            {
                GameSettingsData data = JsonUtility.FromJson<GameSettingsData>(File.ReadAllText(path));
                return data ?? new GameSettingsData();
            }
            catch (Exception error)
            {
                Debug.LogError($"CityX: could not read {path} -- {error.Message}. Using defaults; " +
                               "the file has been left where it is.");
                return new GameSettingsData();
            }
        }

        /// <summary>Writes the settings. Returns whether it got to disk.</summary>
        public static bool Save(GameSettingsData data)
        {
            if (data == null)
            {
                return false;
            }

            string directory = Directory;
            string path = Path_;
            string temporary = path + ".tmp";

            try
            {
                System.IO.Directory.CreateDirectory(directory);
                File.WriteAllText(temporary, JsonUtility.ToJson(data, prettyPrint: true));

                // Replace rather than Move: Move refuses to clobber on
                // Windows, so the first save would work and every one after
                // it would silently fail with the temporary file left behind.
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporary, path);
                }
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"CityX: could not write {path} -- {error.Message}");
                return false;
            }
        }
    }
}
