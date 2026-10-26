using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the Wraith rendered (owner, 2026-10-26: "make this wraith enemy looks good"), by day beside the player for its size, from the
    /// front, three-quarters and the side; a strip of eight moments over four seconds of floating (the sway of its arms, the writhe of its
    /// wisps); and by night, when its glow shows. Written to Logs/CityShots/wraith-*.png.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class WraithCapture
    {
        [UnityTest]
        public IEnumerator RenderTheWraith()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            ground.GetComponent<Renderer>().material.color = new Color(0.32f, 0.33f, 0.36f);
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
                new Vector3(-1.6f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            GameObject wraithObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Wraith.prefab"),
                new Vector3(1.2f, WraithDrift.HoverHeight, 0.0f), Quaternion.identity);
            var wraith = wraithObject.GetComponent<Wraith>();
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Vector3 chest = wraithObject.transform.position + Vector3.up * 1.5f;
            Vector3 between = (player.transform.position + wraithObject.transform.position) * 0.5f + Vector3.up * 1.3f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-with-player", between + new Vector3(0.0f, 0.5f, 9.0f), between);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-front", chest + wraithObject.transform.forward * 6.0f + Vector3.up * 0.3f, chest);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-threequarter",
                chest + (wraithObject.transform.forward + wraithObject.transform.right).normalized * 6.0f + Vector3.up * 1.2f, chest);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-side", chest + wraithObject.transform.right * 6.0f + Vector3.up * 0.3f, chest);

            // Floating: eight moments over four seconds, the camera following it.
            for (int shot = 0; shot < 8; shot++)
            {
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                }
                chest = wraithObject.transform.position + Vector3.up * 1.4f;
                BuildingShapeCapture.Shoot(camera, folder, $"wraith-drift-{shot}", chest + new Vector3(4.5f, 1.0f, 4.5f), chest);
            }

            // Night: no sun, a near-black world; its own glow is the light.
            sun.intensity = 0.0f;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            ground.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.09f);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            chest = wraithObject.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-night-threequarter", chest + new Vector3(4.5f, 1.0f, 4.5f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-night-close", chest + new Vector3(2.0f, 0.6f, 2.4f), chest);

            // Hit: the flash and the flinch, a few frames in.
            ((IDamageable)wraith).ApplyHit(CombatTuning.PunchDamage, chest + Vector3.forward, CombatTuning.HitstunSeconds);
            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }
            chest = wraithObject.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-night-hit", chest + new Vector3(4.5f, 1.0f, 4.5f), chest);

            Object.Destroy(wraithObject);
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
