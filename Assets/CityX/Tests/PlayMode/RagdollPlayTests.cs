using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What the ragdoll actually does when it is switched on.
    ///
    /// These run the real solver against a real floor, because every way a
    /// ragdoll fails is a physics behaviour rather than a wrong field: it
    /// explodes on the first step, or sinks through the ground, or never
    /// settles. None of those are visible in the prefab.
    /// </summary>
    public class RagdollPlayTests
    {
        private GameObject _floor;
        private GameObject _body;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Floor";
            _floor.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _floor.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            // Scale is applied to the transform, not the collider, until the
            // physics scene is told. Without this the floor is still a 1x1x1
            // cube and everything falls past it.
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_body != null) Object.DestroyImmediate(_body);
            if (_floor != null) Object.DestroyImmediate(_floor);
        }

        /// <summary>
        /// A stand-in rig: three boxes in a chain, jointed and limited the same
        /// way the real one is. The real rig needs the imported skeleton, which
        /// a PlayMode test cannot load; what is under test here is Ragdoll's own
        /// switching behaviour, and that is identical either way.
        /// </summary>
        private Ragdoll MakeRig(float height)
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0.0f, height, 0.0f);

            var bodies = new Rigidbody[3];
            Transform parent = _body.transform;
            Rigidbody previous = null;

            for (int i = 0; i < bodies.Length; i++)
            {
                var piece = new GameObject($"Piece {i}");
                piece.transform.SetParent(parent, false);
                piece.transform.localPosition = i == 0 ? Vector3.zero : new Vector3(0.0f, 0.4f, 0.0f);

                var box = piece.AddComponent<BoxCollider>();
                box.size = Vector3.one * 0.3f;

                var rigid = piece.AddComponent<Rigidbody>();
                // The torso is heaviest, as in the real rig.
                rigid.mass = i == 0 ? 30.0f : 8.0f;
                rigid.linearDamping = Ragdoll.LinearDamping;
                rigid.angularDamping = Ragdoll.AngularDamping;
                rigid.isKinematic = true;
                rigid.maxDepenetrationVelocity = 3.0f;

                if (previous != null)
                {
                    var joint = piece.AddComponent<CharacterJoint>();
                    joint.connectedBody = previous;
                    joint.swing1Limit = new SoftJointLimit { limit = 40.0f };
                    joint.swing2Limit = new SoftJointLimit { limit = 40.0f };
                    joint.lowTwistLimit = new SoftJointLimit { limit = -20.0f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 20.0f };
                    joint.enablePreprocessing = false;
                }

                bodies[i] = rigid;
                previous = rigid;
                parent = piece.transform;
            }

            var ragdoll = _body.AddComponent<Ragdoll>();
            ragdoll.Bodies = bodies;
            ragdoll.Torso = bodies[0];
            // Awake already ran at AddComponent with no bodies set, so the
            // collision-ignore pass has to be redone now they exist. Disabling
            // and re-enabling is the supported way to make that happen.
            _body.SetActive(false);
            _body.SetActive(true);
            return ragdoll;
        }

        private static IEnumerator Simulate(float seconds)
        {
            // Stepped rather than measured in frames: batch mode runs far
            // faster than realtime, so counting frames simulates almost no
            // time at all and every fall test would pass trivially.
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator AKinematicRigDoesNotMove()
        {
            Ragdoll ragdoll = MakeRig(4.0f);
            Vector3 before = ragdoll.Torso.position;

            yield return Simulate(1.0f);

            Assert.AreEqual(before.y, ragdoll.Torso.position.y, 0.001f,
                "the rig fell before anything activated it -- it would collapse on spawn");
            Assert.IsFalse(ragdoll.IsLimp);
        }

        [UnityTest]
        public IEnumerator GoingLimpFallsAndComesToRestOnTheGround()
        {
            Ragdoll ragdoll = MakeRig(4.0f);
            ragdoll.Activate(Vector3.zero);
            Assert.IsTrue(ragdoll.IsLimp);

            yield return Simulate(4.0f);

            float restingY = ragdoll.Torso.position.y;
            Assert.Less(restingY, 4.0f, "the rig never fell");
            Assert.Greater(restingY, -0.5f,
                "the rig sank through the floor instead of landing on it");

            // Settled, not still tumbling.
            yield return Simulate(1.0f);
            Assert.AreEqual(restingY, ragdoll.Torso.position.y, 0.15f,
                "the rig is still moving a second after it should have settled");
        }

        /// <summary>
        /// The bug this guards against is the loud one: bodies that start
        /// overlapping at a joint get pushed apart on the first step and the
        /// corpse fires off down the street. It is why the rig ignores its own
        /// collisions and caps depenetration velocity.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRigDoesNotExplodeOnItsFirstStep()
        {
            Ragdoll ragdoll = MakeRig(4.0f);
            Vector3 before = ragdoll.Torso.position;

            ragdoll.Activate(Vector3.zero);
            yield return Simulate(0.2f);

            float travelled = Vector3.Distance(before, ragdoll.Torso.position);
            Assert.Less(travelled, 1.0f,
                $"the rig moved {travelled:0.00}m in a fifth of a second -- it was flung apart " +
                "rather than dropped");

            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.Less(body.linearVelocity.magnitude, 20.0f,
                    $"'{body.name}' is travelling at {body.linearVelocity.magnitude:0} m/s");
            }
        }

        [UnityTest]
        public IEnumerator TheBodyCarriesItsMomentumIntoTheFall()
        {
            Ragdoll ragdoll = MakeRig(4.0f);
            ragdoll.Activate(new Vector3(6.0f, 0.0f, 0.0f));

            yield return Simulate(0.5f);

            Assert.Greater(ragdoll.Torso.position.x, 1.0f,
                "a body killed while running should keep going, not drop straight down");
        }

        [UnityTest]
        public IEnumerator OnlyTheTorsoIsSpunSoTheRigTopplesRatherThanFlails()
        {
            Ragdoll ragdoll = MakeRig(4.0f);
            ragdoll.Activate(Vector3.zero);

            // Read on the same frame, before the joints have dragged anything.
            float torsoSpin = ragdoll.Torso.angularVelocity.magnitude;
            Assert.Greater(torsoSpin, 0.1f, "the torso got no spin, so the body drops like a plank");

            for (int i = 1; i < ragdoll.Bodies.Length; i++)
            {
                Assert.AreEqual(0.0f, ragdoll.Bodies[i].angularVelocity.magnitude, 0.001f,
                    $"'{ragdoll.Bodies[i].name}' was given its own spin -- several unrelated " +
                    "rotations starting at once read as the rig exploding, not as going limp");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASecondKillingBlowDoesNotRelaunchTheCorpse()
        {
            Ragdoll ragdoll = MakeRig(4.0f);
            ragdoll.Activate(Vector3.zero);
            yield return Simulate(2.0f);

            Vector3 resting = ragdoll.Torso.position;
            ragdoll.Activate(new Vector3(0.0f, 50.0f, 0.0f));
            yield return Simulate(0.3f);

            Assert.Less(ragdoll.Torso.position.y, resting.y + 1.0f,
                "activating an already-limp ragdoll launched it into the air");
        }

        [UnityTest]
        public IEnumerator StandingBackUpReturnsControlToTheAnimation()
        {
            Ragdoll ragdoll = MakeRig(4.0f);
            ragdoll.Activate(Vector3.zero);
            yield return Simulate(1.0f);

            ragdoll.Deactivate();
            Assert.IsFalse(ragdoll.IsLimp);
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.IsTrue(body.isKinematic, $"'{body.name}' is still live after standing up");
            }

            Vector3 after = ragdoll.Torso.position;
            yield return Simulate(1.0f);
            Assert.AreEqual(after.y, ragdoll.Torso.position.y, 0.001f,
                "the rig kept falling after being stood back up");
        }
    }
}
