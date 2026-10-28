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
    /// Not a test: renders the three staffs (docs/STAFFS.md) to Logs/CityShots/staff-*.png -- each carried, then thrust out charging (the Arcane
    /// Staff with its three orbs circling the head, the Fireball Staff at a full charge) or shooting (the Ice Shard Staff). Explicit; run by name
    /// with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.StaffCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StaffCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheStaffs()
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
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            foreach ((string id, string name, float hold) in new[]
                     {
                         (ItemRegistry.ArcaneStaff, "staff-arcane", 0.6f),
                         (ItemRegistry.FireballStaff, "staff-fireball", 1.6f),
                         (ItemRegistry.IceShardStaff, "staff-ice", 0.4f),
                     })
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                player.GetComponent<Inventory>().EquipItemId(id, player);
                var staff = player.GetComponentInChildren<StaffItem>();
                yield return Hold(player, staff, false, 0.6f);
                CrouchCapture.Views(camera, player, name + "-carried");
                yield return Hold(player, staff, true, hold);
                Vector3 middle = player.transform.position + Vector3.up * 1.6f;
                yield return ArcaneOrbCapture.ShootFacing(camera, name + "-casting-quarter",
                    middle + (player.transform.forward * 0.7f + player.transform.right).normalized * 6.0f + Vector3.up * 0.8f, middle);
                yield return ArcaneOrbCapture.ShootFacing(camera, name + "-casting-side", middle + player.transform.right * 6.0f, middle);
                staff.CancelCharge(player);
                Object.Destroy(player);
                foreach (ArcaneOrbProjectile b in Object.FindObjectsByType<ArcaneOrbProjectile>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
                foreach (IceShardProjectile s in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None)) Object.Destroy(s.gameObject);
                yield return null;
            }
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Hold(GameObject player, IEquippableStaff staff, bool held, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = player.transform.position + Vector3.up * 2.6f - player.transform.forward * 4.0f;
                staff.Tick(held, eye, player.transform.forward, player, Step);
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
        }
    }
}
