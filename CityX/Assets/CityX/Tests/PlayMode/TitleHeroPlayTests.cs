using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The title's rooftop (TitleHero; owner, 2026-11-11: "your most recently played character standing on top of a building in idle stance,
    /// holding their primary weapon equipped"): the right character, dressed, the weapon in their hands, standing on the roof; and a body
    /// that is only looked at, so it counts no play and saves nothing, even with that character still chosen from a game just left.
    /// </summary>
    public sealed class TitleHeroPlayTests
    {
        private string _directory;
        private Scene _title;
        private CharacterSave _latest;

        [UnitySetUp]
        public IEnumerator OpenTheTitleWithTwoCharacters()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXPlayTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            CharacterStore.DirectoryOverride = _directory;
            PlaySession.Clear();

            CharacterSave older = CharacterSave.Create("Older", Color.white);
            older.PrimaryWeapon = ItemRegistry.BaseballBat;
            older.LastPlayedUnixSeconds = 1000;
            _latest = CharacterSave.Create("Latest", new Color(0.55f, 0.4f, 0.32f));
            _latest.PrimaryWeapon = ItemRegistry.CombatSword;
            _latest.Wearing = new List<string> { ItemRegistry.RedCamoTShirt };
            _latest.LastPlayedUnixSeconds = 2000;
            LocalServer.Database.For(PlayerKey.Local).Add(older);
            LocalServer.Database.For(PlayerKey.Local).Add(_latest);

            yield return CharacterCreatorPlayTests.LoadTitle();
            _title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
        }

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            if (_title.IsValid() && _title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_title);
            }
            PlaySession.Clear();
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float waited = 0.0f; waited < seconds; waited += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheLastPlayedCharacterStandsOnTheRoofDressedWithTheirWeaponInHand()
        {
            var hero = Object.FindFirstObjectByType<TitleHero>();
            Assert.IsNotNull(hero, "the title has no rooftop character");
            yield return Seconds(1.0f);

            Assert.IsNotNull(hero.Showing, "nobody stands on the roof");
            Assert.AreEqual("Latest", hero.Showing.Name, "not the character played last");
            Assert.IsTrue(hero.Body.activeInHierarchy);
            var inventory = hero.Body.GetComponent<Inventory>();
            Assert.AreEqual(ItemRegistry.CombatSword, inventory.EquippedItemId(Inventory.PrimarySlot), "their primary weapon is not in their hands");
            Assert.AreEqual(ItemRegistry.RedCamoTShirt, inventory.EquippedItemId(EquipSlots.Torso), "their clothes are not on");

            // On the real roof the title chose (TitleStage), not fallen through it.
            TitleViewpoint.Pick roof = Object.FindFirstObjectByType<TitleStage>().Chosen;
            Assert.IsTrue(roof.Found, "the title found no roof");
            Assert.AreEqual(roof.Feet.y, hero.Body.transform.position.y, 0.4f, "they are not standing on the roof");
        }

        [UnityTest]
        public IEnumerator ItCountsNoPlayAndSavesNothingWithTheCharacterStillChosen()
        {
            // As after Quit To Title: the character still chosen.
            PlaySession.Begin(_latest, isNew: false);
            double played = _latest.PlaytimeSeconds;
            yield return Seconds(2.0f);

            var hero = Object.FindFirstObjectByType<TitleHero>();
            Assert.IsNotNull(hero.Showing, "setup: nobody stands on the roof");
            Assert.IsNull(CharacterIdentity.Ours, "the rooftop body took itself for the character being played");
            Assert.AreEqual(played, _latest.PlaytimeSeconds, 1e-6, "looking at the title counted as playing");
            Assert.AreEqual(2000, LocalServer.Characters.List(PlayerKey.Local).Find(c => c.Name == "Latest").LastPlayedUnixSeconds,
                "the title saved the character");
        }

        [UnityTest]
        public IEnumerator TheCharacterScreensTakeItAwayAndTheMenuBringsItBack()
        {
            var title = Object.FindFirstObjectByType<TitleScreen>();
            var hero = Object.FindFirstObjectByType<TitleHero>();
            title.Show(TitleScreen.Panel.Menu);
            yield return null;
            title.Show(TitleScreen.Panel.CharacterSelect);
            yield return null;
            Assert.IsFalse(hero.gameObject.activeInHierarchy, "the rooftop stays up behind the character list");
            title.Show(TitleScreen.Panel.Menu);
            yield return null;
            Assert.IsTrue(hero.gameObject.activeInHierarchy, "the rooftop did not come back");
        }
    }
}
