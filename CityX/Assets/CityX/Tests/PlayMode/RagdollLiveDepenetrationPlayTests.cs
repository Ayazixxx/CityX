using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT ASK: "do what you can to avoid ragdolls from getting stuck
    /// in walls/floor." Ragdoll.DepenetrateFromWorld (see JetpackWallCrashTests)
    /// only ever runs ONCE, at the instant Activate hands the rig to live
    /// physics -- it cannot catch a body that tunnels or gets wedged into
    /// something AFTER it is already tumbling/settling, which is exactly
    /// what this file drives: activate somewhere clear, THEN move a real
    /// wall into the already-live torso, and prove Ragdoll's own real
    /// FixedUpdate loop (TickLiveDepenetration) clears it without any
    /// external help, the same way a real crash-then-slide-into-a-corner
    /// would need to be cleared automatically.
    /// </summary>
    public sealed class RagdollLiveDepenetrationPlayTests
    {
        private GameObject _player;
        private GameObject _wall;
        private Ragdoll _ragdoll;
        private Rigidbody _torso;

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

            _ragdoll = _player.AddComponent<Ragdoll>();
            _ragdoll.Bodies = new[] { _torso };
            _ragdoll.Torso = _torso;
            _ragdoll.Bind();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_player);
            if (_wall != null)
            {
                Object.Destroy(_wall);
            }
        }

        [UnityTest]
        public IEnumerator ClearsAnOverlapThatAppearsAfterTheRigIsAlreadyLive()
        {
            // Activated somewhere with nothing nearby -- DepenetrateFromWorld's
            // own one-time pass at Activate has nothing to do here, so any
            // clearing seen below can only be TickLiveDepenetration's own,
            // ongoing work.
            _ragdoll.Activate(Vector3.zero);

            // A wall dropped directly on top of the NOW-LIVE torso -- standing
            // in for a body that tumbles into a corner, or a limb a joint
            // swings through a railing, sometime after it is already
            // simulating, which the one-time Activate-time pass could never
            // have seen coming.
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _torso.transform.position;
            _wall.transform.localScale = Vector3.one * 3.0f;

            Bounds wallBoundsBefore = _wall.GetComponent<Collider>().bounds;
            Bounds torsoBoundsBefore = _torso.GetComponent<Collider>().bounds;
            Assert.IsTrue(wallBoundsBefore.Intersects(torsoBoundsBefore),
                "setup failed -- the torso was never actually overlapping the wall");

            // Generous but bounded -- real gameplay needs this to resolve in
            // a handful of steps, not eventually.
            bool cleared = false;
            for (int step = 0; step < 120 && !cleared; step++)
            {
                yield return new WaitForFixedUpdate();
                Bounds wallBounds = _wall.GetComponent<Collider>().bounds;
                Bounds torsoBounds = _torso.GetComponent<Collider>().bounds;
                cleared = !wallBounds.Intersects(torsoBounds);
            }

            Assert.IsTrue(cleared,
                "the torso is still overlapping a wall that appeared after it went live -- " +
                "TickLiveDepenetration never cleared it");
        }

        /// <summary>
        /// REGRESSION for a bug the fix above shipped THE SAME DAY: "my guy
        /// is like clipping through the floor making him shake and its
        /// making it so i cant get up." TickLiveDepenetration originally
        /// corrected ANY positive penetration distance, however
        /// microscopic -- but a body resting on an ordinary floor under
        /// ExtraGravityScale's extra downward push is not perfectly
        /// zero-penetration every single frame, so it kept getting shoved
        /// up by a few centimetres, fell back down, got shoved up again,
        /// forever. That continuous shove also kept the torso's own
        /// linearVelocity above RestVelocityThreshold permanently, so
        /// CanStandUp could never become true. See
        /// Ragdoll.DepenetrationMinDistance's own doc comment for the fix.
        ///
        /// A REAL FALL ONTO A REAL FLOOR, not a pre-set overlap -- the
        /// whole point is to reach the exact kind of solver-settled resting
        /// contact PhysX actually produces, not a synthetic stand-in for it.
        /// </summary>
        [UnityTest]
        public IEnumerator ARagdollRestingOnAnOrdinaryFloorCanStillStandUpEventually()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Floor";
            _wall.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _wall.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);

            _player.transform.position = new Vector3(0.0f, 2.0f, 0.0f);
            _ragdoll.Activate(Vector3.zero);

            // Real physics falling/landing, PLUS Ragdoll.Tick every step --
            // exactly what PlayerInputReader's own ragdoll branch does every
            // frame while limp -- so this reaches CanStandUp the same way a
            // real play session would. Generous but bounded: falling from
            // 2m plus a full StandUpRestSeconds (2s) of genuine rest is
            // comfortably under 200 fixed steps; 400 leaves real headroom
            // without letting a real regression hang the test forever.
            for (int step = 0; step < 400 && !_ragdoll.CanStandUp; step++)
            {
                _ragdoll.Tick(Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_ragdoll.CanStandUp,
                "a body resting on an ordinary floor never became eligible to stand up -- " +
                "ordinary resting contact is being treated as \"stuck\"");
        }
    }
}
