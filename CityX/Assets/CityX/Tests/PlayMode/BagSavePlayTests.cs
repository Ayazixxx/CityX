using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE BAG ON A REAL BODY (docs/SERVER_SAVING.md, step 3): a character played with a saved bag gets it all back -- the storage, the clothes, a
    /// pair of pistols holstered with their rounds, the AK-47 that was in hand drawn -- and what it carries when it saves is what the server keeps.
    /// </summary>
    public sealed class BagSavePlayTests
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

        private static BagEntry Gun(string slot, string itemId, int magazine, int reserve) =>
            new BagEntry { Slot = slot, ItemId = itemId, HasAmmo = true, Magazine = magazine, Reserve = reserve };

        [UnityTest]
        public IEnumerator APlayedCharacterGetsItsWholeBagBackAndSavesWhatItCarries()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(40, 1, 40);
            _made.Add(ground);

            CharacterSave save = CharacterSave.Create("Packer", Color.white);
            save.LearnedSkills.Add(SkillCatalog.DualPistols);
            save.Wearing = new List<string> { ItemRegistry.TShirt };
            save.Bag = new List<BagEntry>
            {
                new BagEntry { Index = 4, ItemId = ItemRegistry.Grenade, Count = 2 },
                new BagEntry { Slot = EquipSlots.Torso, ItemId = ItemRegistry.TShirt },
                Gun(EquipSlots.Primary, ItemRegistry.Pistol, 7, 20),
                Gun(EquipSlots.PrimaryOffHand, ItemRegistry.Pistol, 3, 10),
                Gun(EquipSlots.Secondary, ItemRegistry.AK47, 12, 60),
            };
            save.BagVersion = CharacterSave.CurrentBagVersion;
            save.WeaponInHand = EquipSlots.Secondary;
            LocalServer.Database.For(PlayerKey.Local).Add(save);
            PlaySession.Begin(save, isNew: false);

            var holder = new GameObject("Holder");
            holder.SetActive(false);
            _made.Add(holder);
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0, 0.05f, 0), Quaternion.identity, holder.transform);
            body.GetComponent<PlayerInputReader>().enabled = false;
            var inventory = body.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.TShirt, Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/TShirt.prefab") },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.Pistol, Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab") },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.AK47, Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/AK47.prefab") },
            };
            body.transform.SetParent(null, true);
            _made.Add(body);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(ItemRegistry.Grenade, inventory.Storage[4].ItemId, "the storage did not come back");
            Assert.AreEqual(2, inventory.Storage[4].Count);
            Assert.AreEqual(ItemRegistry.TShirt, inventory.EquippedItemId(EquipSlots.Torso));
            Assert.AreEqual(EquipSlots.Secondary, inventory.ActiveWeaponSlot, "the AK-47 that was in hand is not");
            var ak = inventory.EquippedInstance(EquipSlots.Secondary).GetComponent<PistolWeapon>();
            Assert.AreEqual(12, ak.MagazineAmmo, "the AK-47 came back reloaded: its rounds were not kept");
            Assert.AreEqual(60, ak.ReserveAmmo);
            Assert.IsTrue(inventory.CycleWeapon(body), "the pistols are not there to draw");
            yield return null;
            var right = inventory.EquippedInstance(EquipSlots.Primary).GetComponent<PistolWeapon>();
            var left = inventory.EquippedInstance(EquipSlots.PrimaryOffHand).GetComponent<PistolWeapon>();
            Assert.IsTrue(left.InLeftHand, "the second pistol is not in the left hand");
            Assert.AreEqual(7, right.MagazineAmmo);
            Assert.AreEqual(3, left.MagazineAmmo);

            // What it carries now is what the server keeps: a round fired from the left gun, and the grenades moved.
            Assert.IsTrue(left.Fire(body.transform.position + Vector3.up * 2.0f, body.transform.forward, false, body));
            Assert.IsTrue(body.GetComponent<CharacterIdentity>().Write());
            CharacterSave stored = LocalServer.Characters.List(PlayerKey.Local).Single(c => c.Id == save.Id);
            Assert.AreEqual(CharacterSave.CurrentBagVersion, stored.BagVersion);
            Assert.AreEqual(EquipSlots.Primary, stored.WeaponInHand);
            BagEntry kept = stored.Bag.Single(e => e.Slot == EquipSlots.PrimaryOffHand);
            Assert.AreEqual(2, kept.Magazine, "the round fired is not gone from the saved bag");
            Assert.AreEqual(1, stored.Bag.Count(e => e.Slot == EquipSlots.Secondary && e.ItemId == ItemRegistry.AK47 && e.Magazine == 12));
            Assert.AreEqual(1, stored.Bag.Count(e => e.InStorage && e.ItemId == ItemRegistry.Grenade && e.Count == 2));
#else
            yield break;
#endif
        }
    }
}
