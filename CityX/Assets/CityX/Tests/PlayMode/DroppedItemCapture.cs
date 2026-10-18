using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the swordsman cut down in the real city and his combat sword falling from his hand to the street, filmed from the side at
    /// moments through the fall (dropped-sword-tNN.png) and lying there from the player's own view (dropped-sword-lying.png). Owner, 2026-10-17:
    /// "make it so enemies and players drop their weapon they are holding when they die". Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.DroppedItemCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class DroppedItemCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private static readonly int[] FilmFrames = { 0, 4, 8, 12, 18, 24, 36, 48, 72, 120 };

        private Scene _scene;
        private GameObject _swordsman;
        private Camera _camera;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_swordsman != null) Object.Destroy(_swordsman);
            if (_camera != null) Object.Destroy(_camera.gameObject);
            DroppedItems.Shared.Clear();
            Time.captureFramerate = 0;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheSwordFallingFromTheBeatenSwordsman()
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
            var cycle = Object.FindFirstObjectByType<CityX.View.DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = CityX.View.SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(CityX.View.SkyCycle.At(cycle.TimeOfDay));
            }

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            vitals.InfiniteHealth = true;
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            Transform body = vitals.transform;
            Vector3 forward = body.forward;

            _swordsman = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                body.position + forward * 3.0f + Vector3.up * 0.1f, Quaternion.LookRotation(-forward, Vector3.up));
            _swordsman.GetComponent<SwordsmanBrain>().enabled = false;   // he stands for it
            var foe = _swordsman.GetComponent<PlayerVitals>();
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }

            _camera = new GameObject("DropCam").AddComponent<Camera>();
            _camera.fieldOfView = 40.0f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            _camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            // A killing cut from the player's side: the corpse is flung back and the sword leaves his hand.
            Vector3 chest = _swordsman.transform.position + Vector3.up * 1.6f;
            DamageContext.Strike(foe, 100000.0f, body.position, 0.6f, DamageKind.Physical, 0.0f, chest - forward * 0.3f);
            DroppedItem sword = null;
            foreach (DroppedItem item in DroppedItems.Shared.Shown)
            {
                sword = item;
            }
            Assert.IsNotNull(sword, "the beaten swordsman dropped nothing");
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 watch = _swordsman.transform.position + forward * 0.6f + Vector3.up * 1.0f;
            int frame = 0;
            foreach (int at in FilmFrames)
            {
                for (; frame < at + 1; frame++)
                {
                    yield return null;   // a coroutine resumes before the frame's poses: one frame more shows frame `at`
                }
                CaptureShots.Shoot(_camera, $"dropped-sword-t{at:000}", watch + side * 6.5f + Vector3.up * 0.6f, watch);
            }
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            Vector3 lying = sword.Centre;
            CaptureShots.Shoot(_camera, "dropped-sword-lying", body.position + Vector3.up * 4.2f - forward * 1.5f + side * 1.8f, lying);
            CaptureShots.Shoot(_camera, "dropped-sword-close", lying + new Vector3(0.0f, 1.4f, 0.0f) - forward * 1.6f + side * 0.8f, lying);
            Debug.Log($"CityX: dropped sword lies at {lying}, {Vector3.Distance(lying, _swordsman.transform.position):0.00} m from the corpse, " +
                      $"lowest point {sword.Solid.bounds.min.y - body.position.y:0.00} m above the player's feet");
#else
            yield break;
#endif
        }
    }
}
