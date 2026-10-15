using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE FEET LIE FLAT (owner, 2026-10-15: "make the feet lay flat on the ground i always see the heels hovering, idk if thats apart of the idle
    /// animation"). The real body standing in its idle on flat ground: under each foot the heel and the toe are both on the ground, the sole level.
    /// Measured on the drawn foot: where its heel and toe are, from the foot bone and the foot's own shape (FusedBodyShape).
    /// </summary>
    public sealed class FlatFeetPlayTests
    {
        private GameObject _ground;
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        /// <summary>The heel's and the toe's height above the ground (y 0), metres, from the foot bone (the sole is FootY under the ankle).</summary>
        public static void HeelAndToe(Transform foot, out float heel, out float toe)
        {
            // The foot's shape is in the body's own units (the foot bone's scale takes it to the world), its sole FootY under the ankle,
            // as foot IK reads it.
            float scale = foot.lossyScale.y;
            float back = (FusedBodyShape.FootHeelRadius - FusedBodyShape.FootHeelCentreForward) * 0.9f;
            float forward = (FusedBodyShape.FootToeCentreForward + FusedBodyShape.FootToeRadius) * 0.9f;
            heel = (foot.position + foot.rotation * (new Vector3(0.0f, -BodyRig.FootY, -back) * scale)).y;
            toe = (foot.position + foot.rotation * (new Vector3(0.0f, -BodyRig.FootY, forward) * scale)).y;
        }

        [UnityTest]
        public IEnumerator StandingInTheIdleBothHeelsAndToesAreOnTheGround()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            // Driven as the game drives it, with no keys down (the reader reads a keyboard this test has none of).
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var standing = _player.GetComponent<PlayerLocomotion>();
            float worstHeel = 0.0f, worstToe = 0.0f, worstTilt = 0.0f;
            for (int i = 0; i < 240; i++)
            {
                standing.Tick(new LocomotionInput { YawDegrees = 0.0f }, 1.0f / 60.0f);
                yield return null;
                if (i < 60)
                {
                    continue;   // settled on the ground
                }
                foreach (Transform foot in new[] { left, right })
                {
                    HeelAndToe(foot, out float heel, out float toe);
                    worstHeel = Mathf.Max(worstHeel, heel);
                    worstToe = Mathf.Max(worstToe, toe);
                    worstTilt = Mathf.Max(worstTilt, Mathf.Abs(heel - toe));
                }
            }
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            Debug.Log($"CityX flat feet: idle heels up to {worstHeel * 100.0f:0.0} cm, toes up to {worstToe * 100.0f:0.0} cm, heel to toe {worstTilt * 100.0f:0.0} cm; " +
                $"foot IK {(locomotion.FootIkActive ? "on" : "off")}, body at {_player.transform.position.y * 100.0f:0.0} cm, ankles at {left.position.y * 100.0f:0.0} and " +
                $"{right.position.y * 100.0f:0.0} cm (the sole is {BodyRig.FootY * left.lossyScale.y * 100.0f:0.0} cm under them), foot scale {left.lossyScale.y:0.000}");
            Assert.Less(worstHeel, 0.02f, $"a heel hovers {worstHeel * 100.0f:0.0} cm over the ground in the idle");
            Assert.Less(worstToe, 0.02f, $"a toe hovers {worstToe * 100.0f:0.0} cm over the ground in the idle");
            Assert.Less(worstTilt, 0.015f, $"a sole is tipped {worstTilt * 100.0f:0.0} cm from heel to toe in the idle");
#else
            yield break;
#endif
        }

        /// <summary>Not a test: the feet in the idle, from the side and the front, and the whole body for its proportions, to Logs/CityShots/feet-*.png.</summary>
        [UnityTest]
        [Explicit("renders screenshots; needs a graphics device")]
        public IEnumerator RenderTheFeet()
        {
#if UNITY_EDITOR
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("needs a graphics device: run with -Graphics");
            }
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var lightObject = new GameObject("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            lightObject.transform.rotation = Quaternion.Euler(40.0f, 30.0f, 0.0f);
            var cameraObject = new GameObject("FeetCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 35.0f;
            camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.6f, 0.68f);
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(directory);
            try
            {
                _player.GetComponent<PlayerInputReader>().enabled = false;
                var standing = _player.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 90; i++)
                {
                    standing.Tick(new LocomotionInput { YawDegrees = 0.0f }, 1.0f / 60.0f);
                    yield return null;
                }
                Vector3 feet = _player.transform.position + Vector3.up * 0.12f;
                Shot(camera, directory, "feet-0-side", feet + new Vector3(1.4f, 0.05f, 0.2f), feet);
                Shot(camera, directory, "feet-1-front", feet + new Vector3(0.25f, 0.15f, 1.5f), feet);
                Shot(camera, directory, "feet-2-body", _player.transform.position + new Vector3(4.2f, 1.4f, 4.2f), _player.transform.position + Vector3.up * 1.2f);
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
            }
#else
            yield break;
#endif
        }

        private static void Shot(Camera camera, string directory, string name, Vector3 eye, Vector3 look)
        {
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            const int width = 960, height = 720;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
                Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
