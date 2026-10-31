using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The full map's compass rose, in its corner (MapPainter.DrawCompass): the full map is always north up, so the rose stands still, its red point up.
    /// Clicks go through it to the map underneath.
    /// </summary>
    public sealed class MapCompassView : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0.0f || rect.height <= 0.0f)
            {
                return;
            }
            var frame = new MapFrame(rect, 0.0f, 0.0f, 1.0f);
            MapPainter.DrawCompass(vh, frame, frame.Centre, Mathf.Min(rect.width, rect.height) * 0.5f, MapPainter.NorthOnPanel(frame));
        }
    }
}
