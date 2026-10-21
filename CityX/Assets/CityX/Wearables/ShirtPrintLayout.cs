using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Prints a picture onto a garment's texture: where a placement on the cloth lands on the texture, and the picture laid over the cloth's
    /// colour there. Pure, so the placement maths is tested without a scene.
    /// </summary>
    public static class ShirtPrintLayout
    {
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

        /// <summary>The picture at (u, v) in 0..1, blended between its four nearest texels.</summary>
        private static Color Sample(Color32[] logo, int width, int height, float u, float v)
        {
            if (width < 2 || height < 2)
            {
                return logo[0];
            }
            float x = Mathf.Clamp(u * width - 0.5f, 0.0f, width - 1.0f), y = Mathf.Clamp(v * height - 0.5f, 0.0f, height - 1.0f);
            int ix = Mathf.Min((int)x, width - 2), iy = Mathf.Min((int)y, height - 2);
            float fx = x - ix, fy = y - iy;
            Color bottom = Color.Lerp(Premultiplied(logo[iy * width + ix]), Premultiplied(logo[iy * width + ix + 1]), fx);
            Color top = Color.Lerp(Premultiplied(logo[(iy + 1) * width + ix]), Premultiplied(logo[(iy + 1) * width + ix + 1]), fx);
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
