using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Bug report, round 8 of the console/grapple stretch saga
    /// (see grapple_swing_deprioritized memory): round 7 fixed a real
    /// permanent-lockup bug, but the user reported the glitch STILL
    /// happening. A fresh watchdog log entry taken right after showed FIVE
    /// violations in under 30 seconds, each roughly 20-40 metres apart
    /// (up to ~19 m/s between samples) with Swing.IsActive: False every
    /// time -- far faster than PlayerLocomotion.SprintSpeed (7.5 m/s) can
    /// ever produce on its own. Two other real-scene stability tests
    /// (jetpack flight, sustained real-city sprinting) both came back
    /// clean, ruling those out.
    ///
    /// THE ACTUAL SPEED REGIME THIS POINTS AT: GrappleSwing.MaxSwingSpeed
    /// is 35 m/s -- nearly 5x sprint -- and GrappleSwing.Release hands that
    /// velocity to PlayerLocomotion.SetResidualVelocity, which explicitly
    /// "rides on top of the ordinary WASD blend" and persists untouched
    /// until the body lands (see that method's own doc comment). This is a
    /// genuinely distinct, high-speed locomotion regime that ordinary
    /// ground movement never reaches on its own, driven by the SAME
    /// Animator Speed parameter/blend tree that was almost certainly tuned
    /// and tested against the 0-7.5 m/s range only. Every existing swing
    /// test (GrappleSwingPosePlayTests) checks the ATTACH moment and the
    /// short 0.15s post-release blend; NONE of them keep driving ordinary
    /// PlayerLocomotion.Tick for several real seconds afterward while the
    /// residual velocity decays, at real, PLAUSIBLE, correlated player
    /// input (not console, not held mid-swing) -- exactly what a player
    /// releasing a swing and continuing to fly/slide through the city on
    /// momentum would actually be doing when Swing.IsActive already reads
    /// False.
    /// </summary>
    public sealed class PostSwingHighSpeedAnimatorStabilityPlayTests
    {
        private GameObject _player;
        private GameObject _ground;
        private PlayerLocomotion _locomotion;
        private GrappleSwing _swing;
        private string _logPath;

        private const float StepSeconds = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 1.0f / SteadyFrameTime.FramesPerSecond;   // back to the run's steady frame time, not off
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            if (_logPath != null && File.Exists(_logPath)) File.Delete(_logPath);
        }

        private void SpawnPlayer()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "TestGround";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);

            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 2.0f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(_locomotion, "Player.prefab has no PlayerLocomotion");
            _locomotion.Bind();

            _swing = _player.AddComponent<GrappleSwing>();
            _swing.Bind();
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
#endif
        }

        [UnityTest]
        public IEnumerator FlyingOnHighResidualVelocityAfterAReleaseNeverSnapsAnArmOrHeadBoneOnItsOwn()
        {
#if UNITY_EDITOR
            SpawnPlayer();

            var watchdog = _player.AddComponent<PoseDistortionWatchdog>();
            watchdog.Locomotion = _locomotion;
            watchdog.Swing = _swing;
            watchdog.Bind();
            _logPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "pose_distortion_watchdog.log");
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }

            Time.captureDeltaTime = StepSeconds;

            for (int i = 0; i < 60 && !_locomotion.IsGrounded; i++)
            {
                _locomotion.Tick(default, StepSeconds);
                yield return null;
            }
            Assert.IsTrue(_locomotion.IsGrounded, "setup failed -- never actually landed on the test ground");

            // A real swing, high above the ground, with sustained real
            // input so the rope's own SwingAccel has real time to build up
            // genuine speed toward MaxSwingSpeed rather than releasing
            // almost immediately at a near-zero velocity.
            _swing.Attach(_player.transform.position + new Vector3(20.0f, 30.0f, 10.0f));
            var rng = new System.Random(20260918);
            for (int i = 0; i < 180 && _swing.IsActive; i++)
            {
                float t = i / 60.0f;
                Vector2 move = new Vector2(Mathf.Sin(t * 0.9f), 1.0f);
                float yaw = i * 3.0f;
                _swing.Tick(move, yaw, false, false, false, false, StepSeconds);
                yield return null;
            }

            float releaseSpeed = _swing.Velocity.magnitude;
            _swing.Release(true); // boosted -- the release a player mashing the button for max distance would use
            Assert.IsFalse(_swing.IsActive, "setup failed -- the swing never actually released");

            // The actual scenario the real watchdog log matches: ordinary
            // PlayerLocomotion.Tick, real changing WASD input, Swing.IsActive
            // already False, for several real seconds while high residual
            // velocity (SetResidualVelocity) decays -- not console, not
            // held mid-swing, not the short 0.15s release blend window.
            for (int i = 0; i < 300; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float magnitude = (float)rng.NextDouble();
                var input = new LocomotionInput
                {
                    Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude,
                    YawDegrees = (float)(rng.NextDouble() * 360.0),
                    Sprint = rng.NextDouble() < 0.3,
                    Jump = rng.NextDouble() < 0.03,
                };
                _locomotion.Tick(input, StepSeconds);
                yield return null;
            }

            Assert.IsFalse(File.Exists(_logPath),
                $"PoseDistortionWatchdog caught an implausible pose while flying on high residual velocity " +
                $"(released at {releaseSpeed:F1} m/s) after a swing, with nothing ever disabling the Animator " +
                "-- a real controller instability at a speed ordinary ground movement never reaches on its " +
                "own. See CityX/Logs/pose_distortion_watchdog.log for the exact snapshot.");
#else
            yield break;
#endif
        }
    }
}
