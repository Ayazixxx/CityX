using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: brack guy (hobo look 3) walking about with nothing in hand, from the front, the side and behind, then hit, with his machete
    /// out. Written to Logs/CityShots/hobo3-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class HoboCapture
    {
        [UnityTest]
        public IEnumerator RenderBrackGuy()
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
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 35.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject hobo = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Hobos/Hobo_3_Machete.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var brain = hobo.GetComponent<SwordsmanBrain>();
            brain.CalmArmedShare = 0.0f;
            brain.enabled = false;   // standing still for the pictures
            for (int i = 0; i < 40; i++) yield return null;
            Vector3 chest = hobo.transform.position + Vector3.up * 1.5f;
            BuildingShapeCapture.Shoot(camera, folder, "hobo3-front", chest + new Vector3(0.0f, 0.3f, 6.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "hobo3-side", chest + new Vector3(6.0f, 0.3f, 0.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "hobo3-back", chest + new Vector3(0.0f, 0.3f, -6.0f), chest);
            Vector3 head = hobo.transform.position + Vector3.up * 2.3f;
            BuildingShapeCapture.Shoot(camera, folder, "hobo3-face", head + new Vector3(0.6f, 0.1f, 2.4f), head);

            brain.enabled = true;
            ((IDamageable)hobo.GetComponent<PlayerVitals>()).ApplyHit(5.0f, hobo.transform.position + Vector3.forward * 2.0f, 0.0f);
            for (int i = 0; i < 30; i++) yield return null;
            chest = hobo.transform.position + Vector3.up * 1.5f;
            BuildingShapeCapture.Shoot(camera, folder, "hobo3-drawn", chest + new Vector3(4.0f, 0.5f, 4.0f), chest);

            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            Object.Destroy(hobo);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
