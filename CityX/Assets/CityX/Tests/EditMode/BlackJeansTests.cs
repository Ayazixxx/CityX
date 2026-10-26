using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Black Jeans (owner, 2026-10-24: "add them as "Black Jeans" and try to fit it to the player the best you can"): worn on the legs,
    /// within the trousers' budget with one small map, bending at the knees (the shins keep their own share), following nothing above the
    /// waist, and worn on this body from above the hips down to the ankles.
    /// </summary>
    public sealed class BlackJeansTests
    {
        private const int TriangleBudget = 12000;
        private const int MapSizeLimit = 1024;

        private static SkinnedGarment Jeans()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BlackJeans.prefab");
            Assert.IsNotNull(prefab, "no BlackJeans.prefab: rebuild the scene (CityMassingSceneBuilder)");
            var jeans = prefab.GetComponent<CityX.Wearables.Jeans>();
            Assert.IsNotNull(jeans.GarmentMesh, "the jeans have no mesh");
            return jeans;
        }

        /// <summary>The other colours (owner, 2026-10-25): the same pair, each its own item, name and texture.</summary>
        [TestCase("BlueJeans", ItemRegistry.BlueJeans, "Blue Jeans")]
        [TestCase("BredJeans", ItemRegistry.BredJeans, "Bred Jeans")]
        [TestCase("MaroonJeans", ItemRegistry.MaroonJeans, "Maroon Jeans")]
        [TestCase("Khakis", ItemRegistry.Khakis, "Khakis")]
        public void EveryColourIsTheSamePairWithItsOwnItemAndTexture(string prefabName, string itemId, string displayName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefabName}.prefab");
            Assert.IsNotNull(prefab, $"no {prefabName}.prefab: rebuild the scene (CityMassingSceneBuilder)");
            var pair = prefab.GetComponent<CityX.Wearables.Jeans>();
            Assert.AreEqual(itemId, pair.ItemId);
            Assert.IsTrue(ItemRegistry.TryGet(itemId, out ItemDefinition item));
            Assert.AreEqual(displayName, item.DisplayName);
            Assert.AreEqual(EquipSlots.Legs, item.EquipSlot);
            SkinnedGarment black = Jeans();
            Assert.AreSame(black.GarmentMesh, pair.GarmentMesh, "each colour should share the one mesh");
            Texture map = pair.GarmentMaterial.GetTexture("_BaseMap");
            Assert.IsNotNull(map);
            Assert.AreNotSame(black.GarmentMaterial.GetTexture("_BaseMap"), map, "the colour wears the black pair's texture");
            Assert.LessOrEqual(map.width, MapSizeLimit);
            Assert.AreEqual(black.HiddenBodyTriangles, pair.HiddenBodyTriangles, "each colour should hide the same body");
        }

        private static int Index(HumanBodyBones bone) => System.Array.IndexOf(SkinnedGarment.Bones, bone);

        [Test]
        public void TheBlackJeansAreWornOnTheLegs()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.BlackJeans, out ItemDefinition jeans));
            Assert.AreEqual("Black Jeans", jeans.DisplayName);
            Assert.AreEqual(EquipSlots.Legs, jeans.EquipSlot);
            Assert.AreEqual(ItemDefinition.WearableCategory, jeans.Category);
            Assert.AreEqual(ItemRegistry.BlackJeans, Jeans().ItemId);
        }

        [Test]
        public void TheJeansAreWithinTheirBudgetAndWearOneSmallMap()
        {
            SkinnedGarment jeans = Jeans();
            Assert.LessOrEqual(jeans.GarmentMesh.triangles.Length / 3, TriangleBudget);
            Assert.AreEqual(jeans.GarmentMesh.vertexCount, jeans.GarmentMesh.uv.Length, "the jeans have no texture coordinates");
            Texture map = jeans.GarmentMaterial.GetTexture("_BaseMap");
            Assert.IsNotNull(map, "the jeans have no texture");
            Assert.LessOrEqual(map.width, MapSizeLimit, "the jeans' texture is too big for many players at once");
        }

        [Test]
        public void TheJeansBendAtTheKneesAndFollowNothingAboveTheWaist()
        {
            Mesh mesh = Jeans().GarmentMesh;
            int[] never =
            {
                Index(HumanBodyBones.Neck), Index(HumanBodyBones.Head),
                Index(HumanBodyBones.LeftUpperArm), Index(HumanBodyBones.LeftLowerArm), Index(HumanBodyBones.LeftHand),
                Index(HumanBodyBones.RightUpperArm), Index(HumanBodyBones.RightLowerArm), Index(HumanBodyBones.RightHand),
            };
            int leftShin = Index(HumanBodyBones.LeftLowerLeg), rightShin = Index(HumanBodyBones.RightLowerLeg);
            int onLeftShin = 0, onRightShin = 0;
            foreach (BoneWeight w in mesh.boneWeights)
            {
                Assert.AreEqual(1.0f, w.weight0 + w.weight1 + w.weight2 + w.weight3, 0.01f);
                foreach ((int bone, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (weight > 0.001f)
                    {
                        CollectionAssert.DoesNotContain(never, bone, "part of the jeans follows an arm, a hand, the neck or the head");
                    }
                }
                onLeftShin += w.boneIndex0 == leftShin ? 1 : 0;
                onRightShin += w.boneIndex0 == rightShin ? 1 : 0;
            }
            Assert.Greater(onLeftShin, mesh.vertexCount / 10, "the left leg below the knee does not follow the shin");
            Assert.Greater(onRightShin, mesh.vertexCount / 10, "the right leg below the knee does not follow the shin");
        }

        [Test]
        public void TheJeansGoFromAboveTheHipsToTheAnklesAndHideTheLegsUnderThem()
        {
            SkinnedGarment jeans = Jeans();
            Bounds bounds = jeans.GarmentMesh.bounds;
            float knee = Mathf.Lerp(BodyRig.HipY, BodyRig.FootY, BodyRig.KneeT);
            Assert.Greater(bounds.max.y, BodyRig.HipY, "the waistband is below the hips");
            Assert.Less(bounds.max.y, BodyRig.HipY + 0.3f, "the jeans come up the chest");
            Assert.Less(bounds.min.y, knee - 0.25f, "the legs stop at the knees");
            Assert.Greater(bounds.min.y, 0.03f, "the hems drag on the ground");
            Assert.Less(bounds.max.x, BodyRig.HipX + BodyRig.LegRadius + 0.1f, "the jeans stand far out from the hips");
            Assert.IsNotNull(jeans.HiddenBodyTriangles, "the jeans hide none of the body under them");
            Assert.AreEqual((jeans.HiddenFromBodyTriangles + 7) / 8, jeans.HiddenBodyTriangles.Length);
        }

        /// <summary>
        /// Owner, 2026-10-26: "when pants and shoes are being worn you can make the feet underneath invisible". In shoes the jeans hide all
        /// they do bare-footed and the ankles under their hems too, which they leave drawn for bare feet.
        /// </summary>
        [Test]
        public void InShoesTheJeansAlsoHideTheAnklesUnderTheirHems()
        {
            SkinnedGarment jeans = Jeans();
            byte[] bare = jeans.HiddenBodyTriangles, shod = jeans.HiddenBodyTrianglesWithShoes;
            Assert.IsNotNull(shod, "the jeans hide nothing more in shoes");
            Assert.AreEqual(bare.Length, shod.Length);
            int more = 0;
            for (int t = 0; t < jeans.HiddenFromBodyTriangles; t++)
            {
                Assert.IsFalse(BodyCover.IsHidden(bare, t) && !BodyCover.IsHidden(shod, t), "in shoes the jeans show skin they hide bare-footed");
                more += !BodyCover.IsHidden(bare, t) && BodyCover.IsHidden(shod, t) ? 1 : 0;
            }
            Assert.Greater(more, 0, "in shoes the jeans still leave the ankles under their hems drawn");
        }
    }
}
