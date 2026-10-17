using System.Collections;
using System.IO;
using CityX.Character;
using CityX.UI;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders graffiti in the real city to Logs/CityShots/graffiti-*.png -- the canvas open on a wall near the spawn with a tag
    /// sprayed on it, then the tag up on the wall by day (straight on and from the side, where the sun's light and the sheen show), and at
    /// night in the dark and under a lamp held beside it (the paint must take the light as the wall under it does).
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.GraffitiCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GraffitiCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Camera _camera;
        private Camera _sceneCamera;
        private GameObject _lamp;
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (GraffitiMenuView.IsAnyOpen) GraffitiMenuView.Current.Close(apply: false);
            GraffitiWall.Clear();
            if (_lamp != null) Object.Destroy(_lamp);
            if (_camera != null) Object.Destroy(_camera.gameObject);
            if (_sceneCamera != null) _sceneCamera.enabled = true;
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private void Render(string name, Vector3 eye, Vector3 look)
        {
            _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            const int width = 1280, height = 720;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                _camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(_directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
                Object.Destroy(image);
            }
            finally
            {
                _camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>The nearest upright wall round the body at chest height, within 40 m.</summary>
        private static bool FindWall(Transform body, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;
            float best = float.MaxValue;
            Vector3 chest = body.position + Vector3.up * 1.5f;
            for (int i = 0; i < 32; i++)
            {
                float angle = i / 32.0f * Mathf.PI * 2.0f;
                var direction = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
                foreach (RaycastHit hit in Physics.RaycastAll(chest, direction, 40.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(body) || hit.collider.GetComponentInParent<IDamageable>() != null)
                    {
                        continue;
                    }
                    // A real wall: upright, and wide and tall enough round the spot for a whole piece (no lamp post or bollard).
                    if (Mathf.Abs(hit.normal.y) < 0.1f && hit.distance > 3.0f && hit.distance < best && WideWall(hit))
                    {
                        best = hit.distance;
                        point = hit.point;
                        normal = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
                    }
                }
            }
            return best < float.MaxValue;
        }

        private static bool WideWall(RaycastHit hit)
        {
            Vector3 n = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, n);
            foreach (Vector3 offset in new[] { across * 1.2f, -across * 1.2f, Vector3.up * 1.2f })
            {
                Vector3 from = hit.point + offset + n * 0.5f;
                if (!Physics.Raycast(from, -n, out RaycastHit h, 1.0f, ~0, QueryTriggerInteraction.Ignore) || Vector3.Dot(h.normal, n) < 0.95f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A tag: "CX" in fat letters, outlined, with drips, an underline swoosh and a sparkle -- what a player might spray.</summary>
        private static void SprayTag(GraffitiCanvas canvas)
        {
            Color outline = new Color(0.05f, 0.05f, 0.07f);
            Color fill = new Color(0.98f, 0.35f, 0.75f);
            Color fill2 = new Color(0.2f, 0.85f, 0.95f);
            var c = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.Lerp(50.0f, 310.0f, i / 24.0f) * Mathf.Deg2Rad;
                c.Add(new Vector2(82.0f + Mathf.Cos(a) * 48.0f, 140.0f + Mathf.Sin(a) * 52.0f));
            }
            Vector2[][] x = { new[] { new Vector2(146, 90), new Vector2(222, 190) }, new[] { new Vector2(146, 190), new Vector2(222, 90) } };
            Vector2[][] drips = { new[] { new Vector2(70, 90), new Vector2(70, 58) }, new[] { new Vector2(206, 92), new Vector2(206, 66) } };
            var swoosh = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20.0f;
                swoosh.Add(new Vector2(Mathf.Lerp(34, 236, t), 44.0f + Mathf.Sin(t * Mathf.PI) * 14.0f - t * 10.0f));
            }

            for (int pass = 0; pass < 2; pass++)
            {
                Line(canvas, c, 17.0f, outline);
                foreach (Vector2[] l in x) Line(canvas, l, 17.0f, outline);
                foreach (Vector2[] d in drips) Line(canvas, d, 7.0f, outline);
                Line(canvas, swoosh, 10.0f, outline);
            }
            for (int pass = 0; pass < 3; pass++)
            {
                Line(canvas, c, 9.5f, fill);
                foreach (Vector2[] l in x) Line(canvas, l, 9.5f, fill2);
                foreach (Vector2[] d in drips) Line(canvas, d, 3.5f, fill);
                Line(canvas, swoosh, 4.5f, Color.white);
            }
            canvas.Spray(226.0f, 214.0f, 7.0f, Color.white, 1.0f);
            canvas.Stroke(new Vector2(214, 214), new Vector2(238, 214), 1.5f, Color.white);
            canvas.Stroke(new Vector2(226, 202), new Vector2(226, 226), 1.5f, Color.white);
        }

        private static void Line(GraffitiCanvas canvas, System.Collections.Generic.IList<Vector2> points, float radius, Color colour)
        {
            for (int i = 0; i + 1 < points.Count; i++)
            {
                canvas.Stroke(points[i], points[i + 1], radius, colour);
            }
        }

        [UnityTest]
        public IEnumerator RenderGraffiti()
        {
#if UNITY_EDITOR
            // Rendering with no graphics device crashes Unity outright (a regex filter runs Explicit captures too): needs -Graphics.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("needs a graphics device: run with -Graphics");
            }
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            var reader = vitals.GetComponent<PlayerInputReader>();
            reader.enabled = false;

            _sceneCamera = Camera.main;
            _sceneCamera.enabled = false;
            var cameraObject = new GameObject("GraffitiCam") { tag = "MainCamera" };
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 55.0f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            yield return null;

            Transform body = vitals.transform;
            Assert.IsTrue(FindWall(body, out Vector3 wallPoint, out Vector3 normal), "no wall near the spawn to paint");
            Vector3 centre = new Vector3(wallPoint.x, body.position.y + 1.6f, wallPoint.z);
            Vector3 across = Vector3.Cross(Vector3.up, normal);

            // The canvas open on the wall, with the tag sprayed on it (the overlay drawn by this camera for the shot).
            var menu = Object.FindFirstObjectByType<GraffitiMenuView>();
            Assert.IsNotNull(menu, "the HUD has no graffiti canvas");
            menu.OpenAt(reader, centre, normal);
            SprayTag(menu.Canvas);
            menu.ShowPaint();
            Canvas overlay = menu.Overlay;
            Assert.IsNotNull(overlay, "the canvas did not open");
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = _camera;
            overlay.planeDistance = 0.5f;
            yield return null;
            Render("graffiti-0-canvas", centre + normal * 6.0f, centre);
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            menu.Close(apply: true);
            reader.enabled = false;
            Assert.AreEqual(1, GraffitiWall.Count, "the tag did not go up");
            GraffitiPiece piece = GraffitiWall.All()[0];
            yield return null;

            Render("graffiti-1-day", centre + normal * 5.5f + Vector3.up * 0.3f, centre);
            Render("graffiti-2-day-side", centre + normal * 3.0f + across * 4.5f + Vector3.up * 0.4f, centre);
            Render("graffiti-3-day-street", centre + normal * 12.0f - across * 3.0f + Vector3.up * 1.5f, centre + Vector3.up * 0.5f);

            // Night: the paint is as dark as the wall, and a lamp beside it lights both alike.
            if (cycle != null)
            {
                cycle.TimeOfDay = SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.4f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            yield return null;
            Render("graffiti-4-night", centre + normal * 5.5f + Vector3.up * 0.3f, centre);
            _lamp = new GameObject("Lamp");
            _lamp.transform.position = centre + normal * 1.6f + across * 1.8f + Vector3.up * 0.6f;
            var lamp = _lamp.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 7.0f;
            lamp.intensity = 6.0f;
            lamp.color = new Color(1.0f, 0.85f, 0.6f);
            lamp.shadows = LightShadows.None;
            yield return null;
            Render("graffiti-5-night-lamp", centre + normal * 5.5f + Vector3.up * 0.3f, centre);

            Debug.Log($"CityX graffiti capture: piece {piece.Id} at {piece.Centre} facing {piece.Normal}, {piece.Paint.Length} bytes of paint " +
                $"(of {GraffitiCanvas.MaxEncodedBytes} allowed); the wall {Vector3.Distance(body.position, wallPoint):0.0} m from the spawn");
#else
            yield break;
#endif
        }
    }
}
