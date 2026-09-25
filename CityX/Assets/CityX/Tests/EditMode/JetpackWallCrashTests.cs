using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, with a screenshot: "whenever i fly into a wall
    /// or window i just get stuck in it" -- an arm visibly embedded in a
    /// building wall after a jetpack crash. STILL true after a first
    /// attempt at this fix (a fixed PlayerInputReader.CrashPullBackDistance
    /// nudge applied before calling Activate) -- a guessed constant cannot
    /// know how far a limb actually tunnelled on any given hit.
    ///
    /// ROOT CAUSE: Ragdoll.Activate hands the rig's own colliders to live
    /// physics wherever the root transform currently sits. Character
    /// Controller.Move only ever stops the CAPSULE cleanly at a wall -- it
    /// has no idea an outstretched limb from whatever pose was playing
    /// (the jetpack's own Superman dive reaches an arm straight out, right
    /// at whatever was just hit) already extends well past that same
    /// surface. Handing that limb a real, live collider while it is still
    /// embedded is what leaves it stuck half inside the geometry instead of
    /// bouncing off it. Ragdoll.Activate now measures the REAL overlap
    /// itself with Physics.ComputePenetration (DepenetrateFromWorld) and
    /// pushes the whole rig back by exactly that depth -- this drives the
    /// REAL CharacterController into a REAL wall to prove that actually
    /// clears the rig, rather than trusting a fixed number.
    /// </summary>
    public sealed class JetpackWallCrashTests
    {
        private GameObject _player;
        private GameObject _wall;
        private GameObject _jetpackObject;
        private PlayerLocomotion _locomotion;
        private Ragdoll _ragdoll;
        private Jetpack _jetpack;
        private Rigidbody _arm;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _locomotion = _player.AddComponent<PlayerLocomotion>();
            _player.AddComponent<PlayerVitals>();
            _locomotion.Bind();

            _jetpackObject = new GameObject("Jetpack");
            _jetpack = _jetpackObject.AddComponent<Jetpack>();
            _jetpack.OnEquip(_player, ItemInstanceData.None);

            // A wide, thick wall directly ahead -- the same shape a
            // building facade presents, big enough that wherever the rig
            // actually hits it, it is still touching it.
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Wall";
            _wall.transform.position = new Vector3(0.0f, 0.0f, 20.0f);
            _wall.transform.localScale = new Vector3(20.0f, 20.0f, 1.0f);

            // A minimal rig standing in for the real one: a torso at the
            // root, and an "arm" offset forward from it the way a Superman
            // dive pose's own outstretched arm reaches ahead of the torso
            // -- the actual thing the screenshot showed embedded.
            var torsoObject = new GameObject("Torso");
            torsoObject.transform.SetParent(_player.transform, false);
            Rigidbody torso = torsoObject.AddComponent<Rigidbody>();
            torsoObject.AddComponent<CapsuleCollider>();

            var armObject = new GameObject("Arm");
            armObject.transform.SetParent(_player.transform, false);
            armObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.8f);
            _arm = armObject.AddComponent<Rigidbody>();
            armObject.AddComponent<CapsuleCollider>();

            _ragdoll = _player.AddComponent<Ragdoll>();
            _ragdoll.Bodies = new[] { torso, _arm };
            _ragdoll.Torso = torso;
            _ragdoll.Bind();

            // EditMode never runs a physics step on its own -- a freshly
            // positioned collider's transform has to be synced by hand
            // before CharacterController.Move can actually see it, the
            // same requirement RagdollTests' own real-rig test already
            // established for this exact environment.
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_wall);
            Object.DestroyImmediate(_jetpackObject);
        }

        /// <summary>
        /// Drives the REAL CharacterController into the REAL wall under
        /// full jetpack dive speed until Move() itself reports the hit --
        /// the same CollisionFlags.Sides signal PlayerInputReader's own
        /// crash detection reads.
        /// </summary>
        private void FlyTowardWallUntilBlocked()
        {
            const float step = 1.0f / 60.0f;
            var input = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                YawDegrees = 0.0f,
                JetpackBoosting = true,
                JetpackDiving = true,
            };
            for (int i = 0; i < 300; i++)
            {
                _jetpack.Tick(true, new Vector2(0.0f, 1.0f), true, step);
                _locomotion.Tick(input, step);
                if ((_locomotion.LastMoveCollisionFlags & CollisionFlags.Sides) != 0)
                {
                    return;
                }
            }
            Assert.Fail("never actually hit the wall -- setup is broken");
        }

        /// <summary>
        /// The control case, checked FIRST: driving into the wall really
        /// does leave the outstretched arm embedded in it, before Activate
        /// (and its own depenetration) ever runs -- proving the test below
        /// can actually catch the bug, not just pass regardless of what
        /// Ragdoll does.
        /// </summary>
        [Test]
        public void DrivingIntoTheWallReallyDoesEmbedTheArmBeforeActivating()
        {
            FlyTowardWallUntilBlocked();
            Physics.SyncTransforms();

            Bounds wallBounds = _wall.GetComponent<Collider>().bounds;
            Bounds armBounds = _arm.GetComponent<Collider>().bounds;
            Assert.IsTrue(wallBounds.Intersects(armBounds),
                "the control case is broken -- the arm was never actually embedded in the first place, " +
                "so the fix below isn't proven to do anything");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "make it so my limbs cant clip through
        /// walls and get stuck when im flying around with jetpack." A hard
        /// enough crash ragdolls and gets cleared by Activate's own
        /// DepenetrateFromWorld (see the test above) -- but plenty of real
        /// flying never crosses that threshold at all, and until now
        /// nothing ever corrected THAT case: the rig stays kinematic the
        /// whole time, so an embedded arm just sat there. Proves the fix
        /// (Ragdoll.TickKinematicLimbClearance, called every frame from
        /// PlayerInputReader while jetpackThrusting is true) clears the
        /// SAME embedding this file's control case above already confirms
        /// really happens -- without ever ragdolling at all.
        /// </summary>
        [Test]
        public void RepeatedlyFlyingIntoTheWallNeverLeavesTheArmEmbeddedWithoutEverRagdolling()
        {
            FlyTowardWallUntilBlocked();
            Physics.SyncTransforms();

            Bounds wallBounds = _wall.GetComponent<Collider>().bounds;
            bool cleared = false;
            for (int step = 0; step < 60 && !cleared; step++)
            {
                _ragdoll.TickKinematicLimbClearance();
                Physics.SyncTransforms();
                wallBounds = _wall.GetComponent<Collider>().bounds;
                cleared = true;
                foreach (Rigidbody body in _ragdoll.Bodies)
                {
                    if (wallBounds.Intersects(body.GetComponent<Collider>().bounds))
                    {
                        cleared = false;
                        break;
                    }
                }
            }

            Assert.IsTrue(cleared,
                "a body is still overlapping the wall after repeated TickKinematicLimbClearance calls -- " +
                "a flying limb can still clip through and get stuck without the rig ever ragdolling");
            Assert.IsFalse(_ragdoll.IsLimp,
                "clearing the embedding should never have ragdolled the character");
        }

        [Test]
        public void ActivatingAutomaticallyClearsTheRigOfTheWallItCrashedInto()
        {
            FlyTowardWallUntilBlocked();

            Vector3 awayFromWall = -_locomotion.Velocity;
            Assert.Greater(awayFromWall.sqrMagnitude, 0.0001f,
                "setup failed -- no real velocity to knock back along");

            // NO MANUAL PULL-BACK -- Ragdoll.Activate's own
            // DepenetrateFromWorld has to find and clear the real overlap
            // by itself now, with nothing external measuring it first.
            _ragdoll.Activate(awayFromWall.normalized * 9.0f);

            // EditMode never runs its own physics step -- a collider's own
            // .bounds is otherwise stale (still reporting wherever it sat
            // BEFORE 300 frames of flying moved the whole rig, confirmed
            // directly: it read back the original SetUp position until
            // this was added), which would make this pass or fail for the
            // wrong reason regardless of the real fix.
            Physics.SyncTransforms();

            Bounds wallBounds = _wall.GetComponent<Collider>().bounds;
            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                Bounds bodyBounds = body.GetComponent<Collider>().bounds;
                Assert.IsFalse(wallBounds.Intersects(bodyBounds),
                    $"'{body.name}' is still overlapping the wall after Activate -- " +
                    "DepenetrateFromWorld did not actually clear it");
            }
        }
    }

    /// <summary>
    /// EXPLICIT BUG REPORT: "i bugged out when running into the corner of
    /// the building with jetpack and got stuck into it instead ragdolling."
    /// See PlayerInputReader.NewWallContactAngleDegrees' own doc comment
    /// for the full root cause -- a plain "was I touching A wall last
    /// frame" bool cannot tell a corner's second face apart from
    /// continuing to touch the SAME wall, so a real, hard impact into the
    /// second face was silently swallowed by the edge-detection gate still
    /// armed from an earlier, weaker graze against the first face. Tests
    /// the extracted pure decision (PlayerInputReader.IsNewWallContact)
    /// directly against the exact geometric cases, rather than needing a
    /// real keyboard/jetpack/physics scene just to reach a few lines of
    /// vector math.
    /// </summary>
    public sealed class JetpackCornerCrashDetectionTests
    {
        [Test]
        public void NotTouchingAWallAtAllIsNeverANewContact()
        {
            Assert.IsFalse(PlayerInputReader.IsNewWallContact(
                hitWall: false, wasJetpackCrashing: true, Vector3.forward, Vector3.right));
        }

        [Test]
        public void TheVeryFirstWallTouchIsAlwaysANewContact()
        {
            Assert.IsTrue(PlayerInputReader.IsNewWallContact(
                hitWall: true, wasJetpackCrashing: false, Vector3.forward, Vector3.forward));
        }

        [Test]
        public void ContinuingToTouchTheSameWallIsNotANewContact()
        {
            Assert.IsFalse(PlayerInputReader.IsNewWallContact(
                hitWall: true, wasJetpackCrashing: true, Vector3.forward, Vector3.forward));
        }

        /// <summary>
        /// A flat static wall's own reported contact normal is not
        /// perfectly bit-identical frame to frame (solver noise) -- a
        /// small jitter must not be mistaken for a genuinely new wall.
        /// </summary>
        [Test]
        public void ASmallNormalJitterOnTheSameWallIsNotANewContact()
        {
            Quaternion jitter = Quaternion.Euler(0.0f, 10.0f, 0.0f);
            Assert.IsFalse(PlayerInputReader.IsNewWallContact(
                hitWall: true, wasJetpackCrashing: true, jitter * Vector3.forward, Vector3.forward));
        }

        /// <summary>THE ACTUAL BUG: a square building corner's second face sits 90 degrees from the first -- this must re-arm the crash check even though the edge-detector is still "true" from the first face.</summary>
        [Test]
        public void ABuildingCornersSecondFaceIsANewContactEvenWhileAlreadyCrashing()
        {
            Assert.IsTrue(PlayerInputReader.IsNewWallContact(
                hitWall: true, wasJetpackCrashing: true, Vector3.right, Vector3.forward));
        }

        [Test]
        public void ExactlyAtTheThresholdAngleIsNotYetANewContact()
        {
            Quaternion atThreshold = Quaternion.Euler(0.0f, PlayerInputReader.NewWallContactAngleDegrees, 0.0f);
            Assert.IsFalse(PlayerInputReader.IsNewWallContact(
                hitWall: true, wasJetpackCrashing: true, atThreshold * Vector3.forward, Vector3.forward));
        }
    }
}
