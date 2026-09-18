using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Sticky-feet wall running's own physics, driven through the real
    /// component with a real CharacterController and real colliders --
    /// same technique PlayerClimbingTests already established: sprint while
    /// airborne against a wall grips it, WASD moves freely across the whole
    /// surface (not just one locked axis the way PlayerClimbing's ladder
    /// grip is), stamina drains the whole time stuck, Space hops off with
    /// an outward+upward push, running out of stamina releases without
    /// one, and running off the wall's own edge releases cleanly.
    /// </summary>
    public sealed class WallStickTests
    {
        private GameObject _player;
        private GameObject _wall;
        private WallStick _wallStick;
        private PlayerVitals _vitals;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _vitals = _player.AddComponent<PlayerVitals>();
            _wallStick = _player.AddComponent<WallStick>();
            // EditMode never calls Awake -- see WallStick.Bind's own doc comment.
            _wallStick.Bind();
            // Player at the origin facing +Z (Unity's own default forward
            // for an identity rotation), same 0.7m clearance PlayerClimbingTests
            // uses -- well within GrabCheckDistance (1.0) but not touching.
            _player.transform.position = Vector3.zero;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            if (_wall != null) Object.DestroyImmediate(_wall);
        }

        private void MakeWall(float width, float height, float nearFaceZ)
        {
            _wall = new GameObject("Wall");
            _wall.transform.position = new Vector3(0.0f, height * 0.5f, nearFaceZ + 0.5f);
            _wall.AddComponent<BoxCollider>().size = new Vector3(width, height, 1.0f);
        }

        [Test]
        public void SprintAndAirborneAgainstAWallSticks()
        {
            MakeWall(10.0f, 30.0f, 0.2f);
            Physics.SyncTransforms();

            Assert.IsFalse(_wallStick.TryStart(sprintHeld: false, grounded: false, stamina: 100.0f),
                "airborne alone started a stick");
            Assert.IsFalse(_wallStick.TryStart(sprintHeld: true, grounded: true, stamina: 100.0f),
                "sprint while grounded started a stick");
            Assert.IsTrue(_wallStick.TryStart(sprintHeld: true, grounded: false, stamina: 100.0f),
                "sprinting airborne against a real wall didn't stick");
            Assert.IsTrue(_wallStick.IsSticking);
        }

        [Test]
        public void NoStaminaMeansNoStick()
        {
            MakeWall(10.0f, 30.0f, 0.2f);
            Physics.SyncTransforms();

            Assert.IsFalse(_wallStick.TryStart(sprintHeld: true, grounded: false, stamina: 0.0f),
                "stuck with zero stamina");
        }

        [Test]
        public void MovingUpTheWallRaisesHeightAndDrainsStaminaWhileStayingGlued()
        {
            MakeWall(10.0f, 30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_wallStick.TryStart(true, false, _vitals.Stamina));

            float staminaBefore = _vitals.Stamina;
            float yBefore = _player.transform.position.y;

            for (int i = 0; i < 30; i++)
            {
                _wallStick.Tick(jumpPressed: false, moveInput: new Vector2(0.0f, 1.0f), _vitals, 1.0f / 60.0f);
            }

            Assert.Greater(_player.transform.position.y, yBefore + 0.5f,
                "moving up the wall didn't meaningfully raise the player's own height");

            float expectedDrain = PlayerVitals.StaminaDrainPerSecond * (30.0f / 60.0f);
            Assert.Greater(staminaBefore - _vitals.Stamina, expectedDrain * 0.5f,
                "sticking didn't drain stamina");

            Assert.AreEqual(0.0f, _player.transform.position.x, 0.5f,
                "moving straight up drifted sideways off the wall");
            Assert.Greater(_player.transform.position.z, -0.5f,
                "the player floated off the wall's own cling distance");
            Assert.Less(_player.transform.position.z, 1.0f,
                "the player sank into the wall instead of staying glued to its surface");
        }

        [Test]
        public void MovingSidewaysTraversesTheWallFreely()
        {
            MakeWall(10.0f, 30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_wallStick.TryStart(true, false, 100.0f));

            for (int i = 0; i < 30; i++)
            {
                _wallStick.Tick(jumpPressed: false, moveInput: new Vector2(1.0f, 0.0f), _vitals, 1.0f / 60.0f);
            }

            Assert.IsTrue(_wallStick.IsSticking, "fell off partway across a wide wall");
            Assert.Greater(_player.transform.position.x, 0.5f,
                "moving sideways didn't meaningfully traverse the wall -- PlayerClimbing's own locked " +
                "axis would fail this, which is the whole point of this ability existing");
        }

        [Test]
        public void HoppingOffEndsTheStickWithAnOutwardUpwardPush()
        {
            MakeWall(10.0f, 30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_wallStick.TryStart(true, false, 100.0f));

            _wallStick.Tick(jumpPressed: true, moveInput: Vector2.zero, _vitals, 1.0f / 60.0f);

            Assert.IsFalse(_wallStick.IsSticking, "hopping off didn't clear IsSticking");
            Assert.Greater(_wallStick.LastEndVelocity.y, 0.5f, "hopping off gave no upward push");
            // The wall faces -Z back at the player (stuck from z=0 against a
            // wall whose near face is in front, at +Z) -- the outward push
            // is along the wall's own normal, back toward the player.
            Assert.Less(_wallStick.LastEndVelocity.z, -0.5f, "hopping off gave no outward push");
        }

        [Test]
        public void RunningOutOfStaminaReleasesTheGripWithoutAPush()
        {
            MakeWall(10.0f, 30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_wallStick.TryStart(true, false, 100.0f));

            // About to run out -- spent down through the public API rather
            // than reaching into PlayerVitals' own private field, matching
            // this project's own "drive it through the real component"
            // testability convention (see PlayerClimbingTests' own
            // identical use of TrySpendStamina).
            _vitals.TrySpendStamina(_vitals.Stamina - 0.05f);

            _wallStick.Tick(false, Vector2.zero, _vitals, 1.0f / 60.0f);

            Assert.IsFalse(_wallStick.IsSticking, "player kept sticking after stamina hit 0");
            Assert.AreEqual(Vector3.zero, _wallStick.LastEndVelocity,
                "a stamina-exhausted release incorrectly applied a hop-off-style push");
        }

        [Test]
        public void RunningOffTheWallsEdgeReleasesCleanly()
        {
            // Narrow wall -- moving sideways for long enough runs straight
            // off its edge, unlike the wide wall the other tests use.
            MakeWall(2.0f, 30.0f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(_wallStick.TryStart(true, false, 100.0f));

            bool ranOffTheEdge = false;
            for (int i = 0; i < 120; i++)
            {
                _wallStick.Tick(false, new Vector2(1.0f, 0.0f), _vitals, 1.0f / 60.0f);
                if (!_wallStick.IsSticking)
                {
                    ranOffTheEdge = true;
                    break;
                }
            }

            Assert.IsTrue(ranOffTheEdge, "never ran off the edge of a 2m-wide wall");
            Assert.AreEqual(Vector3.zero, _wallStick.LastEndVelocity,
                "running off the wall's own edge applied a hop-off-style push");
        }
    }
}
