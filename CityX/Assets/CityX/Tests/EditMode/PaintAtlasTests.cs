using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The paint atlas, and specifically the four things that went wrong in the
    /// Godot build and were fixed there.
    ///
    /// Porting a fix without porting the test that proves it is how a fix gets
    /// lost the next time somebody tidies the code -- and every one of these
    /// was found by looking at a wrong-looking brush stroke, which is exactly
    /// the kind of bug nobody goes looking for twice.
    /// </summary>
    public class PaintAtlasTests
    {
        [Test]
        public void EveryPaintedPartHasItsOwnRowAndTheHeadIsFirst()
        {
            Assert.AreEqual(7, PaintAtlas.PartCount);
            Assert.AreEqual(0, PaintAtlas.RowOf(HumanBodyBones.Head));
            Assert.AreEqual(1, PaintAtlas.RowOf(HumanBodyBones.Hips));

            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (HumanBodyBones part in PaintAtlas.Parts)
            {
                Assert.IsTrue(seen.Add(PaintAtlas.RowOf(part)), $"{part} shares a row");
            }
        }

        /// <summary>
        /// A forearm paints into its upper arm's row, so a stroke drawn across
        /// the elbow is one stroke rather than two halves in different rows.
        /// </summary>
        [Test]
        public void SubSplitBonesShareTheirLimbsRow()
        {
            Assert.AreEqual(HumanBodyBones.LeftUpperArm,
                PaintAtlas.PaintBoneFor(HumanBodyBones.LeftLowerArm));
            Assert.AreEqual(HumanBodyBones.LeftUpperArm,
                PaintAtlas.PaintBoneFor(HumanBodyBones.LeftHand));
            Assert.AreEqual(HumanBodyBones.RightUpperLeg,
                PaintAtlas.PaintBoneFor(HumanBodyBones.RightLowerLeg));
            Assert.AreEqual(HumanBodyBones.Hips,
                PaintAtlas.PaintBoneFor(HumanBodyBones.Spine));

            // And a bone that owns its own row is left alone.
            Assert.AreEqual(HumanBodyBones.Neck,
                PaintAtlas.PaintBoneFor(HumanBodyBones.Neck));
        }

        /// <summary>
        /// The arm's UV axis spans the WHOLE arm, not the upper arm's own
        /// segment. If it stopped at the elbow, everything past it would be out
        /// in cap-overflow territory and the forearm would paint into a sliver.
        /// </summary>
        [Test]
        public void ALimbsAxisSpansPastItsOwnFirstBone()
        {
            PaintAtlas.AxisOf(HumanBodyBones.LeftUpperArm, out Vector3 a, out Vector3 b);

            Assert.AreEqual(BodyRig.PositionOf(HumanBodyBones.LeftUpperArm), a);
            Assert.Greater(Vector3.Distance(a, b),
                Vector3.Distance(a, BodyRig.PositionOf(HumanBodyBones.LeftLowerArm)) * 1.5f,
                "the arm's paint axis stops around the elbow");
        }

        /// <summary>
        /// THE COLLAR BUG. Points past a capsule's end used to collapse onto one
        /// V, producing zero-area triangles the brush could not solve a tangent
        /// from. Distinct points past the end must still get distinct V.
        /// </summary>
        [Test]
        public void PointsPastTheEndOfALimbStillGetDistinctCoordinates()
        {
            PaintAtlas.AxisOf(HumanBodyBones.Hips, out Vector3 a, out Vector3 b);
            Vector3 along = (b - a).normalized;

            Vector2 justPast = PaintAtlas.Coordinate(b + along * 0.02f, a, b, 1);
            Vector2 further = PaintAtlas.Coordinate(b + along * 0.10f, a, b, 1);
            Vector2 furthest = PaintAtlas.Coordinate(b + along * 0.25f, a, b, 1);

            Assert.AreNotEqual(justPast.y, further.y, "two points past the cap share one V");
            Assert.AreNotEqual(further.y, furthest.y);
            Assert.Less(justPast.y, further.y, "V does not increase past the end");
            Assert.Less(further.y, furthest.y);
        }

        /// <summary>
        /// The cap slice is approached asymptotically over the distances a real
        /// vertex can be at, so the far end never becomes a new degenerate case
        /// the way the hard clamp was.
        ///
        /// ASSERTED OVER THE BODY'S OWN SIZE, and it took a failure to get that
        /// right. The first version of this test used a point a hundred metres
        /// past the cap and demanded the edge still not be reached -- but
        /// 1 - exp(-100/0.22) is exactly 1 in float, so it is reached, and the
        /// test was asserting a property the maths does not have rather than
        /// finding a bug. It does not matter: no vertex is ever out there, the
        /// furthest is one cap radius past the end. And landing exactly on the
        /// row's last V would be harmless anyway, because what keeps a vertex
        /// out of its neighbour's row is RowMargin, not the asymptote.
        /// </summary>
        [Test]
        public void TheCapSliceIsApproachedGraduallyAcrossTheBodysOwnScale()
        {
            PaintAtlas.AxisOf(HumanBodyBones.Hips, out Vector3 a, out Vector3 b);
            Vector3 along = (b - a).normalized;
            float v1 = 2.0f / PaintAtlas.PartCount - PaintAtlas.RowMargin;

            // A cap reaches about one torso radius past the end; a whole metre
            // is already far more than any real vertex.
            Vector2 pastCap = PaintAtlas.Coordinate(b + along * 1.0f, a, b, 1);
            Assert.Less(pastCap.y, v1, "a point a metre past the cap reached the row's edge");

            // And it is still climbing there rather than having flattened off
            // early, which is what made the collar's triangles degenerate.
            Vector2 nearer = PaintAtlas.Coordinate(b + along * 0.3f, a, b, 1);
            Assert.Less(nearer.y, pastCap.y);
        }

        /// <summary>
        /// THE CROTCH BUG. Every coordinate has to sit strictly inside its own
        /// row: adjacent rows share their boundary exactly, so a value landing
        /// on it can round into the neighbour -- which put paint from the
        /// bottom of the head onto the hips.
        /// </summary>
        [Test]
        public void EveryCoordinateStaysStrictlyInsideItsOwnRow()
        {
            for (int row = 0; row < PaintAtlas.PartCount; row++)
            {
                float low = (float)row / PaintAtlas.PartCount;
                float high = (float)(row + 1) / PaintAtlas.PartCount;

                PaintAtlas.AxisOf(HumanBodyBones.Hips, out Vector3 a, out Vector3 b);
                Vector3 along = (b - a).normalized;

                for (float t = -3.0f; t <= 4.0f; t += 0.05f)
                {
                    Vector3 point = a + (b - a) * t + Vector3.right * 0.1f;
                    float v = PaintAtlas.Coordinate(point, a, b, row).y;

                    Assert.Greater(v, low, $"row {row} leaked below its boundary at t={t:0.00}");
                    Assert.Less(v, high, $"row {row} leaked above its boundary at t={t:0.00}");
                }
            }
        }

        /// <summary>
        /// THE ARMPIT BUG. A triangle straddling the wrap seam interpolates
        /// backwards through the far side of the limb, scattering a stroke
        /// across unrelated places in the same row.
        /// </summary>
        [Test]
        public void ATriangleAcrossTheWrapSeamIsUnwrappedRatherThanInterpolatedTheLongWay()
        {
            var a = new Vector2(0.98f, 0.3f);
            var b = new Vector2(0.02f, 0.3f);
            var c = new Vector2(0.99f, 0.32f);

            PaintAtlas.FixThetaSeam(ref a, ref b, ref c);

            float span = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            Assert.Less(span, 0.5f, "the triangle still spans the long way round");
            Assert.AreEqual(1.02f, b.x, 0.0001f, "the wrapped corner was not unwrapped");
        }

        [Test]
        public void AnOrdinaryTriangleIsLeftAlone()
        {
            var a = new Vector2(0.40f, 0.3f);
            var b = new Vector2(0.44f, 0.3f);
            var c = new Vector2(0.42f, 0.32f);

            PaintAtlas.FixThetaSeam(ref a, ref b, ref c);

            Assert.AreEqual(0.40f, a.x, 0.0001f);
            Assert.AreEqual(0.44f, b.x, 0.0001f);
        }

        /// <summary>
        /// Going right round a limb sweeps u across its whole range exactly
        /// once. A mapping that only used part of the row would waste texture
        /// and change the effective brush size with the angle.
        /// </summary>
        [Test]
        public void GoingRoundALimbSweepsTheFullWidthOnce()
        {
            PaintAtlas.AxisOf(HumanBodyBones.Hips, out Vector3 a, out Vector3 b);
            Vector3 middle = Vector3.Lerp(a, b, 0.5f);

            float lowest = 1.0f;
            float highest = 0.0f;
            for (int i = 0; i < 64; i++)
            {
                float angle = i / 64.0f * Mathf.PI * 2.0f;
                var around = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * 0.3f;
                float u = PaintAtlas.Coordinate(middle + around, a, b, 1).x;

                lowest = Mathf.Min(lowest, u);
                highest = Mathf.Max(highest, u);
            }

            Assert.Less(lowest, 0.05f, "u never reaches the left of the row");
            Assert.Greater(highest, 0.95f, "u never reaches the right of the row");
        }
    }
}
