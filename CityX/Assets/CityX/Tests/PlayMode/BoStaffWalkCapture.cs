using System.Collections;
using System.IO;
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
    /// Not a test: the player in the parka walking and crouch-walking with the bo staff, and standing with it, in the real scene, seen through the game's own
    /// camera, from the side and from the front, eight moments of each (owner, 2026-11-22: "the walking animation has the arms clipping through the
    /// body"). Written to Logs/CityShots/bo-staff-walk-sheet.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BoStaffWalkCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int CellWidth = 300;
        private const int CellHeight = 300;
        private const int Moments = 8;
        private const int Views = 3;

        private Scene _scene;
        private Keyboard _keyboard;

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
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RenderTheBoStaffCarriedWalkingAndCrouching()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            GameObject player = input.gameObject;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Time.captureFramerate = 60;
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            vitals.InfiniteStamina = true;
            var inventory = player.GetComponent<Inventory>();
            inventory.EquipItemId(ItemRegistry.BoStaff, player);
            Camera game = Camera.main;
            var other = new GameObject("OtherCam").AddComponent<Camera>();
            other.fieldOfView = 30.0f;
            other.enabled = false;

            // Rows, all in the parka: walking with the staff, crouch-walking with it, and standing still with it.
            const int rows = 3;
            inventory.EquipItemId(ItemRegistry.GreenFurHoodedParka, player);
            var sheet = new Texture2D(CellWidth * Moments, CellHeight * Views * rows, TextureFormat.RGB24, false);
            for (int row = 0; row < rows; row++)
            {
                bool crouch = row == 1, idle = row == 2;
                if (!idle)
                {
                    Press(_keyboard.wKey);
                }
                yield return null;
                if (crouch)
                {
                    Press(_keyboard.leftCtrlKey);
                }
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                }
                for (int shot = 0; shot < Moments; shot++)
                {
                    for (int f = 0; f < 5; f++)
                    {
                        yield return null;
                    }
                    int y = (rows - 1 - row) * Views * CellHeight;
                    Shoot(game, sheet, shot * CellWidth, y + 2 * CellHeight);
                    Vector3 chest = player.transform.position + Vector3.up * 1.5f;
                    other.transform.position = chest + player.transform.right * 4.5f + Vector3.up * 0.2f;
                    other.transform.LookAt(chest);
                    Shoot(other, sheet, shot * CellWidth, y + CellHeight);
                    other.transform.position = chest + player.transform.forward * 4.5f + player.transform.right * -1.2f + Vector3.up * 0.2f;
                    other.transform.LookAt(chest);
                    Shoot(other, sheet, shot * CellWidth, y);
                }
                if (crouch)
                {
                    Release(_keyboard.leftCtrlKey);
                    yield return null;
                }
                if (!idle)
                {
                    Release(_keyboard.wKey);
                }
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                }
            }
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, "bo-staff-walk-sheet.png"), (byte[])encode.Invoke(null, new object[] { sheet }));
            Object.Destroy(other.gameObject);
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, Texture2D sheet, int x, int y)
        {
            RenderTexture target = RenderTexture.GetTemporary(CellWidth, CellHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture before = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), x, y);
            RenderTexture.active = null;
            camera.targetTexture = before;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
