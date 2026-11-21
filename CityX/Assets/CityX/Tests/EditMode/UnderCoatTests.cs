using System.Collections.Generic;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A shirt under a coat is drawn without its sleeves (UnderCoat; owner, 2026-11-21: "make it so i can wear a shirt underneath the parka"): the
    /// long-sleeved shirt's came out through the parka's upper sleeves. Its body stays, for the coat's open front.
    /// </summary>
    public sealed class UnderCoatTests
    {
        private static BoneWeight On(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1.0f };

        [Test]
        public void ATriangleWholeOnAnArmGoesAndOneTouchingTheBodyStays()
        {
            HashSet<int> arms = UnderCoat.ArmBones();
            int spine = System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.Spine);
            int forearm = System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.LeftLowerArm);
            Assert.IsTrue(arms.Contains(forearm) && !arms.Contains(spine));
            var weights = new List<BoneWeight> { On(forearm), On(forearm), On(forearm), On(spine) };
            List<int> kept = UnderCoat.WithoutSleeves(new[] { 0, 1, 2, 0, 1, 3 }, weights, arms);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, kept, "the sleeve's triangle stayed, or the one joining the body went");
        }

        [Test]
        public void ShirtsAreTheTopsThatAreNotCoats()
        {
            Assert.IsTrue(UnderCoat.IsShirt(ItemRegistry.LongSleeveTShirt));
            Assert.IsTrue(UnderCoat.IsShirt(ItemRegistry.TShirt));
            Assert.IsFalse(UnderCoat.IsShirt(ItemRegistry.GreenFurHoodedParka));
            Assert.IsFalse(UnderCoat.IsShirt(ItemRegistry.BlackJeans));
        }

        [TestCase("LongSleeveTShirt")]
        [TestCase("TShirt")]
        public void AShirtUnderACoatKeepsItsBodyAndLosesItsSleeves(string prefab)
        {
            Mesh shirt = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Mesh under = UnderCoat.SleevesOff(shirt);
            int all = shirt.triangles.Length / 3, kept = under.triangles.Length / 3;
            Assert.Less(kept, all, "nothing of the shirt's sleeves went");
            Assert.Greater(kept, all / 4, "the shirt's body went with its sleeves");
            HashSet<int> arms = UnderCoat.ArmBones();
            BoneWeight[] weights = shirt.boneWeights;
            int[] triangles = under.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                bool sleeve = UnderCoat.OnArm(weights[triangles[t]], arms) >= UnderCoat.SleeveWeight &&
                              UnderCoat.OnArm(weights[triangles[t + 1]], arms) >= UnderCoat.SleeveWeight &&
                              UnderCoat.OnArm(weights[triangles[t + 2]], arms) >= UnderCoat.SleeveWeight;
                Assert.IsFalse(sleeve, "a sleeve's triangle is still drawn under the coat");
            }
            Assert.AreSame(under, UnderCoat.SleevesOff(shirt), "the sleeveless shirt is made again every time");
        }
    }
}
