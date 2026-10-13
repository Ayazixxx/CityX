using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The spike sword (owner's Sharpspikesword.glb, 2026-10-13): its own item, a real two-handed sword, equippable and seen by other players.</summary>
    public sealed class SpikeSwordTests
    {
        private const string PrefabPath = "Assets/CityX/Weapons/SpikeSword.prefab";

        [Test]
        public void ItIsATwoHandedSwordItemOfItsOwn()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.SpikeSword, out ItemDefinition definition));
            Assert.AreEqual("hand_r", definition.EquipSlot);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            SwordWeapon sword = prefab.GetComponent<SwordWeapon>();
            Assert.IsNotNull(sword, "not a sword: no swings, no two-handed hold");
            Assert.AreEqual(ItemRegistry.SpikeSword, sword.ItemId, "picking it up would give the old greatsword");
            Bounds bounds = prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            Assert.Greater(bounds.size.y, 1.5f, "the model did not come through");
        }

        [Test]
        public void EveryBodyCanEquipItSoOthersSeeIt()
        {
            foreach (string path in new[] { "Assets/CityX/Character/NetworkedPlayer.prefab" })
            {
                var inventory = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Inventory>(true);
                bool found = false;
                foreach (Inventory.ItemPrefabEntry entry in inventory.ItemPrefabs)
                {
                    found |= entry.ItemId == ItemRegistry.SpikeSword && entry.Prefab != null;
                }
                Assert.IsTrue(found, $"{path} cannot build the spike sword, so other players would not see it");
            }
        }
    }
}
