using System.Collections;
using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>A body played in a test saves into the run's scratch folder, never into the player's own characters (ScratchDataFixture).</summary>
    public sealed class ScratchDataPlayTests
    {
        [UnityTest]
        public IEnumerator ACharacterPlayedInATestSavesIntoTheScratchFolder()
        {
            Assert.IsNotNull(ScratchDataFixture.Folder, "the scratch folder was never set up");
            StringAssert.StartsWith(ScratchDataFixture.Folder, CharacterStore.Directory, "saving does not point at the scratch folder");
            var body = new GameObject("Body", typeof(PlayerVitals), typeof(CharacterIdentity));
            CharacterSave played = CharacterSave.Create("Scratch Guard", Color.white);
            body.GetComponent<CharacterIdentity>().Apply(played);
            yield return null;
            Object.Destroy(body);
            yield return null;
            Assert.IsTrue(new CharacterFile(ScratchDataFixture.Folder).Load().Exists(c => c.Id == played.Id), "the played character was not saved into the scratch folder");
        }
    }
}
