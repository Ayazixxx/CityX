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

        /// <summary>
        /// Owner, 2026-11-13: sword swings "akin to dark souls combat", and committed swings chosen over walking under them. W held all
        /// through a swing, the body goes only as far as the attack's own step carries it; from the recovery's tail on it walks again.
        /// </summary>
        [UnityTest]
        public IEnumerator AClickStartsASwingThatRootsTheBodyAndCarriesItByItsOwnStep()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            Press(_keyboard.wKey);
            yield return null;
            Vector3 before = _vitals.transform.position;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;

            Assert.IsTrue(_combat.IsCommitted, "a real click never started a swing");
            Assert.AreEqual(_combat.Light1.Animation, _combat.CurrentAttack.Animation);

            float rootedFor = 0.0f;
            while (_combat.SlowsMovement && rootedFor < 2.0f)
            {
                rootedFor += Time.deltaTime;
                yield return null;
            }
            Vector3 after = _vitals.transform.position;
            float went = new Vector2(after.x - before.x, after.z - before.z).magnitude;

            float freeWalk = _locomotion.WalkSpeed * rootedFor;
            float step = _combat.Light1.LungeMetres;
            Debug.Log($"CityX committed swing: W held, the body went {went:0.00} m in {rootedFor:0.00} s (its step is {step:0.00} m; walking would be {freeWalk:0.00} m)");
            Assert.Greater(went, step * 0.8f, $"the body went {went:0.00} m: the swing's own step ({step:0.00} m) did not carry it");
            // (The frame the click is read on and the one the swing starts on are walked.)
            Assert.Less(went, step + 0.3f, $"the body went {went:0.00} m through a swing of {rootedFor:0.00} s with a {step:0.00} m step: it walks under the swing");

            // Out of the swing the keys walk the body again.
            Vector3 freed = _vitals.transform.position;
            for (float t = 0.0f; t < 0.6f; t += Time.deltaTime)
            {
                yield return null;
            }
            Release(_keyboard.wKey);
            Vector3 walked = _vitals.transform.position - freed;
            Assert.Greater(new Vector2(walked.x, walked.z).magnitude, 0.8f, "the body stays rooted after the swing has let it go");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-13: "the movement of the character carries over into the attack ... if you are walking forward it does a slightly
        /// different attack ... right now it feels like attacking forces you to stay still or slow down". Walking, a click throws the sweep
        /// made for a body on the move, and the body carries on at its pace into it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingAClickThrowsTheCutOnTheMoveAndTheBodyKeepsItsPaceIntoIt()
        {
            yield return ThrowOnTheMove(sprinting: false);
        }

        /// <summary>"... for instance if you are sprinting it does a different attack": the running attack, with the sprint carried into it.</summary>
        [UnityTest]
        public IEnumerator SprintingAClickThrowsTheRunningAttackAndTheBodyKeepsItsPaceIntoIt()
        {
            yield return ThrowOnTheMove(sprinting: true);
        }

        private IEnumerator ThrowOnTheMove(bool sprinting)
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            Time.captureFramerate = 60;
            try
            {
                const float slice = 0.1f;
                Press(_keyboard.wKey);
                yield return null;
                if (sprinting)
                {
                    Press(_keyboard.leftShiftKey);
                }
                for (float t = 0.0f; t < 1.1f; t += Time.deltaTime)
                {
                    yield return null;
                }
                Vector3 sliceFrom = _vitals.transform.position;
                float sliceSeconds = 0.0f;
                while (sliceSeconds < slice - 1e-4f)
                {
                    yield return null;
                    sliceSeconds += Time.deltaTime;
                }
                float pace = Flat(_vitals.transform.position - sliceFrom).magnitude / sliceSeconds;
                SoulsAttack expected = sprinting ? _combat.RunAttack : _combat.WalkLight1;
                Assert.Greater(pace, sprinting ? SwordStrikes.RunOpenerFromSpeed : SwordStrikes.WalkOpenerFromSpeed, "the body never got up to its pace: nothing to carry");

                Vector3 clickedAt = _vitals.transform.position;
                Press(_mouse.leftButton);
                yield return null;
                Release(_mouse.leftButton);
                yield return null;
                Assert.IsTrue(_combat.IsCommitted, "a real click never started a swing");
                Assert.AreEqual(expected.Animation, _combat.CurrentAttack.Animation, $"going at {pace:0.0} m/s the click threw the wrong opener");
                Assert.Greater(_combat.EntrySpeed, pace * 0.8f, "the swing did not take the body's pace with it");

                // Slice by slice until the blade has done its work: the body is never brought to a stop on the way in.
                float window = expected.Startup + expected.Active;
                float slowest = float.MaxValue, first = -1.0f;
                Vector3 from = _vitals.transform.position;
                float sliceTime = 0.0f, elapsed = 2.0f / 60.0f;
                while (elapsed < window)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                    sliceTime += Time.deltaTime;
                    if (sliceTime >= slice - 1e-4f)
                    {
                        float speed = Flat(_vitals.transform.position - from).magnitude / sliceTime;
                        if (first < 0.0f) first = speed;
                        if (elapsed < window * 0.8f) slowest = Mathf.Min(slowest, speed);
                        from = _vitals.transform.position;
                        sliceTime = 0.0f;
                    }
                }
                float carried = Flat(_vitals.transform.position - clickedAt).magnitude;
                Debug.Log($"CityX thrown on the move ({expected.Animation}): came in at {pace:0.0} m/s, went on at {first:0.0} m/s, never slower than {slowest:0.0} m/s " +
                          $"before the blow, carried {carried:0.00} m (made for {expected.LungeMetres:0.00} m)");
                Assert.Greater(first, pace * 0.75f, $"the body came in at {pace:0.0} m/s and was down to {first:0.0} m/s a tenth of a second into the swing: the swing stops it");
                Assert.Greater(slowest, 1.0f, $"the body all but stopped ({slowest:0.0} m/s) on its way into the cut");
                // (The frame the click is read on and the one the swing starts on are still walked or run.)
                Assert.AreEqual(expected.LungeMetres + pace * 2.0f / 60.0f, carried, 0.3f, "the body was not carried as far as the attack is made for");

                // The chain goes on from it into the second light.
                Press(_mouse.leftButton);
                yield return null;
                Release(_mouse.leftButton);
                for (float t = 0.0f; t < 1.0f && _combat.CurrentAttack.Animation != _combat.Light2.Animation; t += Time.deltaTime)
                {
                    yield return null;
                }
                Assert.AreEqual(_combat.Light2.Animation, _combat.CurrentAttack.Animation, "the opener did not chain into the second light");
                Assert.AreEqual(0.0f, _combat.EntrySpeed, "a chained cut carries a pace: it starts from a body at rest");
            }
            finally
            {
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
#else
            yield break;
#endif
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        /// <summary>
        /// Owner, 2026-10-24: "make it so holding click does a heavy guardbreaking attack". A click held a quarter of a second makes the
        /// heavy (the slam, with the two-handed spike sword); a quick click stays the light; the wheel's click no longer does anything.
        /// </summary>
        [UnityTest]
        public IEnumerator HoldingTheClickSwingsTheHeavyATapTheLight()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            Time.captureFramerate = 60;
            try
            {
                Press(_mouse.leftButton);
                for (int i = 0; i < 4; i++)   // about 0.07 s: a click
                {
                    yield return null;
                }
                Release(_mouse.leftButton);
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                }
                Assert.AreEqual(_combat.Light1.Animation, _combat.CurrentAttack?.Animation, "a quick click did not stay the light");
                for (int i = 0; i < 180 && _combat.IsCommitted; i++)
                {
                    yield return null;
                }

                Press(_mouse.middleButton);
                yield return null;
                Release(_mouse.middleButton);
                yield return null;
                Assert.IsFalse(_combat.IsCommitted, "the wheel's click still swings");
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                }

                Press(_mouse.leftButton);
                for (int i = 0; i < 15; i++)   // a quarter of a second
                {
                    yield return null;
                }
                Release(_mouse.leftButton);
                yield return null;
                Assert.AreEqual(_combat.Heavy.Animation, _combat.CurrentAttack?.Animation, "holding the click did not swing the heavy");
                Assert.IsTrue(_combat.CurrentAttack.BreaksGuard, "the held heavy does not break guards");
            }
            finally
            {
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
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
