using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A cut-off part or a chunk of gore keeps its physics until it is taken away (GoreChunk; owner, 2026-11-26: "keep physics working for each
    /// dismembered limb"): lying still it sleeps, still simulated, and falls again when what it lay on goes, wholly or under one end only (owner,
    /// 2026-11-19, of an arm hanging in the air above some corpses, and 2026-11-26: "arms at angles where its obvious the physics have stopped
    /// working and they are floating"). One lying on the ground stays put, and people walk through pieces.
    /// </summary>
    public sealed class GoreChunkRestPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private GameObject Box(Vector3 at, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = at;
            box.transform.localScale = size;
            _made.Add(box);
            return box;
        }

        /// <summary>A piece as Dismemberer.Launch makes one: a box round it, a rigid body, a GoreChunk.</summary>
        private GoreChunk Piece(Vector3 at, Quaternion turned = default)
        {
            var piece = new GameObject("Arm");
            piece.transform.SetPositionAndRotation(at, turned == default ? Quaternion.identity : turned);
            piece.AddComponent<BoxCollider>().size = new Vector3(0.7f, 0.12f, 0.12f);
            var body = piece.AddComponent<Rigidbody>();
            body.mass = 3.0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            _made.Add(piece);
            GoreChunk chunk = piece.AddComponent<GoreChunk>();
            chunk.Bind(new GoreChunk.Smears(0), 1.0f);
            return chunk;
        }

        private static IEnumerator Until(float seconds, System.Func<bool> done)
        {
            for (float t = 0.0f; t < seconds && !done(); t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static void AssertSimulated(GoreChunk piece, string what)
        {
            Assert.IsFalse(piece.GetComponent<Rigidbody>().isKinematic, $"{what}: its physics are off");
            Assert.IsTrue(piece.GetComponent<Collider>().enabled, $"{what}: its collider is off");
        }

        [UnityTest]
        public IEnumerator APieceLyingOnSomethingThatGoesFallsToTheGroundAndOneOnTheGroundStaysPut()
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(40.0f, 1.0f, 40.0f));
            GameObject corpse = Box(new Vector3(0.0f, 0.6f, 0.0f), new Vector3(1.0f, 1.2f, 1.0f));   // what the arm comes to lie on
            GoreChunk onTop = Piece(new Vector3(0.0f, 1.6f, 0.0f));
            GoreChunk onTheGround = Piece(new Vector3(3.0f, 0.4f, 0.0f));
            yield return Until(4.0f, () => onTop.Resting && onTheGround.Resting);
            Assert.IsTrue(onTop.Resting && onTheGround.Resting, "setup: the pieces never came to rest");
            float restingAt = onTop.transform.position.y;
            Assert.Greater(restingAt, 1.1f, "setup: the arm is not lying on the box");
            AssertSimulated(onTop, "a piece lying still");

            Object.Destroy(corpse);
            yield return Until(3.0f, () => onTop.transform.position.y < 0.2f && onTop.Resting);
            Assert.Less(onTop.transform.position.y, 0.2f, "what it lay on went and the piece stayed in the air");
            Assert.IsTrue(onTop.Resting, "the fallen piece never came to rest again");

            Assert.IsTrue(onTheGround.Resting, "a piece on the ground woke with nothing gone from under it");
            Assert.AreEqual(0.06f, onTheGround.transform.position.y, 0.03f, "the piece on the ground moved");
            AssertSimulated(onTheGround, "a piece on the ground");
        }

        /// <summary>
        /// An arm left leaning, one end on a body and the other on the ground: the body goes, and the arm comes down to lie flat. Before, the
        /// ground under its low end counted as holding it up, and it hung at its angle in the air.
        /// </summary>
        [UnityTest]
        public IEnumerator AnArmLeaningOnSomethingThatGoesComesDownFlat()
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(40.0f, 1.0f, 40.0f));
            GameObject corpse = Box(new Vector3(0.45f, 0.2f, 0.0f), new Vector3(0.4f, 0.4f, 1.0f));   // a low body under one end
            GoreChunk arm = Piece(new Vector3(0.05f, 0.32f, 0.0f), Quaternion.Euler(0.0f, 0.0f, 28.0f));
            yield return Until(4.0f, () => arm.Resting);
            Assert.IsTrue(arm.Resting, "setup: the arm never came to rest");
            float leaning = Vector3.Angle(arm.transform.right, Vector3.ProjectOnPlane(arm.transform.right, Vector3.up));
            Assert.Greater(leaning, 10.0f, $"setup: the arm is not leaning (at {leaning:0} degrees)");

            Object.Destroy(corpse);
            yield return Until(3.0f, () => arm.Resting && Vector3.Angle(arm.transform.right, Vector3.ProjectOnPlane(arm.transform.right, Vector3.up)) < 5.0f);
            float lying = Vector3.Angle(arm.transform.right, Vector3.ProjectOnPlane(arm.transform.right, Vector3.up));
            Assert.Less(lying, 5.0f, $"the body under its end went and the arm stayed leaning at {lying:0} degrees in the air");
            Assert.Less(arm.GetComponent<Collider>().bounds.max.y, 0.25f, "the arm is not down on the ground");
        }

        /// <summary>People's capsules pass through pieces: nobody trips on a street of them or is lifted onto one.</summary>
        [UnityTest]
        public IEnumerator PeopleWalkThroughPieces()
        {
            var person = new GameObject("Person");
            _made.Add(person);
            CharacterController capsule = person.AddComponent<CharacterController>();
            yield return null;
            GoreChunk piece = Piece(new Vector3(5.0f, 0.4f, 0.0f));
            Assert.IsTrue(Physics.GetIgnoreCollision(piece.GetComponent<Collider>(), capsule), "a piece blocks people's capsules");
        }
    }
}
