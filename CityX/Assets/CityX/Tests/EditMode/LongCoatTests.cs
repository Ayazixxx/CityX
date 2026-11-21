using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The long coats (the Midnight Coat, the owner's own fit, 2026-10-19; the Green and Maroon Fur Hooded Parkas, 2026-11-20/21): each a top in the
    /// registry, within the long-coat budget with one small map, skinned only to the bones a coat covers, and worn on this body (shoulders
    /// to below the hips, sleeves out along the arms).
    /// </summary>
    [TestFixture("MidnightCoat", ItemRegistry.MidnightCoat, "Midnight Coat", 512)]
    [TestFixture("GreenFurHoodedParka", ItemRegistry.GreenFurHoodedParka, "Green Fur Hooded Parka", 1024)]
    [TestFixture("MaroonFurHoodedParka", ItemRegistry.MaroonFurHoodedParka, "Maroon Fur Hooded Parka", 1024)]
    public sealed class LongCoatTests
    {
        private const int TriangleBudget = 15000;

        /// <summary>How far below the hip joints the skirt bones have taken the coat over entirely (garment_rig.hang_skirt).</summary>
        private const float SkirtBelowHips = 0.2f;

        private readonly string _prefab;
        private readonly string _itemId;
        private readonly string _name;
        private readonly int _mapSizeLimit;

        public LongCoatTests(string prefab, string itemId, string name, int mapSizeLimit)
        {
            _prefab = prefab;
            _itemId = itemId;
            _name = name;
            _mapSizeLimit = mapSizeLimit;
        }

        private SkinnedGarment Coat()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{_prefab}.prefab");
            Assert.IsNotNull(prefab, $"no {_prefab}.prefab: rebuild the scene (CityMassingSceneBuilder)");
            var coat = prefab.GetComponent<SkinnedGarment>();
            Assert.IsNotNull(coat.GarmentMesh, "the coat has no mesh");
            return coat;
        }

        [Test]
        public void TheCoatIsATopWithItsOwnNameAndABlueCard()
        {
            Assert.IsTrue(ItemRegistry.TryGet(_itemId, out ItemDefinition coat));
            Assert.AreEqual(_name, coat.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, coat.EquipSlot);
            Assert.AreEqual(_itemId, Coat().ItemId);
            Assert.AreEqual(Rarity.Rare, ItemRarity.Of(_itemId));
        }

        [Test]
        public void TheCoatIsWithinItsBudgetAndWearsOneSmallMap()
        {
            SkinnedGarment coat = Coat();
            Assert.LessOrEqual(coat.GarmentMesh.triangles.Length / 3, TriangleBudget);
            Assert.AreEqual(coat.GarmentMesh.vertexCount, coat.GarmentMesh.uv.Length, "the coat has no texture coordinates");
            Texture map = coat.GarmentMaterial.GetTexture("_BaseMap");
            Assert.IsNotNull(map, "the coat has no texture");
            Assert.LessOrEqual(map.width, _mapSizeLimit, "the coat's texture is too big for many players at once");
        }

        [Test]
        public void TheCoatFollowsOnlyWhatACoatCoversAndEveryWeightAddsUp()
        {
            Mesh mesh = Coat().GarmentMesh;
            Assert.AreEqual(SkinnedGarment.Bones.Length + GarmentHelperBones.Helpers.Length, mesh.bindposes.Length,
                "the coat is not bound to the body's bones and the helper bones (skirt, shoulders, collar)");
            int[] never =
            {
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.Head),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.LeftHand),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.RightHand),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.LeftLowerLeg),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.RightLowerLeg),
            };
            foreach (BoneWeight w in mesh.boneWeights)
            {
                Assert.AreEqual(1.0f, w.weight0 + w.weight1 + w.weight2 + w.weight3, 0.01f);
                foreach ((int bone, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (weight > 0.001f)
                    {
                        CollectionAssert.DoesNotContain(never, bone, "part of the coat follows the head, a hand or a shin");
                    }
                }
            }
        }

        [Test]
        public void TheSkirtHangsFromTheSkirtBonesAndTheCoatHidesTheBodyItCovers()
        {
            SkinnedGarment coat = Coat();
            // Below the hips, where a knee would come through it, the coat hangs from the skirt bones.
            int firstHelper = SkinnedGarment.Bones.Length;
            int below = 0;
            int onSkirt = 0;
            Vector3[] points = coat.GarmentMesh.vertices;
            BoneWeight[] weights = coat.GarmentMesh.boneWeights;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].y > BodyRig.HipY - SkirtBelowHips)
                {
                    continue;
                }
                below++;
                if (weights[i].boneIndex0 >= firstHelper && weights[i].boneIndex0 < firstHelper + 4)
                {
                    onSkirt++;
                }
            }
            Assert.Greater(below, 0, "nothing of the coat hangs below the hips");
            Assert.Greater(onSkirt, below * 9 / 10, $"only {onSkirt} of the coat's {below} points below the hips hang from the skirt bones");
            Assert.IsNotNull(coat.HiddenBodyTriangles, "the coat hides none of the body under it");
            Assert.AreEqual((coat.HiddenFromBodyTriangles + 7) / 8, coat.HiddenBodyTriangles.Length);
        }

        [Test]
        public void TheCoatHangsFromTheShouldersToBelowTheHipsWithSleevesToTheWrists()
        {
            Bounds bounds = Coat().GarmentMesh.bounds;
            Assert.Greater(bounds.max.y, BodyRig.ShoulderY, "the coat does not reach the shoulders");
            Assert.Less(bounds.max.y, BodyRig.HeadCentreY + 0.2f, "the coat's collar climbs over the head");
            Assert.Less(bounds.min.y, BodyRig.HipY - 0.3f, "the coat does not hang below the hips");
            Assert.Greater(bounds.min.y, 0.15f, "the coat drags on the ground");
            Assert.Greater(bounds.max.x, BodyRig.ShoulderX + 0.15f, "no sleeve reaches out along the arm");
            Assert.Less(bounds.max.x, 0.8f, "the coat sticks out far past the hands");
        }
    }
}
