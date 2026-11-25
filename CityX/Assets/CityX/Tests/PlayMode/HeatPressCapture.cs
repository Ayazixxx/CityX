using System.Collections;
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
    }
}
