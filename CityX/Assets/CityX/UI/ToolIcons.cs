using System.Collections.Generic;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Draws the creator's three tool icons -- brush, bucket, eyedropper --
    /// into small textures. Ported from character_creator/tool_icon.gd.
    ///
    /// PROCEDURAL RATHER THAN IMPORTED ART, which is how this project draws
    /// everything else: the weapons, the buildings and the character itself are
    /// all built in code, and a pair of PNGs for two buttons would be the only
    /// hand-drawn assets in the repository.
    ///
    /// Godot got these by rendering a Control's _draw() into a throwaway
    /// SubViewport and capturing the result. Unity has no equivalent that works
    /// outside play mode, so the same handful of shapes are rasterised straight
    /// into a Texture2D here. The coordinates below are tool_icon.gd's own,
    /// unchanged, including its y-down screen convention -- <see cref="Plot"/>
    /// is the only place the flip to Unity's y-up texture happens.
    /// </summary>
    public static class ToolIcons
    {
        /// <summary>tool_icon.gd's own TOOL_ICON_SIZE.</summary>
        public const int Size = 64;

        /// <summary>Samples per pixel per axis. Three is enough to hide the stair-steps.</summary>
        private const int Samples = 3;

        private static readonly Color Stroke = new Color(0.9f, 0.92f, 1.0f, 1.0f);
        private static readonly Color Accent = new Color(0.55f, 0.75f, 1.0f, 1.0f);

        public static Texture2D Build(string kind)
        {
            var shapes = new List<Shape>();
            switch (kind)
            {
                case "bucket": Bucket(shapes); break;
                case "eyedropper": Eyedropper(shapes); break;
                default: Brush(shapes); break;
            }
            return Rasterise(shapes);
        }

        /// <summary>
        /// A diagonal handle, a metal ferrule band near the tip, a fanned-out
        /// bristle tip and a small paint dab under it.
        /// </summary>
        private static void Brush(List<Shape> shapes)
        {
            shapes.Add(Shape.Line(new Vector2(10, 54), new Vector2(32, 32), Stroke, 6.0f));
            shapes.Add(Shape.Line(new Vector2(28, 36), new Vector2(38, 26), Accent, 7.0f));
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(34, 30), new Vector2(52, 12),
                new Vector2(45, 7), new Vector2(28, 24),
            }, Stroke));
            shapes.Add(Shape.Circle(new Vector2(50, 16), 3.0f, Accent));
        }

        /// <summary>A trapezoid pail with a rim, a top handle arc and a drip below.</summary>
        private static void Bucket(List<Shape> shapes)
        {
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(16, 26), new Vector2(48, 26),
                new Vector2(42, 54), new Vector2(22, 54),
            }, Stroke));
            shapes.Add(Shape.Line(new Vector2(14, 26), new Vector2(50, 26), Accent, 4.0f));
            shapes.Add(Shape.Arc(new Vector2(32, 22), 13.0f,
                Mathf.PI + 0.3f, Mathf.PI * 2.0f - 0.3f, Accent, 3.0f));
            shapes.Add(Shape.Circle(new Vector2(32, 59), 2.5f, Accent));
        }

        /// <summary>A round bulb, a diagonal glass stem, a tapered tip and a falling drop.</summary>
        private static void Eyedropper(List<Shape> shapes)
        {
            shapes.Add(Shape.Circle(new Vector2(18, 14), 9.0f, Accent));
            shapes.Add(Shape.Line(new Vector2(23, 20), new Vector2(44, 41), Stroke, 6.0f));
            shapes.Add(Shape.Line(new Vector2(44, 41), new Vector2(51, 48), Stroke, 3.0f));
            shapes.Add(Shape.Circle(new Vector2(53, 55), 3.0f, Accent));
        }

        // --- the rasteriser ---

        /// <summary>
        /// Paints the shapes over each other in order, the way a canvas does,
        /// rather than picking a winner per pixel -- the brush's ferrule is
        /// drawn ON TOP of its handle and the two overlap deliberately.
        /// </summary>
        private static Texture2D Rasterise(List<Shape> shapes)
        {
            var pixels = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color accumulated = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                    foreach (Shape shape in shapes)
                    {
                        float coverage = Coverage(shape, x, y);
                        if (coverage <= 0.0f)
                        {
                            continue;
                        }
                        Color over = shape.Tint;
                        float alpha = coverage;
                        accumulated.r = Mathf.Lerp(accumulated.r, over.r, alpha);
                        accumulated.g = Mathf.Lerp(accumulated.g, over.g, alpha);
                        accumulated.b = Mathf.Lerp(accumulated.b, over.b, alpha);
                        accumulated.a = accumulated.a + alpha * (1.0f - accumulated.a);
                    }
                    Plot(pixels, x, y, accumulated);
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "CityXToolIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// The one place the coordinate systems meet: the shapes are all in
        /// Godot's y-down screen space, and a Texture2D's first row is its
        /// bottom one.
        /// </summary>
        private static void Plot(Color[] pixels, int x, int y, Color colour)
        {
            pixels[(Size - 1 - y) * Size + x] = colour;
        }

        private static float Coverage(Shape shape, int x, int y)
        {
            int inside = 0;
            for (int sy = 0; sy < Samples; sy++)
            {
                for (int sx = 0; sx < Samples; sx++)
                {
                    var point = new Vector2(
                        x + (sx + 0.5f) / Samples,
                        y + (sy + 0.5f) / Samples);
                    if (shape.Contains(point))
                    {
                        inside++;
                    }
                }
            }
            return inside / (float)(Samples * Samples);
        }

        private enum Kind { Line, Polygon, Circle, Arc }

        private readonly struct Shape
        {
            private readonly Kind _kind;
            private readonly Vector2 _a;
            private readonly Vector2 _b;
            private readonly float _radius;
            private readonly float _width;
            private readonly float _from;
            private readonly float _to;
            private readonly Vector2[] _points;

            public readonly Color Tint;

            private Shape(Kind kind, Vector2 a, Vector2 b, float radius, float width,
                float from, float to, Vector2[] points, Color tint)
            {
                _kind = kind;
                _a = a;
                _b = b;
                _radius = radius;
                _width = width;
                _from = from;
                _to = to;
                _points = points;
                Tint = tint;
            }

            public static Shape Line(Vector2 a, Vector2 b, Color tint, float width) =>
                new Shape(Kind.Line, a, b, 0.0f, width, 0.0f, 0.0f, null, tint);

            public static Shape Polygon(Vector2[] points, Color tint) =>
                new Shape(Kind.Polygon, Vector2.zero, Vector2.zero, 0.0f, 0.0f, 0.0f, 0.0f,
                    points, tint);

            public static Shape Circle(Vector2 centre, float radius, Color tint) =>
                new Shape(Kind.Circle, centre, Vector2.zero, radius, 0.0f, 0.0f, 0.0f, null, tint);

            public static Shape Arc(Vector2 centre, float radius, float from, float to,
                Color tint, float width) =>
                new Shape(Kind.Arc, centre, Vector2.zero, radius, width, from, to, null, tint);

            public bool Contains(Vector2 point)
            {
                switch (_kind)
                {
                    case Kind.Line:
                        return DistanceToSegment(point, _a, _b) <= _width * 0.5f;
                    case Kind.Circle:
                        return Vector2.Distance(point, _a) <= _radius;
                    case Kind.Arc:
                        return InArc(point);
                    default:
                        return InPolygon(point);
                }
            }

            private bool InArc(Vector2 point)
            {
                Vector2 offset = point - _a;
                if (Mathf.Abs(offset.magnitude - _radius) > _width * 0.5f)
                {
                    return false;
                }
                // Wrapped into [from, from + sweep], so an arc that crosses zero
                // is one range rather than two.
                float angle = Mathf.Atan2(offset.y, offset.x);
                float sweep = _to - _from;
                float relative = Mathf.Repeat(angle - _from, Mathf.PI * 2.0f);
                return relative <= sweep;
            }

            private bool InPolygon(Vector2 point)
            {
                bool inside = false;
                for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
                {
                    Vector2 p = _points[i];
                    Vector2 q = _points[j];
                    if (p.y > point.y != q.y > point.y
                        && point.x < (q.x - p.x) * (point.y - p.y) / (q.y - p.y) + p.x)
                    {
                        inside = !inside;
                    }
                }
                return inside;
            }

            private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
            {
                Vector2 along = b - a;
                float lengthSquared = along.sqrMagnitude;
                if (lengthSquared < 1e-6f)
                {
                    return Vector2.Distance(point, a);
                }
                float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / lengthSquared);
                return Vector2.Distance(point, a + along * t);
            }
        }
    }
}
