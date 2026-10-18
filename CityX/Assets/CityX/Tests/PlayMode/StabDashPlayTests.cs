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
    /// The stab dash (the Mii Swordfighter's Power Thrust) through the real keys in the real city: from its slot on the bar with the sword in hand
    /// it draws back in a squat, then lunges a dash's 4.8 m where the camera looks with the blade thrust out, running through what is ahead
    /// once; the body is held from the key to the end; a wall stops the slide short; without a sword nothing happens.
    /// </summary>
    public sealed class StabDashPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float Frame = 1.0f / 60.0f;
        private Scene _scene;
        private Keyboard _keyboard;
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private StabDashAbility _stab;
        private readonly List<GameObject> _made = new List<GameObject>();

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
            InputSystem.AddDevice<Mouse>();
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
            _stab = _vitals.GetComponent<StabDashAbility>();
            Assert.IsNotNull(_stab, "the player has no stab dash -- PlayerAbilityWiring");
            // On key 1, where the fireball is by default: a body with no character knows every skill (SkillBook.AsBeforeTheTree).
            Assert.IsTrue(PlayerSkills.On(_vitals.gameObject).PutOnBar(0, SkillCatalog.StabDash), "the stab dash would not go on the bar");
            if (withSword)
            {
                Inventory inventory = _vitals.GetComponent<Inventory>();
                inventory.AddItem(ItemRegistry.Sword, 1);
                Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.Sword, 1, out ItemInstanceData data));
                Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Sword, _vitals.gameObject, data), "could not equip the sword");
            }
            _vitals.InfiniteHealth = true;
            Time.captureFramerate = 60;   // one frame = 1/60 s, so the move's frames are the game's
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
            _made.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<Target>();
        }

        private IEnumerator UntilMoveFrame(float frame)
        {
            for (int i = 0; i < 200 && _stab.IsActive && _stab.Elapsed < frame * Frame - 1e-4f; i++)
            {
                yield return null;
            }
        }

        private IEnumerator PressStabKey()
        {
            Press(_keyboard.digit1Key);
            yield return null;
            Release(_keyboard.digit1Key);
        }

        [UnityTest]
        public IEnumerator TheKeyDrawsBackThenLungesADashsLengthWhereTheCameraLooksRunningThroughWhatIsAheadOnce()
        {
            yield return LoadScene(withSword: true);
            Vector3 heading = OpenRun.Face(_vitals, _made, 12.0f);
            Vector3 side = Vector3.Cross(Vector3.up, heading).normalized;
            yield return null;
            Vector3 start = _vitals.transform.position;
            // Beside the line it slides along, inside the thrust's reach as it passes: run through, never bumped into.
            Target target = Dummy(start + heading * 3.6f + side * 1.3f);
            Transform hips = _vitals.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            float standingHips = hips.localPosition.y;

            yield return PressStabKey();
            Assert.IsTrue(_stab.IsActive, "the key with the sword in hand did not start the stab dash");
            Press(_keyboard.aKey);   // the move holds the body: walking does nothing
            Press(_keyboard.spaceKey);   // nor does jumping

            yield return UntilMoveFrame(9);
            Vector3 flat = _vitals.transform.position - start;
            flat.y = 0.0f;
            Assert.Less(flat.magnitude, 0.1f, "the body moved while drawing back");
            Assert.Greater(_locomotion.BakedPoseWeight, 0.99f, "the animation is not on the body");
            Assert.Less(hips.localPosition.y, standingHips - 0.25f, "the body is not down in the squat");

            yield return UntilMoveFrame(20);
            Assert.Greater(Vector3.Dot(_vitals.transform.forward, heading), 0.98f, "the body does not face the stab");
            Assert.Less(hips.localPosition.y, standingHips - 0.3f, "the body is not low in the lunge");
            Transform hand = _vitals.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            Assert.Greater(_vitals.transform.InverseTransformPoint(hand.position).z, 0.9f, "the sword arm is not thrust out ahead");

            for (int i = 0; i < 120 && _stab.IsActive; i++)
            {
                yield return null;
            }
            Release(_keyboard.aKey);
            Release(_keyboard.spaceKey);
            Assert.IsFalse(_stab.IsActive, "the stab dash never ended");
            Vector3 travelled = _vitals.transform.position - start;
            float along = Vector3.Dot(travelled, heading);
            Assert.That(along, Is.InRange(4.4f, 5.1f), $"the lunge went {along:0.00} m, not a dash's 4.8");
            Assert.Less(Mathf.Abs(Vector3.Dot(travelled, side)), 0.3f, "the lunge did not go straight where the camera looked");
            Assert.AreEqual(1, target.Hits, "the thrust did not run through what it passed exactly once");
            Assert.AreEqual(1, target.Launches, "the clean thrust did not throw what it caught");
            Assert.AreEqual(0.0f, _locomotion.BakedPoseWeight, "the animation was not let go");
            Assert.IsFalse(_locomotion.MovementRooted, "the body was left rooted");
        }

        [UnityTest]
        public IEnumerator AWallStopsTheSlideShortAndTheMoveStillEnds()
        {
            yield return LoadScene(withSword: true);
            Vector3 heading = OpenRun.Face(_vitals, _made, 12.0f);
            yield return null;
            Vector3 start = _vitals.transform.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.transform.position = start + heading * 2.4f + Vector3.up * 1.5f;
            wall.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            wall.transform.localScale = new Vector3(6.0f, 4.0f, 0.4f);
            Physics.SyncTransforms();

            yield return PressStabKey();
            for (int i = 0; i < 120 && _stab.IsActive; i++)
            {
                yield return null;
            }
            Assert.IsFalse(_stab.IsActive, "the stab dash never ended");
            Assert.IsTrue(_stab.Stopped, "the wall did not stop the slide");
            Assert.Less(Vector3.Dot(_vitals.transform.position - start, heading), 2.2f, "the lunge went through the wall");
        }

        [UnityTest]
        public IEnumerator WithoutASwordTheKeyDoesNothing()
        {
            yield return LoadScene(withSword: false);
            Vector3 start = _vitals.transform.position;
            yield return PressStabKey();
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }
            Assert.IsFalse(_stab.IsActive, "the stab dash started with no sword in hand");
            Vector3 flat = _vitals.transform.position - start;
            flat.y = 0.0f;
            Assert.Less(flat.magnitude, 0.1f, "the body moved");
        }
    }
}
