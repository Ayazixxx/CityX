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

        /// <summary>
        /// Under the Brown Coat a shirt hugs the body from the chest down (owner, 2026-11-24: "make the t shirt fit a bit tigher when brown coat is
        /// equipped so it doesnt overlap like that", "the top part looks good its just the stomach"); under the parka it is left as it is.
        /// </summary>
        [TestCase("LongSleeveTShirt")]
        [TestCase("TShirt")]
        public void UnderTheBrownCoatAShirtsStomachIsDrawnInAndItsChestIsNot(string prefab)
        {
            Assert.IsTrue(UnderCoat.FitsClose(ItemRegistry.BrownCoat));
            Assert.IsFalse(UnderCoat.FitsClose(ItemRegistry.GreenFurHoodedParka), "the parka's shirt is drawn in too");
            Mesh shirt = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Mesh coat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BrownCoat.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Mesh close = UnderCoat.FrontOnlyClose(shirt, coat);
            Assert.Greater(close.triangles.Length, 0, "nothing of the shirt shows in the coat's gap");
            Assert.Less(close.triangles.Length, UnderCoat.FrontOnly(shirt).triangles.Length, "the shirt is still drawn under the coat's panels");
            HashSet<(int, int)> covered = UnderCoat.CoveredInFront(coat);
            float tuck = UnderCoat.CoverCell * (UnderCoat.TuckCells + 1.5f) * 1.5f;
            Vector3[] own = shirt.vertices, drawn = close.vertices;
            float loosest = 0.0f;
            int moved = 0;
            foreach (int v in close.triangles)
            {
                float reach = new Vector2(drawn[v].x, drawn[v].z).magnitude;
                if (drawn[v].y < UnderCoat.CloseBelow)
                {
                    loosest = Mathf.Max(loosest, reach);
                }
                if (drawn[v].y > UnderCoat.OwnShapeFrom)
                {
                    Assert.AreEqual(own[v], drawn[v], "the chest, which looked good, was moved");
                }
                if ((own[v] - drawn[v]).sqrMagnitude > 1e-8f) moved++;
            }
            TestContext.WriteLine($"{prefab}: {moved} corners drawn in, the stomach now {loosest * 100.0f:0.0} cm from the middle line at the most");
            Assert.Greater(moved, 0, "nothing of the shirt was drawn in");
            Assert.LessOrEqual(loosest, UnderCoat.TrunkRadius + UnderCoat.CloseOffTheTrunk + 1e-4f, "the stomach still hangs off the body");
            Assert.AreSame(close, UnderCoat.FrontOnlyClose(shirt, coat));
            Assert.Greater(covered.Count, 1000, "the coat covers nothing of the front");
            Assert.Greater(tuck, 0.0f);
        }
    }
}
