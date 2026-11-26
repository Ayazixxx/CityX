using System.Collections;
using CityX.Character;
using CityX.Weapons;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: Squirrel Boy (docs/SQUIRREL_BOY.md) standing, from the front, the side and behind; then badly hurt, on his way up a wall.
    /// Written to Logs/CityShots/squirrelboy-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SquirrelBoyCapture
    {
        [UnityTest]
        public IEnumerator RenderSquirrelBoy()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 10.0f, 12.0f);
            wall.transform.localScale = new Vector3(30.0f, 20.0f, 2.0f);
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

            GameObject squirrel = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/SquirrelBoy.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            for (int i = 0; i < 40; i++) yield return null;
            Vector3 chest = squirrel.transform.position + Vector3.up * 1.6f;
            BuildingShapeCapture.Shoot(camera, folder, "squirrelboy-front", chest + new Vector3(0.0f, 0.4f, 6.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "squirrelboy-side", chest + new Vector3(6.0f, 0.4f, 0.0f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "squirrelboy-back", chest + new Vector3(0.0f, 0.4f, -6.0f), chest);
            Vector3 face = squirrel.transform.position + Vector3.up * 2.35f;
            BuildingShapeCapture.Shoot(camera, folder, "squirrelboy-face", face + new Vector3(0.0f, 0.1f, 2.2f), face);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, -2.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = squirrel.GetComponent<PlayerVitals>();
            vitals.TakeDamage(vitals.MaxHealth * 0.8f);
            var brain = squirrel.GetComponent<SquirrelBoyBrain>();
            int shot = 0;
            for (int frame = 0; frame < 60 * 12 && shot < 4; frame++)
            {
                yield return null;
                if (brain.Mind.State == SquirrelBoyState.Climb && frame % 45 == 0)
                {
                    Vector3 on = squirrel.transform.position + Vector3.up * 1.3f;
                    BuildingShapeCapture.Shoot(camera, folder, $"squirrelboy-climb-{shot++}", on + new Vector3(6.0f, 1.0f, -5.0f), on);
                }
            }
            Object.Destroy(player);
            Object.Destroy(squirrel);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(wall);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
