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
    /// Not a test: renders the Magic Wand and the Magic Staff (docs/MAGIC_WAND.md) to Logs/CityShots/magic-*.png -- the wand's model on its own,
    /// carried at the side, pointed with a speck just gone and in flight, and the staff shooting its speck, at dusk so the glow shows. Explicit;
    /// run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.MagicWandCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class MagicWandCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheWandAndTheStaff()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(80.0f, 1.0f, 80.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.5f;
            sun.transform.rotation = Quaternion.Euler(20.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.22f, 0.3f);

            GameObject wandPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/MagicWand.prefab");
            Assert.IsNotNull(wandPrefab, "no MagicWand.prefab -- rebuild the scene");
            GameObject alone = Object.Instantiate(wandPrefab, new Vector3(6.0f, 1.0f, 0.0f), Quaternion.Euler(0.0f, 0.0f, -60.0f));
            Vector3 middle = alone.transform.TransformPoint(new Vector3(0.0f, 0.13f, 0.0f));
            yield return Shoot(camera, "magic-wand-model", middle + new Vector3(0.0f, 0.12f, 0.62f), middle);
            yield return Shoot(camera, "magic-wand-handle", alone.transform.position + new Vector3(0.05f, 0.06f, 0.25f), alone.transform.position);
            Object.Destroy(alone);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<MagicSpeckCaster>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var wand = Object.Instantiate(wandPrefab).GetComponent<MagicWand>();
            wand.OnEquip(player, ItemInstanceData.None);
            yield return Settle(locomotion, 40);
            Vector3 body = player.transform.position;
            yield return Shoot(camera, "magic-wand-carried", body + new Vector3(2.8f, 1.9f, 3.0f), body + Vector3.up * 1.3f);

            Vector3 eye = body + Vector3.up * 2.6f - Vector3.forward * 4.0f;
            for (int i = 0; i < 4; i++)
            {
                wand.Tick(i == 0, eye, Vector3.forward, player, Step);
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
            yield return Shoot(camera, "magic-wand-pointed", body + new Vector3(3.2f, 2.0f, 1.6f), body + new Vector3(0.0f, 1.6f, 1.2f));
            yield return Shoot(camera, "magic-wand-speck", body + new Vector3(1.6f, 2.2f, 4.0f), body + new Vector3(0.3f, 2.0f, 6.0f));
            yield return Shoot(camera, "magic-wand-pointed-game-view", body + new Vector3(0.87f, 3.2f, -5.0f), body + new Vector3(0.87f, 2.6f, 10.0f));

            wand.OnUnequip(player);
            yield return null;
            var staff = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/MagicStaff.prefab"))
                .GetComponent<MagicStaff>();
            Assert.IsNotNull(staff, "no MagicStaff.prefab -- rebuild the scene");
            staff.OnEquip(player, ItemInstanceData.None);
            // Held still for a second first: the wand's shot still paces the caster, and a staff waits it out too.
            for (int i = 0; i < 68; i++)
            {
                staff.Tick(i == 60, eye, Vector3.forward, player, Step);
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
            yield return Shoot(camera, "magic-staff", body + new Vector3(3.4f, 2.4f, -3.2f), body + new Vector3(0.0f, 2.0f, 2.5f));

            foreach (GameObject go in new[] { player, staff.gameObject, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator Settle(PlayerLocomotion locomotion, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
