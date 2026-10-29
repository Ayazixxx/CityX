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
    /// Not a test: renders healing (docs/HEALING.md) to Logs/CityShots/healing-*.png -- the Staff of Healing's gold beam on a friend, from the
    /// side and over the shoulder, and the Healing Aura's green circle round its caster with a friend standing in it, by day and at dusk.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.HealingCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class HealingCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderHealing()
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

            GameObject healer = Body(Vector3.zero, false);
            GameObject friend = Body(new Vector3(1.5f, 0.0f, 9.0f), true);
            friend.transform.rotation = Quaternion.Euler(0.0f, 200.0f, 0.0f);
            healer.AddComponent<HealingBeamCaster>();
            var staff = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/HealingStaff.prefab"))
                .GetComponent<StaffItem>();
            staff.OnEquip(healer, ItemInstanceData.None);
            friend.GetComponent<PlayerVitals>().TakeDamage(60.0f);
            Vector3 chest = HealingBeamCaster.ChestOf(friend.transform);
            Vector3 look = (chest - (healer.transform.position + Vector3.up * 2.4f)).normalized;
            for (float t = 0.0f; t < 1.0f; t += Step)
            {
                Vector3 eye = healer.transform.position + Vector3.up * 2.4f - look * 4.0f;
                staff.Tick(true, eye, look, healer, Step);
                healer.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            Vector3 middle = (healer.transform.position + friend.transform.position) * 0.5f + Vector3.up * 2.0f;
            yield return Shoot(camera, "healing-staff-side", middle + new Vector3(10.0f, 1.0f, 0.0f), middle);
            yield return Shoot(camera, "healing-staff-shoulder", new Vector3(-1.2f, 3.2f, -4.0f), chest);
            staff.Tick(false, Vector3.zero, Vector3.forward, healer, Step);

            // The aura, round a caster of its own with a friend standing in it.
            GameObject caster = Body(new Vector3(20.0f, 0.0f, 0.0f), false);
            GameObject inside = Body(new Vector3(22.5f, 0.0f, 1.0f), true);
            var aura = caster.AddComponent<HealingAuraAbility>();
            yield return null;
            aura.TryCast();
            for (float t = 0.0f; t < 1.0f; t += Step)
            {
                yield return null;
            }
            Vector3 centre = caster.transform.position + Vector3.up * 1.0f;
            yield return Shoot(camera, "healing-aura-day", centre + new Vector3(6.0f, 5.0f, -7.0f), centre);
            sun.intensity = 0.08f;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.08f);
            RenderSettings.ambientIntensity = 0.2f;
            yield return Shoot(camera, "healing-aura-dusk", centre + new Vector3(6.0f, 5.0f, -7.0f), centre);
            RenderSettings.ambientIntensity = 1.0f;

            foreach (GameObject go in new[] { healer, friend, caster, inside, camera.gameObject, ground, sunObject })
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static GameObject Body(Vector3 at, bool player)
        {
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = player;
            return body;
        }
#endif

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
