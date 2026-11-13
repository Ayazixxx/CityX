using System.Collections;
using CityX.Character;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders Glacier Spikes (docs/GLACIER_SPIKES.md) in the real city to Logs/CityShots/glacier-*.png, from the game's own
    /// camera behind the player and from the side: armed with the arrow, the front halfway, the impact, the whole field standing, close on
    /// the crystals, sinking; then the same at midnight. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.GlacierSpikesCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GlacierSpikesCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private static readonly float Day = SkyCycle.DaySeconds * 0.42f;
        private static readonly float Midnight = SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f;

        private Scene _scene;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();
        private Camera _game;
        private Camera _side;
        private GlacierSpikesAbility _spikes;
        private Transform _body;
        private Vector3 _heading;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderGlacierSpikes()
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
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            SetTime(cycle, Day);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            vitals.InfiniteHealth = true;
            _body = vitals.transform;
            _spikes = vitals.GetComponent<GlacierSpikesAbility>();
            Assert.IsNotNull(_spikes, "no Glacier Spikes on the player -- rebuild the scene");
            _heading = OpenRun.Face(vitals, _made);
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            _body.rotation = Quaternion.LookRotation(_heading);
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Assert.IsNotNull(follow, "the scene has no follow camera");
            follow.Yaw = Quaternion.LookRotation(_heading).eulerAngles.y;
            follow.Pitch = ThirdPersonCamera.DefaultPitch;
            _game = follow.GetComponent<Camera>();
            _side = new GameObject("SideCam").AddComponent<Camera>();
            _made.Add(_side.gameObject);
            _side.fieldOfView = 50.0f;
            _side.farClipPlane = 2000.0f;
            _side.allowHDR = true;
            _side.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            _side.enabled = false;

            // Someone standing on the line, for scale and to be struck.
            GameObject target = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Ground(_body.position + _heading * 12.0f) + Vector3.up * 0.1f, Quaternion.LookRotation(-_heading));
            target.GetComponent<PlayerInputReader>().enabled = false;
            target.GetComponent<PlayerVitals>().InfiniteHealth = true;
            _made.Add(target);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            yield return Cast("glacier");

            yield return Seconds(GlacierSpikesRules.CooldownSeconds);
            SetTime(cycle, Midnight);
            yield return Seconds(0.2f);
            yield return Cast("glacier-night");
#else
            yield break;
#endif
        }

        /// <summary>Armed, then cast, with a picture at each moment worth seeing, every one named after <paramref name="prefix"/>.</summary>
        private IEnumerator Cast(string prefix)
        {
            Assert.IsTrue(_spikes.TryArm(), "it would not arm");
            for (int i = 0; i < 20; i++)
            {
                _spikes.Aim(_heading);
                yield return null;
            }
            _spikes.Aim(_heading);
            Both(prefix + "-armed");
            Assert.IsTrue(_spikes.TryCast(), "it would not cast");
            GlacierSpikesField field = _spikes.LastField;
            float length = field.Cast.Length;
            yield return Until(field, GlacierSpikesRules.ArrivalTime(length * 0.5f));
            Both(prefix + "-front-half");
            yield return Until(field, GlacierSpikesRules.ArrivalTime(length) + 0.06f);
            Both(prefix + "-impact");
            yield return Until(field, 1.3f);
            Both(prefix + "-field");
            Vector3 end = field.Cast.End;
            Vector3 side = Vector3.Cross(Vector3.up, _heading);
            Shoot(_side, prefix + "-close", end - _heading * 9.0f + side * 6.5f + Vector3.up * 2.2f, end - _heading * 3.0f + Vector3.up * 2.0f);
            Shoot(_side, prefix + "-high", _body.position - _heading * 6.0f + side * 9.0f + Vector3.up * 14.0f, _body.position + _heading * 12.0f);
            yield return Until(field, GlacierSpikesRules.SinkStartSeconds + GlacierSpikesRules.SinkSeconds * 0.5f);
            Both(prefix + "-sinking");
        }

        private static IEnumerator Until(GlacierSpikesField field, float age)
        {
            for (int i = 0; i < 1000 && field != null && field.Age < age; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        /// <summary>From where the game's camera is (behind the right shoulder), and from the side of the line.</summary>
        private void Both(string name)
        {
            Texture2D image = BuildingShapeCapture.Render(_game);
            BuildingShapeCapture.Save(image, BuildingShapeCapture.ShotsFolder, name + "-game");
            Object.Destroy(image);
            Vector3 side = Vector3.Cross(Vector3.up, _heading);
            Vector3 middle = _body.position + _heading * 11.0f;
            Shoot(_side, name + "-side", middle + side * 16.0f + Vector3.up * 3.5f, middle + Vector3.up * 1.5f);
        }

        private static void Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }

        private static Vector3 Ground(Vector3 at) =>
            Physics.Raycast(at + Vector3.up * 15.0f, Vector3.down, out RaycastHit hit, 40.0f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : at;

        private static void SetTime(DayNightCycle cycle, float seconds)
        {
            if (cycle == null)
            {
                return;
            }
            cycle.Running = false;
            cycle.TimeOfDay = seconds;
            cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
        }
    }
}
