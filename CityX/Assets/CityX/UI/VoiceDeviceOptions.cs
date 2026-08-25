using System.Collections.Generic;
using CityX.Core;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The device lists the settings panel's Input/Output Device steppers
    /// step through, ported from ui/settings_panel.gd's own device
    /// OptionButtons -- see that file's own AudioServer.get_output_device_list()/
    /// get_input_device_list() calls.
    ///
    /// RUNTIME-ENUMERATED, UNLIKE DisplaySettings' OWN FIXED Options ARRAY --
    /// a resolution list is the same on every machine; which microphones are
    /// plugged in is not, so this asks the OS fresh every time rather than
    /// keeping a cached array like DisplaySettings does.
    ///
    /// "DEFAULT" IS ALWAYS INDEX 0 IN BOTH LISTS, matching settings_panel.gd's
    /// own doc comment on why: it lets a saved device name that is no longer
    /// plugged in fall back cleanly, the same reasoning IndexOf's own doc
    /// comment repeats.
    /// </summary>
    public static class VoiceDeviceOptions
    {
        /// <summary>
        /// Alias for GameSettings.DefaultDevice -- that constant lives in
        /// Core rather than here because CityX.Character.PlayerVoiceCapture
        /// needs it too and Character cannot reference UI. Kept here as
        /// well so every UI call site that already reads
        /// VoiceDeviceOptions.DefaultDevice keeps working unchanged.
        /// </summary>
        public const string DefaultDevice = GameSettings.DefaultDevice;

        /// <summary>"Default" plus whatever Microphone.devices reports right now.</summary>
        public static string[] InputOptions()
        {
            var options = new List<string> { DefaultDevice };
            options.AddRange(Microphone.devices);
            return options.ToArray();
        }

        /// <summary>
        /// ALWAYS JUST "Default" -- not a stub waiting to be filled in.
        /// Godot's AudioServer.get_output_device_list() has no Unity
        /// equivalent: Unity plays audio through whatever the OS's current
        /// default output device is and exposes no API to enumerate or
        /// redirect to a different one. Kept as a genuine (if permanently
        /// single-item) list, the same shape InputOptions returns, so the
        /// settings views can drive both pickers through identical stepper
        /// code rather than special-casing Output as unavailable.
        /// </summary>
        public static string[] OutputOptions()
        {
            return new[] { DefaultDevice };
        }

        /// <summary>
        /// A saved or typed device name's index in <paramref name="options"/>,
        /// falling back to 0 ("Default") if that name is not (or no longer)
        /// present -- the same "a device that vanished between sessions
        /// should not throw" reasoning DisplaySettings.Clamp applies to a
        /// shrunken resolution list.
        /// </summary>
        public static int IndexOf(string[] options, string name)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == name)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
