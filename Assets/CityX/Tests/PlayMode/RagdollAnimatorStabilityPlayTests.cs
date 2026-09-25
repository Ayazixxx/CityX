using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT GAP, found live from a REAL PoseDistortionWatchdog log entry
    /// during actual play (not a synthetic test): `Swing.IsActive: False,
    /// Console.IsOpen: False, Animator.enabled: False` -- neither of the two
    /// systems the whole "console and grapple wildly stretches the body"
    /// investigation had ever considered was active. A project-wide grep for
    /// `.enabled = false` on an Animator found the third cause in about a
    /// minute: `Ragdoll.Activate` disables the Animator too, for any fall,
    /// crash, knockdown, or death -- completely unrelated to console or
    /// grapple, and never examined once across ~28 prior diagnostics plus two
    /// same-day rounds of fixes for the other two triggers.
    ///
    /// ARGUABLY WORSE THAN A CONSOLE FREEZE: a console freeze only HOLDS a
    /// bad pose (ugly, but static). Ragdoll.Activate hands the bones to REAL
    /// PHYSICS immediately after disabling the Animator (SetKinematic(false),
    /// a few lines down) -- an already-extreme captured rotation becomes the
    /// STARTING configuration for real ragdoll joints, which would
    /// immediately fight their own limits from the very first physics step.
    /// That plausibly reads as MORE dramatic "stretching" than a mere freeze.
    ///
    /// FIXED the same way as GrappleSwing.Attach/DevConsoleView.Open:
    /// PlayerLocomotion.SuppressImplausibleArmSnapsNow, called right before
    /// `Animator.enabled = false` in Ragdoll.Activate. Checked here the same
    /// way LocomotionAnimatorStabilityPlayTests checks the ordinary-frame
    /// case: real, changing locomotion input, a real PoseDistortionWatchdog
    /// watching, and Activate fired at several different, well-separated
    /// points in the transition space (a fresh player per point, since
    /// Activate is a one-way trip for that instance -- standing back up is
    /// real machinery this test does not need to exercise).
    /// </summary>
    public sealed class RagdollAnimatorStabilityPlayTests
    {
        private GameObject _player;
        private GameObject _ground;
        private string _logPath;

        private const float StepSeconds = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            // See LocomotionAnimatorStabilityPlayTests's own TearDown
            // comment for why this is set and why it must always be cleared.
            Time.captureDeltaTime = 0.0f;
            DestroyCurrentPlayer();
        }

        private void DestroyCurrentPlayer()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            _player = null;
            _ground = null;
        }

        private (PlayerLocomotion locomotion, Ragdoll ragdoll) SpawnPlayer()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "TestGround";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 2.0f, 0.0f), Quaternion.identity);
            Animator animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Player.prefab has no Animator");
            PlayerLocomotion locomotion = _player.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(locomotion, "Player.prefab has no PlayerLocomotion");
            locomotion.Bind();
            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "Player.prefab has no Ragdoll");
            ragdoll.Bind();

            PoseDistortionWatchdog watchdog = _player.AddComponent<PoseDistortionWatchdog>();
            watchdog.Locomotion = locomotion;
            watchdog.Bind();

            // A clean slate -- this file is a real, shared append-log, not
            // something TearDown can reset any other way (same idiom as
            // LocomotionAnimatorStabilityPlayTests's own SpawnPlayer).
            _logPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "pose_distortion_watchdog.log");
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }

            return (locomotion, ragdoll);
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            return (null, null);
#endif
        }

        [UnityTest]
        public IEnumerator ActivatingDuringChangingLiveMovementNeverHandsPhysicsAnImplausibleStartingPose()
        {
            Time.captureDeltaTime = StepSeconds;
            var rng = new System.Random(20260910);

            // A handful of well-separated activation points -- a fresh
            // player each time, since Activate is a one-way trip for that
            // instance. Varying the burst length before each Activate lands
            // it at a different point in the locomotion blend tree's own
            // transition space every time, the same idea
            // GrappleSwingPosePlayTests.AttachingDuringChangingLiveMovement
            // NeverCapturesAnImplausibleBase uses for Attach.
            int[] burstLengths = { 47, 113, 61, 89, 24 };

            foreach (int burstLength in burstLengths)
            {
                (PlayerLocomotion locomotion, Ragdoll ragdoll) = SpawnPlayer();

                for (int i = 0; i < 120 && !locomotion.IsGrounded; i++)
                {
                    locomotion.Tick(default, StepSeconds);
                    yield return null;
                }
                Assert.IsTrue(locomotion.IsGrounded, "setup failed -- never actually landed on the test ground");

                for (int i = 0; i < burstLength; i++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float magnitude = (float)rng.NextDouble();
                    var input = new LocomotionInput
                    {
                        Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude,
                        YawDegrees = (float)(rng.NextDouble() * 360.0),
                        Sprint = rng.NextDouble() < 0.3,
                        Crouch = rng.NextDouble() < 0.15,
                        Aiming = rng.NextDouble() < 0.2,
                        Jump = rng.NextDouble() < 0.03,
                    };
                    locomotion.Tick(input, StepSeconds);
                    yield return null;
                }

                // The same trigger PlayerVitals.TriggerFinisherKnockdown/a
                // real fall/crash uses -- landing right at the end of a
                // real, still-changing locomotion burst.
                ragdoll.Activate(new Vector3(2.0f, 3.0f, 1.0f));
                // Lets PoseDistortionWatchdog's own LateUpdate (a real
                // MonoBehaviour, run by Unity's own player loop -- no
                // coroutine resynchronisation needed) observe whatever
                // Activate just wrote before this iteration moves on.
                yield return null;

                DestroyCurrentPlayer();
            }

            string snapshot = File.Exists(_logPath) ? File.ReadAllText(_logPath) : null;
            Assert.IsFalse(File.Exists(_logPath),
                "PoseDistortionWatchdog caught an implausible pose the instant Ragdoll.Activate fired during " +
                "live, changing movement -- real ragdoll physics would have started from that bad pose instead " +
                $"of a plausible one.\n{snapshot}");
        }
    }
}
