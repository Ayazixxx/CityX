using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the White Sneaker lying on the ground, then on the player with bare legs (standing, close up from four sides) and in the
    /// Black Jeans (standing, mid-walk, at three moments of a sprint), so the fit and the texture can be looked at. Written to
    /// Logs/CityShots/sneaker-*.png.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class WhiteSneakerCapture
    {
        [UnityTest]
        public IEnumerator RenderTheWhiteSneaker()
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
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject onGround = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/WhiteSneaker.prefab"),
                new Vector3(20.0f, 0.0f, 0.0f), Quaternion.identity);
            yield return null;
            Vector3 pair = onGround.transform.position + Vector3.up * 0.08f;
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-ground-threequarter", pair + new Vector3(1.2f, 0.9f, 1.4f), pair);
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-ground-side", pair + new Vector3(1.9f, 0.15f, 0.0f), pair);
            Object.Destroy(onGround);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            GameObject sneakers = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/WhiteSneaker.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            sneakers.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 feet = player.transform.position + Vector3.up * 0.15f;
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-bare-front", feet + new Vector3(0.0f, 0.5f, 2.4f), feet);
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-bare-side", feet + new Vector3(2.4f, 0.4f, 0.0f), feet);
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-bare-behind", feet + new Vector3(0.0f, 0.6f, -2.4f), feet);
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-bare-threequarter", feet + new Vector3(1.6f, 1.0f, 1.8f), feet);
            Vector3 hips = player.transform.position + Vector3.up * 0.9f;
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-bare-whole", hips + new Vector3(3.0f, 0.6f, 5.5f), hips);

            GameObject jeans = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BlackJeans.prefab"),
                new Vector3(-30.0f, 0.5f, 30.0f), Quaternion.identity);
            jeans.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            for (int i = 0; i < 30; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            feet = player.transform.position + Vector3.up * 0.15f;
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-jeans-threequarter", feet + new Vector3(1.6f, 1.0f, 1.8f), feet);
            BuildingShapeCapture.Shoot(camera, folder, "sneaker-jeans-side", feet + new Vector3(2.4f, 0.4f, 0.0f), feet);

            foreach ((string name, bool sprint, int frames) in new[] { ("walk", false, 50), ("sprint1", true, 60), ("sprint2", true, 8),
                ("sprint3", true, 8) })
            {
                for (int i = 0; i < frames; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = sprint }, 1.0f / 60.0f);
                    yield return null;
                }
                feet = player.transform.position + Vector3.up * 0.3f;
                Vector3 side = player.transform.right;
                Vector3 ahead = player.transform.forward;
                BuildingShapeCapture.Shoot(camera, folder, $"sneaker-{name}-side", feet + side * 3.2f + Vector3.up * 0.3f, feet);
                BuildingShapeCapture.Shoot(camera, folder, $"sneaker-{name}-front", feet + ahead * 3.2f + Vector3.up * 0.5f, feet);
            }

            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        /// <summary>The BW Sneakers (2026-10-26) on the ground, then worn with the Khakis, standing: bwsneaker-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderTheBWSneakers()
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
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject onGround = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BWSneakers.prefab"),
                new Vector3(20.0f, 0.0f, 0.0f), Quaternion.identity);
            yield return null;
            Vector3 pair = onGround.transform.position + Vector3.up * 0.08f;
            BuildingShapeCapture.Shoot(camera, folder, "bwsneaker-ground-threequarter", pair + new Vector3(1.2f, 0.9f, 1.4f), pair);
            Object.Destroy(onGround);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            foreach (string worn in new[] { "BWSneakers", "Khakis" })
            {
                Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{worn}.prefab"),
                    new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity).GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            }
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 feet = player.transform.position + Vector3.up * 0.15f;
            BuildingShapeCapture.Shoot(camera, folder, "bwsneaker-threequarter", feet + new Vector3(1.6f, 1.0f, 1.8f), feet);
            BuildingShapeCapture.Shoot(camera, folder, "bwsneaker-side", feet + new Vector3(2.4f, 0.4f, 0.0f), feet);
            Vector3 hips = player.transform.position + Vector3.up * 0.9f;
            BuildingShapeCapture.Shoot(camera, folder, "bwsneaker-whole", hips + new Vector3(3.0f, 0.6f, 5.5f), hips);

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
