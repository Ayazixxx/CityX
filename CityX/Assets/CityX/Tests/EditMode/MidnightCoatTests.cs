using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Midnight Coat (the owner's own fit, 2026-10-19): a top in the registry, within the long-coat budget with one small map, skinned only to
    /// the bones a coat covers, and worn on this body (shoulders to below the hips, sleeves out along the arms).
    /// </summary>
    public sealed class MidnightCoatTests
    {
        private const int TriangleBudget = 15000;
        private const int MapSizeLimit = 512;

        private static SkinnedGarment Coat()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/MidnightCoat.prefab");
            Assert.IsNotNull(prefab, "no MidnightCoat.prefab: rebuild the scene (CityMassingSceneBuilder)");
            var coat = prefab.GetComponent<MidnightCoat>();
            Assert.IsNotNull(coat.GarmentMesh, "the coat has no mesh");
            return coat;
        }

        [Test]
        public void TheMidnightCoatIsATopCalledMidnightCoat()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.MidnightCoat, out ItemDefinition coat));
            Assert.AreEqual("Midnight Coat", coat.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, coat.EquipSlot);
            Assert.AreEqual(ItemRegistry.MidnightCoat, Coat().ItemId);
        }

        [Test]
        public void TheCoatIsWithinItsBudgetAndWearsOneSmallMap()
        {
            SkinnedGarment coat = Coat();
            Assert.LessOrEqual(coat.GarmentMesh.triangles.Length / 3, TriangleBudget);
            Assert.AreEqual(coat.GarmentMesh.vertexCount, coat.GarmentMesh.uv.Length, "the coat has no texture coordinates");
            Texture map = coat.GarmentMaterial.GetTexture("_BaseMap");
            Assert.IsNotNull(map, "the coat has no texture");
            Assert.LessOrEqual(map.width, MapSizeLimit, "the coat's texture is too big for many players at once");
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
            int firstHelper = SkinnedGarment.Bones.Length;
            int onSkirt = 0;
            foreach (BoneWeight w in coat.GarmentMesh.boneWeights)
            {
                if (w.boneIndex0 >= firstHelper && w.boneIndex0 < firstHelper + 4)
                {
                    onSkirt++;
                }
            }
            Assert.Greater(onSkirt, coat.GarmentMesh.vertexCount / 5, "the skirt does not hang from the skirt bones");
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
