using CityX.Items;
using CityX.Wearables;
using CityX.WearablesEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The black shirts (owner, 2026-10-22: "make a black version of the T-Shirt", then "make a longsleeve version of the tshirt both black and
    /// white"): each its own item, the white shirt's mesh and fit, in black cotton.
    /// </summary>
    [TestFixture(TShirtBuilder.PrefabPath, TShirtBuilder.BlackPrefabPath, ItemRegistry.BlackTShirt, "Black T-Shirt")]
    [TestFixture(TShirtBuilder.LongSleevePrefabPath, TShirtBuilder.BlackLongSleevePrefabPath, ItemRegistry.BlackLongSleeveTShirt,
        "Black Long Sleeve T-Shirt")]
    public sealed class BlackTShirtTests
    {
        private readonly string _whitePrefab;
        private readonly string _blackPrefab;
        private readonly string _itemId;
        private readonly string _displayName;

        public BlackTShirtTests(string whitePrefab, string blackPrefab, string itemId, string displayName)
        {
            _whitePrefab = whitePrefab;
            _blackPrefab = blackPrefab;
            _itemId = itemId;
            _displayName = displayName;
        }

        [Test]
        public void TheBlackShirtIsATop()
        {
            Assert.IsTrue(ItemRegistry.TryGet(_itemId, out ItemDefinition shirt));
            Assert.AreEqual(_displayName, shirt.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, shirt.EquipSlot);
            Assert.IsTrue(shirt.IsWearable);
        }

        [Test]
        public void ItIsTheWhiteShirtInBlackCotton()
        {
            TShirt white = Prefab(_whitePrefab);
            TShirt black = Prefab(_blackPrefab);
            Assert.AreEqual(_itemId, black.ItemId);
            Assert.AreSame(white.GarmentMesh, black.GarmentMesh, "the black shirt is not the white one's own mesh");
            CollectionAssert.AreEqual(white.HiddenBodyTriangles, black.HiddenBodyTriangles, "the black shirt hides the body differently");
            Color colour = black.GarmentMaterial.GetColor("_BaseColor");
            Assert.Less(Mathf.Max(colour.r, colour.g, colour.b), 0.1f, "the shirt is not black");
            Assert.Greater(colour.r, 0.0f, "pure black hides the shirt's folds");
            Assert.AreNotSame(white.GarmentMaterial, black.GarmentMaterial, "the black shirt dyed the white one");
        }

        private static TShirt Prefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"no {path}: rebuild the scene (CityMassingSceneBuilder)");
            return prefab.GetComponent<TShirt>();
        }
    }
}
