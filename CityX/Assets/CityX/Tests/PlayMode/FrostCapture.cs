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
    /// Not a test: renders the Frost Staff (docs/FROST.md) to Logs/CityShots/frost-*.png -- the spray held on a swordsman from the side and over
    /// the shoulder, and the swordsman frozen solid beside a frozen player, from behind and the front. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.FrostCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FrostCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheFrost()
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

            GameObject caster = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            caster.GetComponent<PlayerInputReader>().enabled = false;
            if (caster.GetComponent<FrostSprayCaster>() == null)
            {
                caster.AddComponent<FrostSprayCaster>();
            }
            var staff = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FrostStaff.prefab"))
                .GetComponent<StaffItem>();
            staff.OnEquip(caster, ItemInstanceData.None);
            GameObject swordsman = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                new Vector3(0.0f, 0.0f, 6.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            swordsman.GetComponent<PlayerVitals>().InfiniteHealth = true;

            yield return Hold(caster, staff, true, 1.0f);
            Vector3 middle = new Vector3(0.0f, 1.8f, 3.0f);
            yield return Shoot(camera, "frost-spray-side", middle + new Vector3(7.0f, 0.8f, 0.0f), middle);
            yield return Hold(caster, staff, true, 0.05f);
            yield return Shoot(camera, "frost-spray-shoulder", new Vector3(1.4f, 3.2f, -4.0f), new Vector3(0.0f, 1.6f, 6.0f));
            // A pale body beside the black one, frozen mid-stride as a copy is told it, to show the tinge on both.
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(2.4f, 0.0f, 6.0f), Quaternion.Euler(0.0f, 200.0f, 0.0f));
            player.GetComponent<PlayerInputReader>().enabled = false;
            var playerLocomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 20; i++)
            {
                playerLocomotion.Tick(new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 200.0f }, Step);
                yield return null;
            }
            yield return Hold(caster, staff, true, 1.2f);
            player.GetComponent<Chill>().PlayRemote(new AbilityEvent(Chill.Froze, player.transform.position, Vector3.zero, 5.0f));
            yield return Hold(caster, staff, false, 0.3f);
            Vector3 chest = swordsman.transform.position + Vector3.up * 1.5f;
            yield return Shoot(camera, "frost-frozen", chest + new Vector3(2.6f, 0.6f, 3.6f), chest);
            Vector3 between = new Vector3(1.2f, 1.4f, 6.0f);
            yield return Shoot(camera, "frost-frozen-front", between + new Vector3(0.6f, 0.6f, -5.5f), between);
            Debug.Log($"CityX frost capture: frozen {swordsman.GetComponent<Chill>().IsFrozen} {player.GetComponent<Chill>().IsFrozen}");

            foreach (GameObject go in new[] { caster, swordsman, player, camera.gameObject, ground, sunObject })
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator Hold(GameObject caster, IEquippableStaff staff, bool held, float seconds)
        {
            var locomotion = caster.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = caster.transform.position + Vector3.up * 2.4f - caster.transform.forward * 4.0f;
                Vector3 look = (new Vector3(0.0f, 1.4f, 6.0f) - eye).normalized;
                staff.Tick(held, eye, look, caster, Step);
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
