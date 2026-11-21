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
    /// Not a test: the player in the parka swinging the fists, the combat sword, the bo staff and the sledgehammer in the real scene, seen through the
    /// game's own camera and from the side, eight moments of each (owner, 2026-11-21: "the knee clips through the jacket", "make it look good
    /// ingame"). Written to Logs/CityShots/coat-legs-sheet.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CoatLegsCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int CellWidth = 360;
        private const int CellHeight = 300;
        private const int Moments = 8;

        private static readonly string[] Weapons = { null, ItemRegistry.CombatSword, ItemRegistry.BoStaff, ItemRegistry.Sledgehammer };

        private Scene _scene;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
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
        public IEnumerator RenderTheCoatThroughMeleeSwings()
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
            inventory.EquipItemId(ItemRegistry.GreenFurHoodedParka, player);
            Camera game = Camera.main;
            var side = new GameObject("SideCam").AddComponent<Camera>();
            side.fieldOfView = 30.0f;
            side.enabled = false;

            var sheet = new Texture2D(CellWidth * Moments, CellHeight * Weapons.Length * 2, TextureFormat.RGB24, false);
            for (int row = 0; row < Weapons.Length; row++)
            {
                if (Weapons[row] != null)
                {
                    inventory.EquipItemId(Weapons[row], player);
                }
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                }
                int shot = 0;
                for (int swing = 0; swing < 4 && shot < Moments; swing++)
                {
                    Press(_mouse.leftButton);
                    yield return null;
                    Release(_mouse.leftButton);
                    for (int f = 0; f < 21 && shot < Moments; f++)
                    {
                        yield return null;
                        if (f % 10 != 4)
                        {
                            continue;
                        }
                        int y = (Weapons.Length - 1 - row) * 2 * CellHeight;
                        Shoot(game, sheet, shot * CellWidth, y + CellHeight);
                        Vector3 hips = player.transform.position + Vector3.up * 1.0f;
                        side.transform.position = hips + player.transform.right * 5.0f + player.transform.forward * -1.5f + Vector3.up * 0.3f;
                        side.transform.LookAt(hips);
                        Shoot(side, sheet, shot * CellWidth, y);
                        shot++;
                    }
                }
                if (Weapons[row] != null)
                {
                    string slot = inventory.EquippedItemId(Inventory.PrimarySlot) == Weapons[row] ? Inventory.PrimarySlot : Inventory.SecondarySlot;
                    inventory.UnequipToSlot(slot, player);
                }
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                }
            }
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, "coat-legs-sheet.png"), (byte[])encode.Invoke(null, new object[] { sheet }));
            Object.Destroy(side.gameObject);
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
