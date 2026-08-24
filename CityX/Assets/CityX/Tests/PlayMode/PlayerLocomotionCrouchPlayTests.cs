using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "CROUCH WALKING ISN'T WORKING RIGHT" -- confirmed directly: crouching
    /// (or un-crouching) while already moving used to sit through the WHOLE
    /// StandingToCrouch/CrouchToIdle fold animation before the body's pose
    /// caught up with Crouched/Locomotion, even though the CharacterController
    /// itself keeps moving the entire time (PlayerLocomotion.Tick's own
    /// movement code is unconditional) -- the body visibly slides across the
    /// ground under a fold pose that was never meant to carry any travel.
    ///
    /// Ported from character.gd's own start_crouch()/end_crouch(), whose own
    /// class comments say plainly: "Firing this while the player is ALREADY
    /// moving is fine: set_walking's own 'crouching' branch plays crouch_walk
    /// instead the very same/next frame, so this transition only actually
    /// gets seen through to completion while standing still." PlayerRigBuilder
    /// .AddCrouchStates now has its own explicit moving-skips-the-pose
    /// transitions for exactly this.
    ///
    /// NEEDS THE REAL Player.prefab, NOT A BARE GameObject -- see
    /// PlayerLocomotionHipsPlayTests's own comment on why; there is no
    /// Crouched/StandingToCrouch/CrouchToIdle state without the genuine
    /// AnimatorController PlayerRigBuilder actually built.
    /// </summary>
    public sealed class PlayerLocomotionCrouchPlayTests
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

        [UnityTest]
        public IEnumerator CrouchingWhileAlreadyWalkingReachesTheCrouchedStateQuicklyRatherThanSlidingThroughTheFold()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            // Get moving and let facing settle first.
            var forward = new LocomotionInput { Move = new Vector2(0.0f, 1.0f) };
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(forward, StepSeconds);
                yield return null;
            }

            // NOW start crouching, still holding forward -- the reported bug.
            // 1800 real frames is comfortably short of the OLD, exit-time-
            // gated path's own settling point (confirmed taking close to
            // 3000 real frames from a cold start in manual measurement), so
            // reaching Crouched by here is real evidence of the moving-
            // interrupt transition firing rather than the old path just
            // having had enough time to finish on its own.
            var crouchMoving = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Crouch = true };
            for (int step = 0; step < 1800; step++)
            {
                _locomotion.Tick(crouchMoving, StepSeconds);
                yield return null;
            }

            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Crouched"),
                "still not in the Crouched state after 1800 real frames of holding both " +
                "forward and crouch -- the body is sliding through the fold pose instead " +
                "of skipping straight to crouch_walk the way it does while standing still");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ReleasingCrouchWhileStillWalkingReturnsToLocomotionQuicklyRatherThanSlidingThroughTheUnfold()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            var forward = new LocomotionInput { Move = new Vector2(0.0f, 1.0f) };
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(forward, StepSeconds);
                yield return null;
            }

            var crouchMoving = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Crouch = true };
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(crouchMoving, StepSeconds);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Crouched"),
                "setup failed -- never actually reached Crouched before testing the release");

            var uncrouchMoving = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Crouch = false };
            for (int step = 0; step < 1800; step++)
            {
                _locomotion.Tick(uncrouchMoving, StepSeconds);
                yield return null;
            }

            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),
                "still not back in Locomotion after 1800 real frames of releasing crouch " +
                "while still walking -- the body is sliding through crouch_to_idle instead " +
                "of skipping straight back to the walk cycle");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator CrouchingWhileStandingStillStillShowsTheFoldPoseRatherThanSkippingIt()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            // Standing still throughout -- the moving-skips-the-pose
            // transitions must NOT fire here, or a standing crouch would
            // lose its own fold animation entirely. Sampled across the whole
            // window rather than checked once at a fixed frame count: batch
            // mode's real fps is not perfectly constant run to run (confirmed
            // flaky at a single fixed sample when run alongside the rest of
            // the suite rather than in isolation), so this only needs to have
            // been in StandingToCrouch at SOME point, not at one exact frame.
            var crouchStill = new LocomotionInput { Move = Vector2.zero, Crouch = true };
            bool sawFoldPose = false;
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(crouchStill, StepSeconds);
                yield return null;
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("StandingToCrouch"))
                {
                    sawFoldPose = true;
                }
            }

            Assert.IsTrue(sawFoldPose,
                "never once saw the StandingToCrouch fold pose across 3000 real frames of " +
                "standing still and holding crouch -- it should only ever be skipped while " +
                "actually moving");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
