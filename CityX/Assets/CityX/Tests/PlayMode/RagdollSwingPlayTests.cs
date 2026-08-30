using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The grapple swing's own partial ragdoll -- explicit ask: "his right
    /// hand stay attached to it but the rest of his body is ragdolling
    /// until he reaches land or dashes". Separate from RagdollPlayTests,
    /// which only exercises the full death ragdoll (Activate/Deactivate);
    /// this is specifically about ActivateSwing/TickSwingPose/
    /// SetLeftArmPose/DeactivateSwing keeping Chest and the right arm
    /// kinematic while everything else goes limp.
    /// </summary>
    public class RagdollSwingPlayTests
    {
        private GameObject _body;

        [TearDown]
        public void TearDown()
        {
            if (_body != null) Object.DestroyImmediate(_body);
        }

        /// <summary>
        /// A stand-in rig with the SAME named bones RagdollBuilder wires on
        /// the real one -- Hips/Chest/Head plus both arms -- since the
        /// logic under test is specifically about which of THOSE stay
        /// kinematic, not the physics itself (RagdollPlayTests' own stand-in
        /// rig already covers that). No legs: nothing here reads them.
        /// </summary>
        private Ragdoll MakeSwingRig()
        {
            _body = new GameObject("Body");

            Rigidbody hips = MakePiece("Hips", _body.transform, Vector3.zero, null);
            Rigidbody chest = MakePiece("Chest", hips.transform, new Vector3(0.0f, 0.4f, 0.0f), hips);
            Rigidbody head = MakePiece("Head", chest.transform, new Vector3(0.0f, 0.3f, 0.0f), chest);
            Rigidbody rightUpper = MakePiece("RightUpperArm", chest.transform, new Vector3(0.3f, 0.0f, 0.0f), chest);
            Rigidbody rightLower = MakePiece("RightLowerArm", rightUpper.transform, new Vector3(0.0f, -0.3f, 0.0f), rightUpper);
            Rigidbody leftUpper = MakePiece("LeftUpperArm", chest.transform, new Vector3(-0.3f, 0.0f, 0.0f), chest);
            Rigidbody leftLower = MakePiece("LeftLowerArm", leftUpper.transform, new Vector3(0.0f, -0.3f, 0.0f), leftUpper);

            var ragdoll = _body.AddComponent<Ragdoll>();
            ragdoll.Bodies = new[] { hips, chest, head, rightUpper, rightLower, leftUpper, leftLower };
            ragdoll.Torso = hips;
            ragdoll.Chest = chest;
            ragdoll.Head = head;
            ragdoll.RightUpperArm = rightUpper;
            ragdoll.RightLowerArm = rightLower;
            ragdoll.LeftUpperArm = leftUpper;
            ragdoll.LeftLowerArm = leftLower;

            // Same reason RagdollPlayTests' own MakeRig does this: Awake
            // already ran with no bodies set, so the collision-ignore pass
            // needs redoing now they exist.
            _body.SetActive(false);
            _body.SetActive(true);
            return ragdoll;
        }

        private static Rigidbody MakePiece(string name, Transform parent, Vector3 localPosition, Rigidbody connectedTo)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.2f;

            var rigid = go.AddComponent<Rigidbody>();
            rigid.mass = 5.0f;
            rigid.linearDamping = Ragdoll.LinearDamping;
            rigid.angularDamping = Ragdoll.AngularDamping;
            rigid.isKinematic = true;
            rigid.maxDepenetrationVelocity = 3.0f;

            if (connectedTo != null)
            {
                var joint = go.AddComponent<CharacterJoint>();
                joint.connectedBody = connectedTo;
                joint.swing1Limit = new SoftJointLimit { limit = 60.0f };
                joint.swing2Limit = new SoftJointLimit { limit = 60.0f };
                joint.lowTwistLimit = new SoftJointLimit { limit = -30.0f };
                joint.highTwistLimit = new SoftJointLimit { limit = 30.0f };
                joint.enablePreprocessing = false;
            }

            return rigid;
        }

        [Test]
        public void ActivateSwingKeepsChestAndTheRightArmKinematicButRagdollsEverythingElse()
        {
            Ragdoll ragdoll = MakeSwingRig();

            ragdoll.ActivateSwing(Vector3.zero);

            Assert.IsTrue(ragdoll.IsSwinging);
            Assert.IsTrue(ragdoll.Chest.isKinematic, "Chest should stay driven, not ragdoll");
            Assert.IsTrue(ragdoll.RightUpperArm.isKinematic, "the pinned arm's own upper segment should stay driven");
            Assert.IsTrue(ragdoll.RightLowerArm.isKinematic, "the pinned arm's own lower segment should stay driven");

            Assert.IsFalse(ragdoll.Torso.isKinematic, "the hips should be ragdolling");
            Assert.IsFalse(ragdoll.Head.isKinematic, "the head should be ragdolling");
            Assert.IsFalse(ragdoll.LeftUpperArm.isKinematic, "the free arm should start ragdolling");
            Assert.IsFalse(ragdoll.LeftLowerArm.isKinematic, "the free arm should start ragdolling");
        }

        [UnityTest]
        public IEnumerator TickSwingPoseDragsChestToTheGivenTransformAndHoldsTheRightArmPose()
        {
            Ragdoll ragdoll = MakeSwingRig();
            ragdoll.ActivateSwing(Vector3.zero);

            var targetPosition = new Vector3(3.0f, 5.0f, -2.0f);
            var targetRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
            var upperPose = Quaternion.Euler(-10.0f, 15.0f, -150.0f);
            var lowerPose = Quaternion.Euler(60.0f, 0.0f, 0.0f);

            ragdoll.TickSwingPose(targetPosition, targetRotation, upperPose, lowerPose);
            // MovePosition/MoveRotation on a kinematic body only actually
            // lands on the transform at the next physics step.
            yield return new WaitForFixedUpdate();

            Assert.Less(Vector3.Distance(ragdoll.Chest.position, targetPosition), 0.01f,
                "TickSwingPose did not move Chest to the swing's own live position");
            Assert.Less(Quaternion.Angle(ragdoll.Chest.rotation, targetRotation), 1.0f,
                "TickSwingPose did not turn Chest to face the swing's own direction");
            Assert.AreEqual(upperPose, ragdoll.RightUpperArm.transform.localRotation,
                "the right upper arm did not hold the given swing pose");
            Assert.AreEqual(lowerPose, ragdoll.RightLowerArm.transform.localRotation,
                "the right lower arm did not hold the given swing pose");
        }

        [Test]
        public void SetLeftArmPoseCanPullTheFreeArmOutOfTheRagdollAndBackIn()
        {
            Ragdoll ragdoll = MakeSwingRig();
            ragdoll.ActivateSwing(Vector3.zero);
            Assert.IsFalse(ragdoll.LeftUpperArm.isKinematic, "setup failed -- the free arm should start ragdolling");

            var castUpper = Quaternion.Euler(-10.0f, -15.0f, 150.0f);
            var castLower = Quaternion.Euler(60.0f, 0.0f, 0.0f);
            ragdoll.SetLeftArmPose(true, castUpper, castLower);

            Assert.IsTrue(ragdoll.LeftUpperArm.isKinematic, "casting should pull the arm out of the ragdoll");
            Assert.IsTrue(ragdoll.LeftLowerArm.isKinematic);
            Assert.AreEqual(castUpper, ragdoll.LeftUpperArm.transform.localRotation);
            Assert.AreEqual(castLower, ragdoll.LeftLowerArm.transform.localRotation);

            ragdoll.SetLeftArmPose(false, castUpper, castLower);

            Assert.IsFalse(ragdoll.LeftUpperArm.isKinematic, "the arm should go back to ragdolling once the cast ends");
            Assert.IsFalse(ragdoll.LeftLowerArm.isKinematic);
        }

        [Test]
        public void DeactivateSwingRestoresFullKinematicControlToEveryBody()
        {
            Ragdoll ragdoll = MakeSwingRig();
            ragdoll.ActivateSwing(Vector3.zero);

            ragdoll.DeactivateSwing();

            Assert.IsFalse(ragdoll.IsSwinging);
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.IsTrue(body.isKinematic, $"'{body.name}' is still live after the swing ended");
            }
        }

        /// <summary>Can't start a partial ragdoll on a body that is already a full one -- dead is dead.</summary>
        [Test]
        public void ActivateSwingDoesNothingWhileAlreadyLimp()
        {
            Ragdoll ragdoll = MakeSwingRig();
            ragdoll.Activate(Vector3.zero);

            ragdoll.ActivateSwing(Vector3.zero);

            Assert.IsFalse(ragdoll.IsSwinging, "a dead body should not also start swinging");
        }

        /// <summary>
        /// Dying mid-swing already made Activate() take over with a full
        /// ragdoll. Update ordering between PlayerInputReader's own death
        /// check (which releases the swing) and PlayerVitals.Died (which
        /// activates the death ragdoll) is not guaranteed, so
        /// DeactivateSwing must not undo the real death ragdoll if it
        /// happens to run second.
        /// </summary>
        [Test]
        public void DeactivateSwingDoesNotUndoARealDeathRagdoll()
        {
            Ragdoll ragdoll = MakeSwingRig();
            ragdoll.ActivateSwing(Vector3.zero);
            ragdoll.Activate(Vector3.zero);

            ragdoll.DeactivateSwing();

            Assert.IsTrue(ragdoll.IsLimp, "the death ragdoll should still be in control");
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.IsFalse(body.isKinematic,
                    $"'{body.name}' was put back under animated control despite the body being dead");
            }
        }
    }
}
