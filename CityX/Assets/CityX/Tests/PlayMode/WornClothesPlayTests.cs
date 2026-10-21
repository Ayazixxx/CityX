using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Clothes kept on a character (owner, 2026-10-20: "make it so items you equipped stay equipped when you close the game and relaunch and
    /// select that character"): the real player puts on what its save says it wears, and what it puts on or takes off is saved at once.
    /// </summary>
    public sealed class WornClothesPlayTests
    {
        private string _directory;
        private GameObject _player;

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
            if (_player != null)
            {
                Object.Destroy(_player);
            }
            PlaySession.Clear();
            CharacterStore.DirectoryOverride = null;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator TheSavedClothesArePutOnAndTakingOneOffIsSaved()
        {
#if UNITY_EDITOR
            CharacterSave save = CharacterSave.Create("Ayaz", Color.white);
            save.Wearing = new List<string> { ItemRegistry.TShirt, ItemRegistry.TopHat, ItemRegistry.Pistol };
            CharacterStore.Add(save);
            PlaySession.Begin(save, isNew: false);

            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"));
            Assert.IsNotNull(_player.GetComponent<CharacterIdentity>(), "Player.prefab has no CharacterIdentity");
            // The scene's player gets its inventory from CityMassingSceneBuilder, before the first frame.
            var inventory = _player.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                Entry(ItemRegistry.TShirt, "Assets/CityX/Wearables/TShirt.prefab"),
                Entry(ItemRegistry.TopHat, "Assets/CityX/Wearables/TopHat.prefab"),
                Entry(ItemRegistry.Pistol, "Assets/CityX/Weapons/Pistol.prefab"),
            };
            yield return null;
            yield return null;

            Assert.AreEqual(ItemRegistry.TShirt, inventory.EquippedItemId(EquipSlots.Torso), "the saved T-shirt was not put back on");
            Assert.AreEqual(ItemRegistry.TopHat, inventory.EquippedItemId(EquipSlots.Helmet), "the saved hat was not put back on");
            Assert.IsTrue(_player.GetComponent<SkinnedGarmentCombiner>().IsShowing(ItemRegistry.TShirt), "the T-shirt is on but not shown");
            Assert.IsEmpty(inventory.EquippedItemId(EquipSlots.Primary), "a weapon in a save's clothes was put on");

            Assert.IsTrue(inventory.UnequipToSlot(EquipSlots.Helmet, _player));
            CollectionAssert.AreEqual(new[] { ItemRegistry.TShirt }, CharacterStore.Load()[0].Wearing, "taking the hat off was not saved");
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static Inventory.ItemPrefabEntry Entry(string itemId, string path) =>
            new Inventory.ItemPrefabEntry { ItemId = itemId, Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) };
#endif
    }
}
