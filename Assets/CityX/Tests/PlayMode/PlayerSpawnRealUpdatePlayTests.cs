using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, reported AGAIN after
    /// PlayerSpawnGroundedPlayTests was already green: "i loaded in as
    /// ayazixxx and [w]ent through the floor again." That test drives
    /// PlayerLocomotion.Tick directly and explicitly DISABLES
    /// PlayerInputReader (its own doc comment: Keyboard.current is null in
    /// the test runner, so PlayerInputReader.Update() would never call
    /// Tick at all) -- which proves the real saved scene's own spawn point
    /// and collision mesh agree, but proves NOTHING about whatever
    /// PlayerInputReader's own real Update() dispatch does before/around
    /// that call. This test closes that exact gap: a REAL simulated
    /// keyboard/mouse (UnityEngine.InputSystem's own InputTestFixture, the
    /// same technique GrappleSwingRealInputPlayTests already established
    /// for "every other test calls Tick directly, only this one drives the
    /// real dispatch") with PlayerInputReader left fully ENABLED, doing
    /// nothing but existing -- exactly what a player who has just loaded in
    /// and not touched a key yet experiences.
    /// </summary>
    public sealed class PlayerSpawnRealUpdatePlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator TheRealPlayerSpawnSettlesOnTheGroundThroughTheRealInputDispatch()
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
            var controller = locomotion.GetComponent<CharacterController>();
            Assert.IsNotNull(controller, "the real player has no CharacterController");
            var inputReader = locomotion.GetComponent<PlayerInputReader>();
            Assert.IsNotNull(inputReader, "no PlayerInputReader on the real player");
            Assert.IsTrue(inputReader.enabled, "setup failed -- the real reader should be live for this test");

            float startY = locomotion.transform.position.y;
            float lowestY = startY;
            bool everGrounded = false;

            // Real frames, real Update() dispatch, no key ever pressed --
            // 5 real seconds is generous headroom over the 300-step/60fps
            // window PlayerSpawnGroundedPlayTests itself uses.
            float elapsed = 0.0f;
            while (elapsed < 5.0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float y = locomotion.transform.position.y;
                if (y < lowestY)
                {
                    lowestY = y;
                }
                if (controller.isGrounded)
                {
                    everGrounded = true;
                }
            }

            float finalY = locomotion.transform.position.y;
            TestContext.WriteLine($"spawn Y={startY:F2}, lowest Y reached={lowestY:F2}, " +
                $"final Y={finalY:F2}, isGrounded at end={controller.isGrounded}");

            Assert.IsTrue(everGrounded,
                $"the real player never reported isGrounded across 5 real seconds through the real " +
                "PlayerInputReader dispatch -- this is exactly \"loaded in and went through the floor\"");
            Assert.Greater(finalY, startY - 10.0f,
                $"the real player dropped from {startY:F2}m to {finalY:F2}m and kept dropping through " +
                "the real PlayerInputReader dispatch");
#else
            yield break;
#endif
        }
    }
}
