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
    /// Not a test: renders the Giant Boomerang (docs/BOOMERANG.md) to Logs/CityShots/giantrang-*.png: on the back, drawn into the aim, the aimed throw and the
    /// quick throw off the back, each from the game's camera behind the shoulder and from the side, and in the air. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GiantBoomerangCapture
    {
        [UnityTest]
        public IEnumerator RenderTheGiantBoomerang()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(160.0f, 1.0f, 160.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/GiantBoomerang.prefab");
            Assert.IsNotNull(prefab, "no GiantBoomerang.prefab -- rebuild the scene");
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var thrower = player.AddComponent<BoomerangThrower>();
            thrower.GiantThrownPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownGiantBoomerang.prefab");
            GiantBoomerangItem item = Object.Instantiate(prefab).GetComponent<GiantBoomerangItem>();
            item.OnEquip(player, ItemInstanceData.None);
            Vector3 body = player.transform.position;
            for (int i = 0; i < 20; i++) yield return null;

            yield return Views(camera, "giantrang-back", body);
            yield return Shoot(camera, "giantrang-back-behind", body + new Vector3(0.3f, 2.2f, -3.2f), body + Vector3.up * 1.6f);

            // Drawn into the aim: halfway (the grab), then held.
            thrower.SetGiantAiming(true);
            yield return Frames(GiantBoomerangMoves.DrawGrabSeconds);
            yield return Views(camera, "giantrang-draw-grab", body);
            yield return Frames(0.6f);
            yield return Views(camera, "giantrang-aim", body);
            // The aimed throw: the wind's end, and the release.
            thrower.StartGiantThrow(Vector3.forward, 0.0f, 0.0f);
            yield return Frames(GiantBoomerangMoves.AimedReleaseSeconds * 0.5f);
            yield return Views(camera, "giantrang-aimed-mid", body);
            yield return Frames(GiantBoomerangMoves.AimedReleaseSeconds * 0.5f + 0.05f);
            yield return Views(camera, "giantrang-aimed-released", body);
            yield return Frames(0.25f);
            Assert.IsNotNull(thrower.Flying, "the aimed throw never let it go");
            Vector3 at = thrower.Flying.transform.position;
            yield return Shoot(camera, "giantrang-air-behind", body + new Vector3(0.87f, 2.8f, -4.5f), at);
            at = thrower.Flying.transform.position;
            yield return Shoot(camera, "giantrang-air-side", at + new Vector3(6.0f, 1.0f, 0.0f), at);
            // The catch: the hand out for it as it comes in, the moment it is caught, the arm giving with it, and put back on the back.
            for (float t = 0.0f; t < 8.0f && thrower.IsOut && !thrower.GiantMoves.IsCatching; t += Time.deltaTime) yield return null;
            Assert.IsTrue(thrower.GiantMoves.IsCatching, "the hand never went out for it");
            yield return Shoot(camera, "giantrang-catch-reach-game", body + new Vector3(0.87f, 2.9f, -5.0f), body + new Vector3(0.0f, 1.9f, 2.0f));
            for (float t = 0.0f; t < 8.0f && thrower.IsOut; t += Time.deltaTime) yield return null;
            Assert.IsFalse(thrower.IsOut, "it never came back");
            yield return Views(camera, "giantrang-catch-caught", body);
            yield return Frames(0.2f);
            yield return Views(camera, "giantrang-catch-give", body);
            yield return Frames(0.5f);
            yield return Views(camera, "giantrang-catch-putting-away", body);
            yield return Frames(0.6f);
            yield return Views(camera, "giantrang-caught", body);

            // The quick throw straight off the back.
            yield return Frames(BoomerangRules.ThrowEverySeconds);
            thrower.StartGiantThrow(Vector3.forward, 0.0f, 0.0f);
            yield return Frames(GiantBoomerangMoves.QuickGrabSeconds);
            yield return Views(camera, "giantrang-quick-grab", body);
            yield return Frames((GiantBoomerangMoves.QuickReleaseSeconds - GiantBoomerangMoves.QuickGrabSeconds) * 0.6f);
            yield return Views(camera, "giantrang-quick-wind", body);
            yield return Frames((GiantBoomerangMoves.QuickReleaseSeconds - GiantBoomerangMoves.QuickGrabSeconds) * 0.4f + 0.03f);
            yield return Views(camera, "giantrang-quick-released", body);

            foreach (GameObject go in new[] { player, item != null ? item.gameObject : null, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
            foreach (ThrownBoomerang b in Object.FindObjectsByType<ThrownBoomerang>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Frames(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        /// <summary>From the game's camera behind the right shoulder, and from the right side.</summary>
        private static IEnumerator Views(Camera camera, string name, Vector3 body)
        {
            yield return Shoot(camera, name + "-game", body + new Vector3(0.87f, 2.9f, -5.0f), body + new Vector3(0.0f, 1.9f, 2.0f));
            yield return Shoot(camera, name + "-side", body + new Vector3(6.0f, 1.8f, 0.6f), body + new Vector3(0.0f, 1.5f, 0.3f));
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
