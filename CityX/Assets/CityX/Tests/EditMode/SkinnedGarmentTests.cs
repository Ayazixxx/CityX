using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    public sealed class SkinnedGarmentTests
    {
        [Test]
        public void SkelliJeansAndHoodedCloakReportTheRegistryIdsTheyWereBuiltFor()
        {
            var jeansObject = new GameObject("Jeans");
            var cloakObject = new GameObject("Cloak");
            try
            {
                Assert.AreEqual(ItemRegistry.SkelliJeans, jeansObject.AddComponent<SkelliJeans>().ItemId);
                Assert.AreEqual(ItemRegistry.HoodedCloak, cloakObject.AddComponent<HoodedCloak>().ItemId);
            }
            finally
            {
                Object.DestroyImmediate(jeansObject);
                Object.DestroyImmediate(cloakObject);
            }
        }

        [Test]
        public void EquippingWithNoAnimatorOnThePlayerDoesNotThrow()
        {
            var jeansObject = new GameObject("Jeans");
            var player = new GameObject("PlayerWithNoAnimator");
            try
            {
                var jeans = jeansObject.AddComponent<SkelliJeans>();
                jeans.GarmentMesh = new Mesh();
                Assert.DoesNotThrow(() => jeans.OnEquip(player, ItemInstanceData.None));
            }
            finally
            {
                Object.DestroyImmediate(jeansObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void EquippingWithAnAnimatorThatHasNoAvatarDoesNotThrow()
        {
            // Animator.GetBoneTransform THROWS rather than returning null
            // when the animator has no avatar at all -- unlike a missing
            // individual bone, which it tolerates -- so this has to be
            // caught before ever calling it, not just null-checked after.
            var jeansObject = new GameObject("Jeans");
            var player = new GameObject("PlayerWithBareAnimator");
            player.AddComponent<Animator>();
            try
            {
                var jeans = jeansObject.AddComponent<SkelliJeans>();
                jeans.GarmentMesh = new Mesh();
                Assert.DoesNotThrow(() => jeans.OnEquip(player, ItemInstanceData.None));
            }
            finally
            {
                Object.DestroyImmediate(jeansObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void EquippingWithNoGarmentMeshDoesNotThrow()
        {
            var cloakObject = new GameObject("Cloak");
            var player = new GameObject("Player");
            player.AddComponent<Animator>();
            try
            {
                var cloak = cloakObject.AddComponent<HoodedCloak>();
                Assert.DoesNotThrow(() => cloak.OnEquip(player, ItemInstanceData.None));
            }
            finally
            {
                Object.DestroyImmediate(cloakObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void BoneOrderMatchesTheFourteenNamedBonesTheGlbExportsAgree()
        {
            // Bind order is load-bearing -- BoneWeight.boneIndexN indexes
            // straight into this array, so it has to match exactly what
            // skelli_jeans.glb/hooded_cloak.glb both export (confirmed by
            // inspecting the imported SkinnedMeshRenderer.bones names).
            Assert.AreEqual(14, SkinnedGarment.Bones.Length);
            Assert.AreEqual(HumanBodyBones.Hips, SkinnedGarment.Bones[0]);
            Assert.AreEqual(HumanBodyBones.LeftLowerLeg, SkinnedGarment.Bones[11]);
            Assert.AreEqual(HumanBodyBones.RightLowerLeg, SkinnedGarment.Bones[13]);
        }
    }
}
