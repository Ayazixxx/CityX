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

        /// <summary>A click with no key held: the body is stepped into its cut by the attack's own stride, and no further.</summary>
        [UnityTest]
        public IEnumerator StandingAClickStepsTheBodyIntoItsCut()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            Time.captureFramerate = 60;
            try
            {
                Vector3 before = _vitals.transform.position;
                Press(_mouse.leftButton);
                yield return null;
                Release(_mouse.leftButton);
                yield return null;
                Assert.IsTrue(_combat.IsCommitted, "a real click never started a swing");
                Assert.AreEqual(_combat.Light1.Animation, _combat.CurrentAttack.Animation);
                for (float t = 0.0f; t < 2.0f && _combat.IsCommitted; t += Time.deltaTime)
                {
                    yield return null;
                }
                float went = Flat(_vitals.transform.position - before).magnitude;
                Assert.AreEqual(_combat.Light1.LungeMetres, went, 0.1f, "a standing body was not stepped into its cut by the attack's own stride");
            }
            finally
            {
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-13: "You need to have the sword movements conserve momentum and move freely while attacking. It feels bad right
        /// now because you are slowed down every time you attack. You should be able to move freely with zero slowdown". Walking with W
        /// held, a click swings the chain's first cut and the body walks on through the whole swing at the pace it had.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingAClickSwingsAndTheBodyWalksOnAtItsFullPace()
        {
            yield return SwingOnTheMove(sprinting: false);
        }

        /// <summary>
        /// "... if you are sprinting it does a different attack": the running attack, and the body sprints on through it at its full pace.
        /// </summary>
        [UnityTest]
        public IEnumerator SprintingAClickThrowsTheRunningAttackAndTheBodySprintsOnAtItsFullPace()
        {
            yield return SwingOnTheMove(sprinting: true);
        }

        /// <summary>The least of its pace a body may be measured at through a swing: a twentieth is the measuring (a tenth of a second's worth of frames).</summary>
        private const float FullPaceShare = 0.95f;

        private IEnumerator SwingOnTheMove(bool sprinting)
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            _vitals.InfiniteStamina = true;   // a sprint that runs out of breath mid-swing is another slowdown, and not the sword's
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
                Assert.Greater(pace, (sprinting ? _locomotion.SprintSpeed : _locomotion.WalkSpeed) * 0.9f, "the body never got up to its pace");

                SoulsAttack expected = sprinting ? _combat.RunAttack : _combat.Light1;
                Press(_mouse.leftButton);
                yield return null;
                Release(_mouse.leftButton);
                yield return null;
                Assert.IsTrue(_combat.IsCommitted, "a real click never started a swing");
                Assert.AreEqual(expected.Animation, _combat.CurrentAttack.Animation, $"going at {pace:0.0} m/s the click threw the wrong attack");

                // Slice by slice through the swing, wind-up, blow and recovery, and on into the chain's next cut (clicked for as the first
                // one lands): the body goes on at the pace it had.
                Vector3 swungFrom = _vitals.transform.position, from = swungFrom;
                float slowest = float.MaxValue, swung = 0.0f, sliceTime = 0.0f, intoTheNext = 0.0f;
                int clicked = 0;
                while (swung < 3.0f && intoTheNext < 0.4f)
                {
                    if (clicked == 0 && _combat.Phase == SoulsPhase.Recovery)
                    {
                        Press(_mouse.leftButton);
                        clicked = 1;
                    }
                    else if (clicked == 1)
                    {
                        Release(_mouse.leftButton);
                        clicked = 2;
                    }
                    yield return null;
                    swung += Time.deltaTime;
                    if (_combat.CurrentAttack.Animation != expected.Animation)
                    {
                        intoTheNext += Time.deltaTime;
                    }
                    sliceTime += Time.deltaTime;
                    if (sliceTime >= slice - 1e-4f)
                    {
                        slowest = Mathf.Min(slowest, Flat(_vitals.transform.position - from).magnitude / sliceTime);
                        from = _vitals.transform.position;
                        sliceTime = 0.0f;
                    }
                }
                float went = Flat(_vitals.transform.position - swungFrom).magnitude;
                Debug.Log($"CityX swing on the move ({expected.Animation}): going at {pace:0.00} m/s; through a swing of {swung:0.00} s never slower than {slowest:0.00} m/s, " +
                          $"and {went:0.00} m covered ({pace * swung:0.00} m at that pace)");
                Assert.AreEqual(_combat.Light2.Animation, _combat.CurrentAttack.Animation, "the attack did not chain into the second light");
                Assert.Greater(swung, expected.Startup + expected.Active + 0.4f, "the swings were over before their blows");
                Assert.Greater(slowest, pace * FullPaceShare, $"the body was slowed to {slowest:0.00} m/s during the swings (it was going at {pace:0.00} m/s)");
                Assert.AreEqual(pace * swung, went, pace * swung * (1.0f - FullPaceShare), "the body did not cover the ground its pace would: the swings slowed it, or pushed it on");
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
