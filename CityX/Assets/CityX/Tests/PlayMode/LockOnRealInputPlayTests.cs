using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// TAB LOCK-ON through the real input path: a simulated keyboard/mouse, the
    /// real PlayerInputReader and ThirdPersonCamera, the real saved scene.
    /// "Make it tab to lock on to an enemy and then when you keep pressing it,
    /// it rotates through the surrounding enemies."
    ///
    /// The pure target-picking rules are in LockOnTests (EditMode); this is the
    /// wiring -- that Tab reaches the camera, the camera actually swings to the
    /// enemy, the body faces it, WASD strafes around it, and the button cycles
    /// and finally lets go.
    /// </summary>
    public sealed class LockOnRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;

        private PlayerVitals _vitals;
        private ThirdPersonCamera _camera;
        private readonly List<SparringDummy> _dummies = new List<SparringDummy>();
        private readonly List<GameObject> _extras = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (SparringDummy dummy in _dummies)
            {
                if (dummy != null)
                {
                    Object.Destroy(dummy.gameObject);
                }
            }
            _dummies.Clear();
            foreach (GameObject extra in _extras)
            {
                if (extra != null)
                {
                    Object.Destroy(extra);
                }
            }
            _extras.Clear();

            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            _vitals = Object.FindFirstObjectByType<PlayerVitals>();
            Assert.IsNotNull(_vitals, "no PlayerVitals in the real scene");
            _camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Assert.IsNotNull(_camera, "no ThirdPersonCamera in the real scene");
            _vitals.InfiniteHealth = true;

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// A dummy `distance` metres from the player, `degrees` to the right of
        /// where the camera is looking (negative = left), standing on the ground.
        /// </summary>
        private SparringDummy PlaceDummy(float distance, float degrees)
        {
            Vector3 look = _camera.transform.forward;
            look.y = 0.0f;
            look.Normalize();
            Vector3 direction = Quaternion.Euler(0.0f, degrees, 0.0f) * look;
            Vector3 position = _vitals.transform.position + direction * distance;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "LockOnDummy";
            // Stand it on the ground at the PLAYER'S height, found by casting
            // down from just above the player -- casting from high up lands on
            // a rooftop wherever a building is near, and a dummy on a roof is
            // out of sight (the lock correctly lets go of it).
            position.y = _vitals.transform.position.y;
            if (Physics.Raycast(position + Vector3.up * 2.0f, Vector3.down, out RaycastHit hit, 6.0f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }
            go.transform.position = position + Vector3.up * 1.0f;
            var dummy = go.AddComponent<SparringDummy>();
            dummy.Reset();
            _dummies.Add(dummy);
            Physics.SyncTransforms();
            return dummy;
        }

        private IEnumerator TapTab()
        {
            Press(_keyboard.tabKey);
            yield return null;
            Release(_keyboard.tabKey);
            yield return null;
        }

        private IEnumerator Wait(float seconds)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private static float HorizontalAngle(Vector3 forward, Vector3 toTarget)
        {
            forward.y = 0.0f;
            toTarget.y = 0.0f;
            return Vector3.Angle(forward, toTarget);
        }

        [UnityTest]
        public IEnumerator TabWithNoEnemiesDoesNothing()
        {
#if UNITY_EDITOR
            yield return LoadScene();

            yield return TapTab();

            Assert.IsFalse(_camera.IsLockedOn, "locked onto nothing");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TabLocksOntoTheEnemyYouAreLookingAtAndTheCameraSwingsToIt()
        {
#if UNITY_EDITOR
            yield return LoadScene();
            SparringDummy behind = PlaceDummy(5.0f, 180.0f);
            SparringDummy ahead = PlaceDummy(9.0f, 15.0f);

            yield return TapTab();

            Assert.IsTrue(_camera.IsLockedOn, "Tab did not lock on");
            Assert.AreSame(ahead.transform, _camera.LockedTarget,
                "it locked onto the closer enemy behind instead of the one being looked at");

            yield return Wait(1.5f);

            float cameraError = HorizontalAngle(_camera.transform.forward,
                ahead.transform.position - _vitals.transform.position);
            Assert.Less(cameraError, 5.0f, $"the camera never swung round to the enemy ({cameraError:0.0} degrees off)");

            float bodyError = HorizontalAngle(_vitals.transform.forward,
                ahead.transform.position - _vitals.transform.position);
            Assert.Less(bodyError, 10.0f, $"the body is not facing the enemy ({bodyError:0.0} degrees off)");
            Assert.IsNotNull(behind);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator KeepPressingTabRotatesThroughEveryEnemyThenReleases()
        {
#if UNITY_EDITOR
            yield return LoadScene();
            SparringDummy left = PlaceDummy(9.0f, -30.0f);
            SparringDummy centre = PlaceDummy(9.0f, 2.0f);
            SparringDummy right = PlaceDummy(9.0f, 30.0f);

            yield return TapTab();
            Assert.AreSame(centre.transform, _camera.LockedTarget, "the first press should grab the one dead ahead");

            var visited = new HashSet<Transform> { _camera.LockedTarget };
            int presses = 1;
            while (_camera.IsLockedOn && presses < 8)
            {
                yield return TapTab();
                presses++;
                if (_camera.IsLockedOn)
                {
                    visited.Add(_camera.LockedTarget);
                }
            }

            Assert.AreEqual(3, visited.Count, "pressing Tab never reached every enemy");
            Assert.IsTrue(visited.Contains(left.transform));
            Assert.IsTrue(visited.Contains(right.transform));
            Assert.IsFalse(_camera.IsLockedOn, "the press after the last enemy did not release");
            Assert.AreEqual(4, presses, "3 enemies should be: lock, cycle, cycle, release");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator StrafingWhileLockedCirclesTheEnemyAndKeepsFacingIt()
        {
#if UNITY_EDITOR
            yield return LoadScene();
            SparringDummy target = PlaceDummy(9.0f, 0.0f);

            yield return TapTab();
            Assert.IsTrue(_camera.IsLockedOn);
            yield return Wait(1.0f);

            Vector3 startPosition = _vitals.transform.position;
            float startDistance = Vector3.Distance(startPosition, target.transform.position);

            Press(_keyboard.dKey);
            yield return Wait(1.0f);
            Release(_keyboard.dKey);

            Vector3 moved = _vitals.transform.position - startPosition;
            moved.y = 0.0f;
            Assert.Greater(moved.magnitude, 1.0f, "holding D while locked did not move the body");

            float faceError = HorizontalAngle(_vitals.transform.forward,
                target.transform.position - _vitals.transform.position);
            Assert.Less(faceError, 15.0f,
                $"strafing turned the body {faceError:0.0} degrees away from the enemy -- it should keep facing it");

            // Moving sideways, the distance to the enemy should stay roughly the same.
            float endDistance = Vector3.Distance(_vitals.transform.position, target.transform.position);
            Assert.Greater(endDistance, startDistance * 0.6f);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HoldingWWhileLockedWalksTowardTheEnemy()
        {
#if UNITY_EDITOR
            yield return LoadScene();
            SparringDummy target = PlaceDummy(12.0f, 0.0f);

            yield return TapTab();
            yield return Wait(1.0f);
            float before = Vector3.Distance(_vitals.transform.position, target.transform.position);

            Press(_keyboard.wKey);
            yield return Wait(1.0f);
            Release(_keyboard.wKey);

            float after = Vector3.Distance(_vitals.transform.position, target.transform.position);
            Assert.Less(after, before - 1.0f, "W while locked did not close the distance to the enemy");
#else
            yield break;
#endif
        }

        /// <summary>
        /// The ask was to lock onto ENEMIES. A pedestrian dead ahead must not
        /// take the lock (nor become a stop when cycling), or walking down any
        /// street makes Tab useless.
        /// </summary>
        [UnityTest]
        public IEnumerator PedestriansAreNotLockableButAHostileMonsterIs()
        {
#if UNITY_EDITOR
            yield return LoadScene();

            Vector3 look = _camera.transform.forward;
            look.y = 0.0f;
            look.Normalize();

            var civilianObject = new GameObject("Pedestrian");
            _extras.Add(civilianObject);
            civilianObject.transform.position = _vitals.transform.position + look * 6.0f;
            var body = civilianObject.AddComponent<CharacterController>();
            body.height = 2.2f;
            body.radius = 0.35f;
            body.center = new Vector3(0.0f, 1.1f, 0.0f);
            civilianObject.AddComponent<Civilian>();
            Physics.SyncTransforms();

            yield return TapTab();
            Assert.IsFalse(_camera.IsLockedOn, "Tab locked onto a pedestrian");

            var clawObject = new GameObject("Claw");
            _extras.Add(clawObject);
            clawObject.transform.position = _vitals.transform.position + look * 10.0f;
            var capsule = clawObject.AddComponent<CapsuleCollider>();
            capsule.height = 2.0f;
            capsule.center = new Vector3(0.0f, 1.0f, 0.0f);
            var claw = clawObject.AddComponent<CrimsonClaw>();
            claw.LeftClawPivot = new GameObject("L").transform;
            claw.LeftClawPivot.SetParent(clawObject.transform, false);
            claw.RightClawPivot = new GameObject("R").transform;
            claw.RightClawPivot.SetParent(clawObject.transform, false);
            Physics.SyncTransforms();

            yield return TapTab();
            Assert.IsTrue(_camera.IsLockedOn, "Tab did not lock onto a hostile monster");
            Assert.AreSame(clawObject.transform, _camera.LockedTarget);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheLockReleasesWhenTheEnemyDies()
        {
#if UNITY_EDITOR
            yield return LoadScene();
            SparringDummy target = PlaceDummy(9.0f, 0.0f);

            yield return TapTab();
            Assert.IsTrue(_camera.IsLockedOn);

            ((IDamageable)target).ApplyHit(target.MaxHealth * 10.0f, Vector3.zero, 0.0f);
            yield return Wait(0.2f);

            Assert.IsFalse(_camera.IsLockedOn, "the lock stayed on a corpse");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheLockReleasesWhenTheEnemyIsFarOutOfRange()
        {
#if UNITY_EDITOR
            yield return LoadScene();
            SparringDummy target = PlaceDummy(10.0f, 0.0f);

            yield return TapTab();
            Assert.IsTrue(_camera.IsLockedOn);

            target.transform.position += (target.transform.position - _vitals.transform.position).normalized * 60.0f;
            Physics.SyncTransforms();
            yield return Wait(0.2f);

            Assert.IsFalse(_camera.IsLockedOn, "the lock followed an enemy 70m away");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASwordSwingWhileLockedGoesAtTheEnemy()
        {
#if UNITY_EDITOR
            yield return LoadScene();

            Inventory inventory = _vitals.GetComponent<Inventory>();
            inventory.AddItem(ItemRegistry.SpikeSword, 1);
            Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.SpikeSword, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.SpikeSword, _vitals.gameObject, data));

            SparringDummy target = PlaceDummy(2.4f, 40.0f);   // well off to the side of where the camera looks
            yield return TapTab();
            Assert.IsTrue(_camera.IsLockedOn);
            yield return Wait(1.5f);

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return Wait(1.2f);

            Assert.GreaterOrEqual(target.TimesHit, 1,
                "a swing thrown while locked on did not land on the locked enemy");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-10-24: "turn off tab lock on for ranged weapons". With the pistol in hand Tab locks onto nothing (the gun is aimed with
        /// the crosshair), and drawing it while locked on lets the lock go; put away, Tab locks on again.
        /// </summary>
        [UnityTest]
        public IEnumerator WithAGunInHandTabDoesNotLockOnAndDrawingOneLetsALockGo()
        {
#if UNITY_EDITOR
            yield return LoadScene();
            PlaceDummy(6.0f, 10.0f);
            yield return TapTab();
            Assert.IsTrue(_camera.IsLockedOn, "Tab did not lock on bare-handed");

            Inventory inventory = _vitals.GetComponent<Inventory>();
            inventory.AddItem(ItemRegistry.Pistol, 1);
            Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.Pistol, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Pistol, _vitals.gameObject, data), "the pistol could not be drawn");
            yield return null;
            yield return null;
            Assert.IsTrue(_vitals.GetComponent<PlayerInputReader>().HoldsRangedWeapon, "the pistol does not count as a ranged weapon");
            Assert.IsFalse(_camera.IsLockedOn, "drawing the pistol did not let the lock go");

            yield return TapTab();
            Assert.IsFalse(_camera.IsLockedOn, "Tab locked on with a gun in hand");

            Assert.IsTrue(inventory.UnequipToSlot(EquipSlots.Primary, _vitals.gameObject), "the pistol could not be put away");
            yield return null;
            yield return TapTab();
            Assert.IsTrue(_camera.IsLockedOn, "with the gun put away, Tab no longer locks on");
#else
            yield break;
#endif
        }
    }
}
