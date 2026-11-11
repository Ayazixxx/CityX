using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// An arrow button's face (<see cref="ArrowShape"/>), drawn in the canvas with no texture: a black outline, the yellow band that shows
    /// between it and the arrow while the button is pointed at (<see cref="Highlight"/>; owner, 2026-11-10: "make yellow outline appear in
    /// between the black outline and the arrow when you hover over the buttons"), the arrow in a gradient from <see cref="Top"/> to
    /// <see cref="Bottom"/>, and a gloss over its upper half. Only the arrow itself takes the pointer, not the empty corners of its box.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArrowGraphic : MaskableGraphic, ICanvasRaycastFilter, IButtonFace
    {
        [Tooltip("The curled arrow (BACK) rather than the straight one (PROCEED).")]
        public bool Curled;

        [Tooltip("The arrow's colour at its top and at its bottom.")]
        public Color Top = new Color(0.50f, 0.83f, 0.20f);

        public Color Bottom = new Color(0.27f, 0.58f, 0.05f);

        public Color OutlineColour = Color.black;

        [Tooltip("The black outline's width, canvas units.")]
        public float OutlineWidth = 3.0f;

        public Color HoverColour = new Color(1.0f, 0.84f, 0.12f);

        [Tooltip("The yellow band's width when fully shown, canvas units.")]
        public float HoverWidth = 4.0f;

        [Tooltip("How white the gloss is at the arrow's top.")]
        [Range(0.0f, 1.0f)]
        public float Gloss = 0.4f;

        private float _highlight;
        private float _pressed;

        /// <summary>0 to 1: how much of the yellow band shows.</summary>
        public float Highlight
        {
            get => _highlight;
            set
            {
                if (!Mathf.Approximately(_highlight, value))
                {
                    _highlight = value;
                    SetVerticesDirty();
                }
            }
        }

        /// <summary>0 to 1: how far the arrow is darkened, held down.</summary>
        public float Pressed
        {
            get => _pressed;
            set
            {
                if (!Mathf.Approximately(_pressed, value))
                {
                    _pressed = value;
                    SetVerticesDirty();
                }
            }
        }

        /// <summary>How much darker the arrow is, fully held down.</summary>
        private const float PressedDarker = 0.18f;

        public List<Vector2[]> Parts() => Curled ? ArrowShape.Curved() : ArrowShape.Straight();

        /// <summary>The scale and the offset that fit the shape, with room for its outline and band, into this graphic's rect.</summary>
        private void Fit(List<Vector2[]> parts, out float scale, out Vector2 offset)
        {
            Rect rect = rectTransform.rect;
            Rect shape = ArrowShape.Bounds(parts, 0.0f);
            float margin = OutlineWidth + HoverWidth;
            scale = Mathf.Max(1e-4f, Mathf.Min((rect.width - 2.0f * margin) / shape.width, (rect.height - 2.0f * margin) / shape.height));
            offset = rect.center - shape.center * scale;
        }

        /// <summary>A point of the shape's own units, in this graphic's local space (where its labels go).</summary>
        public Vector2 ToLocal(Vector2 unit)
        {
            Fit(Parts(), out float scale, out Vector2 offset);
            return unit * scale + offset;
        }

        /// <summary>How many canvas units one of the shape's units is.</summary>
        public float Scale
        {
            get
            {
                Fit(Parts(), out float scale, out _);
                return scale;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            List<Vector2[]> parts = Parts();
            Fit(parts, out float scale, out Vector2 offset);
            Rect shape = ArrowShape.Bounds(parts, 0.0f);
            float band = HoverWidth * _highlight;
            Color tint = color;

            foreach (Vector2[] part in parts)
            {
                Fan(vh, ArrowShape.Grown(part, (OutlineWidth + band) / scale), scale, offset, p => OutlineColour * tint);
            }
            if (band > 0.0f)
            {
                foreach (Vector2[] part in parts)
                {
                    Fan(vh, ArrowShape.Grown(part, band / scale), scale, offset, p => HoverColour * tint);
                }
            }
            float darker = 1.0f - PressedDarker * _pressed;
            foreach (Vector2[] part in parts)
            {
                Fan(vh, part, scale, offset, p =>
                {
                    Color c = Color.Lerp(Bottom, Top, Mathf.InverseLerp(shape.yMin, shape.yMax, p.y));
                    return new Color(c.r * darker, c.g * darker, c.b * darker, c.a) * tint;
                });
            }
            float middle = shape.center.y;
            foreach (Vector2[] part in parts)
            {
                Vector2[] upper = ArrowShape.Above(part, middle);
                if (upper.Length >= 3)
                {
                    Fan(vh, upper, scale, offset, p => new Color(1.0f, 1.0f, 1.0f, Gloss * Mathf.InverseLerp(middle, shape.yMax, p.y)) * tint);
                }
            }
        }

        /// <summary>A convex polygon of the shape's units as a fan of triangles, each corner coloured by where it is.</summary>
        private static void Fan(VertexHelper vh, Vector2[] polygon, float scale, Vector2 offset, System.Func<Vector2, Color> colourAt)
        {
            int first = vh.currentVertCount;
            foreach (Vector2 p in polygon)
            {
                vh.AddVert(p * scale + offset, colourAt(p), Vector2.zero);
            }
            for (int i = 1; i + 1 < polygon.Length; i++)
            {
                vh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local))
            {
                return false;
            }
            List<Vector2[]> parts = Parts();
            Fit(parts, out float scale, out Vector2 offset);
            return ArrowShape.Contains(parts, (local - offset) / scale, OutlineWidth / scale);
        }
    }
}
