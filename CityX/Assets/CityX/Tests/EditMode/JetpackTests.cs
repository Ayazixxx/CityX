using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The jetpack's own two-piece mechanic, driven through the real
    /// component with a real CharacterController underneath -- same
    /// technique GrappleSwingTests/PlayerClimbingTests already establish
    /// for testing movement without a scene: thrust sets a real upward
    /// velocity PlayerLocomotion.Tick actually applies, and thrusting
    /// continuously refreshes PlayerVitals' own fall-tracking peak so that
    /// letting go only ever measures the genuine unpowered fall afterward,
    /// never whatever height the jetpack climbed to.
    ///
    /// NO ANIMATOR ON THIS FIXTURE, DELIBERATELY -- Jetpack.OnEquip's own
    /// spine lookup tolerates a missing Animator (skips the visual attach,
    /// same shape PlayerClimbingTests' own no-Animator top-out fallback
    /// already relies on), which is what makes the underlying mechanic
    /// testable here at all without a real rig.
    /// </summary>
    public sealed class JetpackTests
    {
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private GameObject _jetpackObject;
        private Jetpack _jetpack;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _locomotion = _player.AddComponent<PlayerLocomotion>();
            _vitals = _player.AddComponent<PlayerVitals>();
            _locomotion.Bind();

            _jetpackObject = new GameObject("Jetpack");
            _jetpack = _jetpackObject.AddComponent<Jetpack>();
            _jetpack.OnEquip(_player, ItemInstanceData.None);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_jetpackObject);
        }

        [Test]
        public void ThrustingLiftsTheBody()
        {
            float yBefore = _player.transform.position.y;
            for (int i = 0; i < 10; i++)
            {
                _jetpack.Tick(thrustHeld: true, deltaTime: 1.0f / 60.0f);
                _locomotion.Tick(default, 1.0f / 60.0f);
            }

            Assert.Greater(_player.transform.position.y, yBefore,
                "holding thrust didn't actually raise the body");
        }

        [Test]
        public void LettingGoResumesOrdinaryGravity()
        {
            for (int i = 0; i < 10; i++)
            {
                _jetpack.Tick(true, 1.0f / 60.0f);
                _locomotion.Tick(default, 1.0f / 60.0f);
            }
            Assert.Greater(_locomotion.Velocity.y, 0.0f, "setup failed -- thrust didn't leave the body rising");

            // NOT a position comparison -- a released body launched upward
            // at real speed keeps climbing on its own momentum for over a
            // second at this rig's gravity before it would fall back below
            // where it let go (confirmed directly: still 3.8m up, off a
            // 3.9m peak, a full 50 ticks after release), so "is it now
            // BELOW where it was" is the wrong question on any timescale
            // this test can afford. "Is gravity now winning" is the actual
            // claim, and velocity says that directly: unpowered, it must
            // go negative once thrust stops decelerating it.
            for (int i = 0; i < 60; i++)
            {
                _jetpack.Tick(false, 1.0f / 60.0f);
                _locomotion.Tick(default, 1.0f / 60.0f);
            }

            Assert.Less(_locomotion.Velocity.y, 0.0f,
                "letting go of thrust never let gravity actually reverse the climb");
        }

        /// <summary>
        /// The whole point of the explicit ask ("it negates fall damage
        /// from the last height it was on"): a huge climb under thrust,
        /// released only a few metres above the ground, must land as a safe
        /// drop -- not one measured all the way back to the thrust's own
        /// peak height.
        /// </summary>
        [Test]
        public void ReleasingCloseToTheGroundAfterAHighClimbTakesNoFallDamage()
        {
            float healthBefore = _vitals.Health;

            // A long climb under thrust -- each tick refreshes the
            // fall-tracking peak to wherever the player currently is,
            // exactly as Jetpack.Tick's own real path does every frame
            // thrust is held.
            _player.transform.position = new Vector3(0.0f, 100.5f, 0.0f);
            _jetpack.Tick(true, 1.0f / 60.0f);

            // Still thrusting, now descended under control to just above
            // the ground -- this is the LAST reset before letting go, and
            // it is what "release close to the ground" actually means: the
            // 100m climb behind it is irrelevant, only what happens from
            // here on is real unpowered fall.
            _player.transform.position = new Vector3(0.0f, 2.0f, 0.0f);
            _jetpack.Tick(true, 1.0f / 60.0f);

            // Thrust released -- a genuine 2m drop to the ground, well
            // inside any reasonable safe-fall distance.
            _vitals.TickFallDamage(grounded: false, currentY: 2.0f);
            float damage = _vitals.TickFallDamage(grounded: true, currentY: 0.0f);

            Assert.AreEqual(0.0f, damage, "a safe landing after releasing thrust still took fall damage");
            Assert.AreEqual(healthBefore, _vitals.Health,
                "health dropped even though the landing should have been safe");
        }

        [Test]
        public void WithoutResettingFallTrackingTheSameClimbWouldHaveHurt()
        {
            // The control case, proving the test above isn't accidentally
            // safe on its own merits: the exact same climb and landing,
            // but tracked the ordinary way (no resets), takes real damage --
            // confirming ResetFallTracking is what makes the difference.
            _vitals.TickFallDamage(grounded: false, currentY: 0.5f);
            _vitals.TickFallDamage(grounded: false, currentY: 100.5f);
            float damage = _vitals.TickFallDamage(grounded: true, currentY: 0.0f);

            Assert.Greater(damage, 0.0f,
                "a 100m drop with no fall-tracking reset should have hurt -- the control case is broken");
        }
    }
}
