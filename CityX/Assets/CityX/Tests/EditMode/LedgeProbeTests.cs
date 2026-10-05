using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The ledge validator on plain colliders. THE REPORTED BUG: climbing "reached an edge" where there was none (a gap in the wall) and mantled into
    /// the wall, stuck. A ledge must be a flat, wide, deep top that belongs to this wall with room to stand on it; every look-alike must be refused.
    /// The player stands at the origin facing +Z, the wall's near face at z = 0.5 (so its outward normal is -Z).
    /// </summary>
    public sealed class LedgeProbeTests
    {
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();
        private const float Height = 2.6f;
        private const float Radius = 0.3f;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private GameObject Box(Vector3 centre, Vector3 size, Vector3? euler = null)
        {
            var go = new GameObject("Box");
            go.transform.position = centre;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            go.AddComponent<BoxCollider>().size = size;
            _made.Add(go);
            return go;
        }

        /// <summary>A wall from the ground to <paramref name="top"/>, 1.5 m thick, 10 m wide, near face at z = 0.5.</summary>
        private void Wall(float top)
        {
            Box(new Vector3(0.0f, top * 0.5f, 1.25f), new Vector3(10.0f, top, 1.5f));
            Physics.SyncTransforms();
        }

        private bool Find(float feetY, out LedgeProbe.Ledge ledge)
        {
            return LedgeProbe.TryFind(new Vector3(0.0f, feetY, 0.0f), Vector3.back, Height, Radius, null, out ledge);
        }

        [Test]
        public void AFlatWideDeepTopWithRoomAboveIsALedge()
        {
            Wall(3.2f);
            Assert.IsTrue(Find(1.0f, out LedgeProbe.Ledge ledge), "a plain wall top was not found");
            Assert.AreEqual(3.2f, ledge.Height, 0.05f);
            Assert.AreEqual(3.2f, ledge.Stand.y, 0.05f);
            Assert.Greater(ledge.Stand.z, 0.5f, "the standing spot must be ON the ledge, past the face");
            Assert.Less(ledge.Stand.z, 2.0f, "and over the wall's top, not beyond its back");
            Assert.AreEqual(0.5f, ledge.Lip.z, 0.05f);
        }

        [Test]
        public void ATallWallWithNoTopWithinReachIsNotALedge()
        {
            Wall(30.0f);
            Assert.IsFalse(Find(1.0f, out _), "the middle of a tall wall read as a ledge");
        }

        [Test]
        public void AGapInTheWallIsNotALedge()
        {
            // A wall with a window-sized opening at hand height: solid below 1.5 and above 3.0 but nothing between (the reported false edge).
            Box(new Vector3(0.0f, 0.75f, 1.25f), new Vector3(10.0f, 1.5f, 1.5f));
            Box(new Vector3(0.0f, 8.0f, 1.25f), new Vector3(10.0f, 10.0f, 1.5f));   // from y = 3 up
            Physics.SyncTransforms();
            Assert.IsFalse(Find(0.6f, out _), "a window opening was taken for the top of the wall");
            Assert.IsFalse(Find(1.3f, out _), "the sill below a window opening was taken for a ledge too far from the same face");
        }

        [Test]
        public void AThinPoleOrFenceIsNotALedge()
        {
            Wall(1.0f);
            Box(new Vector3(0.0f, 2.0f, 1.0f), new Vector3(0.15f, 3.0f, 0.15f));   // a pole standing on it
            Physics.SyncTransforms();
            Assert.IsFalse(Find(0.9f, out _), "a pole was taken for a ledge");
        }

        [Test]
        public void ASlopedTopIsNotALedge()
        {
            Box(new Vector3(0.0f, 1.0f, 1.25f), new Vector3(10.0f, 2.0f, 1.5f));
            Box(new Vector3(0.0f, 2.4f, 1.25f), new Vector3(10.0f, 0.3f, 1.8f), new Vector3(35.0f, 0.0f, 0.0f));   // a steep roof on top
            Physics.SyncTransforms();
            Assert.IsFalse(Find(0.7f, out _), "a steep slope was taken for a flat ledge");
        }

        [Test]
        public void ALedgeWithACeilingOverItHasNoRoomToStand()
        {
            Wall(3.0f);
            Vector3 stand = new Vector3(0.0f, 3.0f, 0.8f);
            Assert.IsTrue(LedgeProbe.HasRoom(stand, Height, Radius, null), "setup: an open ledge should have room");
            Box(new Vector3(0.0f, 4.2f, 1.25f), new Vector3(10.0f, 0.4f, 1.5f));   // an overhang 1.0 m above the ledge: no room for a 2.6 m body
            Physics.SyncTransforms();
            Assert.IsFalse(LedgeProbe.HasRoom(stand, Height, Radius, null), "the body was allowed to stand where it could not fit");
            // And the ledge UNDER the overhang is never offered as a place to mantle to (the probe sees the overhang first).
            if (Find(1.0f, out LedgeProbe.Ledge found))
            {
                Assert.Greater(found.Height, 3.5f, "the ledge under an overhang was offered");
            }
        }

        [Test]
        public void ARecessedShelfFarBehindTheFaceIsNotThisWallsTop()
        {
            // The face at hand height is 0.5; the only top is a slab whose own face is 2 m further back.
            Box(new Vector3(0.0f, 0.5f, 1.25f), new Vector3(10.0f, 1.0f, 1.5f));          // low wall, face at z = 0.5, top 1.0
            Box(new Vector3(0.0f, 2.0f, 4.0f), new Vector3(10.0f, 4.0f, 2.0f));           // tall block set back, face at z = 3
            Physics.SyncTransforms();
            Assert.IsFalse(Find(0.0f, out _), "a shelf set back from the face was taken for this wall's top");
        }

        [Test]
        public void TheMantlePathRisesThenStepsOverTheLipAndEndsOnTheStandingSpot()
        {
            Wall(3.2f);
            Assert.IsTrue(Find(1.0f, out LedgeProbe.Ledge ledge));
            Vector3 start = new Vector3(0.0f, 1.0f, 0.0f);
            Assert.AreEqual(start, PlayerClimbing.MantlePath(start, ledge, 0.0f));
            Vector3 end = PlayerClimbing.MantlePath(start, ledge, 1.0f);
            Assert.AreEqual(ledge.Stand.x, end.x, 1e-3f);
            Assert.AreEqual(ledge.Stand.y, end.y, 1e-3f);
            Assert.AreEqual(ledge.Stand.z, end.z, 1e-3f);

            // Never INTO the wall: while still below the ledge's height the body has not moved toward it.
            for (float u = 0.0f; u <= 0.55f; u += 0.05f)
            {
                Vector3 p = PlayerClimbing.MantlePath(start, ledge, u);
                Assert.AreEqual(start.z, p.z, 1e-3f, $"u={u}: it moved toward the wall before it was up");
                Assert.LessOrEqual(p.y, ledge.Height + 0.01f);
            }
            // And it is monotonic in height up to the top.
            float last = start.y;
            for (float u = 0.0f; u <= 0.6f; u += 0.02f)
            {
                float y = PlayerClimbing.MantlePath(start, ledge, u).y;
                Assert.GreaterOrEqual(y, last - 1e-4f);
                last = y;
            }
        }
    }
}
