using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The world-to-screen maths behind the map panel, pulled out on its own
    /// so it can be checked without a screen. Ported from ui/map_panel.gd's
    /// own _current_scale/_zoom_at/_draw_map.
    ///
    /// ONE PLACE FOR THE SCALE, matching Godot's own reasoning: the drag, the
    /// wheel and the draw all have to agree on pixels-per-metre or the map
    /// slides sideways as it zooms -- which is exactly what happened in an
    /// earlier pass there before it was centralised.
    /// </summary>
    public static class MapProjection
    {
        /// <summary>How many chunks fit across the shorter screen axis at zoom 1. Godot's own tuned value.</summary>
        public const float ViewChunks = 16.0f;

        public const float ZoomStep = 1.15f;
        public const float ZoomMin = 0.35f;
        public const float ZoomMax = 6.0f;

        /// <summary>Pixels per world metre, including zoom. Derived from the shorter panel axis so ViewChunks is what you actually get across the tighter dimension.</summary>
        public static float ScaleFor(Vector2 panelSize, float chunkSize, float zoom)
        {
            if (panelSize.x <= 0.0f || panelSize.y <= 0.0f || chunkSize <= 0.0f)
            {
                return 1.0f;
            }
            return Mathf.Min(panelSize.x, panelSize.y) / (ViewChunks * chunkSize) * zoom;
        }

        /// <summary>World (x,z) -> panel-local pixels, centred on whatever the view is currently looking at.</summary>
        public static Vector2 ToScreen(Vector2 panelCentre, float worldX, float worldZ,
            float viewX, float viewZ, float scale)
        {
            return panelCentre + new Vector2(worldX - viewX, worldZ - viewZ) * scale;
        }

        public static float ClampZoom(float zoom) => Mathf.Clamp(zoom, ZoomMin, ZoomMax);

        /// <summary>
        /// The pan adjustment that keeps whatever was under the cursor pinned
        /// in place across a zoom change -- without this, zooming toward
        /// something off-centre walks it straight out of view.
        /// </summary>
        public static Vector2 PanAdjustmentForZoom(Vector2 cursorOffsetFromCentre,
            float scaleBefore, float scaleAfter)
        {
            if (scaleBefore <= 0.0f || scaleAfter <= 0.0f)
            {
                return Vector2.zero;
            }
            return cursorOffsetFromCentre / scaleBefore - cursorOffsetFromCentre / scaleAfter;
        }

        /// <summary>Cheap off-screen test: a chunk fully past any edge of the panel costs nothing to skip.</summary>
        public static bool IsOffScreen(Vector2 topLeft, float pixelSize, Vector2 panelSize)
        {
            return topLeft.x > panelSize.x || topLeft.y > panelSize.y ||
                   topLeft.x + pixelSize < 0.0f || topLeft.y + pixelSize < 0.0f;
        }

        /// <summary>How tall a building reads against the low/tall shading range, 0 at MinHeight and 1 at MaxHeight.</summary>
        public static float HeightFraction(float height, float minHeight, float maxHeight)
        {
            if (maxHeight <= minHeight)
            {
                return 0.0f;
            }
            return Mathf.Clamp01((height - minHeight) / (maxHeight - minHeight));
        }
    }
}
