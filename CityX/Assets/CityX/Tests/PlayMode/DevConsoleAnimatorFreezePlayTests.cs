using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Requested, after seven rounds of the console/grapple distortion
    /// saga (see grapple_swing_deprioritized memory): "remove the console
    /// function and rebuild it entirely if you have to, just get rid of
    /// it." DevConsoleView.Open() used to disable the player's Animator to
    /// stop whatever animation was already playing from looping while the
    /// console stayed open -- every attempt to make that freeze SAFE
    /// (forced resyncs, rate-based snap suppression, ownership checks, an
    /// execution-order fix) carried the same structural risk: Mecanim can
    /// occasionally produce an implausible one-frame pose during an
    /// ordinary blend-tree transition, and freezing on that exact frame
    /// makes an otherwise invisible, self-correcting glitch permanent for
    /// as long as the console stays open. Open() no longer disables the
    /// Animator at all -- see its own doc comment. These tests now assert
    /// that directly, across the same real, changing-movement scenarios
    /// this saga's history already established as the ones that matter.
    /// </summary>
    public sealed class DevConsoleAnimatorFreezePlayTests
    {
        private GameObject _player;
        private GameObject _consoleObject;
        private GameObject _ground;
        private GrappleSwing _swing;

        private const float StepSeconds = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 1.0f / SteadyFrameTime.FramesPerSecond;   // back to the run's steady frame time, not off
            // Open() now pauses real time when no NetworkBootstrap is wired
            // (treated as Singleplayer -- see its own doc comment), and
            // every test below opens a bare console with none wired. Reset
            // here or every PlayMode test that runs after this one in the
            // same session would silently run at zero speed.
            Time.timeScale = 1.0f;
            if (_player != null) Object.Destroy(_player);
            if (_consoleObject != null) Object.Destroy(_consoleObject);
            if (_ground != null) Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator OpeningTheConsoleDuringChangingLiveMovementNeverDisablesTheAnimator()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "TestGround";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 2.0f, 0.0f), Quaternion.identity);
            Animator animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Player.prefab has no Animator");
            PlayerVitals vitals = _player.GetComponent<PlayerVitals>();
            vitals.Revive();
            PlayerLocomotion locomotion = _player.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(locomotion, "Player.prefab has no PlayerLocomotion");
            locomotion.Bind();

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Bind();

            Time.captureDeltaTime = StepSeconds;

            var rng = new System.Random(20260910);

            for (int i = 0; i < 120 && !locomotion.IsGrounded; i++)
            {
                locomotion.Tick(default, StepSeconds);
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- never actually landed on the test ground");

            for (int cycle = 0; cycle < 40; cycle++)
            {
                // A handful of frames of real, varied, occasionally abrupt
                // input right before opening -- direction reversals and
                // jumps force the locomotion blend tree through a real
                // transition, landing Open at a different point in it
                // every cycle. The claim under test does not depend on
                // WHERE in a transition Open lands, precisely because it
                // no longer reads or reacts to the Animator's pose at all.
                int framesBeforeOpening = 2 + (cycle % 9);
                for (int i = 0; i < framesBeforeOpening; i++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float magnitude = (float)rng.NextDouble();
                    var input = new LocomotionInput
                    {
                        Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude,
                        YawDegrees = (float)(rng.NextDouble() * 360.0),
                        Sprint = rng.NextDouble() < 0.3,
                        Crouch = rng.NextDouble() < 0.15,
                        Aiming = rng.NextDouble() < 0.2,
                        Jump = rng.NextDouble() < 0.2,
                    };
                    locomotion.Tick(input, StepSeconds);
                    yield return null;
                }

                view.Open();
                Assert.IsTrue(animator.enabled,
                    $"opening the console (cycle {cycle}) disabled the animator -- it must never do this any more");
                yield return null;

                view.Close();
                Assert.IsTrue(animator.enabled,
                    $"closing the console (cycle {cycle}) left the animator disabled");
                yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// GrappleSwing was rebuilt from square one after eight rounds of
        /// live pose-distortion bugs traced to its old bone-owning pose
        /// (see GrappleSwing's own class doc comment) -- it never disables
        /// the Animator any more, at any point, so opening/closing the
        /// console mid-swing has nothing left to fight over. The rope
        /// itself stays attached throughout (nothing about the console
        /// touches GrappleSwing's own state), matching real play: the
        /// console only disables InputReader, which stops anything from
        /// calling Swing.Tick, not the swing itself.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningTheConsoleMidSwingNeverDisablesTheAnimatorOrTheSwing()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 20.0f, 0.0f), Quaternion.identity);
            Animator animator = _player.GetComponentInChildren<Animator>();
            PlayerVitals vitals = _player.GetComponent<PlayerVitals>();
            vitals.Revive();
            PlayerLocomotion locomotion = _player.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(locomotion, "Player.prefab has no PlayerLocomotion");
            locomotion.Bind();

            _swing = _player.AddComponent<GrappleSwing>();
            _swing.Bind();

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Swing = _swing;
            view.Bind();

            for (int i = 0; i < 10; i++)
            {
                locomotion.Tick(default, StepSeconds);
                yield return null;
            }

            _swing.Attach(_player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));

            for (int i = 0; i < 60; i++)
            {
                _swing.Tick(new Vector2(0.3f, 0.6f), i * 2.0f, false, false, false, false, StepSeconds);
                yield return null;
            }
            Assert.IsTrue(_swing.IsActive, "setup failed -- swing self-released before the console could open");
            Assert.IsTrue(animator.enabled, "setup failed -- swinging must never disable the animator any more");

            view.Open();

            Assert.IsTrue(animator.enabled, "opening the console mid-swing disabled the animator");
            Assert.IsTrue(_swing.IsActive, "opening the console mid-swing released the rope");

            view.Close();
            yield return null;

            Assert.IsTrue(animator.enabled, "closing the console mid-swing disabled the animator");
            Assert.IsTrue(_swing.IsActive, "closing the console mid-swing released the rope");
#else
            yield break;
#endif
        }
    }
}
