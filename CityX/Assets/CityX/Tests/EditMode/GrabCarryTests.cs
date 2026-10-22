using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The carry as Skyrim has it: lifted once E is held, carried while it stays down, dropped when it comes up, thrown on a click.</summary>
    public sealed class GrabCarryTests
    {
        private GameObject _playerObject;
        private GameObject _eyeObject;
        private GrabCarry _grab;

        /// <summary>An item lying in the world as a still prop, as most do: a trigger to be picked up by, a model, no physics.</summary>
        private sealed class LyingItem : MonoBehaviour, IEquippableItem
        {
            public void OnEquip(GameObject player, ItemInstanceData instanceData)
            {
            }

            public void OnUnequip(GameObject player)
            {
            }

            public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
        }

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _grab = _playerObject.AddComponent<GrabCarry>();

            _eyeObject = new GameObject("Eye");
            _eyeObject.transform.position = Vector3.zero;
            _eyeObject.transform.rotation = Quaternion.identity; // forward = +Z
            _grab.Eye = _eyeObject.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_eyeObject);
        }

        private GameObject MakeGrabbableBody(Vector3 position, bool kinematic = false, bool grabbableTag = true)
        {
            var go = new GameObject("Body");
            go.transform.position = position;
            go.AddComponent<SphereCollider>();
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = kinematic;
            if (grabbableTag)
            {
                go.AddComponent<Grabbable>();
            }
            Physics.SyncTransforms();
            return go;
        }

        private static GameObject MakeLyingItem(Vector3 position)
        {
            var item = new GameObject("Item");
            item.transform.position = position;
            item.AddComponent<LyingItem>();
            item.AddComponent<SphereCollider>().isTrigger = true;
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(item.transform, false);
            model.transform.localScale = new Vector3(0.2f, 0.1f, 0.8f);
            Physics.SyncTransforms();
            return item;
        }

        [Test]
        public void StartsWithNothingCarried()
        {
            Assert.IsFalse(_grab.IsCarrying);
            Assert.IsNull(_grab.CarriedBody);
        }

        [Test]
        public void FindGrabTargetFindsADynamicGrabbableBodyAhead()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.5f, 2.0f));
            try
            {
                Assert.AreSame(body, _grab.FindGrabTarget());
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void FindGrabTargetIgnoresAKinematicBody()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.5f, 2.0f), kinematic: true);
            try
            {
                Assert.IsNull(_grab.FindGrabTarget(), "a still-kinematic (living) ragdoll bone should never be grabbable");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void FindGrabTargetIgnoresABodyWithNoGrabbableMarker()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.5f, 2.0f), grabbableTag: false);
            try
            {
                Assert.IsNull(_grab.FindGrabTarget(), "a raw physics body with no Grabbable marker should never count");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void FindGrabTargetIsNullPastTheRangeAndBehindThePlayer()
        {
            GameObject far = MakeGrabbableBody(new Vector3(0.0f, 0.5f, GrabCarry.GrabRange + 5.0f));
            GameObject behind = MakeGrabbableBody(new Vector3(0.0f, 0.5f, -2.0f));
            try
            {
                Assert.IsNull(_grab.FindGrabTarget());
            }
            finally
            {
                Object.DestroyImmediate(far);
                Object.DestroyImmediate(behind);
            }
        }

        [Test]
        public void HoldingEOnAnItemLyingAsAPropLiftsItAsARealBody()
        {
            GameObject item = MakeLyingItem(new Vector3(0.0f, 0.3f, 2.0f));
            try
            {
                _grab.Tick(holdBegan: true, interactDown: true, throwPressed: false);
                Assert.IsTrue(_grab.IsCarrying, "holding E on an item did not lift it");
                Rigidbody body = item.GetComponent<Rigidbody>();
                Assert.AreSame(body, _grab.CarriedBody);
                Assert.IsFalse(body.useGravity, "a lifted item floats in the hands");
                Collider solid = null;
                foreach (Collider c in item.GetComponents<Collider>())
                {
                    if (!c.isTrigger) solid = c;
                }
                Assert.IsNotNull(solid, "the lifted item has nothing solid to land on");
                Assert.AreEqual(0.8f, solid.bounds.size.z, 0.05f, "its box is not its model's size");
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void AnItemSomeoneHoldsCannotBeLifted()
        {
            var vitals = new GameObject("Other", typeof(PlayerVitals));
            vitals.transform.position = new Vector3(0.0f, 0.0f, 2.0f);
            GameObject item = MakeLyingItem(new Vector3(0.0f, 0.3f, 2.0f));
            item.transform.SetParent(vitals.transform, true);
            Physics.SyncTransforms();
            try
            {
                Assert.IsNull(_grab.FindGrabTarget(), "an item worn or held by another body was liftable");
            }
            finally
            {
                Object.DestroyImmediate(vitals);
            }
        }

        [Test]
        public void NothingIsLiftedBeforeTheHoldAndLettingGoOfEDropsIt()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.5f, 2.0f));
            try
            {
                _grab.Tick(holdBegan: false, interactDown: true, throwPressed: false);
                Assert.IsFalse(_grab.IsCarrying, "lifted before the press became a hold");

                _grab.Tick(holdBegan: true, interactDown: true, throwPressed: false);
                Assert.IsTrue(_grab.IsCarrying);
                _grab.Tick(holdBegan: false, interactDown: true, throwPressed: false);
                Assert.IsTrue(_grab.IsCarrying, "kept while E stays down");

                _grab.Tick(holdBegan: false, interactDown: false, throwPressed: false);
                Assert.IsFalse(_grab.IsCarrying, "letting go of E did not drop it");
                Assert.IsTrue(body.GetComponent<Rigidbody>().useGravity, "dropped, it falls again");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void AClickWhileCarryingThrowsItAlongTheLook()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.5f, 2.0f));
            try
            {
                _grab.StartCarry(body.GetComponent<Rigidbody>());
                _grab.Tick(holdBegan: false, interactDown: true, throwPressed: true);
                Assert.IsFalse(_grab.IsCarrying, "a thrown thing is still in the hands");
                Assert.Greater(body.GetComponent<Rigidbody>().linearVelocity.z, GrabCarry.ThrowSpeed * 0.9f, "it was not thrown where the player looks");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void NoGrabCanStartWhileStunned()
        {
            var vitals = _playerObject.AddComponent<PlayerVitals>();
            _grab.Vitals = vitals;
            vitals.Stun(1.0f);

            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.5f, 2.0f));
            try
            {
                _grab.Tick(holdBegan: true, interactDown: true, throwPressed: false);
                Assert.IsFalse(_grab.IsCarrying);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void DyingDropsWhateverWasBeingCarried()
        {
            var vitals = _playerObject.AddComponent<PlayerVitals>();
            _grab.Vitals = vitals;

            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.5f, 2.0f));
            try
            {
                _grab.StartCarry(body.GetComponent<Rigidbody>());
                vitals.TakeDamage(vitals.MaxHealth);
                _grab.Tick(holdBegan: false, interactDown: true, throwPressed: false);
                Assert.IsFalse(_grab.IsCarrying, "dying should drop whatever was being carried");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ATapIsAShortPressAndAHoldBeginsOnceAtTheThreshold()
        {
            var press = new InteractPress();
            press.Step(true, 0.0f);
            press.Step(true, 0.1f);
            press.Step(false, 0.02f);
            Assert.IsTrue(press.Tapped, "a short press let go is a tap");

            press.Step(true, 0.0f);
            int holds = 0;
            for (int i = 0; i < 30; i++)
            {
                press.Step(true, 0.05f);
                holds += press.HoldBegan ? 1 : 0;
            }
            Assert.AreEqual(1, holds, "a long press begins one hold");
            press.Step(false, 0.02f);
            Assert.IsFalse(press.Tapped, "letting go of a hold is not a tap");
        }
    }
}
