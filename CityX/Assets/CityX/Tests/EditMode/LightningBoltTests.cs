using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The jagged path of a lightning bolt: pinned to its endpoints, deterministic, and bounded so it can never wander off.</summary>
    public sealed class LightningBoltTests
    {
        private static readonly Vector3 Cloud = new Vector3(40.0f, 480.0f, 10.0f);
        private static readonly Vector3 Ground = new Vector3(0.0f, 120.0f, 600.0f);

        [Test]
        public void ABoltRunsFromTheCloudExactlyToTheStrikePoint()
        {
            Vector3[] path = LightningBolt.Path(Cloud, Ground, 1, 6);

            Assert.AreEqual(Cloud, path[0]);
            Assert.AreEqual(Ground, path[path.Length - 1]);
        }

        [TestCase(1, 3)]
        [TestCase(4, 17)]
        [TestCase(6, 65)]
        public void EachSubdivisionDoublesTheSegments(int subdivisions, int expectedPoints)
        {
            Assert.AreEqual(expectedPoints, LightningBolt.Path(Cloud, Ground, 3, subdivisions).Length);
        }

        [Test]
        public void TheSameSeedIsTheSameBoltAndADifferentOneIsNot()
        {
            Vector3[] a = LightningBolt.Path(Cloud, Ground, 5, 6);
            Vector3[] b = LightningBolt.Path(Cloud, Ground, 5, 6);
            Vector3[] c = LightningBolt.Path(Cloud, Ground, 6, 6);

            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
        }

        [Test]
        public void ABoltIsJaggedNotAStraightLine()
        {
            Vector3[] path = LightningBolt.Path(Cloud, Ground, 2, 6);
            float straight = 0.0f;
            Vector3 axis = (Ground - Cloud).normalized;
            for (int i = 1; i < path.Length - 1; i++)
            {
                Vector3 off = (path[i] - Cloud) - axis * Vector3.Dot(path[i] - Cloud, axis);
                straight = Mathf.Max(straight, off.magnitude);
            }
            Assert.Greater(straight, 5.0f, "a bolt that never leaves the straight line is a laser");
        }

        [Test]
        public void ABoltNeverWandersFarFromTheWayItIsGoing()
        {
            float length = (Ground - Cloud).magnitude;
            Vector3 axis = (Ground - Cloud) / length;
            for (int seed = 0; seed < 30; seed++)
            {
                foreach (Vector3 point in LightningBolt.Path(Cloud, Ground, seed, 6))
                {
                    Vector3 off = (point - Cloud) - axis * Vector3.Dot(point - Cloud, axis);
                    Assert.Less(off.magnitude, length * 0.5f, $"seed {seed} strayed {off.magnitude:0}m from a {length:0}m bolt");
                }
            }
        }

        [Test]
        public void ANearlyVerticalBoltStillKinks()
        {
            // Straight down: the cross product with up is zero, which must not collapse the kinks.
            Vector3[] path = LightningBolt.Path(new Vector3(0.0f, 500.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), 4, 6);
            bool kinked = false;
            foreach (Vector3 p in path)
            {
                if (Mathf.Abs(p.x) > 1.0f || Mathf.Abs(p.z) > 1.0f)
                {
                    kinked = true;
                }
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z));
            }
            Assert.IsTrue(kinked);
        }

        [Test]
        public void AForkLeavesTheTrunkAndHeadsOffToTheSide()
        {
            Vector3[] trunk = LightningBolt.Path(Cloud, Ground, 8, 6);
            Vector3[] fork = LightningBolt.Branch(trunk, 8, 4);

            Assert.IsNotNull(fork);
            CollectionAssert.Contains(trunk, fork[0], "a fork starts ON the trunk");
            Assert.Less((fork[fork.Length - 1] - fork[0]).magnitude, (Ground - Cloud).magnitude * 0.6f, "shorter than the trunk");
        }

        [Test]
        public void ATooShortTrunkHasNoFork()
        {
            Assert.IsNull(LightningBolt.Branch(new[] { Cloud, Ground }, 1, 3));
            Assert.IsNull(LightningBolt.Branch(null, 1, 3));
        }
    }
}
