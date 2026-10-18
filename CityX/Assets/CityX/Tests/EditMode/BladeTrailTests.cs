using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The light trailing from a blade: it follows the arc a fast cut swung (not the chord), forgets what is older than its life, and is wide
    /// and bright at the blade, thin and faded at its tail.</summary>
    public sealed class BladeTrailTests
    {
        [Test]
        public void AFastCutsTrailFollowsTheArcTheBladeSwungNotTheStraightLineBetween()
        {
            var path = new BladeTrailPath();
            // A 2 m blade swung a hundred and twenty degrees about its root in one frame.
            path.Add(0.0f, Vector3.zero, new Vector3(2.0f, 0.0f, 0.0f), 8);
            Vector3 end = Quaternion.AngleAxis(120.0f, Vector3.up) * new Vector3(2.0f, 0.0f, 0.0f);
            path.Add(1.0f / 60.0f, Vector3.zero, end, 8);
            Assert.AreEqual(9, path.Count, "eight steps in all from one blade to the next");
            for (int i = 0; i < path.Count; i++)
            {
                Assert.AreEqual(2.0f, path[i].Outer.magnitude, 1e-3f, $"step {i}: the tip left the arc it swung");
                Assert.AreEqual(1.0f / 60.0f * i / 8.0f, path[i].Time, 1e-5f);
            }
            float middle = Vector3.SignedAngle(Vector3.right, path[4].Outer, Vector3.up);
            Assert.AreEqual(60.0f, middle, 0.5f, "half way through the frame the blade is half way round");
        }

        /// <summary>The sword spin: the blade held out at arm's length while the whole body turns 45 degrees a frame. Its inner end goes round the body too.</summary>
        [Test]
        public void ABladeSweptRoundTheBodyKeepsToItsCircleNotThePolygonOfItsFrames()
        {
            var path = new BladeTrailPath();
            for (int f = 0; f <= 4; f++)
            {
                Quaternion turn = Quaternion.AngleAxis(f * 45.0f, Vector3.up);
                path.Add(f / 60.0f, turn * new Vector3(2.0f, 1.2f, 0.0f), turn * new Vector3(4.0f, 1.2f, 0.3f), 8);
            }
            Assert.AreEqual(33, path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 inner = path[i].Inner, outer = path[i].Outer;
                Assert.AreEqual(2.0f, new Vector2(inner.x, inner.z).magnitude, 1e-3f, $"step {i}: the blade's inner end cut inside the circle");
                Assert.AreEqual(Mathf.Sqrt(16.0f + 0.09f), new Vector2(outer.x, outer.z).magnitude, 1e-3f, $"step {i}: the tip cut inside the circle");
                Assert.AreEqual(1.2f, inner.y, 1e-4f);
            }
            float middle = Vector3.SignedAngle(Vector3.right, path[4].Inner - Vector3.up * 1.2f, Vector3.up);
            Assert.AreEqual(22.5f, middle, 0.5f, "half way through a frame the blade is half way round its turn");
        }

        [Test]
        public void ItForgetsWhatIsOlderThanItsLife()
        {
            var path = new BladeTrailPath();
            for (int f = 0; f < 20; f++)
            {
                path.Add(f / 60.0f, Vector3.zero, Vector3.right, 1);
            }
            path.Trim(19.0f / 60.0f, 0.105f);
            Assert.AreEqual(7, path.Count, "a little over a tenth of a second at 60 frames a second: the last seven frames");
            Assert.GreaterOrEqual(path[0].Time, 19.0f / 60.0f - 0.105f);
        }

        [Test]
        public void TheRibbonIsWideAndBrightAtTheBladeAndThinAndFadedAtItsTail()
        {
            var path = new BladeTrailPath();
            for (int f = 0; f <= 6; f++)
            {
                Vector3 outer = Quaternion.AngleAxis(f * 20.0f, Vector3.up) * new Vector3(2.0f, 0.0f, 0.0f);
                path.Add(f / 60.0f, outer * 0.3f, outer, 1);
            }
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            float now = 6.0f / 60.0f;
            path.Build(now, 0.1f, 0.7f, vertices, uvs, colours, triangles);
            Assert.AreEqual(path.Count * 2, vertices.Count);
            Assert.AreEqual((path.Count - 1) * 12, triangles.Count, "both sides of every quad");

            int newest = path.Count - 1;
            float headWidth = Vector3.Distance(vertices[newest * 2], vertices[newest * 2 + 1]);
            float tailWidth = Vector3.Distance(vertices[0], vertices[1]);
            Assert.AreEqual(1.4f, headWidth, 1e-3f, "at the blade the band covers the blade's outer part");
            Assert.Less(tailWidth, 0.1f, "the tail does not narrow onto the tip's path");
            Assert.AreEqual(0.7f, colours[newest * 2].a, 1e-4f, "at the blade it is as bright as asked");
            Assert.Less(colours[0].a, 0.01f, "the tail does not fade out");
            Assert.AreEqual(0.0f, uvs[0].x, 1e-5f);
            Assert.AreEqual(1.0f, uvs[newest * 2].x, 1e-5f);
        }
    }
}
