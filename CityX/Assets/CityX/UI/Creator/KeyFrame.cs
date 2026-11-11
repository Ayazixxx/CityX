using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// A key cap's box: one square frame, drawn as one shape (owner, 2026-11-10: "get rid of the intersecting lines in the corners of the
    /// key just make it a smooth box around it"). Four separate sides overlapped at the corners and, faded, showed every overlap.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KeyFrame : MaskableGraphic
    {
        [Tooltip("How thick the frame is, in canvas units.")]
        public float Thickness = 5.0f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float t = Mathf.Min(Thickness, Mathf.Min(r.width, r.height) * 0.5f);
            // The top and bottom across the whole width, the sides between them: no two pieces cover the same place.
            Quad(vh, r.xMin, r.yMax - t, r.xMax, r.yMax);
            Quad(vh, r.xMin, r.yMin, r.xMax, r.yMin + t);
            Quad(vh, r.xMin, r.yMin + t, r.xMin + t, r.yMax - t);
            Quad(vh, r.xMax - t, r.yMin + t, r.xMax, r.yMax - t);
        }

        private void Quad(VertexHelper vh, float x0, float y0, float x1, float y1)
        {
            int first = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), color, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), color, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), color, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), color, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first + 2, first + 3, first);
        }
    }
}
