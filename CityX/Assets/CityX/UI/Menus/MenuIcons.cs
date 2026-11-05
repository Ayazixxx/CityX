using System.Collections.Generic;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Draws the C-menu's icons: the exclamation mark, the person, two people, the map pin, and the Player fly-out's own vest, chart, star
    /// and bag. Each is a few shapes supersampled into a texture, the cut-outs (a pin's hole, the gap round a head) drawn as shapes that
    /// erase. Drawn when the menu wakes (MenuIconImage), once each, rather than baked into the scene: textures made by a builder are saved
    /// inside the scene file. ReDraw's brush is ToolIcons' own.
    /// </summary>
    public static class MenuIcons
    {
        public const int Size = 128;
        private const int Samples = 4;

        private static readonly Color Stroke = new Color(0.9f, 0.92f, 1.0f, 1.0f);
        private static readonly Color Accent = new Color(0.55f, 0.75f, 1.0f, 1.0f);

        private static readonly Dictionary<string, Sprite> Made = new Dictionary<string, Sprite>();

        /// <summary>The icon of <paramref name="kind"/> as a sprite, drawn the first time it is asked for.</summary>
        public static Sprite Sprite(string kind)
        {
            if (Made.TryGetValue(kind, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            Texture2D texture = kind == "brush" ? ToolIcons.Build("brush") : Build(kind);
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            sprite.name = "CityXMenuIcon " + kind;
            Made[kind] = sprite;
            return sprite;
        }

        public static Texture2D Build(string kind)
        {
            var shapes = new List<Shape>();
            switch (kind)
            {
                case "person": Person(shapes, 64.0f, 0.0f, Stroke); break;
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

        /// <summary>A stem tapering to its foot with rounded ends, and a round dot under it: the quest marker.</summary>
        private static void Exclamation(List<Shape> shapes)
        {
            shapes.Add(Shape.Circle(new Vector2(64.0f, 26.0f), 13.0f, Stroke));
            shapes.Add(Shape.Polygon(new[] { new Vector2(51.0f, 26.0f), new Vector2(77.0f, 26.0f), new Vector2(71.0f, 78.0f), new Vector2(57.0f, 78.0f) }, Stroke));
            shapes.Add(Shape.Circle(new Vector2(64.0f, 78.0f), 7.0f, Stroke));
            shapes.Add(Shape.Circle(new Vector2(64.0f, 104.0f), 11.0f, Stroke));
        }

        /// <summary>A head over rounded shoulders, a gap between them: the Player button. Drawn about (<paramref name="centreX"/>, <paramref name="lift"/> up).</summary>
        private static void Person(List<Shape> shapes, float centreX, float lift, Color tint)
        {
            var head = new Vector2(centreX, 40.0f - lift);
            shapes.Add(Shape.Ellipse(new Vector2(centreX, 118.0f - lift), 40.0f, 46.0f, tint, 0.0f, 118.0f - lift));
            shapes.Add(Shape.Circle(head, 25.0f, Color.clear));   // the gap round the head
            shapes.Add(Shape.Circle(head, 20.0f, tint));
        }

        /// <summary>Two people, the second set back behind the first with a gap round it: the Social button.</summary>
        private static void People(List<Shape> shapes)
        {
            Person(shapes, 82.0f, 10.0f, Accent);
            // The front one's outline, a little larger, erased out of the one behind, then the front one itself.
            shapes.Add(Shape.Ellipse(new Vector2(48.0f, 118.0f), 45.0f, 51.0f, Color.clear, 0.0f, 118.0f));
            shapes.Add(Shape.Circle(new Vector2(48.0f, 40.0f), 25.0f, Color.clear));
            Person(shapes, 48.0f, 0.0f, Stroke);
        }

        /// <summary>A teardrop with a round hole, its point resting on a small shadow: the Map button.</summary>
        private static void Pin(List<Shape> shapes)
        {
            var centre = new Vector2(64.0f, 48.0f);
            const float radius = 32.0f;
            var tip = new Vector2(64.0f, 112.0f);
            // The sides run from the tip to where they touch the circle.
            float away = Vector2.Distance(centre, tip);
            float touch = Mathf.Asin(radius / away);
            float side = Mathf.Sqrt(away * away - radius * radius);
            Vector2 up = (centre - tip) / away;
            Vector2 left = Rotate(up, touch) * side + tip, right = Rotate(up, -touch) * side + tip;
            shapes.Add(Shape.Ellipse(new Vector2(64.0f, 116.0f), 24.0f, 6.0f, Accent, 0.0f, 128.0f));
            shapes.Add(Shape.Polygon(new[] { left, right, tip }, Stroke));
            shapes.Add(Shape.Circle(centre, radius, Stroke));
            shapes.Add(Shape.Circle(centre, 12.0f, Color.clear));
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>A vest, a V at the neck and the armholes cut in, zipped down the middle with two pockets: the Loadout button.</summary>
        private static void Vest(List<Shape> shapes)
        {
            shapes.Add(Shape.Polygon(new[]
            {
                new Vector2(40.0f, 14.0f), new Vector2(54.0f, 14.0f), new Vector2(64.0f, 38.0f), new Vector2(74.0f, 14.0f), new Vector2(88.0f, 14.0f),
                new Vector2(96.0f, 30.0f), new Vector2(104.0f, 50.0f), new Vector2(104.0f, 112.0f), new Vector2(24.0f, 112.0f), new Vector2(24.0f, 50.0f),
                new Vector2(32.0f, 30.0f),
            }, Stroke));
            shapes.Add(Shape.Circle(new Vector2(110.0f, 38.0f), 12.0f, Color.clear));   // the armholes
            shapes.Add(Shape.Circle(new Vector2(18.0f, 38.0f), 12.0f, Color.clear));
            shapes.Add(Shape.Line(new Vector2(64.0f, 40.0f), new Vector2(64.0f, 112.0f), Color.clear, 4.0f));   // the zip
            shapes.Add(Shape.Line(new Vector2(36.0f, 82.0f), new Vector2(52.0f, 82.0f), Accent, 5.0f));          // the pockets
            shapes.Add(Shape.Line(new Vector2(76.0f, 82.0f), new Vector2(92.0f, 82.0f), Accent, 5.0f));
        }

        /// <summary>Three rounded bars rising left to right on a base line: the Stats button.</summary>
        private static void Chart(List<Shape> shapes)
        {
            shapes.Add(Shape.Line(new Vector2(18.0f, 112.0f), new Vector2(110.0f, 112.0f), Stroke, 6.0f));
            shapes.Add(Shape.Line(new Vector2(36.0f, 98.0f), new Vector2(36.0f, 74.0f), Stroke, 18.0f));
            shapes.Add(Shape.Line(new Vector2(64.0f, 98.0f), new Vector2(64.0f, 50.0f), Accent, 18.0f));
            shapes.Add(Shape.Line(new Vector2(92.0f, 98.0f), new Vector2(92.0f, 24.0f), Stroke, 18.0f));
        }

        /// <summary>A five-point star with softened points: the Skills button.</summary>
        private static void Star(List<Shape> shapes)
        {
            const int points = 5;
            var vertices = new Vector2[points * 2];
            var centre = new Vector2(64.0f, 68.0f);
            const float outer = 56.0f;
            const float inner = 24.0f;
            for (int i = 0; i < points * 2; i++)
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI / points;
                float radius = i % 2 == 0 ? outer : inner;
                vertices[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            shapes.Add(Shape.Polygon(vertices, Stroke));
            // Each point rounded: its tip trimmed and a small circle laid inside it.
            for (int i = 0; i < points * 2; i += 2)
            {
                Vector2 toTip = (vertices[i] - centre).normalized;
                shapes.Add(Shape.Line(vertices[i] + toTip * 6.0f, vertices[i] + toTip * 6.0f, Color.clear, 14.0f));
                shapes.Add(Shape.Circle(vertices[i] - toTip * 7.0f, 6.0f, Stroke));
            }
        }

        /// <summary>A shopping bag, a little narrower at the top, its handle an arc: the Items button.</summary>
        private static void Bag(List<Shape> shapes)
        {
            shapes.Add(Shape.Arc(new Vector2(64.0f, 46.0f), 20.0f, Mathf.PI, Mathf.PI * 2.0f, Stroke, 8.0f));
            shapes.Add(Shape.Polygon(new[] { new Vector2(30.0f, 44.0f), new Vector2(98.0f, 44.0f), new Vector2(106.0f, 114.0f), new Vector2(22.0f, 114.0f) }, Stroke));
            shapes.Add(Shape.Circle(new Vector2(46.0f, 60.0f), 5.0f, Color.clear));   // where the handle goes in
            shapes.Add(Shape.Circle(new Vector2(82.0f, 60.0f), 5.0f, Color.clear));
            shapes.Add(Shape.Line(new Vector2(32.0f, 94.0f), new Vector2(96.0f, 94.0f), Accent, 6.0f));
        }

        // --- the rasteriser ---

        private static Texture2D Rasterise(List<Shape> shapes)
        {
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var accumulated = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                    foreach (Shape shape in shapes)
                    {
                        if (!shape.Near(x, y))
                        {
                            continue;
                        }
                        float coverage = Coverage(shape, x, y);
                        if (coverage <= 0.0f)
                        {
                            continue;
                        }
                        if (shape.Erases)
                        {
                            accumulated.a *= 1.0f - coverage;
                            continue;
                        }
                        Color over = shape.Tint;
                        accumulated.r = Mathf.Lerp(accumulated.r, over.r, coverage);
                        accumulated.g = Mathf.Lerp(accumulated.g, over.g, coverage);
                        accumulated.b = Mathf.Lerp(accumulated.b, over.b, coverage);
                        accumulated.a += coverage * (1.0f - accumulated.a);
                    }
                    pixels[(Size - 1 - y) * Size + x] = accumulated;   // shapes are drawn y-down; a texture's first row is its bottom
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "CityXMenuIcon",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        private static float Coverage(Shape shape, int x, int y)
        {
            int inside = 0;
            for (int sy = 0; sy < Samples; sy++)
            {
                for (int sx = 0; sx < Samples; sx++)
                {
                    if (shape.Contains(new Vector2(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples)))
                    {
                        inside++;
                    }
                }
            }
            return inside / (float)(Samples * Samples);
        }

        private enum Kind { Line, Polygon, Circle, Ellipse, Arc }

        private readonly struct Shape
        {
            private readonly Kind _kind;
            private readonly Vector2 _a;
            private readonly Vector2 _b;
            private readonly float _width;
            private readonly float _from;
            private readonly float _to;
            private readonly Vector2[] _points;
            private readonly Rect _bounds;

            public readonly Color Tint;

            /// <summary>Drawn in clear: it takes out what was drawn under it (a pin's hole, the gap round a head).</summary>
            public bool Erases => Tint.a <= 0.0f;

            private Shape(Kind kind, Vector2 a, Vector2 b, float width, float from, float to, Vector2[] points, Color tint, Rect bounds)
            {
                _kind = kind;
                _a = a;
                _b = b;
                _width = width;
                _from = from;
                _to = to;
                _points = points;
                Tint = tint;
                _bounds = bounds;
            }

            public static Shape Line(Vector2 a, Vector2 b, Color tint, float width) =>
                new Shape(Kind.Line, a, b, width, 0.0f, 0.0f, null, tint, Around(Vector2.Min(a, b), Vector2.Max(a, b), width * 0.5f));

            public static Shape Polygon(Vector2[] points, Color tint)
            {
                Vector2 min = points[0], max = points[0];
                foreach (Vector2 p in points)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                return new Shape(Kind.Polygon, Vector2.zero, Vector2.zero, 0.0f, 0.0f, 0.0f, points, tint, Around(min, max, 0.0f));
            }

            public static Shape Circle(Vector2 centre, float radius, Color tint) =>
                new Shape(Kind.Circle, centre, new Vector2(radius, radius), 0.0f, 0.0f, 0.0f, null, tint, Around(centre, centre, radius));

            /// <summary>An ellipse of half-sizes <paramref name="halfWidth"/>, <paramref name="halfHeight"/>, kept between <paramref name="top"/> and <paramref name="bottom"/> (y-down).</summary>
            public static Shape Ellipse(Vector2 centre, float halfWidth, float halfHeight, Color tint, float top, float bottom) =>
                new Shape(Kind.Ellipse, centre, new Vector2(halfWidth, halfHeight), 0.0f, top, bottom, null, tint,
                    Rect.MinMaxRect(centre.x - halfWidth, Mathf.Max(top, centre.y - halfHeight), centre.x + halfWidth, Mathf.Min(bottom, centre.y + halfHeight)));

            public static Shape Arc(Vector2 centre, float radius, float from, float to, Color tint, float width) =>
                new Shape(Kind.Arc, centre, new Vector2(radius, radius), width, from, to, null, tint, Around(centre, centre, radius + width * 0.5f));

            private static Rect Around(Vector2 min, Vector2 max, float margin) =>
                Rect.MinMaxRect(min.x - margin, min.y - margin, max.x + margin, max.y + margin);

            /// <summary>Whether any of the pixel at (<paramref name="x"/>, <paramref name="y"/>) can lie in the shape.</summary>
            public bool Near(int x, int y) => x + 1 >= _bounds.xMin && x <= _bounds.xMax && y + 1 >= _bounds.yMin && y <= _bounds.yMax;

            public bool Contains(Vector2 point)
            {
                switch (_kind)
                {
                    case Kind.Line:
                        return DistanceToSegment(point, _a, _b) <= _width * 0.5f;
                    case Kind.Circle:
                        return Vector2.Distance(point, _a) <= _b.x;
                    case Kind.Ellipse:
                    {
                        if (point.y < _from || point.y > _to)
                        {
                            return false;
                        }
                        float dx = (point.x - _a.x) / _b.x, dy = (point.y - _a.y) / _b.y;
                        return dx * dx + dy * dy <= 1.0f;
                    }
                    case Kind.Arc:
                    {
                        Vector2 offset = point - _a;
                        if (Mathf.Abs(offset.magnitude - _b.x) > _width * 0.5f)
                        {
                            return false;
                        }
                        float relative = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) - _from, Mathf.PI * 2.0f);
                        return relative <= _to - _from;
                    }
                    default:
                        return InPolygon(point);
                }
            }

            private bool InPolygon(Vector2 point)
            {
                bool inside = false;
                for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
                {
                    Vector2 p = _points[i];
                    Vector2 q = _points[j];
                    if (p.y > point.y != q.y > point.y && point.x < (q.x - p.x) * (point.y - p.y) / (q.y - p.y) + p.x)
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
