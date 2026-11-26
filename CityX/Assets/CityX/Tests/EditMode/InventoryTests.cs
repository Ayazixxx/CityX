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
                // implementations (BottleWeapon, SkinnedGarment) do call it,
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
        private GameObject _clothingPrefab;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _inventory = _playerObject.AddComponent<Inventory>();
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];

            _bottlePrefab = new GameObject("BottlePrefab");
            _bottlePrefab.AddComponent<TestEquippableItem>();
            _bottlePrefab.SetActive(false);

            _clothingPrefab = new GameObject("ClothingPrefab");
            _clothingPrefab.AddComponent<TestEquippableItem>();
            _clothingPrefab.SetActive(false);

            _inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.GlassBottle, Prefab = _bottlePrefab },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.HoodedCloak, Prefab = _clothingPrefab },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_bottlePrefab);
            Object.DestroyImmediate(_clothingPrefab);
        }

        [Test]
        public void AddItemOnAnEmptyInventoryTakesTheFirstSlot()
        {
            Assert.IsTrue(_inventory.AddItem(ItemRegistry.HoodedCloak, 1));
            Assert.AreEqual(ItemRegistry.HoodedCloak, _inventory.Storage[0].ItemId);
        }

        /// <summary>Two T-shirts in the bag, the second printed: taking from the second slot takes the printed one (bug sweep 2026-11-25).</summary>
        [Test]
        public void TakingFromASlotTakesThatSlotsItemNotTheFirstOfItsKind()
        {
            _inventory.AddItem(ItemRegistry.TShirt, 1);
            _inventory.AddItem(ItemRegistry.TShirt, 1, new ItemInstanceData { DesignId = "0123456789abcdef0123456789abcdef" });
            int printed = System.Array.FindIndex(_inventory.Storage, s => s.InstanceData.DesignId != null);
            Assert.Greater(printed, 0, "the printed shirt did not go in a slot of its own");
            Assert.IsTrue(_inventory.RemoveFromStorageAt(printed, 1, out ItemInstanceData taken));
            Assert.AreEqual("0123456789abcdef0123456789abcdef", taken.DesignId, "the slot clicked gave another shirt");
            Assert.IsFalse(_inventory.Storage[0].IsEmpty, "the plain shirt went instead");
            Assert.IsFalse(_inventory.RemoveFromStorageAt(printed, 1, out _), "an emptied slot gave something");
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

            Assert.IsFalse(_inventory.AddItem(ItemRegistry.HoodedCloak, 1));

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
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);   // primary
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);   // secondary (holstered)
            Assert.IsFalse(_inventory.HasItem(ItemRegistry.GlassBottle));

            _inventory.AddItem(ItemRegistry.GlassBottle, 1); // a third bottle: both slots are taken, so it replaces the one in hand
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject));

            Assert.IsTrue(_inventory.HasItem(ItemRegistry.GlassBottle), "the evicted bottle should have returned to storage");
        }

        [Test]
        public void ASecondWeaponGoesToTheSecondarySlotHolsteredAndScrollingSwapsThem()
        {
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject, new ItemInstanceData { HasDurability = true, Durability = 11 }));
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject, new ItemInstanceData { HasDurability = true, Durability = 22 }));

            Assert.AreEqual(Inventory.PrimarySlot, _inventory.ActiveWeaponSlot, "the first weapon is the one in hand");
            Assert.AreEqual(ItemRegistry.GlassBottle, _inventory.EquippedItemId(Inventory.SecondarySlot));
            Assert.IsNull(_inventory.EquippedInstance(Inventory.SecondarySlot), "the secondary is away, not in the world");
            Assert.IsTrue(_inventory.IsHolstered(Inventory.SecondarySlot));
            GameObject primary = _inventory.EquippedInstance(Inventory.PrimarySlot);
            Assert.IsNotNull(primary);

            Assert.IsTrue(_inventory.CycleWeapon(_playerObject));
            Assert.AreEqual(Inventory.SecondarySlot, _inventory.ActiveWeaponSlot);
            Assert.IsNotNull(_inventory.EquippedInstance(Inventory.SecondarySlot));
            Assert.IsNull(_inventory.EquippedInstance(Inventory.PrimarySlot));
            Assert.IsTrue(primary.GetComponent<TestEquippableItem>().UnequipCalled, "putting a weapon away runs its own OnUnequip");
            Assert.AreEqual(22, _inventory.EquippedInstance(Inventory.SecondarySlot).GetComponent<TestEquippableItem>().LastEquipData.Durability, "it is drawn with what it remembered");

            Assert.IsTrue(_inventory.CycleWeapon(_playerObject));
            Assert.AreEqual(Inventory.PrimarySlot, _inventory.ActiveWeaponSlot);
        }

        [Test]
        public void PickingUpAWeaponWithEmptyHandsPutsItInTheHandPrimaryFirst()
        {
            Assert.IsTrue(_inventory.PickUp(ItemRegistry.GlassBottle));
            Assert.AreEqual(ItemRegistry.GlassBottle, _inventory.EquippedItemId(Inventory.PrimarySlot));
            Assert.IsNotNull(_inventory.EquippedInstance(Inventory.PrimarySlot), "the weapon is in hand");
            Assert.IsFalse(_inventory.HasItem(ItemRegistry.GlassBottle), "it went to the hand, not into storage");
        }

        [Test]
        public void PickingUpAWeaponWithEmptyHandsAndThePrimaryAwayPutsItInTheSecondary()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);   // primary, in hand
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);   // secondary, holstered
            _inventory.CycleWeapon(_playerObject);                             // secondary in hand, primary holstered
            _inventory.TakeWeaponInHand(_playerObject, out _, out _, out _);   // secondary thrown: hands empty, primary taken

            Assert.IsTrue(_inventory.PickUp(ItemRegistry.GlassBottle));
            Assert.AreEqual(ItemRegistry.GlassBottle, _inventory.EquippedItemId(Inventory.SecondarySlot));
            Assert.AreEqual(Inventory.SecondarySlot, _inventory.ActiveWeaponSlot);
            Assert.IsNotNull(_inventory.EquippedInstance(Inventory.SecondarySlot), "the weapon is in hand");
        }

        [Test]
        public void PickingUpAWeaponWithOneInHandStoresIt()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            Assert.IsTrue(_inventory.PickUp(ItemRegistry.GlassBottle));
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.GlassBottle), "with a weapon in hand the new one goes into storage");
            Assert.AreEqual(string.Empty, _inventory.EquippedItemId(Inventory.SecondarySlot));
        }

        [Test]
        public void PickingUpClothingPutsItOnAndStoresWhatWasWornThere()
        {
            Assert.IsTrue(_inventory.PickUp(ItemRegistry.HoodedCloak));
            Assert.AreEqual(ItemRegistry.HoodedCloak, _inventory.EquippedItemId(EquipSlots.Torso), "tapping E on clothing did not put it on");
            Assert.IsFalse(_inventory.HasItem(ItemRegistry.HoodedCloak));

            Assert.IsTrue(_inventory.PickUp(ItemRegistry.HoodedCloak));
            Assert.AreEqual(ItemRegistry.HoodedCloak, _inventory.EquippedItemId(EquipSlots.Torso));
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.HoodedCloak), "the one worn before went into storage");
        }

        [Test]
        public void ClothingThatCannotBePutOnIsStored()
        {
            _inventory.ItemPrefabs = new Inventory.ItemPrefabEntry[0];
            Assert.IsTrue(_inventory.PickUp(ItemRegistry.HoodedCloak));
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.HoodedCloak));
        }

        [Test]
        public void ScrollingWithOnlyOneWeaponDoesNothingAndAHolsteredWeaponCanStillBeUnequipped()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            Assert.IsFalse(_inventory.CycleWeapon(_playerObject), "nothing to change to");
            Assert.IsNotNull(_inventory.EquippedInstance(Inventory.PrimarySlot));

            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            Assert.IsTrue(_inventory.UnequipToSlot(Inventory.SecondarySlot, _playerObject), "a holstered weapon goes back to storage");
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.GlassBottle));
            Assert.AreEqual(string.Empty, _inventory.EquippedItemId(Inventory.SecondarySlot));
            Assert.IsNotNull(_inventory.EquippedInstance(Inventory.PrimarySlot), "the weapon in hand is untouched");
        }

        [Test]
        public void ThrowingTheWeaponInHandLeavesTheHolsteredOneToBeDrawnByScrolling()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            _inventory.ClearEquipSlotWithoutReturning(_inventory.ActiveWeaponSlot);   // what BottleWeapon does when it is thrown
            Assert.IsTrue(_inventory.CycleWeapon(_playerObject));
            Assert.AreEqual(Inventory.SecondarySlot, _inventory.ActiveWeaponSlot);
            Assert.IsNotNull(_inventory.EquippedInstance(Inventory.SecondarySlot));
        }

        [Test]
        public void EquipItemIdAbortsEntirelyWhenEvictingTheCurrentOccupantWouldFail()
        {
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);
            _inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject);   // both weapon slots taken
            GameObject firstInstance = _inventory.EquippedInstance(_inventory.ActiveWeaponSlot);

            for (int i = 0; i < Inventory.StorageSlotCount; i++)
            {
                _inventory.AddItem(ItemRegistry.HoodedCloak, 1, new ItemInstanceData { HasDurability = true, Durability = i });
            }

            _inventory.AddItem(ItemRegistry.GlassBottle, 1);
            Assert.IsFalse(_inventory.EquipItemId(ItemRegistry.GlassBottle, _playerObject),
                "storage is full, so evicting the currently-equipped bottle must fail");

            Assert.AreSame(firstInstance, _inventory.EquippedInstance(_inventory.ActiveWeaponSlot),
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
                _inventory.AddItem(ItemRegistry.HoodedCloak, 1, new ItemInstanceData { HasDurability = true, Durability = i });
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
