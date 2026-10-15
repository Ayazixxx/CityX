using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE HUD FRAME'S ART, painted in code (owner, 2026-10-14, with two World of Warcraft unit frames: "make it look like the world of warcraft
    /// version ours looks like crap"): a gold ring shaded like a metal tube lit from the top left, with a dark line along each rim; a dark plate
    /// with a gold edge behind the name and the bars; glossy bar fills (grey, so each bar's colour tints them); a dark bar well with a shadow
    /// along its top; a soft shadow inside the portrait's rim so the face sits in a hollow. Made the first time a HUD needs them and shared,
    /// never stored: a 256-pixel texture saved as an asset is half a megabyte (CircleMaskTexture.asset), and the repository is kept small.
    /// </summary>
    public static class HudArt
    {
        /// <summary>The ring's gold where the light falls straight on it.</summary>
        public static readonly Color Gold = new Color(0.82f, 0.64f, 0.26f, 1.0f);

        /// <summary>The ring's width as a fraction of its radius.</summary>
        public const float RingWidthFraction = 0.13f;

        private static Sprite _ring;
        private static Sprite _plate;
        private static Sprite _barFill;
        private static Sprite _barWell;
        private static Sprite _rimShadow;

        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeRing(256, RingWidthFraction));

        public static Sprite Plate => _plate != null ? _plate : (_plate = MakePlate(64, 8));

        public static Sprite BarFill => _barFill != null ? _barFill : (_barFill = MakeBarFill(8, 32));

        public static Sprite BarWell => _barWell != null ? _barWell : (_barWell = MakeBarWell(16, 4));

        public static Sprite RimShadow => _rimShadow != null ? _rimShadow : (_rimShadow = MakeRimShadow(256));

        /// <summary>Puts the art on a HUD's frame (its Awake does; so does the editor's HUD capture, where nothing runs Awake).</summary>
        public static void Paint(PlayerHud hud)
        {
            SetSprite(hud.PortraitRing, Ring, Image.Type.Simple);
            SetSprite(hud.LevelRing, Ring, Image.Type.Simple);
            SetSprite(hud.PortraitRimShadow, RimShadow, Image.Type.Simple);
            SetSprite(hud.Plate, Plate, Image.Type.Sliced);
            if (hud.BarWells != null)
            {
                foreach (Image well in hud.BarWells)
                {
                    SetSprite(well, BarWell, Image.Type.Sliced);
                }
            }
            if (hud.BarFills != null)
            {
                foreach (Image fill in hud.BarFills)
                {
                    SetSprite(fill, BarFill, Image.Type.Simple);
                }
            }
        }

        private static void SetSprite(Image image, Sprite sprite, Image.Type type)
        {
            if (image == null)
            {
                return;
            }
            image.sprite = sprite;
            image.type = type;
            image.fillCenter = true;
        }

        private static Texture2D NewTexture(int width, int height, bool mips)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, mips)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave,
                name = "HudArt",
            };
        }

        private static Sprite Finish(Texture2D texture, Color[] pixels, Vector4 border, string name)
        {
            texture.SetPixels(pixels);
            texture.Apply(texture.mipmapCount > 1, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f, 0,
                SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>
        /// A gold ring, shaded as a round metal tube: each pixel's surface normal turns from the inner rim (facing in) over the crown (facing out
        /// of the screen) to the outer rim (facing out), lit from the top left with a little shine; a dark engraved line along both rims.
        /// </summary>
        private static Sprite MakeRing(int size, float widthFraction)
        {
            Texture2D texture = NewTexture(size, size, true);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float outer = half - 1.0f;
            float inner = outer * (1.0f - widthFraction);
            Vector3 light = new Vector3(-0.5f, 0.6f, 0.62f).normalized;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    if (alpha <= 0.0f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }
                    float u = Mathf.Clamp01((d - inner) / (outer - inner));
                    float across = (u - 0.5f) * Mathf.PI;
                    float rx = dx / Mathf.Max(d, 1e-4f);
                    float ry = dy / Mathf.Max(d, 1e-4f);
                    var normal = new Vector3(rx * Mathf.Sin(across), ry * Mathf.Sin(across), Mathf.Cos(across));
                    float lit = Vector3.Dot(normal, light);
                    float diffuse = Mathf.Max(0.0f, lit);
                    Vector3 reflected = 2.0f * lit * normal - light;
                    float shine = Mathf.Pow(Mathf.Max(0.0f, reflected.z), 22.0f);
                    Color c = Gold * (0.28f + 0.85f * diffuse) + new Color(1.0f, 0.94f, 0.78f) * (0.6f * shine);
                    float rim = Mathf.Min(u, 1.0f - u);
                    if (rim < 0.12f)
                    {
                        c *= Mathf.Lerp(0.3f, 1.0f, rim / 0.12f);
                    }
                    c.a = alpha;
                    pixels[y * size + x] = c;
                }
            }
            return Finish(texture, pixels, Vector4.zero, "HudRing");
        }

        /// <summary>
        /// The plate behind the name and the bars, nine-sliced: a black outer line, a two-pixel gold edge lit from above, a dark inner line,
        /// and a dark fill that is a touch lighter at the top. Its corners are rounded a little.
        /// </summary>
        private static Sprite MakePlate(int size, int border)
        {
            Texture2D texture = NewTexture(size, size, false);
            var pixels = new Color[size * size];
            const float corner = 5.0f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float inset = InsetInRoundedSquare(x + 0.5f, y + 0.5f, size, corner);
                    float vertical = (y + 0.5f) / size;
                    Color c;
                    if (inset < 0.0f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }
                    if (inset < 1.0f)
                    {
                        c = new Color(0.0f, 0.0f, 0.0f, 0.9f);
                    }
                    else if (inset < 3.0f)
                    {
                        c = Gold * Mathf.Lerp(0.55f, 1.05f, vertical);
                        c.a = 1.0f;
                    }
                    else if (inset < 4.0f)
                    {
                        c = new Color(0.05f, 0.04f, 0.02f, 0.95f);
                    }
                    else
                    {
                        float shade = Mathf.Lerp(0.035f, 0.11f, vertical);
                        c = new Color(shade, shade * 0.95f, shade * 0.85f, 0.86f);
                    }
                    c.a *= Mathf.Clamp01(inset + 0.5f);
                    pixels[y * size + x] = c;
                }
            }
            return Finish(texture, pixels, new Vector4(border, border, border, border), "HudPlate");
        }

        /// <summary>How far a point is inside a square with rounded corners, in pixels (negative outside).</summary>
        private static float InsetInRoundedSquare(float x, float y, int size, float corner)
        {
            float qx = Mathf.Abs(x - size * 0.5f) - (size * 0.5f - corner);
            float qy = Mathf.Abs(y - size * 0.5f) - (size * 0.5f - corner);
            float outside = new Vector2(Mathf.Max(qx, 0.0f), Mathf.Max(qy, 0.0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0.0f);
            return corner - outside;
        }

        /// <summary>A bar's fill in grey (the bar's own colour tints it): darker at the bottom, a bright band of shine above the middle.</summary>
        private static Sprite MakeBarFill(int width, int height)
        {
            Texture2D texture = NewTexture(width, height, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                float shine = Mathf.Exp(-Mathf.Pow((v - 0.72f) / 0.09f, 2.0f));
                float value = Mathf.Clamp01(Mathf.Lerp(0.58f, 0.9f, v) + 0.22f * shine);
                if (y == height - 1)
                {
                    value = 0.62f;   // a darker line along the top edge, against the well
                }
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color(value, value, value, 1.0f);
                }
            }
            return Finish(texture, pixels, Vector4.zero, "HudBarFill");
        }

        /// <summary>The well a bar sits in, nine-sliced: a black edge and a near-black inside, darkest along the top (a shadow cast into it).</summary>
        private static Sprite MakeBarWell(int size, int border)
        {
            Texture2D texture = NewTexture(size, size, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    float fromTop = size - 1 - y;
                    float shade = edge ? 0.0f : Mathf.Lerp(0.02f, 0.07f, Mathf.Clamp01(fromTop / 4.0f));
                    pixels[y * size + x] = new Color(shade, shade, shade * 1.15f, edge ? 0.95f : 0.82f);
                }
            }
            return Finish(texture, pixels, new Vector4(border, border, border, border), "HudBarWell");
        }

        /// <summary>A shadow inside the portrait's rim: clear in the middle, darkening toward the edge, so the face sits in a hollow under the ring.</summary>
        private static Sprite MakeRimShadow(int size)
        {
            Texture2D texture = NewTexture(size, size, true);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float alpha = Mathf.SmoothStep(0.0f, 0.72f, Mathf.InverseLerp(0.68f, 1.0f, r));
                    // A touch more shadow under the top of the rim, as the light comes from above.
                    alpha *= Mathf.Lerp(0.8f, 1.15f, Mathf.InverseLerp(-half, half, dy));
                    pixels[y * size + x] = new Color(0.0f, 0.0f, 0.0f, Mathf.Clamp01(alpha));
                }
            }
            return Finish(texture, pixels, Vector4.zero, "HudRimShadow");
        }
    }
}
