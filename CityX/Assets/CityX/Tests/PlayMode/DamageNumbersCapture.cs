using System.Collections;
using System.IO;
using CityX.Character;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the damage numbers as the player sees them, in the real city through the game's own camera. Three swordsmen in front of
    /// the player -- a weak one (40 health), an even one (100) and a strong one (1000) -- each take the same cut of 22, and a fourth blow, a
    /// heavy of 50, lands on the even one a moment later. Shot as the first numbers pop, a quarter of a second on and half a second on:
    /// Logs/CityShots/damage-numbers-1..3.png. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class DamageNumbersCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Width = 1280;
        private const int Height = 720;
        private Scene _scene;
        private readonly GameObject[] _foes = new GameObject[3];
        private GameObject _numbersObject;
        private RenderTexture _target;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (GameObject foe in _foes)
            {
                if (foe != null) Object.Destroy(foe);
            }
            if (_numbersObject != null) Object.Destroy(_numbersObject);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheDamageNumbers()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
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
            Camera camera = follow.GetComponent<Camera>();
            _target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = _target;

            Transform body = vitals.transform;
            float yaw = body.eulerAngles.y;
            Vector3 forward = body.forward;
            Vector3 right = body.right;
            float[] bars = { 40.0f, 100.0f, 1000.0f };
            var foeVitals = new PlayerVitals[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = body.position + forward * 5.0f + right * ((i - 1) * 2.4f) + Vector3.up * 0.1f;
                _foes[i] = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                    at, Quaternion.LookRotation(-forward, Vector3.up));
                _foes[i].GetComponent<SwordsmanBrain>().enabled = false;
                foeVitals[i] = _foes[i].GetComponent<PlayerVitals>();
                foeVitals[i].InfiniteHealth = true;
                foeVitals[i].SetHealthFromNetwork(bars[i], bars[i]);
            }

            // The scene's own numbers watch the player the input drives (switched off here); these watch the body standing in for it, drawn
            // through the game camera so the render has them.
            _numbersObject = new GameObject("CaptureDamageNumbers", typeof(RectTransform));
            var numbers = _numbersObject.AddComponent<DamageNumbers>();
            numbers.Player = body;
            numbers.View = camera;
            Canvas canvas = _numbersObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1.0f;

            follow.Yaw = yaw;
            follow.Pitch = ThirdPersonCamera.DefaultPitch;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            for (int i = 0; i < 3; i++)
            {
                DamageContext.Strike(foeVitals[i], 22.0f, body.position, 0.4f, DamageKind.Physical, 0.0f, null, body.gameObject);
            }
            // The heavy a few frames later, so it is a number of its own (blows landing together add into one).
            const int heavyAt = 8;
            int[] shootAt = { 3, 15, 30 };
            int frame = 0;
            for (int s = 0; s < shootAt.Length; s++)
            {
                for (; frame < shootAt[s]; frame++)
                {
                    if (frame == heavyAt)
                    {
                        DamageContext.Strike(foeVitals[1], 50.0f, body.position, HitImpactEffects.HeavyHitstunSeconds, DamageKind.Physical, 0.0f,
                            null, body.gameObject);
                    }
                    yield return null;
                }
                Save(camera, $"damage-numbers-{s + 1}");
            }
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(_target);
#else
            yield break;
#endif
        }

        private void Save(Camera camera, string name)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            var conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
        }
    }
}
