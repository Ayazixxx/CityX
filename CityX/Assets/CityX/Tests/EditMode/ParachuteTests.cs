using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The parachute's own toggle/auto-close/no-fall-damage rules, driven
    /// through the real component with a real CharacterController underneath
    /// -- same technique JetpackTests already establishes for testing
    /// movement without a scene. It refreshes PlayerVitals' own
    /// fall-tracking peak every open frame so a landing only ever measures
    /// the drop since it was last open, never the whole original fall.
    ///
    /// NO ANIMATOR ON THIS FIXTURE, DELIBERATELY -- same reasoning
    /// JetpackTests' own doc comment gives: Parachute.OnEquip's spine lookup
    /// tolerates a missing Animator, and PackVisual/CanopyVisual are left
    /// null here on purpose (SetCanopyVisible already no-ops on null), which
    /// is what makes the underlying mechanic testable without a real rig.
    ///
    /// THE ACTUAL FALL-SPEED CLAMP IS NOT TESTED HERE -- see
    /// ParachuteFallPlayTests instead. This bare rig has no ground collider
    /// at all, and CharacterController.isGrounded is not reliable without
    /// one (confirmed directly: a bare CharacterController that has never
    /// contacted anything still reports grounded here, which pins
    /// PlayerLocomotion's own "grounded &amp;&amp; verticalVelocity &lt;= 0"
    /// snap-to-ground rule at exactly -2.0 every tick regardless of height --
    /// both an open and a never-opened parachute landed on that same -2.0,
    /// proving this rig cannot tell the two apart). PlayMode genuinely
    /// simulates physics each frame, so a real ground far below actually
    /// leaves it airborne while high up.
    /// </summary>
    public sealed class ParachuteTests
    {
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private GameObject _parachuteObject;
        private Parachute _parachute;

        private const float Step = 1.0f / 60.0f;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _locomotion = _player.AddComponent<PlayerLocomotion>();
            _vitals = _player.AddComponent<PlayerVitals>();
            _locomotion.Bind();

            _parachuteObject = new GameObject("Parachute");
            _parachute = _parachuteObject.AddComponent<Parachute>();
            _parachute.OnEquip(_player, ItemInstanceData.None);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_parachuteObject);
        }

        /// <summary>Requested: "press space and open and close it."</summary>
        [Test]
        public void APressTogglesItOpenAndASecondPressTogglesItClosed()
        {
            Assert.IsFalse(_parachute.IsOpen, "should start packed away");

            _parachute.Tick(toggledThisFrame: true, grounded: false, deltaTime: Step);
            Assert.IsTrue(_parachute.IsOpen, "the first press should have deployed it");

            _parachute.Tick(toggledThisFrame: true, grounded: false, deltaTime: Step);
            Assert.IsFalse(_parachute.IsOpen, "the second press should have packed it away again");
        }

        /// <summary>
        /// Matches a real glider's canopy collapsing the instant your feet
        /// are down -- landing should not require a manual close first.
        /// </summary>
        [Test]
        public void LandingWhileOpenClosesItEvenWithoutATogglePress()
        {
            _parachute.Tick(toggledThisFrame: true, grounded: false, deltaTime: Step);
            Assert.IsTrue(_parachute.IsOpen, "setup failed -- never actually opened");

            _parachute.Tick(toggledThisFrame: false, grounded: true, deltaTime: Step);

            Assert.IsFalse(_parachute.IsOpen, "landing with the canopy out should auto-close it");
        }

        /// <summary>
        /// The whole point of the explicit ask read together with a real
        /// glider: opening near the ground after a long fall should land
        /// safely, the same way ReleasingCloseToTheGroundAfterAHighClimbTakesNoFallDamage
        /// proves it for the jetpack's own identical ResetFallTracking call.
        /// </summary>
        [Test]
        public void OpeningCloseToTheGroundAfterALongFallTakesNoFallDamage()
        {
            float healthBefore = _vitals.Health;

            // A long, genuine unpowered fall being tracked the ordinary way.
            _vitals.TickFallDamage(grounded: false, currentY: 100.0f);

            // Opened only a few metres above the ground -- this is the last
            // reset before landing, and it is what "opens it near the
            // ground" actually means: the 100m fall behind it is
            // irrelevant, only the drop from here on counts.
            _player.transform.position = new Vector3(0.0f, 2.0f, 0.0f);
            _parachute.Tick(toggledThisFrame: true, grounded: false, deltaTime: Step);

            // A genuine 2m drop to the ground under the open canopy, well
            // inside any reasonable safe-fall distance.
            _vitals.TickFallDamage(grounded: false, currentY: 2.0f);
            float damage = _vitals.TickFallDamage(grounded: true, currentY: 0.0f);

            Assert.AreEqual(0.0f, damage, "a safe landing under an open parachute still took fall damage");
            Assert.AreEqual(healthBefore, _vitals.Health,
                "health dropped even though the landing should have been safe");
        }
    }
}
