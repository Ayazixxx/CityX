using System.Collections;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Real-frame behavior for the farmer hat -- mirrors TopHatPlayTests
    /// exactly, since FarmerHat mirrors TopHat's own shape exactly (see
    /// FarmerHat's own class comment). Bone-attachment fidelity against a
    /// real Humanoid avatar and the imported mesh's own fit are verified
    /// visually instead of here, for the same reason TopHatPlayTests'
    /// own class comment gives.
    /// </summary>
    public sealed class FarmerHatPlayTests
    {
        private GameObject _hatObject;
        private FarmerHat _hat;

        [SetUp]
        public void SetUp()
        {
            _hatObject = new GameObject("FarmerHat");
            _hat = _hatObject.AddComponent<FarmerHat>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hatObject != null)
            {
                Object.DestroyImmediate(_hatObject);
            }
        }

        [Test]
        public void OnEquipWithNoAnimatorAtAllDoesNotThrowAndDoesNotAttach()
        {
            var player = new GameObject("PlayerWithNoAnimator");

            Assert.DoesNotThrow(() => _hat.OnEquip(player, ItemInstanceData.None));
            Assert.IsNull(_hatObject.transform.parent, "nothing to attach to means the hat should stay put");

            Object.DestroyImmediate(player);
        }

        [Test]
        public void OnEquipWithABareAnimatorAndNoAvatarDoesNotThrowAndDoesNotAttach()
        {
            var player = new GameObject("PlayerWithBareAnimator");
            player.AddComponent<Animator>();

            Assert.DoesNotThrow(() => _hat.OnEquip(player, ItemInstanceData.None));
            Assert.IsNull(_hatObject.transform.parent, "no avatar means no head bone to bind to");

            Object.DestroyImmediate(player);
        }

        [Test]
        public void OnEquipDisablesThePickupTrigger()
        {
            var player = new GameObject("Player");
            var trigger = _hatObject.GetComponent<SphereCollider>();

            _hat.OnEquip(player, ItemInstanceData.None);

            Assert.IsFalse(trigger.enabled, "a worn hat should no longer be pickupable");

            Object.DestroyImmediate(player);
        }

        [UnityTest]
        public IEnumerator OnUnequipDestroysTheRoot()
        {
            var player = new GameObject("Player");
            _hat.OnEquip(player, ItemInstanceData.None);

            _hat.OnUnequip(player);
            yield return null; // Destroy() is deferred to end of frame

            Assert.IsTrue(_hatObject == null, "the root must not survive OnUnequip");
            _hatObject = null; // already gone -- TearDown must not double-destroy it

            Object.DestroyImmediate(player);
        }

        [Test]
        public void GetInstanceDataIsAlwaysNone()
        {
            Assert.IsTrue(_hat.GetInstanceData().IsEmpty, "plain clothing carries no per-instance state");
        }
    }
}
