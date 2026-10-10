using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Crouching and dashing, driven through the real component with a real
    /// CharacterController under it.
    ///
    /// PlayMode would be a truer test of the movement itself, but the two rules
    /// that actually bite here -- sprint beating crouch, and a dash ignoring
    /// live input -- are decided in Tick and readable straight afterwards, so
    /// they can be pinned without waiting for frames to pass.
    /// </summary>
    public sealed class PlayerLocomotionTests
    {
        private GameObject _object;
        private PlayerLocomotion _locomotion;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Body");
            _object.AddComponent<CharacterController>();
            _locomotion = _object.AddComponent<PlayerLocomotion>();
            // EditMode never calls Awake, and the component caches its
            // controller there. Doing it by hand is what makes this testable at
            // all -- see the class note.
            _locomotion.Bind();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        private static LocomotionInput Forward(bool sprint = false, bool crouch = false)
        {
            return new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                YawDegrees = 0.0f,
                Sprint = sprint,
                Crouch = crouch,
            };
        }

        /// <summary>
        /// Ticks the same input enough times for HorizontalAcceleration to
        /// actually reach it -- a single 0.016s tick from a dead stop only
        /// covers 60*0.016=0.96 m/s of a possibly much larger target, which
        /// used to be irrelevant when speed snapped to its target in one
        /// step but now genuinely takes real (if short) time. 20 ticks
        /// covers up to 20 m/s of acceleration, comfortably past
        /// SprintSpeed, so every settled-speed assertion below reads the
        /// body once it has actually arrived rather than mid-ramp.
        /// </summary>
        private void Settle(LocomotionInput input, int ticks = 20)
        {
            for (int i = 0; i < ticks; i++)
            {
                _locomotion.Tick(input, 0.016f);
            }
        }

        [Test]
        public void HoldingCrouchCrouches()
        {
            _locomotion.Tick(Forward(crouch: true), 0.016f);
            Assert.IsTrue(_locomotion.IsCrouching);

            _locomotion.Tick(Forward(), 0.016f);
            Assert.IsFalse(_locomotion.IsCrouching);
        }

        /// <summary>
        /// Holding both keys must sprint, not crouch. There is no crouch-sprint
        /// clip, so a body that stayed crouched would be shown walking in a
        /// crouch while actually travelling at 7.5 m/s -- the exact feet-
        /// sliding mismatch the directional clips exist to prevent.
        /// </summary>
        [Test]
        public void SprintBeatsCrouch()
        {
            Settle(Forward(sprint: true, crouch: true));

            Assert.IsFalse(_locomotion.IsCrouching);
            Assert.IsTrue(_locomotion.IsSprinting);
            Assert.AreEqual(_locomotion.SprintSpeed, _locomotion.CurrentSpeed, 0.01f);
        }

        /// <summary>
        /// The explicit ask: holding shift while the jetpack is thrusting
        /// should reach sprint speed. Checked BACKWARD, not forward -- see
        /// LocomotionInput.JetpackBoosting's own doc comment for why this
        /// path is deliberately not restricted to forward the way ground
        /// Sprint is: JetpackThrusting already forces a fixed pose
        /// regardless of travel direction, so there is no missing clip for
        /// a backward or sideways burst of speed to expose.
        /// </summary>
        [Test]
        public void JetpackBoostingReachesSprintSpeedEvenGoingBackward()
        {
            var input = new LocomotionInput
            {
                Move = new Vector2(0.0f, -1.0f),
                JetpackBoosting = true,
            };
            Settle(input);

            Assert.AreEqual(_locomotion.SprintSpeed, _locomotion.CurrentSpeed, 0.01f);
        }

        /// <summary>
        /// JetpackBoosting reaches the same SPEED as sprint, but is not
        /// itself a sprint -- IsSprinting drives the ground run animation/
        /// footstep loop, neither of which applies while airborne on a
        /// jetpack (JetpackThrusting already forces its own fixed pose, and
        /// footstep audio is separately gated on IsGrounded).
        /// </summary>
        [Test]
        public void JetpackBoostingDoesNotCountAsSprintingForAnimationPurposes()
        {
            var input = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                JetpackBoosting = true,
            };
            Settle(input);

            Assert.IsFalse(_locomotion.IsSprinting);
            Assert.AreEqual(_locomotion.SprintSpeed, _locomotion.CurrentSpeed, 0.01f);
        }

        /// <summary>
        /// EXPLICIT ASK: "the jetpack goes twice as fast" while diving
        /// (w+shift). Checked against SprintSpeed * JetpackDiveSpeedMultiplier
        /// directly, the same "pin the real constant, not just greater-than"
        /// reasoning JetpackBoostingReachesSprintSpeedEvenGoingBackward uses.
        /// </summary>
        [Test]
        public void JetpackDivingReachesTwiceSprintSpeed()
        {
            var input = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                JetpackBoosting = true,
                JetpackDiving = true,
            };
            Settle(input);

            Assert.AreEqual(_locomotion.SprintSpeed * _locomotion.JetpackDiveSpeedMultiplier,
                _locomotion.CurrentSpeed, 0.01f);
        }

        [Test]
        public void CrouchingDoesNotSlowTheBody()
        {
            // Godot's crouch is a pose, not a speed -- there is no crouch speed
            // constant anywhere in player.gd. Pinned because "crouch should be
            // slower" is an easy assumption to add later by accident.
            //
            // BOTH SIDES SETTLED FROM THEIR OWN FRESH START, not one read
            // mid-ramp against the other one tick further along -- crouched
            // and uncrouched share the exact same target (WalkSpeed), so
            // the only way to compare them fairly is once HorizontalAcceleration
            // has actually finished closing the gap on both, from identical
            // starting conditions. A second body, not a reused one, keeps
            // this independent of whatever state the first left behind.
            Settle(Forward(crouch: true));
            float crouched = _locomotion.CurrentSpeed;

            var secondObject = new GameObject("SecondBody");
            try
            {
                secondObject.AddComponent<CharacterController>();
                PlayerLocomotion second = secondObject.AddComponent<PlayerLocomotion>();
                second.Bind();
                for (int i = 0; i < 20; i++)
                {
                    second.Tick(Forward(), 0.016f);
                }
                Assert.AreEqual(second.CurrentSpeed, crouched, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void ADashIsFasterThanASprintAndRunsOut()
        {
            _locomotion.StartDash(Vector3.forward);
            Assert.IsTrue(_locomotion.IsDashing);

            _locomotion.Tick(default, 0.05f);
            Assert.Greater(_locomotion.CurrentSpeed, _locomotion.SprintSpeed,
                "the dash is no faster than a sprint");

            // Past the burst, and back under the player's own control.
            _locomotion.Tick(default, PlayerLocomotion.DashSeconds);
            Assert.IsFalse(_locomotion.IsDashing);

            // NOT INSTANT ANY MORE, DELIBERATELY -- HorizontalAcceleration
            // decelerates the dash's own high exit speed back down toward
            // zero the same way it eases INTO any other speed change now,
            // rather than the dash cutting to a dead stop the instant its
            // timer runs out. Settled here (20 ticks, the same margin
            // Settle's own doc comment uses) rather than asserting on the
            // very next frame, which is now genuinely still mid-deceleration.
            for (int i = 0; i < 20; i++)
            {
                _locomotion.Tick(default, 0.016f);
            }
            Assert.AreEqual(0.0f, _locomotion.CurrentSpeed, 0.001f,
                "the body never actually stopped after the dash ended");
        }

        /// <summary>
        /// A dash must cover the same ground however badly the frame rate is
        /// behaving. Applying the full dash speed across a step longer than the
        /// dash has left carries the body past where it should have stopped --
        /// a 0.2s dash on a 0.3s hitch travelling half again as far. A dodge
        /// whose distance depends on the frame rate is not a dodge.
        /// </summary>
        [Test]
        public void OneLongFrameDoesNotMakeTheDashGoFurther()
        {
            _locomotion.StartDash(Vector3.forward);
            _locomotion.Tick(default, 1.0f);

            float travelled = _locomotion.Velocity.z * 1.0f;
            float expected = PlayerLocomotion.DashSpeed * PlayerLocomotion.DashSeconds;
            Assert.AreEqual(expected, travelled, 0.01f,
                "a single slow frame stretched the dash");
        }

        /// <summary>
        /// The direction is committed at the start. Steering mid-dash would
        /// make it a boost rather than a lunge, and the whole reason it costs
        /// stamina and grants i-frames is that you cannot change your mind.
        /// </summary>
        [Test]
        public void ADashIgnoresWhereYouSteerDuringIt()
        {
            _locomotion.StartDash(Vector3.forward);

            _locomotion.Tick(new LocomotionInput { Move = new Vector2(1.0f, 0.0f) }, 0.05f);

            Vector3 travel = _locomotion.Velocity;
            travel.y = 0.0f;
            Assert.Greater(travel.normalized.z, 0.99f,
                "steering during the dash turned it");
        }

        [Test]
        public void ADashWithNoDirectionDoesNothing()
        {
            _locomotion.StartDash(Vector3.zero);
            Assert.IsFalse(_locomotion.IsDashing);
        }

        /// <summary>
        /// A dash straight up is a dash nowhere -- the flat component is empty.
        /// Worth pinning because normalising a near-vertical vector after
        /// flattening it is exactly where a NaN gets in.
        /// </summary>
        [Test]
        public void AVerticalDashIsNotADash()
        {
            _locomotion.StartDash(Vector3.up);

            Assert.IsFalse(_locomotion.IsDashing);
            Assert.IsFalse(float.IsNaN(_locomotion.Velocity.x));
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "fix the pistol make it so you hold it
        /// straight crosshair centered character not at weird angle." The
        /// body used to only ever turn to face the camera WHILE MOVING --
        /// stopping to aim (the natural way to aim: stop, aim, shoot) left
        /// it frozen facing wherever it last walked, with the pistol
        /// (rigidly parented to the right hand) pointing off at whatever
        /// angle that happened to be relative to the actual crosshair.
        /// </summary>
        [Test]
        public void AimingWhileStandingStillTurnsTheBodyToFaceTheCamera()
        {
            var input = new LocomotionInput
            {
                Move = Vector2.zero,
                YawDegrees = 90.0f,
                Aiming = true,
            };

            _locomotion.Tick(input, 0.016f);

            Assert.AreEqual(90.0f, _locomotion.transform.eulerAngles.y, 0.01f,
                "aiming while standing still should turn the body to face the camera");
        }

        /// <summary>
        /// SNAPS, DOES NOT EASE -- a gradual TurnDegreesPerSecond-style catch
        /// up (the ordinary walking-turn rate) would itself read as "at a
        /// weird angle" for the first several frames after aiming starts
        /// from a large angle mismatch, which is exactly the reported bug.
        /// </summary>
        [Test]
        public void AimingSnapsInstantlyRatherThanEasingLikeAnOrdinaryWalkingTurn()
        {
            var input = new LocomotionInput
            {
                Move = Vector2.zero,
                YawDegrees = 179.0f,
                Aiming = true,
            };

            // One tick, deliberately -- an easing turn would only cover a
            // few degrees in a single 0.016s step; a snap covers the whole
            // 179 in that same step.
            _locomotion.Tick(input, 0.016f);

            Assert.AreEqual(179.0f, _locomotion.transform.eulerAngles.y, 0.01f);
        }

        /// <summary>Scoped to aiming specifically -- standing still and NOT aiming must keep the old behaviour untouched.</summary>
        [Test]
        public void StandingStillAndNotAimingLeavesFacingUntouched()
        {
            _locomotion.transform.rotation = Quaternion.Euler(0.0f, 45.0f, 0.0f);
            var input = new LocomotionInput
            {
                Move = Vector2.zero,
                YawDegrees = 90.0f,
                Aiming = false,
            };

            _locomotion.Tick(input, 0.016f);

            Assert.AreEqual(45.0f, _locomotion.transform.eulerAngles.y, 0.01f,
                "standing still without aiming should leave the body facing wherever it already was");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "he is rotated to the left [while] holding
        /// right click," reported specifically while walking. Aiming used
        /// to be an else-if under "moving", so aiming WHILE moving took the
        /// ordinary eased TurnDegreesPerSecond walking-turn instead of the
        /// instant snap AimingSnapsInstantlyRatherThanEasingLikeAnOrdinary
        /// WalkingTurn already covers for standing still -- meaning the
        /// body visibly lagged the camera for as long as it kept turning
        /// while walking and aiming at once, which reads as "rotated
        /// wrong" for as long as the lag lasts, not just a one-frame snap.
        /// Moving forward is exactly the case that used to hit the eased
        /// branch, so Move is deliberately nonzero here, not
        /// Vector2.zero like the standing-still tests above.
        /// </summary>
        [Test]
        public void AimingWhileMovingAlsoSnapsInstantlyInsteadOfEasing()
        {
            var input = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                YawDegrees = 90.0f,
                Aiming = true,
            };

            _locomotion.Tick(input, 0.016f);

            Assert.AreEqual(90.0f, _locomotion.transform.eulerAngles.y, 0.01f,
                "aiming while moving should snap to face the camera instantly, the same as standing still");
        }

        /// <summary>
        /// LOCK-ON: the body faces the camera's yaw (which is steering at the
        /// target) even standing still -- ordinary movement only turns the
        /// body while moving, which would leave a locked-on fighter looking
        /// away from the enemy until they took a step.
        /// </summary>
        [Test]
        public void LockedOnFacesTheCameraYawEvenStandingStill()
        {
            var input = new LocomotionInput { Move = Vector2.zero, YawDegrees = 120.0f, LockedOn = true };

            _locomotion.Tick(input, 0.016f);

            Assert.AreEqual(120.0f, _locomotion.transform.eulerAngles.y, 0.01f);
        }

        /// <summary>Strafing sideways while locked must NOT turn the body to face the strafe.</summary>
        [Test]
        public void LockedOnStrafingKeepsFacingTheTarget()
        {
            var input = new LocomotionInput
            {
                Move = new Vector2(1.0f, 0.0f),   // pure sideways
                YawDegrees = 30.0f,
                LockedOn = true,
            };

            for (int i = 0; i < 30; i++)
            {
                _locomotion.Tick(input, 0.016f);
            }

            Assert.AreEqual(30.0f, _locomotion.transform.eulerAngles.y, 0.01f,
                "the body turned to face its strafe direction instead of the target");
        }

        /// <summary>Aiming down sights trades speed for a steady aim, as every third-person shooter does.</summary>
        [Test]
        public void AimingWalksSlowerThanNotAiming()
        {
            var walking = new LocomotionInput { Move = new Vector2(0.0f, 1.0f) };
            for (int i = 0; i < 180; i++)
            {
                _locomotion.Tick(walking, 1.0f / 60.0f);
            }
            float walkingSpeed = _locomotion.CurrentSpeed;

            var aiming = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Aiming = true };
            for (int i = 0; i < 180; i++)
            {
                _locomotion.Tick(aiming, 1.0f / 60.0f);
            }

            Assert.AreEqual(_locomotion.WalkSpeed, walkingSpeed, 0.05f, "setup: plain walking is not WalkSpeed");
            Assert.AreEqual(_locomotion.WalkSpeed * _locomotion.AimMoveSpeedMultiplier, _locomotion.CurrentSpeed, 0.05f);
            Assert.Less(_locomotion.CurrentSpeed, walkingSpeed - 0.1f, "aiming did not slow the walk");
        }

        [Test]
        public void TheAimSlowdownIsTunableAndOneMeansNone()
        {
            _locomotion.AimMoveSpeedMultiplier = 1.0f;
            var aiming = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Aiming = true };
            for (int i = 0; i < 180; i++)
            {
                _locomotion.Tick(aiming, 1.0f / 60.0f);
            }

            Assert.AreEqual(_locomotion.WalkSpeed, _locomotion.CurrentSpeed, 0.05f);
        }

        [Test]
        public void NotLockedOnStandingStillLeavesTheFacingAlone()
        {
            _locomotion.transform.rotation = Quaternion.Euler(0.0f, 45.0f, 0.0f);

            _locomotion.Tick(new LocomotionInput { Move = Vector2.zero, YawDegrees = 120.0f }, 0.016f);

            Assert.AreEqual(45.0f, _locomotion.transform.eulerAngles.y, 0.01f,
                "looking around while standing still must not spin the body (only lock-on does that)");
        }

        [Test]
        public void SideSteppingTurnsTheLeadLegTowardTheStepMoreThanTheOther()
        {
            // Walking right (A/D), the right leg leads: it turns toward the right more than the left leg does, so the legs open and do not cross.
            PlayerLocomotion.StrafeLegYaw(new Vector2(3.0f, 0.0f), out float left, out float right);
            Assert.AreEqual(PlayerLocomotion.StrafeLeadFootDegrees, right, 0.01f);
            Assert.AreEqual(PlayerLocomotion.StrafeTrailFootDegrees, left, 0.01f);
            Assert.Greater(left, 0.0f, "both legs turn the way the body goes");

            PlayerLocomotion.StrafeLegYaw(new Vector2(-3.0f, 0.0f), out left, out right);
            Assert.AreEqual(-PlayerLocomotion.StrafeLeadFootDegrees, left, 0.01f, "walking left the left leg leads");
            Assert.AreEqual(-PlayerLocomotion.StrafeTrailFootDegrees, right, 0.01f);

            PlayerLocomotion.StrafeLegYaw(new Vector2(0.0f, 4.0f), out left, out right);
            Assert.AreEqual(0.0f, left, 1e-4f, "walking straight ahead turns nothing");
            Assert.AreEqual(0.0f, right, 1e-4f);

            PlayerLocomotion.StrafeLegYaw(Vector2.zero, out left, out right);
            Assert.AreEqual(0.0f, left, 1e-4f, "standing still turns nothing");
            Assert.AreEqual(0.0f, right, 1e-4f);

            PlayerLocomotion.StrafeLegYaw(new Vector2(2.0f, 2.0f), out left, out right);
            Assert.That(right, Is.InRange(1.0f, PlayerLocomotion.StrafeLeadFootDegrees - 1.0f), "a diagonal gets part of the turn");
        }
    }
}
