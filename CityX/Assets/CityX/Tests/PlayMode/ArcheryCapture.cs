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
    /// Not a test: renders the bow and the crossbow (docs/ARCHERY.md) to Logs/CityShots/archery-*.png -- the bow carried, aimed, at full draw
    /// (from the side and over the shoulder), an arrow stuck in someone, and the crossbow carried and aimed. Explicit; run by name with
    /// -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.ArcheryCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ArcheryCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheBowAndTheCrossbow()
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
            GameObject archer = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            archer.GetComponent<PlayerInputReader>().enabled = false;
            GameObject target = Object.Instantiate(prefab, new Vector3(0.3f, 0.05f, 9.0f), Quaternion.Euler(0.0f, 200.0f, 0.0f));
            target.GetComponent<PlayerInputReader>().enabled = false;
            Vector3 middle = archer.transform.position + Vector3.up * 1.6f;

            archer.GetComponent<Inventory>().EquipItemId(ItemRegistry.Bow, archer);
            var bow = archer.GetComponentInChildren<BowWeapon>();
            yield return Hold(archer, bow, false, false, 0.5f);
            CrouchCapture.Views(camera, archer, "archery-bow-carried");
            yield return Hold(archer, bow, false, true, 0.4f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "archery-bow-aimed", middle + new Vector3(3.0f, 0.6f, 2.5f), middle);
            yield return Hold(archer, bow, true, true, ArcheryRules.FullDrawSeconds + 0.3f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "archery-bow-drawn-side", middle + new Vector3(3.2f, 0.4f, 0.3f), middle);
            yield return ArcaneOrbCapture.ShootFacing(camera, "archery-bow-drawn-front", middle + new Vector3(1.5f, 0.3f, 3.0f), middle);
            yield return ArcaneOrbCapture.ShootFacing(camera, "archery-bow-drawn-shoulder", middle + new Vector3(0.9f, 1.0f, -2.5f), middle + Vector3.forward * 3.0f);
            yield return Hold(archer, bow, false, true, 0.5f);
            Vector3 chest = target.transform.position + Vector3.up * 1.4f;
            yield return ArcaneOrbCapture.ShootFacing(camera, "archery-arrow-stuck", chest + new Vector3(2.0f, 0.4f, -2.0f), chest);
            archer.GetComponent<Inventory>().UnequipToSlot(EquipSlots.Primary, archer);
            yield return Hold(archer, null, false, false, 0.2f);

            archer.GetComponent<Inventory>().EquipItemId(ItemRegistry.Crossbow, archer);
            var crossbow = archer.GetComponentInChildren<PistolWeapon>();
            yield return Hold(archer, null, false, false, 0.5f);
            CrouchCapture.Views(camera, archer, "archery-crossbow-carried");
            var locomotion = archer.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < 0.6f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f, Aiming = true, GunEquipped = true }, Step);
                yield return null;
            }
            yield return ArcaneOrbCapture.ShootFacing(camera, "archery-crossbow-aimed", middle + new Vector3(3.0f, 0.6f, 2.5f), middle);
            crossbow.Fire(middle + Vector3.forward * 0.3f, Vector3.forward, true, archer);
            yield return null;
            yield return ArcaneOrbCapture.ShootFacing(camera, "archery-crossbow-shot", middle + new Vector3(3.2f, 0.4f, 0.3f), middle + Vector3.forward * 1.5f);

            Object.Destroy(archer);
            Object.Destroy(target);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Hold(GameObject player, BowWeapon bow, bool held, bool aiming, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 origin = player.transform.position + Vector3.up * 1.6f + player.transform.forward * 0.3f;
                if (bow != null)
                {
                    bow.Draw(held, origin, player.transform.forward, aiming, player, Step);
                }
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y, Aiming = aiming }, Step);
                yield return null;
            }
        }
    }
}
