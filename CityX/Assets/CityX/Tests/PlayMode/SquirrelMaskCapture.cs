using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player in the Squirrel Mask, standing (front, close on the nose, three-quarters, side, behind, from the game's camera
    /// behind and above, whole), so the fit on the head, the fur, the black eyes and the nose standing out can be looked at in the game's cel
    /// look. Written to Logs/CityShots/squirrelmask-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SquirrelMaskCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInTheSquirrelMask()
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
            // Before the face and a little to its right, so the front is lit (the cel look greys a face in shade).
            sun.transform.rotation = Quaternion.Euler(35.0f, 200.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/SquirrelMask.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity).GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 head = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position;
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-front", head + new Vector3(0.0f, 0.2f, 5.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-close", head + new Vector3(0.0f, 0.1f, 2.2f), head + new Vector3(0.0f, -0.05f, 0.0f));
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-nose", head + new Vector3(1.4f, 0.05f, 1.4f), head + new Vector3(0.0f, -0.08f, 0.3f));
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-threequarter", head + new Vector3(3.5f, 1.0f, 3.5f), head);
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-side", head + new Vector3(5.0f, 0.3f, 0.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-behind", head + new Vector3(-1.5f, 0.6f, -5.0f), head);
            // The game's camera (docs/CAMERA.md): 5 m behind and a little to the right, the head's top at the picture's middle.
            Vector3 hips = player.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-game-camera", hips + new Vector3(0.9f, 1.4f, -5.0f), hips + Vector3.up * 1.0f);
            BuildingShapeCapture.Shoot(camera, folder, "squirrelmask-whole", hips + new Vector3(3.0f, 1.0f, 8.0f), hips);

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
