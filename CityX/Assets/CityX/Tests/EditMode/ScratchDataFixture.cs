using System;
using System.IO;
using CityX.Core;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Everything a test writes (characters, settings, the player key) goes to a scratch folder of the run's own, never the player's: tests
    /// that played a character (the redraw creator, the HUD portrait) were saving it into the real characters.json on every run, about 2,500
    /// of them by 2026-10-22. Set through <see cref="DataFolder.EnvironmentVariable"/>, which outlasts the code reload Unity does on entering
    /// play mode. In the tests' own namespace: a set-up fixture outside every namespace never ran for the play tests. A test that points
    /// saving at a folder of its own puts it back to <see cref="Folder"/>.
    /// </summary>
    [SetUpFixture]
    public sealed class ScratchDataFixture
    {
        public static string Folder => Environment.GetEnvironmentVariable(DataFolder.EnvironmentVariable);

        [OneTimeSetUp]
        public void UseScratchFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "CityXTests", "run-" + Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
            Environment.SetEnvironmentVariable(DataFolder.EnvironmentVariable, folder);
        }

        [OneTimeTearDown]
        public void RemoveScratchFolder()
        {
            string folder = Folder;
            Environment.SetEnvironmentVariable(DataFolder.EnvironmentVariable, null);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
