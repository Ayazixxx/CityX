using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// GRAFFITI, THE PURE PARTS (owner, 2026-10-15: a spray can that paints on walls, "visible to other players online", lasting past log-off
    /// later): the spray's paint, a stroke, paint over paint, the bytes a piece is sent and saved as, and the checks a piece from another machine
    /// must pass.
    /// </summary>
    public sealed class GraffitiTests
    {
        private static Color32 At(GraffitiCanvas canvas, int x, int y) => canvas.Pixels[y * GraffitiCanvas.Size + x];

        [Test]
        public void APuffIsSolidInTheMiddleSoftAtTheEdgeAndLeavesTheRestBare()
        {
            var canvas = new GraffitiCanvas();
            canvas.Clear();
            Assert.IsTrue(canvas.IsEmpty);
            canvas.Spray(100.0f, 100.0f, 10.0f, Color.red);
            Color32 middle = At(canvas, 100, 100);
            Assert.Greater(middle.a, 200, "the middle of a puff is solid");
            Assert.Greater(middle.r, 240);
            Assert.Less(middle.g, 20);
            Assert.Less(At(canvas, 108, 100).a, middle.a, "the edge is softer than the middle");
            Assert.AreEqual(0, At(canvas, 115, 100).a, "paint outside the brush");
            Assert.AreEqual(0, At(canvas, 10, 10).a, "paint far away");
            Assert.IsFalse(canvas.IsEmpty);
        }

        [Test]
        public void PaintGoesOverPaintAndAStrokeLeavesNoGaps()
        {
            var canvas = new GraffitiCanvas();
            canvas.Clear();
            canvas.Spray(50.0f, 50.0f, 8.0f, Color.red);
            for (int i = 0; i < 4; i++)
            {
                canvas.Spray(50.0f, 50.0f, 8.0f, Color.blue);
            }
            Color32 over = At(canvas, 50, 50);
            Assert.Greater(over.b, 200, "blue over red should end blue");
            Assert.Less(over.r, 60);

            canvas.Clear();
            canvas.Stroke(new Vector2(20.0f, 200.0f), new Vector2(220.0f, 200.0f), 4.0f, Color.green);
            for (int x = 22; x <= 218; x += 2)
            {
                Assert.Greater(At(canvas, x, 200).a, 60, $"a gap in the stroke at x {x}");
            }
        }

        [Test]
        public void PaintDoesNotStickWhereTheMaskSaysItIsNotTheWall()
        {
            var canvas = new GraffitiCanvas();
            canvas.Clear();
            var mask = new byte[GraffitiCanvas.Size * GraffitiCanvas.Size];
            for (int y = 0; y < GraffitiCanvas.Size; y++)
            {
                for (int x = 0; x < GraffitiCanvas.Size / 2; x++)
                {
                    mask[y * GraffitiCanvas.Size + x] = 255;   // the left half is the wall; past x 128 is the air beyond its corner
                }
            }
            canvas.Mask = mask;
            canvas.Stroke(new Vector2(40.0f, 100.0f), new Vector2(220.0f, 100.0f), 8.0f, Color.red);
            Assert.Greater(At(canvas, 80, 100).a, 100, "no paint on the wall");
            for (int x = 129; x < GraffitiCanvas.Size; x++)
            {
                Assert.AreEqual(0, At(canvas, x, 100).a, $"paint stuck to thin air at x {x}");
            }

            canvas.Mask = new byte[7];
            Assert.IsNull(canvas.Mask, "a mask of the wrong size must not be taken");
        }

        [Test]
        public void APieceTravelsAndKeepsAsAFewKilobytesAndGarbageIsRefused()
        {
            var canvas = new GraffitiCanvas();
            canvas.Clear();
            canvas.Stroke(new Vector2(30.0f, 30.0f), new Vector2(220.0f, 180.0f), 9.0f, new Color(1.0f, 0.8f, 0.1f));
            canvas.Spray(128.0f, 128.0f, 20.0f, new Color(0.2f, 0.3f, 0.9f));
            byte[] bytes = canvas.Encode();
            Assert.Less(bytes.Length, GraffitiCanvas.MaxEncodedBytes, "too heavy to send");
            Assert.Less(bytes.Length, 32 * 1024, "a simple piece should be a few kilobytes");

            var back = new GraffitiCanvas();
            Assert.IsTrue(back.Decode(bytes));
            CollectionAssert.AreEqual(canvas.Pixels, back.Pixels, "the piece came back different");

            Assert.IsFalse(back.Decode(new byte[] { 1, 2, 3, 4 }), "garbage decoded");
            Assert.IsFalse(back.Decode(null));
            CollectionAssert.AreEqual(canvas.Pixels, back.Pixels, "a refused decode changed the piece");
        }

        [Test]
        public void APieceFromAnotherMachineMustBeASaneOne()
        {
            var canvas = new GraffitiCanvas();
            canvas.Clear();
            canvas.Spray(10.0f, 10.0f, 3.0f, Color.white);
            var good = new GraffitiPiece { Id = 1, Centre = new Vector3(5, 3, 9), Normal = Vector3.forward, Size = GraffitiWall.PieceSize, Paint = canvas.Encode() };
            Assert.IsTrue(GraffitiWall.IsSane(good));

            GraffitiPiece bad = good;
            bad.Centre = new Vector3(float.NaN, 0, 0);
            Assert.IsFalse(GraffitiWall.IsSane(bad), "a NaN position");
            bad = good;
            bad.Normal = Vector3.zero;
            Assert.IsFalse(GraffitiWall.IsSane(bad), "no wall direction");
            bad = good;
            bad.Size = 500.0f;
            Assert.IsFalse(GraffitiWall.IsSane(bad), "a piece the size of a building");
            bad = good;
            bad.Paint = new byte[GraffitiCanvas.MaxEncodedBytes + 1];
            Assert.IsFalse(GraffitiWall.IsSane(bad), "paint too heavy");
            bad.Paint = null;
            Assert.IsFalse(GraffitiWall.IsSane(bad), "no paint");
        }

        [Test]
        public void TheSameWallSpotHasTheSameIdOnEveryMachine()
        {
            Vector3 spot = new Vector3(12.34f, 5.0f, -8.76f);
            Assert.AreEqual(GraffitiWall.IdFor(spot, Vector3.right), GraffitiWall.IdFor(spot + new Vector3(0.0f, 0.0f, 0.01f), Vector3.right));
            Assert.AreNotEqual(GraffitiWall.IdFor(spot, Vector3.right), GraffitiWall.IdFor(spot, Vector3.left), "the other side of the wall");
            Assert.AreNotEqual(GraffitiWall.IdFor(spot, Vector3.right), GraffitiWall.IdFor(spot + Vector3.up * 3.0f, Vector3.right));
        }
    }
}
