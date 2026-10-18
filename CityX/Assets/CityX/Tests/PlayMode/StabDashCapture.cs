using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: films the stab dash in the real scene on the frames that matter (StabDashMove's, which are Smash's), from the side as Smash
    /// shows it and from behind as the game's camera does, following the body, and from one fixed point beside the slide so its length shows, to
    /// Logs/CityShots/stabdash-*.png, so the animation can be LOOKED AT (green tests are no proof that it looks right). Explicit, so the suite
    /// skips it; run it by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.StabDashCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StabDashCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private static readonly int[] Frames = { 1, 4, 9, 11, 12, 13, 16, 22, 30, 35, 45, 52, 58 };
        private static readonly int[] FixedFrames = { 9, 13, 18, 24, 35 };
        private Scene _scene;
        private Keyboard _keyboard;
        private readonly List<GameObject> _made = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheStabDash()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<CityX.View.DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = CityX.View.SkyCycle.DaySeconds * 0.5f;   // noon: the street out of the buildings' shadow
                cycle.Apply(CityX.View.SkyCycle.At(cycle.TimeOfDay));
            }

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            var stab = vitals.GetComponent<StabDashAbility>();
            Inventory inventory = vitals.GetComponent<Inventory>();
            inventory.AddItem(ItemRegistry.Sword, 1);
            inventory.RemoveFromStorage(ItemRegistry.Sword, 1, out ItemInstanceData data);
            inventory.EquipItemId(ItemRegistry.Sword, vitals.gameObject, data);
            PlayerSkills.On(vitals.gameObject).PutOnBar(0, SkillCatalog.StabDash);
            vitals.InfiniteHealth = true;
            Time.captureFramerate = 60;   // frame-exact: the animation is timed
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Vector3 heading = OpenRun.Face(vitals, _made, 12.0f);
            // Nobody else in the shots (the Old Guy waits by the spawn).
            foreach (UniqueNpc npc in Object.FindObjectsByType<UniqueNpc>(FindObjectsSortMode.None))
            {
                npc.gameObject.SetActive(false);
            }
            foreach (Civilian walker in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
            {
                walker.gameObject.SetActive(false);
            }
            for (int i = 0; i < 20; i++)
            {
                yield return null;   // turned to the run
            }

            var cameraObject = new GameObject("StabDashCaptureCamera");
            _made.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 45.0f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600.0f;
            camera.allowHDR = true;
            Transform body = vitals.transform;
            Vector3 middle = body.position + heading * (StabDashMove.SlideMetres * 0.5f) + Vector3.up * 1.2f;
            // Shot from whichever side of the run is open (a building's wall may be close on the other).
            Vector3 side = Vector3.Cross(Vector3.up, heading).normalized;
            if (Physics.Raycast(middle, side, 9.0f))
            {
                side = -side;
            }

            Press(_keyboard.digit1Key);
            yield return null;
            Release(_keyboard.digit1Key);
            var shots = new SortedSet<int>(Frames);
            shots.UnionWith(FixedFrames);
            foreach (int frame in shots)
            {
                // A coroutine resumes before the frame's pose is laid on the body: one frame more shows frame `frame`.
                for (int i = 0; i < 200 && stab.IsActive && stab.Elapsed < (frame + 1) / 60.0f - 1e-4f; i++)
                {
                    yield return null;
                }
                Vector3 centre = body.position + Vector3.up * 1.2f + heading * 0.6f;
                if (System.Array.IndexOf(Frames, frame) >= 0)
                {
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"stabdash-f{frame:00}-side", centre + side * 6.5f + Vector3.up * 0.3f, centre);
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"stabdash-f{frame:00}-behind", centre - heading * 5.0f + Vector3.up * 2.0f, centre + heading * 1.5f);
                }
                if (System.Array.IndexOf(FixedFrames, frame) >= 0)
                {
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"stabdash-fixed-f{frame:00}", middle + side * 8.5f + Vector3.up * 0.5f, middle);
                }
            }
            Debug.Log($"CityX: stab dash capture wrote to {BuildingShapeCapture.ShotsFolder}; slid {stab.Slid:0.00} m, stopped {stab.Stopped}");
#else
            yield break;
#endif
        }
    }
}
