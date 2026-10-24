using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    public sealed class SkinnedGarmentTests
    {
        [Test]
        public void TheHoodedCloakReportsTheRegistryIdItWasBuiltFor()
        {
            var cloakObject = new GameObject("Cloak");
            try
            {
                Assert.AreEqual(ItemRegistry.HoodedCloak, cloakObject.AddComponent<HoodedCloak>().ItemId);
            }
            finally
            {
                Object.DestroyImmediate(cloakObject);
            }
        }

        [Test]
        public void EquippingWithNoAnimatorOnThePlayerDoesNotThrow()
        {
            var cloakObject = new GameObject("Cloak");
            var player = new GameObject("PlayerWithNoAnimator");
            try
            {
                var cloak = cloakObject.AddComponent<HoodedCloak>();
                cloak.GarmentMesh = new Mesh();
                Assert.DoesNotThrow(() => cloak.OnEquip(player, ItemInstanceData.None));
            }
            finally
            {
                Object.DestroyImmediate(cloakObject);
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
            var cloakObject = new GameObject("Cloak");
            var player = new GameObject("PlayerWithBareAnimator");
            player.AddComponent<Animator>();
            try
            {
                var cloak = cloakObject.AddComponent<HoodedCloak>();
                cloak.GarmentMesh = new Mesh();
                Assert.DoesNotThrow(() => cloak.OnEquip(player, ItemInstanceData.None));
            }
            finally
            {
                Object.DestroyImmediate(cloakObject);
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
        public void BoneOrderMatchesTheFourteenNamedBonesTheGlbExports()
        {
            // Bind order is load-bearing -- BoneWeight.boneIndexN indexes
            // straight into this array, so it has to match exactly what
            // hooded_cloak.glb exports (confirmed by inspecting the imported
            // SkinnedMeshRenderer.bones names).
            Assert.AreEqual(14, SkinnedGarment.Bones.Length);
            Assert.AreEqual(HumanBodyBones.Hips, SkinnedGarment.Bones[0]);
            Assert.AreEqual(HumanBodyBones.LeftLowerLeg, SkinnedGarment.Bones[11]);
            Assert.AreEqual(HumanBodyBones.RightLowerLeg, SkinnedGarment.Bones[13]);
        }
    }
}
