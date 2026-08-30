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
                _jetpack.Tick(thrustHeld: true, moveInput: Vector2.zero, boostHeld: false, deltaTime: 1.0f / 60.0f);
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
                _jetpack.Tick(true, Vector2.zero, false, 1.0f / 60.0f);
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
                _jetpack.Tick(false, Vector2.zero, false, 1.0f / 60.0f);
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
            _jetpack.Tick(true, Vector2.zero, false, 1.0f / 60.0f);

            // Still thrusting, now descended under control to just above
            // the ground -- this is the LAST reset before letting go, and
            // it is what "release close to the ground" actually means: the
            // 100m climb behind it is irrelevant, only what happens from
            // here on is real unpowered fall.
            _player.transform.position = new Vector3(0.0f, 2.0f, 0.0f);
            _jetpack.Tick(true, Vector2.zero, false, 1.0f / 60.0f);

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

        /// <summary>
        /// TiltTarget/EaseTilt are pure and static specifically so the
        /// lean's own direction and magnitude are checkable without a real
        /// Animator -- both signs below were confirmed against Unity's own
        /// rotation convention with a scratch script before being pinned
        /// here (see TiltTarget's own doc comment), not just assumed from
        /// how the numbers read.
        /// </summary>
        [Test]
        public void TiltTargetLeansForwardOnW()
        {
            Assert.Greater(Jetpack.TiltTarget(new Vector2(0.0f, 1.0f)).x, 0.0f,
                "pressing W should pitch the body forward, not back or not at all");
        }

        [Test]
        public void TiltTargetLeansBackOnS()
        {
            Assert.Less(Jetpack.TiltTarget(new Vector2(0.0f, -1.0f)).x, 0.0f,
                "pressing S should pitch the body back");
        }

        [Test]
        public void TiltTargetBanksIntoARightStrafe()
        {
            Assert.Less(Jetpack.TiltTarget(new Vector2(1.0f, 0.0f)).y, 0.0f,
                "pressing D should bank the body's right side down, into the strafe");
        }

        [Test]
        public void TiltTargetBanksIntoALeftStrafe()
        {
            Assert.Greater(Jetpack.TiltTarget(new Vector2(-1.0f, 0.0f)).y, 0.0f,
                "pressing A should bank the body's left side down, into the strafe");
        }

        [Test]
        public void TiltTargetIsStraightWithNoInput()
        {
            Assert.AreEqual(Vector2.zero, Jetpack.TiltTarget(Vector2.zero));
        }

        [Test]
        public void TiltTargetScalesWithHowHardTheStickIsPushed()
        {
            // A half-deflected input should not lean as far as a full one --
            // otherwise a gentle nudge and a full press would look identical.
            Vector2 half = Jetpack.TiltTarget(new Vector2(0.0f, 0.5f));
            Vector2 full = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f));
            Assert.Less(half.x, full.x);
            Assert.Greater(half.x, 0.0f);
        }

        [Test]
        public void EaseTiltDoesNotJumpStraightToItsTarget()
        {
            // One ordinary frame at the full easing rate covers only a
            // fraction of the max lean -- an instant snap here would be the
            // exact "teleport to speed" problem PlayerLocomotion's own
            // HorizontalAcceleration was written to avoid, just for tilt
            // instead of movement.
            Vector2 target = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f));
            Vector2 velocity = Vector2.zero;
            Vector2 eased = Jetpack.EaseTilt(Vector2.zero, target, ref velocity, 1.0f / 60.0f);
            Assert.Greater(eased.x, 0.0f);
            Assert.Less(eased.x, target.x);
        }

        [Test]
        public void EaseTiltReachesItsTargetGivenEnoughTime()
        {
            Vector2 target = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f));
            Vector2 tilt = Vector2.zero;
            Vector2 velocity = Vector2.zero;
            // SmoothDamp settles more gradually than the old constant-rate
            // ramp did (see TiltSmoothTime's own doc comment) -- 30 frames
            // (half a second) covered the old rate's own full lean easily,
            // but is not long enough for a curve that eases OUT approaching
            // its target too; this loop runs long enough to actually settle.
            for (int i = 0; i < 180; i++)
            {
                tilt = Jetpack.EaseTilt(tilt, target, ref velocity, 1.0f / 60.0f);
            }
            Assert.AreEqual(target.x, tilt.x, 0.01f);
        }

        /// <summary>
        /// Letting go of W (or D) drops that axis's OWN target back to
        /// zero, straightening the body -- the explicit "when you let go of
        /// one of those keys it goes back to straight" ask.
        /// </summary>
        [Test]
        public void EaseTiltStraightensOutOnceTheKeyIsReleased()
        {
            Vector2 held = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f));
            Vector2 tilt = Vector2.zero;
            Vector2 velocity = Vector2.zero;
            for (int i = 0; i < 180; i++)
            {
                tilt = Jetpack.EaseTilt(tilt, held, ref velocity, 1.0f / 60.0f);
            }
            Assert.Greater(tilt.x, 1.0f, "setup failed -- the body never actually leaned");

            Vector2 released = Jetpack.TiltTarget(Vector2.zero);
            for (int i = 0; i < 180; i++)
            {
                tilt = Jetpack.EaseTilt(tilt, released, ref velocity, 1.0f / 60.0f);
            }
            Assert.AreEqual(0.0f, tilt.x, 0.01f, "letting go of W never straightened the body back out");
        }

        /// <summary>
        /// The actual bug report this exists to catch: a constant-rate ramp
        /// arrives at its target at full speed and stops dead on the very
        /// next frame -- a real discontinuity, which is what "jumps into
        /// place" describes. A SmoothDamp curve decelerates as it
        /// approaches, so the last stretch before settling should cover
        /// LESS ground per frame than an earlier stretch further from the
        /// target, not the same fixed amount every frame the way
        /// MoveTowards would.
        /// </summary>
        [Test]
        public void EaseTiltDeceleratesApproachingItsTargetRatherThanArrivingAtFullSpeed()
        {
            Vector2 target = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f));
            Vector2 tilt = Vector2.zero;
            Vector2 velocity = Vector2.zero;

            float midDelta = 0.0f;
            for (int i = 0; i < 180; i++)
            {
                float before = tilt.x;
                tilt = Jetpack.EaseTilt(tilt, target, ref velocity, 1.0f / 60.0f);
                if (i == 10)
                {
                    midDelta = tilt.x - before;
                }
            }
            float before2 = tilt.x;
            tilt = Jetpack.EaseTilt(tilt, target, ref velocity, 1.0f / 60.0f);
            float lateDelta = tilt.x - before2;

            Assert.Less(lateDelta, midDelta,
                "the lean is still moving at the same rate right up to the target -- that reads as snapping into place");
        }

        /// <summary>
        /// "shift is a boost when midair using jetpack" -- holding a
        /// direction spends the boost on a further lean, checked directly
        /// against TiltTarget's own two constants rather than just "greater
        /// than the plain lean" so a future TiltMaxDegrees tweak can't
        /// silently satisfy this without BoostTiltMaxDegrees actually being
        /// the one in effect.
        /// </summary>
        [Test]
        public void TiltTargetAmplifiesWhenBoosting()
        {
            Vector2 boosted = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f), boosting: true);
            Assert.AreEqual(Jetpack.BoostTiltMaxDegrees, boosted.x, 0.001f);
        }

        [Test]
        public void TiltTargetUsesThePlainMaxWhenNotBoosting()
        {
            Vector2 plain = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f), boosting: false);
            Assert.AreEqual(Jetpack.TiltMaxDegrees, plain.x, 0.001f);
        }

        /// <summary>
        /// Boosting with nothing to lean toward must not manufacture a lean
        /// out of thin air -- see Jetpack.Tick's own "spends on either lean
        /// or climb, never both" doc comment.
        /// </summary>
        [Test]
        public void TiltTargetIgnoresBoostingWithNoDirectionHeld()
        {
            Assert.AreEqual(Vector2.zero, Jetpack.TiltTarget(Vector2.zero, boosting: true));
        }

        /// <summary>
        /// EXPLICIT CORRECTION: the lean used to follow WASD alone, so
        /// air-steering a fall with the jetpack equipped but NOT thrusting
        /// still leaned the body as if flying. Holding a direction with
        /// thrust released must stay level instead -- the model should read
        /// as idle/falling, not banked.
        /// </summary>
        [Test]
        public void HoldingADirectionWithoutThrustNeverLeansTheBody()
        {
            for (int i = 0; i < 30; i++)
            {
                _jetpack.Tick(thrustHeld: false, moveInput: new Vector2(0.0f, 1.0f), boostHeld: false,
                    deltaTime: 1.0f / 60.0f);
            }

            Assert.AreEqual(Vector2.zero, _jetpack.CurrentTilt,
                "the body leaned even though thrust was never held");
        }

        /// <summary>
        /// The control case, proving the test above isn't accidentally
        /// passing because leaning is broken outright -- the exact same
        /// held direction, but WITH thrust, must still lean.
        /// </summary>
        [Test]
        public void HoldingADirectionWithThrustStillLeansTheBody()
        {
            for (int i = 0; i < 30; i++)
            {
                _jetpack.Tick(thrustHeld: true, moveInput: new Vector2(0.0f, 1.0f), boostHeld: false,
                    deltaTime: 1.0f / 60.0f);
            }

            Assert.Greater(_jetpack.CurrentTilt.x, 1.0f,
                "the body never leaned even with thrust held -- the control case is broken");
        }

        /// <summary>
        /// Releasing thrust mid-lean must ease back to level, the same way
        /// releasing a single WASD key already eases that axis back to zero
        /// (see EaseTiltStraightensOutOnceTheKeyIsReleased) -- thrust ending
        /// is just another way the target becomes zero.
        /// </summary>
        [Test]
        public void ReleasingThrustStraightensOutAnExistingLean()
        {
            for (int i = 0; i < 30; i++)
            {
                _jetpack.Tick(thrustHeld: true, moveInput: new Vector2(0.0f, 1.0f), boostHeld: false,
                    deltaTime: 1.0f / 60.0f);
            }
            Assert.Greater(_jetpack.CurrentTilt.x, 1.0f, "setup failed -- the body never actually leaned");

            // SmoothDamp settles more gradually than the old constant-rate
            // ramp did (see TiltSmoothTime's own doc comment) -- 30 frames
            // (half a second) was plenty against the old rate, but is not
            // long enough for a curve that also eases OUT approaching zero.
            for (int i = 0; i < 180; i++)
            {
                _jetpack.Tick(thrustHeld: false, moveInput: new Vector2(0.0f, 1.0f), boostHeld: false,
                    deltaTime: 1.0f / 60.0f);
            }
            Assert.AreEqual(0.0f, _jetpack.CurrentTilt.x, 0.01f,
                "releasing thrust never straightened the body back out");
        }

        [Test]
        public void BoostWithNoDirectionClimbsFasterThanPlainThrust()
        {
            _jetpack.Tick(true, Vector2.zero, boostHeld: true, deltaTime: 1.0f / 60.0f);
            _locomotion.Tick(default, 1.0f / 60.0f);

            Assert.Greater(_locomotion.Velocity.y, Jetpack.ThrustSpeed,
                "boosting straight up should climb faster than plain thrust");
        }

        /// <summary>
        /// The explicit ask reads as an either/or ("if not holding wasd
        /// keys then just make it go up faster") -- holding a direction
        /// spends the boost on the amplified lean instead, so the climb
        /// speed here must stay at the ordinary ThrustSpeed rather than
        /// also picking up BoostThrustSpeed on top of it.
        /// </summary>
        [Test]
        public void BoostWithADirectionHeldDoesNotAlsoClimbFaster()
        {
            _jetpack.Tick(true, new Vector2(0.0f, 1.0f), boostHeld: true, deltaTime: 1.0f / 60.0f);
            _locomotion.Tick(default, 1.0f / 60.0f);

            Assert.LessOrEqual(_locomotion.Velocity.y, Jetpack.ThrustSpeed,
                "boosting while holding a direction should spend the boost on lean, not extra climb");
        }
    }
}
