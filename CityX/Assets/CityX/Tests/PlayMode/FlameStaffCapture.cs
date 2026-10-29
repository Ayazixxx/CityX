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
    /// Not a test: renders the flame staff (docs/STAFFS.md) to Logs/CityShots/flame-staff-*.png -- carried, and burning someone from the side,
    /// over the shoulder and at night. Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.FlameStaffCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FlameStaffCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheFlameStaff()
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
            camera.fieldOfView = 40.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            GameObject caster = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            caster.GetComponent<PlayerInputReader>().enabled = false;
            GameObject victim = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 5.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            victim.GetComponent<PlayerInputReader>().enabled = false;
            caster.GetComponent<Inventory>().EquipItemId(ItemRegistry.FlameStaff, caster);
            var staff = caster.GetComponentInChildren<StaffItem>();

            yield return Hold(caster, staff, false, 0.6f, null);
            CrouchCapture.Views(camera, caster, "flame-staff-carried");
            Vector3 chest = victim.transform.position + Vector3.up * 1.4f;
            yield return Hold(caster, staff, true, 0.5f, chest);
            Vector3 middle = caster.transform.position + Vector3.up * 1.6f;
            yield return ArcaneOrbCapture.ShootFacing(camera, "flame-staff-side", middle + new Vector3(8.0f, 1.0f, 2.5f), middle + Vector3.forward * 2.5f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "flame-staff-shoulder", middle + new Vector3(1.2f, 1.2f, -4.0f), middle + Vector3.forward * 4.0f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "flame-staff-victim", chest + new Vector3(3.0f, 0.5f, 2.5f), chest);
            sun.intensity = 0.05f;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            yield return Hold(caster, staff, true, 0.2f, chest);
            yield return ArcaneOrbCapture.ShootFacing(camera, "flame-staff-night", middle + new Vector3(8.0f, 1.0f, 2.5f), middle + Vector3.forward * 2.5f);

            staff.CancelCharge(caster);
            Object.Destroy(caster);
            Object.Destroy(victim);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Hold(GameObject player, IEquippableStaff staff, bool held, float seconds, Vector3? at)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = player.transform.position + Vector3.up * 2.4f - player.transform.forward * 4.0f;
                Vector3 look = at.HasValue ? (at.Value - eye).normalized : player.transform.forward;
                staff.Tick(held, eye, look, player, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y }, Step);
                yield return null;
            }
        }
    }
}
