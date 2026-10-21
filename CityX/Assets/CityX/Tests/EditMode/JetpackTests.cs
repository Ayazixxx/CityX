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
            //
            // GENEROUS BUT BOUNDED, not a fixed 60 ticks -- a fixed budget
            // sized for the old ThrustSpeed (7.0) left too little margin
            // once it became 11.0 (matching the old BoostThrustSpeed, see
            // ThrustSpeed's own doc comment): reversing a faster climb
            // takes proportionally longer, and 60 ticks (1s) was already
            // close to the reversal time at the old speed alone.
            bool reversed = false;
            for (int i = 0; i < 180 && !reversed; i++)
            {
                _jetpack.Tick(false, Vector2.zero, false, 1.0f / 60.0f);
                _locomotion.Tick(default, 1.0f / 60.0f);
                reversed = _locomotion.Velocity.y < 0.0f;
            }

            Assert.IsTrue(reversed,
                "letting go of thrust never let gravity actually reverse the climb");
        }

        /// <summary>
        /// Bug report: "im still getting up at an angle when i
        /// ragdoll." See ResetTilt's own doc comment in Jetpack.cs for the
        /// full reasoning -- this pins the bookkeeping half of the fix
        /// (CurrentTilt snapping to exactly zero, immediately, not eased);
        /// the real end-to-end dispatch half (PlayerInputReader actually
        /// calling this the instant a ragdoll goes limp) is covered by
        /// JetpackRagdollPlayTests instead, since that needs a real scene
        /// and a real Ragdoll.
        /// </summary>
        [Test]
        public void ResetTiltSnapsTheLeanBackToUprightImmediatelyRatherThanEasingIt()
        {
            for (int i = 0; i < 30; i++)
            {
                _jetpack.Tick(true, new Vector2(0.0f, 1.0f), false, 1.0f / 60.0f, cameraPitchDegrees: 30.0f);
            }
            Assert.Greater(_jetpack.CurrentTilt.x, 1.0f, "setup failed -- the body never actually leaned");

            _jetpack.ResetTilt();

            Assert.AreEqual(Vector2.zero, _jetpack.CurrentTilt,
                "ResetTilt should snap the lean back to zero immediately, not ease it out over time");

            // The carried SmoothDamp velocity has to be cleared too, not just
            // the position -- otherwise a hover's own already-zero target
            // would still visibly overshoot away from zero for a moment on
            // the very next Tick, off nothing but leftover momentum.
            _jetpack.Tick(true, Vector2.zero, false, 1.0f / 60.0f);
            Assert.AreEqual(0.0f, _jetpack.CurrentTilt.x, 0.01f,
                "leftover tilt velocity was not cleared -- the body drifted off zero again right after the reset");
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

        // --- ApplyCrashDamage: Requested, "horizontal fall damage... calculated same as fall damage but instead horizontal" ---

        [Test]
        public void ApplyCrashDamageBelowTheSafeEquivalentSpeedDealsNothing()
        {
            float healthBefore = _vitals.Health;
            // A speed comfortably under whatever converts (via d = v^2/2g)
            // to exactly SafeCrashDistance -- ApplyCrashDamage's own
            // (deliberately lower than SafeFallDistance) safe threshold,
            // see SafeCrashDistance's own doc comment for why.
            float safeSpeed = Mathf.Sqrt(2.0f * Physics.gravity.magnitude * PlayerVitals.SafeCrashDistance) * 0.9f;
            float damage = _vitals.ApplyCrashDamage(safeSpeed);

            Assert.AreEqual(0.0f, damage);
            Assert.AreEqual(healthBefore, _vitals.Health);
        }

        [Test]
        public void ApplyCrashDamageAboveTheSafeEquivalentSpeedHurts()
        {
            float healthBefore = _vitals.Health;
            // Modest overshoot, deliberately -- the conversion is v^2/2g, so
            // even a small speed increase past the safe threshold balloons
            // the equivalent fall distance fast. 1.3x keeps this a real but
            // survivable hit; a much bigger multiplier here once dealt more
            // damage than max health, which clamped at 0 and made the
            // straight subtraction below fail for a reason that had nothing
            // to do with the formula being tested.
            float fastSpeed = Mathf.Sqrt(2.0f * Physics.gravity.magnitude * PlayerVitals.SafeCrashDistance) * 1.3f;
            float damage = _vitals.ApplyCrashDamage(fastSpeed);

            Assert.Greater(damage, 0.0f, "a hard crash should have actually hurt");
            Assert.Less(damage, healthBefore, "setup failed -- this speed should be a survivable hit, not lethal");
            Assert.AreEqual(healthBefore - damage, _vitals.Health, 0.01f);
        }

        /// <summary>
        /// Not just "greater than zero" -- the exact same RATE
        /// TickFallDamage itself uses (FallDamagePerMetreFraction), fed the
        /// free-fall distance that would produce this same impact speed
        /// against ApplyCrashDamage's own SafeCrashDistance threshold, so a
        /// future change to either fall damage's own tuning or this
        /// conversion can't drift apart from each other unnoticed.
        /// </summary>
        [Test]
        public void ApplyCrashDamageMatchesTheFallDamageFormulaThroughTheSpeedConversion()
        {
            const float speed = 25.0f;
            float equivalentFallDistance = (speed * speed) / (2.0f * Physics.gravity.magnitude);
            float expectedDamage = (equivalentFallDistance - PlayerVitals.SafeCrashDistance) *
                (PlayerVitals.BaseMaxHealth * PlayerVitals.FallDamagePerMetreFraction);

            float damage = _vitals.ApplyCrashDamage(speed);

            Assert.Greater(expectedDamage, 0.0f, "setup failed --25 m/s should convert well past the safe distance");
            Assert.AreEqual(expectedDamage, damage, 0.01f);
        }

        /// <summary>
        /// Bug report, after the detection mechanism itself was
        /// already fixed once: "the jetpack sideways fall damage thing
        /// still hasnt workedt." The other ApplyCrashDamage tests above all
        /// feed it a hand-picked speed chosen to land on a particular side
        /// of the threshold -- none of them ask whether the jetpack can
        /// ever ACTUALLY reach a hurting speed in the first place. Drives
        /// the real PlayerLocomotion.Tick the same way a player holding
        /// W+shift+space against a wall would, then feeds its own real
        /// CurrentSpeed into ApplyCrashDamage -- proving the two systems
        /// agree, not just that each one is internally consistent.
        /// </summary>
        [Test]
        public void TheRealTopDiveSpeedIsActuallyFastEnoughToTriggerCrashDamage()
        {
            var input = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                JetpackThrusting = true,
                JetpackBoosting = true,
                JetpackDiving = true,
            };
            for (int i = 0; i < 60; i++)
            {
                _locomotion.Tick(input, 1.0f / 60.0f);
            }

            float diveSpeed = _locomotion.CurrentSpeed;
            Assert.AreEqual(_locomotion.SprintSpeed * _locomotion.JetpackDiveSpeedMultiplier, diveSpeed, 0.01f,
                "setup failed -- a full second of holding the dive input never reached the jetpack's own top speed");

            float damage = _vitals.ApplyCrashDamage(diveSpeed);
            Assert.Greater(damage, 0.0f,
                $"a full-speed jetpack dive ({diveSpeed:0.0} m/s) crashing into a wall deals ZERO damage. " +
                $"That speed converts to {(diveSpeed * diveSpeed) / (2.0f * Physics.gravity.magnitude):0.0}m of " +
                $"equivalent fall distance against a {PlayerVitals.SafeFallDistance:0.0}m safe threshold -- the " +
                "single fastest way to hit a wall with this jetpack still can't clear the bar this feature " +
                "reuses from vertical fall damage, so it can never fire from ordinary flying no matter how " +
                "reliably the wall contact itself is detected.");
        }

        /// <summary>
        /// TiltTarget/EaseTilt are pure and static specifically so the
        /// lean's own direction and magnitude are checkable without a real
        /// Animator -- both signs below were confirmed against Unity's own
        /// rotation convention with a scratch script before being pinned
        /// here (see TiltTarget's own doc comment), not just assumed from
        /// how the numbers read.
        ///
        /// STALE, CORRECTED (twice): this used to check pitch matched the
        /// camera 1:1 with no offset at all, then briefly checked the
        /// unconditional "+90 always" formula from the FOURTH correction.
        /// Both are superseded by the FIFTH -- a plain hover (flyingForward
        /// false, the default here) now stays CLAMPED at standing (0) for
        /// any camera at or above level, per TiltTarget's own doc comment;
        /// it only starts pitching once the camera actually looks down. See
        /// TiltTargetAHoverStaysUprightLookingUpOrLevelButAForwardFlightGoesHorizontal
        /// for the behaviour this test used to (incorrectly) check.
        /// </summary>
        [Test]
        public void TiltTargetPitchesUpWhenTheCameraLooksUp()
        {
            Assert.AreEqual(0.0f, Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: -30.0f).x, 0.001f,
                "a plain hover looking up should stay clamped at standing upright, not pitch further at all");
        }

        /// <summary>
        /// STALE, CORRECTED: this used to assert a hover pitched further
        /// down the more the camera looked down, back when the fifth
        /// correction's `2 * Clamp(camera, 0, 90)` curve was still in use
        /// for a plain hover. SIXTH Correction removed that too --
        /// "make it so if your just holding space... the character just
        /// stay upright and doesnt tilt forward when you look down." A
        /// hover no longer pitches at all, regardless of camera direction.
        /// </summary>
        [Test]
        public void TiltTargetPitchesDownWhenTheCameraLooksDown()
        {
            Vector2 lookingDown = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: 10.0f);
            Assert.AreEqual(0.0f, lookingDown.x, 0.001f,
                "a hovering body should stay upright even when the camera looks down");
        }

        /// <summary>
        /// Bug report: "now hold space defaults to superman
        /// position make it default to standing upright." The FOURTH
        /// correction's unconditional offset made a plain hover (Space
        /// alone) pitch to a full 90 (horizontal) at this jetpack's own
        /// everyday, roughly-level look angle -- exactly "Superman
        /// position" with nothing but thrust held. A hover and an actual
        /// forward flight need DIFFERENT curves at the identical camera
        /// angle: flying forward (flyingForward true) still targets
        /// horizontal at a level camera, exactly as it always has and still
        /// should; a plain hover (flyingForward false) instead stays
        /// upright at that same camera angle, since standing is its own
        /// rest pose, not a midpoint of a sweep.
        /// </summary>
        [Test]
        public void TiltTargetAHoverStaysUprightLookingUpOrLevelButAForwardFlightGoesHorizontal()
        {
            Vector2 hoverLevel = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: 0.0f, flyingForward: false);
            Assert.AreEqual(0.0f, hoverLevel.x, 0.001f,
                "a plain hover at a level camera should default to standing upright, not the Superman pose");

            Vector2 forwardLevel = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f), cameraPitchDegrees: 0.0f, flyingForward: true);
            Assert.AreEqual(90.0f, forwardLevel.x, 0.001f,
                "actually flying forward at a level camera should still be fully horizontal, unchanged");
        }

        /// <summary>
        /// Bug report: "if your only holding space with jetpack no
        /// w it still lets you lean all the way back and forward fix
        /// that... implement the same rule where it lets you look all the
        /// way up and down but the character only goes standing straight to
        /// headstand." Checked with EVERY input combination TickTilt could
        /// actually hand this while thrusting -- a plain hover (no WASD at
        /// all), pure forward, and a boosted dive -- all now land on the
        /// identical curve, since the offset is no longer conditional on
        /// any of them.
        /// </summary>
        [Test]
        public void TiltTargetAlwaysAppliesTheStandingToHeadstandOffsetRegardlessOfInput()
        {
            var flyingForwardCombinations = new[]
            {
                (moveInput: new Vector2(0.0f, 1.0f), boosting: false),   // plain forward thrust
                (moveInput: new Vector2(0.0f, 1.0f), boosting: true),    // boosted dive
            };

            foreach (var (moveInput, boosting) in flyingForwardCombinations)
            {
                float lookingStraightUp = Jetpack.TiltTarget(moveInput, Jetpack.JetpackMinPitchDegrees,
                    boosting, flyingForward: true).x;
                Assert.AreEqual(0.0f, lookingStraightUp, 0.001f,
                    $"looking straight up while flying forward should stand the body up straight " +
                    $"(moveInput={moveInput}, boosting={boosting})");

                float lookingStraightDown = Jetpack.TiltTarget(moveInput, Jetpack.JetpackMaxPitchDegrees,
                    boosting, flyingForward: true).x;
                Assert.AreEqual(180.0f, lookingStraightDown, 0.001f,
                    $"looking straight down while flying forward should invert the body into a full headstand " +
                    $"(moveInput={moveInput}, boosting={boosting})");
            }
        }

        /// <summary>
        /// SIXTH Correction: "make it so if your just holding
        /// space or holding shift+space no w with jetpack then the
        /// character just stay upright and doesnt tilt forward when you
        /// look down." A plain hover (flyingForward false, with or without
        /// boosting -- "holding space or holding shift+space") no longer
        /// tips toward a headstand at all -- it stays upright regardless of
        /// where the camera looks, unlike TiltTargetAlwaysAppliesTheStandingToHeadstandOffsetRegardlessOfInput's
        /// own forward-flight combinations just above, which still sweep
        /// the full range.
        /// </summary>
        [Test]
        public void TiltTargetStaysUprightWhenHoveringNoMatterWhereTheCameraLooks()
        {
            foreach (bool boosting in new[] { false, true })
            {
                float lookingStraightUp = Jetpack.TiltTarget(Vector2.zero, Jetpack.JetpackMinPitchDegrees,
                    boosting, flyingForward: false).x;
                Assert.AreEqual(0.0f, lookingStraightUp, 0.001f,
                    $"a hover looking up should stand upright (boosting={boosting})");

                float level = Jetpack.TiltTarget(Vector2.zero, 0.0f, boosting, flyingForward: false).x;
                Assert.AreEqual(0.0f, level, 0.001f,
                    $"a hover looking level should stand upright (boosting={boosting})");

                float lookingStraightDown = Jetpack.TiltTarget(Vector2.zero, Jetpack.JetpackMaxPitchDegrees,
                    boosting, flyingForward: false).x;
                Assert.AreEqual(0.0f, lookingStraightDown, 0.001f,
                    $"a hover looking straight down should stay upright, not tip toward a headstand " +
                    $"(boosting={boosting})");
            }
        }

        /// <summary>
        /// Roll is still WASD-driven, unaffected by the mouse-follow pitch
        /// change above -- strafing still banks the body into the turn.
        /// </summary>
        [Test]
        public void TiltTargetBanksIntoARightStrafe()
        {
            Assert.Less(Jetpack.TiltTarget(new Vector2(1.0f, 0.0f), cameraPitchDegrees: 0.0f).y, 0.0f,
                "pressing D should bank the body's right side down, into the strafe");
        }

        [Test]
        public void TiltTargetBanksIntoALeftStrafe()
        {
            Assert.Greater(Jetpack.TiltTarget(new Vector2(-1.0f, 0.0f), cameraPitchDegrees: 0.0f).y, 0.0f,
                "pressing A should bank the body's left side down, into the strafe");
        }

        /// <summary>
        /// STALE, CORRECTED: this used to check the whole Vector2 was
        /// exactly zero with no input and a level camera -- true only back
        /// when pitch had no offset at all. Pitch now always sits at
        /// SupermanPitchOffsetDegrees (90) at a level camera; only ROLL is
        /// still genuinely zero with no strafe held.
        /// </summary>
        [Test]
        public void TiltTargetIsStraightWithNoInputAndLevelCamera()
        {
            Assert.AreEqual(0.0f, Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: 0.0f).y,
                "no strafe held should still mean zero roll, regardless of the pitch offset");
        }

        [Test]
        public void TiltTargetRollScalesWithHowHardTheStickIsPushed()
        {
            // A half-deflected strafe should not bank as far as a full one --
            // otherwise a gentle nudge and a full press would look identical.
            Vector2 half = Jetpack.TiltTarget(new Vector2(0.5f, 0.0f), cameraPitchDegrees: 0.0f);
            Vector2 full = Jetpack.TiltTarget(new Vector2(1.0f, 0.0f), cameraPitchDegrees: 0.0f);
            Assert.Less(half.y, 0.0f);
            Assert.Greater(half.y, full.y);
        }

        /// <summary>
        /// UPDATED FOR THE SIXTH Correction: a plain hover no
        /// longer has a nonzero pitch target at any camera angle (see
        /// EaseTiltStraightensOutOnceTheCameraLevelsOut's own doc comment),
        /// so there would be nothing left to ease TOWARD here -- driven
        /// through actual forward flight instead, whose target still
        /// genuinely varies with camera angle.
        /// </summary>
        [Test]
        public void EaseTiltDoesNotJumpStraightToItsTarget()
        {
            // One ordinary frame at the full easing rate covers only a
            // fraction of the max lean -- an instant snap here would be the
            // exact "teleport to speed" problem PlayerLocomotion's own
            // HorizontalAcceleration was written to avoid, just for tilt
            // instead of movement.
            Vector2 target = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f), cameraPitchDegrees: 30.0f, flyingForward: true);
            Vector2 velocity = Vector2.zero;
            Vector2 eased = Jetpack.EaseTilt(Vector2.zero, target, ref velocity, 1.0f / 60.0f);
            Assert.Greater(eased.x, 0.0f);
            Assert.Less(eased.x, target.x);
        }

        [Test]
        public void EaseTiltReachesItsTargetGivenEnoughTime()
        {
            Vector2 target = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f), cameraPitchDegrees: 30.0f, flyingForward: true);
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
        /// one of those keys it goes back to straight" ask. Pitch's own
        /// equivalent is the camera returning to level, not a released key.
        ///
        /// UPDATED FOR THE SIXTH Correction: a plain hover
        /// (flyingForward false) now stays at a constant 0 regardless of
        /// the camera, so it can no longer stand in for "a target that
        /// actually changes as the camera moves" -- this now drives the
        /// scenario through actual forward flight instead
        /// (flyingForward true), whose pitch still genuinely varies with
        /// camera angle (cameraPitchDegrees + SupermanPitchOffsetDegrees),
        /// to keep proving the same thing the test always cared about:
        /// easing actually tracks a NEW target once the camera moves,
        /// rather than snapping or sticking to the old one.
        /// </summary>
        [Test]
        public void EaseTiltStraightensOutOnceTheCameraLevelsOut()
        {
            Vector2 forward = new Vector2(0.0f, 1.0f);
            Vector2 held = Jetpack.TiltTarget(forward, cameraPitchDegrees: 30.0f, flyingForward: true);
            Vector2 tilt = Vector2.zero;
            Vector2 velocity = Vector2.zero;
            for (int i = 0; i < 180; i++)
            {
                tilt = Jetpack.EaseTilt(tilt, held, ref velocity, 1.0f / 60.0f);
            }
            Assert.AreEqual(120.0f, tilt.x, 0.01f, "setup failed -- the body never actually leaned to the held target");

            Vector2 released = Jetpack.TiltTarget(forward, cameraPitchDegrees: Jetpack.JetpackMinPitchDegrees,
                flyingForward: true);
            for (int i = 0; i < 180; i++)
            {
                tilt = Jetpack.EaseTilt(tilt, released, ref velocity, 1.0f / 60.0f);
            }
            Assert.AreEqual(0.0f, tilt.x, 0.01f, "looking all the way back up never straightened the body back out");
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
            // Driven through actual forward flight, not a hover -- since the
            // sixth explicit correction a hover's own pitch target is always
            // 0 regardless of camera, which would leave EaseTilt nothing to
            // decelerate into (see EaseTiltStraightensOutOnceTheCameraLevelsOut).
            Vector2 target = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f), cameraPitchDegrees: 30.0f, flyingForward: true);
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
        /// SECOND Correction: "roll not turn left to right." The
        /// FIRST fix here checked that the body's own POST-PITCH forward
        /// stayed fixed under roll -- true, but the wrong property: once
        /// SupermanPitchOffsetDegrees has tipped the body a full 90
        /// degrees, "forward" points straight down, not along the flight
        /// path, so "spins around forward" was actually a YAW around a
        /// vertical axis. Roll now always turns around the FIXED, real,
        /// un-pitched Vector3.forward (`roll * pitch`, roll applied
        /// outermost) -- the direction the parent is actually flying
        /// toward, unaffected by however far pitch has tipped the body.
        ///
        /// The axis that has to stay fixed under roll is therefore
        /// whichever LOCAL axis pitch itself carries TO Vector3.forward --
        /// at pitch=0 that is Vector3.forward itself (ordinary, near-level
        /// flight: unchanged from before this fix); at pitch=90 (the
        /// Superman dive) it is Vector3.up (a standing person's own
        /// head-to-toe axis, tipped 90 degrees, IS the direction they are
        /// now travelling in) -- exactly the case this bug report was
        /// about.
        /// </summary>
        [Test]
        public void TiltRotationKeepsTheRealFlightDirectionFixedRegardlessOfRoll()
        {
            // (pitch, the LOCAL axis that pitch carries to Vector3.forward)
            var cases = new (float pitch, Vector3 axis)[]
            {
                (0.0f, Vector3.forward),
                (90.0f, Vector3.up),
            };
            foreach (var (pitch, axis) in cases)
            {
                Vector3 atNoRoll = Jetpack.TiltRotation(new Vector2(pitch, 0.0f)) * axis;
                foreach (float roll in new[] { -20.0f, 5.0f, 45.0f })
                {
                    Vector3 withRoll = Jetpack.TiltRotation(new Vector2(pitch, roll)) * axis;
                    Assert.Less(Vector3.Distance(atNoRoll, withRoll), 0.001f,
                        $"at pitch={pitch}, rolling by {roll} degrees moved the real flight direction -- " +
                        "a real roll should spin the wings around it, never move it");
                }
            }
        }

        /// <summary>Sanity check that TiltRotation still actually rolls SOMETHING -- the right ("wing") vector has to move even though the body's own length axis does not.</summary>
        [Test]
        public void TiltRotationActuallyBanksTheRightVector()
        {
            Vector3 rightAtNoRoll = Jetpack.TiltRotation(new Vector2(90.0f, 0.0f)) * Vector3.right;
            Vector3 rightWithRoll = Jetpack.TiltRotation(new Vector2(90.0f, 30.0f)) * Vector3.right;
            Assert.Greater(Vector3.Distance(rightAtNoRoll, rightWithRoll), 0.01f,
                "rolling by 30 degrees should visibly bank the body, not leave it looking identical");
        }

        /// <summary>
        /// "shift is a boost when midair using jetpack" -- holding a
        /// direction spends the boost on a further ROLL (pitch no longer
        /// scales with boosting at all -- it just follows the camera),
        /// checked directly against TiltTarget's own two constants rather
        /// than just "greater than the plain lean" so a future
        /// TiltMaxDegrees tweak can't silently satisfy this without
        /// BoostTiltMaxDegrees actually being the one in effect.
        /// </summary>
        [Test]
        public void TiltTargetAmplifiesRollWhenBoosting()
        {
            Vector2 boosted = Jetpack.TiltTarget(new Vector2(1.0f, 0.0f), cameraPitchDegrees: 0.0f, boosting: true);
            Assert.AreEqual(-Jetpack.BoostTiltMaxDegrees, boosted.y, 0.001f);
        }

        [Test]
        public void TiltTargetUsesThePlainRollMaxWhenNotBoosting()
        {
            Vector2 plain = Jetpack.TiltTarget(new Vector2(1.0f, 0.0f), cameraPitchDegrees: 0.0f, boosting: false);
            Assert.AreEqual(-Jetpack.TiltMaxDegrees, plain.y, 0.001f);
        }

        /// <summary>
        /// Boosting with nothing to lean toward must not manufacture a roll
        /// out of thin air -- see Jetpack.Tick's own "spends on either lean
        /// or climb, never both" doc comment.
        /// </summary>
        [Test]
        public void TiltTargetIgnoresBoostingWithNoDirectionHeld()
        {
            // Only ROLL is checked here -- pitch is no longer zero at a
            // level camera (see the FOURTH Correction), but
            // boosting with nothing to strafe toward should still never
            // manufacture a roll out of thin air.
            Assert.AreEqual(0.0f,
                Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: 0.0f, boosting: true).y, 0.001f);
        }

        /// <summary>
        /// Requested: "if your in superman position and you press A or
        /// D the model rolls slightly in that direction" -- a dive still
        /// picks up a real roll off a held strafe, at SupermanRollMaxDegrees
        /// (see TiltTargetDivingUsesTheSupermanRollMax and that constant's
        /// own doc comment for why 12 degrees, the first attempt, was
        /// confirmed too subtle to actually read as rolling once checked
        /// against a real equipped-body screenshot rather than just the
        /// isolated numbers). Pitch itself is unaffected by the diving
        /// flag -- it already follows the camera on every frame, diving or
        /// not.
        /// </summary>
        [Test]
        public void TiltTargetDivingStillBanksIntoAHeldStrafe()
        {
            Vector2 diving = Jetpack.TiltTarget(new Vector2(0.6f, 1.0f), cameraPitchDegrees: 40.0f,
                boosting: true, flyingForward: true);
            Assert.AreEqual(40.0f + Jetpack.SupermanPitchOffsetDegrees, diving.x, 0.001f,
                "diving should add the Superman offset on top of the camera's own pitch");
            Assert.Less(diving.y, 0.0f,
                "pressing D (positive x) while diving should still bank the body into the strafe");
        }

        /// <summary>
        /// The dive's own bank uses ITS OWN dedicated max, not either
        /// ordinary lean constant -- a dive is boosting by definition (see
        /// TickTilt's own "diving" derivation), so without this override
        /// it would otherwise inherit BoostTiltMaxDegrees automatically,
        /// which is smaller. Checked directly against the constant, the
        /// same reasoning TiltTargetAmplifiesRollWhenBoosting already uses.
        /// </summary>
        [Test]
        public void TiltTargetDivingUsesTheSupermanRollMax()
        {
            Vector2 diving = Jetpack.TiltTarget(new Vector2(1.0f, 1.0f), cameraPitchDegrees: 0.0f,
                boosting: true, flyingForward: true);
            Assert.AreEqual(-Jetpack.SupermanRollMaxDegrees, diving.y, 0.001f);
        }

        /// <summary>Looking straight ahead with no strafe held while diving should still read as a clean, level nose-dive.</summary>
        [Test]
        public void TiltTargetDivingWithNoStrafeStaysLevel()
        {
            Vector2 diving = Jetpack.TiltTarget(new Vector2(0.0f, 1.0f), cameraPitchDegrees: 0.0f,
                boosting: true, flyingForward: true);
            Assert.AreEqual(0.0f, diving.y, 0.001f);
        }

        // --- SupermanPitchOffsetDegrees: SECOND Requested, refining the first ---

        /// <summary>Looking forward while diving is the pose the whole feature is named after.</summary>
        [Test]
        public void TiltTargetIsFullyHorizontalWhenDivingWhileLookingForward()
        {
            Vector2 diving = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: 0.0f,
                boosting: true, flyingForward: true);
            Assert.AreEqual(90.0f, diving.x, 0.001f,
                "looking forward while diving should be fully horizontal, Superman-style");
        }

        /// <summary>Requested: "if looking straight up he should be stood up straight normally."</summary>
        [Test]
        public void TiltTargetStandsUpstraightWhenDivingWhileLookingStraightUp()
        {
            Vector2 diving = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: Jetpack.JetpackMinPitchDegrees,
                boosting: true, flyingForward: true);
            Assert.AreEqual(0.0f, diving.x, 0.001f,
                "looking straight up while diving should stand the body back up straight");
        }

        /// <summary>Requested: "if looking down he should be head down feet in the air, straight vertically."</summary>
        [Test]
        public void TiltTargetGoesFullyInvertedWhenDivingWhileLookingStraightDown()
        {
            Vector2 diving = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: Jetpack.JetpackMaxPitchDegrees,
                boosting: true, flyingForward: true);
            Assert.AreEqual(180.0f, diving.x, 0.001f,
                "looking straight down while diving should invert the body fully, head down feet up");
        }

        // STALE TEST REMOVED (TiltTargetPlainThrustDoesNotGetTheSupermanOffset):
        // used to assert a plain hover (Space alone, nothing else held) got
        // NO Superman offset at all -- true right up until the FOURTH
        // Correction (see SupermanPitchOffsetDegrees' own doc
        // comment) made the offset unconditional. See
        // TiltTargetAlwaysAppliesTheStandingToHeadstandOffsetRegardlessOfInput,
        // which explicitly covers this exact "plain hover" case as its
        // first input combination.

        /// <summary>
        /// THIRD Correction -- roll's own Superman-sized cap now
        /// requires BOTH boosting AND flyingForward together, not
        /// flyingForward alone. Real callers (TickTilt) never actually
        /// separated these (its own "amplify" is already boostHeld-gated),
        /// but this pins the priority down explicitly: plain forward
        /// thrust without boost -- which now DOES get the standing/
        /// headstand PITCH mapping -- must still keep the small, ordinary
        /// TiltMaxDegrees roll, not the decisive Superman-dive bank. See
        /// TiltTargetFlyingForwardWithoutBoostingGetsTheSameStandingToHeadstandPitchMapping
        /// for the pitch side of the same scenario.
        /// </summary>
        [Test]
        public void FlyingForwardWithoutBoostingKeepsTheOrdinaryRollNotTheSupermanOne()
        {
            Vector2 result = Jetpack.TiltTarget(new Vector2(1.0f, 1.0f), cameraPitchDegrees: 40.0f,
                boosting: false, flyingForward: true);
            Assert.AreEqual(-Jetpack.TiltMaxDegrees, result.y, 0.001f,
                "flying forward without boosting should keep the small, ordinary roll cap");
        }

        /// <summary>
        /// Bug report: "when holding space and w without shift...
        /// if you look all the way up its like your character is lying on
        /// his back in the sky[,] ... looking all the way down it is
        /// rotated too far as well. make it so the farthest he rotates...
        /// when looking up is just him standing up straight no further...
        /// and when you are looking all the way down its should only
        /// rotate as far as him doing a headstand midair no further."
        /// The exact same three-point mapping TiltTargetIsFullyHorizontalWhenDivingWhileLookingForward/
        /// TiltTargetStandsUpstraightWhenDivingWhileLookingStraightUp/
        /// TiltTargetGoesFullyInvertedWhenDivingWhileLookingStraightDown
        /// already prove for the boosted dive, now proven again with
        /// boosting explicitly false -- "just moving up/down" needed the
        /// identical standing/horizontal/headstand endpoints, not the old
        /// raw, unclamped 1:1 camera-follow that let it rotate PAST
        /// standing (onto its own back) at the top and only PART of the
        /// way to a headstand (a merely horizontal prone pose) at the
        /// bottom.
        /// </summary>
        [Test]
        public void TiltTargetFlyingForwardWithoutBoostingGetsTheSameStandingToHeadstandPitchMapping()
        {
            Vector2 lookingUp = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: Jetpack.JetpackMinPitchDegrees,
                boosting: false, flyingForward: true);
            Assert.AreEqual(0.0f, lookingUp.x, 0.001f,
                "looking straight up while flying forward (no boost) should stand the body up straight, not lay it on its back");

            Vector2 lookingForward = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: 0.0f,
                boosting: false, flyingForward: true);
            Assert.AreEqual(90.0f, lookingForward.x, 0.001f,
                "looking level while flying forward (no boost) should be fully horizontal");

            Vector2 lookingDown = Jetpack.TiltTarget(Vector2.zero, cameraPitchDegrees: Jetpack.JetpackMaxPitchDegrees,
                boosting: false, flyingForward: true);
            Assert.AreEqual(180.0f, lookingDown.x, 0.001f,
                "looking straight down while flying forward (no boost) should invert the body into a full headstand, not stop at merely horizontal");
        }

        /// <summary>
        /// Correction: the lean used to follow WASD alone, so
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
        /// held direction, but WITH thrust, must still lean. Roll (strafe),
        /// not pitch -- pitch no longer comes from moveInput at all, it
        /// follows the camera (see TiltTarget's own doc comment); moveInput
        /// still drives roll exactly as before.
        /// </summary>
        [Test]
        public void HoldingADirectionWithThrustStillLeansTheBody()
        {
            for (int i = 0; i < 30; i++)
            {
                _jetpack.Tick(thrustHeld: true, moveInput: new Vector2(1.0f, 0.0f), boostHeld: false,
                    deltaTime: 1.0f / 60.0f);
            }

            Assert.Less(_jetpack.CurrentTilt.y, -1.0f,
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
                _jetpack.Tick(thrustHeld: true, moveInput: new Vector2(1.0f, 0.0f), boostHeld: false,
                    deltaTime: 1.0f / 60.0f);
            }
            Assert.Less(_jetpack.CurrentTilt.y, -1.0f, "setup failed -- the body never actually leaned");

            // SmoothDamp settles more gradually than the old constant-rate
            // ramp did (see TiltSmoothTime's own doc comment) -- 30 frames
            // (half a second) was plenty against the old rate, but is not
            // long enough for a curve that also eases OUT approaching zero.
            for (int i = 0; i < 180; i++)
            {
                _jetpack.Tick(thrustHeld: false, moveInput: new Vector2(1.0f, 0.0f), boostHeld: false,
                    deltaTime: 1.0f / 60.0f);
            }
            Assert.AreEqual(0.0f, _jetpack.CurrentTilt.y, 0.01f,
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

        /// <summary>
        /// Requested: "hold w + shift while using jetpack your character
        /// tilts all the way horizontally" -- end to end through the real
        /// public Tick, not just TiltTarget in isolation, so this also
        /// proves TickTilt's own W-held detection actually reaches it. Pitch
        /// itself now tracks wherever the camera is pointed rather than a
        /// fixed 90 -- pointed steeply down here, matching an actual dive.
        /// </summary>
        [Test]
        public void HoldingForwardAndBoostSettlesPitchToTheCameraWithNoRoll()
        {
            for (int i = 0; i < 180; i++)
            {
                _jetpack.Tick(true, new Vector2(0.0f, 1.0f), boostHeld: true, deltaTime: 1.0f / 60.0f,
                    cameraPitchDegrees: 80.0f);
            }
            Assert.AreEqual(80.0f + Jetpack.SupermanPitchOffsetDegrees, _jetpack.CurrentTilt.x, 0.5f);
            Assert.AreEqual(0.0f, _jetpack.CurrentTilt.y, 0.5f, "a dive should not also carry a roll");
        }

        /// <summary>A pure sideways or backward boost is not "w + shift" and must keep the ordinary amplified roll rather than levelling it out.</summary>
        [Test]
        public void BoostingSidewaysKeepsTheOrdinaryRollRatherThanLevellingIt()
        {
            for (int i = 0; i < 180; i++)
            {
                _jetpack.Tick(true, new Vector2(1.0f, 0.0f), boostHeld: true, deltaTime: 1.0f / 60.0f);
            }
            Assert.Less(_jetpack.CurrentTilt.y, -1.0f,
                "a pure sideways boost should still bank into the strafe, not level out as if diving");
        }

        // --- ClimbVelocityFor: Requested, "make it curve and turn with where your looking" ---

        [Test]
        public void FlyingForwardWhileLookingUpClimbs()
        {
            float velocity = Jetpack.ClimbVelocityFor(new Vector2(0.0f, 1.0f), boostHeld: false, cameraPitchDegrees: -30.0f);
            Assert.Greater(velocity, 0.0f, "looking up while flying forward should climb, not sink");
        }

        [Test]
        public void FlyingForwardWhileLookingDownDives()
        {
            float velocity = Jetpack.ClimbVelocityFor(new Vector2(0.0f, 1.0f), boostHeld: false, cameraPitchDegrees: 30.0f);
            Assert.Less(velocity, 0.0f, "looking down while flying forward should dive, not climb");
        }

        /// <summary>Hovering (no forward component) must not start climbing or sinking just because the camera looked somewhere.</summary>
        [Test]
        public void HoveringInPlaceIgnoresCameraPitch()
        {
            Assert.AreEqual(Jetpack.ThrustSpeed,
                Jetpack.ClimbVelocityFor(Vector2.zero, boostHeld: false, cameraPitchDegrees: -30.0f), 0.001f);
            Assert.AreEqual(Jetpack.ThrustSpeed,
                Jetpack.ClimbVelocityFor(Vector2.zero, boostHeld: false, cameraPitchDegrees: 30.0f), 0.001f);
        }

        /// <summary>Straight up (or boosted straight up) is a flat push, not pitch-derived -- level pitch is not the same claim as "camera pitch is ignored here."</summary>
        [Test]
        public void HoveringWithBoostStillReachesTheFlatBoostSpeedRegardlessOfPitch()
        {
            Assert.AreEqual(Jetpack.BoostThrustSpeed,
                Jetpack.ClimbVelocityFor(Vector2.zero, boostHeld: true, cameraPitchDegrees: 15.0f), 0.001f);
        }

        /// <summary>A W+shift dive climbs or dives twice as fast as the same pitch would plainly, matching the horizontal 2x from PlayerLocomotion.JetpackDiveSpeedMultiplier.</summary>
        [Test]
        public void DivingDoublesTheClimbRateAtTheSamePitch()
        {
            float plain = Jetpack.ClimbVelocityFor(new Vector2(0.0f, 1.0f), boostHeld: false, cameraPitchDegrees: -20.0f);
            float diving = Jetpack.ClimbVelocityFor(new Vector2(0.0f, 1.0f), boostHeld: true, cameraPitchDegrees: -20.0f);
            Assert.AreEqual(plain * Jetpack.DiveClimbMultiplier, diving, 0.01f);
        }

        /// <summary>Perfectly level while diving means flat, fast horizontal flight -- no silent climb or sink just from turning the boost on.</summary>
        [Test]
        public void DivingAtLevelPitchNeitherClimbsNorDives()
        {
            float velocity = Jetpack.ClimbVelocityFor(new Vector2(0.0f, 1.0f), boostHeld: true, cameraPitchDegrees: 0.0f);
            Assert.AreEqual(0.0f, velocity, 0.001f);
        }

        /// <summary>
        /// StopThrustEffects is called from PlayerInputReader's own ragdoll
        /// branch every frame while limp (see its own doc comment) -- this
        /// fixture's Jetpack has neither FlameEffect nor ThrustAudio
        /// assigned (see the class's own "no Animator" doc comment for why
        /// a minimal fixture like this has none of the visual pieces
        /// wired), so the real, meaningful proof that it actually stops a
        /// playing flame/audio lives in JetpackRealScenePlayTests
        /// (RagdollingWhileThrustingStopsTheFlameAndAudioEvenWithoutReleasingSpace)
        /// against the real prefab -- this only guards the null case
        /// running every single frame is safe.
        /// </summary>
        [Test]
        public void StopThrustEffectsIsSafeWithNeitherFlameNorAudioAssigned()
        {
            Assert.DoesNotThrow(() => _jetpack.StopThrustEffects());
        }
    }
}
