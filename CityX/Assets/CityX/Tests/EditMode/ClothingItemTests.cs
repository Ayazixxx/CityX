using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    public sealed class ClothingItemTests
    {
        [Test]
        public void TShirtAndHoodieReportTheRegistryIdsTheyWereBuiltFor()
        {
            var shirtObject = new GameObject("Shirt");
            var hoodieObject = new GameObject("Hoodie");
            try
            {
                Assert.AreEqual(ItemRegistry.TShirt, shirtObject.AddComponent<TShirt>().ItemId);
                Assert.AreEqual(ItemRegistry.Hoodie, hoodieObject.AddComponent<Hoodie>().ItemId);
            }
            finally
            {
                Object.DestroyImmediate(shirtObject);
                Object.DestroyImmediate(hoodieObject);
            }
        }

        [Test]
        public void EquippingWithNoAnimatorOnThePlayerDoesNotThrow()
        {
            var shirtObject = new GameObject("Shirt");
            var player = new GameObject("PlayerWithNoAnimator");
            try
            {
                var shirt = shirtObject.AddComponent<TShirt>();
                Assert.DoesNotThrow(() => shirt.OnEquip(player, ItemInstanceData.None));
            }
            finally
            {
                Object.DestroyImmediate(shirtObject);
                Object.DestroyImmediate(player);
            }
        }
    }
}
