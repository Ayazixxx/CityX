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
    /// Not a test: renders the slingshot (docs/SLINGSHOT.md) to Logs/CityShots/slingshot-*.png -- the model on its own, the slingshot aimed, fully
    /// drawn from the side and from the game's camera, and a pebble just flung. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SlingshotCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SlingshotCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheSlingshot()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(80.0f, 1.0f, 80.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SlingshotPaths.Slingshot);
            Assert.IsNotNull(prefab, "no Slingshot.prefab -- rebuild the scene");
            GameObject alone = Object.Instantiate(prefab, new Vector3(6.0f, 1.0f, 0.0f), Quaternion.Euler(0.0f, -30.0f, 0.0f));
            Vector3 middle = alone.transform.position + Vector3.up * 0.03f;
            yield return Shoot(camera, "slingshot-model", middle + new Vector3(0.25f, 0.12f, 0.5f), middle);

            Object.Destroy(alone);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var archery = player.GetComponent<Archery>();
            if (archery == null)
            {
                archery = player.AddComponent<Archery>();
            }
            archery.PebblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SlingshotPaths.Pebble);
            var sling = Object.Instantiate(prefab).GetComponent<BowWeapon>();
            sling.OnEquip(player, ItemInstanceData.None);
            Vector3 body = player.transform.position;
            yield return Hold(player, sling, false, false, 0.5f);
            yield return Shoot(camera, "slingshot-carried", body + new Vector3(2.4f, 1.9f, 3.0f), body + Vector3.up * 1.3f);
            yield return Hold(player, sling, false, true, 0.4f);
            yield return Shoot(camera, "slingshot-aimed", body + new Vector3(3.2f, 2.2f, 1.8f), body + new Vector3(0.0f, 1.9f, 0.5f));
            yield return Hold(player, sling, true, true, SlingshotRules.FullDrawSeconds + 0.3f);
            yield return Shoot(camera, "slingshot-drawn", body + new Vector3(3.2f, 2.2f, 1.8f), body + new Vector3(0.0f, 1.9f, 0.5f));
            // The pinch close, from above and in front (the thumb's side), and from behind the hand; then from overhead, the hand and the
            // bands clear of the head.
            Vector3 pinch = player.GetComponent<PlayerLocomotion>().BowNock;
            yield return Shoot(camera, "slingshot-pinch-front", pinch + new Vector3(0.25f, 0.35f, 0.55f), pinch);
            yield return Shoot(camera, "slingshot-pinch-back", pinch + new Vector3(0.35f, 0.25f, -0.55f), pinch);            yield return Shoot(camera, "slingshot-drawn-top", body + new Vector3(0.3f, 6.5f, 0.3f), body + new Vector3(0.3f, 2.0f, 0.31f));
            // As the player sees it: the game's camera, behind and to the right, its aim field of view (docs/CAMERA.md).
            camera.fieldOfView = 50.0f;
            yield return Shoot(camera, "slingshot-drawn-game-view", body + new Vector3(0.87f, 3.2f, -5.0f), body + new Vector3(0.87f, 2.6f, 10.0f));
            // Aimed in (right-click), as the player sees it: the game's own camera on this body in its aim framing, given a second to settle in.
            camera.fieldOfView = 30.0f;
            var aimObject = new GameObject("AimCam");
            Camera aimCamera = aimObject.AddComponent<Camera>();
            aimCamera.fieldOfView = ThirdPersonCamera.DefaultFieldOfView;
            aimCamera.nearClipPlane = 0.02f;
            aimCamera.clearFlags = CameraClearFlags.SolidColor;
            aimCamera.backgroundColor = camera.backgroundColor;
            var aimRig = aimObject.AddComponent<ThirdPersonCamera>();
            aimRig.Target = player.transform;
            aimRig.Yaw = player.transform.eulerAngles.y;
            aimRig.IsAiming = true;
            yield return Hold(player, sling, true, true, 1.5f);
            Transform aimView = aimObject.transform;
            yield return Shoot(aimCamera, "slingshot-drawn-aim-view", aimView.position, aimView.position + aimView.forward * 10.0f);
            Object.Destroy(aimObject);
            yield return Hold(player, sling, false, true, 4.0f * Step);
            yield return Shoot(camera, "slingshot-flung", body + new Vector3(3.0f, 3.4f, -2.6f), body + new Vector3(0.0f, 2.0f, 4.0f));

            foreach (GameObject go in new[] { player, sling.gameObject, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator Hold(GameObject player, BowWeapon sling, bool held, bool aiming, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 origin = player.transform.position + Vector3.up * 1.6f + player.transform.forward * 0.3f;
                sling.Draw(held, origin, player.transform.forward, aiming, player, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y, Aiming = aiming }, Step);
                yield return null;
            }
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
