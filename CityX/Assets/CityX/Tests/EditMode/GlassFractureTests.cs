using System.Collections.Generic;
using System.Linq;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// HOW A WINDOW BREAKS (owner, 2026-10-15: "improve upon the window fracturing system"): a pane is cut round the blow into the pattern struck
    /// glass makes -- the pieces cover it exactly once, slivers round the blow and big shards at the frame, the same for the same seed, a shop
    /// window a few dozen pieces, in microseconds -- and the pane cut is where the city drew that window's glass.
    /// </summary>
    public sealed class GlassFractureTests
    {
        private static readonly (float w, float h, Vector2 at)[] Panes =
        {
            (1.3f, 1.9f, new Vector2(0.65f, 0.95f)),   // an ordinary window, struck in the middle
            (1.3f, 1.9f, new Vector2(0.05f, 0.1f)),    // ... near its corner
            (1.3f, 1.9f, new Vector2(1.3f, 1.2f)),     // ... on its edge
            (3.1f, 2.4f, new Vector2(1.0f, 1.0f)),     // a shop window
            (0.5f, 0.7f, new Vector2(0.2f, 0.5f)),     // a small one
        };

        private static bool Inside(Vector2[] polygon, Vector2 p)
        {
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                if ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) < 0.0f)
                {
                    return false;
                }
            }
            return true;
        }

        [Test]
        public void ThePiecesCoverThePaneExactlyOnceAndEachIsAConvexPieceOfIt()
        {
            int seed = 11;
            foreach ((float w, float h, Vector2 at) in Panes)
            {
                List<GlassPiece> pieces = GlassFracture.Fracture(w, h, at, seed++);
                float area = 0.0f;
                foreach (GlassPiece piece in pieces)
                {
                    Assert.Greater(GlassFracture.SignedArea(piece.Corners), 0.0f, "a piece is wound the wrong way or is empty");
                    for (int i = 0; i < piece.Corners.Length; i++)
                    {
                        Vector2 p = piece.Corners[i];
                        Assert.That(p.x >= -1e-4f && p.x <= w + 1e-4f && p.y >= -1e-4f && p.y <= h + 1e-4f, $"a corner {p} is off the {w} x {h} pane");
                        Vector2 a = piece.Corners[i];
                        Vector2 b = piece.Corners[(i + 1) % piece.Corners.Length];
                        Vector2 c = piece.Corners[(i + 2) % piece.Corners.Length];
                        float turn = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                        Assert.GreaterOrEqual(turn, -1e-5f, "a piece is not convex");
                    }
                    area += GlassFracture.Area(piece.Corners);
                }
                Assert.AreEqual(w * h, area, w * h * 0.001f, $"the pieces of a {w} x {h} pane do not add up to it");

                // Every point of the pane is in exactly one piece.
                var random = new System.Random(seed);
                for (int s = 0; s < 400; s++)
                {
                    var p = new Vector2((float)random.NextDouble() * w, (float)random.NextDouble() * h);
                    int holders = pieces.Count(piece => Inside(piece.Corners, p));
                    Assert.AreEqual(1, holders, $"the point {p} of a {w} x {h} pane is in {holders} pieces");
                }
            }
        }

        [Test]
        public void SliversRoundTheBlowBigShardsAtTheFrameAndTheFrameOnesAreKnown()
        {
            List<GlassPiece> pieces = GlassFracture.Fracture(1.3f, 1.9f, new Vector2(0.65f, 0.95f), 3);
            float inner = pieces.Where(p => p.Ring == 0).Average(p => GlassFracture.Area(p.Corners));
            int outerRing = pieces.Max(p => p.Ring);
            float outer = pieces.Where(p => p.Ring == outerRing).Average(p => GlassFracture.Area(p.Corners));
            Assert.Less(inner * 10.0f, outer, "the pieces round the blow should be slivers next to those at the frame");
            Assert.GreaterOrEqual(pieces.Count(p => p.Ring == 0), GlassFracture.MinRadials, "a sliver for every radial crack");
            foreach (GlassPiece piece in pieces.Where(p => p.Ring == 0))
            {
                Assert.IsTrue(piece.Corners.Any(c => (c - new Vector2(0.65f, 0.95f)).sqrMagnitude < 1e-8f), "a sliver's tip is at the blow");
                Assert.IsFalse(piece.OnTheFrame);
            }
            Assert.Greater(pieces.Count(p => p.OnTheFrame), 4, "the pieces along the frame should be known as such");
        }

        [Test]
        public void TheSameSeedBreaksTheSameWayAndAShopWindowIsAFewDozenPieces()
        {
            List<GlassPiece> one = GlassFracture.Fracture(1.3f, 1.9f, new Vector2(0.4f, 0.7f), 42);
            List<GlassPiece> two = GlassFracture.Fracture(1.3f, 1.9f, new Vector2(0.4f, 0.7f), 42);
            List<GlassPiece> other = GlassFracture.Fracture(1.3f, 1.9f, new Vector2(0.4f, 0.7f), 43);
            Assert.AreEqual(one.Count, two.Count);
            for (int i = 0; i < one.Count; i++)
            {
                CollectionAssert.AreEqual(one[i].Corners, two[i].Corners, "the same seed broke differently");
            }
            Assert.IsFalse(one.Count == other.Count && one.Zip(other, (a, b) => a.Corners.SequenceEqual(b.Corners)).All(x => x),
                "a different seed broke the same way");

            List<GlassPiece> shop = GlassFracture.Fracture(3.1f, 2.4f, new Vector2(1.2f, 1.0f), 5);
            Assert.That(shop.Count, Is.InRange(20, GlassFracture.MaxPieces), "a shop window's pieces");
            Assert.Less(shop.Max(p => GlassFracture.Area(p.Corners)), 0.8f, "a shard the size of a door");
        }

        [Test]
        public void BreakingAPaneTakesMicroseconds()
        {
            GlassFracture.Fracture(1.3f, 1.9f, new Vector2(0.6f, 0.9f), 1);   // warm up
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int count = 500;
            int pieces = 0;
            for (int i = 0; i < count; i++)
            {
                pieces += GlassFracture.Fracture(1.3f, 1.9f, new Vector2(0.2f + (i % 9) * 0.1f, 0.3f + (i % 13) * 0.1f), i).Count;
            }
            watch.Stop();
            double each = watch.Elapsed.TotalMilliseconds / count;
            Debug.Log($"CityX glass fracture: {each * 1000.0:0} microseconds a pane, {pieces / (float)count:0.0} pieces each");
            Assert.Less(each, 1.0, $"cutting one pane took {each:0.000} ms");
        }

        // --- where the glass is ---

        private const int Seed = 20260810;

        [Test]
        public void ThePaneCutIsTheGlassTheCityDrewForThatWindow()
        {
            var world = new CityWorld(Seed);
            int checkedPanes = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 4))
            {
                if (site.Sides >= 3)
                {
                    continue;
                }
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                for (int row = 1; row < Mathf.Min(rows.Count, 4); row++)
                {
                    for (int column = 0; column < columns.Count; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                        {
                            continue;
                        }
                        Vector3 point = TestSites.At(site, (float)site.MinX + (column + 0.5f) * columns.Size, site.BaseHeight + (row + 0.5f) * rows.Size,
                            (float)site.MaxZ + 0.02f);
                        Assert.IsTrue(GlassDamage.TryPaneAt(world, site, point, out PaneKey key));
                        Assert.IsTrue(GlassPaneFrame.TryFrame(world, site, key, point, out PaneFrame frame), "no frame for a real window");
                        Assert.Less(frame.Width, columns.Size, "the glass is narrower than its cell (it has a frame)");
                        Assert.Greater(frame.Width, columns.Size * 0.4f, "the glass is far too narrow");
                        Assert.Less(frame.Height, rows.Size);
                        Assert.Greater(frame.Height, rows.Size * 0.3f);
                        Vector2 at = frame.Of(point);
                        Assert.AreEqual(frame.Width * 0.5f, at.x, 0.02f, "the middle of the cell should be the middle of its glass across");
                        Assert.That(at.y, Is.InRange(0.0f, frame.Height), "the blow is not on the glass");
                        Assert.AreEqual(0.0f, Vector3.Dot(point - frame.Origin, frame.Normal), 0.001f, "the cut is not in the glass's plane");
                        Assert.Greater(Vector3.Dot(frame.Normal, CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North)), 0.99f);
                        // The four corners are on the window: a point just inside each is the same pane.
                        foreach (Vector2 corner in new[] { new Vector2(0.03f, 0.03f), new Vector2(frame.Width - 0.03f, 0.03f),
                                     new Vector2(0.03f, frame.Height - 0.03f), new Vector2(frame.Width - 0.03f, frame.Height - 0.03f) })
                        {
                            Assert.IsTrue(GlassDamage.TryPaneAt(world, site, frame.At(corner) + frame.Normal * 0.02f, out PaneKey there) && there.Equals(key),
                                "a corner of the cut is not on the same window");
                        }
                        checkedPanes++;
                    }
                }
                if (checkedPanes > 30)
                {
                    break;
                }
            }
            Assert.Greater(checkedPanes, 10, "not enough real windows checked");
        }
    }
}
