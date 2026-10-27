using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: a sword lying, lifted and carried, and thrown, from beside the player (Logs/CityShots/carry-NN.png). Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class CarryCapture
    {
        [UnityTest]
        public IEnumerator FilmTheCarry()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var eye = new GameObject("Eye");
            eye.transform.position = new Vector3(0.0f, 3.2f, -4.5f);
            eye.transform.rotation = Quaternion.LookRotation(new Vector3(0.0f, 0.3f, 2.0f) - eye.transform.position);
            var carry = player.AddComponent<GrabCarry>();
            carry.Eye = eye.transform;
            GameObject sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab"),
                new Vector3(0.0f, 0.15f, 2.0f), Quaternion.Euler(0.0f, 0.0f, 90.0f));
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) yield return null;

            Vector3 side = new Vector3(6.5f, 2.2f, 1.0f), look = new Vector3(0.0f, 1.2f, 1.2f);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "carry-00", side, look);
            carry.Tick(holdBegan: true, interactDown: true, throwPressed: false);
            eye.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            for (int i = 0; i < 60; i++)
            {
                carry.Tick(false, true, false);
                yield return null;
            }
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "carry-01", side, look);
            player.transform.rotation = Quaternion.Euler(0.0f, 40.0f, 0.0f);
            eye.transform.rotation = Quaternion.Euler(0.0f, 40.0f, 0.0f);
            for (int i = 0; i < 60; i++)
            {
                carry.Tick(false, true, false);
                yield return null;
            }
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "carry-02", side, look);
            carry.Tick(false, true, true);
            for (int i = 0; i < 12; i++) yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "carry-03", side + new Vector3(2.0f, 0.0f, 4.0f), look + new Vector3(2.0f, 0.0f, 4.0f));
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            Object.Destroy(sword);
            Object.Destroy(player);
            Object.Destroy(eye);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
