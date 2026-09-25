using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, after extensive investigation
    /// ([[spawn_clip_investigation_2026_09_04]] -- see that memory) could
    /// not pin a single reproducible cause on any of four different
    /// real-flow tests, all of which settled the real player onto real
    /// ground cleanly: "make me fucking spawn above ground its still
    /// happening." This is the safety net PlayerLocomotion.Tick now runs
    /// every frame regardless of the diagnosis: whatever the real cause
    /// turns out to be, a body found comfortably below anywhere real ground
    /// could ever legitimately be gets rescued back onto it.
    /// </summary>
    public sealed class PlayerFallThroughWorldRecoveryTests
    {
        private const int Seed = 20260810;
        private GameObject _object;
        private PlayerLocomotion _locomotion;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Body");
            _object.AddComponent<CharacterController>();
            _locomotion = _object.AddComponent<PlayerLocomotion>();
            _locomotion.Bind();
            CurrentWorld.Instance = new CityWorld(Seed);
        }

        [TearDown]
        public void TearDown()
        {
            CurrentWorld.Instance = null;
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void ABodyFarBelowTheWorldIsRescuedBackOntoRealGround()
        {
            _object.transform.position = new Vector3(50.0f, -500.0f, 30.0f);

            _locomotion.Tick(default, 1.0f / 60.0f);

            float realGround = (float)CurrentWorld.Instance.Terrain.ContinuousHeightAt(50.0, 30.0);
            Assert.Greater(_object.transform.position.y, PlayerLocomotion.FallThroughWorldY,
                "the body was not rescued at all -- still below the fall-through floor");
            Assert.AreEqual(realGround + 2.0f, _object.transform.position.y, 0.5f,
                "the body was moved, but not back to the real ground at its own x/z");
        }

        /// <summary>
        /// Ordinary falling -- jumping off a building, a long jetpack dive --
        /// routinely spends real time above FallThroughWorldY but still well
        /// below the body's own last-grounded height. This must never fire
        /// for that, or a normal jump would get silently rewritten mid-air.
        /// </summary>
        [Test]
        public void OrdinaryFallingWithinTheWorldIsLeftAlone()
        {
            float startY = 5.0f;
            _object.transform.position = new Vector3(50.0f, startY, 30.0f);

            for (int i = 0; i < 10; i++)
            {
                _locomotion.Tick(default, 1.0f / 60.0f);
            }

            Assert.Greater(_object.transform.position.y, PlayerLocomotion.FallThroughWorldY,
                "setup sanity check -- ten ordinary falling ticks should not have reached the floor");
        }

        [Test]
        public void WithNoCurrentWorldTheBodyIsLeftWhereItIsRatherThanThrowing()
        {
            CurrentWorld.Instance = null;
            _object.transform.position = new Vector3(0.0f, -500.0f, 0.0f);

            Assert.DoesNotThrow(() => _locomotion.Tick(default, 1.0f / 60.0f));
        }
    }
}
