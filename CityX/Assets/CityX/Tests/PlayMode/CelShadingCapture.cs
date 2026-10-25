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
    /// Not a test: the real city through the game's own camera, by day, at sunset and at midnight: a street with a swordsman down it, the player close, and
    /// the street from above. Each view is saved under a label so two runs (or two looks) can be laid side by side. Writes
    /// Logs/CityShots/cel-&lt;label&gt;-*.png. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CelShadingCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Width = 1280;
        private const int Height = 720;
        private const float Dt = 1.0f / 60.0f;
        private static readonly float Day = CityX.View.SkyCycle.DaySeconds * 0.42f;
        private static readonly float Evening = CityX.View.SkyCycle.DaySeconds * 0.93f;
        private static readonly float Midnight = CityX.View.SkyCycle.DaySeconds + CityX.View.SkyCycle.NightSeconds * 0.5f;
        private Scene _scene;
        private GameObject _swordsman;
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
            CityX.View.CelShading.Set(true);
            if (_swordsman != null) Object.Destroy(_swordsman);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheCity()
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
            Assert.IsNotNull(cycle, "the city has no day and night");
            cycle.Running = false;

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            vitals.InfiniteHealth = true;
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Camera camera = follow.GetComponent<Camera>();

            Transform body = vitals.transform;
            float yaw = ClearestYaw(body);
            body.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
            Vector3 forward = body.forward;
            _swordsman = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                body.position + forward * 5.5f + Vector3.up * 0.1f, Quaternion.LookRotation(-forward, Vector3.up));
            _swordsman.GetComponent<SwordsmanBrain>().enabled = false;
            _swordsman.GetComponent<PlayerVitals>().InfiniteHealth = true;
            var foe = _swordsman.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 40; i++)
            {
                yield return Stand(locomotion, yaw, foe, yaw + 180.0f);
            }

            foreach ((string time, float seconds) in new[] { ("day", Day), ("evening", Evening), ("night", Midnight) })
            {
                cycle.TimeOfDay = seconds;
                cycle.Apply(CityX.View.SkyCycle.At(cycle.TimeOfDay));
                for (int i = 0; i < 10; i++)
                {
                    yield return Stand(locomotion, yaw, foe, yaw + 180.0f);
                }
                yield return ShootAll(camera, follow, body, yaw, locomotion, foe, time);
            }
            Debug.Log($"CityX cel capture: facing {yaw:0} degrees");
#else
            yield break;
#endif
        }

        /// <summary>The three views, saved as cel-&lt;look&gt;-&lt;time&gt;-&lt;view&gt;.png for each look the game can show.</summary>
        private IEnumerator ShootAll(Camera camera, ThirdPersonCamera follow, Transform body, float yaw, PlayerLocomotion player,
            PlayerLocomotion foe, string time)
        {
            foreach (string look in Looks())
            {
                SetLook(look);
                follow.enabled = true;
                follow.Yaw = yaw;
                follow.Pitch = ThirdPersonCamera.DefaultPitch;
                for (int i = 0; i < 3; i++)
                {
                    yield return Stand(player, yaw, foe, yaw + 180.0f);
                }
                Save(Shoot(camera), $"cel-{look}-{time}-street");

                // Close on the player, from in front and a little to the side.
                follow.enabled = false;
                Vector3 chest = body.position + Vector3.up * 1.6f;
                camera.transform.SetPositionAndRotation(chest + body.forward * 3.2f + body.right * 1.2f + Vector3.up * 0.3f,
                    Quaternion.LookRotation(chest - (chest + body.forward * 3.2f + body.right * 1.2f + Vector3.up * 0.3f)));
                yield return Stand(player, yaw, foe, yaw + 180.0f);
                Save(Shoot(camera), $"cel-{look}-{time}-close");

                // From above and behind, down the street.
                Vector3 high = body.position - body.forward * 14.0f + Vector3.up * 26.0f;
                camera.transform.SetPositionAndRotation(high, Quaternion.LookRotation(body.position + body.forward * 30.0f - high));
                yield return Stand(player, yaw, foe, yaw + 180.0f);
                Save(Shoot(camera), $"cel-{look}-{time}-high");
                follow.enabled = true;
            }
        }

        /// <summary>The looks shot: the cel look off (the game as it was) and on.</summary>
        private static string[] Looks() => new[] { "off", "on" };

        private static void SetLook(string look)
        {
            CityX.View.CelShading.Set(look == "on");
        }

        private static IEnumerator Stand(PlayerLocomotion player, float yaw, PlayerLocomotion foe, float foeYaw)
        {
            player.Tick(new LocomotionInput { YawDegrees = yaw }, Dt);
            foe.Tick(new LocomotionInput { YawDegrees = foeYaw }, Dt);
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

        private void Save(Texture2D image, string name)
        {
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(_directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
        }
    }
}
