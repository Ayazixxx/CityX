using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Everything a player does in the world is asked of GameControls, never read straight off the keyboard or mouse (docs/CONTROLLER.md): a
    /// key read straight from the keyboard is a thing a controller cannot do. The character creator and the editor's fly camera are their own.
    /// </summary>
    public sealed class ControlsGuardTests
    {
        private static readonly string[] Folders = { "Character", "Vehicles", "View/Interactables", "Weapons" };

        private static readonly HashSet<string> OwnInput = new HashSet<string> { "GameControls.cs", "CharacterCreator.cs" };

        [Test]
        public void NothingThePlayerDoesInTheWorldReadsTheKeyboardStraight()
        {
            var offenders = new List<string>();
            foreach (string folder in Folders)
            {
                string root = Path.Combine(Application.dataPath, "CityX", folder);
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (OwnInput.Contains(Path.GetFileName(file)) || file.Replace('\\', '/').Contains("/Editor/"))
                    {
                        continue;
                    }
                    foreach (string line in File.ReadAllLines(file))
                    {
                        string code = line.Trim();
                        if (code.StartsWith("//"))
                        {
                            continue;
                        }
                        if (code.Contains("Keyboard.current") || code.Contains("Mouse.current") || code.Contains("Gamepad.current"))
                        {
                            offenders.Add($"{Path.GetFileName(file)}: {code}");
                        }
                    }
                }
            }
            Assert.IsEmpty(offenders, "these read a device straight instead of asking GameControls");
        }
    }
}
