using System.Collections.Generic;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The map's compass rose (owner, 2026-10-30: "is there a compass on the map? if not there probably should be"): its red point is the one that points
    /// north, whichever way north is on the panel, and north on an unturned map is straight up.
    /// </summary>
    public sealed class MapCompassTests
    {
        private static readonly Rect Panel = new Rect(-50.0f, -50.0f, 100.0f, 100.0f);

        /// <summary>The farthest red vertex from the rose's centre, as a direction in the panel's own pixels (+Y down).</summary>
        private static Vector2 RedPointDirection(Vector2 north)
        {
            var frame = new MapFrame(Panel, 0.0f, 0.0f, 1.0f);
            var vh = new VertexHelper();
            MapPainter.DrawCompass(vh, frame, frame.Centre, 40.0f, north);
            var vertices = new List<UIVertex>();
            vh.GetUIVertexStream(vertices);
            Vector3 centre = frame.Vertex(frame.Centre, Color.white).position;
            Vector2 farthest = Vector2.zero;
            foreach (UIVertex v in vertices)
            {
                Color c = v.color;
                Color red = MapPainter.CompassNorthColour;
                if (Mathf.Abs(c.r - red.r) + Mathf.Abs(c.g - red.g) + Mathf.Abs(c.b - red.b) > 0.02f)
                {
                    continue;
                }
                Vector2 offset = v.position - centre;
                if (offset.sqrMagnitude > farthest.sqrMagnitude)
                {
                    farthest = offset;
                }
            }
            vh.Dispose();
            return new Vector2(farthest.x, -farthest.y).normalized;   // vertex space is +Y up, the panel +Y down
        }

        [Test]
        public void TheRedPointPointsNorthWhereverNorthIs()
        {
            foreach (Vector2 north in new[] { new Vector2(0.0f, -1.0f), new Vector2(1.0f, 0.0f), new Vector2(-0.6f, 0.8f) })
            {
                Vector2 red = RedPointDirection(north);
                Assert.Greater(Vector2.Dot(red, north.normalized), 0.99f, $"the red point is at {red}, north at {north}");
            }
        }

        [Test]
        public void NorthOnAnUnturnedMapIsStraightUpAndTurnsWithTheMinimap()
        {
            Vector2 up = MapPainter.NorthOnPanel(new MapFrame(Panel, 0.0f, 0.0f, 1.0f));
            Assert.Less(Vector2.Distance(up, new Vector2(0.0f, -1.0f)), 1e-4f, "north is not at the top of the north-up map");
            // Facing east (+X) on the turned minimap, north is to the left.
            var facingEast = new MapFrame(Panel, 0.0f, 0.0f, 1.0f, MapFrame.RotationPuttingUp(new Vector2(1.0f, 0.0f)));
            Assert.Less(Vector2.Distance(MapPainter.NorthOnPanel(facingEast), new Vector2(-1.0f, 0.0f)), 1e-3f, "facing east, north is not to the left");
        }
    }
}
