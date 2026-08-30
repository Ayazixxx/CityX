using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The grapple swing's own ragdoll -- explicit ask: "his right hand
    /// stay attached to it but the rest of his body is ragdolling until he
    /// reaches land or dashes". Separate from RagdollPlayTests, which only
    /// exercises the full death ragdoll (Activate/Deactivate); this is
    /// specifically about ActivateSwing/SetSwingPullTarget/SetLeftArmPose/
    /// DeactivateSwing.
    ///
    /// TWO EARLIER VERSIONS OF THIS MECHANISM BOTH STRETCHED THE RIG APART
    /// LIVE -- first a version that computed Chest's target pose fresh
    /// every frame while keeping it kinematic, then a version that captured
    /// Chest's own starting pose for continuity but still kept it kinematic.
    /// Both failed for the same underlying reason: a kinematic body forced
    /// through a real swing's own fast, curving motion gives the joint
    /// solver connecting it to non-kinematic neighbours an impossible job
    /// EVERY physics step, not just at the start. The current design makes
    /// the whole rig genuinely non-kinematic (a real, ordinary ragdoll) and
    /// pulls RightLowerArm toward the swing's own target with a bounded,
    /// damped FORCE instead -- a normal input to the same solver, not an
    /// override fighting it. SwingingDoesNotStretchTheRigApartOrFlingItAway
    /// is the regression test for that whole class of bug, simulating a
    /// real multi-second swing rather than a single frame.
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
        /// logic under test is specifically about those bones, not the
        /// physics itself (RagdollPlayTests' own stand-in rig already
        /// covers that).
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

        /// <summary>
        /// The actual bug report this exists to catch, reproduced as
        /// faithfully as a stand-in rig allows: "he just starts stretching
        /// apart and his limbs start flying all over the place when i use
        /// the grappling hook". Runs a real multi-second simulated swing
        /// (not just one frame -- the earlier, kinematic-Chest design could
        /// pass a single-frame check and still destabilise continuously
        /// once the anchor kept moving) and checks the two things "the rig
        /// stayed together" actually means: neighbouring joints did not
        /// drift apart, and nothing reached an unreasonable velocity.
        /// </summary>
        [UnityTest]
        public IEnumerator SwingingDoesNotStretchTheRigApartOrFlingItAway()
        {
            Ragdoll ragdoll = MakeSwingRig();
            _body.transform.position = new Vector3(2.0f, 1.3f, -3.5f);

            _body.AddComponent<CharacterController>();
            var swing = _body.AddComponent<GrappleSwing>();
            swing.Bind();

            swing.Attach(_body.transform.position + new Vector3(3.0f, 6.0f, 2.0f));

            for (int i = 0; i < 180; i++)
            {
                swing.Tick(Vector2.zero, 0.0f, false, false, false, false, 1.0f / 60.0f);
                yield return new WaitForFixedUpdate();
            }

            float chestToHips = Vector3.Distance(ragdoll.Chest.position, ragdoll.Torso.position);
            Assert.Less(chestToHips, 1.0f,
                $"Chest and Hips ended up {chestToHips:0.00}m apart -- a real joint between them should " +
                "never let that happen no matter what pulls on the rest of the rig");

            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.Less(body.linearVelocity.magnitude, 30.0f,
                    $"'{body.name}' is travelling at {body.linearVelocity.magnitude:0}m/s after three seconds " +
                    "of swinging -- that reads as the rig being flung apart, not hanging from a rope");
            }
        }

        [Test]
        public void ActivateSwingMakesEveryBodyNonKinematic()
        {
            Ragdoll ragdoll = MakeSwingRig();

            ragdoll.ActivateSwing(Vector3.zero);

            Assert.IsTrue(ragdoll.IsSwinging);
            // EVERY body, Chest and the right arm included -- see this
            // file's own class comment for why an earlier version kept
            // those two kinematic and what that broke.
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.IsFalse(body.isKinematic, $"'{body.name}' should be a real physics body while swinging");
            }
        }

        [UnityTest]
        public IEnumerator SetSwingPullTargetPullsTheRightHandTowardItOverTime()
        {
            // Gravity off for this one -- real GrappleSwing usage never
            // fights it in isolation like this test does (the swing's own
            // pendulum physics, which already accounts for gravity, is
            // what moves the target every frame in the real game), so an
            // unsupported rig free-falling under REAL gravity for a full
            // second while this asks it to climb straight up would test
            // "does gravity beat an isolated upward pull", not "does the
            // pull mechanism work" -- confirmed directly: it failed for
            // exactly that reason before gravity was excluded here.
            Vector3 realGravity = Physics.gravity;
            Physics.gravity = Vector3.zero;
            try
            {
                Ragdoll ragdoll = MakeSwingRig();
                ragdoll.ActivateSwing(Vector3.zero);

                Vector3 target = ragdoll.RightLowerArm.position + new Vector3(2.0f, 1.0f, 0.0f);
                float startDistance = Vector3.Distance(ragdoll.RightLowerArm.position, target);

                for (int i = 0; i < 60; i++)
                {
                    ragdoll.SetSwingPullTarget(target);
                    yield return new WaitForFixedUpdate();
                }

                float endDistance = Vector3.Distance(ragdoll.RightLowerArm.position, target);
                Assert.Less(endDistance, startDistance,
                    $"the pull never closed any distance to its own target ({startDistance:0.00}m -> {endDistance:0.00}m)");
            }
            finally
            {
                Physics.gravity = realGravity;
            }
        }

        /// <summary>
        /// The other half of SwingPullMaxAcceleration's own job: even a
        /// target placed absurdly far away must not fling the hand at
        /// extreme speed -- the cap exists specifically so a stale or
        /// otherwise wrong target cannot explode the rig the way an
        /// unbounded spring force could.
        /// </summary>
        [UnityTest]
        public IEnumerator SetSwingPullTargetNeverExceedsTheAccelerationCap()
        {
            Ragdoll ragdoll = MakeSwingRig();
            ragdoll.ActivateSwing(Vector3.zero);

            Vector3 farTarget = ragdoll.RightLowerArm.position + new Vector3(500.0f, 0.0f, 0.0f);

            for (int i = 0; i < 5; i++)
            {
                ragdoll.SetSwingPullTarget(farTarget);
                yield return new WaitForFixedUpdate();
            }

            float maxExpectedSpeed = Ragdoll.SwingPullMaxAcceleration * Time.fixedDeltaTime * 6.0f;
            Assert.Less(ragdoll.RightLowerArm.linearVelocity.magnitude, maxExpectedSpeed,
                $"the hand reached {ragdoll.RightLowerArm.linearVelocity.magnitude:0}m/s pulling toward a " +
                "far target -- the acceleration cap did not hold");
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
