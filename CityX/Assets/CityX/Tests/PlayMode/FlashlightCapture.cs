using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the flashlight (docs/FLASHLIGHT.md) to Logs/CityShots/flashlight-*.png -- at night over the shoulder as the game's
    /// camera sees it (the beam on a wall, a body in it casting its shadow), from the side (the shaft in the air), from in front looking into it
    /// (the lens's glare), and by day. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.FlashlightCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FlashlightCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheFlashlight()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 4.0f, 14.0f);
            wall.transform.localScale = new Vector3(30.0f, 8.0f, 0.5f);
            var sunObject = new GameObject("Moon");
            Light moon = sunObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.04f;
            moon.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            float ambient = RenderSettings.ambientIntensity;
            RenderSettings.ambientIntensity = 0.12f;
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.05f);

            GameObject holder = Body(Vector3.zero);
            GameObject stranger = Body(new Vector3(-1.2f, 0.0f, 7.0f));
            stranger.transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            var torch = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Flashlight.prefab"))
                .GetComponent<FlashlightItem>();
            torch.OnEquip(holder, ItemInstanceData.None);
            Vector3 look = new Vector3(-0.08f, -0.12f, 1.0f).normalized;
            yield return Hold(holder, torch, true, 0.8f, look);

            Vector3 shoulder = holder.transform.position + Vector3.up * 2.9f + holder.transform.right * 0.9f - holder.transform.forward * 4.5f;
            yield return Shoot(camera, "flashlight-behind", shoulder, holder.transform.position + Vector3.up * 1.8f + look * 10.0f);
            yield return Shoot(camera, "flashlight-side", new Vector3(9.0f, 3.0f, 4.0f), new Vector3(0.0f, 1.8f, 5.0f));
            yield return Shoot(camera, "flashlight-front", new Vector3(-0.6f, 1.9f, 9.0f), holder.transform.position + Vector3.up * 2.0f);

            moon.intensity = 1.3f;
            RenderSettings.ambientIntensity = ambient;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);
            yield return Shoot(camera, "flashlight-day", shoulder, holder.transform.position + Vector3.up * 1.8f + look * 10.0f);

            foreach (GameObject go in new[] { holder, stranger, wall, camera.gameObject, ground, sunObject })
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static GameObject Body(Vector3 at)
        {
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body;
        }
#endif

        private static IEnumerator Hold(GameObject body, FlashlightItem torch, bool click, float seconds, Vector3 look)
        {
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = body.transform.position + Vector3.up * 2.4f - look * 4.0f;
                torch.Tick(click && t < Step, eye, look, body, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
