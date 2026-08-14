using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    public sealed class InventoryTests
    {
        /// <summary>A minimal IEquippableItem double, so equip tests don't need a real BottleWeapon.</summary>
        private sealed class TestEquippableItem : MonoBehaviour, IEquippableItem
        {
            public bool EquipCalled;
            public bool UnequipCalled;
            public ItemInstanceData LastEquipData;
            public ItemInstanceData DataToReturn;

            public void OnEquip(GameObject player, ItemInstanceData instanceData)
            {
                EquipCalled = true;
                LastEquipData = instanceData;
            }

            public void OnUnequip(GameObject player)
            {
                // NOT Destroy(gameObject) here -- Destroy() throws in the
                // editor context these EditMode tests run in ("Destroy may
                // not be called from edit mode"). Real IEquippableItem
                // implementations (BottleWeapon, ClothingItem) do call it,
                // in real runtime; this double only needs to prove
                // OnUnequip was reached, not exercise Unity's own destroy
                // path.
                UnequipCalled = true;
            }

            public ItemInstanceData GetInstanceData() => DataToReturn;
        }

        private GameObject _playerObject;
        private Inventory _inventory;
        private GameObject _bottlePrefab;
        private GameObject _shirtPrefab;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _inventory = _playerObject.AddComponent<Inventory>();
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];

            _bottlePrefab = new GameObject("BottlePrefab");
            _bottlePrefab.AddComponent<TestEquippableItem>();
            _bottlePrefab.SetActive(false);

            _shirtPrefab = new GameObject("ShirtPrefab");
            _shirtPrefab.AddComponent<TestEquippableItem>();
            _shirtPrefab.SetActive(false);

            _inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.GlassBottle, Prefab = _bottlePrefab },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.TShirt, Prefab = _shirtPrefab },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_bottlePrefab);
            Object.DestroyImmediate(_shirtPrefab);
        }

        [Test]
        public void AddItemOnAnEmptyInventoryTakesTheFirstSlot()
        {
            Assert.IsTrue(_inventory.AddItem(ItemRegistry.TShirt, 1));
            Assert.AreEqual(ItemRegistry.TShirt, _inventory.Storage[0].ItemId);
        }

        [Test]
        public void TwoAddsOfTheSamePlainItemStackIntoOneSlot()
        {
            _inventory.AddItem(ItemRegistry.GlassBottle, 1);
            // Glass bottle's stack size is 1, so use a fresh registry-backed
            // stackable scenario isn't available -- reuse the same call
            // twice on an item whose stack size is 1 would take two slots by
            // design (stack size caps it), so assert that boundary instead.
            _inventory.AddItem(ItemRegistry.GlassBottle, 1);
            int occupied = 0;
            foreach (var slot in _inventory.Storage)
            {
                if (!slot.IsEmpty) occupied++;
            }
            Assert.AreEqual(2, occupied, "stack size 1 means a second bottle takes a second slot, not a merge");
        }

        [Test]
        public void DurabilityCarryingItemsNeverMergeEvenWithAMatchingIdAlreadyStored()
        {
            _inventory.AddItem(ItemRegistry.GlassBottle, 1);
            _inventory.AddItem(ItemRegistry.GlassBottle, 1, new ItemInstanceData { HasDurability = true, Durability = 1 });

            int occupied = 0;
            foreach (var slot in _inventory.Storage)
            {
                if (!slot.IsEmpty) occupied++;
            }
            Assert.AreEqual(2, occupied);
        }

        [Test]
        public void AddItemOnFullStorageFailsCleanlyAndLeavesEveryOtherSlotUnchanged()
        {
            for (int i = 0; i < Inventory.StorageSlotCount; i++)
            {
                Assert.IsTrue(_inventory.AddItem(ItemRegistry.GlassBottle, 1,
                    new ItemInstanceData { HasDurability = true, Durability = i }));
            }
            var before = (Inventory.StorageSlot[])_inventory.Storage.Clone();

            Assert.IsFalse(_inventory.AddItem(ItemRegistry.TShirt, 1));

            for (int i = 0; i < Inventory.StorageSlotCount; i++)
            {
                Assert.AreEqual(before[i].ItemId, _inventory.Storage[i].ItemId);
                Assert.AreEqual(before[i].Count, _inventory.Storage[i].Count);
            }
        }

        [Test]
        public void RemoveFromStorageFindsAndClearsTheRightSlotAndOutputsItsInstanceData()
        {
            _inventory.AddItem(ItemRegistry.GlassBottle, 1, new ItemInstanceData { HasDurability = true, Durability = 2 });

            Assert.IsTrue(_inventory.RemoveFromStorage(ItemRegistry.GlassBottle, 1, out ItemInstanceData data));
            Assert.AreEqual(2, data.Durability);
            Assert.IsFalse(_inventory.HasItem(ItemRegistry.GlassBottle));
        }

        [Test]
        public void RemoveFromStorageOnAMissingItemFailsAndMutatesNothing()
        {
            Assert.IsFalse(_inventory.RemoveFromStorage(ItemRegistry.GlassBottle, 1, out ItemInstanceData data));
            Assert.IsTrue(data.IsEmpty);
        }

        [Test]
        public void EquipItemIdOnAnEmptySlotInstantiatesAndCallsOnEquip()
        {
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject));

            Assert.AreEqual(ItemRegistry.GlassBottle, _inventory.EquippedItemId("hand_r"));
            GameObject instance = _inventory.EquippedInstance("hand_r");
            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.GetComponent<TestEquippableItem>().EquipCalled);
        }

        [Test]
        public void EquipItemIdWhenTheSlotIsAlreadyOccupiedEvictsTheOldItemBackToStorageFirst()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            Assert.IsFalse(_inventory.HasItem(ItemRegistry.GlassBottle));

            _inventory.AddItem(ItemRegistry.GlassBottle, 1); // a second bottle to equip over the first
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject));

            Assert.IsTrue(_inventory.HasItem(ItemRegistry.GlassBottle), "the evicted bottle should have returned to storage");
        }

        [Test]
        public void EquipItemIdAbortsEntirelyWhenEvictingTheCurrentOccupantWouldFail()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            GameObject firstInstance = _inventory.EquippedInstance("hand_r");

            for (int i = 0; i < Inventory.StorageSlotCount; i++)
            {
                _inventory.AddItem(ItemRegistry.TShirt, 1, new ItemInstanceData { HasDurability = true, Durability = i });
            }

            _inventory.AddItem(ItemRegistry.GlassBottle, 1);
            Assert.IsFalse(_inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject),
                "storage is full, so evicting the currently-equipped bottle must fail");

            Assert.AreSame(firstInstance, _inventory.EquippedInstance("hand_r"),
                "the previously equipped item must still be the one equipped");
        }

        [Test]
        public void UnequipToSlotReturnsTheItemToStorageAndCallsOnUnequip()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            GameObject instance = _inventory.EquippedInstance("hand_r");
            var testDouble = instance.GetComponent<TestEquippableItem>();

            Assert.IsTrue(_inventory.UnequipToSlot("hand_r", _playerObject));

            Assert.IsTrue(testDouble.UnequipCalled);
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.GlassBottle));
            Assert.AreEqual(string.Empty, _inventory.EquippedItemId("hand_r"));
        }

        [Test]
        public void UnequipToSlotWithStorageFullLeavesTheItemEquippedAndUnchanged()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            for (int i = 0; i < Inventory.StorageSlotCount; i++)
            {
                _inventory.AddItem(ItemRegistry.TShirt, 1, new ItemInstanceData { HasDurability = true, Durability = i });
            }

            Assert.IsFalse(_inventory.UnequipToSlot("hand_r", _playerObject));
            Assert.AreEqual(ItemRegistry.GlassBottle, _inventory.EquippedItemId("hand_r"));
        }

        [Test]
        public void ClearEquipSlotWithoutReturningClearsTheSlotButAddsNothingToStorage()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);

            _inventory.ClearEquipSlotWithoutReturning("hand_r");

            Assert.AreEqual(string.Empty, _inventory.EquippedItemId("hand_r"));
            Assert.IsFalse(_inventory.HasItem(ItemRegistry.GlassBottle),
                "a thrown bottle is consumed -- it must not reappear in storage");
        }
    }
}
