using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The inventory with pairs of pistols (docs/DUAL_PISTOLS.md): up to four carried, a pair drawn and put away together, a pair broken up
    /// the way the owner would expect (the left-hand pistol takes the slot, or goes back to storage when it cannot stay), and what another
    /// player's copy is told.
    /// </summary>
    public sealed class DualPistolsInventoryTests
    {
        /// <summary>A pistol double: which hand it was put in, and whether it was put away.</summary>
        private sealed class HandedItem : MonoBehaviour, IEquippableItem, IOffHandItem
        {
            public bool InLeftHand;
            public bool InRightHand;
            public bool PutAway;

            public void OnEquip(GameObject player, ItemInstanceData instanceData) => InRightHand = true;

            public void OnEquipOffHand(GameObject player, ItemInstanceData instanceData) => InLeftHand = true;

            public void MountCosmeticOffHand(GameObject body)
            {
            }

            public void OnUnequip(GameObject player) => PutAway = true;   // no Destroy: EditMode

            public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
        }

        private GameObject _player;
        private Inventory _inventory;
        private PlayerSkills _skills;
        private GameObject _pistolPrefab;
        private GameObject _plainPrefab;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _inventory = _player.AddComponent<Inventory>();
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];
            _skills = _player.AddComponent<PlayerSkills>();
            _skills.LearnEverything();
            _pistolPrefab = new GameObject("PistolDouble");
            _pistolPrefab.AddComponent<HandedItem>();
            _pistolPrefab.SetActive(false);
            _plainPrefab = new GameObject("PlainDouble");
            _plainPrefab.AddComponent<HandedItem>();
            _plainPrefab.SetActive(false);
            _inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.Pistol, Prefab = _pistolPrefab },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.CombatSword, Prefab = _plainPrefab },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.RiotShield, Prefab = _plainPrefab },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_pistolPrefab);
            Object.DestroyImmediate(_plainPrefab);
        }

        private HandedItem In(string slot)
        {
            GameObject instance = _inventory.EquippedInstance(slot);
            return instance != null ? instance.GetComponent<HandedItem>() : null;
        }

        private void EquipPistols(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.Pistol, _player), $"pistol {i + 1}");
            }
        }

        [Test]
        public void FourPistolsAPairInEachSlotTheFirstPairInTheHands()
        {
            EquipPistols(4);
            foreach (string slot in new[] { EquipSlots.Primary, EquipSlots.PrimaryOffHand, EquipSlots.Secondary, EquipSlots.SecondaryOffHand })
            {
                Assert.AreEqual(ItemRegistry.Pistol, _inventory.EquippedItemId(slot), slot);
            }
            Assert.IsTrue(In(EquipSlots.Primary).InRightHand);
            Assert.IsTrue(In(EquipSlots.PrimaryOffHand).InLeftHand, "the second goes in the left hand");
            Assert.IsNull(In(EquipSlots.Secondary), "the secondary pair waits in its holsters");
            Assert.IsNull(In(EquipSlots.SecondaryOffHand));
            Assert.AreEqual(EquipSlots.Primary, _inventory.ActiveWeaponSlot);
        }

        [Test]
        public void ScrollingPutsOnePairAwayAndDrawsTheOther()
        {
            EquipPistols(4);
            HandedItem right = In(EquipSlots.Primary), left = In(EquipSlots.PrimaryOffHand);
            Assert.IsTrue(_inventory.CycleWeapon(_player));
            Assert.IsTrue(right.PutAway && left.PutAway, "both put away");
            Assert.AreEqual(EquipSlots.Secondary, _inventory.ActiveWeaponSlot);
            Assert.IsTrue(In(EquipSlots.Secondary).InRightHand);
            Assert.IsTrue(In(EquipSlots.SecondaryOffHand).InLeftHand);
            Assert.IsNull(In(EquipSlots.Primary));
            Assert.IsNull(In(EquipSlots.PrimaryOffHand));
            Assert.IsTrue(_inventory.CycleWeapon(_player));
            Assert.IsTrue(In(EquipSlots.Primary).InRightHand && In(EquipSlots.PrimaryOffHand).InLeftHand, "and back");
        }

        [Test]
        public void WithoutTheSkillTheSecondPistolGoesToTheSecondarySlot()
        {
            _skills.ResetAll();
            EquipPistols(2);
            Assert.AreEqual(ItemRegistry.Pistol, _inventory.EquippedItemId(EquipSlots.Secondary));
            Assert.IsEmpty(_inventory.EquippedItemId(EquipSlots.PrimaryOffHand));
        }

        [Test]
        public void PuttingTheRightHandPistolAwayHandsItsSlotToTheLeftHandOne()
        {
            EquipPistols(2);
            HandedItem left = In(EquipSlots.PrimaryOffHand);
            Assert.IsTrue(_inventory.UnequipToSlot(EquipSlots.Primary, _player));
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.Pistol), "one back in storage");
            Assert.IsTrue(left.PutAway);
            Assert.AreEqual(ItemRegistry.Pistol, _inventory.EquippedItemId(EquipSlots.Primary), "the other took its place");
            Assert.IsEmpty(_inventory.EquippedItemId(EquipSlots.PrimaryOffHand));
            Assert.IsTrue(In(EquipSlots.Primary).InRightHand, "into the right hand");
        }

        [Test]
        public void AMenuClickGivesBackTheLeftHandPistolFirst()
        {
            EquipPistols(2);
            Assert.AreEqual(ItemRegistry.Pistol, _inventory.PairedItemId(EquipSlots.Primary));
            Assert.IsTrue(_inventory.UnequipLast(EquipSlots.Primary, _player));
            Assert.IsEmpty(_inventory.PairedItemId(EquipSlots.Primary));
            Assert.AreEqual(ItemRegistry.Pistol, _inventory.EquippedItemId(EquipSlots.Primary), "the right hand keeps its own");
        }

        [Test]
        public void AWeaponThatCannotBePairedTakingAPairsSlotSendsTheLeftHandPistolBack()
        {
            EquipPistols(2);
            _inventory.EquipItemId(ItemRegistry.CombatSword, _player);   // the secondary
            Assert.AreEqual(ItemRegistry.CombatSword, _inventory.EquippedItemId(EquipSlots.Secondary));
            Assert.IsTrue(_inventory.CycleWeapon(_player));
            Assert.IsTrue(_inventory.CycleWeapon(_player));   // back to the pistols
            // Both slots full: a sword replaces the pair in hand, and the left-hand pistol cannot stay beside it.
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.CombatSword, _player));
            Assert.AreEqual(ItemRegistry.CombatSword, _inventory.EquippedItemId(EquipSlots.Primary));
            Assert.IsEmpty(_inventory.PairedItemId(EquipSlots.Primary));
            Assert.AreEqual(2, CountInStorage(ItemRegistry.Pistol), "both pistols back in storage");
        }

        [Test]
        public void AShieldTakesTheLeftHandSoNoPistolStaysInIt()
        {
            EquipPistols(4);
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.RiotShield, _player));
            Assert.IsEmpty(_inventory.PairedItemId(EquipSlots.Primary));
            Assert.IsEmpty(_inventory.PairedItemId(EquipSlots.Secondary));
            Assert.AreEqual(2, CountInStorage(ItemRegistry.Pistol));
            Assert.IsFalse(_inventory.PairsPistols, "no pairs while the shield is on");
            _inventory.EquipItemId(ItemRegistry.Pistol, _player);
            Assert.IsEmpty(_inventory.PairedItemId(EquipSlots.Primary), "a pistol equipped now replaces, it does not pair");
        }

        [Test]
        public void ForgettingTheSkillSendsTheLeftHandPistolsBack()
        {
            EquipPistols(4);
            _skills.ResetAll();
            Assert.IsEmpty(_inventory.PairedItemId(EquipSlots.Primary));
            Assert.IsEmpty(_inventory.PairedItemId(EquipSlots.Secondary));
            Assert.AreEqual(ItemRegistry.Pistol, _inventory.EquippedItemId(EquipSlots.Primary));
            Assert.AreEqual(ItemRegistry.Pistol, _inventory.EquippedItemId(EquipSlots.Secondary));
            Assert.AreEqual(2, CountInStorage(ItemRegistry.Pistol));
        }

        [Test]
        public void AnotherPlayersCopyIsToldWhichPistolIsInTheLeftHand()
        {
            EquipPistols(2);
            var shown = _inventory.VisibleItemIds();
            CollectionAssert.AreEquivalent(new[] { ItemRegistry.Pistol, Inventory.PairMark + ItemRegistry.Pistol }, shown);
            Assert.IsTrue(Inventory.ShownAsPair(Inventory.PairMark + ItemRegistry.Pistol, out string item));
            Assert.AreEqual(ItemRegistry.Pistol, item);
            Assert.IsFalse(Inventory.ShownAsPair(ItemRegistry.Pistol, out item));
            Assert.AreEqual(ItemRegistry.Pistol, item);
        }

        [Test]
        public void ADyingBodyLetsGoOfBothPistols()
        {
            EquipPistols(2);
            Assert.IsTrue(_inventory.TakeWeaponInHand(_player, out string first, out _, out _));
            Assert.IsTrue(In(EquipSlots.Primary) != null && _inventory.EquippedInstance(EquipSlots.PrimaryOffHand) == null, "the left hand's first");
            Assert.IsTrue(_inventory.TakeWeaponInHand(_player, out string second, out _, out _));
            Assert.AreEqual(ItemRegistry.Pistol, first);
            Assert.AreEqual(ItemRegistry.Pistol, second);
            Assert.IsFalse(_inventory.TakeWeaponInHand(_player, out _, out _, out _), "nothing left in hand");
        }

        private int CountInStorage(string itemId)
        {
            int count = 0;
            foreach (Inventory.StorageSlot slot in _inventory.Storage)
            {
                if (!slot.IsEmpty && slot.ItemId == itemId)
                {
                    count += slot.Count;
                }
            }
            return count;
        }
    }
}
