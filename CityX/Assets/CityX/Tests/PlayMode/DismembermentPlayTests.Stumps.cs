using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    // The stump on a cut-off limb (owner, 2026-11-07: "the red round part of the detatched limbs is way larger than the circumference of the
    // limb"): its red cap is as wide as the limb where it was cut, enough to cover the hole and hardly more.
    public sealed partial class DismembermentPlayTests
    {
        /// <summary>The cap's radius over the limb's at the cut: at least this, so it covers the hole.</summary>
        private const float CapCoversTheLimb = 1.0f;

        /// <summary>... and at most this, so it reads as the end of the limb and not a plate stuck on it.</summary>
        private const float CapHardlyWiderThanTheLimb = 1.15f;

        /// <summary>How far along the limb an open edge of the piece may lie from the stump and still be the cut's (the wrist's seam is not).</summary>
        private const float RimReach = 0.15f;

        [UnityTest]
        [TestCase(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, ExpectedResult = null)]
        [TestCase(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, ExpectedResult = null)]
        [TestCase(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, ExpectedResult = null)]
        [TestCase(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, ExpectedResult = null)]
        public IEnumerator ACutOffLimbsCapIsAsWideAsTheLimbWhereItWasCut(HumanBodyBones from, HumanBodyBones to)
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            Kill(body, BonePoint(body, from, to, 0.5f), 60.0f, edged: true);
            Assert.AreEqual(Dismemberment.Sever, dismemberer.Last, "setup: the blade did not cut");
            GameObject piece = dismemberer.LastPiece;
            Assert.IsNotNull(piece, "setup: nothing flew off");
            Transform stump = piece.transform.Find("Stump");
            Assert.IsNotNull(stump, "the cut-off piece has no stump");
            Transform skin = piece.transform.Find(BodySurface.SkinName);
            Assert.IsNotNull(skin, "setup: the piece has no skin");

            float limb = RimRadius(skin.GetComponent<MeshFilter>().sharedMesh, skin, stump.position, stump.up, out int rimPoints);
            Assert.Greater(rimPoints, 8, "setup: the piece's cut edge was not found");
            float cap = stump.Find("Cap").lossyScale.x * 0.5f;
            float collar = stump.Find("Collar").lossyScale.x * 0.5f;
            Assert.That(cap / limb, Is.InRange(CapCoversTheLimb, CapHardlyWiderThanTheLimb),
                $"the {dismemberer.LastPart}'s cap is {cap / limb:F2}x the limb's width where it was cut");
            Assert.That(collar / limb, Is.LessThanOrEqualTo(CapHardlyWiderThanTheLimb * 1.1f),
                $"the blood ring round the {dismemberer.LastPart}'s cap is {collar / limb:F2}x the limb's width");
        }

        /// <summary>
        /// The limb's radius at its cut: the mean distance, across the stump's axis, of the corners on the piece's open edge near the stump
        /// (corners welded by position, so the seams the mesh keeps for its paint are not edges).
        /// </summary>
        private static float RimRadius(Mesh mesh, Transform at, Vector3 centre, Vector3 axis, out int count)
        {
            Vector3[] vertices = mesh.vertices;
            var weld = new Dictionary<Vector3Int, int>();
            var id = new int[vertices.Length];
            var welded = new List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 10000.0f);
                if (!weld.TryGetValue(key, out int w))
                {
                    w = welded.Count;
                    weld[key] = w;
                    welded.Add(at.TransformPoint(vertices[i]));
                }
                id[i] = w;
            }
            var uses = new Dictionary<long, int>();
            int[] triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = id[triangles[t + e]], b = id[triangles[t + (e + 1) % 3]];
                    long edge = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    uses[edge] = uses.TryGetValue(edge, out int n) ? n + 1 : 1;
                }
            }
            var rim = new HashSet<int>();
            foreach (KeyValuePair<long, int> edge in uses)
            {
                if (edge.Value == 1)
                {
                    rim.Add((int)(edge.Key >> 32));
                    rim.Add((int)(edge.Key & 0xffffffff));
                }
            }
            float sum = 0.0f;
            count = 0;
            foreach (int w in rim)
            {
                Vector3 offset = welded[w] - centre;
                if (Mathf.Abs(Vector3.Dot(offset, axis)) > RimReach)
                {
                    continue;
                }
                sum += Vector3.ProjectOnPlane(offset, axis).magnitude;
                count++;
            }
            return count > 0 ? sum / count : 0.0f;
        }
    }
}
