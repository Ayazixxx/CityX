using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the tutorial house (docs/OLD_GUYS_HOUSE.md) to Logs/CityShots/house-*.png -- outside from the street and from the
    /// garden, and inside: the garage, the kitchen's knife block, the living room from the gallery, the grandson's desk, the study's glass case,
    /// the swordsman in the bedroom, the bridge to the tree house. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.OldGuysHouseCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class OldGuysHouseCapture
    {
        [UnityTest]
        public IEnumerator RenderTheHouse()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            var made = new List<GameObject>();
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.PrefabFor = id => OldGuysHousePlayTests.PrefabPaths.TryGetValue(id, out string path) ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            Time.captureFramerate = 60;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50.0f, -40.0f, 0.0f);
            made.Add(sun.gameObject);
            OldGuysHousePlayTests.MakeHouse(made, true, true);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.88f);
            made.Add(camera.gameObject);
            for (int i = 0; i < 30; i++) yield return null;

            camera.fieldOfView = 45.0f;
            yield return Shoot(camera, "house-street", new Vector3(-16, 10, -26), new Vector3(10, 5, 6));
            yield return Shoot(camera, "house-garden", new Vector3(30, 16, 44), new Vector3(8, 6, 16));
            yield return Shoot(camera, "house-side", new Vector3(36, 7, 4), new Vector3(18, 4, 8));
            camera.fieldOfView = 70.0f;
            yield return Shoot(camera, "house-garage", new Vector3(8.4f, 2.9f, 0.8f), new Vector3(3.0f, 0.8f, 9.0f));
            yield return Shoot(camera, "house-kitchen", new Vector3(6.4f, 6.6f, 10.2f), new Vector3(0.6f, 5.2f, 13.6f));
            yield return Shoot(camera, "house-living-from-gallery", new Vector3(8.4f, 10.6f, 8.2f), new Vector3(1.0f, 5.0f, 3.5f));
            yield return Shoot(camera, "house-grandson", new Vector3(7.6f, 10.8f, 9.8f), new Vector3(2.4f, 8.8f, 15.4f));
            yield return Shoot(camera, "house-study", new Vector3(14.8f, 10.6f, 5.2f), new Vector3(18.9f, 8.9f, 1.4f));
            yield return Shoot(camera, "house-bedroom", new Vector3(14.6f, 10.6f, 9.5f), new Vector3(17.5f, 8.8f, 12.5f));
            yield return Shoot(camera, "house-bridge", new Vector3(18.8f, 10.8f, 17.6f), new Vector3(6.0f, 9.0f, 26.5f));
            yield return Shoot(camera, "house-den", new Vector3(14.6f, 6.6f, 9.6f), new Vector3(18.5f, 4.6f, 14.5f));

            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in made) if (go != null) Object.Destroy(go);
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.PrefabFor = Inventory.FindPrefab;
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
