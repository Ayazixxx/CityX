using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Ammunition in the bag (owner, 2026-11-21: "pick up into your inventory and press r to reload into your guns"): rounds counted and taken
    /// across stacks, added without a stack passing its size; and every gun's ammunition is an item with a box on the ground that every body
    /// can show, its card saying what it loads.
    /// </summary>
    public sealed class AmmoTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private Inventory Bag()
        {
            var go = new GameObject("Bag");
            _made.Add(go);
            return go.AddComponent<Inventory>();
        }

        [Test]
        public void RoundsGoInStackByStackSizeAndComeOutAcrossStacks()
        {
            Inventory bag = Bag();
            int stack = ItemRegistry.ShotgunShellStack;
            Assert.AreEqual(stack + 10, bag.AddStacked(ItemRegistry.ShotgunShells, stack + 10));
            int stacks = 0;
            foreach (Inventory.StorageSlot slot in bag.Storage)
            {
                if (slot.ItemId == ItemRegistry.ShotgunShells)
                {
                    stacks++;
                    Assert.LessOrEqual(slot.Count, stack, "a stack went past its size (the server would refuse the bag)");
                }
            }
            Assert.AreEqual(2, stacks);
            Assert.AreEqual(stack + 10, bag.CountInStorage(ItemRegistry.ShotgunShells));
            Assert.AreEqual(stack + 5, bag.TakeFromStorage(ItemRegistry.ShotgunShells, stack + 5), "not taken across both stacks");
            Assert.AreEqual(5, bag.CountInStorage(ItemRegistry.ShotgunShells));
            Assert.AreEqual(5, bag.TakeFromStorage(ItemRegistry.ShotgunShells, 50), "more taken than there was");
            Assert.AreEqual(0, bag.CountInStorage(ItemRegistry.ShotgunShells));
        }

        [Test]
        public void AFullBagTakesOnlyWhatFits()
        {
            Inventory bag = Bag();
            for (int i = 0; i < Inventory.StorageSlotCount - 1; i++)
            {
                bag.Storage[i] = new Inventory.StorageSlot { ItemId = ItemRegistry.Pistol, Count = 1 };
            }
            Assert.AreEqual(ItemRegistry.RpgRocketStack, bag.AddStacked(ItemRegistry.RpgRockets, ItemRegistry.RpgRocketStack + 3), "one free slot takes one stack");
        }

        [Test]
        public void EveryGunsAmmunitionIsAnItemWithABoxOnTheGroundThatEveryBodyCanShow()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponent<Inventory>();
            foreach (string gunId in new[] { ItemRegistry.Pistol, ItemRegistry.AK47, ItemRegistry.Shotgun, ItemRegistry.RPG })
            {
                PistolWeapon gun = player.PrefabForItem(gunId).GetComponent<PistolWeapon>();
                string ammoId = gun.Profile.AmmoItemId;
                Assert.IsTrue(ItemRegistry.TryGet(ammoId, out ItemDefinition ammo), $"{gunId} loads \"{ammoId}\", which is no item");
                Assert.AreEqual(ItemRegistry.AmmoCategory, ammo.Category);
                Assert.IsEmpty(ammo.EquipSlot, $"{ammoId} can be equipped");
                GameObject box = player.PrefabForItem(ammoId);
                Assert.IsNotNull(box, $"no box of {ammoId} on the body's list -- rebuild the scene and NetworkedPlayer");
                Assert.AreEqual(ammoId, box.GetComponent<AmmoBox>().ItemId);
                Assert.Greater(box.GetComponent<AmmoBox>().Rounds, 0);
                Assert.LessOrEqual(box.GetComponent<AmmoBox>().Rounds, ammo.StackSize, "a box holds more than a bag slot takes");
                Assert.Greater(box.GetComponentsInChildren<Renderer>().Length, 0, $"the box of {ammoId} has no model");
                Assert.AreEqual("Ammunition", ItemCards.Describe(ammoId, box).Kind);
            }
        }
    }
}
