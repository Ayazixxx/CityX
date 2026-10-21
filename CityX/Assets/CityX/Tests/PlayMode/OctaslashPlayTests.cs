using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Octaslash (key 6, sword in hand) through the real keys in the real city: the wind-up holds the body still for 2.5 s and then dashes about
    /// 14 m where the camera looks, cutting eight times; a click goes early; without a sword nothing happens; another player's copy shows it
    /// and hurts nothing.
    /// </summary>
    public sealed class OctaslashPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private OctaslashAbility _octaslash;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        private sealed class Target : MonoBehaviour, IDamageable, ILaunchable
        {
            public bool IsAlive => true;
            public int Hits;
            public int Launches;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                return HitResponse.Landed;
            }

            public void Launch(Vector3 velocity)
            {
                Launches++;
            }
        }

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

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

        private IEnumerator LoadScene(bool withSword)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _vitals = Object.FindFirstObjectByType<PlayerVitals>();
            _locomotion = _vitals.GetComponent<PlayerLocomotion>();
            _octaslash = _vitals.GetComponent<OctaslashAbility>();
            Assert.IsNotNull(_octaslash, "the player has no Octaslash -- PlayerAbilityWiring");
            if (withSword)
            {
                Inventory inventory = _vitals.GetComponent<Inventory>();
                inventory.AddItem(ItemRegistry.SpikeSword, 1);
                Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.SpikeSword, 1, out ItemInstanceData data));
                Assert.IsTrue(inventory.EquipItemId(ItemRegistry.SpikeSword, _vitals.gameObject, data), "could not equip the sword");
            }
            _vitals.InfiniteHealth = true;
            Time.captureFramerate = 60;   // batch frames are a fraction of a millisecond: one frame = 1/60 s, so frame counts are seconds
            for (int i = 0; i < 400 && !_locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Assert.IsTrue(_locomotion.IsGrounded, "setup failed -- the player never settled on the ground");
#else
            yield break;
#endif
        }

        private Target Dummy(Vector3 feet)
        {
            var go = new GameObject("Target");
            go.transform.position = feet + Vector3.up * 1.0f;
            var body = go.AddComponent<CapsuleCollider>();
            body.height = 2.0f;
            body.radius = 0.4f;
            go.AddComponent<Target>();
            _made.Add(go);
            Physics.SyncTransforms();
            return go.GetComponent<Target>();
        }

        private IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SixWindsUpForTwoAndAHalfSecondsThenDashesWhereTheCameraLooksCuttingEightTimes()
        {
            yield return LoadScene(withSword: true);
            Vector3 heading = OpenRun.Face(_vitals, _made);
            yield return null;
            Vector3 start = _vitals.transform.position;
            Target target = Dummy(start + heading * 7.0f);

            Press(_keyboard.digit6Key);
            yield return null;
            Release(_keyboard.digit6Key);
            Assert.IsTrue(_octaslash.IsCharging, "6 with the sword in hand did not start the wind-up");

            Transform hips = _vitals.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            float standingHips = hips.localPosition.y;
            Press(_keyboard.wKey);   // the wind-up roots the body: walking does nothing
            yield return Seconds(1.5f);
            Release(_keyboard.wKey);
            Assert.IsTrue(_octaslash.IsCharging, "the wind-up ended early by itself");
            Vector3 flat = _vitals.transform.position - start;
            flat.y = 0.0f;
            Assert.Less(flat.magnitude, 0.3f, "the body walked during the wind-up");
            Assert.Greater(_locomotion.BakedPoseWeight, 0.99f, "the wind-up's animation is not on the body");
            Assert.Less(hips.localPosition.y, standingHips - 0.15f, "the body is not crouched in the coil");
            Assert.Greater(Vector3.Dot(_vitals.transform.forward, heading), 0.95f, "the body did not turn to face where the dash will go");

            for (int i = 0; i < 400 && !_octaslash.IsDashing && _octaslash.IsActive; i++)
            {
                yield return null;
            }
            Assert.IsTrue(_octaslash.IsDashing || _octaslash.CutsDone > 0, "the dash never left by itself");
            float leanIntoTheDash = -1.0f;
            float facingAlongIt = -1.0f;
            bool trailed = false;
            for (int i = 0; i < 400 && _octaslash.IsActive; i++)
            {
                if (_octaslash.IsDashing && _octaslash.CutsDone == 3)
                {
                    leanIntoTheDash = Vector3.Dot(hips.up, heading);
                    facingAlongIt = Vector3.Dot(Vector3.ProjectOnPlane(hips.forward, Vector3.up).normalized, heading);
                }
                trailed |= _octaslash.Trail != null && _octaslash.Trail.IsShowing && _octaslash.Trail.SampleCount > 8;
                yield return null;
            }
            Assert.That(leanIntoTheDash, Is.InRange(0.4f, 0.95f), "the body did not lunge into the dash (it stayed upright, or lay flat)");
            Assert.Greater(facingAlongIt, 0.8f, "the body did not face along the dash");
            Assert.IsTrue(trailed, "the cuts left no light trailing from the blade");
            Assert.AreEqual(0.0f, _locomotion.BakedPoseWeight, "the body was left holding the animation");
            yield return Seconds(BladeTrail.LifeSeconds + 0.1f);
            Assert.IsFalse(_octaslash.Trail.IsShowing, "the slash light never faded");

            Vector3 travelled = _vitals.transform.position - start;
            travelled.y = 0.0f;
            TestContext.WriteLine($"dashed {travelled.magnitude:0.0} m; cuts {_octaslash.CutsDone}; target hit {target.Hits}, launched {target.Launches}");
            Assert.AreEqual(OctaslashMove.CutCount, _octaslash.CutsDone, "not all eight cuts");
            Assert.Greater(travelled.magnitude, OctaslashMove.DashMetres * 0.7f, "the dash did not carry the body");
            Assert.Greater(Vector3.Dot(travelled.normalized, heading), 0.9f, "the dash did not go where the camera looks");
            Assert.GreaterOrEqual(target.Hits, 2, "the cuts did not hit what the dash passed through");
            Assert.IsFalse(_locomotion.HasForcedVelocity, "the dash left the body's movement taken over");
            Assert.IsFalse(_locomotion.MovementRooted, "the body was left rooted after it");
        }

        [UnityTest]
        public IEnumerator AClickDuringTheWindUpSendsItAtOnce()
        {
            yield return LoadScene(withSword: true);
            OpenRun.Face(_vitals, _made);
            yield return null;

            Press(_keyboard.digit6Key);
            yield return null;
            Release(_keyboard.digit6Key);
            yield return Seconds(0.4f);
            Assert.IsTrue(_octaslash.IsCharging);

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
            Assert.IsTrue(_octaslash.IsDashing || _octaslash.CutsDone > 0, "the click did not send the dash");
        }

        [UnityTest]
        public IEnumerator WithoutASwordSixDoesNothing()
        {
            yield return LoadScene(withSword: false);
            Press(_keyboard.digit6Key);
            yield return null;
            Release(_keyboard.digit6Key);
            yield return null;
            Assert.IsFalse(_octaslash.IsActive, "Octaslash started with no sword in hand");
            Assert.IsFalse(_octaslash.TryStart(Vector3.forward), "Octaslash started with no sword in hand");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersOctaslashIsShownAndHurtsNothing()
        {
            var copy = new GameObject("RemoteCopy");
            _made.Add(copy);
            var ability = copy.AddComponent<OctaslashAbility>();
            Target target = Dummy(copy.transform.position + Vector3.forward * 1.5f);
            yield return null;

            ability.PlayRemote(new AbilityEvent(OctaslashAbility.EventCharging, copy.transform.position, Vector3.forward));
            Assert.IsTrue(ability.IsCharging);
            ability.Tick(1.0f);
            Assert.IsTrue(ability.IsCharging, "a copy dashed by itself; the owner decides when");

            ability.PlayRemote(new AbilityEvent(OctaslashAbility.EventDashing, copy.transform.position, Vector3.forward));
            for (int i = 0; i < 60; i++)
            {
                ability.Tick(1.0f / 60.0f);
            }
            Assert.AreEqual(OctaslashMove.CutCount, ability.CutsDone, "the copy did not show all eight slashes");
            Assert.AreEqual(0, target.Hits, "a copy's slashes hurt someone");
            Assert.IsFalse(ability.IsActive, "the copy's move never ended");
        }

        /// <summary>
        /// Saves Logs/CityShots/octaslash-*.png: the coil from the side and from behind (where the camera is), then dash frames 1, 2, 5, 9 and 15
        /// and the follow-through, each from the side and from behind. Run with -Graphics and look.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator OctaslashCapture()
        {
            yield return LoadScene(withSword: true);
            Vector3 heading = OpenRun.Face(_vitals, _made);
            Time.captureFramerate = 60;
            yield return null;
            Vector3 side = Vector3.Cross(Vector3.up, heading);

            Press(_keyboard.digit6Key);
            yield return null;
            Release(_keyboard.digit6Key);
            for (int i = 0; i < 50; i++) yield return null;
            Both("octaslash-windup", side, heading);

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            int frame = 0;
            var wanted = new System.Collections.Generic.HashSet<int> { 1, 2, 5, 9, 15 };
            while (_octaslash.IsDashing && frame < 30)
            {
                frame = Mathf.RoundToInt(_octaslash.PhaseTime * 60.0f);
                if (wanted.Remove(frame))
                {
                    Both("octaslash-dash-" + frame, side, heading);
                }
                yield return null;
            }
            for (int i = 0; i < 6 && _octaslash.IsActive; i++) yield return null;
            Both("octaslash-follow-through", side, heading);
        }

        /// <summary>
        /// Saves Logs/CityShots/octaslash-film/side-NNN.png and behind-NNN.png: every frame from the held wind-up to the end of the follow-through,
        /// from the side and from behind, for a video of the move as the game draws it. Run with -Graphics.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator OctaslashFilm()
        {
            yield return LoadScene(withSword: true);
            Vector3 heading = OpenRun.Face(_vitals, _made);
            Time.captureFramerate = 60;
            yield return null;
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots", "octaslash-film");
            if (System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.Delete(directory, true);
            }
            System.IO.Directory.CreateDirectory(directory);

            Press(_keyboard.digit6Key);
            yield return null;
            Release(_keyboard.digit6Key);
            for (int i = 0; i < 40; i++) yield return null;
            int shot = 0;
            for (int i = 0; i < 12; i++)
            {
                Film(shot++, side, heading);
                yield return null;
            }
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            for (int i = 0; i < 70 && (_octaslash.IsActive || i < 52); i++)
            {
                Film(shot++, side, heading);
                yield return null;
            }
        }

        private void Film(int shot, Vector3 side, Vector3 heading)
        {
            Vector3 middle = _vitals.transform.position + Vector3.up * 1.3f;
            Shoot(middle + side * 7.0f + Vector3.up * 0.6f, middle, $"octaslash-film/side-{shot:D3}");
            Shoot(middle - heading * 6.0f + side * 1.6f + Vector3.up * 1.4f, middle + heading * 2.0f, $"octaslash-film/behind-{shot:D3}");
        }

        private void Both(string name, Vector3 side, Vector3 heading)
        {
            Vector3 middle = _vitals.transform.position + Vector3.up * 1.3f;
            Shoot(middle + side * 7.0f + Vector3.up * 0.6f, middle, name + "-side");
            Shoot(middle - heading * 6.0f + side * 1.6f + Vector3.up * 1.4f, middle + heading * 2.0f, name + "-behind");
        }

        private void Shoot(Vector3 eye, Vector3 at, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }
            var holder = new GameObject("OctaslashCam");
            _made.Add(holder);
            Camera camera = holder.AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            holder.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
            var rt = RenderTexture.GetTemporary(960, 640, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(960, 640, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(directory);
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
            Object.Destroy(holder);
        }
    }
}
