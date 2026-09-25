using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The title screen's rain: a screen-space overlay of drifting streaks,
    /// with an occasional flat splash ring where one reaches the bottom. A
    /// direct port of title_screen/rain_layer.gd -- the same 260 drops and
    /// the same numbers -- rather than a 3D particle system, because the
    /// original was never in the 3D scene at all: it is a picture laid over
    /// the skyline, and that is what gives the title its thin, even, slightly
    /// flat rain instead of rain that falls past buildings in perspective.
    ///
    /// Units are canvas units (the reference is 1152x648, as Godot's was), and
    /// motion is per FRAME at 60fps in the original, so it is scaled by delta
    /// here to look the same at any frame rate.
    ///
    /// A UI Graphic, so it sits in the canvas draw order like every other
    /// layer and needs no material or texture of its own.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleRainLayer : MaskableGraphic
    {
        public const int DropCount = 120;

        /// <summary>rain_layer.gd's DROP_COLOR and SPLASH_COLOR.</summary>
        public static readonly Color DropColour = new Color(200.0f / 255.0f, 210.0f / 255.0f, 1.0f);
        public static readonly Color SplashColour = new Color(210.0f / 255.0f, 220.0f / 255.0f, 1.0f);

        /// <summary>A splash is a ring flattened to a third of its width, lying on the ground.</summary>
        public const float SplashSquash = 0.35f;

        /// <summary>The bottom band a drop can splash in, canvas units.</summary>
        public const float SplashBand = 40.0f;

        public struct Drop
        {
            public float X, Y, Length, Speed, Drift, Opacity, Width;
        }

        public struct Splash
        {
            public float X, Y, Radius, MaxRadius, Opacity;
        }

        private readonly List<Drop> _drops = new List<Drop>(DropCount);
        private readonly List<Splash> _splashes = new List<Splash>();
        private System.Random _random = new System.Random();
        private Vector2 _size;

        public IReadOnlyList<Drop> Drops => _drops;

        public IReadOnlyList<Splash> Splashes => _splashes;

        /// <summary>Fixes the random sequence, so a test can say exactly where the rain is.</summary>
        public void Reseed(int seed)
        {
            _random = new System.Random(seed);
            _drops.Clear();
            _splashes.Clear();
        }

        private float Next() => (float)_random.NextDouble();

        private Drop MakeDrop(float width, float height)
        {
            return new Drop
            {
                X = Next() * Mathf.Max(width, 1.0f),
                Y = Next() * Mathf.Max(height, 1.0f),
                Length = 10.0f + Next() * 20.0f,
                Speed = 7.0f + Next() * 9.0f,
                Drift = 1.2f + Next() * 0.8f,
                Opacity = 0.15f + Next() * 0.35f,
                Width = Next() < 0.15f ? 2.0f : 1.0f,
            };
        }

        private void EnsureDrops()
        {
            Rect rect = rectTransform.rect;
            _size = new Vector2(rect.width, rect.height);
            while (_drops.Count < DropCount)
            {
                _drops.Add(MakeDrop(_size.x, _size.y));
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            Step(Time.unscaledDeltaTime);
            SetVerticesDirty();
        }

        /// <summary>Advances the rain by <paramref name="seconds"/> (the original steps once per 60fps frame).</summary>
        public void Step(float seconds)
        {
            EnsureDrops();
            float frames = Mathf.Clamp(seconds * 60.0f, 0.0f, 6.0f);

            for (int i = 0; i < _drops.Count; i++)
            {
                Drop drop = _drops[i];
                drop.X -= drop.Drift * frames;
                drop.Y += drop.Speed * frames;

                if (drop.Y > _size.y - SplashBand && drop.Y < _size.y && Next() < 0.02f * frames)
                {
                    _splashes.Add(new Splash
                    {
                        X = drop.X,
                        Y = _size.y - Next() * 6.0f,
                        Radius = 1.0f,
                        MaxRadius = 4.0f + Next() * 4.0f,
                        Opacity = 0.5f,
                    });
                }

                if (drop.Y > _size.y || drop.X < -20.0f)
                {
                    drop.X = Next() * Mathf.Max(_size.x, 1.0f);
                    drop.Y = -20.0f;
                }
                _drops[i] = drop;
            }

            for (int i = _splashes.Count - 1; i >= 0; i--)
            {
                Splash splash = _splashes[i];
                splash.Radius += 0.4f * frames;
                splash.Opacity -= 0.04f * frames;
                if (splash.Opacity <= 0.0f || splash.Radius > splash.MaxRadius)
                {
                    _splashes.RemoveAt(i);
                }
                else
                {
                    _splashes[i] = splash;
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            EnsureDrops();
            Rect rect = rectTransform.rect;

            foreach (Drop drop in _drops)
            {
                Color colour = DropColour;
                colour.a = drop.Opacity * color.a;
                Vector2 from = ToLocal(rect, drop.X, drop.Y);
                Vector2 to = ToLocal(rect, drop.X - drop.Drift * (drop.Length / 6.0f), drop.Y + drop.Length);
                AddLine(vh, from, to, drop.Width, colour);
            }

            const int segments = 24;
            foreach (Splash splash in _splashes)
            {
                Color colour = SplashColour;
                colour.a = splash.Opacity * color.a;
                Vector2 previous = default;
                for (int s = 0; s <= segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2.0f;
                    Vector2 point = ToLocal(rect,
                        splash.X + Mathf.Cos(angle) * splash.Radius,
                        splash.Y + Mathf.Sin(angle) * splash.Radius * SplashSquash);
                    if (s > 0)
                    {
                        AddLine(vh, previous, point, 1.0f, colour);
                    }
                    previous = point;
                }
            }
        }

        /// <summary>Godot's canvas has y DOWN from the top-left; a Unity rect has y up.</summary>
        private static Vector2 ToLocal(Rect rect, float x, float y)
        {
            return new Vector2(rect.xMin + x, rect.yMax - y);
        }

        private static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color colour)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector2 side = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);

            int start = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = colour;
            vertex.position = from - side; vh.AddVert(vertex);
            vertex.position = from + side; vh.AddVert(vertex);
            vertex.position = to + side; vh.AddVert(vertex);
            vertex.position = to - side; vh.AddVert(vertex);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
