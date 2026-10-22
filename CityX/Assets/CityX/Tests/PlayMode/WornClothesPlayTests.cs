using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
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
            LocalServer.Database.For(PlayerKey.Local).Add(save);
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
            CollectionAssert.AreEqual(new[] { ItemRegistry.TShirt }, LocalServer.Characters.List(PlayerKey.Local)[0].Wearing, "taking the hat off was not saved");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-10-22: in multiplayer the Blood Raven tee wore the face paint, and after a redraw the Midnight Coat turned skin-tan:
        /// the look (paint, skin colour) was put on every renderer under the body, clothes included, whenever it arrived after them (the
        /// look sent over the network; saving a redraw). Now only the skin and the skull take it.
        /// </summary>
        [UnityTest]
        public IEnumerator PaintAndSkinColourPutOnAfterTheClothesLeaveTheClothesAlone()
        {
#if UNITY_EDITOR
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"));
            var inventory = _player.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                Entry(ItemRegistry.BloodRavenTShirt, "Assets/CityX/Wearables/BloodRavenTShirt.prefab"),
                Entry(ItemRegistry.TopHat, "Assets/CityX/Wearables/TopHat.prefab"),
            };
            yield return null;
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.BloodRavenTShirt, _player));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.TopHat, _player));
            yield return null;
            Renderer shirt = _player.GetComponent<SkinnedGarmentCombiner>().GetComponentsInChildren<Renderer>(true)
                .First(r => r.sharedMaterials.Length > 0 && System.Array.Exists(r.sharedMaterials, m => m != null && m.name.Contains("BloodRaven")));
            Material[] shirtBefore = shirt.sharedMaterials;
            Renderer hat = inventory.EquippedInstance(EquipSlots.Helmet).GetComponentInChildren<Renderer>();
            Material hatBefore = hat.sharedMaterial;

            var identity = _player.GetComponent<CharacterIdentity>();
            var paint = new Texture2D(4, 4);
            identity.ApplyPaint(paint);
            identity.Recolour(new Color(0.8f, 0.6f, 0.4f));

            CollectionAssert.AreEqual(shirtBefore, shirt.sharedMaterials, "the paint or skin colour went onto the shirt");
            Assert.AreSame(hatBefore, hat.sharedMaterial, "the paint or skin colour went onto the hat");
            Renderer skin = BodySurface.Renderers(_player.transform).First(r => r.name == BodySurface.SkinName);
            Assert.AreSame(paint, skin.sharedMaterial.GetTexture("_BaseMap"), "the body itself did not take the paint");
            Object.Destroy(paint);
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
