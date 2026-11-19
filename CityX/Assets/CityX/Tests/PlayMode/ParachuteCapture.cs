using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// PICTURES OF A PARACHUTE JUMP (docs/PARACHUTE.md), Explicit, with -Graphics: the pack on the back; spread face down in the fall and diving;
    /// the pull; the canopy opening; flying under it from the game's camera, the side and the front; a turn; the flare over the ground; the
    /// landing. Written to Logs/CityShots/chute-*.png to be looked at: green tests do not mean it looks right.
    /// </summary>
    [Explicit]
    public sealed class ParachuteCapture
    {
        private const float Dt = 1.0f / 60.0f;
        private const int Width = 960, Height = 600;

        private string _directory;
        private GameObject _scenery;
        private GameObject _player;
        private Parachute _chute;
        private PlayerLocomotion _locomotion;
        private Camera _followCamera;
        private ThirdPersonCamera _follow;
        private Camera _side;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _scenery, _player, _chute != null ? _chute.gameObject : null, _followCamera != null ? _followCamera.gameObject : null, _side != null ? _side.gameObject : null })
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        [UnityTest]
        public IEnumerator RenderAJump()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);
            _scenery = new GameObject("Scenery");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(_scenery.transform);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(600, 1, 600);
            ground.GetComponent<Renderer>().material.color = new Color(0.42f, 0.47f, 0.38f);
            for (int i = 0; i < 12; i++)
            {
                // Blocks on the ground to see the height and the speed by.
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.SetParent(_scenery.transform);
                block.transform.position = new Vector3((i % 4 - 1.5f) * 30.0f, 4.0f, (i / 4) * 40.0f + 20.0f);
                block.transform.localScale = new Vector3(8.0f, 8.0f, 8.0f);
                block.GetComponent<Renderer>().material.color = new Color(0.62f, 0.6f, 0.56f);
            }
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(_scenery.transform);
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40, -30, 0);

            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0, 0.05f, -40.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            var chutePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Parachute.prefab");
            Assert.IsNotNull(chutePrefab, "no Parachute.prefab -- rebuild the scene");
            _chute = Object.Instantiate(chutePrefab, new Vector3(30, 1, 30), Quaternion.identity).GetComponent<Parachute>();
            _chute.OnEquip(_player, ItemInstanceData.None);

            var cameraObject = new GameObject("FollowCamera") { tag = "MainCamera" };
            _followCamera = cameraObject.AddComponent<Camera>();
            _followCamera.fieldOfView = 50.0f;
            _followCamera.nearClipPlane = 0.05f;
            _followCamera.farClipPlane = 1500.0f;
            _followCamera.clearFlags = CameraClearFlags.SolidColor;
            _followCamera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            _follow = cameraObject.AddComponent<ThirdPersonCamera>();
            _follow.Target = _player.transform;
            _chute.FeelCamera = _follow;
            _side = new GameObject("SideCamera").AddComponent<Camera>();
            _side.enabled = false;
            _side.fieldOfView = 40.0f;
            _side.nearClipPlane = 0.05f;
            _side.farClipPlane = 1500.0f;
            _side.clearFlags = CameraClearFlags.SolidColor;
            _side.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            yield return null;
            _follow.Yaw = 0.0f;
            _follow.Pitch = 8.0f;

            yield return Run(0.6f, Vector2.zero);
            Shoot("0-pack-back", null);
            Shoot("0-pack-side", Side(new Vector3(-2.6f, 0.2f, -1.2f), 1.6f));

            // Up high and let go.
            _locomotion.transform.position = new Vector3(0.0f, 140.0f, -40.0f);
            Physics.SyncTransforms();
            yield return Run(ParachuteFlight.ReadySeconds + 0.5f, Vector2.zero);
            Assert.AreEqual(ChuteState.Freefall, _chute.Flight.State);
            Shoot("1-freefall-pov", null);
            Shoot("1-freefall-side", Side(new Vector3(4.0f, 0.5f, 0.0f), 1.3f));
            Shoot("1-freefall-front", Side(new Vector3(0.6f, 0.8f, 4.0f), 1.3f));
            yield return Run(1.0f, new Vector2(0.0f, 1.0f));
            Shoot("1-dive-side", Side(new Vector3(4.0f, 0.5f, 0.0f), 1.3f));

            _chute.Tick(true, Vector2.zero, Dt);
            _locomotion.Tick(new LocomotionInput(), Dt);
            yield return null;
            yield return Run(0.18f, Vector2.zero);
            Shoot("2-pull-side", Side(new Vector3(4.0f, 0.5f, 0.0f), 1.3f));
            yield return Run(0.42f, Vector2.zero);
            Shoot("2-opening-pov", null);
            Shoot("2-opening-side", Side(new Vector3(9.0f, 1.0f, 0.0f), 3.0f));
            yield return Run(1.6f, Vector2.zero);
            Assert.AreEqual(ChuteState.Open, _chute.Flight.State);
            Shoot("3-open-pov", null);
            Shoot("3-open-side", Side(new Vector3(10.0f, 1.0f, 0.0f), 3.0f));
            Shoot("3-open-front", Side(new Vector3(2.0f, 0.5f, 11.0f), 3.0f));
            Shoot("3-open-below", Side(new Vector3(2.5f, -5.0f, 3.0f), 3.5f));
            _follow.Yaw = _player.transform.eulerAngles.y;
            yield return Run(1.0f, new Vector2(-1.0f, 0.0f));
            Shoot("4-left-pov", null);
            Shoot("4-left-back", Side(new Vector3(0.0f, 1.5f, -11.0f), 3.0f));
            yield return Run(1.0f, Vector2.zero);
            _follow.Yaw = _player.transform.eulerAngles.y;
            yield return Run(1.2f, new Vector2(0.0f, -1.0f));
            Shoot("5-brakes-side", Side(new Vector3(10.0f, 1.0f, 0.0f), 3.0f));

            // Down to the ground, flared at the last moment.
            for (float t = 0.0f; _locomotion.transform.position.y > 3.0f && t < 60.0f; t += Dt)
            {
                yield return Run(Dt, Vector2.zero);
            }
            yield return Run(0.35f, new Vector2(0.0f, -1.0f));
            Shoot("6-flare-side", Side(new Vector3(9.0f, 0.5f, 0.0f), 2.5f));
            Shoot("6-flare-pov", null);
            for (float t = 0.0f; _chute.DrivesBody && t < 5.0f; t += Dt)
            {
                yield return Run(Dt, new Vector2(0.0f, -1.0f));
            }
            Debug.Log($"CityX capture chute: landed {_chute.Flight.Landing} sinking {_chute.Flight.LandingSink:0.0} m/s");
            yield return Run(0.3f, Vector2.zero);
            Shoot("7-landed-side", Side(new Vector3(9.0f, 1.0f, 0.0f), 2.0f));
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private IEnumerator Run(float seconds, Vector2 keys)
        {
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                _chute.Tick(false, keys, Dt);
                _locomotion.Tick(new LocomotionInput { Move = _chute.DrivesBody ? Vector2.zero : keys, YawDegrees = _follow.Yaw }, Dt);
                yield return null;
            }
        }

        /// <summary>A camera at <paramref name="offset"/> from the chest, turned with the body, looking at <paramref name="up"/> metres over the feet.</summary>
        private Camera Side(Vector3 offset, float up)
        {
            Vector3 at = _player.transform.position + Vector3.up * up;
            _side.transform.position = at + Quaternion.Euler(0.0f, _player.transform.eulerAngles.y, 0.0f) * offset;
            _side.transform.LookAt(at);
            return _side;
        }

        private void Shoot(string name, Camera camera)
        {
            camera = camera != null ? camera : _followCamera;
            var target = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = null;
            target.Release();
            Object.Destroy(target);
            image.Apply();
            CapturePng.Write(image, Path.Combine(_directory, "chute-" + name + ".png"));
            Object.Destroy(image);
        }
#endif
    }
}
