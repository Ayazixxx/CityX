using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Wall climbing's own physics, driven through the real component with
    /// a real CharacterController and real colliders -- same technique
    /// GrappleSwingTests already established, and ported from
    /// tests/test_climbing_check.gd: sprint+W against a wall grabs on, W/S
    /// climb up/down once attached, stamina drains at the sprint rate,
    /// Space hops off with an outward+upward push, running out of stamina
    /// releases without one, climbing down onto solid ground releases
    /// cleanly, and reaching the top of a short wall tops out and relocates
    /// near the ledge.
    ///
    /// NO ANIMATOR ON THESE FIXTURES, DELIBERATELY -- PlayerClimbing.BeginTopOut's
    /// own null-animator fallback (skip the one-shot clip, complete the
    /// mantle immediately) is what makes the top-out relocation math
    /// testable here at all without a live Play Mode Animator actually
    /// advancing a state's normalizedTime; PlayerVoiceCapturePlayTests'
    /// own class doc comment documents the same kind of tolerant fallback
    /// existing specifically so a headless test runner has something to
    /// exercise.
    /// </summary>
    public sealed class PlayerClimbingTests
    {
        private GameObject _player;
        private GameObject _wall;
        private GameObject _ground;
        private PlayerClimbing _climbing;
        private PlayerVitals _vitals;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _vitals = _player.AddComponent<PlayerVitals>();
            _climbing = _player.AddComponent<PlayerClimbing>();
            // EditMode never calls Awake -- see PlayerClimbing.Bind's own doc comment.
            _climbing.Bind();
            // Player at the origin facing +Z (Unity's own default forward
            // for an identity rotation), matching test_climbing_check.gd's
            // own "0.7m clear of the wall's own near face, well within
            // CLIMB_GRAB_CHECK_DISTANCE (1.0) but not touching."
            _player.transform.position = Vector3.zero;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            if (_wall != null) Object.DestroyImmediate(_wall);
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        private void MakeWall(float height, float nearFaceZ)
        {
            _wall = new GameObject("Wall");
            _wall.transform.position = new Vector3(0.0f, height * 0.5f, nearFaceZ + 0.5f);
            _wall.AddComponent<BoxCollider>().size = new Vector3(10.0f, height, 1.0f);
        }

        private void MakeGround()
        {
            _ground = new GameObject("Ground");
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.AddComponent<BoxCollider>().size = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [Test]
        public void HoldingTheGrabKeyAndForwardAgainstAWallGrabsOn()
        {
            MakeWall(30.0f, 0.2f);
            Physics.SyncTransforms();

            Assert.IsFalse(_climbing.TryStart(new GrabIntent(), forwardHeld: true, stamina: 100.0f),
                "a wall next to a running player started a climb without anyone asking for one");
            Assert.IsFalse(_climbing.TryStart(GrabIntent.AlreadyHeld(), forwardHeld: false, stamina: 100.0f),
                "holding the grab key without pushing toward the wall started a climb");
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), forwardHeld: true, stamina: 100.0f),
                "holding the grab key and W against a real wall didn't start a climb");
            Assert.IsTrue(_climbing.IsClimbing);
        }

        [Test]
        public void NoStaminaMeansNoGrab()
        {
            MakeWall(30.0f, 0.2f);
            Physics.SyncTransforms();

            Assert.IsFalse(_climbing.TryStart(GrabIntent.AlreadyHeld(), forwardHeld: true, stamina: 0.0f),
                "grabbed on with zero stamina");
        }

        [Test]
        public void ClimbingUpRaisesHeightAndDrainsStaminaWhileStayingGlued()
        {
            MakeWall(30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), true, _vitals.Stamina));

            float staminaBefore = _vitals.Stamina;
            float yBefore = _player.transform.position.y;

            for (int i = 0; i < 30; i++)
            {
                _climbing.Tick(jumpPressed: false, climbUpHeld: true, climbDownHeld: false, _vitals, 1.0f / 60.0f);
            }

            Assert.Greater(_player.transform.position.y, yBefore + 0.5f,
                "climbing up didn't meaningfully raise the player's own height");

            float expectedDrain = PlayerVitals.StaminaDrainPerSecond * (30.0f / 60.0f);
            Assert.Greater(staminaBefore - _vitals.Stamina, expectedDrain * 0.5f,
                "climbing didn't drain stamina");

            Assert.AreEqual(0.0f, _player.transform.position.x, 0.01f,
                "player drifted sideways off the wall's own grab spot");
        }

        [Test]
        public void HoppingOffEndsTheClimbWithAnOutwardUpwardPush()
        {
            MakeWall(30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            _climbing.Tick(jumpPressed: true, climbUpHeld: false, climbDownHeld: false, _vitals, 1.0f / 60.0f);

            Assert.IsFalse(_climbing.IsClimbing, "hopping off didn't clear IsClimbing");
            Assert.Greater(_climbing.LastEndVelocity.y, 0.5f, "hopping off gave no upward push");
            // The wall faces -Z back at the player (grabbed from z=0 against
            // a wall whose near face is in front, at +Z) -- the outward push
            // is along the wall's own normal, back toward the player.
            Assert.Less(_climbing.LastEndVelocity.z, -0.5f, "hopping off gave no outward push");
        }

        [Test]
        public void ClimbingDownToTheGroundReleasesCleanly()
        {
            MakeWall(30.0f, 0.2f);
            MakeGround();
            Physics.SyncTransforms();
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            // Climb up a bit first so there's real height to descend from.
            for (int i = 0; i < 20; i++)
            {
                _climbing.Tick(false, true, false, _vitals, 1.0f / 60.0f);
            }
            Assert.IsTrue(_climbing.IsClimbing, "setup failed -- lost the grip while climbing up");

            bool reachedGround = false;
            for (int i = 0; i < 300; i++)
            {
                _climbing.Tick(false, false, true, _vitals, 1.0f / 60.0f);
                if (!_climbing.IsClimbing)
                {
                    reachedGround = true;
                    break;
                }
            }

            Assert.IsTrue(reachedGround, "climbing down never reached the ground");
            Assert.AreEqual(Vector3.zero, _climbing.LastEndVelocity,
                "climbing down to the ground applied a hop-off-style push");
        }

        [Test]
        public void RunningOutOfStaminaReleasesTheGripWithoutAPush()
        {
            MakeWall(30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            // About to run out -- spent down through the public API rather
            // than reaching into PlayerVitals' own private field, matching
            // this project's own "drive it through the real component"
            // testability convention.
            _vitals.TrySpendStamina(_vitals.Stamina - 0.05f);

            _climbing.Tick(false, true, false, _vitals, 1.0f / 60.0f);   // climbing UP: moving, so it costs stamina

            Assert.IsFalse(_climbing.IsClimbing, "player kept climbing after stamina hit 0");
            Assert.AreEqual(Vector3.zero, _climbing.LastEndVelocity,
                "a stamina-exhausted release incorrectly applied a hop-off-style push");
        }

        [Test]
        public void HangingStillCostsNoStaminaAndMovingDoes()
        {
            MakeWall(30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
            _vitals.TrySpendStamina(20.0f);
            float before = _vitals.Stamina;
            for (int i = 0; i < 60; i++) _climbing.Tick(false, false, false, _vitals, 1.0f / 60.0f);   // hang for a second
            Assert.AreEqual(before, _vitals.Stamina, 0.001f, "hanging still drained (or regenerated) stamina");
            for (int i = 0; i < 60; i++) _climbing.Tick(false, true, false, _vitals, 1.0f / 60.0f);    // climb for a second
            Assert.Less(_vitals.Stamina, before - 1.0f, "climbing did not cost stamina");
        }

        [Test]
        public void ReachingTheTopOfAShortWallMantlesOntoTheLedge()
        {
            const float wallHeight = 4.0f;
            MakeWall(wallHeight, 0.2f);
            MakeGround();
            Physics.SyncTransforms();
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            bool toppedOut = false;
            for (int i = 0; i < 200; i++)
            {
                _climbing.Tick(false, true, false, _vitals, 1.0f / 60.0f);
                // With no Animator on this fixture, BeginTopOut's own
                // null-animator fallback completes the mantle immediately --
                // so by the time IsClimbing goes false without a push having
                // been recorded, topping out (not a stamina/ground release)
                // is what happened.
                if (!_climbing.IsClimbing)
                {
                    toppedOut = true;
                    break;
                }
            }

            Assert.IsTrue(toppedOut, "never reached the top of the short wall");
            Assert.IsFalse(_climbing.IsToppingOut, "topping out with no Animator should complete immediately");
            Assert.Greater(_player.transform.position.y, wallHeight - 1.0f,
                "the player wasn't relocated near the top of the wall");
        }
    }
}
