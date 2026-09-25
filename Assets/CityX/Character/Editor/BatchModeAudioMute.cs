using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// A headless test/build/capture run must be SILENT. The PlayMode suite
    /// plays real gunshots, footsteps, glass and thunder through whatever audio
    /// device the machine has, and a full run is 15+ minutes of it -- somebody
    /// in the room heard it and (rightly) wanted it stopped.
    ///
    /// Batch mode only: an editor a person opens by hand is left exactly as
    /// they set it, never muted behind their back.
    /// </summary>
    [InitializeOnLoad]
    public static class BatchModeAudioMute
    {
        static BatchModeAudioMute()
        {
            if (Application.isBatchMode)
            {
                EditorUtility.audioMasterMute = true;
            }
        }
    }
}
