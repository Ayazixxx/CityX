using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: another player's copy of a body (NetworkedPlayer.prefab) wearing what the wearer announced -- a long-sleeved shirt, jeans
    /// and a top hat (NetworkedEquipmentSync.Show) -- standing, so the clothes others see can be looked at. Written to
    /// Logs/CityShots/copy-clothes-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CopyClothesCapture
    {
        [UnityTest]
        public IEnumerator RenderACopyInClothes()
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
            sun.transform.rotation = Quaternion.Euler(40.0f, 150.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            GameObject copy = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab"),
                Vector3.zero, Quaternion.identity);
            yield return null;

            copy.GetComponent<NetworkedEquipmentSync>().Show(new[] { ItemRegistry.LongSleeveTShirt, ItemRegistry.SkelliJeans, ItemRegistry.TopHat });
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Vector3 chest = copy.transform.position + Vector3.up * 1.5f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "copy-clothes-front", chest + new Vector3(0.0f, 0.4f, 8.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "copy-clothes-threequarter", chest + new Vector3(6.0f, 1.2f, -5.0f), chest);

            Object.Destroy(copy);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
