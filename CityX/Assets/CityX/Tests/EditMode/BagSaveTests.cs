using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE BAG KEPT ON THE SERVER (owner, 2026-11-19: "ok do server side saving next"; docs/SERVER_SAVING.md, step 3): everything carried goes to
    /// the server with the character and comes back on the body as it was, the storage slots, the clothes, each weapon in its own slot with its
    /// rounds, a pair of pistols, the one in hand.
    /// </summary>
    public sealed class BagSaveTests
    {
        private const string Ayaz = "0123456789abcdef0123456789abcdef";

        /// <summary>An item double that keeps the state it was equipped with and which hand it went in.</summary>
        private sealed class KeptItem : MonoBehaviour, IEquippableItem, IOffHandItem
        {
            public ItemInstanceData Data;
            public bool InHand;
            public bool InLeftHand;

            public void OnEquip(GameObject player, ItemInstanceData instanceData)
            {
                Data = instanceData;
                InHand = true;
            }

            public void OnEquipOffHand(GameObject player, ItemInstanceData instanceData)
            {
                Data = instanceData;
                InLeftHand = true;
            }

            public void MountCosmeticOffHand(GameObject body)
            {
            }

            public void OnUnequip(GameObject player)
            {
            }

            public ItemInstanceData GetInstanceData() => Data;
        }

        private string _directory;
        private readonly List<GameObject> _made = new List<GameObject>();
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            CharacterStore.DirectoryOverride = _directory;
            _prefab = new GameObject("KeptItem");
            _prefab.AddComponent<KeptItem>();
            _prefab.SetActive(false);
            _made.Add(_prefab);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                Object.DestroyImmediate(go);
            }
            _made.Clear();
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private Inventory Body()
        {
            var body = new GameObject("Body");
            _made.Add(body);
            var inventory = body.AddComponent<Inventory>();
            inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];
            body.AddComponent<PlayerSkills>().LearnEverything();
            inventory.ItemPrefabs = new[] { ItemRegistry.Pistol, ItemRegistry.AK47, ItemRegistry.TShirt, ItemRegistry.KnightHelmet }
                .Select(id => new Inventory.ItemPrefabEntry { ItemId = id, Prefab = _prefab }).ToArray();
            return inventory;
        }

        private static ItemInstanceData Rounds(int magazine, int reserve) => new ItemInstanceData { HasAmmo = true, Magazine = magazine, Reserve = reserve };

        /// <summary>A body carrying a bit of everything: storage, clothes, a pair of pistols in the primary slot, an AK-47 in the secondary, in hand.</summary>
        private Inventory Loaded()
        {
            Inventory inventory = Body();
            GameObject player = inventory.gameObject;
            inventory.AddItem(ItemRegistry.Grenade, 2);
            inventory.AddItem(ItemRegistry.GlassBottle, 1, new ItemInstanceData { HasDurability = true, Durability = 1 });
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.TShirt, player));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.KnightHelmet, player));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Pistol, player, Rounds(7, 20)));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Pistol, player, Rounds(3, 10)));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.AK47, player, Rounds(12, 60)));
            Assert.IsTrue(inventory.CycleWeapon(player), "setup: the AK-47 was not drawn");
            return inventory;
        }

        private static string Describe(BagEntry e) =>
            $"{e.Slot}#{(e.InStorage ? e.Index : 0)} {e.ItemId} x{e.Count} d{e.HasDurability}:{e.Durability} a{e.HasAmmo}:{e.Magazine}/{e.Reserve} c{e.HasContents}:{e.Contents}";

        [Test]
        public void EverythingCarriedComesBackOnTheBodyAsItWas()
        {
            Inventory before = Loaded();
            List<BagEntry> bag = before.ToBag();
            Assert.AreEqual(EquipSlots.Secondary, before.ActiveWeaponSlot);
            CollectionAssert.Contains(bag.Select(Describe).ToList(), $"{EquipSlots.Primary}#0 {ItemRegistry.Pistol} x1 dFalse:0 aTrue:7/20 cFalse:0",
                "a holstered gun's rounds are not in the bag");
            CollectionAssert.Contains(bag.Select(Describe).ToList(), $"{EquipSlots.Secondary}#0 {ItemRegistry.AK47} x1 dFalse:0 aTrue:12/60 cFalse:0",
                "the gun in hand's rounds are not in the bag");

            Inventory after = Body();
            after.FromBag(bag, before.ActiveWeaponSlot, after.gameObject);
            CollectionAssert.AreEquivalent(bag.Select(Describe).ToList(), after.ToBag().Select(Describe).ToList(), "the bag did not come back as it went");
            Assert.AreEqual(EquipSlots.Secondary, after.ActiveWeaponSlot, "the weapon that was in hand is not in hand");
            Assert.IsNotNull(after.EquippedInstance(EquipSlots.Secondary), "the AK-47 was not drawn");
            Assert.IsNull(after.EquippedInstance(EquipSlots.Primary), "the pistols came out of their holsters");
            Assert.IsTrue(after.CycleWeapon(after.gameObject));
            Assert.IsTrue(after.EquippedInstance(EquipSlots.Primary).GetComponent<KeptItem>().InHand);
            Assert.IsTrue(after.EquippedInstance(EquipSlots.PrimaryOffHand).GetComponent<KeptItem>().InLeftHand, "the pair is not drawn into the left hand");
            Assert.AreEqual(3, after.EquippedInstance(EquipSlots.PrimaryOffHand).GetComponent<KeptItem>().Data.Magazine);
            Assert.AreEqual(ItemRegistry.TShirt, after.EquippedItemId(EquipSlots.Torso));
        }

        [Test]
        public void ASavedBagIsTheWholeBagNotAddedToWhatTheBodyHad()
        {
            Inventory body = Body();
            body.AddItem(ItemRegistry.Grenade, 1);
            body.EquipItemId(ItemRegistry.AK47, body.gameObject);
            body.FromBag(new List<BagEntry> { new BagEntry { Index = 5, ItemId = ItemRegistry.GlassBottle } }, EquipSlots.Primary, body.gameObject);
            CollectionAssert.AreEqual(new[] { "#5 glass_bottle x1 dFalse:0 aFalse:0/0 cFalse:0" }, body.ToBag().Select(Describe).ToArray());
        }

        [Test]
        public void ThingsThatCannotBeWhereTheBagSaysAreKeptInStorageAndUnknownOnesLeftOut()
        {
            Inventory body = Body();
            var bag = new List<BagEntry>
            {
                new BagEntry { Index = 0, ItemId = ItemRegistry.Grenade, Count = 2 },
                new BagEntry { Index = 0, ItemId = ItemRegistry.GlassBottle },                   // the same storage slot again
                new BagEntry { Index = 1, ItemId = "golden_minigun" },                            // not a thing the game has
                new BagEntry { Slot = EquipSlots.Primary, ItemId = ItemRegistry.TShirt },         // a shirt is not a weapon
                new BagEntry { Slot = EquipSlots.PrimaryOffHand, ItemId = ItemRegistry.AK47 },    // only a pistol goes beside a pistol
            };
            body.FromBag(bag, EquipSlots.Primary, body.gameObject);
            string[] kept = body.Storage.Where(s => !s.IsEmpty).Select(s => s.ItemId).OrderBy(id => id).ToArray();
            CollectionAssert.AreEqual(new[] { ItemRegistry.AK47, ItemRegistry.GlassBottle, ItemRegistry.Grenade, ItemRegistry.TShirt }, kept);
            Assert.IsEmpty(body.EquippedItemId(EquipSlots.Primary));
            Assert.IsEmpty(body.EquippedItemId(EquipSlots.PrimaryOffHand));
        }

        [Test]
        public void TheServerKeepsTheBagAndAnOlderCopyOfTheGameCannotEmptyIt()
        {
            var service = new CharacterService(new CharacterDatabase(Path.Combine(_directory, "server")));
            CharacterSave made = service.Create(Ayaz, CharacterSave.Create("Ayaz", Color.white));
            Assert.AreEqual(0, made.BagVersion, "a new character has no bag yet: it plays as one always did");
            CharacterSave playing = service.Play(Ayaz, made.Id);
            playing.Bag = Loaded().ToBag();
            playing.BagVersion = CharacterSave.CurrentBagVersion;
            playing.WeaponInHand = EquipSlots.Secondary;
            Assert.IsTrue(service.Save(Ayaz, playing));

            CharacterSave stored = service.Play(Ayaz, made.Id);   // from the file again
            Assert.AreEqual(CharacterSave.CurrentBagVersion, stored.BagVersion);
            Assert.AreEqual(EquipSlots.Secondary, stored.WeaponInHand);
            CollectionAssert.AreEquivalent(playing.Bag.Select(Describe).ToList(), stored.Bag.Select(Describe).ToList(), "the bag did not survive the server's file");

            CharacterSave old = service.Play(Ayaz, made.Id);
            old.Bag = new List<BagEntry>();
            old.BagVersion = 0;   // a copy of the game from before the bag was kept
            service.Save(Ayaz, old);
            Assert.AreEqual(playing.Bag.Count, service.Play(Ayaz, made.Id).Bag.Count, "an older copy of the game emptied the bag");
        }
    }
}
