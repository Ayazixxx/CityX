using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the frag grenade and its explosion (docs/GRENADE.md) to Logs/CityShots/grenade-*.png -- the grenade on the street,
    /// then the explosion frame by frame (the flash, the fireball, the sparks and debris flying, the smoke rising, the scorch left), by day and
    /// at night. Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.GrenadeCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GrenadeCapture
    {
        [UnityTest]
        public IEnumerator RenderTheGrenade()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 4.0f, 9.0f);
            wall.transform.localScale = new Vector3(30.0f, 8.0f, 0.5f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);
            GameObject bystander = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(-3.0f, 0.0f, 4.0f), Quaternion.Euler(0.0f, 140.0f, 0.0f));
            bystander.GetComponent<PlayerInputReader>().enabled = false;

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownGrenade.prefab");
            ThrownGrenade grenade = ThrownGrenade.Spawn(prefab, new Vector3(0.0f, 0.3f, 0.0f), Vector3.zero, null, false);
            yield return Wait(GrenadeRules.FuseSeconds - 0.5f);
            Vector3 eye = new Vector3(2.5f, 1.4f, -4.5f);
            yield return Shoot(camera, "grenade-ticking", new Vector3(0.6f, 0.5f, -0.8f), grenade.transform.position);
            float[] at = { 0.03f, 0.1f, 0.25f, 0.6f, 1.5f, 3.0f };
            yield return Wait(10.0f, () => grenade == null);
            float since = 0.0f;
            foreach (float moment in at)
            {
                yield return Wait(moment - since);
                since = moment;
                yield return Shoot(camera, $"grenade-boom-{moment:0.00}s", eye + new Vector3(4.0f, 2.0f, -8.0f), new Vector3(0.0f, 1.5f, 0.0f));
            }

            // The same at night: the flash lights the street, the fireball glows, the smoke is lit from inside.
            sun.intensity = 0.05f;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.05f);
            float ambient = RenderSettings.ambientIntensity;
            RenderSettings.ambientIntensity = 0.15f;
            Explosion.Show(new Vector3(8.0f, 0.06f, 0.0f));
            since = 0.0f;
            foreach (float moment in new[] { 0.05f, 0.25f, 1.0f })
            {
                yield return Wait(moment - since);
                since = moment;
                yield return Shoot(camera, $"grenade-night-{moment:0.00}s", new Vector3(18.0f, 3.0f, -12.0f), new Vector3(8.0f, 1.5f, 0.0f));
            }
            RenderSettings.ambientIntensity = ambient;

            foreach (GameObject go in new[] { bystander, wall, camera.gameObject, ground, sunObject })
            {
                Object.Destroy(go);
            }
            FireScene.Clear();
#else
            yield break;
#endif
        }

        private static IEnumerator Wait(float seconds, System.Func<bool> until = null)
        {
            for (float t = 0.0f; t < seconds && (until == null || !until()); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
