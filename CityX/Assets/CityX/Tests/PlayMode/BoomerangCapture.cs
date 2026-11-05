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
    /// Not a test: renders the Wooden Boomerang (docs/BOOMERANG.md) to Logs/CityShots/boomerang-*.png: the model, in the hand, the arm's swing, and in the air
    /// from behind the thrower and from the side. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BoomerangCapture
    {
        [UnityTest]
        public IEnumerator RenderTheBoomerang()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(120.0f, 1.0f, 120.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Boomerang.prefab");
            Assert.IsNotNull(prefab, "no Boomerang.prefab -- rebuild the scene");
            GameObject alone = Object.Instantiate(prefab, new Vector3(30.0f, 1.0f, 0.0f), Quaternion.Euler(50.0f, 20.0f, 0.0f));
            yield return Shoot(camera, "boomerang-model", alone.transform.position + new Vector3(0.2f, 0.25f, 0.9f), alone.transform.position);
            Object.Destroy(alone);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<BoomerangThrower>().ThrownPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownBoomerang.prefab");
            BoomerangItem item = Object.Instantiate(prefab).GetComponent<BoomerangItem>();
            item.OnEquip(player, ItemInstanceData.None);
            Vector3 body = player.transform.position;
            for (int i = 0; i < 20; i++) yield return null;
            yield return Shoot(camera, "boomerang-held", body + new Vector3(2.6f, 2.0f, 3.2f), body + Vector3.up * 1.2f);
            yield return Shoot(camera, "boomerang-held-near", body + new Vector3(1.6f, 1.6f, 1.6f), body + new Vector3(0.6f, 1.1f, 0.2f));

            var thrower = player.GetComponent<BoomerangThrower>();
            Vector3 from = body + Vector3.up * 1.7f;
            item.Throw(player, from, Vector3.forward);
            string[] names = { "boomerang-swing-1", "boomerang-swing-2", "boomerang-swing-3" };
            foreach (string name in names)
            {
                for (int i = 0; i < 5; i++) yield return null;
                yield return Shoot(camera, name, body + new Vector3(4.0f, 2.2f, 1.0f), body + new Vector3(0.0f, 1.8f, 0.5f));
            }
            ThrownBoomerang flying = thrower.Flying;
            Vector3 at = flying.transform.position;
            yield return Shoot(camera, "boomerang-air-behind", body + new Vector3(0.87f, 3.2f, -5.0f), at);
            at = thrower.Flying.transform.position;
            camera.fieldOfView = 15.0f;
            yield return Shoot(camera, "boomerang-air-close-behind", at + new Vector3(0.0f, 0.6f, -3.0f), at);
            at = thrower.Flying.transform.position;
            yield return Shoot(camera, "boomerang-air-close-side", at + new Vector3(3.0f, 0.3f, 0.0f), at);

            foreach (GameObject go in new[] { player, item.gameObject, camera.gameObject, ground, sun.gameObject })
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
