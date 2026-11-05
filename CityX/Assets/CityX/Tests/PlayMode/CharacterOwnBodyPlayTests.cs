using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Only this machine's own body is the character chosen on it. In a session every player's body is on every machine, and each one used
    /// to make itself that character: it wore their saved clothes, added its own seconds to their playtime (so two bodies played two
    /// seconds a second, and the server refused the save as impossible from then on), and wrote its own stale copy over theirs.
    /// A redraw changes the look and nothing else; leaving for the title saves first.
    /// </summary>
    public sealed class CharacterOwnBodyPlayTests
    {
        private string _directory;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXPlayTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            CharacterStore.DirectoryOverride = _directory;
            PlaySession.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            PlaySession.Clear();
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static CharacterSave Begin()
        {
            CharacterSave save = CharacterSave.Create("Ayaz", Color.white);
            save.Wearing = new List<string> { ItemRegistry.TShirt };
            LocalServer.Database.For(PlayerKey.Local).Add(save);
            PlaySession.Begin(save, isNew: false);
            return save;
        }

        /// <summary>A player's body with a bag that knows the T-shirt; <paramref name="anothers"/> is some other player's body, as a session marks it before it wakes.</summary>
        private GameObject Body(bool anothers)
        {
#if UNITY_EDITOR
            var holder = new GameObject("Holder");
            holder.SetActive(false);   // so the body is told whose it is before it wakes, as NetworkedPlayerIdentity tells it in its own Awake
            _made.Add(holder);
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), holder.transform);
            body.GetComponent<PlayerInputReader>().enabled = !anothers;
            var inventory = body.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.TShirt, Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/TShirt.prefab") },
            };
            if (anothers)
            {
                body.GetComponent<CharacterIdentity>().StandBy();
            }
            body.transform.SetParent(null, true);
            _made.Add(body);
            return body;
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersBodyIsNotTheCharacterWearsNoneOfTheirClothesCountsNoPlayAndSavesNothing()
        {
            CharacterSave save = Begin();
            GameObject ours = Body(anothers: false);
            GameObject theirs = Body(anothers: true);
            theirs.transform.position = new Vector3(3.0f, 0.0f, 0.0f);
            float played = 0.0f;
            for (int i = 0; i < 400 && played < 2.0f; i++)
            {
                yield return null;
                played += Time.deltaTime;
            }

            Assert.AreEqual(ItemRegistry.TShirt, ours.GetComponent<Inventory>().EquippedItemId(EquipSlots.Torso), "setup: this machine's own body did not put its clothes on");
            Assert.IsEmpty(theirs.GetComponent<Inventory>().EquippedItemId(EquipSlots.Torso), "another player's body was dressed in this machine's character's clothes");
            Assert.AreEqual(played, save.PlaytimeSeconds, 0.25f, "play was counted once for each body here, not once");
            Assert.AreSame(ours.GetComponent<CharacterIdentity>(), CharacterIdentity.Ours);
            Assert.IsNull(theirs.GetComponent<CharacterIdentity>().Character, "another player's body is this machine's character");
            Assert.IsFalse(theirs.GetComponent<CharacterIdentity>().Write(), "another player's body wrote over this machine's character");
        }

        [UnityTest]
        public IEnumerator ABodyHeldBackBecomesTheCharacterOnceItIsKnownToBeOurs()
        {
            CharacterSave save = Begin();
            save.Level = 4;
            GameObject body = Body(anothers: true);
            yield return null;
            yield return null;
            var identity = body.GetComponent<CharacterIdentity>();
            Assert.IsNull(CharacterIdentity.Ours, "a body nobody has claimed is this machine's character");
            Assert.AreNotEqual(4, body.GetComponent<PlayerVitals>().Level, "a body held back was given the character's level");

            identity.BeOurs();   // what the session does once it says the body is this machine's (NetworkedPlayerIdentity)
            yield return null;
            Assert.AreSame(identity, CharacterIdentity.Ours);
            Assert.AreEqual(4, body.GetComponent<PlayerVitals>().Level, "our own body did not become the character");
            Assert.AreEqual(ItemRegistry.TShirt, body.GetComponent<Inventory>().EquippedItemId(EquipSlots.Torso), "our own body did not put the character's clothes on");
            double before = save.PlaytimeSeconds;
            yield return null;
            Assert.Greater(save.PlaytimeSeconds, before, "our own body counts no play");
        }
    }
}
