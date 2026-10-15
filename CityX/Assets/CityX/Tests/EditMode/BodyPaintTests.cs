using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The creator's brush is measured on the body, not in atlas pixels (BodyPaint.BrushStepMetres). Owner, 2026-10-10: "the brush sizes dont
    /// scale up naturally the first one feels very choppy when you move it around and appears as a line or ovel instead of a pixel or circle and
    /// then the 2nd brush size is way bigger". Earlier rounds asked the same of an atlas-pixel brush ("make the paint trail round not a ovel",
    /// "size 1 ... shows up as lines or ovels instead of a dot") and each fix moved the problem: size 1 as one texel is a sliver on a stretched
    /// part, and correcting the stretch per triangle multiplied size 2 by it.
    ///
    /// Every test paints a flat square whose atlas is stretched seven to one, as an arm's is: one texel is about 1.3 mm across and 9 mm up.
    /// What is measured is the paint's size ON THE SQUARE, in metres.
    /// </summary>
    public sealed class BodyPaintTests
    {
        private const float Side = 0.4f;
        private static readonly Vector2 UvLow = new Vector2(0.1f, 0.02f);
        private static readonly Vector2 UvHigh = new Vector2(0.9f, 0.12f);

        /// <summary>The square, facing -z, lying in x and y from 0 to <see cref="Side"/>, with an optional twin facing the other way just behind it.</summary>
        private static BodyPaint BuildStretchedSquare(bool withBack = false)
        {
            var vertices = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(0, Side, 0), new Vector3(Side, Side, 0),
                new Vector3(0, 0, 0), new Vector3(Side, Side, 0), new Vector3(Side, 0, 0),
            };
            var uvs = new System.Collections.Generic.List<Vector2>
            {
                Uv(0, 0), Uv(0, 1), Uv(1, 1),
                Uv(0, 0), Uv(1, 1), Uv(1, 0),
            };
            if (withBack)
            {
                // 1 cm behind, wound the other way (it faces +z), on another row of the atlas.
                const float behind = 0.01f;
                vertices.AddRange(new[]
                {
                    new Vector3(0, 0, behind), new Vector3(Side, Side, behind), new Vector3(0, Side, behind),
                    new Vector3(0, 0, behind), new Vector3(Side, 0, behind), new Vector3(Side, Side, behind),
                });
                Vector2 lift = new Vector2(0.0f, 0.3f);
                uvs.AddRange(new[]
                {
                    Uv(0, 0) + lift, Uv(1, 1) + lift, Uv(0, 1) + lift,
                    Uv(0, 0) + lift, Uv(1, 0) + lift, Uv(1, 1) + lift,
                });
            }
            var mesh = new Mesh { vertices = vertices.ToArray(), uv = uvs.ToArray() };
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }
            mesh.triangles = indices;

            var paint = new BodyPaint(Color.white);
            paint.Bind(mesh);
            return paint;
        }

        private static Vector2 Uv(float s, float t)
        {
            return new Vector2(Mathf.Lerp(UvLow.x, UvHigh.x, s), Mathf.Lerp(UvLow.y, UvHigh.y, t));
        }

        /// <summary>Metres on the square per texel, across and up.</summary>
        private static float TexelAcross => Side / ((UvHigh.x - UvLow.x) * PaintAtlas.Width);

        private static float TexelUp => Side / ((UvHigh.y - UvLow.y) * PaintAtlas.Height);

        /// <summary>How wide and tall the paint on the front square is, in metres (texels counted, times their size).</summary>
        private static void PaintedSize(BodyPaint paint, out float width, out float height)
        {
            paint.Apply();
            Color32[] pixels = paint.Atlas.GetPixels32();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            int rowLimit = Mathf.CeilToInt(UvHigh.y * PaintAtlas.Height) + 2;
            for (int y = 0; y < rowLimit; y++)
            {
                for (int x = 0; x < PaintAtlas.Width; x++)
                {
                    Color32 c = pixels[y * PaintAtlas.Width + x];
                    if (c.r == 255 && c.g == 255 && c.b == 255)
                    {
                        continue;
                    }
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            Assert.LessOrEqual(minX, maxX, "nothing was painted at all");
            width = (maxX - minX + 1) * TexelAcross;
            height = (maxY - minY + 1) * TexelUp;
        }

        private static readonly Vector3 Middle = new Vector3(Side * 0.5f, Side * 0.5f, 0.0f);

        [Test]
        public void TwoBrushesOnTheSameBodyShareOneTexelMapAndKeepTheirOwnPaint()
        {
            // The texel map is the biggest thing the game builds, and every character creator binds the same body: the title screen built a new
            // one on every visit until a test run that loads it dozens of times ran out of memory (2026-10-15). The second brush on the same
            // surface takes the first one's map over; each keeps its own paint.
            BodyPaint first = BuildStretchedSquare();
            first.PrepareBrush();
            int built = BodyPaint.TexelMapsBuilt;
            BodyPaint second = BuildStretchedSquare();
            second.PrepareBrush();
            Assert.AreEqual(built, BodyPaint.TexelMapsBuilt, "the second brush on the same surface built a map of its own");

            second.PaintAt(Middle, 0, Color.black, 6);
            PaintedSize(second, out float width, out _);
            Assert.Greater(width, 0.0f, "the second brush does not paint with the map it took over");
            first.Apply();
            foreach (Color32 c in first.Atlas.GetPixels32())
            {
                Assert.IsTrue(c.r == 255 && c.g == 255 && c.b == 255, "painting with one brush painted the other's body too");
            }

            // The bucket's index is built by whichever brush fills first and shared with the other.
            first.BucketFillAt(Middle, 0, Color.red);
            second.BucketFillAt(Middle, 0, Color.blue);
            first.Apply();
            second.Apply();
            Color32 a = first.Atlas.GetPixel(Mathf.RoundToInt(PaintAtlas.Width * 0.5f), Mathf.RoundToInt(PaintAtlas.Height * (UvLow.y + UvHigh.y) * 0.5f));
            Color32 b = second.Atlas.GetPixel(Mathf.RoundToInt(PaintAtlas.Width * 0.5f), Mathf.RoundToInt(PaintAtlas.Height * (UvLow.y + UvHigh.y) * 0.5f));
            Assert.AreEqual(255, a.r, "the first brush's fill did not take");
            Assert.AreEqual(255, b.b, "the second brush's fill did not take");

            BodyPaint other = BuildStretchedSquare(withBack: true);
            other.PrepareBrush();
            Assert.AreEqual(built + 1, BodyPaint.TexelMapsBuilt, "a different surface did not get a map of its own");
        }

        [Test]
        public void AStampIsRoundOnTheBodyHoweverStretchedTheAtlasIs()
        {
            BodyPaint paint = BuildStretchedSquare();
            paint.PaintAt(Middle, 0, Color.black, 6);
            PaintedSize(paint, out float width, out float height);
            float diameter = 2.0f * BodyPaint.BrushRadiusMetres(6);
            Assert.AreEqual(diameter, width, 2.0f * TexelAcross, $"the stamp is {width * 100:0.0} cm wide, not the brush's {diameter * 100:0.0} cm");
            Assert.AreEqual(diameter, height, TexelUp, $"the stamp is {height * 100:0.0} cm tall, not the brush's {diameter * 100:0.0} cm");
        }

        [Test]
        public void SizeOneIsASmallDotNotALine()
        {
            BodyPaint paint = BuildStretchedSquare();
            paint.PaintAt(Middle, 0, Color.black, 1);
            PaintedSize(paint, out float width, out float height);
            Assert.Less(Mathf.Max(width, height), 0.025f, $"size 1 painted {width * 100:0.0} x {height * 100:0.0} cm -- too big for the smallest brush");
            Assert.Less(Mathf.Max(width, height) / Mathf.Min(width, height), 2.0f,
                $"size 1 painted {width * 100:0.0} x {height * 100:0.0} cm -- a line, not a dot");
        }

        [Test]
        public void EverySizeIsTheSameStepWiderThanTheLast()
        {
            float previous = 0.0f;
            for (int size = 1; size <= 15; size++)
            {
                BodyPaint paint = BuildStretchedSquare();
                paint.PaintAt(Middle, 0, Color.black, size);
                PaintedSize(paint, out float width, out _);
                Assert.Greater(width, previous, $"size {size} is no wider than size {size - 1}");
                if (size > 1)
                {
                    float step = width - previous;
                    Assert.AreEqual(2.0f * BodyPaint.BrushStepMetres, step, 3.0f * TexelAcross,
                        $"size {size} is {step * 100:0.00} cm wider than size {size - 1}; every step should add the same");
                }
                previous = width;
            }
        }

        [Test]
        public void AStrokeIsOneUnbrokenLine()
        {
            BodyPaint paint = BuildStretchedSquare();
            var from = new Vector3(0.05f, Side * 0.5f, 0.0f);
            var to = new Vector3(Side - 0.05f, Side * 0.5f, 0.0f);
            paint.PaintStroke(from, 0, to, 0, Color.black, 1);
            paint.Apply();

            Color32[] pixels = paint.Atlas.GetPixels32();
            int row = Mathf.FloorToInt((UvLow.y + (UvHigh.y - UvLow.y) * 0.5f) * PaintAtlas.Height);
            int first = Mathf.CeilToInt((UvLow.x + (UvHigh.x - UvLow.x) * (from.x / Side)) * PaintAtlas.Width);
            int last = Mathf.FloorToInt((UvLow.x + (UvHigh.x - UvLow.x) * (to.x / Side)) * PaintAtlas.Width) - 1;
            int gaps = 0;
            for (int x = first; x <= last; x++)
            {
                Color32 c = pixels[row * PaintAtlas.Width + x];
                if (c.r == 255 && c.g == 255 && c.b == 255)
                {
                    gaps++;
                }
            }
            Assert.AreEqual(0, gaps, $"a size-1 stroke left {gaps} unpainted texels along its own middle");
        }

        [Test]
        public void ABigBrushDoesNotComeThroughOnTheBack()
        {
            BodyPaint paint = BuildStretchedSquare(withBack: true);
            paint.PaintAt(Middle, 0, Color.black, 15);
            paint.Apply();
            Color32[] pixels = paint.Atlas.GetPixels32();
            int backFrom = Mathf.FloorToInt((UvLow.y + 0.3f) * PaintAtlas.Height);
            int backTo = Mathf.CeilToInt((UvHigh.y + 0.3f) * PaintAtlas.Height);
            int front = 0;
            int back = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r == 255 && pixels[i].g == 255 && pixels[i].b == 255)
                {
                    continue;
                }
                int y = i / PaintAtlas.Width;
                if (y >= backFrom && y <= backTo) back++;
                else front++;
            }
            Assert.Greater(front, 0, "the front was not painted");
            Assert.AreEqual(0, back, "the brush painted through to the side facing away from it");
        }

        /// <summary>Paint saved before the atlas got finer (Godot's size) still loads, every old texel scaled up to a block in the same place.</summary>
        [Test]
        public void PaintSavedAtTheOldSizeStillLoads()
        {
            int width = PaintAtlas.LegacyWidth;
            int height = PaintAtlas.LegacyHeight;
            var raw = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                raw[i * 4] = raw[i * 4 + 1] = raw[i * 4 + 2] = raw[i * 4 + 3] = 255;
            }
            // One red texel, in the old atlas's row 3, column 100.
            int marked = (3 * PaintAtlas.Cell + 10) * width + 100;
            raw[marked * 4 + 1] = raw[marked * 4 + 2] = 0;
            byte[] saved;
            using (var output = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Fastest))
                {
                    zip.Write(raw, 0, raw.Length);
                }
                saved = output.ToArray();
            }

            var paint = new BodyPaint(Color.white);
            Assert.IsTrue(paint.Decode(saved), "an old-size save would not load");
            Color32[] pixels = paint.Atlas.GetPixels32();
            int x = 100 * PaintAtlas.ColumnDetail;
            int y = (3 * PaintAtlas.Cell + 10) * PaintAtlas.RowDetail;
            for (int dy = 0; dy < PaintAtlas.RowDetail; dy++)
            {
                for (int dx = 0; dx < PaintAtlas.ColumnDetail; dx++)
                {
                    Color32 c = pixels[(y + dy) * PaintAtlas.Width + x + dx];
                    Assert.AreEqual(255, c.r);
                    Assert.AreEqual(0, c.g, "the old red texel did not become a red block in the same place");
                }
            }
            Assert.AreEqual(255, pixels[y * PaintAtlas.Width + x - 1].g, "the red spread past its own block");
        }

        /// <summary>
        /// What a player's paint costs to send: every player's goes to everyone (NetworkedPlayerIdentity). A busy body -- a hundred strokes of
        /// different colours -- must stay small, because the finer atlas holds eight times the texels of Godot's.
        /// </summary>
        [Test]
        public void ABusilyPaintedBodyStillSavesSmall()
        {
            BodyPaint paint = BuildStretchedSquare();
            var random = new System.Random(7);
            for (int stroke = 0; stroke < 100; stroke++)
            {
                var from = new Vector3((float)random.NextDouble() * Side, (float)random.NextDouble() * Side, 0.0f);
                var to = new Vector3((float)random.NextDouble() * Side, (float)random.NextDouble() * Side, 0.0f);
                var colour = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                paint.PaintStroke(from, 0, to, 0, colour, 1 + random.Next(6));
            }
            paint.Apply();
            byte[] saved = paint.Encode();
            Assert.Less(saved.Length, 200 * 1024, $"a busy body saves to {saved.Length / 1024} KB");
            var back = new BodyPaint(Color.white);
            Assert.IsTrue(back.Decode(saved));
            CollectionAssert.AreEqual(paint.Atlas.GetPixels32(), back.Atlas.GetPixels32(), "the paint did not come back exactly");
        }

        /// <summary>
        /// At a joint the hidden end of one tube uses the same texels as the visible tube over it (the arm's row maps by position along the arm).
        /// Painting the visible surface must still paint them, wherever else they are drawn: the stroke down an arm broke off at the elbow.
        /// </summary>
        [Test]
        public void TexelsSharedWithAHiddenSurfaceArePaintedFromTheVisibleOne()
        {
            Vector2[] squareUvs = { Uv(0, 0), Uv(0, 1), Uv(1, 1), Uv(0, 0), Uv(1, 1), Uv(1, 0) };
            var vertices = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(0, Side, 0), new Vector3(Side, Side, 0),
                new Vector3(0, 0, 0), new Vector3(Side, Side, 0), new Vector3(Side, 0, 0),
            };
            // The hidden one, 5 cm inside, facing the same way, LAST -- so it is the one a single place per texel would keep.
            foreach (Vector3 v in vertices.ToArray())
            {
                vertices.Add(v + new Vector3(0, 0, 0.05f));
            }
            var uvs = new System.Collections.Generic.List<Vector2>(squareUvs);
            uvs.AddRange(squareUvs);
            var mesh = new Mesh { vertices = vertices.ToArray(), uv = uvs.ToArray() };
            mesh.triangles = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Range(0, vertices.Count));
            var paint = new BodyPaint(Color.white);
            paint.Bind(mesh);

            paint.PaintAt(Middle, 0, Color.black, 3);
            PaintedSize(paint, out float width, out float height);
            float diameter = 2.0f * BodyPaint.BrushRadiusMetres(3);
            Assert.AreEqual(diameter, width, 2.0f * TexelAcross, "the visible surface's texels were not all painted");
            Assert.AreEqual(diameter, height, TexelUp, "the visible surface's texels were not all painted");
        }

        [Test]
        public void TheMirroredStampLandsOnTheOtherSide()
        {
            // The square moved to straddle x = 0: a stamp at x = +0.1 mirrors to x = -0.1.
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.2f, 0, 0), new Vector3(-0.2f, 0.4f, 0), new Vector3(0.2f, 0.4f, 0),
                    new Vector3(-0.2f, 0, 0), new Vector3(0.2f, 0.4f, 0), new Vector3(0.2f, 0, 0),
                },
                uv = new[] { Uv(0, 0), Uv(0, 1), Uv(1, 1), Uv(0, 0), Uv(1, 1), Uv(1, 0) },
                triangles = new[] { 0, 1, 2, 3, 4, 5 },
            };
            var paint = new BodyPaint(Color.white);
            paint.Bind(mesh);
            paint.PaintMirroredAt(new Vector3(0.1f, 0.2f, 0.0f), 0, Color.black, 2);
            paint.Apply();
            Color32[] pixels = paint.Atlas.GetPixels32();
            int row = Mathf.FloorToInt((UvLow.y + (UvHigh.y - UvLow.y) * 0.5f) * PaintAtlas.Height);
            int left = Mathf.FloorToInt(Mathf.Lerp(UvLow.x, UvHigh.x, 0.25f) * PaintAtlas.Width);
            int right = Mathf.FloorToInt(Mathf.Lerp(UvLow.x, UvHigh.x, 0.75f) * PaintAtlas.Width);
            Assert.AreNotEqual(255, pixels[row * PaintAtlas.Width + left].r, "the mirrored stamp is not at x = -0.1");
            Assert.AreEqual(255, pixels[row * PaintAtlas.Width + right].r, "the mirror also painted the original spot");
        }
    }
}
