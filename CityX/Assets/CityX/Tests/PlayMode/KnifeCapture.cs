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
    /// Not a test: renders the kitchen knife (docs/KNIFE.md) to Logs/CityShots/knife-*.png -- the stand-in model on its own, flat on and from
    /// the edge, and held in the hand, close. Its swings are in SwordStrikeCapture (strike-knife-*.png). Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.KnifeCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class KnifeCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheKnife()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/KitchenKnife.prefab");
            Assert.IsNotNull(prefab, "no KitchenKnife.prefab -- rebuild the scene");
            GameObject alone = Object.Instantiate(prefab, new Vector3(5.0f, 1.0f, 0.0f), Quaternion.identity);
            Vector3 middle = alone.transform.position + Vector3.up * 0.04f;
            yield return Shoot(camera, "knife-model-flat", middle + Vector3.forward * 0.75f, middle);
            yield return Shoot(camera, "knife-model-quarter", middle + new Vector3(0.45f, 0.25f, 0.55f), middle);
            yield return Shoot(camera, "knife-model-edge", middle + Vector3.left * 0.75f, middle);
            Object.Destroy(alone);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var knife = Object.Instantiate(prefab).GetComponent<SwordWeapon>();
            knife.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 60; i++)
            {
                locomotion.Tick(new LocomotionInput { Sword1HEquipped = true }, Step);
                yield return null;
            }
            Vector3 hand = knife.BladeBaseWorld;
            yield return Shoot(camera, "knife-in-hand", hand + new Vector3(0.9f, 0.35f, 0.9f), hand + Vector3.up * 0.05f);
            yield return Shoot(camera, "knife-held", player.transform.position + new Vector3(2.6f, 2.0f, 3.4f), player.transform.position + Vector3.up * 1.3f);

            foreach (GameObject go in new[] { player, knife.gameObject, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
