using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT: "make it so my limbs cant clip through walls
    /// and get stuck when im flying around with jetpack or swinging
    /// around with grapple." Neither state ever ragdolls, so
    /// Ragdoll.TickKinematicLimbClearance is the only thing that ever
    /// pulls a still-kinematic, animator-driven limb back out of world
    /// geometry it swung/flew into -- see its own doc comment for the
    /// full mechanism. These tests exercise the method directly, the same
    /// way RagdollLiveDepenetrationPlayTests exercises TickLiveDepenetration
    /// directly, rather than through a specific jetpack/swing scenario
    /// (JetpackWallCrashTests covers that integration separately).
    /// </summary>
    public sealed class RagdollKinematicLimbClearanceTests
    {
        private GameObject _player;
        private GameObject _wall;
        private Ragdoll _ragdoll;
        private Rigidbody _torso;
        private Rigidbody _arm;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _player.AddComponent<PlayerLocomotion>();
            _player.AddComponent<PlayerVitals>();

            var torsoObject = new GameObject("Torso");
            torsoObject.transform.SetParent(_player.transform, false);
            _torso = torsoObject.AddComponent<Rigidbody>();
            torsoObject.AddComponent<BoxCollider>();

            // A SECOND body offset from the torso, standing in for an
            // outstretched arm -- the same "torso plus one limb reaching
            // ahead" shape JetpackWallCrashTests uses, since a single body
            // sitting exactly at the root can't distinguish "the whole rig
            // moved" from "just that one body was checked."
            var armObject = new GameObject("Arm");
            armObject.transform.SetParent(_player.transform, false);
            armObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.8f);
            _arm = armObject.AddComponent<Rigidbody>();
            armObject.AddComponent<CapsuleCollider>();

            _ragdoll = _player.AddComponent<Ragdoll>();
            _ragdoll.Bodies = new[] { _torso, _arm };
            _ragdoll.Torso = _torso;
            _ragdoll.Bind();

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            if (_wall != null)
            {
                Object.DestroyImmediate(_wall);
            }
        }

        [Test]
        public void ClearsAnArmEmbeddedInAWallWithoutEverRagdolling()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _arm.transform.position;
            _wall.transform.localScale = Vector3.one * 3.0f;
            Physics.SyncTransforms();

            Bounds wallBounds = _wall.GetComponent<Collider>().bounds;
            Assert.IsTrue(wallBounds.Intersects(_arm.GetComponent<Collider>().bounds),
                "setup failed -- the arm was never actually embedded in the first place");

            bool cleared = false;
            for (int step = 0; step < 60 && !cleared; step++)
            {
                _ragdoll.TickKinematicLimbClearance();
                Physics.SyncTransforms();
                wallBounds = _wall.GetComponent<Collider>().bounds;
                cleared = !wallBounds.Intersects(_torso.GetComponent<Collider>().bounds) &&
                          !wallBounds.Intersects(_arm.GetComponent<Collider>().bounds);
            }

            Assert.IsTrue(cleared,
                "the arm is still overlapping the wall after repeated TickKinematicLimbClearance " +
                "calls -- a flying/swinging limb can still clip through and get stuck");
            Assert.IsFalse(_ragdoll.IsLimp,
                "clearing the overlap should never have ragdolled the character");
        }

        [Test]
        public void DoesNothingWhenNothingOverlaps()
        {
            // Well clear of the rig -- ordinary proximity (standing near a
            // wall, not embedded in it) must never trigger a correction,
            // the same false-positive guard DepenetrationMinDistance
            // already protects TickLiveDepenetration with.
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _arm.transform.position + new Vector3(0.0f, 0.0f, 10.0f);
            _wall.transform.localScale = Vector3.one * 3.0f;
            Physics.SyncTransforms();

            Vector3 before = _player.transform.position;
            _ragdoll.TickKinematicLimbClearance();

            Assert.AreEqual(before, _player.transform.position,
                "the rig moved even though nothing was actually overlapping");
        }

        [Test]
        public void DoesNothingWhileAlreadyRagdolled()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _arm.transform.position;
            _wall.transform.localScale = Vector3.one * 3.0f;
            Physics.SyncTransforms();

            // Activate's own DepenetrateFromWorld already ran once here --
            // TickKinematicLimbClearance must not ALSO try to correct the
            // now-live rig; that overlap is TickLiveDepenetration's job,
            // and having both move things at once would have them fight.
            _ragdoll.Activate(Vector3.zero);
            Physics.SyncTransforms();
            Vector3 afterActivate = _player.transform.position;

            _ragdoll.TickKinematicLimbClearance();

            Assert.AreEqual(afterActivate, _player.transform.position,
                "TickKinematicLimbClearance moved the rig while already ragdolled");
        }
    }
}
