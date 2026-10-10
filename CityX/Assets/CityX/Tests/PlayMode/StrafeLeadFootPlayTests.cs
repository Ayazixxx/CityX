using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A real player side-stepping (A/D) turns its lead leg toward the step and the other one less (PlayerLocomotion.ApplyStrafeLeadFoot); walking
    /// straight it turns neither. With a graphics device it also saves Logs/CityShots/strafe-*.png, the side-step seen from in front.
    /// </summary>
    public sealed class StrafeLeadFootPlayTests
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
        public IEnumerator SideSteppingTurnsTheLeadLegAndWalkingStraightDoesNot()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0, -0.5f, 0);
            _ground.transform.localScale = new Vector3(200, 1, 200);
            Physics.SyncTransforms();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            yield return null;
            yield return null;
            const float dt = 1.0f / 60.0f;

            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.right }, dt);
                yield return null;
                if (i % 15 == 14 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Shoot($"strafe-right-{i / 15}");
                }
            }
            Assert.Greater(locomotion.RightLegYaw, PlayerLocomotion.StrafeLeadFootDegrees * 0.6f, "stepping right, the right leg does not lead");
            Assert.Greater(locomotion.LeftLegYaw, 0.0f, "the trailing leg turns the same way");
            Assert.Greater(locomotion.RightLegYaw, locomotion.LeftLegYaw * 1.5f, "the lead leg turns more than the trailing one");

            for (int i = 0; i < 60; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up }, dt);
                yield return null;
            }
            Assert.AreEqual(0.0f, locomotion.RightLegYaw, 1.0f, "walking straight ahead, a leg stays turned");
            Assert.AreEqual(0.0f, locomotion.LeftLegYaw, 1.0f);
#else
            yield break;
#endif
        }

        private void Shoot(string name)
        {
            var cameraObject = new GameObject("StrafeCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40.0f;
            Vector3 centre = _player.transform.position + Vector3.up * 1.2f;
            camera.transform.position = centre + _player.transform.forward * 6.0f + Vector3.up * 0.8f;
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
