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
            StringAssert.StartsWith(ScratchDataFixture.Folder, CharacterDatabase.LocalServerRoot, "this PC's server keeps its data outside the scratch folder");
            CharacterSave played = CharacterSave.Create("Scratch Guard", Color.white);
            LocalServer.Database.For(PlayerKey.Local).Add(played);
            var body = new GameObject("Body", typeof(PlayerVitals), typeof(CharacterIdentity));
            body.GetComponent<CharacterIdentity>().Apply(played);
            yield return new WaitForSeconds(0.2f);
            Object.Destroy(body);
            yield return null;
            Assert.Greater(LocalServer.Characters.Play(PlayerKey.Local, played.Id).PlaytimeSeconds, 0.0,
                "the played character was not saved into the scratch folder's server");
        }
    }
}
