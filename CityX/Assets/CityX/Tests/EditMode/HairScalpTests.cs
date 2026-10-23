using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The scalp cap under the dreads (owner, 2026-10-22: "a thin layer of material covering the top of the head in the shape of a humans
    /// typical hair growth pattern ... make the scalp cap not too low in the back"): a hairline low over the forehead's top, above the ears,
    /// level with the head's middle behind; thin, few triangles, facing out.
    /// </summary>
    public sealed class HairScalpTests
    {
        [Test]
        public void TheHairlineIsAtTheForeheadAboveTheEarsAndMidHeadBehind()
        {
            Assert.AreEqual(HairScalp.FrontHairline, HairScalp.Hairline(0.0f), 1e-3f);
            Assert.AreEqual(HairScalp.SideHairline, HairScalp.Hairline(90.0f), 1e-3f);
            Assert.AreEqual(HairScalp.SideHairline, HairScalp.Hairline(270.0f), 1e-3f);
            Assert.AreEqual(HairScalp.BackHairline, HairScalp.Hairline(180.0f), 1e-3f);
            Assert.GreaterOrEqual(HairScalp.BackHairline, 0.0f, "the cap reaches down the back, over the hanging locks");
        }

        [Test]
        public void TheCapIsThinFewTrianglesAndFacesOut()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            HairScalp.Build(0.35f, vertices, normals, triangles);
            Assert.LessOrEqual(triangles.Count / 3, 600, "the cap adds too many triangles");
            foreach (Vector3 v in vertices)
            {
                Assert.AreEqual(0.35f * HairScalp.Lift, v.magnitude, 1e-4f, "a point of the cap is not just off the head");
            }
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 facing = Vector3.Cross(b - a, c - a);
                // Unity's front faces are clockwise seen from outside, which puts this cross product outward.
                Assert.Greater(Vector3.Dot(facing, a + b + c), 0.0f, $"triangle {t / 3} of the cap faces into the head");
            }
        }

        [Test]
        public void ACapReachingLowGetsMoreRingsSoItsFacetsStayOffTheHead()
        {
            const float hairline = -50.0f;   // Hyde's cap reaches about this far down the back
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            HairScalp.Build(0.35f, _ => hairline, vertices, normals, triangles);
            int rings = (vertices.Count - 1) / HairScalp.Around;
            Assert.LessOrEqual((90.0f - hairline) / rings, HairScalp.MostRingStep + 1e-3f, "the rings step too far down the head");
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 middle = (vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3.0f;
                Assert.Greater(middle.magnitude, 0.35f * 1.01f, $"triangle {t / 3} of the cap dips to the head");
            }
        }
    }
}
