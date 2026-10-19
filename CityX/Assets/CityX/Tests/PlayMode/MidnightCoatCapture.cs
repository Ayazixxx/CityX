using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player wearing the Midnight Coat, standing (front, side, behind, three-quarters, the cuffs close up) and mid-stride and
    /// mid-sprint, so the fit, the texture and the skinning can be looked at. Written to Logs/CityShots/midnightcoat-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class MidnightCoatCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInTheMidnightCoat()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            GameObject coat = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/MidnightCoat.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            coat.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);

            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 chest = player.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "midnightcoat-front", chest + new Vector3(0.0f, 0.3f, 9.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "midnightcoat-side", chest + new Vector3(9.0f, 0.3f, 0.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "midnightcoat-behind", chest + new Vector3(0.0f, 0.6f, -9.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "midnightcoat-threequarter", chest + new Vector3(6.4f, 1.5f, 6.4f), chest);
            Vector3 wrist = player.transform.position + new Vector3(0.6f, 1.05f, 0.0f);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "midnightcoat-cuff", wrist + new Vector3(2.2f, 0.2f, 1.6f), wrist);

            // The walk once; the sprint at four moments of a stride (the knees and the armpits move most there).
            foreach ((string name, bool sprint, int frames) in new[] { ("walk", false, 50), ("sprint1", true, 60), ("sprint2", true, 6),
                ("sprint3", true, 6), ("sprint4", true, 6) })
            {
                for (int i = 0; i < frames; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = sprint }, 1.0f / 60.0f);
                    yield return null;
                }
                chest = player.transform.position + Vector3.up * 1.4f;
                Vector3 side = player.transform.right;
                Vector3 ahead = player.transform.forward;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"midnightcoat-{name}-side", chest + side * 9.0f + Vector3.up * 0.3f, chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"midnightcoat-{name}-front", chest + ahead * 9.0f + Vector3.up * 0.3f, chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"midnightcoat-{name}-armpit",
                    chest + (ahead + side) * 3.2f + Vector3.down * 0.4f, chest + side * 0.3f);
                Vector3 collar = chest + Vector3.up * 0.65f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"midnightcoat-{name}-collar",
                    collar + (ahead * 2.0f + side) * 1.2f, collar);
            }

            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
