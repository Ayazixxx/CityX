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
    /// Not a test: renders the Arcane Beam Staff (docs/STAFFS.md) to Logs/CityShots/arcane-beam-*.png -- the beam held on a target from the
    /// side, over the shoulder and where it lands, by day, and over the shoulder at dusk. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.ArcaneBeamCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ArcaneBeamCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheBeam()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 45.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            if (player.GetComponent<ArcaneBeamCaster>() == null)
            {
                player.AddComponent<ArcaneBeamCaster>();
            }
            var staff = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ArcaneBeamStaff.prefab"))
                .GetComponent<StaffItem>();
            staff.OnEquip(player, ItemInstanceData.None);
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.transform.position = new Vector3(0.0f, 1.3f, 9.0f);
            target.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);

            yield return Hold(player, staff, 1.0f);
            Vector3 middle = new Vector3(0.0f, 2.0f, 4.5f);
            yield return Shoot(camera, "arcane-beam-side", middle + new Vector3(9.5f, 0.8f, 0.0f), middle);
            yield return Hold(player, staff, 0.1f);
            yield return Shoot(camera, "arcane-beam-shoulder", new Vector3(1.4f, 3.2f, -4.0f), new Vector3(0.0f, 2.0f, 9.0f));
            yield return Hold(player, staff, 0.1f);
            yield return Shoot(camera, "arcane-beam-landing", new Vector3(2.5f, 2.4f, 6.0f), new Vector3(0.0f, 1.9f, 8.6f));

            sun.intensity = 0.08f;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.08f);
            RenderSettings.ambientIntensity = 0.2f;
            yield return Hold(player, staff, 0.1f);
            yield return Shoot(camera, "arcane-beam-dusk", new Vector3(1.4f, 3.2f, -4.0f), new Vector3(0.0f, 2.0f, 9.0f));
            RenderSettings.ambientIntensity = 1.0f;

            Object.Destroy(player);
            Object.Destroy(target);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Hold(GameObject player, IEquippableStaff staff, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = player.transform.position + Vector3.up * 2.4f - player.transform.forward * 4.0f;
                staff.Tick(true, eye, player.transform.forward, player, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
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
