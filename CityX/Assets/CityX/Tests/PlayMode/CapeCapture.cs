using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the red cape standing (front, side, behind, three-quarter, the knot close), then walking, sprinting and turning while sprinting,
    /// seen from the side and behind. Written to Logs/CityShots/cape-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CapeCapture
    {
        [UnityTest]
        public IEnumerator RenderTheCape()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(600.0f, 1.0f, 600.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var body = player.GetComponent<PlayerLocomotion>();
            GameObject cape = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CapeColourways.PrefabPath(CapeColourways.RedItemId)),
                new Vector3(3.0f, 0.0f, 3.0f), Quaternion.identity);
            cape.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            GameObject lying = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CapeColourways.PrefabPath(CapeColourways.BlackItemId)),
                new Vector3(2.2f, 0.0f, 1.0f), Quaternion.Euler(0.0f, 20.0f, 0.0f));

            for (int i = 0; i < 150; i++)
            {
                body.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 chest = player.transform.position + Vector3.up * 1.3f;
            BuildingShapeCapture.Shoot(camera, folder, "cape-front", chest + new Vector3(0.0f, 0.3f, 9.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "cape-side", chest + new Vector3(9.0f, 0.3f, 0.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "cape-behind", chest + new Vector3(0.0f, 0.6f, -9.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "cape-threequarter", chest + new Vector3(6.4f, 1.5f, -6.4f), chest);
            Vector3 knot = player.transform.position + Vector3.up * 1.8f;
            BuildingShapeCapture.Shoot(camera, folder, "cape-knot", knot + new Vector3(0.9f, 0.4f, 2.6f), knot);
            BuildingShapeCapture.Shoot(camera, folder, "cape-lying", new Vector3(2.2f, 1.6f, 4.2f), new Vector3(2.2f, 0.0f, 1.0f));
            Object.Destroy(lying);

            foreach ((string name, bool sprint, float turn, int frames) in new[]
                     { ("walk", false, 0.0f, 60), ("sprint", true, 0.0f, 90), ("sprint2", true, 0.0f, 9), ("turn", true, 1.0f, 30), ("stop", false, 0.0f, 0) })
            {
                Vector2 move = name == "stop" ? Vector2.zero : Vector2.up;
                int count = name == "stop" ? 20 : frames;
                for (int i = 0; i < count; i++)
                {
                    if (turn != 0.0f) player.transform.Rotate(0.0f, 4.0f, 0.0f);
                    body.Tick(new LocomotionInput { Move = move, Sprint = sprint }, 1.0f / 60.0f);
                    yield return null;
                }
                chest = player.transform.position + Vector3.up * 1.3f;
                Vector3 side = player.transform.right, ahead = player.transform.forward;
                BuildingShapeCapture.Shoot(camera, folder, $"cape-{name}-side", chest + side * 9.0f + Vector3.up * 0.4f, chest);
                BuildingShapeCapture.Shoot(camera, folder, $"cape-{name}-behind", chest - ahead * 8.0f + side * 2.0f + Vector3.up * 1.2f, chest);
            }

            Time.captureFramerate = 0;
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
