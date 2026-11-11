using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Draws <see cref="Embers"/> in the canvas: a soft dot each (the Bloodrage aura's dot) in the canvas's own material (the aura's particle one
    /// draws nothing in a canvas), rising off the points of a drawing given as shares of this graphic's rect (0,0 bottom left, 1,1 top right).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EmberGraphic : MaskableGraphic
    {
        [Tooltip("Points of the drawing the embers come off, as shares of this rect.")]
        public Vector2[] Sources = System.Array.Empty<Vector2>();

        [Tooltip("Which embers: the same seed rises the same way.")]
        public int Seed = 1;

        private Embers _embers;
        private readonly List<Vector2> _points = new List<Vector2>();
        private float _strength;

        /// <summary>How many embers are rising now.</summary>
        public int Count => _embers != null ? _embers.Alive.Count : 0;

        [Tooltip("The soft round dot each ember is (the Bloodrage aura's).")]
        public Texture Dot;

        public override Texture mainTexture => Dot != null ? Dot : s_WhiteTexture;

        /// <summary>How strongly they come off now, 0 (none: the risen ones finish) to 1.</summary>
        public float Strength
        {
            set => _strength = value;
        }

        /// <summary><paramref name="seconds"/> on; drawn again only while there are embers.</summary>
        public void Step(float seconds)
        {
            _embers ??= new Embers(Seed);
            int before = _embers.Alive.Count;
            Rect rect = rectTransform.rect;
            if (_strength > 0.0f && _points.Count != Sources.Length)
            {
                _points.Clear();
            }
            if (_strength > 0.0f && _points.Count == 0)
            {
                foreach (Vector2 share in Sources)
                {
                    _points.Add(new Vector2(rect.xMin + share.x * rect.width, rect.yMin + share.y * rect.height));
                }
            }
            _embers.Step(seconds, _strength, _points, rect.height);
            if (before > 0 || _embers.Alive.Count > 0)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            _points.Clear();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_embers == null)
            {
                return;
            }
            foreach (Embers.Ember ember in _embers.Alive)
            {
                float life = ember.Age / ember.Life;
                Vector2 at = Embers.DrawnAt(ember);
                float half = ember.Size * Embers.SizeAt(life) * 0.5f;
                Color32 colour = Embers.ColourAt(life) * color;
                int first = vh.currentVertCount;
                vh.AddVert(new Vector3(at.x - half, at.y - half), colour, new Vector4(0.0f, 0.0f));
                vh.AddVert(new Vector3(at.x - half, at.y + half), colour, new Vector4(0.0f, 1.0f));
                vh.AddVert(new Vector3(at.x + half, at.y + half), colour, new Vector4(1.0f, 1.0f));
                vh.AddVert(new Vector3(at.x + half, at.y - half), colour, new Vector4(1.0f, 0.0f));
                vh.AddTriangle(first, first + 1, first + 2);
                vh.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
