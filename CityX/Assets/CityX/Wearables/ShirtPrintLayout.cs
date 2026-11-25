using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Prints a picture onto a garment's texture: where a placement on the cloth lands on the texture, and the picture laid over the cloth's
    /// colour there. Pure, so the placement maths is tested without a scene.
    /// </summary>
    public static class ShirtPrintLayout
    {
        /// <summary>Where the T-Shirt's front and back pieces lie on its texture (tshirt_build.py's PIECE_BOXES).</summary>
        public static readonly Rect FrontPiece = new Rect(0.0f, 0.4f, 0.5f, 0.6f);

        public static readonly Rect BackPiece = new Rect(0.5f, 0.4f, 0.5f, 0.6f);

        /// <summary>The middle of the chest and of the back, bind pose (tshirt_build.py's TEMPLATE_MARKS).</summary>
        public const float MiddleHeight = 1.62f;

        /// <summary>
        /// Where a print's centre lands on the texture: the middle of its side (<paramref name="middleUv"/>) moved <paramref name="rightMetres"/>
        /// and <paramref name="upMetres"/> along the cloth, at <paramref name="uvPerMetre"/> (the side's piece lies upright on the texture,
        /// as seen from that side: u to the right, v up).
        /// </summary>
        public static Vector2 CentreUv(Vector2 middleUv, float uvPerMetre, float rightMetres, float upMetres)
        {
            return middleUv + new Vector2(rightMetres, upMetres) * uvPerMetre;
        }

        /// <summary>
        /// A square texture of <paramref name="size"/> texels in <paramref name="cloth"/>, with the picture (<paramref name="logo"/>, its rows
        /// bottom up like Texture2D's) laid over it centred at <paramref name="centreUv"/>, <paramref name="widthUv"/> wide, turned
        /// <paramref name="rotationDegrees"/> anticlockwise, its own transparency letting the cloth through.
        /// </summary>
        public static Color32[] Print(Color32[] logo, int logoWidth, int logoHeight, int size, Color32 cloth, Vector2 centreUv, float widthUv,
            float rotationDegrees)
        {
            var texels = new Color32[size * size];
            for (int i = 0; i < texels.Length; i++)
            {
                texels[i] = cloth;
            }
            float heightUv = widthUv * logoHeight / logoWidth;
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            float reach = 0.5f * Mathf.Sqrt(widthUv * widthUv + heightUv * heightUv);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((centreUv.x - reach) * size)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt((centreUv.x + reach) * size));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((centreUv.y - reach) * size)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt((centreUv.y + reach) * size));
            Color clothColour = cloth;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float du = (x + 0.5f) / size - centreUv.x, dv = (y + 0.5f) / size - centreUv.y;
                    float across = (du * cos + dv * sin) / widthUv + 0.5f;    // turned back into the picture's own frame
                    float up = (-du * sin + dv * cos) / heightUv + 0.5f;
                    if (across < 0.0f || across > 1.0f || up < 0.0f || up > 1.0f)
                    {
                        continue;
                    }
                    Color ink = Sample(logo, logoWidth, logoHeight, across, up);
                    texels[y * size + x] = Color.Lerp(clothColour, new Color(ink.r, ink.g, ink.b, 1.0f), ink.a);
                }
            }
            return texels;
        }

        /// <summary>
        /// Lays <paramref name="picture"/> (rows bottom up) over a square texture of <paramref name="size"/> texels already holding the cloth,
        /// centred at <paramref name="centreUv"/>, <paramref name="widthUv"/> wide, upright; only inside <paramref name="piece"/>, so nothing
        /// spills onto the texture's other pieces (a sleeve, the other side). Its transparency lets the cloth through.
        /// </summary>
        public static void Overlay(Color32[] texels, int size, Color32[] picture, int pictureWidth, int pictureHeight, Vector2 centreUv, float widthUv,
            Rect piece)
        {
            float heightUv = widthUv * pictureHeight / pictureWidth;
            float left = Mathf.Max(centreUv.x - widthUv * 0.5f, piece.xMin), right = Mathf.Min(centreUv.x + widthUv * 0.5f, piece.xMax);
            float bottom = Mathf.Max(centreUv.y - heightUv * 0.5f, piece.yMin), top = Mathf.Min(centreUv.y + heightUv * 0.5f, piece.yMax);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(left * size)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(right * size));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(bottom * size)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(top * size));
            for (int y = y0; y <= y1; y++)
            {
                float v = (y + 0.5f) / size;
                float up = (v - centreUv.y) / heightUv + 0.5f;
                if (up < 0.0f || up > 1.0f || !InRange(v, piece.yMin, piece.yMax))
                {
                    continue;
                }
                for (int x = x0; x <= x1; x++)
                {
                    float u = (x + 0.5f) / size;
                    float across = (u - centreUv.x) / widthUv + 0.5f;
                    if (across < 0.0f || across > 1.0f || !InRange(u, piece.xMin, piece.xMax))
                    {
                        continue;
                    }
                    Color ink = Sample(picture, pictureWidth, pictureHeight, across, up);
                    if (ink.a <= 0.0f)
                    {
                        continue;
                    }
                    Color cloth = texels[y * size + x];
                    texels[y * size + x] = Color.Lerp(cloth, new Color(ink.r, ink.g, ink.b, 1.0f), ink.a);
                }
            }
        }

        private static bool InRange(float value, float min, float max) => value >= min && value <= max;

        /// <summary>
        /// A square texture of <paramref name="size"/> texels covered all over with the picture, each copy <paramref name="repeatUv"/> wide
        /// (the garment's pieces lie at one scale on it, so the pattern is one size everywhere on the cloth), the pattern slid by
        /// <paramref name="offsetUv"/> and turned <paramref name="rotationDegrees"/> anticlockwise, the picture's transparency letting
        /// <paramref name="cloth"/> through.
        /// </summary>
        public static Color32[] Cover(Color32[] picture, int pictureWidth, int pictureHeight, int size, Color32 cloth, float repeatUv,
            Vector2 offsetUv = default, float rotationDegrees = 0.0f)
        {
            var texels = new Color32[size * size];
            float repeatHeight = repeatUv * pictureHeight / pictureWidth;
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            Color clothColour = cloth;
            for (int y = 0; y < size; y++)
            {
                float dv = (y + 0.5f) / size - offsetUv.y;
                for (int x = 0; x < size; x++)
                {
                    float du = (x + 0.5f) / size - offsetUv.x;
                    float u = Mathf.Repeat((du * cos + dv * sin) / repeatUv, 1.0f);   // turned back into the pattern's own frame
                    float v = Mathf.Repeat((-du * sin + dv * cos) / repeatHeight, 1.0f);
                    Color ink = Sample(picture, pictureWidth, pictureHeight, u, v, wrap: true);
                    texels[y * size + x] = Color.Lerp(clothColour, new Color(ink.r, ink.g, ink.b, 1.0f), ink.a);
                }
            }
            return texels;
        }

        /// <summary>The picture at (u, v) in 0..1, blended between its four nearest texels (its colour, not weighted by its own transparency).</summary>
        public static Color Sample(Color32[] logo, int width, int height, float u, float v, bool wrap = false)
        {
            if (width < 2 || height < 2)
            {
                return logo[0];
            }
            int ix, iy, ix1, iy1;
            float fx, fy;
            if (wrap)
            {
                // Repeated: the texels past an edge are the other edge's, so one copy runs into the next without a seam.
                float x = Mathf.Repeat(u * width - 0.5f, width), y = Mathf.Repeat(v * height - 0.5f, height);
                ix = Mathf.Min((int)x, width - 1);
                iy = Mathf.Min((int)y, height - 1);
                ix1 = (ix + 1) % width;
                iy1 = (iy + 1) % height;
                fx = x - ix;
                fy = y - iy;
            }
            else
            {
                float x = Mathf.Clamp(u * width - 0.5f, 0.0f, width - 1.0f), y = Mathf.Clamp(v * height - 0.5f, 0.0f, height - 1.0f);
                ix = Mathf.Min((int)x, width - 2);
                iy = Mathf.Min((int)y, height - 2);
                ix1 = ix + 1;
                iy1 = iy + 1;
                fx = x - ix;
                fy = y - iy;
            }
            Color bottom = Color.Lerp(Premultiplied(logo[iy * width + ix]), Premultiplied(logo[iy * width + ix1]), fx);
            Color top = Color.Lerp(Premultiplied(logo[iy1 * width + ix]), Premultiplied(logo[iy1 * width + ix1]), fx);
            Color blended = Color.Lerp(bottom, top, fy);
            return blended.a > 0.0f ? new Color(blended.r / blended.a, blended.g / blended.a, blended.b / blended.a, blended.a) : Color.clear;
        }

        /// <summary>A texel's colour weighted by its transparency, so a see-through texel's colour (often white) does not halo the edges.</summary>
        private static Color Premultiplied(Color32 texel)
        {
            Color c = texel;
            return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
        }
    }
}
