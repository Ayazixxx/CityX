using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A shirt under a coat in the top slot (owner, 2026-11-21: "make it so i can wear a shirt underneath the parka. make it so they both go into
    /// the top slot in inventory and can stack"): either order, one shirt and one coat at most, the coat off first, both worn, shown, saved and
    /// told to other players.
    /// </summary>
    public sealed class TopLayersTests
    {
        /// <summary>A garment double: whether it is on the body.</summary>
        private sealed class Garment : MonoBehaviour, IEquippableItem
        {
            public bool Worn;

            public void OnEquip(GameObject player, ItemInstanceData instanceData) => Worn = true;

            public void OnUnequip(GameObject player) => Worn = false;   // no Destroy: EditMode

            public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
        }

        private const string Shirt = ItemRegistry.TShirt;
        private const string OtherShirt = ItemRegistry.BlackTShirt;
        private const string Parka = ItemRegistry.GreenFurHoodedParka;
        private const string OtherCoat = ItemRegistry.MidnightCoat;

        private GameObject _player;
        private Inventory _inventory;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _inventory = _player.AddComponent<Inventory>();
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];
            _prefab = new GameObject("GarmentDouble");
            _prefab.AddComponent<Garment>();
            _prefab.SetActive(false);
            _inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = Shirt, Prefab = _prefab },
                new Inventory.ItemPrefabEntry { ItemId = OtherShirt, Prefab = _prefab },
                new Inventory.ItemPrefabEntry { ItemId = Parka, Prefab = _prefab },
                new Inventory.ItemPrefabEntry { ItemId = OtherCoat, Prefab = _prefab },
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Garment garment in Object.FindObjectsByType<Garment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(garment.gameObject);
            }
            Object.DestroyImmediate(_player);
        }

        private bool Worn(string slot) => _inventory.EquippedInstance(slot) != null && _inventory.EquippedInstance(slot).GetComponent<Garment>().Worn;

        private void AssertShirtUnderParka(string shirt)
        {
            Assert.AreEqual(Parka, _inventory.EquippedItemId(EquipSlots.Torso), "the coat is not the top");
            Assert.AreEqual(shirt, _inventory.EquippedItemId(EquipSlots.TorsoUnder), "the shirt is not under the coat");
            Assert.IsTrue(Worn(EquipSlots.Torso) && Worn(EquipSlots.TorsoUnder), "both are not on the body");
            Assert.AreEqual(shirt, _inventory.SecondItemId(EquipSlots.Torso), "the menus do not show the shirt beside the coat");
        }

        [Test]
        public void AShirtAndACoatStackInEitherOrder()
        {
            Assert.IsTrue(_inventory.EquipItemId(Shirt, _player));
            Assert.IsTrue(_inventory.EquipItemId(Parka, _player));
            AssertShirtUnderParka(Shirt);

            Assert.IsTrue(_inventory.UnequipToSlot(EquipSlots.Torso, _player));
            Assert.IsTrue(_inventory.UnequipToSlot(EquipSlots.Torso, _player));
            Assert.IsTrue(_inventory.HasItem(Shirt) && _inventory.HasItem(Parka), "taking both off did not put both in storage");
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];
            Assert.IsTrue(_inventory.EquipItemId(Parka, _player));
            Assert.IsTrue(_inventory.EquipItemId(Shirt, _player));
            AssertShirtUnderParka(Shirt);
            Assert.IsFalse(_inventory.HasItem(Shirt) || _inventory.HasItem(Parka), "something worn is in storage too");
        }

        [Test]
        public void ASecondShirtOrCoatTakesThePlaceOfItsOwnKind()
        {
            _inventory.EquipItemId(Shirt, _player);
            _inventory.EquipItemId(Parka, _player);
            Assert.AreEqual(Shirt, TopLayers.Replaces(OtherShirt, _inventory.EquippedItemId));
            Assert.AreEqual(Parka, TopLayers.Replaces(OtherCoat, _inventory.EquippedItemId));

            Assert.IsTrue(_inventory.EquipItemId(OtherShirt, _player));
            AssertShirtUnderParka(OtherShirt);
            Assert.IsTrue(_inventory.HasItem(Shirt), "the shirt taken off did not go to storage");

            Assert.IsTrue(_inventory.EquipItemId(OtherCoat, _player));
            Assert.AreEqual(OtherCoat, _inventory.EquippedItemId(EquipSlots.Torso));
            Assert.AreEqual(OtherShirt, _inventory.EquippedItemId(EquipSlots.TorsoUnder), "a new coat took the shirt off");
            Assert.IsTrue(_inventory.HasItem(Parka), "the coat taken off did not go to storage");

            // Without a coat, a shirt takes the place of the shirt, as before.
            _inventory.UnequipToSlot(EquipSlots.Torso, _player);
            Assert.IsTrue(_inventory.EquipItemId(Shirt, _player));
            Assert.AreEqual(Shirt, _inventory.EquippedItemId(EquipSlots.Torso));
            Assert.IsTrue(string.IsNullOrEmpty(_inventory.EquippedItemId(EquipSlots.TorsoUnder)));
        }

        [Test]
        public void TheCoatComesOffFirstAndTheShirtStaysOn()
        {
            _inventory.EquipItemId(Shirt, _player);
            _inventory.EquipItemId(Parka, _player);
            Assert.IsTrue(_inventory.UnequipLast(EquipSlots.Torso, _player), "a menu click on the top slot took nothing off");
            Assert.AreEqual(Shirt, _inventory.EquippedItemId(EquipSlots.Torso), "the shirt is not the top once the coat is off");
            Assert.IsTrue(string.IsNullOrEmpty(_inventory.EquippedItemId(EquipSlots.TorsoUnder)));
            Assert.IsTrue(Worn(EquipSlots.Torso), "the shirt came off with the coat");
            Assert.IsTrue(_inventory.HasItem(Parka), "the coat did not go to storage");
        }

        [Test]
        public void BothAreSavedToldToOtherPlayersAndPutBackOn()
        {
            _inventory.EquipItemId(Shirt, _player);
            _inventory.EquipItemId(Parka, _player);
            CollectionAssert.AreEquivalent(new[] { Parka, Shirt }, _inventory.WornClothingIds(), "the save does not keep both");
            CollectionAssert.AreEquivalent(new[] { Parka, Shirt }, _inventory.VisibleItemIds(), "other players are not told of both");

            var bag = _inventory.ToBag();
            _inventory.FromBag(bag, Inventory.PrimarySlot, _player);
            AssertShirtUnderParka(Shirt);
        }
    }
}
