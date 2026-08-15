using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Real-frame behavior for clothing items -- Destroy() defers to end of
    /// frame, so verifying anything it does needs a real yielded frame, not
    /// an EditMode test (see ClothingItemTests for the plain-logic half).
    /// </summary>
    public sealed class ClothingItemPlayTests
    {
        private GameObject _playerObject;
        private Inventory _inventory;
        private GameObject _shirtPrefab;
        private GameObject _hoodiePrefab;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _inventory = _playerObject.AddComponent<Inventory>();
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];

            _shirtPrefab = new GameObject("ShirtPrefab");
            _shirtPrefab.AddComponent<TShirt>();
            _shirtPrefab.SetActive(false);

            _hoodiePrefab = new GameObject("HoodiePrefab");
            _hoodiePrefab.AddComponent<Hoodie>();
            _hoodiePrefab.SetActive(false);

            _inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.TShirt, Prefab = _shirtPrefab },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.Hoodie, Prefab = _hoodiePrefab },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_shirtPrefab);
            Object.DestroyImmediate(_hoodiePrefab);
        }

        [UnityTest]
        public IEnumerator OnUnequipDestroysEveryPieceAndTheRoot()
        {
            var itemObject = new GameObject("Shirt");
            var shirt = itemObject.AddComponent<TShirt>();
            var pieceA = new GameObject("TorsoPiece").transform;
            var pieceB = new GameObject("SleevePiece").transform;
            shirt.Pieces = new[]
            {
                new ClothingItem.BonePiece { Piece = pieceA },
                new ClothingItem.BonePiece { Piece = pieceB },
            };

            shirt.OnUnequip(_playerObject);
            yield return null; // Destroy() is deferred to end of frame

            Assert.IsTrue(pieceA == null, "a piece transform must not survive OnUnequip");
            Assert.IsTrue(pieceB == null, "a piece transform must not survive OnUnequip");
            Assert.IsTrue(itemObject == null, "the root must not survive OnUnequip either");
        }

        [UnityTest]
        public IEnumerator EquippingAHoodieOverATShirtEvictsTheShirtBackToStorage()
        {
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.TShirt, _playerObject));
            yield return null;
            Assert.AreEqual(ItemRegistry.TShirt, _inventory.EquippedItemId("top"));

            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.Hoodie, _playerObject));
            yield return null;

            Assert.AreEqual(ItemRegistry.Hoodie, _inventory.EquippedItemId("top"));
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.TShirt), "the evicted shirt should have returned to storage");
        }
    }
}
