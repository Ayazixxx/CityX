using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the road paint in the real city, filmed low along the street where the road bends most under it (owner, 2026-10-17: "the
    /// street decals are often loading in glitching into the terrain. i want them to be apart of the texture of the street"). Writes
    /// Logs/CityShots/road-paint-N.png. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.RoadPaintCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RoadPaintCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Shots = 6;

        private Scene _scene;
        private Camera _camera;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_camera != null) Object.Destroy(_camera.gameObject);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderThePaintWhereTheRoadBends()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<CityX.View.DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = CityX.View.SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(CityX.View.SkyCycle.At(cycle.TimeOfDay));
            }
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            for (int i = 0; i < 400 && !vitals.GetComponent<PlayerLocomotion>().IsGrounded; i++)
            {
                yield return null;
            }
            // Every chunk round the player built afresh, at full detail, by the code under test.
            var streamer = Object.FindFirstObjectByType<CityX.View.CityStreamer>();
            streamer.Rebuild();
            streamer.StreamAround(vitals.transform.position, int.MaxValue);
            yield return new WaitForSecondsRealtime(2.0f);
            vitals.gameObject.SetActive(false);   // out of the shots

            // The paint triangles nearest the player whose neighbours tilt most: where the road bends under a stripe.
            var bends = new List<(float bend, Vector3 at, Vector3 along)>();
            Vector3 home = vitals.transform.position;
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                string name = filter.gameObject.name;
                if ((name != "markings" && name != "lane-dividers") || filter.sharedMesh == null)
                {
                    continue;
                }
                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;
                for (int t = 0; t + 5 < triangles.Length; t += 3)
                {
                    Vector3 here = filter.transform.TransformPoint(vertices[triangles[t]]);
                    if ((here - home).sqrMagnitude > 90.0f * 90.0f)
                    {
                        continue;
                    }
                    float bend = Vector3.Angle(normals[triangles[t]], normals[triangles[t + 3]]);
                    Vector3 along = filter.transform.TransformDirection(vertices[triangles[t + 1]] - vertices[triangles[t]]);
                    along.y = 0.0f;
                    if (bend > 1.5f && along.sqrMagnitude > 1e-4f)
                    {
                        bends.Add((bend, here, along.normalized));
                    }
                }
            }
            Assert.Greater(bends.Count, 0, "no paint where the road bends near the spawn");
            bends.Sort((a, b) => b.bend.CompareTo(a.bend));

            _camera = new GameObject("PaintCam").AddComponent<Camera>();
            _camera.fieldOfView = 45.0f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            _camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            var used = new List<Vector3>();
            int shot = 0;
            foreach ((float bend, Vector3 at, Vector3 along) in bends)
            {
                if (shot >= Shots) break;
                if (used.Exists(u => (u - at).sqrMagnitude < 15.0f * 15.0f)) continue;
                used.Add(at);
                Vector3 side = Vector3.Cross(Vector3.up, along).normalized;
                Vector3 eye = at - along * 5.0f + side * 1.2f + Vector3.up * 0.9f;
                BuildingShapeCapture.Shoot(_camera, BuildingShapeCapture.ShotsFolder, $"road-paint-{shot}", eye, at + along * 2.0f);
                Debug.Log($"CityX: road paint shot {shot} where the road bends {bend:0.0} degrees under it, at {at}");
                shot++;
            }
#else
            yield break;
#endif
        }
    }
}
