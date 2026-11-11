using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the intro in the real city, through the game's own camera, a picture every second and a half from the drop to the hand-off,
    /// every quarter second while the body gets up (Logs/CityShots/intro-NN.png), to look at the cloud thinning, the ragdoll's fall and
    /// landing, the get-up and the camera's glide.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.IntroCutsceneCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class IntroCutsceneCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int FramesBetweenShots = 90;
        private const int FramesBetweenGetUpShots = 15;
        private const int MaxFrames = 60 * 60;

        private Scene _scene;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator FilmTheIntro()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            BuildingShapeCapture.UseTheScenesAir();

            var locomotion = Object.FindFirstObjectByType<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            GameObject player = locomotion.gameObject;
            player.GetComponent<PlayerInputReader>().enabled = false;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            follow.enabled = false;
            Camera camera = follow.GetComponent<Camera>();

            var ragdoll = player.GetComponent<Ragdoll>();
            var cutscene = player.AddComponent<IntroCutscene>();
            bool finished = false;
            cutscene.Finished += () => finished = true;
            cutscene.Begin(player.GetComponent<CharacterController>(), follow, player.GetComponentInChildren<Animator>(), camera.transform, locomotion);
            for (int frame = 0, shot = 0; frame < MaxFrames && !finished; frame++)
            {
                yield return null;
                if (frame % (ragdoll.IsStandingUp ? FramesBetweenGetUpShots : FramesBetweenShots) == 0)
                {
                    Transform view = camera.transform;
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"intro-{shot++:00}", view.position, view.position + view.forward);
                }
            }
            Assert.IsTrue(finished, "the intro never finished");
#else
            yield break;
#endif
        }
    }
}
