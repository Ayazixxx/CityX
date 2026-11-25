using CityX.Core;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// One side of a design as the PNG a design keeps (Core.ShirtDesignBlob), and back: <see cref="ShirtDesignArea.Width"/> by
    /// <see cref="ShirtDesignArea.Height"/> pixels, see-through where nothing is printed. A side too heavy as it is (a photograph) is kept with
    /// its colours a little coarser, which a shirt never shows, until it fits.
    /// </summary>
    public static class DesignPng
    {
        private const int Width = ShirtDesignArea.Width, Height = ShirtDesignArea.Height;

        /// <summary>The PNG of a side; empty when nothing is printed on it, null when even coarse it is too heavy.</summary>
        public static byte[] Encode(Color32[] pixels)
        {
            if (pixels == null || pixels.Length != Width * Height || Clear(pixels))
            {
                return System.Array.Empty<byte>();
            }
            foreach (byte keep in new byte[] { 0xFF, 0xF8, 0xF0, 0xE0 })
            {
                byte[] png = Png(pixels, keep);
                if (png.Length <= ShirtDesignBlob.MaxSideBytes)
                {
                    return png;
                }
            }
            return null;
        }

        /// <summary>A side's pixels from its PNG, at the design's size (a picture of another size stretched to it); null for none.</summary>
        public static Color32[] Pixels(byte[] png)
        {
            if (png == null || png.Length == 0)
            {
                return null;
            }
            var picture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!picture.LoadImage(png))
                {
                    return null;
                }
                Color32[] read = picture.GetPixels32();
                if (picture.width == Width && picture.height == Height)
                {
                    return read;
                }
                var pixels = new Color32[Width * Height];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        pixels[y * Width + x] = ShirtPrintLayout.Sample(read, picture.width, picture.height, (x + 0.5f) / Width, (y + 0.5f) / Height);
                    }
                }
                return pixels;
            }
            finally
            {
                Object.DestroyImmediate(picture);
            }
        }

        private static byte[] Png(Color32[] pixels, byte keep)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                if (keep == 0xFF)
                {
                    texture.SetPixels32(pixels);
                }
                else
                {
                    var coarse = new Color32[pixels.Length];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color32 c = pixels[i];
                        coarse[i] = c.a == 0 ? default : new Color32((byte)(c.r & keep), (byte)(c.g & keep), (byte)(c.b & keep), (byte)(c.a & keep));
                    }
                    texture.SetPixels32(coarse);
                }
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static bool Clear(Color32[] pixels)
        {
            foreach (Color32 c in pixels)
            {
                if (c.a != 0) return false;
            }
            return true;
        }
    }
}
