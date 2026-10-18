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
    /// Not a test: a sword cut landing on the swordsman in the real city, filmed (owner, 2026-10-17: "the hits dont feel impactful at all ...
    /// make the blood effects better too"). From the side, twelve frames a thirtieth of a second apart from the moment the blade connects (the
    /// freeze, the blood, the shove, the recoil); the player's own view through the follow camera at that moment (the jolt); and, after a few
    /// cuts, the floor. Writes Logs/CityShots/hit-impact-strip.png, hit-impact-heavy-strip.png, hit-impact-player-view.png, hit-impact-floor.png.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.HitImpactCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class HitImpactCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Cell = 360;
        private const int Columns = 12;
        private const float Dt = 1.0f / 60.0f;
        private Scene _scene;
        private GameObject _swordsman;
        private GameObject _sword;
        private Camera _side;
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
            if (_side != null) Object.Destroy(_side.gameObject);
            BloodSplats.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderACutLanding()
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
            vitals.InfiniteStamina = true;
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Camera gameCamera = follow.GetComponent<Camera>();

            Transform body = vitals.transform;
            float yaw = body.eulerAngles.y;
            Vector3 forward = body.forward;
            Vector3 side = body.right;
            _sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"));
            _sword.GetComponent<SwordWeapon>().OnEquip(vitals.gameObject, CityX.Items.ItemInstanceData.None);
            var combat = vitals.GetComponent<SoulsSwordCombat>();
            _swordsman = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                body.position + forward * 2.1f + Vector3.up * 0.1f, Quaternion.LookRotation(-forward, Vector3.up));
            _swordsman.GetComponent<SwordsmanBrain>().enabled = false;   // he takes the cuts
            var foeVitals = _swordsman.GetComponent<PlayerVitals>();
            foeVitals.InfiniteHealth = true;
            var foeLocomotion = _swordsman.GetComponent<PlayerLocomotion>();
            float foeYaw = yaw + 180.0f;

            _side = new GameObject("SideCam").AddComponent<Camera>();
            _side.fieldOfView = 38.0f;
            _side.nearClipPlane = 0.1f;
            _side.farClipPlane = 1500.0f;
            _side.allowHDR = true;
            _side.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            for (int i = 0; i < 40; i++)
            {
                yield return Stand(locomotion, yaw, foeLocomotion, foeYaw, combat, foeVitals);
            }

            // The light cut, filmed from the side from the frame the blade connects.
            yield return FilmACut(combat, locomotion, yaw, foeLocomotion, foeYaw, foeVitals, body, heavy: false, "hit-impact-strip", gameCamera);
            for (int i = 0; i < 90; i++)
            {
                yield return Stand(locomotion, yaw, foeLocomotion, foeYaw, combat, foeVitals);
            }
            _swordsman.transform.position = body.position + forward * 2.1f + Vector3.up * 0.1f;
            Physics.SyncTransforms();
            yield return FilmACut(combat, locomotion, yaw, foeLocomotion, foeYaw, foeVitals, body, heavy: true, "hit-impact-heavy-strip", null);

            // A few more cuts, then the floor.
            for (int cut = 0; cut < 4; cut++)
            {
                for (int i = 0; i < 70; i++)
                {
                    yield return Stand(locomotion, yaw, foeLocomotion, foeYaw, combat, foeVitals);
                }
                _swordsman.transform.position = body.position + forward * 2.0f + Vector3.up * 0.1f;
                Physics.SyncTransforms();
                combat.RequestLight(yaw);
                for (int i = 0; i < 60; i++)
                {
                    yield return Stand(locomotion, yaw, foeLocomotion, foeYaw, combat, foeVitals);
                }
            }
            Vector3 middle = (body.position + _swordsman.transform.position) * 0.5f;
            Save(Shoot(_side, middle - forward * 1.5f + side * 3.5f + Vector3.up * 4.0f, middle + Vector3.up * 0.2f, 1280, 720), "hit-impact-floor");
            Debug.Log($"CityX hit impact capture: {BloodSplats.Count} splats on the ground and walls, {BloodBurst.Spawned} bursts");
#else
            yield break;
#endif
        }

        /// <summary>One cut, the strip of frames from its contact, and (for the first) the player's own view at the contact.</summary>
        private IEnumerator FilmACut(SoulsSwordCombat combat, PlayerLocomotion locomotion, float yaw, PlayerLocomotion foeLocomotion, float foeYaw,
            PlayerVitals foeVitals, Transform body, bool heavy, string name, Camera playerView)
        {
            var sheet = new Texture2D(Cell * Columns, Cell, TextureFormat.RGB24, false);
            bool landed = false;
            void OnHit(IDamageable target) => landed = true;
            combat.AttackHit += OnHit;
            if (heavy) combat.RequestHeavy(yaw); else combat.RequestLight(yaw);
            float waited = 0.0f;
            while (!landed && waited < 2.0f)
            {
                yield return Stand(locomotion, yaw, foeLocomotion, foeYaw, combat, foeVitals);
                waited += Dt;
            }
            combat.AttackHit -= OnHit;
            Assert.IsTrue(landed, "the cut never landed on the swordsman");
            if (playerView != null)
            {
                Save(Shoot(playerView, playerView.transform.position, playerView.transform.position + playerView.transform.forward, 1280, 720, keepPlace: true),
                    "hit-impact-player-view");
            }
            int column = 0;
            int frame = 0;
            while (column < Columns)
            {
                if (frame % 2 == 0)
                {
                    Vector3 mid = (body.position + _swordsman.transform.position) * 0.5f + Vector3.up * 1.3f;
                    Vector3 across = Vector3.Cross(Vector3.up, _swordsman.transform.position - body.position).normalized;
                    Texture2D shot = Shoot(_side, mid + across * 4.2f + Vector3.up * 0.3f, mid, Cell, Cell);
                    sheet.SetPixels(column * Cell, 0, Cell, Cell, shot.GetPixels());
                    Object.Destroy(shot);
                    column++;
                }
                frame++;
                yield return Stand(locomotion, yaw, foeLocomotion, foeYaw, combat, foeVitals);
            }
            Save(sheet, name);
            Object.Destroy(sheet);
        }

        private static IEnumerator Stand(PlayerLocomotion player, float yaw, PlayerLocomotion foe, float foeYaw, SoulsSwordCombat combat, PlayerVitals foeVitals)
        {
            combat.Tick(Dt);
            foeVitals.TickHitstun(Dt);
            player.Tick(new LocomotionInput { YawDegrees = yaw, Sword1HEquipped = true, LockedOn = true }, Dt);
            foe.Tick(new LocomotionInput { YawDegrees = foeYaw, Sword1HEquipped = true }, Dt);
            yield return null;
        }

        private static Texture2D Shoot(Camera camera, Vector3 eye, Vector3 look, int width, int height, bool keepPlace = false)
        {
            if (!keepPlace)
            {
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            }
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
