using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Climbing through the REAL input dispatch, with a real simulated keyboard. Reported: "i always find myself
    /// climbing when not intending, like when i'm running up a stairs or trying to jump through a window, or
    /// even sometimes when running through a doorway."
    ///
    /// A wall is put in front of the real player, and the same wall is run into, jumped at and grabbed:
    /// Shift+W (running) must never climb it, a tap of Space (a jump) must not, and holding Space with W must.
    /// </summary>
    public sealed class RealInputClimbingPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private GameObject _wall;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_wall != null) Object.Destroy(_wall);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                yield return null;
            }
        }

        private struct Rig
        {
            public PlayerInputReader Input;
            public PlayerLocomotion Locomotion;
            public PlayerClimbing Climbing;
            public WallStick Stick;
        }

        /// <summary>Loads the real scene, waits for the player to land, and puts a tall wall 1.2m ahead of where the camera looks.</summary>
        private IEnumerator Stage(System.Action<Rig> done)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            var locomotion = input.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            yield return Wait(0.3f);

            // The wall goes where the player will run: along the camera's facing, so W walks straight at it.
            Vector3 ahead = Camera.main.transform.forward;
            ahead.y = 0.0f;
            ahead.Normalize();
            Vector3 player = locomotion.transform.position;
            _wall = new GameObject("TestWall");
            _wall.transform.position = player + ahead * 1.7f + Vector3.up * 14.0f;
            _wall.transform.rotation = Quaternion.LookRotation(ahead);
            _wall.AddComponent<BoxCollider>().size = new Vector3(14.0f, 30.0f, 1.0f);
            Physics.SyncTransforms();

            done(new Rig
            {
                Input = input,
                Locomotion = locomotion,
                Climbing = input.GetComponent<PlayerClimbing>(),
                Stick = input.GetComponent<WallStick>(),
            });
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RunningAtAWallWithShiftAndWNeverClimbsIt()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            Assert.IsNotNull(rig.Climbing, "the player has no climbing component");

            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            float climbedFor = 0.0f;
            for (float elapsed = 0.0f; elapsed < 2.0f; elapsed += Time.deltaTime)
            {
                if (rig.Climbing.IsClimbing || rig.Stick.IsSticking) climbedFor += Time.deltaTime;
                yield return null;
            }
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);

            Assert.AreEqual(0.0f, climbedFor, 1e-4f, "running into a wall (Shift+W) started a climb -- what the player reported");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator JumpingAtAWallWithATapOfSpaceJumpsAndDoesNotGrab()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);

            Press(_keyboard.wKey);
            yield return Wait(0.15f);
            Press(_keyboard.spaceKey);
            yield return Wait(0.06f);   // a tap: well under a grab's hold
            Release(_keyboard.spaceKey);

            float climbedFor = 0.0f;
            float peak = rig.Locomotion.transform.position.y;
            float start = peak;
            for (float elapsed = 0.0f; elapsed < 1.5f; elapsed += Time.deltaTime)
            {
                peak = Mathf.Max(peak, rig.Locomotion.transform.position.y);
                if (rig.Climbing.IsClimbing || rig.Stick.IsSticking) climbedFor += Time.deltaTime;
                yield return null;
            }
            Release(_keyboard.wKey);

            Assert.AreEqual(0.0f, climbedFor, 1e-4f, "a tap of Space at a wall grabbed it instead of just jumping");
            Assert.Greater(peak, start + 0.3f, "the tap did not even jump");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HoldingSpaceAndWAtAWallClimbsIt()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);

            float startY = rig.Locomotion.transform.position.y;
            Press(_keyboard.wKey);
            yield return Wait(0.1f);
            Press(_keyboard.spaceKey);

            bool grabbed = false;
            for (float elapsed = 0.0f; elapsed < 1.5f && !grabbed; elapsed += Time.deltaTime)
            {
                grabbed = rig.Climbing.IsClimbing;
                yield return null;
            }
            Assert.IsTrue(grabbed, "holding Space with W against a tall wall never grabbed it");

            // And it climbs: up the wall while W is held.
            Release(_keyboard.spaceKey);
            yield return Wait(1.0f);
            Release(_keyboard.wKey);
            Assert.Greater(rig.Locomotion.transform.position.y, startY + 1.5f, "the grab did not carry the player up the wall");
#else
            yield break;
#endif
        }
    }
}
