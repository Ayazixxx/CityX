using System.Collections;
using System.IO;
using CityX.Character;
using CityX.UI;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Acid Rain (key 5): a storm 40 m across that burns everyone in the open under it at 6 health a second for 20 s after a 2 s warning; a roof is
    /// full shelter; never the caster; a cosmetic storm (another player's) burns nothing. Stepped by hand (Tick) so 20 s of storm takes no time.
    /// </summary>
    public sealed class AcidRainPlayTests
    {
        private const string PrefabPath = "Assets/CityX/Weapons/AcidRainStorm.prefab";
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float Step = 1.0f / 30.0f;
        private GameObject _prefab;
        private Scene _scene;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            Assert.IsNotNull(_prefab, "the acid rain prefab is missing -- run CityX/Rebuild Acid Rain");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = 0;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (AcidRainStorm s in Object.FindObjectsByType<AcidRainStorm>(FindObjectsSortMode.None)) Object.Destroy(s.gameObject);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private GameObject Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(300, 1, 300);
            Physics.SyncTransforms();
            _made.Add(ground);
            return ground;
        }

        private SparringDummy Dummy(Vector3 feet)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = feet + Vector3.up;
            _made.Add(go);
            Physics.SyncTransforms();
            var dummy = go.AddComponent<SparringDummy>();
            dummy.MaxHealth = 1000.0f;
            dummy.Reset();
            return dummy;
        }

        private static void Run(AcidRainStorm storm, float seconds)
        {
            for (float t = 0.0f; t < seconds && storm != null; t += Step)
            {
                storm.Tick(Step);
            }
        }

        [UnityTest]
        public IEnumerator SomeoneInTheOpenBurnsSixASecondAfterTheWarning()
        {
            Ground();
            SparringDummy dummy = Dummy(new Vector3(10, 0, 5));
            AcidRainStorm storm = AcidRainStorm.Spawn(_prefab, Vector3.zero, 0, null);
            yield return null;

            Run(storm, AcidRain.WarningSeconds - 0.1f);
            Assert.AreEqual(1000.0f, dummy.Health, 0.01f, "it burned before the warning was over");

            Run(storm, AcidRain.Seconds - AcidRain.WarningSeconds + 0.2f);
            float burned = 1000.0f - dummy.Health;
            float expected = AcidRain.DamagePerSecond * (AcidRain.Seconds - AcidRain.WarningSeconds);
            Assert.AreEqual(expected, burned, AcidRain.BurnPerCheck * 1.01f, $"burned {burned:0.0}, expected about {expected:0.0}");
        }

        [UnityTest]
        public IEnumerator ARoofIsFullShelter()
        {
            Ground();
            SparringDummy sheltered = Dummy(new Vector3(-12, 0, 0));
            SparringDummy open = Dummy(new Vector3(12, 0, 0));
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.position = new Vector3(-12, 4.5f, 0);
            roof.transform.localScale = new Vector3(8, 0.3f, 8);
            _made.Add(roof);
            Physics.SyncTransforms();

            AcidRainStorm storm = AcidRainStorm.Spawn(_prefab, Vector3.zero, 0, null);
            yield return null;
            Run(storm, 10.0f);

            Assert.AreEqual(1000.0f, sheltered.Health, 0.01f, "the acid got under the roof");
            Assert.Less(open.Health, 1000.0f - 30.0f, "the one in the open was not burned");
        }

        [UnityTest]
        public IEnumerator NotTheCasterAndNotOutsideTheStorm()
        {
            Ground();
            SparringDummy caster = Dummy(new Vector3(3, 0, 0));
            SparringDummy outside = Dummy(new Vector3(AcidRain.Radius + 6.0f, 0, 0));
            AcidRainStorm storm = AcidRainStorm.Spawn(_prefab, Vector3.zero, 0, caster.GetComponentsInChildren<Collider>());
            yield return null;
            Run(storm, 10.0f);

            Assert.AreEqual(1000.0f, caster.Health, 0.01f, "the caster was burned by their own storm");
            Assert.AreEqual(1000.0f, outside.Health, 0.01f, "someone outside the storm was burned");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersStormOnThisMachineBurnsNothingAndEveryStormEnds()
        {
            Ground();
            SparringDummy dummy = Dummy(new Vector3(5, 0, 5));
            AcidRainStorm storm = AcidRainStorm.Spawn(_prefab, Vector3.zero, 0, null, cosmetic: true);
            yield return null;
            Run(storm, 10.0f);
            Assert.AreEqual(1000.0f, dummy.Health, 0.01f, "a cosmetic storm burned someone");
            Assert.AreEqual(0, storm.Burns);

            Run(storm, AcidRain.Seconds + AcidRain.ClearSeconds);
            yield return null;
            Assert.IsTrue(storm == null, "the storm never ended");
        }

        [UnityTest]
        public IEnumerator TheCasterCallsItDownWhereTheyAimNoFurtherThanItsReachAndOtherPlayersSeeIt()
        {
            Ground();
            var body = new GameObject("Caster");
            _made.Add(body);
            var caster = body.AddComponent<AcidRainCaster>();
            caster.StormPrefab = _prefab;
            AbilityEvent? sent = null;
            caster.Fired += e => sent = e;
            yield return null;

            Vector3 eye = new Vector3(0, 2.4f, 0);
            Assert.IsTrue(caster.TryCast(eye, (new Vector3(0, 0, 20) - eye).normalized, null));
            Assert.IsTrue(sent.HasValue, "the cast told no one");
            Assert.AreEqual(20.0f, sent.Value.Origin.z, 0.5f, "the storm is not where the crosshair met the ground");
            Assert.IsFalse(caster.CanCast, "no cooldown");
            Assert.IsTrue(NetworkedAbilitySync.IsPlausible(sent.Value.Origin, sent.Value.Direction, sent.Value.A, sent.Value.B, sent.Value.N,
                body.transform.position), "the network would refuse this cast");

            // Aimed at the sky: the storm comes down at its reach, never further.
            Vector3 far = AcidRainCaster.Target(eye, new Vector3(0, 0.2f, 1).normalized, null, Vector3.zero);
            Assert.LessOrEqual(Vector3.Distance(far, Vector3.zero), AcidRainCaster.Reach + 0.01f);

            int before = Object.FindObjectsByType<AcidRainStorm>(FindObjectsSortMode.None).Length;
            caster.PlayRemote(sent.Value);
            AcidRainStorm[] storms = Object.FindObjectsByType<AcidRainStorm>(FindObjectsSortMode.None);
            Assert.AreEqual(before + 1, storms.Length, "another player's storm was not shown");
            Assert.IsTrue(System.Array.Exists(storms, s => s.Cosmetic), "the shown storm is not cosmetic");
        }

        /// <summary>Saves Logs/CityShots/acid-rain-*.bmp: the storm over a real street, from the street and from above. Run with -Graphics and look.</summary>
        [UnityTest, Explicit]
        public IEnumerator AcidRainCapture()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);
            Time.captureFramerate = 30;

            var player = Object.FindFirstObjectByType<PlayerVitals>();
            for (int i = 0; i < 60; i++) yield return null;

            Vector3 feet = player.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 centre = AcidRainCaster.Target(feet + Vector3.up * 2.4f, (forward * 25.0f + Vector3.down * 2.4f).normalized,
                player.GetComponentsInChildren<Collider>(), feet);
            AcidRainStorm.Spawn(_prefab, centre, 0, player.GetComponentsInChildren<Collider>());
            for (int i = 0; i < 120; i++) yield return null;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                yield break;
            }
            Camera camera = Camera.main;
            Photograph(camera, feet + Vector3.up * 2.2f - forward * 6.0f, Quaternion.LookRotation(forward + Vector3.up * 0.15f), "acid-rain-street");
            Vector3 above = centre - forward * 60.0f + Vector3.up * 110.0f;
            Photograph(camera, above, Quaternion.LookRotation((centre - above).normalized),
                "acid-rain-from-afar");
#else
            yield break;
#endif
        }

        private static void Photograph(Camera camera, Vector3 position, Quaternion rotation, string name)
        {
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            camera.transform.SetPositionAndRotation(position, rotation);
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
                Directory.CreateDirectory(directory);
                var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }
        }
    }
}
