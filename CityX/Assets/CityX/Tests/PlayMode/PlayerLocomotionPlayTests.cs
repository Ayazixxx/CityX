using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Drives the body with real physics frames.
    ///
    /// This is the layer the EditMode rig tests cannot reach. They prove the
    /// avatar and the animator are wired; only running frames proves the
    /// character actually falls, lands, walks at the speed it claims, and stops
    /// at a wall. Movement is the thing that decides how the whole game feels,
    /// so it should not be the one system with no coverage.
    ///
    /// Built from primitives rather than the real prefab so it tests the
    /// CONTROLLER rather than the art. A broken FBX import should fail the rig
    /// tests, not this.
    /// </summary>
    public class PlayerLocomotionPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            _player = new GameObject("Player");
            _player.transform.position = new Vector3(0.0f, 0.4f, 0.0f);
            _player.AddComponent<CharacterController>();
            _locomotion = _player.AddComponent<PlayerLocomotion>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        /// <summary>
        /// Simulated seconds per step. FIXED, not Time.deltaTime.
        ///
        /// The first version of these tests drove by frame count with the real
        /// delta and half of them failed for a reason that had nothing to do
        /// with the code: batch mode runs uncapped at roughly 2500fps, so 180
        /// frames is 0.07 simulated seconds rather than three. A body cannot
        /// fall six metres in that. Stepping a fixed 1/60 makes the tests
        /// deterministic and independent of how fast the machine happens to be.
        /// </summary>
        private const float Step = 1.0f / 60.0f;

        /// <summary>Runs simulation steps with a fixed input.</summary>
        private IEnumerator Drive(LocomotionInput input, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
                // Jump is an edge, not a state -- repeating it every step would
                // be holding the key down, which is a different thing.
                input.Jump = false;
            }
        }

        /// <summary>
        /// THE BODY FACES THE CAMERA, NOT WHERE IT IS GOING.
        ///
        /// It faced its own velocity until now, which sounds right and quietly
        /// removes a whole dimension: if facing and travel are the same vector
        /// then walking backwards is impossible -- the body just turns round
        /// and walks forwards -- and walk_backward, walk_left and walk_right
        /// can never play. Strafing is the clearest case: pushing the stick
        /// left with the camera pointing north should move the body west while
        /// it still faces north.
        /// </summary>
        [UnityTest]
        public IEnumerator StrafingMovesSidewaysWithoutTurningTheBody()
        {
            _player.transform.rotation = Quaternion.identity;
            Vector3 start = _player.transform.position;

            yield return Drive(new LocomotionInput
            {
                Move = new Vector2(-1.0f, 0.0f),
                YawDegrees = 0.0f,
            }, 120);

            Vector3 travelled = _player.transform.position - start;
            Assert.Less(travelled.x, -1.0f,
                $"pushed left for two seconds and moved {travelled.x:0.00}m in x");
            Assert.AreEqual(0.0f, travelled.z, 0.2f, "strafing should not carry the body forwards");

            Assert.AreEqual(0.0f, Mathf.DeltaAngle(0.0f, _player.transform.eulerAngles.y), 1.0f,
                "the body turned to face where it was walking, so it is not strafing at all -- "
                + "it is walking forwards in a new direction");

            // And the animator is told about it in the body's own frame, which
            // is what reaches the sideways clips.
            Assert.Less(_locomotion.LocalVelocity.x, -1.0f,
                "local velocity does not report a sideways step");
            Assert.AreEqual(0.0f, _locomotion.LocalVelocity.y, 0.2f);
        }

        /// <summary>
        /// Backing up keeps the body facing forward, which is the whole reason
        /// walk_backward exists.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingBackwardsKeepsFacingForward()
        {
            _player.transform.rotation = Quaternion.identity;
            Vector3 start = _player.transform.position;

            yield return Drive(new LocomotionInput
            {
                Move = new Vector2(0.0f, -1.0f),
                YawDegrees = 0.0f,
            }, 120);

            Assert.Less(_player.transform.position.z - start.z, -1.0f, "did not travel backwards");
            Assert.AreEqual(0.0f, Mathf.DeltaAngle(0.0f, _player.transform.eulerAngles.y), 1.0f,
                "the body turned round rather than walking backwards");
            Assert.Less(_locomotion.LocalVelocity.y, -1.0f);
        }

        /// <summary>
        /// SPRINT IS FORWARD ONLY, from player.gd. There is no running-sideways
        /// clip, so a 7.5 m/s strafe would be drawn by a 4 m/s walk -- feet
        /// sliding over the ground, the same moonwalk the blend thresholds
        /// exist to prevent.
        /// </summary>
        [UnityTest]
        public IEnumerator SprintingSidewaysOrBackwardsStaysAtWalkingPace()
        {
            foreach (Vector2 move in new[] { new Vector2(1.0f, 0.0f), new Vector2(0.0f, -1.0f) })
            {
                _player.transform.rotation = Quaternion.identity;
                yield return Drive(new LocomotionInput
                {
                    Move = move,
                    YawDegrees = 0.0f,
                    Sprint = true,
                }, 30);

                Assert.AreEqual(_locomotion.WalkSpeed, _locomotion.CurrentSpeed, 0.01f,
                    $"sprinting with move {move} ran at {_locomotion.CurrentSpeed:0.00} m/s, but "
                    + "the only clip that can show it is a walk");
            }

            // Forward still sprints, or this rule would just be "no sprinting".
            yield return Drive(new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                YawDegrees = 0.0f,
                Sprint = true,
            }, 30);
            Assert.AreEqual(_locomotion.SprintSpeed, _locomotion.CurrentSpeed, 0.01f);
        }

        /// <summary>
        /// Standing still and looking around does not spin the character on the
        /// spot. player.gd only folds the camera's yaw into the body while it
        /// is moving, for exactly this reason.
        /// </summary>
        [UnityTest]
        public IEnumerator LookingAroundWhileStandingStillDoesNotTurnTheBody()
        {
            _player.transform.rotation = Quaternion.identity;

            yield return Drive(new LocomotionInput { Move = Vector2.zero, YawDegrees = 90.0f }, 60);

            Assert.AreEqual(0.0f, Mathf.DeltaAngle(0.0f, _player.transform.eulerAngles.y), 1.0f,
                "the body followed the camera while standing still, so looking around spins it");
        }

        [UnityTest]
        public IEnumerator TheBodyFallsAndComesToRestOnTheGround()
        {
            _player.transform.position = new Vector3(0.0f, 6.0f, 0.0f);
            yield return Drive(default, 180);

            Assert.IsTrue(_locomotion.IsGrounded,
                $"still airborne after 180 frames, at y={_player.transform.position.y:0.00}");
            Assert.AreEqual(0.0f, _player.transform.position.y, 0.2f,
                "came to rest at the wrong height -- the capsule centre is probably wrong");
        }

        [UnityTest]
        public IEnumerator WalkingCoversGroundAtTheStatedSpeed()
        {
            yield return Drive(default, 30);   // settle onto the ground first
            Vector3 start = _player.transform.position;

            var input = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f };
            const int steps = 120;
            yield return Drive(input, steps);
            float elapsed = steps * Step;

            float travelled = Vector3.Distance(
                new Vector3(start.x, 0.0f, start.z),
                new Vector3(_player.transform.position.x, 0.0f, _player.transform.position.z));
            float measured = travelled / elapsed;

            Debug.Log($"CityX: walked {travelled:0.00}m in {elapsed:0.00}s = {measured:0.00} m/s");
            Assert.AreEqual(_locomotion.WalkSpeed, measured, _locomotion.WalkSpeed * 0.1f,
                $"walked at {measured:0.00} m/s but WalkSpeed says {_locomotion.WalkSpeed}");
        }

        [UnityTest]
        public IEnumerator SprintingIsFasterThanWalkingByTheStatedRatio()
        {
            yield return Drive(default, 30);

            Vector3 walkStart = _player.transform.position;
            var walk = new LocomotionInput { Move = new Vector2(0.0f, 1.0f) };
            for (int i = 0; i < 60; i++)
            {
                _locomotion.Tick(walk, Time.deltaTime);
                yield return null;
            }
            float walked = Vector3.Distance(walkStart, _player.transform.position);

            Vector3 sprintStart = _player.transform.position;
            var sprint = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Sprint = true };
            for (int i = 0; i < 60; i++)
            {
                _locomotion.Tick(sprint, Time.deltaTime);
                yield return null;
            }
            float sprinted = Vector3.Distance(sprintStart, _player.transform.position);

            float ratio = sprinted / walked;
            float expected = _locomotion.SprintSpeed / _locomotion.WalkSpeed;
            Debug.Log($"CityX: sprint/walk distance ratio {ratio:0.00}, expected {expected:0.00}");
            Assert.AreEqual(expected, ratio, 0.15f, "sprinting is not the speed it claims");
        }

        /// <summary>
        /// Sprint is only meant to apply when actually moving. Without the
        /// guard, standing still with shift held reports sprint speed to the
        /// animator and the character runs on the spot.
        /// </summary>
        [UnityTest]
        public IEnumerator SprintingWhileStandingStillDoesNotMove()
        {
            yield return Drive(default, 90);   // let it settle fully onto the ground
            Vector3 start = _player.transform.position;

            var input = new LocomotionInput { Move = Vector2.zero, Sprint = true };
            yield return Drive(input, 60);

            Assert.AreEqual(0.0f, _locomotion.CurrentSpeed, 0.001f,
                "reports speed while standing still -- the animator would run on the spot");

            // HORIZONTAL only. Measuring 3D distance also measures the body
            // settling the last few centimetres onto the ground, which is
            // correct behaviour being reported as a failure.
            float drift = Vector2.Distance(
                new Vector2(start.x, start.z),
                new Vector2(_player.transform.position.x, _player.transform.position.z));
            Assert.AreEqual(0.0f, drift, 0.05f, "drifted sideways while standing still");
        }

        [UnityTest]
        public IEnumerator JumpingLeavesTheGroundAndComesBack()
        {
            yield return Drive(default, 30);
            float restingY = _player.transform.position.y;

            var jump = new LocomotionInput { Jump = true };
            _locomotion.Tick(jump, Step);
            yield return null;

            // Apex is v^2/2g = 4.5^2 / (2*9.8) = 1.03m, reached after 4.5/9.8 =
            // 0.46s. 40 steps of 1/60 is 0.67s, comfortably past it.
            float peak = restingY;
            for (int i = 0; i < 40; i++)
            {
                _locomotion.Tick(default, Step);
                peak = Mathf.Max(peak, _player.transform.position.y);
                yield return null;
            }

            Debug.Log($"CityX: jump peaked {peak - restingY:0.00}m above rest");
            Assert.Greater(peak - restingY, 0.5f, "the jump barely left the ground");

            yield return Drive(default, 240);
            Assert.IsTrue(_locomotion.IsGrounded, "never landed again");
        }

        /// <summary>
        /// Movement is relative to yaw, so the same stick direction with the
        /// camera turned must send the body somewhere else. Getting this wrong
        /// gives a character that only ever walks north.
        /// </summary>
        [UnityTest]
        public IEnumerator MovementFollowsTheYawItIsGiven()
        {
            yield return Drive(default, 30);

            Vector3 start = _player.transform.position;
            var north = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f };
            yield return Drive(north, 45);
            Vector3 wentNorth = _player.transform.position - start;

            _player.transform.position = start;
            yield return Drive(default, 10);

            start = _player.transform.position;
            var east = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 90.0f };
            yield return Drive(east, 45);
            Vector3 wentEast = _player.transform.position - start;

            Assert.Greater(wentNorth.z, 0.5f, "yaw 0 with forward input did not go +Z");
            Assert.Greater(wentEast.x, 0.5f, "yaw 90 with forward input did not go +X");
            Assert.Less(Mathf.Abs(wentNorth.x), 0.3f, "yaw 0 drifted sideways");
            Assert.Less(Mathf.Abs(wentEast.z), 0.3f, "yaw 90 drifted sideways");
        }

        /// <summary>
        /// The city is narrow streets between solid buildings. A controller
        /// that tunnels through a wall at sprint speed would make the whole
        /// thing unplayable, and it is exactly the kind of bug that only shows
        /// up at the higher speed.
        /// </summary>
        [UnityTest]
        public IEnumerator SprintingIntoAWallStopsRatherThanPassingThrough()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 4.0f);
            wall.transform.localScale = new Vector3(20.0f, 8.0f, 1.0f);

            // Physics.autoSyncTransforms is off by default, so a collider
            // created and scaled mid-test still has its OLD shape until
            // something syncs. Without this the wall is a 1m cube the character
            // walks straight past, and the test fails describing a tunnelling
            // bug that does not exist.
            Physics.SyncTransforms();
            yield return null;

            try
            {
                yield return Drive(default, 30);
                var input = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Sprint = true };
                yield return Drive(input, 180);

                Assert.Less(_player.transform.position.z, 4.0f,
                    $"ended up at z={_player.transform.position.z:0.00}, past a wall at z=4");
            }
            finally
            {
                Object.Destroy(wall);
            }
        }

        [UnityTest]
        public IEnumerator ChangingStandingHeightResizesTheCapsuleWithIt()
        {
            var controller = _player.GetComponent<CharacterController>();

            _locomotion.StandingHeight = 1.8f;
            _locomotion.ApplyHeight();
            yield return null;
            Assert.AreEqual(1.8f, controller.height, 0.01f);
            Assert.AreEqual(0.9f, controller.center.y, 0.01f, "the capsule is not centred on the body");

            _locomotion.StandingHeight = 2.2f;
            _locomotion.ApplyHeight();
            yield return null;
            Assert.AreEqual(2.2f, controller.height, 0.01f);
            Assert.AreEqual(1.1f, controller.center.y, 0.01f);
            Assert.Less(controller.radius * 2.0f, controller.height, "capsule wider than tall");
        }
    }
}
