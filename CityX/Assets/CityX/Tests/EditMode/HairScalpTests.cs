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


        /// <summary>A dome of hair standing off the crown, from 60 degrees above the head's middle up: a hairstyle's locks over the top.</summary>
        private static void Dome(float radius, List<Vector3> vertices, List<int> triangles)
        {
            const int around = 48, rings = 12;
            vertices.Add(Vector3.up * radius);
            for (int ring = 1; ring <= rings; ring++)
            {
                float height = (90.0f - 30.0f * ring / rings) * Mathf.Deg2Rad;
                for (int i = 0; i < around; i++)
                {
                    float round = 360.0f * i / around * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(Mathf.Cos(height) * Mathf.Sin(round), Mathf.Sin(height), Mathf.Cos(height) * Mathf.Cos(round)) * radius);
                }
            }
            for (int i = 0; i < around; i++)
            {
                triangles.Add(0); triangles.Add(1 + i); triangles.Add(1 + (i + 1) % around);
            }
            for (int ring = 1; ring < rings; ring++)
            {
                int upper = 1 + (ring - 1) * around, lower = upper + around;
                for (int i = 0; i < around; i++)
                {
                    int next = (i + 1) % around;
                    triangles.Add(upper + i); triangles.Add(lower + i); triangles.Add(lower + next);
                    triangles.Add(upper + i); triangles.Add(lower + next); triangles.Add(upper + next);
                }
            }
        }

        /// <summary>
        /// Owner, 2026-11-21, of the dreads: "make the hairstyle curl inward underneath and clip into the head slightly ... to hide the visible white
        /// bit that is a result of being able to see underneath the hairstyle". Under hair standing off the crown the cap is raised to just under
        /// it, its edge tucked into the head; where no hair is over it, it lies just off the head as before.
        /// </summary>
        [Test]
        public void UnderHairStandingOffTheCrownTheCapIsRaisedToItWithItsEdgeInTheHead()
        {
            const float head = 0.35f;
            var cap = new List<Vector3>();
            HairScalp.Build(head, _ => 40.0f, cap, new List<Vector3>(), new List<int>());
            var hair = new List<Vector3>();
            var hairTriangles = new List<int>();
            Dome(head * 1.25f, hair, hairTriangles);
            HairScalp.RaiseUnderTheHair(cap, hair, hairTriangles, head);

            Assert.AreEqual(head * (1.25f - HairScalp.UnderTheHair), cap[0].magnitude, 0.01f, "the crown is not raised to just under the hair");
            int rings = (cap.Count - 1) / HairScalp.Around;
            for (int i = 0; i < cap.Count; i++)
            {
                Assert.LessOrEqual(cap[i].magnitude, head * 1.25f, $"point {i} of the cap stands through the hair");
                float height = Mathf.Asin(cap[i].normalized.y) * Mathf.Rad2Deg;
                int ring = i == 0 ? 0 : (i - 1) / HairScalp.Around + 1;
                if (ring == rings)
                {
                    Assert.AreEqual(head * HairScalp.Lift, cap[i].magnitude, 1e-4f, "the cap's edge, with no hair over it, left the head");
                }
                else if (height > 70.0f)
                {
                    Assert.Greater(cap[i].magnitude, head * 1.15f, $"point {i} of the cap, under the hair, was not raised");
                }
            }

            // With the hair over its edge too, the edge goes into the head.
            var lowCap = new List<Vector3>();
            HairScalp.Build(head, _ => 70.0f, lowCap, new List<Vector3>(), new List<int>());
            HairScalp.RaiseUnderTheHair(lowCap, hair, hairTriangles, head);
            Assert.AreEqual(head * HairScalp.EdgeInTheHead, lowCap[lowCap.Count - 1].magnitude, 1e-4f, "the raised cap's edge is not tucked into the head");
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
