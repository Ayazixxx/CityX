using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "LEGS STILL MOVE WHILE HOLDING SPACE IF YOU ARE MOVING WHILE CHARGING
    /// YOUR JUMP" -- ChargedJump is a single fixed held pose (see
    /// DampenChargedJumpPose), not a blend over Speed/MoveX/MoveZ like
    /// Locomotion, so entering it while already moving used to freeze the
    /// legs mid-stride for as long as space stayed held, even though the
    /// CharacterController itself kept moving the whole time (same shape as
    /// the old crouch fold had).
    /// PlayerRigBuilder.AddChargedJumpState now gates ENTRY into the pose on
    /// Speed (AddSpeedCondition).
    ///
    /// ONLY ENTRY IS GATED, DELIBERATELY -- see AddChargedJumpState's own
    /// doc comment: a matching Speed-gated exit was tried and tests below
    /// (kept in history, not in this file) caught it silently swallowing a
    /// jump release on the one frame movement starts on the exact same
    /// frame space is let go. Starting a charge standing still and THEN
    /// moving stays frozen in the held pose until release -- not covered
    /// here on purpose, not an oversight.
    ///
    /// NEEDS THE REAL Player.prefab, NOT A BARE GameObject -- see
    /// PlayerLocomotionHipsPlayTests's own comment on why; there is no
    /// ChargedJump/Locomotion state without the genuine AnimatorController
    /// PlayerRigBuilder actually built.
    /// </summary>
    public sealed class PlayerLocomotionChargedJumpPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        private const float StepSeconds = 1.0f / 60.0f;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private bool Spawn()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
            return true;
#else
            return false;
#endif
        }

        [UnityTest]
        public IEnumerator ChargingWhileAlreadyMovingKeepsTheWalkCycleRatherThanFreezingIntoTheChargePose()
        {
            if (!Spawn())
            {
                Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
                yield break;
            }

            // Get moving and let facing settle first, same as the crouch tests.
            var forward = new LocomotionInput { Move = new Vector2(0.0f, 1.0f) };
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(forward, StepSeconds);
                yield return null;
            }

            // NOW start charging, still holding forward -- the reported bug.
            var chargingMoving = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), ChargingJump = true };
            bool sawChargedPose = false;
            for (int step = 0; step < 1800; step++)
            {
                _locomotion.Tick(chargingMoving, StepSeconds);
                yield return null;
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("ChargedJump"))
                {
                    sawChargedPose = true;
                }
            }

            Assert.IsFalse(sawChargedPose,
                "entered the ChargedJump pose while holding forward the whole time -- the " +
                "legs should keep walking instead of freezing into the held charge pose");
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),
                "not in Locomotion after 1800 real frames of holding both forward and charge");
        }

        [UnityTest]
        public IEnumerator ChargingWhileStandingStillStillShowsTheChargedPoseRatherThanSkippingIt()
        {
            if (!Spawn())
            {
                Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
                yield break;
            }

            // Standing still throughout -- the moving-skips-the-pose gate must
            // NOT fire here, or a standing charge would lose its own pose
            // entirely. Sampled across the whole window: a pose can be seen
            // only part of the time.
            var chargingStill = new LocomotionInput { ChargingJump = true };
            bool sawChargedPose = false;
            for (int step = 0; step < 1800; step++)
            {
                _locomotion.Tick(chargingStill, StepSeconds);
                yield return null;
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("ChargedJump"))
                {
                    sawChargedPose = true;
                }
            }

            Assert.IsTrue(sawChargedPose,
                "never once saw the ChargedJump pose across 1800 real frames of standing " +
                "still and holding charge -- it should only ever be skipped while actually moving");
        }

        /// <summary>
        /// A plain regression check, not a race: with only the entry gated
        /// (see this class's own doc comment for why the exit was dropped),
        /// ChargedJump's only way out is the same any-state Jump transition
        /// every other state uses, so releasing while also moving has
        /// nothing of this fix's own to interfere with it. Verified anyway,
        /// the same way FullyChargedReleaseJumpsTheConfiguredMultiplierHigher
        /// verifies the unmodified case.
        /// </summary>
        [UnityTest]
        public IEnumerator ReleasingWhileMovingStillPaysOutTheFullBoost()
        {
            if (!Spawn())
            {
                Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
                yield break;
            }

            var chargingStill = new LocomotionInput { ChargingJump = true };
            for (int step = 0; step < 1800; step++)
            {
                _locomotion.Tick(chargingStill, StepSeconds);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("ChargedJump"),
                "setup failed -- never actually reached ChargedJump before testing the release");

            float restingY = _player.transform.position.y;

            var releaseWhileMoving = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                JumpReleased = true,
                JumpChargeFraction = 1.0f,
            };
            _locomotion.Tick(releaseWhileMoving, StepSeconds);
            yield return null;

            // 3000 real frames, not 150 -- confirmed by direct measurement that
            // the ChargedJump-to-Jump any-state transition's own declared
            // 0.08s duration does not translate to a fixed, small frame count
            // here: PlayMode's real Animator.Update runs on real elapsed time,
            // not this loop's fixed StepSeconds, and 150 real frames measured
            // short of the transition actually completing even with the jump
            // physics already unmistakably in flight (a genuine large upward
            // velocity every one of those frames) -- a budget problem, not a
            // sign the trigger was lost.
            bool reachedJumpState = _animator.GetCurrentAnimatorStateInfo(0).IsName("Jump");
            float peak = restingY;
            for (int i = 0; i < 3000; i++)
            {
                _locomotion.Tick(new LocomotionInput { Move = new Vector2(0.0f, 1.0f) }, StepSeconds);
                peak = Mathf.Max(peak, _player.transform.position.y);
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"))
                {
                    reachedJumpState = true;
                }
                yield return null;
            }

            Assert.IsTrue(reachedJumpState,
                "the Jump state was never reached after releasing a full charge while moving");

            float apex = peak - restingY;
            float expected = 1.03f * _locomotion.ChargedJumpHeightMultiplier;
            Debug.Log($"CityX: release-while-moving peaked {apex:0.00}m above rest (expected ~{expected:0.00}m)");
            Assert.AreEqual(expected, apex, expected * 0.15f,
                "the full charge boost did not pay out while releasing while moving");
        }
    }
}
