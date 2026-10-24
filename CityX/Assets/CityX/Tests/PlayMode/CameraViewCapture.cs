using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: what the player sees through the follow camera, in the real city, squared up to a swordsman standing a few metres down the
    /// street (owner, 2026-10-17: "i feel like the camera pov needs to be adjusted so i can see more of whats going on in front of me";
    /// 2026-10-23: "research the saints row the third camera pov and zoom and just copy that"). Each shot is the game's own camera, rendered as
    /// the game renders it, once with the 2026-10-17 framing (raised over the head, 4.8 m back, 75 degrees) and once with the current, Saints
    /// Row one, at the starting pitch and looking level, in sword reach, and in fine aim. Writes Logs/CityShots/camera-*.png, and
    /// camera-compare-*.png with the old framing above the new.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CameraViewCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CameraViewCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Width = 1280;
        private const int Height = 720;
        private const float Dt = 1.0f / 60.0f;
        private Scene _scene;
        private GameObject _swordsman;
        private GameObject _sword;
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
            if (_swordsman != null) Object.Destroy(_swordsman);
            if (_sword != null) Object.Destroy(_sword);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheFollowCamerasView()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);
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
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            vitals.InfiniteHealth = true;
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Assert.IsNotNull(follow, "the scene has no follow camera");
            Camera camera = follow.GetComponent<Camera>();

            // Face the longest clear stretch of street, and stand a swordsman down it, facing back.
            Transform body = vitals.transform;
            float yaw = ClearestYaw(body);
            body.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
            Vector3 forward = body.forward;
            _sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"));
            _sword.GetComponent<SwordWeapon>().OnEquip(vitals.gameObject, CityX.Items.ItemInstanceData.None);
            _swordsman = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                body.position + forward * 6.5f + Vector3.up * 0.1f, Quaternion.LookRotation(-forward, Vector3.up));
            _swordsman.GetComponent<SwordsmanBrain>().enabled = false;   // stands his ground for the pictures
            _swordsman.GetComponent<PlayerVitals>().InfiniteHealth = true;
            var foeLocomotion = _swordsman.GetComponent<PlayerLocomotion>();
            float foeYaw = yaw + 180.0f;
            for (int i = 0; i < 40; i++)
            {
                yield return Stand(locomotion, yaw, foeLocomotion, foeYaw);
            }

            // The 2026-10-17 framing (raised over the head, 75 degrees) above Saints Row: The Third's (2026-10-23).
            const float actionHeight = 1.0f, actionDistance = 4.8f, actionFov = 75.0f;
            var shots = new (string name, float height, float distance, float fov, float pitch)[]
            {
                ("action-start", actionHeight, actionDistance, actionFov, ThirdPersonCamera.DefaultPitch),
                ("sr3-start", ThirdPersonCamera.DefaultFramingHeight, ThirdPersonCamera.DefaultDistance, ThirdPersonCamera.DefaultFieldOfView, ThirdPersonCamera.DefaultPitch),
                ("action-level", actionHeight, actionDistance, actionFov, 0.0f),
                ("sr3-level", ThirdPersonCamera.DefaultFramingHeight, ThirdPersonCamera.DefaultDistance, ThirdPersonCamera.DefaultFieldOfView, 0.0f),
            };
            var images = new Texture2D[shots.Length];
            for (int s = 0; s < shots.Length; s++)
            {
                follow.FramingHeight = shots[s].height;
                follow.Distance = shots[s].distance;
                follow.RestFieldOfView = shots[s].fov;
                camera.fieldOfView = shots[s].fov;
                follow.Yaw = yaw;
                follow.Pitch = shots[s].pitch;
                for (int i = 0; i < 3; i++)
                {
                    yield return Stand(locomotion, yaw, foeLocomotion, foeYaw);
                }
                images[s] = Shoot(camera);
                Save(images[s], "camera-" + shots[s].name);
            }
            SaveStacked(images[0], images[1], "camera-compare-start");
            SaveStacked(images[2], images[3], "camera-compare-level");

            // In sword reach: the distance the swordsman circles at (DuelistMind.EngageMetres).
            _swordsman.transform.position = body.position + forward * DuelistMind.EngageMetres + Vector3.up * 0.1f;
            Physics.SyncTransforms();
            var close = new Texture2D[2];
            for (int s = 0; s < 2; s++)
            {
                follow.FramingHeight = s == 0 ? actionHeight : ThirdPersonCamera.DefaultFramingHeight;
                follow.Distance = s == 0 ? actionDistance : ThirdPersonCamera.DefaultDistance;
                follow.RestFieldOfView = s == 0 ? actionFov : ThirdPersonCamera.DefaultFieldOfView;
                camera.fieldOfView = follow.RestFieldOfView;
                follow.Yaw = yaw;
                follow.Pitch = ThirdPersonCamera.DefaultPitch;
                for (int i = 0; i < 3; i++)
                {
                    yield return Stand(locomotion, yaw, foeLocomotion, foeYaw);
                }
                close[s] = Shoot(camera);
                Save(close[s], s == 0 ? "camera-action-close" : "camera-sr3-close");
            }
            SaveStacked(close[0], close[1], "camera-compare-close");

            // Fine aim: in close over the right shoulder, zoomed, the swordsman back down the street.
            _swordsman.transform.position = body.position + forward * 6.5f + Vector3.up * 0.1f;
            Physics.SyncTransforms();
            follow.IsAiming = true;
            for (int i = 0; i < 40; i++)
            {
                yield return Stand(locomotion, yaw, foeLocomotion, foeYaw);
            }
            Texture2D aim = Shoot(camera);
            Save(aim, "camera-sr3-aim");
            follow.IsAiming = false;
            Object.Destroy(aim);
            foreach (Texture2D image in images)
            {
                Object.Destroy(image);
            }
            Object.Destroy(close[0]);
            Object.Destroy(close[1]);
            Debug.Log($"CityX camera capture: facing {yaw:0} degrees, the swordsman {Vector3.Distance(body.position, _swordsman.transform.position):0.0} m ahead");
#else
            yield break;
#endif
        }

        /// <summary>Both bodies standing in their sword stance for a frame.</summary>
        private static IEnumerator Stand(PlayerLocomotion player, float yaw, PlayerLocomotion foe, float foeYaw)
        {
            player.Tick(new LocomotionInput { YawDegrees = yaw, Sword1HEquipped = true }, Dt);
            foe.Tick(new LocomotionInput { YawDegrees = foeYaw, Sword1HEquipped = true }, Dt);
            yield return null;
        }

        /// <summary>The direction, of sixteen, with the most open street ahead of the body at chest height.</summary>
        private static float ClearestYaw(Transform body)
        {
            float best = 0.0f;
            float bestFree = -1.0f;
            Vector3 chest = body.position + Vector3.up * 1.5f;
            for (int i = 0; i < 16; i++)
            {
                float yaw = i * 22.5f;
                Vector3 direction = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward;
                float free = 40.0f;
                foreach (RaycastHit hit in Physics.RaycastAll(chest, direction, free, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(body) && hit.distance < free)
                    {
                        free = hit.distance;
                    }
                }
                if (free > bestFree)
                {
                    bestFree = free;
                    best = yaw;
                }
            }
            return best;
        }

        /// <summary>The game camera's own picture, post-processing and all.</summary>
        private static Texture2D Shoot(Camera camera)
        {
            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private void SaveStacked(Texture2D top, Texture2D bottom, string name)
        {
            var sheet = new Texture2D(Width, Height * 2 + 8, TextureFormat.RGB24, false);
            var gap = new Color[Width * 8];
            for (int i = 0; i < gap.Length; i++) gap[i] = Color.white;
            sheet.SetPixels(0, 0, Width, Height, bottom.GetPixels());
            sheet.SetPixels(0, Height, Width, 8, gap);
            sheet.SetPixels(0, Height + 8, Width, Height, top.GetPixels());
            sheet.Apply();
            Save(sheet, name);
            Object.Destroy(sheet);
        }

        private void Save(Texture2D image, string name)
        {
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(_directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
        }
    }
}
