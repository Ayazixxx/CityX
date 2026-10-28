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
    /// Not a test: renders the water staff (docs/STAFFS.md) to Logs/CityShots/water-staff-*.png -- carried, and spraying at a body six metres
    /// off, from the side and from behind the caster's shoulder, with the body thrown by it. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.WaterStaffCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class WaterStaffCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheWaterStaff()
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
            GameObject victim = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 6.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            victim.GetComponent<PlayerInputReader>().enabled = false;
            caster.GetComponent<Inventory>().EquipItemId(ItemRegistry.WaterStaff, caster);
            var staff = caster.GetComponentInChildren<StaffItem>();

            yield return Hold(caster, staff, false, 0.6f);
            CrouchCapture.Views(camera, caster, "water-staff-carried");
            Vector3 chest = victim.transform.position + Vector3.up * 1.3f;
            yield return Hold(caster, staff, true, 0.35f, chest);
            Vector3 middle = caster.transform.position + Vector3.up * 1.6f;
            yield return ArcaneOrbCapture.ShootFacing(camera, "water-staff-side", middle + new Vector3(9.0f, 1.0f, 3.0f), middle + Vector3.forward * 3.0f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "water-staff-shoulder", middle + new Vector3(1.2f, 1.2f, -4.0f), middle + Vector3.forward * 4.0f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "water-staff-impact", middle + new Vector3(3.5f, 0.3f, 3.0f), middle + Vector3.forward * 6.0f + Vector3.down * 0.6f);
            yield return Hold(caster, staff, true, 1.2f, chest);
            yield return ArcaneOrbCapture.ShootFacing(camera, "water-staff-thrown", middle + new Vector3(9.0f, 1.5f, 5.0f), middle + Vector3.forward * 6.0f);

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

        private static IEnumerator Hold(GameObject player, IEquippableStaff staff, bool held, float seconds, Vector3? at = null)
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
