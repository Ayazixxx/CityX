using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT ASK: "press space and open... it slows down your fall
    /// pretty basic." Needs real PlayMode physics, not the bare EditMode rig
    /// ParachuteTests otherwise uses -- see that file's own class doc
    /// comment for why: a CharacterController with no ground collider at all
    /// reports isGrounded true regardless of height, which pins
    /// PlayerLocomotion's own ground-snap rule at a fixed -2.0 every tick
    /// and hides any real difference between an open and a closed canopy.
    /// Built from primitives with a real ground far below, mirroring
    /// PlayerLocomotionPlayTests' own exact reasoning for doing the same
    /// thing to test falling in the first place.
    /// </summary>
    public sealed class ParachuteFallPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private GameObject _parachuteObject;
        private Parachute _parachute;

        private const float Step = 1.0f / 60.0f;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            // Far below rather than at 0 -- both tests here drop the player
            // from 50m and only run a second of simulated fall, nowhere
            // near reaching it, so this just guarantees isGrounded reads
            // false the whole time instead of depending on exactly how far
            // a drop from 50m gets in 30 steps.
            _ground.transform.position = new Vector3(0.0f, -500.0f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            _player = new GameObject("Player");
            _player.transform.position = new Vector3(0.0f, 50.0f, 0.0f);
            _player.AddComponent<CharacterController>();
            _locomotion = _player.AddComponent<PlayerLocomotion>();
            _player.AddComponent<PlayerVitals>();

            _parachuteObject = new GameObject("Parachute");
            _parachute = _parachuteObject.AddComponent<Parachute>();
            _parachute.OnEquip(_player, ItemInstanceData.None);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
            if (_parachuteObject != null) Object.DestroyImmediate(_parachuteObject);
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        [UnityTest]
        public IEnumerator OpeningWhileFallingClampsTheDescentNearTheStatedSpeed()
        {
            _parachute.Tick(toggledThisFrame: true, grounded: false, deltaTime: Step);
            _locomotion.Tick(default, Step);
            yield return null;

            for (int i = 0; i < 29; i++)
            {
                _parachute.Tick(toggledThisFrame: false, grounded: false, deltaTime: Step);
                _locomotion.Tick(default, Step);
                yield return null;
            }

            Assert.IsFalse(_locomotion.IsGrounded, "setup failed -- the body reached the ground already");
            Assert.Less(_locomotion.Velocity.y, -Parachute.OpenFallSpeed * 0.5f,
                "an open parachute should still be descending at a meaningful clip, not hovering");
            Assert.Greater(_locomotion.Velocity.y, -(Parachute.OpenFallSpeed + 2.0f),
                $"an open parachute let the body fall at {_locomotion.Velocity.y:0.0} m/s -- " +
                "nowhere near the slow, controlled descent that was asked for");
        }

        /// <summary>
        /// The control case, proving the clamp above isn't just how fast
        /// this rig always falls -- the exact same drop with the canopy
        /// never opened should fall much faster under ordinary gravity.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutOpeningItTheSameDropFallsMuchFaster()
        {
            for (int i = 0; i < 30; i++)
            {
                _locomotion.Tick(default, Step);
                yield return null;
            }

            Assert.IsFalse(_locomotion.IsGrounded, "setup failed -- the body reached the ground already");
            // After half a second of real 9.8 m/s^2 gravity from rest,
            // v = g*t = -4.9 m/s -- comfortably past the clamped case's own
            // -4.0..-4.2 band, which is the actual claim here: unclamped
            // free fall is already faster than the parachute's clamp this
            // soon, not eventually.
            Assert.Less(_locomotion.Velocity.y, -(Parachute.OpenFallSpeed + 0.5f),
                "setup failed -- an unopened parachute should not be slowing the fall at all");
        }
    }
}
