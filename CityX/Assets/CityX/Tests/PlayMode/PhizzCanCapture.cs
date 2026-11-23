using System.Collections;
using NUnit.Framework;
using CityX.Weapons;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the PHIZZ can (docs/PHIZZ_CAN.md) standing, from the front, the back, above (its lid and tab) and below, beside the soda
    /// bottle for its size. Written to Logs/CityShots/can-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class PhizzCanCapture
    {
        [UnityTest]
        public IEnumerator RenderTheCan()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(35.0f, 200.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject can = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Cans/phizz_can.prefab"),
                new Vector3(40.0f, 0.0f, 40.0f), Quaternion.identity);
            Assert.IsNotNull(can, "run CityX/Rebuild PHIZZ Can");
            GameObject lying = Object.Instantiate(can, new Vector3(41.0f, 0.08f, 40.0f), Quaternion.Euler(0.0f, 0.0f, 90.0f));
            var middle = new Vector3(40.0f, 0.15f, 40.0f);
            yield return null;
            BuildingShapeCapture.Shoot(camera, folder, "can-front", middle + new Vector3(0.0f, 0.12f, 1.4f), middle);
            BuildingShapeCapture.Shoot(camera, folder, "can-back", middle + new Vector3(0.0f, 0.12f, -1.4f), middle);
            BuildingShapeCapture.Shoot(camera, folder, "can-side", middle + new Vector3(1.4f, 0.12f, 0.0f), middle);
            BuildingShapeCapture.Shoot(camera, folder, "can-above", middle + new Vector3(0.25f, 0.9f, 0.6f), middle + Vector3.up * 0.12f);
            BuildingShapeCapture.Shoot(camera, folder, "can-lid", middle + new Vector3(0.02f, 0.65f, 0.12f), middle + Vector3.up * 0.15f);
            var under = new Vector3(41.0f, 0.08f, 40.0f);
            BuildingShapeCapture.Shoot(camera, folder, "can-under", under + new Vector3(0.9f, 0.2f, 0.4f), under + Vector3.left * 0.15f);
            Object.Destroy(lying);
            Object.Destroy(can);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        /// <summary>The drink, moment by moment, from in front (close on the hands and the face) and from the game's camera behind: can-drink-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderTheDrink()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(35.0f, 160.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            body.GetComponent<CityX.Character.PlayerInputReader>().enabled = false;
            var drinker = body.GetComponent<CanDrinker>() ?? body.AddComponent<CanDrinker>();
            var can = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Cans/phizz_can.prefab"),
                new Vector3(5.0f, 1.0f, 5.0f), Quaternion.identity).GetComponent<PhizzCan>();
            can.OnEquip(body, CityX.Items.ItemInstanceData.None);
            var locomotion = body.GetComponent<CityX.Character.PlayerLocomotion>();
            for (int i = 0; i < 40; i++)
            {
                locomotion.Tick(new CityX.Character.LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Shoot(camera, folder, "carry");
            can.Drink();
            float seconds = 0.0f;
            int next = 0;
            float[] moments = { 0.2f, 0.32f, 0.42f, 0.6f, 1.0f, 1.36f, 1.7f, 2.1f, 2.4f, 2.8f, 3.1f, 3.3f, 3.6f };
            while (next < moments.Length)
            {
                locomotion.Tick(new CityX.Character.LocomotionInput(), 1.0f / 60.0f);
                yield return null;
                seconds += 1.0f / 60.0f;
                if (seconds >= moments[next])
                {
                    Shoot(camera, folder, $"{moments[next]:0.00}s");
                    next++;
                }
            }
            Object.Destroy(body);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            foreach (DroppedCan dropped in Object.FindObjectsByType<DroppedCan>(FindObjectsSortMode.None))
            {
                Object.Destroy(dropped.gameObject);
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            Assert.Ignore("needs the editor to load the prefab.");
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string folder, string moment)
        {
            var face = new Vector3(0.0f, 2.0f, 0.0f);
            BuildingShapeCapture.Shoot(camera, folder, $"can-drink-front-{moment}", face + new Vector3(1.3f, 0.3f, 2.6f), face + new Vector3(0.1f, 0.0f, 0.2f));
            BuildingShapeCapture.Shoot(camera, folder, $"can-drink-game-{moment}", new Vector3(0.87f, 2.9f, -5.0f), new Vector3(0.3f, 1.6f, 1.0f));
        }
    }
}
