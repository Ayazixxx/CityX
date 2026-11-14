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
        public float MusicVolume = 1.0f;
        public bool Fullscreen = true;

        /// <summary>
        /// An index into CityX.UI.DisplaySettings.Options. Kept as a bare int
        /// here rather than referencing that list directly -- Core sits below
        /// UI in the assembly graph, and a settings file has to be readable
        /// even before that list exists to check it against.
        /// </summary>
        public int ResolutionIndex = 3; // must match CityX.UI.DisplaySettings.DefaultIndex

        /// <summary>
        /// The frame-rate cap, in frames per second; 0 means no cap. Saved as the NUMBER, not an index into
        /// CityX.UI.DisplaySettings.FpsOptions: a cap of 90 still means 90 if the list of choices changes, and
        /// a value the list no longer offers snaps to the nearest one it does. A settings file from before
        /// this existed has no such field and loads as 0 (uncapped), which is what the game always did.
        /// </summary>
        public int MaxFps = 0;

        /// <summary>
        /// Device NAMES rather than indices, unlike ResolutionIndex -- a
        /// plugged-in microphone can vanish between sessions and an index
        /// would silently point at whatever happens to be at that slot
        /// next time; "Default" is always index 0 in
        /// CityX.UI.VoiceDeviceOptions and is exactly what a saved name
        /// that no longer exists falls back to.
        /// </summary>
        public string InputDevice = GameSettings.DefaultDevice;

        /// <summary>
        /// Carried for parity with ui/settings_panel.gd's own Output Device
        /// picker, but Unity has no API to enumerate or select a specific
        /// audio OUTPUT device (only input/recording devices, via
        /// Microphone.devices) -- see VoiceDeviceOptions.OutputOptions's own
        /// doc comment. This is saved and shown, never anything else.
        /// </summary>
        public string OutputDevice = GameSettings.DefaultDevice;

        /// <summary>Matches settings_panel.gd's own default -- push_to_talk starts true.</summary>
        public bool PushToTalk = true;

        /// <summary>The picture's quality: the graphics page of the settings (CityX.View.GraphicsQuality puts it into force).</summary>
        public GraphicsOptions Graphics = new GraphicsOptions();
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

        /// <summary>
        /// What a saved InputDevice/OutputDevice name of "the OS's own
        /// default" looks like. Lives here, in Core, rather than on
        /// CityX.UI.VoiceDeviceOptions where the rest of the device-list
        /// logic lives -- CityX.Character.PlayerVoiceCapture needs this
        /// same constant and Character cannot reference UI (only the
        /// reverse), the identical reason GameSettingsData.ResolutionIndex
        /// is a bare int rather than a CityX.UI.DisplaySettings.Resolution.
        /// </summary>
        public const string DefaultDevice = "Default";

        /// <summary>Overridable so tests write to a scratch directory instead of the real one.</summary>
        public static string DirectoryOverride;

        public static string Directory =>
            string.IsNullOrEmpty(DirectoryOverride)
                ? DataFolder.Path
                : DirectoryOverride;

        public static string Path_ => System.IO.Path.Combine(Directory, FileName);

        /// <summary>
        /// Never throws and never returns null. A corrupt file is just the defaults; a missing one is what this machine starts on
        /// (<see cref="FirstLaunch"/>).
        /// </summary>
        public static GameSettingsData Load()
        {
            string path = Path_;
            if (!File.Exists(path))
            {
                return FirstLaunch();
            }

            try
            {
                GameSettingsData data = JsonUtility.FromJson<GameSettingsData>(File.ReadAllText(path)) ?? new GameSettingsData();
                data.Graphics = (data.Graphics ?? new GraphicsOptions()).Clean();
                return data;
            }
            catch (Exception error)
            {
                Debug.LogError($"CityX: could not read {path} -- {error.Message}. Using defaults; " +
                               "the file has been left where it is.");
                return new GameSettingsData();
            }
        }

        /// <summary>
        /// The settings of a machine whose player has saved none yet: the defaults, with the picture chosen for its graphics card
        /// (<see cref="HardwareDefaults"/>). Tests (a scratch directory) and the builders (batch mode) get the defaults as the game is
        /// made, whatever machine they run on.
        /// </summary>
        public static GameSettingsData FirstLaunch()
        {
            var data = new GameSettingsData();
            if (Application.isBatchMode || !string.IsNullOrEmpty(DirectoryOverride))
            {
                return data;
            }
            Resolution screen = Screen.currentResolution;
            data.Graphics = HardwareDefaults.For(SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize, screen.width, screen.height);
            return data;
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
