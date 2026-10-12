using System.Collections;
using System.IO;
using CityX.Character;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, round 8 of the console/grapple stretch saga
    /// (see grapple_swing_deprioritized memory): round 7 fixed a real
    /// permanent-lockup bug in PlayerLocomotion.SuppressBoneSnap, but the
    /// user reported the glitch STILL happening afterward. A fresh
    /// pose_distortion_watchdog.log entry taken right after that fix
    /// shipped showed something round 7 never considered: five violations
    /// in under 30 seconds, each at a wildly different player position
    /// roughly 20-40 metres apart -- far faster than running or even
    /// sprinting, and the kind of distance covered by JETPACK FLIGHT, not
    /// ground locomotion. Every prior test in this whole saga (
    /// LocomotionAnimatorStabilityPlayTests, GrappleSwingPosePlayTests,
    /// DevConsoleAnimatorFreezePlayTests) drives ground movement input --
    /// Move/Sprint/Crouch/Aiming/Jump -- and even Jump only produces a
    /// brief, ordinary hop. NONE of them ever equip and fly the real
    /// jetpack with realistic, changing thrust/boost/pitch input, which is
    /// a genuinely different Animator Controller path
    /// (JetpackThrusting/JetpackBoosting/JetpackDiving, see
    /// PlayerInputReader.Update's own dispatch) that could plausibly
    /// produce its own large, fast blend-tree transitions never covered by
    /// any existing regression.
    ///
    /// THE REAL SCENE, THE REAL JETPACK, EQUIPPED THROUGH THE REAL CONSOLE --
    /// same technique JetpackRealScenePlayTests already established, so any
    /// finding here reflects the actual shipped Jetpack.prefab/Animator
    /// Controller, not a synthetic stand-in.
    /// </summary>
    public sealed class JetpackAnimatorStabilityPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private string _logPath;

        private const float StepSeconds = 1.0f / 60.0f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 1.0f / SteadyFrameTime.FramesPerSecond;   // back to the run's steady frame time, not off
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
            if (_logPath != null && File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }
        }

        [UnityTest]
        public IEnumerator RealisticChangingFlightNeverSnapsAnArmOrHeadBoneOnItsOwn()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(console, "no DevConsoleView in the real saved scene");
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            Assert.IsNotNull(input, "no PlayerInputReader in the real saved scene");
            var locomotion = Object.FindFirstObjectByType<PlayerLocomotion>();
            Assert.IsNotNull(locomotion, "no PlayerLocomotion in the real saved scene");

            console.Submit("/equip jetpack");
            yield return null;
            Assert.IsNotNull(input.EquippedJetpack, "/equip jetpack did not actually equip one");
            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack, "no live Jetpack component after equipping");

            var watchdog = locomotion.gameObject.AddComponent<PoseDistortionWatchdog>();
            watchdog.Locomotion = locomotion;
            watchdog.Bind();
            _logPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "pose_distortion_watchdog.log");
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }

            // Realistic gameplay pace, not batch mode's own uncapped real
            // frame timing -- same rule every stability test in this saga
            // already follows.
            Time.captureDeltaTime = StepSeconds;

            var rng = new System.Random(20260918);

            // 10 real seconds of actual flight: thrust mostly held (a
            // player flying holds Space, not taps it), boost/dive toggled
            // occasionally, camera pitch swinging through the jetpack's own
            // widened near-vertical range, direction changing continuously
            // -- exactly the kind of sustained, fast, changing 3D movement
            // the real watchdog log showed happening (20+ m/s between
            // samples) when this bug reappeared after round 7.
            for (int i = 0; i < 600; i++)
            {
                bool thrustHeld = rng.NextDouble() < 0.9;
                bool boostHeld = rng.NextDouble() < 0.3;
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float magnitude = (float)rng.NextDouble();
                Vector2 move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude;
                float cameraPitch = (float)(rng.NextDouble() * 160.0 - 80.0);

                jetpack.Tick(thrustHeld, move, boostHeld, StepSeconds, cameraPitch);

                var locoInput = new LocomotionInput
                {
                    Move = move,
                    YawDegrees = (float)(rng.NextDouble() * 360.0),
                    JetpackThrusting = thrustHeld,
                    JetpackBoosting = thrustHeld && boostHeld,
                    JetpackDiving = thrustHeld && boostHeld && move.y > 0.0f,
                };
                locomotion.Tick(locoInput, StepSeconds);
                yield return null;
            }

            Assert.IsFalse(File.Exists(_logPath),
                "PoseDistortionWatchdog caught an implausible pose during realistic, sustained jetpack " +
                "flight, with nothing ever disabling the Animator -- a real controller instability that a " +
                "console/grapple freeze would only be making visible, not causing. See " +
                "CityX/Logs/pose_distortion_watchdog.log for the exact snapshot.");
#else
            yield break;
#endif
        }
    }
}
