using System.Collections.Generic;
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
        /// EVERY PART HAS TO ACTUALLY FILL ITS ROW.
        ///
        /// The head did not. It had no case in AxisOf and fell through to the
        /// default, which returned the world Y axis from the origin -- so a
        /// head sitting between y=1.9 and y=2.6 came out with rawT from 1.9 to
        /// 2.6, all of it past the end of a one-unit axis and all of it
        /// therefore in the cap-overflow branch, which asymptotes. The whole
        /// head collapsed into the top 0.03% of its own row: about a hundredth
        /// of a pixel of v. Every brush stroke on the face rendered as a
        /// full-height stripe.
        ///
        /// Nothing caught it because nothing was wrong in a way a number could
        /// show: the UVs were finite, inside the row, and inside [0,1]. The
        /// only thing wrong was that they were all the SAME.
        /// </summary>
        [Test]
        public void EveryPartSpreadsAcrossItsOwnRowRatherThanCollapsingIntoALine()
        {
            List<FusedBodyShape.Segment> segments = FusedBodyShape.Segments(
                BodyRig.PositionOf, FusedBodyShape.ReferenceHeight);
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, FusedBodyShape.CellSize,
                out List<Vector3> welded, out List<int> weldedTriangles);
            FusedBodyMesh.Unwrap(segments, welded, weldedTriangles,
                out List<Vector3> _, out List<int> triangles, out List<Vector2> uvs);

            var lowest = new float[PaintAtlas.PartCount];
            var highest = new float[PaintAtlas.PartCount];
            var widest = new float[PaintAtlas.PartCount];
            var narrowest = new float[PaintAtlas.PartCount];
            var counted = new int[PaintAtlas.PartCount];
            for (int row = 0; row < PaintAtlas.PartCount; row++)
            {
                lowest[row] = float.MaxValue;
                highest[row] = float.MinValue;
                narrowest[row] = float.MaxValue;
                widest[row] = float.MinValue;
            }

            // THE HEAD'S UVs COME FROM ITS OWN MESH. It is not part of the
            // body's shape field -- see HeadMesh -- so its row is filled by the
            // skull, and looking only at the body would find row 0 empty.
            Mesh head = HeadMesh.Build();
            var everything = new List<Vector2>(uvs);
            everything.AddRange(head.uv);
            Object.DestroyImmediate(head);

            foreach (Vector2 uv in everything)
            {
                int row = Mathf.Clamp(
                    Mathf.FloorToInt(uv.y * PaintAtlas.PartCount), 0, PaintAtlas.PartCount - 1);
                lowest[row] = Mathf.Min(lowest[row], uv.y);
                highest[row] = Mathf.Max(highest[row], uv.y);
                narrowest[row] = Mathf.Min(narrowest[row], Mathf.Repeat(uv.x, 1.0f));
                widest[row] = Mathf.Max(widest[row], Mathf.Repeat(uv.x, 1.0f));
                counted[row]++;
            }

            float rowHeight = 1.0f / PaintAtlas.PartCount;

            for (int row = 0; row < PaintAtlas.PartCount; row++)
            {
                HumanBodyBones part = PaintAtlas.Parts[row];

                // THE NECK'S ROW IS EMPTY, AND THAT IS THE GEOMETRY, not a
                // bug in the unwrap. The neck capsule runs from the shoulder
                // line up to the head bone with a radius of 0.10; the head
                // sphere is centred on that same head bone with a radius of
                // 0.35, so it reaches all the way back down to the shoulder
                // line and swallows the neck whole. No point on the surface is
                // ever nearest the neck, so nothing is ever skinned to it and
                // its row never receives a vertex. The visible throat is
                // painted as part of the head above and the torso below, which
                // means paint runs across it continuously instead of seaming.
                //
                // Godot's neck WAS exposed -- its head was a separate box
                // floating above a shorter neck rather than a sphere fused
                // into it. Asserted rather than skipped so that if the body's
                // proportions ever change, this says so instead of a row
                // quietly filling up with an unwrap nobody checked.
                if (part == HumanBodyBones.Neck)
                {
                    Assert.AreEqual(0, counted[row],
                        "the neck is exposed now -- it needs its unwrap checking like the rest");
                    continue;
                }

                Assert.Greater(counted[row], 0,
                    $"nothing at all is mapped into {part}'s row");

                // Half the row, allowing for the cap slices reserved at each
                // end. A part using less than that is being crushed.
                float used = (highest[row] - lowest[row]) / rowHeight;
                Assert.Greater(used, 0.5f,
                    $"{part} uses only {used:P1} of the height of its row -- "
                    + "a brush stroke on it will smear across the whole part");

                // And all the way round, or one side of the part is unreachable.
                Assert.Greater(widest[row] - narrowest[row], 0.8f,
                    $"{part} does not wrap all the way round");
            }
        }

        /// <summary>
        /// Every part's axis has to be a real axis THROUGH THAT PART. The
        /// silent fallback that shipped was a unit of world Y at the origin,
        /// which is a perfectly well-formed axis nowhere near the head.
        /// </summary>
        [Test]
        public void EveryPartsAxisRunsThroughThePartItBelongsTo()
        {
            foreach (HumanBodyBones part in PaintAtlas.Parts)
            {
                PaintAtlas.AxisOf(part, out Vector3 a, out Vector3 b);

                Assert.Greater((b - a).magnitude, 0.05f, $"{part}'s axis has no length");

                Vector3 bone = BodyRig.PositionOf(part);
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(bone - a, ab) / ab.sqrMagnitude);
                float away = Vector3.Distance(bone, a + ab * t);
                Assert.Less(away, 0.35f,
                    $"{part}'s own bone sits {away:F2} from the axis it is unwrapped around");
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
        /// THE REAL BODY, UNWRAPPED. Everything above tests the mapping on its
        /// own; this tests the mesh a brush will actually paint on.
        /// </summary>
        [Test]
        public void TheUnwrappedBodyIsPaintable()
        {
            List<FusedBodyShape.Segment> segments = FusedBodyShape.Segments(
                BodyRig.PositionOf, FusedBodyShape.ReferenceHeight);
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, FusedBodyShape.CellSize,
                out List<Vector3> welded, out List<int> weldedTriangles);
            FusedBodyMesh.Unwrap(segments, welded, weldedTriangles,
                out List<Vector3> vertices, out List<int> triangles, out List<Vector2> uvs);

            Assert.AreEqual(weldedTriangles.Count, vertices.Count,
                "the unwrap did not produce triangle soup -- vertices are still shared");
            Assert.AreEqual(vertices.Count, uvs.Count, "a vertex has no UV");

            int degenerate = 0;
            int wide = 0;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = uvs[triangles[i]];
                Vector2 b = uvs[triangles[i + 1]];
                Vector2 c = uvs[triangles[i + 2]];

                // ALL THREE CORNERS IN ONE ROW. A triangle spanning two rows
                // sweeps its interior through every row in between and samples
                // whatever is painted there -- the armpit-to-hip bug.
                int rowA = Mathf.FloorToInt(a.y * PaintAtlas.PartCount);
                int rowB = Mathf.FloorToInt(b.y * PaintAtlas.PartCount);
                int rowC = Mathf.FloorToInt(c.y * PaintAtlas.PartCount);
                Assert.AreEqual(rowA, rowB, $"triangle {i / 3} spans two atlas rows");
                Assert.AreEqual(rowA, rowC, $"triangle {i / 3} spans two atlas rows");

                float span = Mathf.Max(a.x, Mathf.Max(b.x, c.x))
                             - Mathf.Min(a.x, Mathf.Min(b.x, c.x));
                if (span > 0.5f)
                {
                    wide++;
                }

                float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y));
                if (area < 1e-9f)
                {
                    degenerate++;
                }
            }

            // THE COLLAR BUG, MEASURED ON THE REAL MESH. Zero-area triangles in
            // UV are the ones a brush cannot solve a tangent from, so it falls
            // back to a generic shape and stamps a diamond. A handful is
            // tolerable; the hard clamp produced them in a band.
            int count = triangles.Count / 3;

            // MEASURED AT 1.27%, PINNED AT 2%. These are the poles: at the very
            // tip of a rounded cap the radial vector is essentially zero and
            // the angle around the limb is arbitrary, so the triangles fanning
            // across it collapse in UV. Every cylindrical unwrap has that, and
            // a handful of pole triangles per cap is what it costs.
            //
            // It was 19% before the limb frame was built from the limb's own
            // direction rather than assuming a vertical one -- so this number
            // is worth watching, because it is exactly what a whole arm
            // unwrapping wrongly looks like.
            float share = (float)degenerate / count;
            Assert.Less(share, 0.02f,
                $"{degenerate} of {count} triangles are degenerate in UV ({share:P2}) -- " +
                "measured is 1.27%, so the unwrap has got meaningfully worse");

            // TRIANGLES THE SEAM UNWRAP CANNOT FIX, COUNTED RATHER THAN
            // ASSERTED AWAY. Shifting the corners below 0.5 up by one only
            // helps a clean seam crossing. A triangle whose corners are
            // genuinely spread over more than half a circumference -- which
            // happens at a cap, where a vertex sits near the axis and its angle
            // is close to arbitrary -- is still spread afterwards, and no
            // choice of shift fixes it. fused_body.gd has the same limitation.
            //
            // I asserted twice that this could not happen before measuring
            // whether it does. It does, on a small number of cap triangles, and
            // the honest thing is to pin how many so a change that makes it
            // meaningfully worse fails.
            float wideShare = (float)wide / count;
            Assert.Less(wideShare, 0.005f,
                $"{wide} of {count} triangles still span the long way after the unwrap " +
                $"({wideShare:P2})");
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
