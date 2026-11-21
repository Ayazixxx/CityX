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
            // The Iron Spiked one's test stands the latest with it (the rooftop stands its character once, as the title opens).
            string test = TestContext.CurrentContext.Test.MethodName;
            bool knife = test == nameof(AKnifeInTheIdleIsTurnedOutOffTheThigh);
            _latest.PrimaryWeapon = test == nameof(ACharacterWithTheIronSpikedGiantBoomerangStandsWithItShowingOnTheirBack)
                ? ItemRegistry.IronSpikedGiantBoomerang : knife ? ItemRegistry.KitchenKnife : ItemRegistry.CombatSword;
            _latest.Wearing = knife
                ? new List<string> { ItemRegistry.RedStripedTShirt, ItemRegistry.GreenFurHoodedParka }
                : new List<string> { ItemRegistry.RedCamoTShirt };
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

        /// <summary>The idle at half speed and the feet on one spot through it (owner, 2026-11-11: "his feet glide to the left and right over the floor").</summary>
        [UnityTest]
        public IEnumerator ItIdlesAtHalfSpeedWithItsFeetStill()
        {
            var hero = Object.FindFirstObjectByType<TitleHero>();
            yield return Seconds(1.0f);
            Assert.IsNotNull(hero.Feet, "the feet were never set down");
            Animator animator = hero.Body.GetComponentInChildren<Animator>();
            Assert.AreEqual(TitleHero.IdleSpeed, animator.speed, 1e-4f, "the idle is not at half speed");
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 leftAt = left.position, rightAt = right.position;
            float drift = 0.0f;
            for (float waited = 0.0f; waited < 3.0f; waited += Time.deltaTime)
            {
                yield return null;
                drift = Mathf.Max(drift, Vector3.Distance(leftAt, left.position), Vector3.Distance(rightAt, right.position));
            }
            Assert.Less(drift, 0.02f, "the feet glide over the roof through the idle");
        }

        /// <summary>
        /// Seen only once it has settled, and at one height from then on (owner, 2026-11-11: "make the playermodel at the same height the entire
        /// time i feel like i see it go up and down at the beginning").
        /// </summary>
        [UnityTest]
        public IEnumerator ItIsSeenOnlyOnceSettledAndAtOneHeightFromThen()
        {
            var hero = Object.FindFirstObjectByType<TitleHero>();
            for (int i = 0; i < 300 && (hero.Body == null || !hero.Body.activeInHierarchy); i++)
            {
                yield return null;
            }
            Renderer[] parts = hero.Body.GetComponentsInChildren<Renderer>();
            float hidden = 0.0f;
            while (!hero.Seen)
            {
                hidden += Time.deltaTime;
                Assert.Less(hidden, 0.6f, "the character is kept out of sight for more than half a second (owner, 2026-11-11: \"the character doesnt appear for the first 2 seconds or so\")");
                Assert.IsFalse(System.Array.Exists(parts, part => part.enabled), "seen before it has settled");
                yield return null;
            }
            Assert.IsTrue(System.Array.Exists(parts, part => part.enabled), "never seen");
            Transform hips = hero.Body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            float lowest = hips.position.y, highest = lowest;
            for (float waited = 0.0f; waited < 2.0f; waited += Time.deltaTime)
            {
                yield return null;
                lowest = Mathf.Min(lowest, hips.position.y);
                highest = Mathf.Max(highest, hips.position.y);
            }
            Assert.Less(highest - lowest, 0.01f, "it bobs up and down once seen");
        }

        /// <summary>
        /// Owner, 2026-11-20: "make the iron spiked boomerang visible on back in the title screen when it was equipped primary". The title's scene is
        /// built (TitleRooftopBuilder) with the items known then, and was last built before it existed: its body had no model for it. A character whose
        /// primary is the Iron Spiked one stands with it on their back, drawn; and the body has a model for every item another player's body has, so
        /// the next new one shows too.
        /// </summary>
        [UnityTest]
        public IEnumerator ACharacterWithTheIronSpikedGiantBoomerangStandsWithItShowingOnTheirBack()
        {
            var hero = Object.FindFirstObjectByType<TitleHero>();
            Assert.IsNotNull(hero, "the title has no rooftop character");
            var inventory = hero.Body.GetComponent<Inventory>();
#if UNITY_EDITOR
            var everyBody = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponent<Inventory>();
            var missing = new List<string>();
            foreach (Inventory.ItemPrefabEntry entry in everyBody.ItemPrefabs)
            {
                if (entry.Prefab != null && inventory.PrefabForItem(entry.ItemId) == null)
                {
                    missing.Add(entry.ItemId);
                }
            }
            Assert.IsEmpty(missing, "the title's body cannot show these -- rebuild the title (TitleScreenBuilder.Build)");
#endif
            yield return Seconds(1.5f);
            Assert.AreEqual(ItemRegistry.IronSpikedGiantBoomerang, inventory.EquippedItemId(Inventory.PrimarySlot), "their primary weapon is not equipped");
            var boomerang = hero.Body.GetComponentInChildren<CityX.Weapons.GiantBoomerangItem>();
            Assert.IsNotNull(boomerang, "no giant boomerang on the body");
            Assert.AreEqual(CityX.Weapons.GiantBoomerangModel.IronSpiked, boomerang.Model);
            Animator animator = hero.Body.GetComponentInChildren<Animator>();
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.Spine), boomerang.transform.parent, "it is not on their back");
            Assert.IsTrue(hero.Seen, "the body is still kept unseen");
            bool drawn = false;
            foreach (Renderer part in boomerang.GetComponentsInChildren<Renderer>())
            {
                drawn |= part.enabled && part.gameObject.activeInHierarchy;
            }
            Assert.IsTrue(drawn, "it is on their back but not drawn");
        }

        /// <summary>
        /// The knife held out to the side through the idle, not down and in across the thigh (owner, 2026-11-21: "the knife clips through the coat
        /// a bit during the idle animation on the title screen just turn his hand so the knife is going the other way").
        /// </summary>
        [UnityTest]
        public IEnumerator AKnifeInTheIdleIsTurnedOutOffTheThigh()
        {
            var hero = Object.FindFirstObjectByType<TitleHero>();
            yield return Seconds(1.5f);
            Assert.AreEqual(ItemRegistry.KitchenKnife, hero.Body.GetComponent<Inventory>().EquippedItemId(Inventory.PrimarySlot), "setup: no knife in hand");
            var knife = hero.Body.GetComponentInChildren<CityX.Weapons.SwordWeapon>();
            Transform body = hero.Body.GetComponentInChildren<Animator>().transform;
            float least = float.PositiveInfinity;
            for (float waited = 0.0f; waited < 4.0f; waited += Time.deltaTime)
            {
                yield return null;   // before this frame's Animator: the bones still stand as last frame's LateUpdate left them
                least = Mathf.Min(least, Vector3.Dot(knife.transform.up, body.right));
            }
            Debug.Log($"[KnifeCarry] the blade's least lean out to the side through the idle: {least:0.00}");
            Assert.Greater(least, 0.4f, "the knife's blade is not turned out to the side in the idle");
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
