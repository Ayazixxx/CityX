using System.Collections;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Real-frame behavior for the top hat -- Destroy() defers to end of
    /// frame, so verifying it needs a real yielded frame, same reasoning as
    /// SkinnedGarmentPlayTests' own class comment. Bone-attachment fidelity
    /// against a real Humanoid avatar is verified visually instead of here,
    /// for the same "out of reach for a play-mode test assembly" reason
    /// that comment gives -- building one needs the real editor-time rig
    /// builder.
    /// </summary>
    public sealed class TopHatPlayTests
    {
        private GameObject _hatObject;
        private TopHat _hat;

        [SetUp]
        public void SetUp()
        {
            _hatObject = new GameObject("TopHat");
            _hat = _hatObject.AddComponent<TopHat>();
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

        /// <summary>
        /// GetBoneTransform THROWS rather than returning null when the
        /// Animator has no avatar at all (as opposed to an avatar that
        /// simply lacks one particular bone, which it tolerates) -- see
        /// TopHat.OnEquip's own doc comment for where this came from.
        /// </summary>
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
