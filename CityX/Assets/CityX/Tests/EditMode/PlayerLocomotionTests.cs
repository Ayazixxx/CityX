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
    }
}
