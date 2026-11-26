using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A blood stain keeps to what it lies on (owner, 2026-11-26: "i keep noticing blood stains floating mid air"): it moves as its surface
    /// moves, goes when it goes, and is never put on anything loose (a piece of gore, a body, a car) that would carry it off.
    /// </summary>
    public sealed class BloodSplatSurfacePlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            BloodSplats.Clear();
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

        private static List<BloodSplat> Splats() => new List<BloodSplat>(Object.FindObjectsByType<BloodSplat>(FindObjectsSortMode.None));

        [UnityTest]
        public IEnumerator AStainOnAPlatformMovesWithItAndGoesWithIt()
        {
            BloodSplats.Clear();
            GameObject platform = Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(6.0f, 1.0f, 6.0f));
            platform.AddComponent<Rigidbody>().isKinematic = true;   // a lift, a door: firm, but it moves
            BloodSplats.Mark(new Vector3(0.0f, 1.2f, 0.0f), Vector3.forward, 1.0f, null);
            yield return null;
            List<BloodSplat> splats = Splats();
            Assert.IsNotEmpty(splats, "setup: no stain was laid on the platform");
            BloodSplat onIt = splats.Find(s => s.Surface == platform.GetComponent<Collider>());
            Assert.IsNotNull(onIt, "the stain does not know what it lies on");
            float before = onIt.transform.position.y;

            platform.transform.position += new Vector3(0.0f, 2.0f, 3.0f);
            yield return null;
            Assert.AreEqual(before + 2.0f, onIt.transform.position.y, 0.01f, "the platform rose and the stain stayed where it was");

            Object.Destroy(platform);
            yield return null;
            yield return null;
            Assert.IsTrue(onIt == null, "the platform went and its stain hung in the air");
        }

        [UnityTest]
        public IEnumerator APieceOfGoreLeavesNoSmearOnAnotherPieceOnlyOnFirmGround()
        {
            BloodSplats.Clear();
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(20.0f, 1.0f, 20.0f));
            GameObject loose = Box(new Vector3(0.0f, 0.3f, 0.0f), new Vector3(0.6f, 0.6f, 0.6f));
            loose.AddComponent<Rigidbody>();   // a piece lying about, still simulated
            int before = BloodSplats.Dropped;
            BloodSplats.Drop(new Vector3(0.0f, 0.6f, 0.0f), Vector3.up, 0.3f, Vector3.forward, loose.GetComponent<Collider>());
            Assert.AreEqual(before, BloodSplats.Dropped, "a smear went on a loose piece, to hang in the air when it rolls off");
            yield return null;
            Collider ground = _made[0].GetComponent<Collider>();
            BloodSplats.Drop(new Vector3(2.0f, 0.0f, 0.0f), Vector3.up, 0.3f, Vector3.forward, ground);
            Assert.AreEqual(before + 1, BloodSplats.Dropped, "no smear on the ground");
            Assert.IsFalse(BloodSplats.IsFirm(loose.GetComponent<Collider>(), null));
            Assert.IsTrue(BloodSplats.IsFirm(ground, null));
        }
    }
}
