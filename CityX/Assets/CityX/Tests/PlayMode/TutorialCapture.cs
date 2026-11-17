using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the tutorial's objective box on the real HUD in Old Guy's den, then the ending's volley through the den's windows and the
    /// two of them down (docs/TUTORIAL.md). Written to Logs/CityShots/tutorial-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class TutorialCapture
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            ScreenBlack.FadeTo(0.0f, 0.0f);
            TutorialHouseDamage.Shared.Clear();
        }

        [UnityTest]
        public IEnumerator RenderTheObjectiveAndTheEnding()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            string folder = BuildingShapeCapture.ShotsFolder;
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            var sun = new GameObject("Sun").AddComponent<Light>();
            _made.Add(sun.gameObject);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(35.0f, -60.0f, 0.0f);
            var lamp = new GameObject("DenLamp").AddComponent<Light>();
            _made.Add(lamp.gameObject);
            lamp.type = LightType.Point;
            lamp.range = 14.0f;
            lamp.intensity = 2.5f;
            lamp.transform.position = new Vector3(23.0f, House.LevelY(1) + 4.0f, 17.0f);

            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(25.0f, House.LevelY(1) + 0.1f, 14.5f), Quaternion.Euler(0.0f, 300.0f, 0.0f));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var oldGuy = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/OldGuy.prefab"),
                House.OldGuyAtTheSteps + Vector3.up * 0.1f, Quaternion.identity);
            _made.Add(oldGuy);

            GameObject hudObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/UI/PlayerHud.prefab"));
            _made.Add(hudObject);
            var hud = hudObject.GetComponent<PlayerHud>();
            hud.Target = player.GetComponent<PlayerVitals>();
            var camera = new GameObject("TutorialShotCamera").AddComponent<Camera>();
            _made.Add(camera.gameObject);
            camera.fieldOfView = 60.0f;
            var canvas = hudObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            var director = hudObject.GetComponent<TutorialDirector>();
            yield return null;
            yield return null;

            Vector3 eye = new Vector3(19.6f, House.LevelY(1) + 3.4f, 12.6f), look = new Vector3(25.0f, House.LevelY(1) + 1.6f, 17.0f);
            Assert.IsNull(director.JumpTo(TutorialStep.Intruder));
            for (int i = 0; i < 30; i++) yield return null;   // past the box's fade-in
            BuildingShapeCapture.Shoot(camera, folder, "tutorial-objective", eye, look);
            Assert.IsNull(director.JumpTo(TutorialStep.ReportBack));
            for (int i = 0; i < 30; i++) yield return null;
            BuildingShapeCapture.Shoot(camera, folder, "tutorial-objective-den", eye, look);

            Assert.IsNull(director.JumpTo(TutorialStep.Ambush));
            for (int i = 0; i < 27; i++) yield return null;   // 0.45 s: the first windows out, the trails flying
            BuildingShapeCapture.Shoot(camera, folder, "tutorial-volley", eye, look);
            for (int i = 0; i < 75; i++) yield return null;   // 1.7 s: both down, the shooting going on
            BuildingShapeCapture.Shoot(camera, folder, "tutorial-down", eye, look);
            for (int i = 0; i < 60; i++) yield return null;
            BuildingShapeCapture.Shoot(camera, folder, "tutorial-down-after", eye, look);
#else
            yield break;
#endif
        }
    }
}
