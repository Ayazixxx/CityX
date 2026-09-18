using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, with screenshots, across two rounds: (1) "make
    /// the paint trail round not a ovel shape... right now when i draw a
    /// straight line there are gaps and there are faded grey parts around
    /// the black"; (2) "using brush size 1 doing single taps around the
    /// body. it shows up as lines or ovels instead of a dot... its also too
    /// large for size 1." BodyPaint.PaintAt's own anisotropic correction had
    /// NO test at all before round (1) -- every claim about its own shape
    /// was validated only by eye, which is exactly how a correction that
    /// read fine in isolation shipped a real, visible bug in each round.
    ///
    /// ROUND (1)'s OWN FIX WAS ITSELF WRONG, in a way these tests originally
    /// missed: it capped how far the atlas-pixel radii rU/rV could diverge
    /// (down to 1.5:1), on the reasoning "tightening toward 1:1 is what
    /// actually makes it round." That is backwards. rawU/rawV (computed from
    /// TangentLengths, see _referenceBrush's own doc comment) are not a
    /// shape choice -- they are the ONLY thing that cancels the surface's
    /// own real UV anisotropy, by design making BOTH atlas axes cover the
    /// SAME real-world distance once mapped through the mesh. Capping that
    /// ratio throws away most of the compensation, which does not make the
    /// ON-BODY shape rounder, it makes it MORE elongated -- confirmed
    /// directly by rendering it. So these tests now check the property that
    /// actually matters -- WORLD-SPACE roundness, i.e. the painted pixel
    /// footprint converted back to real-world distance via this triangle's
    /// own tangent/bitangent -- rather than the atlas-PIXEL aspect ratio,
    /// which is supposed to be anisotropic on a genuinely anisotropic
    /// surface, not capped toward 1:1.
    /// </summary>
    public sealed class BodyPaintTests
    {
        /// <summary>
        /// A single quad (two triangles) with DELIBERATE, EXTREME UV
        /// anisotropy -- moving one world unit along U covers roughly 17x
        /// as many atlas pixels as moving one world unit along V, past even
        /// BodyPaint's own worst measured real case (an arm, ~12.6:1).
        /// Placed inside row 0 comfortably clear of its own margins, so the
        /// stamp is never clipped by the row-boundary logic Stamp applies.
        /// </summary>
        private static BodyPaint BuildAnisotropicPaint()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f), new Vector3(1.0f, 1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f),
                },
                uv = new[]
                {
                    new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.02f), new Vector2(0.9f, 0.06f),
                    new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.06f), new Vector2(0.1f, 0.06f),
                },
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };

            var paint = new BodyPaint(Color.white);
            paint.Bind(mesh);
            return paint;
        }

        /// <summary>The bounding box, in atlas pixels, of every pixel that is not the fill colour.</summary>
        private static void PaintedBounds(BodyPaint paint, Color fill,
            out int width, out int height)
        {
            paint.Apply();
            Color32[] pixels = paint.Atlas.GetPixels32();
            Color32 fill32 = fill;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            for (int y = 0; y < PaintAtlas.Height; y++)
            {
                for (int x = 0; x < PaintAtlas.Width; x++)
                {
                    Color32 c = pixels[y * PaintAtlas.Width + x];
                    if (c.r == fill32.r && c.g == fill32.g && c.b == fill32.b && c.a == fill32.a)
                    {
                        continue;
                    }
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            Assert.LessOrEqual(minX, maxX, "nothing was painted at all");
            width = maxX - minX + 1;
            height = maxY - minY + 1;
        }

        /// <summary>
        /// Independently derived from BuildAnisotropicPaint's own raw
        /// vertices/UVs -- the standard tangent/bitangent (UV-Jacobian
        /// inverse) construction, the same one any correct implementation
        /// has to use, NOT a call into BodyPaint's own TangentLengths. A
        /// test that asked BodyPaint what the right answer is would validate
        /// nothing; this computes it from first principles so a bug in
        /// BodyPaint's own version has something independent to be caught
        /// against.
        /// </summary>
        private static void ExpectedTangentBitangent(out float tangent, out float bitangent)
        {
            Vector3 a = new Vector3(0.0f, 0.0f, 0.0f);
            Vector3 b = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 c = new Vector3(1.0f, 1.0f, 0.0f);
            Vector2 ua = new Vector2(0.1f, 0.02f);
            Vector2 ub = new Vector2(0.9f, 0.02f);
            Vector2 uc = new Vector2(0.9f, 0.06f);

            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            var atlas = new Vector2(PaintAtlas.Width, PaintAtlas.Height);
            Vector2 uvEdge1 = Vector2.Scale(ub - ua, atlas);
            Vector2 uvEdge2 = Vector2.Scale(uc - ua, atlas);

            float determinant = uvEdge1.x * uvEdge2.y - uvEdge2.x * uvEdge1.y;
            Vector3 t = (edge1 * uvEdge2.y - edge2 * uvEdge1.y) / determinant;
            Vector3 bt = (edge2 * uvEdge1.x - edge1 * uvEdge2.x) / determinant;
            tangent = t.magnitude;
            bitangent = bt.magnitude;
        }

        /// <summary>
        /// THE ACTUAL CLAIM: a single tap reads as round on the real body,
        /// not that its atlas pixels happen to form a square. Converts the
        /// painted pixel footprint back to real-world distance via this
        /// triangle's own (independently derived) tangent/bitangent -- on a
        /// surface with genuine anisotropy, an atlas-space stamp SHOULD be
        /// far from square (that is what cancels the distortion); it is the
        /// WORLD-SPACE footprint that should be close to round.
        /// </summary>
        [Test]
        public void AStampReadsRoundInWorldSpaceEvenOnExtremeAnisotropy()
        {
            BodyPaint paint = BuildAnisotropicPaint();
            paint.PaintAt(new Vector3(0.5f, 0.5f, 0.0f), 0, Color.black, brushRadiusPixels: 6);

            PaintedBounds(paint, Color.white, out int width, out int height);
            ExpectedTangentBitangent(out float tangent, out float bitangent);

            float worldWidth = width * tangent;
            float worldHeight = height * bitangent;
            float longer = Mathf.Max(worldWidth, worldHeight);
            float shorter = Mathf.Min(worldWidth, worldHeight);
            Assert.LessOrEqual(longer / shorter, 1.5f,
                $"stamp measured {worldWidth:0.0000} x {worldHeight:0.0000} world units -- reads as an oval " +
                "on the real surface even though its atlas footprint compensates for the UV stretch");
        }

        /// <summary>
        /// EXPLICIT ASK: "make paint size 1 just 1 pixel." No radius, no
        /// anisotropic correction -- regardless of how extreme the
        /// surface's own UV distortion is, brush size 1 is exactly the one
        /// atlas texel the tap landed on. This supersedes the old
        /// "smaller axis never collapses to a sliver" guarantee for size 1
        /// specifically -- a single pixel now IS the intended result there,
        /// not a bug; see AnAnisotropicSurfaceStillReadsRoundInWorldSpace
        /// below for where the round-shape guarantee still applies (size 2
        /// and up).
        /// </summary>
        [Test]
        public void BrushSizeOnePaintsExactlyOnePixelRegardlessOfSurfaceAnisotropy()
        {
            BodyPaint paint = BuildAnisotropicPaint();
            paint.PaintAt(new Vector3(0.5f, 0.5f, 0.0f), 0, Color.black, brushRadiusPixels: 1);

            PaintedBounds(paint, Color.white, out int width, out int height);

            Assert.AreEqual(1, width, $"brush size 1 painted {width} pixels wide, not exactly 1");
            Assert.AreEqual(1, height, $"brush size 1 painted {height} pixels tall, not exactly 1");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, with a screenshot: "this is me using brush
        /// size 1 doing single taps around the body. it shows up as lines or
        /// ovels instead of a dot... its also too large for size 1." Size 1
        /// itself is now a literal single pixel (see the test above) rather
        /// than an anisotropic ellipse, so THIS test moves to size 2 -- the
        /// smallest size that still goes through the real anisotropic
        /// correction, and so the smallest size where a floor/ceiling clamp
        /// has the least room to work with and is most likely to silently
        /// destroy the compensation ratio.
        /// </summary>
        [Test]
        public void ASmallBrushOnAnAnisotropicSurfaceStillReadsRoundInWorldSpace()
        {
            BodyPaint paint = BuildAnisotropicPaint();
            paint.PaintAt(new Vector3(0.5f, 0.5f, 0.0f), 0, Color.black, brushRadiusPixels: 2);

            PaintedBounds(paint, Color.white, out int width, out int height);
            ExpectedTangentBitangent(out float tangent, out float bitangent);

            float worldWidth = width * tangent;
            float worldHeight = height * bitangent;
            float longer = Mathf.Max(worldWidth, worldHeight);
            float shorter = Mathf.Min(worldWidth, worldHeight);
            Assert.LessOrEqual(longer / shorter, 1.5f,
                $"stamp measured {worldWidth:0.0000} x {worldHeight:0.0000} world units -- reads as an oval " +
                "on the real surface at the smallest anisotropically-corrected brush size");
        }

        /// <summary>Consecutive stamps along a drag must fully cover the strip between them -- no unpainted gap for bilinear filtering to fade into.</summary>
        [Test]
        public void OverlappingStampsAlongAStrokeLeaveNoGapBetweenThem()
        {
            BodyPaint paint = BuildAnisotropicPaint();

            // Many stamps along the quad's own U direction, close enough
            // together in ATLAS PIXELS (not just UV) to guarantee overlap
            // regardless of the exact radius this fix computes -- 200 steps
            // across this triangle's own 0.8-wide U range is roughly 1.5
            // atlas pixels between consecutive centres, comfortably under
            // BodyPaint's own minRoundRadius (2.75), so any real gap found
            // here is genuinely BodyPaint's own doing, not just stamps that
            // were never close enough to touch in the first place.
            for (int i = 0; i <= 200; i++)
            {
                float x = 0.1f + 0.8f * (i / 200.0f);
                paint.PaintAt(new Vector3(x, 0.5f, 0.0f), 0, Color.black, brushRadiusPixels: 4);
            }

            paint.Apply();
            Color32[] pixels = paint.Atlas.GetPixels32();
            Color32 white = Color.white;

            // Scan the row with the MOST painted pixels in it, not an
            // assumed row-centre -- the test triangle's own UV places the
            // stroke's real vertical centre wherever PaintAtlas.Coordinate-
            // style math happens to land it within the row, not necessarily
            // exactly Cell/2, and guessing wrong here would scan a row the
            // stroke barely touches at all rather than its own real centre.
            int bestY = -1;
            int bestCount = -1;
            for (int y = 0; y < PaintAtlas.Cell; y++)
            {
                int count = 0;
                for (int x = 0; x < PaintAtlas.Width; x++)
                {
                    Color32 c = pixels[y * PaintAtlas.Width + x];
                    if (!(c.r == white.r && c.g == white.g && c.b == white.b))
                    {
                        count++;
                    }
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    bestY = y;
                }
            }
            Assert.Greater(bestCount, 0, "the stroke never painted anything at all");
            int y2 = bestY;

            int firstPainted = -1, lastPainted = -1;
            for (int x = 0; x < PaintAtlas.Width; x++)
            {
                Color32 c = pixels[y2 * PaintAtlas.Width + x];
                bool painted = !(c.r == white.r && c.g == white.g && c.b == white.b);
                if (painted)
                {
                    if (firstPainted < 0) firstPainted = x;
                    lastPainted = x;
                }
            }
            Assert.GreaterOrEqual(lastPainted, firstPainted, "the stroke never painted anything on this scanline");

            int gapPixels = 0;
            for (int x = firstPainted; x <= lastPainted; x++)
            {
                Color32 c = pixels[y2 * PaintAtlas.Width + x];
                if (c.r == white.r && c.g == white.g && c.b == white.b)
                {
                    gapPixels++;
                }
            }

            // A SMALL TOLERANCE, not zero -- a circle rasterised on a pixel
            // grid can leave the odd boundary pixel just outside the
            // mathematical edge even with centres this close together. That
            // is an inherent property of rasterising a round shape at all,
            // not the bug reported -- the ORIGINAL bug's own gaps spanned a
            // large fraction of the stroke's own height/width (confirmed
            // directly rendering a real stroke before this fix), not one
            // stray texel here and there.
            int span = lastPainted - firstPainted + 1;
            float gapFraction = gapPixels / (float)span;
            Assert.Less(gapFraction, 0.02f,
                $"found {gapPixels}/{span} unpainted pixels between the first and last painted column " +
                $"({gapFraction:P1}) -- a real gap inside what should be one continuous stroke");
        }
    }
}
