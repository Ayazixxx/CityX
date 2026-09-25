using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The grab/carry state machine and target-finding. Ported from
    /// player.gd's _process_grab_carry/_find_grab_target.
    /// </summary>
    public sealed class GrabCarryTests
    {
        private GameObject _playerObject;
        private GameObject _eyeObject;
        private GrabCarry _grab;

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

        [Test]
        public void StartsWithNothingCarried()
        {
            Assert.IsFalse(_grab.IsCarrying);
            Assert.IsNull(_grab.CarriedBody);
        }

        [Test]
        public void FindGrabTargetFindsADynamicGrabbableBodyAhead()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                Rigidbody found = _grab.FindGrabTarget();
                Assert.IsNotNull(found);
                Assert.AreSame(body.GetComponent<Rigidbody>(), found);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void FindGrabTargetIgnoresAKinematicBody()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f), kinematic: true);
            try
            {
                Assert.IsNull(_grab.FindGrabTarget(),
                    "a still-kinematic (living) ragdoll bone should never be grabbable");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void FindGrabTargetIgnoresABodyWithNoGrabbableMarker()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f), grabbableTag: false);
            try
            {
                Assert.IsNull(_grab.FindGrabTarget(),
                    "a raw physics body with no Grabbable marker should never count, matching Godot's group check");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void FindGrabTargetIsNullPastTheRange()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, GrabCarry.GrabRange + 5.0f));
            try
            {
                Assert.IsNull(_grab.FindGrabTarget());
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void HoldingInteractBelowTheThresholdDoesNotGrab()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                _grab.Tick(GrabCarry.GrabHoldSeconds - 0.05f, interactPressedThisFrame: true, interactHeld: true);
                Assert.IsFalse(_grab.IsCarrying);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void HoldingInteractPastTheThresholdGrabsWhateverIsAimedAt()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                _grab.Tick(GrabCarry.GrabHoldSeconds + 0.01f, interactPressedThisFrame: true, interactHeld: true);
                Assert.IsTrue(_grab.IsCarrying);
                Assert.AreSame(body.GetComponent<Rigidbody>(), _grab.CarriedBody);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ReleasingBeforeTheThresholdResetsTheHoldTimer()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                _grab.Tick(GrabCarry.GrabHoldSeconds - 0.05f, interactPressedThisFrame: true, interactHeld: true);
                _grab.Tick(0.02f, interactPressedThisFrame: false, interactHeld: false);
                // A second short hold should not pick up where the first left
                // off -- releasing has to actually reset the timer.
                _grab.Tick(0.04f, interactPressedThisFrame: true, interactHeld: true);

                Assert.IsFalse(_grab.IsCarrying, "the timer should not have carried over across a release");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ATapWhileCarryingDropsIt()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                _grab.StartCarry(body.GetComponent<Rigidbody>());
                Assert.IsTrue(_grab.IsCarrying);

                _grab.Tick(0.016f, interactPressedThisFrame: true, interactHeld: true);

                Assert.IsFalse(_grab.IsCarrying, "a plain tap while carrying should drop, not require a full hold");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void CarryingIgnoresFurtherInteractHoldsUntilDropped()
        {
            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                _grab.StartCarry(body.GetComponent<Rigidbody>());

                // Holding (not tapping) while already carrying should do
                // nothing -- no re-grab, no drop.
                _grab.Tick(1.0f, interactPressedThisFrame: false, interactHeld: true);

                Assert.IsTrue(_grab.IsCarrying);
                Assert.AreSame(body.GetComponent<Rigidbody>(), _grab.CarriedBody);
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

            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                _grab.Tick(GrabCarry.GrabHoldSeconds + 0.01f, interactPressedThisFrame: true, interactHeld: true);
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

            GameObject body = MakeGrabbableBody(new Vector3(0.0f, 0.0f, 2.0f));
            try
            {
                _grab.StartCarry(body.GetComponent<Rigidbody>());
                Assert.IsTrue(_grab.IsCarrying);

                vitals.TakeDamage(vitals.MaxHealth);
                _grab.Tick(0.016f, interactPressedThisFrame: false, interactHeld: false);

                Assert.IsFalse(_grab.IsCarrying, "dying should drop whatever was being carried");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
