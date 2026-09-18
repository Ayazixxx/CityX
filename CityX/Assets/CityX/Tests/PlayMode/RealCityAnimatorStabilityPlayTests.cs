using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, round 8 of the console/grapple stretch saga
    /// (see grapple_swing_deprioritized memory): round 7 fixed a real
    /// permanent-lockup bug, but the user reported the glitch STILL
    /// happening. A fresh watchdog log entry taken right after showed FIVE
    /// violations in under 30 seconds, each ~20-40m apart -- far faster
    /// than the flat, empty test ground LocomotionAnimatorStabilityPlayTests
    /// runs on could ever produce naturally. That test's own randomised
    /// input can only ever trigger a brief, ordinary hop (Jump on a 3%
    /// coin flip, landing almost immediately on the flat plane) -- it has
    /// never once let a real, sustained, multi-second FALL FROM HEIGHT
    /// happen, which this project's own real city (real ledges, stairs,
    /// elevated highways, terraced hillsides) makes routine. A genuinely
    /// different, longer "long fall" Animator path this whole saga's own
    /// history already flags as a real, distinct state
    /// (PoseDistortionWatchdogPlayTests/this file's own memory: "The
    /// Animator's Falling state looking dramatic when frozen") has never
    /// actually been stress-tested for bone-snap stability the way ordinary
    /// ground locomotion has.
    ///
    /// THE REAL SCENE, THE REAL BAKED PLAYER, REAL TERRAIN -- not a flat
    /// synthetic plane, so any fall/landing this produces is whatever the
    /// actual shipped city geometry actually does, not a stand-in.
    /// </summary>
    public sealed class RealCityAnimatorStabilityPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private string _logPath;

        private const float StepSeconds = 1.0f / 60.0f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0.0f;
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
        public IEnumerator SustainedRealCityMovementNeverSnapsAnArmOrHeadBoneOnItsOwn()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var locomotion = Object.FindFirstObjectByType<PlayerLocomotion>();
            Assert.IsNotNull(locomotion, "no PlayerLocomotion in the real saved scene");

            var watchdog = locomotion.gameObject.AddComponent<PoseDistortionWatchdog>();
            watchdog.Locomotion = locomotion;
            watchdog.Bind();
            _logPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "pose_distortion_watchdog.log");
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }

            Time.captureDeltaTime = StepSeconds;

            var rng = new System.Random(20260918);

            // 30 real seconds, heavy sprint bias and full-magnitude moves
            // (real players holding a direction, not gentle drifting) so
            // the body actually covers ground fast enough to reach real
            // ledges, stairs and elevation changes in the real city instead
            // of pacing around its own spawn point the whole time.
            for (int i = 0; i < 1800; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                var input = new LocomotionInput
                {
                    Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    YawDegrees = (float)(rng.NextDouble() * 360.0),
                    Sprint = rng.NextDouble() < 0.7,
                    Jump = rng.NextDouble() < 0.05,
                };
                locomotion.Tick(input, StepSeconds);
                yield return null;
            }

            Assert.IsFalse(File.Exists(_logPath),
                "PoseDistortionWatchdog caught an implausible pose during sustained movement through the " +
                "real city, with nothing ever disabling the Animator -- a real controller instability that " +
                "a console/grapple freeze would only be making visible, not causing. See " +
                "CityX/Logs/pose_distortion_watchdog.log for the exact snapshot.");
#else
            yield break;
#endif
        }
    }
}
