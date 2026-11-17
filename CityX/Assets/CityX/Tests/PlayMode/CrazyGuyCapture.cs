using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: Crazy Guy (guycrazy with the kitchen knife) standing, from the front, the side and behind, then going for a player who stands still,
    /// at moments of the fight. Written to Logs/CityShots/crazyguy-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CrazyGuyCapture
    {
        [UnityTest]
        public IEnumerator RenderCrazyGuy()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
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

            GameObject crazy = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrazyGuy.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var brain = crazy.GetComponent<SwordsmanBrain>();
            for (int i = 0; i < 40; i++) yield return null;
            Vector3 chest = crazy.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "crazyguy-front", chest + new Vector3(0.0f, 0.4f, 7.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "crazyguy-side", chest + new Vector3(7.0f, 0.4f, 0.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "crazyguy-back", chest + new Vector3(0.0f, 0.4f, -7.0f), chest);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, -6.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            crazy.transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            brain.Target = vitals;
            for (int moment = 0; moment < 6; moment++)
            {
                for (int i = 0; i < 40; i++)
                {
                    vitals.TickHitstun(1.0f / 60.0f);
                    yield return null;
                }
                Vector3 middle = (player.transform.position + crazy.transform.position) * 0.5f + Vector3.up * 1.3f;
                BuildingShapeCapture.Shoot(camera, folder, $"crazyguy-fight-{moment}", middle + new Vector3(8.0f, 3.0f, 0.0f), middle);
            }
            Object.Destroy(player);
            Object.Destroy(crazy);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
