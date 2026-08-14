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
            _locomotion.Tick(Forward(sprint: true, crouch: true), 0.016f);

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
            _locomotion.Tick(Forward(crouch: true), 0.016f);
            float crouched = _locomotion.CurrentSpeed;

            _locomotion.Tick(Forward(), 0.016f);
            Assert.AreEqual(_locomotion.CurrentSpeed, crouched, 0.001f);
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

            _locomotion.Tick(default, 0.016f);
            Assert.AreEqual(0.0f, _locomotion.CurrentSpeed, 0.001f,
                "the body kept the dash speed after the burst ended");
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
