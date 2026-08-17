using System.Collections.Generic;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Draws the C-menu's icons -- exclamation mark, person, two people, map
    /// pin, and the Player submenu's own five -- into small textures.
    ///
    /// SAME TECHNIQUE AS ToolIcons, DELIBERATELY NOT SHARED WITH IT. Each
    /// icon is a handful of Shape primitives supersampled into a Texture2D;
    /// ToolIcons' own Shape/Rasterise pair is private to that class (it is
    /// scoped to "the creator's three tool icons" by its own doc comment),
    /// so this is a self-contained sibling rather than a cross-class reach.
    /// The ReDraw button's own icon is the one exception -- it reuses
    /// ToolIcons.Build("brush") directly, since ReDraw reopens the exact
    /// paint tools that icon already represents.
    /// </summary>
    public static class MenuIcons
    {
        public const int Size = 64;
        private const int Samples = 3;

        private static readonly Color Stroke = new Color(0.9f, 0.92f, 1.0f, 1.0f);
        private static readonly Color Accent = new Color(0.55f, 0.75f, 1.0f, 1.0f);

        public static Texture2D Build(string kind)
        {
            var shapes = new List<Shape>();
            switch (kind)
            {
                case "person": Person(shapes, 32.0f, 1.0f); break;
                case "people": People(shapes); break;
                case "pin": Pin(shapes); break;
                case "vest": Vest(shapes); break;
                case "chart": Chart(shapes); break;
                case "star": Star(shapes); break;
                case "bag": Bag(shapes); break;
                default: Exclamation(shapes); break;
            }
            return Rasterise(shapes);
        }

        /// <summary>A thick rounded stem and a dot beneath it -- the quest marker.</summary>
        private static void Exclamation(List<Shape> shapes)
        {
            shapes.Add(Shape.Line(new Vector2(32, 8), new Vector2(32, 38), Stroke, 11.0f));
            shapes.Add(Shape.Circle(new Vector2(32, 53), 6.0f, Stroke));
        }

        /// <summary>A round head over a widening body -- the Player button.</summary>
        private static void Person(List<Shape> shapes, float centreX, float scale)
        {
            shapes.Add(Shape.Circle(new Vector2(centreX, 17.0f * scale), 10.0f * scale, Stroke));
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(centreX - 15.0f * scale, 58.0f * scale),
                new Vector2(centreX + 15.0f * scale, 58.0f * scale),
                new Vector2(centreX + 10.0f * scale, 32.0f * scale),
                new Vector2(centreX - 10.0f * scale, 32.0f * scale),
            }, Stroke));
        }

        /// <summary>Two overlapping people, one set slightly back -- the Social button.</summary>
        private static void People(List<Shape> shapes)
        {
            shapes.Add(Shape.Circle(new Vector2(41.0f, 22.0f), 8.5f, Accent));
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(29.0f, 58.0f), new Vector2(53.0f, 58.0f),
                new Vector2(49.0f, 37.0f), new Vector2(33.0f, 37.0f),
            }, Accent));
            shapes.Add(Shape.Circle(new Vector2(21.0f, 18.0f), 9.5f, Stroke));
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(6.0f, 58.0f), new Vector2(36.0f, 58.0f),
                new Vector2(31.0f, 34.0f), new Vector2(11.0f, 34.0f),
            }, Stroke));
        }

        /// <summary>A round-topped teardrop with a hollow centre -- the Map button.</summary>
        private static void Pin(List<Shape> shapes)
        {
            shapes.Add(Shape.Circle(new Vector2(32.0f, 24.0f), 15.0f, Stroke));
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(19.0f, 30.0f), new Vector2(45.0f, 30.0f), new Vector2(32.0f, 59.0f),
            }, Stroke));
            shapes.Add(Shape.Circle(new Vector2(32.0f, 24.0f), 5.5f, Accent));
        }

        /// <summary>A simple shield/vest silhouette -- the Loadout button.</summary>
        private static void Vest(List<Shape> shapes)
        {
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(32.0f, 8.0f), new Vector2(51.0f, 19.0f), new Vector2(46.0f, 57.0f),
                new Vector2(18.0f, 57.0f), new Vector2(13.0f, 19.0f),
            }, Stroke));
            shapes.Add(Shape.Line(new Vector2(32.0f, 8.0f), new Vector2(32.0f, 30.0f), Accent, 4.0f));
        }

        /// <summary>Three ascending bars -- the Stats button.</summary>
        private static void Chart(List<Shape> shapes)
        {
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(11.0f, 58.0f), new Vector2(23.0f, 58.0f),
                new Vector2(23.0f, 40.0f), new Vector2(11.0f, 40.0f),
            }, Stroke));
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(27.0f, 58.0f), new Vector2(39.0f, 58.0f),
                new Vector2(39.0f, 27.0f), new Vector2(27.0f, 27.0f),
            }, Accent));
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(43.0f, 58.0f), new Vector2(55.0f, 58.0f),
                new Vector2(55.0f, 12.0f), new Vector2(43.0f, 12.0f),
            }, Stroke));
        }

        /// <summary>A five-point star -- the Skills button.</summary>
        private static void Star(List<Shape> shapes)
        {
            const int points = 5;
            var vertices = new Vector2[points * 2];
            var centre = new Vector2(32.0f, 32.0f);
            const float outer = 26.0f;
            const float inner = 10.5f;
            for (int i = 0; i < points * 2; i++)
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI / points;
                float radius = (i % 2 == 0) ? outer : inner;
                vertices[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            shapes.Add(Shape.Polygon(vertices, Stroke));
        }

        /// <summary>A tote body with two carry straps -- the Items button.</summary>
        private static void Bag(List<Shape> shapes)
        {
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(14.0f, 26.0f), new Vector2(50.0f, 26.0f),
                new Vector2(46.0f, 58.0f), new Vector2(18.0f, 58.0f),
            }, Stroke));
            shapes.Add(Shape.Line(new Vector2(23.0f, 26.0f), new Vector2(23.0f, 12.0f), Accent, 4.0f));
            shapes.Add(Shape.Line(new Vector2(41.0f, 26.0f), new Vector2(41.0f, 12.0f), Accent, 4.0f));
            shapes.Add(Shape.Arc(new Vector2(32.0f, 12.0f), 9.0f, Mathf.PI, Mathf.PI * 2.0f, Accent, 3.5f));
        }

        // --- the rasteriser, structurally identical to ToolIcons' own ---

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
                name = "CityXMenuIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Coordinates here are y-down (matches how every shape above was authored); a Texture2D's first row is its bottom one.</summary>
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
            private readonly float _radius;
            private readonly float _width;
            private readonly float _from;
            private readonly float _to;
            private readonly Vector2[] _points;

            public readonly Color Tint;

            private Shape(Kind kind, Vector2 a, float radius, float width,
                float from, float to, Vector2[] points, Color tint)
            {
                _kind = kind;
                _a = a;
                _radius = radius;
                _width = width;
                _from = from;
                _to = to;
                _points = points;
                Tint = tint;
            }

            public static Shape Line(Vector2 a, Vector2 b, Color tint, float width) =>
                new Shape(Kind.Line, a, 0.0f, width, 0.0f, 0.0f, new[] { a, b }, tint);

            public static Shape Polygon(Vector2[] points, Color tint) =>
                new Shape(Kind.Polygon, Vector2.zero, 0.0f, 0.0f, 0.0f, 0.0f, points, tint);

            public static Shape Circle(Vector2 centre, float radius, Color tint) =>
                new Shape(Kind.Circle, centre, radius, 0.0f, 0.0f, 0.0f, null, tint);

            public static Shape Arc(Vector2 centre, float radius, float from, float to,
                Color tint, float width) =>
                new Shape(Kind.Arc, centre, radius, width, from, to, null, tint);

            public bool Contains(Vector2 point)
            {
                switch (_kind)
                {
                    case Kind.Line:
                        return DistanceToSegment(point, _points[0], _points[1]) <= _width * 0.5f;
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
