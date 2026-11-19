using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A cut-off part or a chunk of gore lying still stops being simulated (GoreChunk), and wakes and falls again when what it lay on goes
    /// (owner, 2026-11-19, with a picture of an arm hanging in the air above some corpses: "some of the dismemberment parts freeze in mid
    /// air ... they should have physics"). One lying on the ground stays put.
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
        private GoreChunk Piece(Vector3 at)
        {
            var piece = new GameObject("Arm");
            piece.transform.position = at;
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
            Assert.IsTrue(onTop.GetComponent<Rigidbody>().isKinematic, "a resting piece is still simulated: it costs every frame");

            Object.Destroy(corpse);
            yield return Until(GoreChunk.SupportCheckSeconds + 0.3f, () => !onTop.Resting);
            Assert.IsFalse(onTop.Resting, "what it lay on went and the piece stayed frozen in the air");
            yield return Until(3.0f, () => onTop.Resting);
            Assert.IsTrue(onTop.Resting, "the fallen piece never came to rest again");
            Assert.Less(onTop.transform.position.y, 0.2f, "the piece did not fall to the ground");

            Assert.IsTrue(onTheGround.Resting, "a piece on the ground woke with nothing gone from under it");
            Assert.AreEqual(0.06f, onTheGround.transform.position.y, 0.03f, "the piece on the ground moved");
        }
    }
}
