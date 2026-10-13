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
    /// Shift+W (running) into it climbs it at once (guarded near doors, stairs and when grazing, see PlayerClimbing.TryStartAuto), a tap of Space (a jump) does not, and holding Space with W does.
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

            // This scene's own player and camera. A full PlayMode run can leave another body alive from an earlier test (something kept across
            // scene loads: its log reads "There are 2 event systems in the scene"), and a type-only lookup then drove that one while the wall went
            // in front of this one -- the run-at-a-wall test failed in full runs only (2026-10-12).
            PlayerInputReader input = null;
            Camera camera = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
                if (camera == null)
                {
                    foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (c.CompareTag("MainCamera")) { camera = c; break; }
                    }
                }
            }
            Assert.IsNotNull(input, "no player in the real scene");
            if (camera == null) camera = Camera.main;
            var locomotion = input.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            yield return Wait(0.3f);

            // The wall goes where the player will run: along the camera's facing, so W walks straight at it.
            Vector3 ahead = camera.transform.forward;
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

        /// <summary>How many doors are within <paramref name="radius"/> of a point, found the way PlayerClimbing finds them (their trigger volumes count).</summary>
        private static int DoorsNear(Vector3 point, float radius)
        {
            int doors = 0;
            foreach (Collider c in Physics.OverlapSphere(point + Vector3.up, radius, ~0, QueryTriggerInteraction.Collide))
            {
                if (c != null && c.GetComponentInParent<CityX.World.IOpenableDoor>() != null) doors++;
            }
            return doors;
        }

        /// <summary>What each of WallProbe's rays hits, for a failure only a full run shows.</summary>
        private static string ProbeRays(Vector3 feet, Vector3 forward, float height)
        {
            var text = new System.Text.StringBuilder();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            foreach (float fraction in new[] { 0.2f, 0.5f, 0.8f })
            {
                for (int side = -1; side <= 1; side++)
                {
                    Vector3 origin = feet + Vector3.up * (height * fraction) + right * (side * WallProbe.HalfWidth);
                    if (Physics.Raycast(origin, forward, out RaycastHit hit, WallProbe.Reach, ~0, QueryTriggerInteraction.Ignore))
                    {
                        text.Append($"[{fraction:0.0},{side}] {hit.collider.name} ({LayerMask.LayerToName(hit.collider.gameObject.layer)}) d {hit.distance:0.00} n {hit.normal} ");
                    }
                    else
                    {
                        text.Append($"[{fraction:0.0},{side}] miss ");
                    }
                }
            }
            return text.ToString();
        }

        private sealed class FakeDoor : MonoBehaviour, CityX.World.IOpenableDoor
        {
            public Vector3 Position => transform.position;
            public void SetOpen(bool open) { }
        }

        [UnityTest]
        public IEnumerator RunningAtATallWallWithShiftAndWClimbsItWithNoOtherInput()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            Assert.IsNotNull(rig.Climbing, "the player has no climbing component");
            float startY = rig.Locomotion.transform.position.y;
            Vector3 startAt = rig.Locomotion.transform.position;

            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            bool climbed = false;
            float closest = float.MaxValue;
            for (float elapsed = 0.0f; elapsed < 2.5f; elapsed += Time.deltaTime)
            {
                climbed |= rig.Climbing.IsClimbing;
                closest = Mathf.Min(closest, Vector3.Distance(_wall.GetComponent<Collider>().ClosestPoint(rig.Locomotion.transform.position), rig.Locomotion.transform.position));
                yield return null;
            }
            float endY = rig.Locomotion.transform.position.y;
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);

            if (!climbed)
            {
                // What happened, for a failure that only shows in a full run.
                Vector3 endAt = rig.Locomotion.transform.position;
                TestContext.WriteLine($"no climb: player from {startAt} to {endAt} (moved {Vector3.Distance(startAt, endAt):0.00} m), wall at {_wall.transform.position}, " +
                                      $"closest approach {closest:0.00} m, grounded {rig.Locomotion.IsGrounded}, doors within 3 m {DoorsNear(rig.Locomotion.transform.position, PlayerClimbing.DoorRadiusMetres)}, readers {Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None).Length}, " +
                                      $"scenes {SceneManager.sceneCount}, time scale {Time.timeScale}, capture {Time.captureFramerate}, " +
                                      $"main camera {(Camera.main != null ? Camera.main.name + " in " + Camera.main.gameObject.scene.name + " yaw " + Camera.main.transform.eulerAngles.y.ToString("0") : "none")}, " +
                                      $"run yaw {Quaternion.LookRotation(_wall.transform.forward).eulerAngles.y:0}, cameras {Camera.allCamerasCount}, " +
                                      $"stairs {rig.Climbing.RecentlyOnStairs}, stamina {(rig.Locomotion.GetComponent<PlayerVitals>() != null ? rig.Locomotion.GetComponent<PlayerVitals>().Stamina.ToString("0") : "-")}, " +
                                      $"jetpack {rig.Input.EquippedJetpack != null}, parachute {rig.Input.EquippedParachute != null}, " +
                                      $"wall probe {WallProbe.TryFind(rig.Locomotion.transform.position, _wall.transform.forward, rig.Locomotion.StandingHeight, out _)}, " +
                                      $"rays: {ProbeRays(rig.Locomotion.transform.position, _wall.transform.forward, rig.Locomotion.StandingHeight)}, " +
                                      $"backfaces {Physics.queriesHitBackfaces}, triggers {Physics.queriesHitTriggers}");
            }
            Assert.IsTrue(climbed, "running (Shift+W) into a tall wall did not start a climb");
            Assert.Greater(endY, startY + 1.0f, "it grabbed but did not climb");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RunningAtTheSameWallBesideADoorDoesNotClimb()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            var door = new GameObject("Door");
            door.transform.position = rig.Locomotion.transform.position + Vector3.up + rig.Locomotion.transform.right * 2.0f;
            door.AddComponent<BoxCollider>().isTrigger = true;
            door.AddComponent<FakeDoor>();
            Physics.SyncTransforms();

            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            float climbedFor = 0.0f;
            for (float elapsed = 0.0f; elapsed < 2.0f; elapsed += Time.deltaTime)
            {
                if (rig.Climbing.IsClimbing) climbedFor += Time.deltaTime;
                yield return null;
            }
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);
            Object.Destroy(door);

            Assert.AreEqual(0.0f, climbedFor, 1e-4f, "running at a wall next to a doorway started a climb");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RunningAtAChestHighBoxVaultsOverItAutomatically()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            var vaulting = rig.Locomotion.GetComponent<PlayerVaulting>();
            Assert.IsNotNull(vaulting, "the player has no vaulting component (rebuild the player prefab)");

            // Turn the test wall into a 1.9 m, 1 m deep box standing on the ground where the player runs.
            float groundY = rig.Locomotion.transform.position.y;
            Vector3 ahead = rig.Locomotion.transform.forward;
            ahead.y = 0.0f;
            ahead.Normalize();
            Vector3 box = rig.Locomotion.transform.position + ahead * 2.6f;
            _wall.transform.position = new Vector3(box.x, groundY + 0.95f, box.z);
            _wall.GetComponent<BoxCollider>().size = new Vector3(14.0f, 1.9f, 1.0f);
            Physics.SyncTransforms();
            Vector3 start = rig.Locomotion.transform.position;

            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            bool vaulted = false;
            for (float elapsed = 0.0f; elapsed < 3.0f; elapsed += Time.deltaTime)
            {
                vaulted |= vaulting.IsVaulting;
                yield return null;
            }
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);

            Assert.IsTrue(vaulted, "running (Shift+W) at a chest-high box did not vault it");
            float along = Vector3.Dot(rig.Locomotion.transform.position - start, ahead);
            Assert.Greater(along, 3.6f, "the player did not end up on the far side of the box");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RunningUpAStaircaseIntoTheWallAtItsTopDoesNotClimb()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            Vector3 ahead = rig.Locomotion.transform.forward;
            ahead.y = 0.0f;
            ahead.Normalize();
            Vector3 origin = rig.Locomotion.transform.position;
            var steps = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < 14; i++)
            {
                var step = new GameObject("Step");
                step.transform.position = origin + ahead * (0.9f + 0.32f * i + 0.16f) + Vector3.up * (0.1f * (i + 1));
                step.transform.rotation = Quaternion.LookRotation(ahead);
                step.AddComponent<BoxCollider>().size = new Vector3(3.0f, 0.2f * (i + 1), 0.32f);
                steps.Add(step);
            }
            // The wall at the top of the stair, a step's depth past the last tread.
            _wall.transform.position = origin + ahead * (0.9f + 0.32f * 14 + 0.5f) + Vector3.up * 14.0f;
            Physics.SyncTransforms();

            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            float climbedFor = 0.0f;
            float peak = origin.y;
            for (float elapsed = 0.0f; elapsed < 4.0f; elapsed += Time.deltaTime)
            {
                peak = Mathf.Max(peak, rig.Locomotion.transform.position.y);
                if (rig.Climbing.IsClimbing) climbedFor += Time.deltaTime;
                yield return null;
            }
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);
            foreach (GameObject step in steps) Object.Destroy(step);

            Assert.Greater(peak, origin.y + 1.0f, "setup: the player never got up the stairs");
            Assert.AreEqual(0.0f, climbedFor, 1e-4f, "running up a staircase into the wall at its top started a climb");
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
