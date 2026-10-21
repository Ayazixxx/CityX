using System.Collections;
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
    /// The sword's controls through the REAL input path: a simulated mouse and
    /// keyboard, the real PlayerInputReader, the real saved scene, a sword
    /// equipped the way /equip spikesword equips it. The state machine itself is
    /// covered without any of that (SoulsSwordCombatPlayTests); this is the
    /// wiring -- that a click starts a swing, the body is actually rooted for
    /// it, right-click is a HELD guard, and a roll can cut the recovery short.
    /// Same approach, and same reason, as GrappleSwingRealInputPlayTests.
    /// </summary>
    public sealed class SoulsSwordRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private SoulsSwordCombat _combat;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator LoadSceneWithASword()
        {
#if UNITY_EDITOR
            // The real scene's camera cannot render under the default
            // -nographics run; unrelated to what is under test.
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            _vitals = Object.FindFirstObjectByType<PlayerVitals>();
            if (_vitals != null) _vitals.Roll = () => 1.0f;   // no outright blocks (Block Chance): these guards must behave the same every run
            Assert.IsNotNull(_vitals, "no PlayerVitals in the real scene");
            _locomotion = _vitals.GetComponent<PlayerLocomotion>();

            Inventory inventory = _vitals.GetComponent<Inventory>();
            Assert.IsNotNull(inventory, "the player has no inventory");
            inventory.AddItem(ItemRegistry.SpikeSword, 1);
            Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.SpikeSword, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.SpikeSword, _vitals.gameObject, data), "could not equip the sword");

            _combat = _vitals.GetComponent<SoulsSwordCombat>();
            Assert.IsNotNull(_combat, "equipping the sword did not give the body its soulslike combat");
            _vitals.InfiniteHealth = true;

            // Let the spawn settle onto the ground before anything is asserted.
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AClickStartsASwingAndTheBodyKeepsWalkingUnderItSlowed()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            Vector3 before = _vitals.transform.position;
            Press(_keyboard.wKey);
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;

            Assert.IsTrue(_combat.IsCommitted, "a real click never started a swing");
            Assert.AreEqual(_combat.Light1.Animation, _combat.CurrentAttack.Animation);

            // Held W through the whole swing. Requested: "make the legs walk
            // and not freeze up during sword use too" -- the swing plays on the
            // upper body now, so the body WALKS under it, but a committed swing
            // still slows you hard (CommittedMoveSpeedFraction). It used to be
            // fully rooted; this checks the new middle: moving, but slowed.
            float committedFor = 0.0f;
            while (_combat.IsCommitted && committedFor < 2.0f)
            {
                committedFor += Time.deltaTime;
                yield return null;
            }
            Vector3 after = _vitals.transform.position;
            float horizontal = new Vector2(after.x - before.x, after.z - before.z).magnitude;
            Release(_keyboard.wKey);

            float freeWalk = _locomotion.WalkSpeed * (committedFor + 2.0f * Time.deltaTime);
            float lunge = _combat.Light1.LungeMetres + 0.2f;
            Assert.Greater(horizontal, 0.3f,
                $"the body only moved {horizontal:0.00}m while holding W through a swing -- it is still rooted");
            Assert.Less(horizontal, freeWalk * 0.75f + lunge,
                $"the body walked {horizontal:0.00}m through a swing of {committedFor:0.00}s -- almost free walking ({freeWalk:0.00}m); a committed swing should slow you");
#else
            yield break;
#endif
        }

        /// <summary>Explicit ask: "F shouldnt do anything yet." The heavy exists but has no key.</summary>
        [UnityTest]
        public IEnumerator FDoesNothing()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            float staminaBefore = _vitals.Stamina;
            Press(_keyboard.fKey);
            yield return null;
            Release(_keyboard.fKey);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.IsFalse(_combat.IsCommitted, "F started an attack");
            Assert.GreaterOrEqual(_vitals.Stamina, staminaBefore - 0.01f, "F spent stamina");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RightClickIsAHeldGuardThatDropsWhenReleased()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            Press(_mouse.rightButton);
            float held = 0.0f;
            while (held < 1.5f)
            {
                held += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(_combat.IsBlocking, "right-click did not raise the guard");
            Assert.IsTrue(_vitals.IsBlocking, "the guard timed out while the button was still held");

            Release(_mouse.rightButton);
            yield return null;
            yield return null;

            Assert.IsFalse(_combat.IsBlocking);
            Assert.IsFalse(_vitals.IsBlocking, "releasing right-click did not drop the guard");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SwingingSpendsStaminaAndItComesBackAfterThePause()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            float full = _vitals.Stamina;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
            float afterSwing = _vitals.Stamina;
            Assert.Less(afterSwing, full - 10.0f, "a swing cost no stamina");

            float waited = 0.0f;
            while (waited < 3.0f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(_vitals.Stamina, afterSwing + 20.0f, "stamina never came back after the swing");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ADoubleTapOfShiftRollsOutOfARecoveryButNotOutOfAWindUp()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            // Start a heavy (asked for directly -- no key is bound to it): its
            // wind-up is long, so a roll during it must be refused.
            _combat.RequestHeavy(_vitals.transform.eulerAngles.y);
            yield return null;
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase);

            Press(_keyboard.leftShiftKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return null;
            Press(_keyboard.leftShiftKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return null;

            Assert.IsFalse(_locomotion.IsDashing, "rolled out of a swing's wind-up");
            Assert.IsTrue(_combat.IsCommitted, "the roll attempt cancelled the swing");

            // Now wait for its recovery and roll out of that.
            float waited = 0.0f;
            while (_combat.Phase != SoulsPhase.Recovery && waited < 4.0f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(SoulsPhase.Recovery, _combat.Phase, "the heavy never reached its recovery");

            Press(_keyboard.leftShiftKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return null;
            Press(_keyboard.leftShiftKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return null;

            Assert.IsTrue(_locomotion.IsDashing, "could not roll out of a recovery");
            Assert.IsFalse(_combat.IsCommitted, "the roll did not cut the recovery short");
#else
            yield break;
#endif
        }
    }
}
