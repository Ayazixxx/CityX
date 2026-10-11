using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A real player leans forward as it sprints and back as it stops (PlayerLocomotion.ApplyBodyLean), the whole body tilting at the feet. With a
    /// graphics device it saves Logs/CityShots/lean-*.png from the side.
    /// </summary>
    public sealed class BodyLeanPlayTests
    {
        private GameObject _ground;
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_ground != null) Object.Destroy(_ground);
            if (_player != null) Object.Destroy(_player);
        }

        [UnityTest]
        public IEnumerator SprintingLeansForwardAndStoppingLeansBack()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0, -0.5f, 0);
            _ground.transform.localScale = new Vector3(400, 1, 400);
            Physics.SyncTransforms();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            yield return null;
            yield return null;
            const float dt = 1.0f / 60.0f;
            bool graphics = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

            float mostForward = 0.0f;
            for (int i = 0; i < 120; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = true }, dt);
                yield return null;
                mostForward = Mathf.Max(mostForward, Vector3.Dot(locomotion.LeanTilt, _player.transform.forward));
                if (graphics && (i == 20 || i == 110)) Shoot(i == 20 ? "lean-setting-off" : "lean-sprinting");
            }
            Assert.Greater(Vector3.Dot(locomotion.LeanTilt, _player.transform.forward), 2.0f, "a sprint does not lean forward");
            Assert.Greater(mostForward, 4.0f, "setting off does not lean into the push");

            float mostBack = 0.0f;
            for (int i = 0; i < 40; i++)
            {
                locomotion.Tick(default, dt);
                yield return null;
                mostBack = Mathf.Min(mostBack, Vector3.Dot(locomotion.LeanTilt, _player.transform.forward));
                if (graphics && i == 6) Shoot("lean-stopping");
            }
            Assert.Less(mostBack, -1.0f, "stopping from a sprint does not lean back");
            Assert.LessOrEqual(locomotion.LeanDegrees, BodyLean.MaxLeanDegrees + 0.01f);
#else
            yield break;
#endif
        }

        /// <summary>Tilts the visual body in Update every frame, as Jetpack.TickTilt does.</summary>
        private sealed class TiltsInUpdate : MonoBehaviour
        {
            public Transform Body;
            public Quaternion Tilt;

            private void Update()
            {
                Body.localRotation = Tilt;
            }
        }

        /// <summary>Reads the body's tilt after every LateUpdate, the lean's included: what the frame actually draws.</summary>
        [DefaultExecutionOrder(10000)]
        private sealed class ReadsTheDrawnTilt : MonoBehaviour
        {
            public Transform Body;
            public Quaternion Expected;
            public float WorstDegrees;
            public int Frames;

            private void LateUpdate()
            {
                WorstDegrees = Mathf.Max(WorstDegrees, Quaternion.Angle(Expected, Body.localRotation));
                Frames++;
            }
        }

        /// <summary>
        /// The jetpack leans the same transform the body lean drives. The lean once overwrote it every LateUpdate and a flying body stood bolt
        /// upright (owner, 2026-10-10: "the jetpack lean is broken the character stays upright").
        /// </summary>
        [UnityTest]
        public IEnumerator AJetpacksTiltIsNotUndoneByTheLean()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0, -0.5f, 0);
            _ground.transform.localScale = new Vector3(400, 1, 400);
            Physics.SyncTransforms();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            yield return null;

            Transform body = _player.GetComponentInChildren<Animator>().transform;
            Quaternion upright = body.localRotation;
            var tilter = _player.AddComponent<TiltsInUpdate>();
            tilter.Body = body;
            tilter.Tilt = Quaternion.Euler(35.0f, 0.0f, 10.0f);
            // A frame for the tilter's first Update: added from here, after this frame's Updates have run.
            yield return null;
            var reader = _player.AddComponent<ReadsTheDrawnTilt>();
            reader.Body = body;
            reader.Expected = tilter.Tilt;
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            Assert.Greater(reader.Frames, 5);
            Assert.Less(reader.WorstDegrees, 2.0f, $"the drawn body was up to {reader.WorstDegrees:0.0} degrees off the jetpack's tilt");

            // The jetpack levels its own tilt when it lands (it writes the body upright); the lean must not hold the old tilt on.
            Object.Destroy(reader);
            tilter.Tilt = upright;
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            Object.Destroy(tilter);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            Assert.Less(Quaternion.Angle(upright, body.localRotation), 2.0f, "once the jetpack levels it, the standing body is upright again");
#else
            yield break;
#endif
        }

        private void Shoot(string name)
        {
            var cameraObject = new GameObject("LeanCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40.0f;
            Vector3 centre = _player.transform.position + Vector3.up * 1.3f;
            camera.transform.position = centre + _player.transform.right * 7.0f;
            camera.transform.LookAt(centre);
            RenderTexture rt = RenderTexture.GetTemporary(640, 640, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            image.Apply();
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])enc.Invoke(null, new object[] { image }));
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(image);
            Object.Destroy(cameraObject);
        }
    }
}
