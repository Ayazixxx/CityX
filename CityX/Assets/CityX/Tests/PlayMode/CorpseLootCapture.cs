using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders a fallen swordsman searched (docs/LOOTING.md, bodies) to Logs/CityShots/corpse-loot-*.png: the loot window listing his
    /// Knight Helmet, then his body after it was taken. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CorpseLootCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CorpseLootCapture
    {
        [UnityTest]
        public IEnumerator RenderAFallenSwordsmanSearched()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            var made = new List<GameObject>();
            Time.captureFramerate = 60;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);
            made.Add(sun.gameObject);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            ground.GetComponent<Renderer>().material = new Material(CityX.Core.LitShader.Find()) { color = new Color(0.55f, 0.56f, 0.58f) };
            made.Add(ground);

            var swordsman = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                new Vector3(0.0f, 0.05f, 3.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            made.Add(swordsman);
            swordsman.GetComponent<SwordsmanBrain>().enabled = false;
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            made.Add(player);
            if (player.GetComponent<Inventory>() == null) player.AddComponent<Inventory>();
            player.GetComponent<PlayerInputReader>().enabled = false;
            var window = new GameObject("LootWindow").AddComponent<CityX.UI.LootWindowView>();
            made.Add(window.gameObject);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            made.Add(camera.gameObject);
            camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.88f);
            camera.fieldOfView = 60.0f;
            yield return null;

            swordsman.GetComponent<PlayerVitals>().TakeDamage(10000.0f);
            for (int i = 0; i < 120; i++) yield return null;   // fallen and still
            var loot = swordsman.GetComponent<CorpseLoot>();
            Assert.IsNotNull(loot.Container, "his body cannot be searched");
            Vector3 body = loot.Container.Centre;
            var searcher = player.GetComponent<LootSearcher>();
            searcher.Search(loot.Container);
            for (int i = 0; i < 5; i++) yield return null;
            Canvas windowCanvas = window.GetComponentInChildren<Canvas>(true);
            windowCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            windowCanvas.worldCamera = camera;
            windowCanvas.planeDistance = 0.5f;
            yield return Shoot(camera, "corpse-loot-window", body + new Vector3(-2.2f, 2.6f, -3.0f), body);

            searcher.TakeSelected();
            searcher.CloseWindow();
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shoot(camera, "corpse-loot-taken", body + new Vector3(-2.2f, 2.6f, -3.0f), body);

            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in made) if (go != null) Object.Destroy(go);
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
