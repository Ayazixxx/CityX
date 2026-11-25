using System.Collections;
using System.IO;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the heat press at rest from the front, the side and above, then moments of one pressing (the drawer out for the shirt, the
    /// clamp, the steam, the shirt out done), with the player beside it for size. Written to Logs/CityShots/heatpress-*.png. Explicit; needs
    /// -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class HeatPressCapture
    {
        [UnityTest]
        public IEnumerator RenderTheHeatPress()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 35.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject made = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/HeatPress/HeatPress.prefab"));
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(-1.1f, 0.05f, 0.6f), Quaternion.Euler(0.0f, 120.0f, 0.0f));
            player.GetComponent<Character.PlayerInputReader>().enabled = false;
            var press = made.GetComponentInChildren<HeatPress>();
            yield return null;
            Vector3 top = new Vector3(0.0f, HeatPressShape.TableHeight + 0.25f, 0.0f);
            BuildingShapeCapture.Shoot(camera, folder, "heatpress-front", top + new Vector3(0.9f, 0.5f, 2.2f), top);
            BuildingShapeCapture.Shoot(camera, folder, "heatpress-side", top + new Vector3(2.3f, 0.3f, 0.0f), top);
            BuildingShapeCapture.Shoot(camera, folder, "heatpress-above", top + new Vector3(0.6f, 1.6f, 1.4f), top);
            BuildingShapeCapture.Shoot(camera, folder, "heatpress-withplayer", new Vector3(1.6f, 2.0f, 4.5f), new Vector3(-0.4f, 1.2f, 0.0f));

            press.Begin();
            float[] moments = { 0.75f, 1.62f, 4.0f, HeatPressCycle.SteamAt + 0.35f, HeatPressCycle.SteamAt + 1.1f, HeatPressCycle.SteamAt + 1.6f };
            string[] names = { "loading", "closing", "clamped", "steam", "steam2", "done" };
            float clock = 0.0f;
            for (int i = 0; i < moments.Length; i++)
            {
                while (clock < moments[i])
                {
                    yield return null;
                    clock += 1.0f / 60.0f;
                }
                BuildingShapeCapture.Shoot(camera, folder, $"heatpress-{names[i]}", top + new Vector3(1.4f, 0.5f, 1.8f), top);
            }

            Time.captureFramerate = 0;
            Object.Destroy(player);
            Object.Destroy(made);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }

        /// <summary>
        /// The press screen with a white T-shirt: a stroke drawn and a picture being laid on; then the shirt pressed (on the drawer at the steam)
        /// and worn, front and back. Written to Logs/CityShots/heatpress-print-*.png.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderPrintingAShirt()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string folder = Path.Combine(Path.GetTempPath(), "cityx-press-capture");
            Character.DesignLibrary.Store = new Core.DesignStore(folder);
            Character.DesignLibrary.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, 160.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 35.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string shots = BuildingShapeCapture.ShotsFolder;

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(-2.4f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<Character.PlayerInputReader>().enabled = false;
            Character.Inventory inventory = HeatPressPlayTests.AddInventory(player);
            inventory.EquipItemId(Items.ItemRegistry.TShirt, player);
            GameObject made = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/HeatPress/HeatPress.prefab"),
                new Vector3(0.0f, 0.0f, 1.6f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            var press = made.GetComponentInChildren<HeatPress>();
            var viewObject = new GameObject("HeatPressView");
            var view = viewObject.AddComponent<UI.HeatPressView>();
            yield return null;

            view.Open(press, player);
            view.SetColour(new Color(0.9f, 0.1f, 0.15f));
            view.Canvas.BeginChange(ShirtDesignCanvas.Side.Front);
            view.Canvas.Stroke(ShirtDesignCanvas.Side.Front, new Vector2(60, 470), new Vector2(390, 470), 14.0f, view.Colour, false);
            view.BeginPicture(TestPicture());
            view.TurnPicture(-12.0f);
            yield return null;
            Canvas overlay = view.Overlay;
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = camera;
            overlay.planeDistance = 0.5f;
            yield return null;
            BuildingShapeCapture.Shoot(camera, shots, "heatpress-print-screen", new Vector3(0.0f, 50.0f, 0.0f), new Vector3(0.0f, 50.0f, 1.0f));
            view.PlacePicture();
            view.TurnShirt();
            view.Canvas.BeginChange(ShirtDesignCanvas.Side.Back);
            view.Canvas.Stroke(ShirtDesignCanvas.Side.Back, new Vector2(224, 150), new Vector2(224, 450), 40.0f, new Color(0.1f, 0.3f, 0.9f), false);
            yield return null;
            BuildingShapeCapture.Shoot(camera, shots, "heatpress-print-screen-back", new Vector3(0.0f, 50.0f, 0.0f), new Vector3(0.0f, 50.0f, 1.0f));
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;

            Assert.IsTrue(view.PressIt(), view.Status);
            Vector3 top = press.transform.position + Vector3.up * 0.2f;
            float clock = 0.0f;
            while (clock < HeatPressCycle.SteamAt + HeatPressCycle.OpenSeconds + HeatPressCycle.SlideSeconds + 0.3f)
            {
                yield return null;
                clock += 1.0f / 60.0f;
            }
            BuildingShapeCapture.Shoot(camera, shots, "heatpress-print-drawer", top + new Vector3(0.9f, 1.2f, -1.6f), top + new Vector3(0.0f, 0.0f, -0.3f));
            while (clock < HeatPressCycle.TotalSeconds + 0.5f)
            {
                yield return null;
                clock += 1.0f / 60.0f;
            }
            for (int i = 0; i < 20; i++) yield return null;
            Vector3 chest = player.transform.position + Vector3.up * 1.6f;
            BuildingShapeCapture.Shoot(camera, shots, "heatpress-print-worn-front", chest + new Vector3(0.0f, 0.2f, 5.0f), chest);
            BuildingShapeCapture.Shoot(camera, shots, "heatpress-print-worn-back", chest + new Vector3(0.0f, 0.2f, -5.0f), chest);

            Time.captureFramerate = 0;
            Object.Destroy(viewObject);
            Object.Destroy(player);
            Object.Destroy(made);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(ground);
            Character.DesignLibrary.Store = null;
            Character.DesignLibrary.Reset();
#else
            yield break;
#endif
        }

        /// <summary>A stand-in for a picture off the computer: a yellow ring with a magenta star in it, see-through round it.</summary>
        private static Texture2D TestPicture()
        {
            const int size = 256;
            var picture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var d = new Vector2(x - size * 0.5f, y - size * 0.5f);
                    float r = d.magnitude, angle = Mathf.Atan2(d.y, d.x);
                    float star = 60.0f + 30.0f * Mathf.Cos(angle * 5.0f);
                    if (r > 100.0f && r < 124.0f) pixels[y * size + x] = new Color32(250, 210, 20, 255);
                    else if (r < star) pixels[y * size + x] = new Color32(220, 30, 200, 255);
                }
            }
            picture.SetPixels32(pixels);
            picture.Apply();
            return picture;
        }
    }
}
