using System;
using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// Where the game keeps what it writes (characters, settings, the player key, a local server's data): Unity's per-user data folder,
    /// unless the environment variable <see cref="EnvironmentVariable"/> names another. A dedicated server can be pointed at its own disk
    /// this way; the tests point a run at a scratch folder (an environment variable outlasts the code reload Unity does on entering play
    /// mode, which a static field does not).
    /// </summary>
    public static class DataFolder
    {
        public const string EnvironmentVariable = "CITYX_DATA_FOLDER";

        public static string Path
        {
            get
            {
                string chosen = Environment.GetEnvironmentVariable(EnvironmentVariable);
                return string.IsNullOrEmpty(chosen) ? Application.persistentDataPath : chosen;
            }
        }
    }
}
