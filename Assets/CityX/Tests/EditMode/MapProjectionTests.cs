using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The map panel's own world-to-screen maths. What matters here is what
    /// Godot's own comment on _current_scale flags: the drag, the wheel and
    /// the draw have to agree, or the map slides as it zooms.
    /// </summary>
    public sealed class MapProjectionTests
    {
        [Test]
        public void TheViewerSitsExactlyAtThePanelCentre()
        {
            var panel = new Vector2(800.0f, 600.0f);
            float scale = MapProjection.ScaleFor(panel, chunkSize: 24.0f, zoom: 1.0f);
            Vector2 centre = panel * 0.5f;

            Vector2 screen = MapProjection.ToScreen(centre, worldX: 100.0f, worldZ: 200.0f,
                viewX: 100.0f, viewZ: 200.0f, scale);

            Assert.AreEqual(centre.x, screen.x, 0.001f);
            Assert.AreEqual(centre.y, screen.y, 0.001f);
        }

        [Test]
        public void WorldEastAndSouthMoveRightAndDownOnScreen()
        {
            var panel = new Vector2(800.0f, 600.0f);
            float scale = MapProjection.ScaleFor(panel, chunkSize: 24.0f, zoom: 1.0f);
            Vector2 centre = panel * 0.5f;

            Vector2 east = MapProjection.ToScreen(centre, 10.0f, 0.0f, 0.0f, 0.0f, scale);
            Vector2 south = MapProjection.ToScreen(centre, 0.0f, 10.0f, 0.0f, 0.0f, scale);

            Assert.Greater(east.x, centre.x, "world +X should move right on screen");
            Assert.AreEqual(centre.y, east.y, 0.001f, "world +X should not move vertically");
            Assert.Greater(south.y, centre.y, "world +Z (south) should move down on screen");
            Assert.AreEqual(centre.x, south.x, 0.001f, "world +Z should not move horizontally");
        }

        [Test]
        public void ScaleGrowsWithZoom()
        {
            var panel = new Vector2(800.0f, 600.0f);

            float atOne = MapProjection.ScaleFor(panel, 24.0f, zoom: 1.0f);
            float atTwo = MapProjection.ScaleFor(panel, 24.0f, zoom: 2.0f);

            Assert.AreEqual(atOne * 2.0f, atTwo, 0.0001f);
        }

        [Test]
        public void ScaleIsDerivedFromTheShorterAxis()
        {
            // A wide panel: the shorter axis is the height, so ViewChunks
            // should be exactly what's visible top-to-bottom, not side-to-side.
            var wide = new Vector2(2000.0f, 500.0f);
            float scale = MapProjection.ScaleFor(wide, chunkSize: 24.0f, zoom: 1.0f);

            Assert.AreEqual(500.0f / (MapProjection.ViewChunks * 24.0f), scale, 0.0001f);
        }

        [Test]
        public void AZeroSizedPanelDoesNotDivideByZero()
        {
            Assert.DoesNotThrow(() => MapProjection.ScaleFor(Vector2.zero, 24.0f, 1.0f));
            Assert.AreEqual(1.0f, MapProjection.ScaleFor(Vector2.zero, 24.0f, 1.0f));
        }

        [Test]
        public void ZoomClampsToItsOwnRange()
        {
            Assert.AreEqual(MapProjection.ZoomMin, MapProjection.ClampZoom(0.001f));
            Assert.AreEqual(MapProjection.ZoomMax, MapProjection.ClampZoom(999.0f));
            Assert.AreEqual(1.5f, MapProjection.ClampZoom(1.5f));
        }

        /// <summary>
        /// The whole point of PanAdjustmentForZoom: apply it and whatever was
        /// under the cursor before the zoom is still under the cursor after.
        /// </summary>
        [Test]
        public void ThePointUnderTheCursorStaysUnderTheCursorAcrossAZoom()
        {
            var panel = new Vector2(800.0f, 600.0f);
            Vector2 centre = panel * 0.5f;
            var cursor = new Vector2(560.0f, 340.0f); // off-centre on purpose
            Vector2 offset = cursor - centre;

            float chunkSize = 24.0f;
            float viewX = 50.0f, viewZ = 50.0f;
            float before = MapProjection.ScaleFor(panel, chunkSize, zoom: 1.0f);

            // The world point currently under the cursor.
            Vector2 worldUnderCursorBefore = new Vector2(viewX, viewZ) + offset / before;

            float after = MapProjection.ScaleFor(panel, chunkSize, zoom: 2.0f);
            Vector2 pan = MapProjection.PanAdjustmentForZoom(offset, before, after);
            float newViewX = viewX + pan.x;
            float newViewZ = viewZ + pan.y;

            Vector2 worldUnderCursorAfter = new Vector2(newViewX, newViewZ) + offset / after;

            Assert.AreEqual(worldUnderCursorBefore.x, worldUnderCursorAfter.x, 0.01f);
            Assert.AreEqual(worldUnderCursorBefore.y, worldUnderCursorAfter.y, 0.01f);
        }

        [Test]
        public void ChunksFullyPastEveryEdgeAreOffScreen()
        {
            var panel = new Vector2(800.0f, 600.0f);

            Assert.IsTrue(MapProjection.IsOffScreen(new Vector2(900.0f, 0.0f), 50.0f, panel), "past the right edge");
            Assert.IsTrue(MapProjection.IsOffScreen(new Vector2(0.0f, 700.0f), 50.0f, panel), "past the bottom edge");
            Assert.IsTrue(MapProjection.IsOffScreen(new Vector2(-100.0f, 0.0f), 50.0f, panel), "past the left edge");
            Assert.IsTrue(MapProjection.IsOffScreen(new Vector2(0.0f, -100.0f), 50.0f, panel), "past the top edge");
        }

        [Test]
        public void AChunkStraddlingAnEdgeIsNotOffScreen()
        {
            var panel = new Vector2(800.0f, 600.0f);
            Assert.IsFalse(MapProjection.IsOffScreen(new Vector2(-10.0f, -10.0f), 50.0f, panel));
            Assert.IsFalse(MapProjection.IsOffScreen(new Vector2(790.0f, 590.0f), 50.0f, panel));
        }

        [Test]
        public void HeightFractionIsZeroAtTheFloorAndOneAtTheCeiling()
        {
            Assert.AreEqual(0.0f, MapProjection.HeightFraction(8.0f, 8.0f, 96.0f), 0.0001f);
            Assert.AreEqual(1.0f, MapProjection.HeightFraction(96.0f, 8.0f, 96.0f), 0.0001f);
            Assert.AreEqual(0.5f, MapProjection.HeightFraction(52.0f, 8.0f, 96.0f), 0.0001f);
        }

        [Test]
        public void HeightFractionClampsOutsideItsOwnRange()
        {
            Assert.AreEqual(0.0f, MapProjection.HeightFraction(0.0f, 8.0f, 96.0f));
            Assert.AreEqual(1.0f, MapProjection.HeightFraction(500.0f, 8.0f, 96.0f));
        }
    }
}
