using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The skills' icons, for the bar on the HUD and the stars of the skill map: a few shapes each (a flame for the fireball, shards for the ice,
    /// a bubbling cloud for the poison, rain under a cloud for the acid, a turning arrow for the spin, a crescent for the wave, a burst of eight
    /// cuts for Octaslash, a drop for Bloodrage; a low wall and the arc over it for vaulting, speed lines for the dash, an arrow up a wall for the
    /// climb, double chevrons for the super jump, footprints along a wall for the wall stick).
    ///
    /// DRAWN IN CODE IN WHITE, like the C menu's own (MenuIcons, whose technique this copies: shapes supersampled into a small texture), and
    /// tinted by whatever shows them: the bar's pale gold, a star's state. No image files: the repository is kept small. Made once and shared.
    /// </summary>
    public static class SkillIcons
    {
        public const int Size = 64;
        private const int Samples = 3;

        private static readonly Color Ink = Color.white;
        private static readonly Color Faint = new Color(1.0f, 1.0f, 1.0f, 0.55f);

        private static readonly Dictionary<string, Sprite> Made = new Dictionary<string, Sprite>();

        /// <summary>The icon for a skill, or null for an empty star or a skill with no icon.</summary>
        public static Sprite For(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }
            if (Made.TryGetValue(skillId, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            var shapes = new List<Shape>();
            if (!Draw(skillId, shapes))
            {
                return null;
            }
            Texture2D texture = Rasterise(shapes);
            texture.name = "SkillIcon-" + skillId;
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100.0f, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            Made[skillId] = sprite;
            return sprite;
        }

        /// <summary>
        /// The letter a skill with no drawn icon shows in its place (owner, 2026-10-15: "no need to make hotbar spell icons im going to make them
        /// all myself just set it to the letter the spell starts with"): its name's first letter. Null for a skill with an icon, an empty star,
        /// or nothing at all.
        /// </summary>
        public static string LetterFor(string skillId)
        {
            SkillNode node = SkillCatalog.Find(skillId);
            if (node == null || node.IsPlaceholder || string.IsNullOrEmpty(node.Name) || For(skillId) != null)
            {
                return null;
            }
            return char.ToUpperInvariant(node.Name[0]).ToString();
        }

        private static bool Draw(string id, List<Shape> shapes)
        {
            switch (id)
            {
                case SkillCatalog.Vault: Vault(shapes); return true;
                case SkillCatalog.Dash: Dash(shapes); return true;
                case SkillCatalog.Climb: Climb(shapes); return true;
                case SkillCatalog.SuperJump: SuperJump(shapes); return true;
                case SkillCatalog.WallStick: WallStick(shapes); return true;
                case SkillCatalog.Fireball: Fireball(shapes); return true;
                case SkillCatalog.IceShards: IceShards(shapes); return true;
                case SkillCatalog.PoisonBreath: PoisonBreath(shapes); return true;
                case SkillCatalog.AcidRain: AcidRain(shapes); return true;
                case SkillCatalog.SwordSpin: SwordSpin(shapes); return true;
                case SkillCatalog.SwordWave: SwordWave(shapes); return true;
                case SkillCatalog.Octaslash: Octaslash(shapes); return true;
                case SkillCatalog.Bloodrage: Bloodrage(shapes); return true;
                default: return false;
            }
        }

        // Coordinates are y-down in a 64 x 64 box, as MenuIcons' are.

        /// <summary>A low wall and the arc of the body going over it.</summary>
        private static void Vault(List<Shape> s)
        {
            s.Add(Shape.Polygon(new[] { new Vector2(20, 44), new Vector2(44, 44), new Vector2(44, 58), new Vector2(20, 58) }, Ink));
            s.Add(Shape.Arc(new Vector2(32, 42), 21.0f, Mathf.PI * 1.05f, Mathf.PI * 1.85f, Ink, 5.0f));
            s.Add(Shape.Polygon(new[] { new Vector2(45, 31), new Vector2(57, 33), new Vector2(50, 43) }, Ink));
        }

        /// <summary>Speed lines behind an arrowhead going right.</summary>
        private static void Dash(List<Shape> s)
        {
            s.Add(Shape.Line(new Vector2(6, 22), new Vector2(24, 22), Faint, 4.5f));
            s.Add(Shape.Line(new Vector2(2, 32), new Vector2(26, 32), Ink, 4.5f));
            s.Add(Shape.Line(new Vector2(6, 42), new Vector2(24, 42), Faint, 4.5f));
            s.Add(Shape.Polygon(new[] { new Vector2(30, 12), new Vector2(60, 32), new Vector2(30, 52), new Vector2(38, 32) }, Ink));
        }

        /// <summary>An arrow going up beside a wall.</summary>
        private static void Climb(List<Shape> s)
        {
            s.Add(Shape.Line(new Vector2(48, 4), new Vector2(48, 60), Faint, 7.0f));
            s.Add(Shape.Line(new Vector2(25, 58), new Vector2(25, 22), Ink, 7.0f));
            s.Add(Shape.Polygon(new[] { new Vector2(12, 26), new Vector2(38, 26), new Vector2(25, 7) }, Ink));
            s.Add(Shape.Line(new Vector2(30, 6), new Vector2(46, 6), Ink, 4.0f));
        }

        /// <summary>Two chevrons rising off the ground.</summary>
        private static void SuperJump(List<Shape> s)
        {
            s.Add(Chevron(new Vector2(32, 6), 22.0f, 14.0f, 7.0f, Ink));
            s.Add(Chevron(new Vector2(32, 24), 22.0f, 14.0f, 7.0f, Faint));
            s.Add(Shape.Line(new Vector2(14, 56), new Vector2(50, 56), Ink, 4.0f));
        }

        /// <summary>A wall, and footprints running along it.</summary>
        private static void WallStick(List<Shape> s)
        {
            s.Add(Shape.Line(new Vector2(10, 4), new Vector2(10, 60), Ink, 7.0f));
            s.Add(Shape.Circle(new Vector2(24, 50), 5.0f, Faint));
            s.Add(Shape.Circle(new Vector2(36, 36), 5.5f, Ink));
            s.Add(Shape.Circle(new Vector2(48, 21), 6.0f, Ink));
            s.Add(Shape.Line(new Vector2(18, 58), new Vector2(54, 10), Faint, 2.0f));
        }

        /// <summary>A ball of fire with its flame trailing down behind it.</summary>
        private static void Fireball(List<Shape> s)
        {
            s.Add(Shape.Polygon(new[] { new Vector2(5, 60), new Vector2(26, 26), new Vector2(40, 40) }, Faint));
            s.Add(Shape.Polygon(new[] { new Vector2(14, 58), new Vector2(28, 30), new Vector2(36, 38) }, Ink));
            s.Add(Shape.Circle(new Vector2(38, 26), 16.0f, Ink));
        }

        /// <summary>Three long shards of ice, fanned.</summary>
        private static void IceShards(List<Shape> s)
        {
            s.Add(Shard(new Vector2(16, 36), 22.0f, 6.0f, -0.35f, Faint));
            s.Add(Shard(new Vector2(32, 30), 28.0f, 7.0f, 0.0f, Ink));
            s.Add(Shard(new Vector2(48, 36), 22.0f, 6.0f, 0.35f, Faint));
        }

        /// <summary>A cloud of gas bubbling out.</summary>
        private static void PoisonBreath(List<Shape> s)
        {
            s.Add(Shape.Circle(new Vector2(36, 28), 15.0f, Ink));
            s.Add(Shape.Circle(new Vector2(18, 40), 10.0f, Ink));
            s.Add(Shape.Circle(new Vector2(48, 46), 8.0f, Faint));
            s.Add(Shape.Circle(new Vector2(28, 54), 5.5f, Faint));
            s.Add(Shape.Circle(new Vector2(12, 58), 3.5f, Faint));
        }

        /// <summary>Slanting rain under a cloud.</summary>
        private static void AcidRain(List<Shape> s)
        {
            s.Add(Shape.Circle(new Vector2(22, 20), 9.0f, Ink));
            s.Add(Shape.Circle(new Vector2(35, 14), 12.0f, Ink));
            s.Add(Shape.Circle(new Vector2(47, 21), 9.0f, Ink));
            s.Add(Shape.Polygon(new[] { new Vector2(13, 20), new Vector2(56, 20), new Vector2(56, 29), new Vector2(13, 29) }, Ink));
            for (int i = 0; i < 4; i++)
            {
                float x = 18 + i * 10;
                s.Add(Shape.Line(new Vector2(x, 36), new Vector2(x - 5, 52 + (i % 2) * 6), i % 2 == 0 ? Ink : Faint, 3.5f));
            }
        }

        /// <summary>An arrow turning all the way round.</summary>
        private static void SwordSpin(List<Shape> s)
        {
            s.Add(Shape.Arc(new Vector2(32, 32), 21.0f, Mathf.PI * 0.15f, Mathf.PI * 1.75f, Ink, 6.0f));
            Vector2 tip = new Vector2(32, 32) + new Vector2(Mathf.Cos(Mathf.PI * 0.15f), Mathf.Sin(Mathf.PI * 0.15f)) * 21.0f;
            s.Add(Shape.Polygon(new[] { tip + new Vector2(-9, -6), tip + new Vector2(9, -6), tip + new Vector2(2, 9) }, Ink));
            s.Add(Shape.Line(new Vector2(22, 42), new Vector2(42, 22), Faint, 4.0f));
        }

        /// <summary>A crescent of light with two fainter ones behind it.</summary>
        private static void SwordWave(List<Shape> s)
        {
            s.Add(Shape.Arc(new Vector2(16, 32), 30.0f, -Mathf.PI * 0.38f, Mathf.PI * 0.38f, Ink, 9.0f));
            s.Add(Shape.Arc(new Vector2(8, 32), 26.0f, -Mathf.PI * 0.32f, Mathf.PI * 0.32f, Faint, 4.0f));
            s.Add(Shape.Arc(new Vector2(0, 32), 22.0f, -Mathf.PI * 0.26f, Mathf.PI * 0.26f, Faint, 3.0f));
        }

        /// <summary>Eight cuts crossing in a burst.</summary>
        private static void Octaslash(List<Shape> s)
        {
            var centre = new Vector2(32, 32);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI / 4.0f + 0.2f;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                s.Add(Shape.Line(centre - along * 27.0f, centre + along * 27.0f, i % 2 == 0 ? Ink : Faint, i % 2 == 0 ? 4.5f : 3.0f));
            }
        }

        /// <summary>A drop of blood.</summary>
        private static void Bloodrage(List<Shape> s)
        {
            s.Add(Shape.Circle(new Vector2(32, 40), 16.0f, Ink));
            s.Add(Shape.Polygon(new[] { new Vector2(32, 4), new Vector2(17, 34), new Vector2(47, 34) }, Ink));
        }

        private static Shape Chevron(Vector2 tip, float halfWidth, float height, float thickness, Color tint) => Shape.Polygon(new[]
        {
            tip, tip + new Vector2(halfWidth, height), tip + new Vector2(halfWidth - thickness, height + thickness * 0.8f),
            tip + new Vector2(0, thickness * 1.3f), tip + new Vector2(-halfWidth + thickness, height + thickness * 0.8f), tip + new Vector2(-halfWidth, height),
        }, tint);

        private static Shape Shard(Vector2 centre, float halfLength, float halfWidth, float tilt, Color tint)
        {
            var along = new Vector2(Mathf.Sin(tilt), -Mathf.Cos(tilt));
            var across = new Vector2(-along.y, along.x);
            return Shape.Polygon(new[]
            {
                centre + along * halfLength, centre + across * halfWidth, centre - along * halfLength * 0.8f, centre - across * halfWidth,
            }, tint);
        }

        // --- the rasteriser, as MenuIcons' ---

        private static Texture2D Rasterise(List<Shape> shapes)
        {
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color accumulated = new Color(1.0f, 1.0f, 1.0f, 0.0f);
                    foreach (Shape shape in shapes)
                    {
                        float coverage = Coverage(shape, x, y);
                        if (coverage <= 0.0f)
                        {
                            continue;
                        }
                        float alpha = coverage * shape.Tint.a;
                        accumulated.a = accumulated.a + alpha * (1.0f - accumulated.a);
                    }
                    pixels[(Size - 1 - y) * Size + x] = accumulated;
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
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

            private Shape(Kind kind, Vector2 a, float radius, float width, float from, float to, Vector2[] points, Color tint)
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

            /// <summary>An arc from one angle to another (radians, y down, so angles between pi and 2 pi are the top half).</summary>
            public static Shape Arc(Vector2 centre, float radius, float from, float to, Color tint, float width) =>
                new Shape(Kind.Arc, centre, radius, width, from, to, null, tint);

            public bool Contains(Vector2 point)
            {
                switch (_kind)
                {
                    case Kind.Line:
                        return IconGeometry.DistanceToSegment(point, _points[0], _points[1]) <= _width * 0.5f;
                    case Kind.Circle:
                        return Vector2.Distance(point, _a) <= _radius;
                    case Kind.Arc:
                        return InArc(point);
                    default:
                        return IconGeometry.InPolygon(_points, point);
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
                float relative = Mathf.Repeat(angle - _from, Mathf.PI * 2.0f);
                return relative <= Mathf.Repeat(_to - _from, Mathf.PI * 2.0f);
            }
        }
    }
}
