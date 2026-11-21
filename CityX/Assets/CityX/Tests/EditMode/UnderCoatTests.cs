using System.Collections.Generic;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A shirt under a coat is drawn only in the coat's open front (UnderCoat; owner, 2026-11-21: "im seeing the shirt clipping through around the
    /// shoulders. make the shirt sleeves invisible underneath the parka").
    /// </summary>
    public sealed class UnderCoatTests
    {
        [Test]
        public void OnlyTheTrianglesInTheOpenFrontStay()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-0.05f, 1.5f, 0.2f), new Vector3(0.05f, 1.5f, 0.2f), new Vector3(0.0f, 1.6f, 0.2f),     // the chest, in the opening
                new Vector3(0.30f, 1.9f, 0.05f), new Vector3(0.40f, 1.9f, 0.05f), new Vector3(0.35f, 1.95f, 0.0f),  // the top of a shoulder
                new Vector3(-0.05f, 1.5f, -0.2f), new Vector3(0.05f, 1.5f, -0.2f), new Vector3(0.0f, 1.6f, -0.2f),  // the back
            };
            List<int> kept = UnderCoat.InTheOpening(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, vertices);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, kept, "a shoulder or the back is still drawn under the coat, or the front is not");
        }

        [Test]
        public void ShirtsAreTheTopsThatAreNotCoats()
        {
            Assert.IsTrue(UnderCoat.IsShirt(ItemRegistry.LongSleeveTShirt));
            Assert.IsTrue(UnderCoat.IsShirt(ItemRegistry.RedStripedTShirt));
            Assert.IsFalse(UnderCoat.IsShirt(ItemRegistry.GreenFurHoodedParka));
            Assert.IsFalse(UnderCoat.IsShirt(ItemRegistry.BlackJeans));
        }

        /// <summary>Under a coat, nothing of a shirt is drawn round the shoulders or on the arms, and its chest still is.</summary>
        [TestCase("LongSleeveTShirt")]
        [TestCase("TShirt")]
        public void AShirtUnderACoatShowsItsChestAndNothingRoundTheShoulders(string prefab)
        {
            Mesh shirt = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Mesh under = UnderCoat.FrontOnly(shirt);
            Assert.Greater(under.triangles.Length, 0, "nothing of the shirt shows in the coat's open front");
            Assert.Less(under.triangles.Length, shirt.triangles.Length / 2, "most of the shirt is still drawn under the coat");
            Vector3[] vertices = shirt.vertices;
            foreach (int v in under.triangles)
            {
                Assert.Less(Mathf.Abs(vertices[v].x), Character.BodyRig.ShoulderX, "a point by the shoulders or on an arm is still drawn under the coat");
            }
            Assert.AreSame(under, UnderCoat.FrontOnly(shirt), "the shirt as it shows under a coat is made again every time");
        }
    }
}
