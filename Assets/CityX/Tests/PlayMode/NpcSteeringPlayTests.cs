using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Obstacle avoidance, against real colliders.
    ///
    /// PLAYMODE BECAUSE IT RAYCASTS. Every decision this makes comes from
    /// Physics.Raycast, and an EditMode test has no physics scene for those to
    /// hit -- every cast would come back clear and the whole thing would look
    /// like it worked.
    /// </summary>
    public sealed class NpcSteeringPlayTests
    {
        private GameObject _wall;
        private GameObject _body;
        private NpcSteering _steering;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Walker");
            _body.transform.position = Vector3.zero;
            _steering = new NpcSteering();
            _steering.Configure(0.4f, 1.8f, 0.4f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
            if (_wall != null)
            {
                Object.DestroyImmediate(_wall);
            }
        }

        private void WallAt(Vector3 centre, Vector3 size)
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = centre;
            _wall.transform.localScale = size;
        }

        /// <summary>
        /// Open ground has to come back untouched. Every NPC in the game runs
        /// through this every frame, and one that quietly bent a straight line
        /// would make all of them walk in curves for no reason.
        /// </summary>
        [UnityTest]
        public IEnumerator OpenGroundIsLeftExactlyAlone()
        {
            yield return null;

            Vector3 steered = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.AreEqual(Vector3.forward.x, steered.x, 0.001f);
            Assert.AreEqual(Vector3.forward.z, steered.z, 0.001f);
            Assert.IsFalse(_steering.IsAvoiding);
        }

        [UnityTest]
        public IEnumerator AWallAheadTurnsTheWalkerAside()
        {
            WallAt(new Vector3(0.0f, 2.0f, 3.0f), new Vector3(12.0f, 4.0f, 1.0f));
            yield return new WaitForFixedUpdate();

            Vector3 steered = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsTrue(_steering.IsAvoiding, "the wall was not noticed at all");
            Assert.Greater(Mathf.Abs(steered.x), 0.3f,
                "it is still heading straight into the wall");
        }

        /// <summary>
        /// THE JITTER TEST, and the reason the commitment window exists. An NPC
        /// square on to a flat wall re-picks left or right every frame as its
        /// own tiny movements flip which tangent scores better, and it shakes in
        /// place instead of going anywhere. The side has to hold.
        /// </summary>
        [UnityTest]
        public IEnumerator ItCommitsToOneSideRatherThanJittering()
        {
            WallAt(new Vector3(0.0f, 2.0f, 3.0f), new Vector3(12.0f, 4.0f, 1.0f));
            yield return new WaitForFixedUpdate();

            float first = Mathf.Sign(
                _steering.Steer(_body.transform, Vector3.forward, 0.016f).x);

            for (int i = 0; i < 30; i++)
            {
                // Nudged a little each step, which is what makes a
                // frame-by-frame decision flip in the first place.
                _body.transform.position += new Vector3(0.01f, 0.0f, 0.0f);
                Vector3 steered = _steering.Steer(_body.transform, Vector3.forward, 0.016f);
                Assert.AreEqual(first, Mathf.Sign(steered.x),
                    $"the chosen side flipped on step {i}");
            }
        }

        /// <summary>
        /// And it lets go the moment the way is clear, rather than tracing the
        /// rest of the building. Without this an NPC rounds a corner and keeps
        /// following the wall away from where it was going.
        /// </summary>
        [UnityTest]
        public IEnumerator ItPeelsOffAsSoonAsTheWayIsClear()
        {
            WallAt(new Vector3(0.0f, 2.0f, 3.0f), new Vector3(12.0f, 4.0f, 1.0f));
            yield return new WaitForFixedUpdate();

            _steering.Steer(_body.transform, Vector3.forward, 0.016f);
            Assert.IsTrue(_steering.IsAvoiding);

            Object.DestroyImmediate(_wall);
            _wall = null;
            yield return new WaitForFixedUpdate();

            Vector3 steered = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsFalse(_steering.IsAvoiding, "it kept avoiding a wall that is gone");
            Assert.AreEqual(1.0f, steered.z, 0.001f);
        }

        /// <summary>
        /// The probe sits at chest height on purpose. Along the floor it would
        /// hit the kerb every street in this city has and read a 6cm step as a
        /// wall -- which would leave every NPC refusing to cross a road.
        /// </summary>
        [UnityTest]
        public IEnumerator AKerbIsNotAWall()
        {
            WallAt(new Vector3(0.0f, 0.03f, 3.0f), new Vector3(12.0f, 0.06f, 1.0f));
            yield return new WaitForFixedUpdate();

            Vector3 steered = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsFalse(_steering.IsAvoiding, "a kerb was treated as a wall");
            Assert.AreEqual(1.0f, steered.z, 0.001f);
        }

        [UnityTest]
        public IEnumerator AGoalOfNowhereIsHandedStraightBack()
        {
            yield return null;

            Vector3 steered = _steering.Steer(_body.transform, Vector3.zero, 0.016f);

            Assert.AreEqual(Vector3.zero, steered);
        }
    }
}
