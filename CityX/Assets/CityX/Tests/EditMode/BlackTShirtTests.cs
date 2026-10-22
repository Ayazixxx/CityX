using CityX.Items;
using CityX.Wearables;
using CityX.WearablesEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The Black T-Shirt (owner, 2026-10-22: "make a black version of the T-Shirt"): its own item, the T-Shirt's mesh and fit, black cotton.</summary>
    public sealed class BlackTShirtTests
    {
        [Test]
        public void TheBlackTShirtIsATop()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.BlackTShirt, out ItemDefinition shirt));
            Assert.AreEqual("Black T-Shirt", shirt.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, shirt.EquipSlot);
            Assert.IsTrue(shirt.IsWearable);
        }

        [Test]
        public void ItIsTheTShirtInBlackCotton()
        {
            TShirt plain = Prefab(TShirtBuilder.PrefabPath);
            TShirt black = Prefab(TShirtBuilder.BlackPrefabPath);
            Assert.AreEqual(ItemRegistry.BlackTShirt, black.ItemId);
            Assert.AreSame(plain.GarmentMesh, black.GarmentMesh, "the black shirt is not the T-Shirt's own mesh");
            CollectionAssert.AreEqual(plain.HiddenBodyTriangles, black.HiddenBodyTriangles, "the black shirt hides the body differently");
            Color colour = black.GarmentMaterial.GetColor("_BaseColor");
            Assert.Less(Mathf.Max(colour.r, colour.g, colour.b), 0.1f, "the shirt is not black");
            Assert.Greater(colour.r, 0.0f, "pure black hides the shirt's folds");
            Assert.AreNotSame(plain.GarmentMaterial, black.GarmentMaterial, "the black shirt dyed the white one");
        }

        private static TShirt Prefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"no {path}: rebuild the scene (CityMassingSceneBuilder)");
            return prefab.GetComponent<TShirt>();
        }
    }
}
