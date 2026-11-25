using System.Collections.Generic;
using CityX.Core;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A SHIRT WITH A PRESSED DESIGN, AS IT IS DRAWN (docs/HEAT_PRESS.md): the shirt's own cloth (its colour, or its texture with whatever is
    /// printed on it already) with the design's front and back pictures laid over their rectangles (<see cref="ShirtDesignArea"/>), as one
    /// texture and a copy of the shirt's material. Made once for each shirt and design on this machine and shared by everyone wearing it (a
    /// crowd in the same shirt costs one texture), compressed on the graphics card like the shirts made in the editor.
    /// </summary>
    public static class PrintedShirt
    {
        /// <summary>The texture's size: the shirts' own (ShirtPrintBaker.TextureSize).</summary>
        public const int TextureSize = 2048;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private static readonly Dictionary<string, Material> Made = new Dictionary<string, Material>();

        /// <summary>The shirt's material with <paramref name="blob"/> printed on it; null when the design is not a sane one.</summary>
        public static Material MaterialFor(TShirt shirt, string designId, byte[] blob)
        {
            if (shirt == null || shirt.GarmentMaterial == null || !ShirtDesignBlob.TryUnpack(blob, out byte[] front, out byte[] back))
            {
                return null;
            }
            string key = $"{shirt.GarmentMaterial.GetInstanceID()}|{shirt.UvPerMetre}|{designId}";
            if (Made.TryGetValue(key, out Material made) && made != null)
            {
                return made;
            }
            Color32[] texels = Cloth(shirt.GarmentMaterial);
            Lay(texels, front, shirt.FrontMiddleUv, shirt.UvPerMetre, ShirtPrintLayout.FrontPiece);
            Lay(texels, back, shirt.BackMiddleUv, shirt.UvPerMetre, ShirtPrintLayout.BackPiece);
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, true) { name = "Printed " + designId };
            texture.SetPixels32(texels);
            texture.Apply(true);
            texture.Compress(false);
            texture.Apply(false, true);   // the copy in memory let go of: only the graphics card's is needed
            made = new Material(shirt.GarmentMaterial) { name = shirt.GarmentMaterial.name + " printed" };
            made.SetTexture(BaseMap, texture);
            made.SetColor(BaseColor, Color.white);   // the cloth's colour is in the texture now
            Made[key] = made;
            return made;
        }

        /// <summary>The cloth as it is: the material's texture times its colour, or its colour alone.</summary>
        public static Color32[] Cloth(Material material)
        {
            Color tint = material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
            Texture map = material.HasProperty(BaseMap) ? material.GetTexture(BaseMap) : null;
            var texels = new Color32[TextureSize * TextureSize];
            if (map == null)
            {
                var plain = (Color32)new Color(tint.r, tint.g, tint.b, 1.0f);
                for (int i = 0; i < texels.Length; i++) texels[i] = plain;
                return texels;
            }
            // Read through the graphics card: the shirts' own textures are not kept readable.
            RenderTexture target = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture before = RenderTexture.active;
            var read = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(map, target);
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, TextureSize, TextureSize), 0, 0, false);
                read.Apply(false);
                Color32[] cloth = read.GetPixels32();
                for (int i = 0; i < cloth.Length; i++)
                {
                    Color32 c = cloth[i];
                    texels[i] = new Color32((byte)(c.r * tint.r), (byte)(c.g * tint.g), (byte)(c.b * tint.b), 255);
                }
                return texels;
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(read);   // at once, in a test outside play too
            }
        }

        /// <summary>One side's picture over its rectangle.</summary>
        private static void Lay(Color32[] texels, byte[] png, Vector2 middleUv, float uvPerMetre, Rect piece)
        {
            if (png == null || png.Length == 0 || uvPerMetre <= 0.0f)
            {
                return;
            }
            var picture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!picture.LoadImage(png) || picture.width < 2 || picture.height < 2)
                {
                    return;
                }
                ShirtPrintLayout.Overlay(texels, TextureSize, picture.GetPixels32(), picture.width, picture.height,
                    ShirtDesignArea.CentreUv(middleUv, uvPerMetre), ShirtDesignArea.WidthMetres * uvPerMetre, piece);
            }
            finally
            {
                Object.DestroyImmediate(picture);
            }
        }
    }
}
