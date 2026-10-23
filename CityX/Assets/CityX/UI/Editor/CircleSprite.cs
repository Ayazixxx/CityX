using UnityEditor;
using UnityEngine;

namespace CityX.UIEditor
{
    /// <summary>
    /// A plain white circle, alpha-antialiased at the edge, saved once as a
    /// Sprite asset -- what the portrait's ring and its Mask both use to
    /// read as a real circle rather than a masked square. Godot draws the
    /// same shape with a single fragment shader (ui/circular_mask.gdshader);
    /// a pre-baked sprite plus uGUI's own Mask component does the same job
    /// without needing a custom URP UI shader for one clipped image.
    ///
    /// The crosshair's dot is the same circle, small and with mipmaps, so it
    /// stays round and soft-edged a few pixels across (the big one has no
    /// mipmaps to shrink that far with).
    /// </summary>
    public static class CircleSprite
    {
        private const int Resolution = 256;

        private const string TexturePath = "Assets/CityX/UI/CircleMaskTexture.asset";

        private const int DotResolution = 32;

        private const string DotTexturePath = "Assets/CityX/UI/CrosshairDotTexture.asset";

        public static Sprite Ensure() => Make(TexturePath, Resolution, "CircleMask", mipmaps: false);

        /// <summary>The crosshair's dot.</summary>
        public static Sprite Dot() => Make(DotTexturePath, DotResolution, "CrosshairDot", mipmaps: true);

        private static Sprite Make(string path, int resolution, string name, bool mipmaps)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipmaps)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear,
            };

            float radius = resolution * 0.5f;
            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // One-pixel antialiased falloff at the boundary rather than
                    // a hard cutoff, which reads as jagged at the HUD's own
                    // on-screen size.
                    float alpha = Mathf.Clamp01(radius - dist);
                    pixels[y * resolution + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(mipmaps);

            AssetDatabase.CreateAsset(texture, path);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f), resolution);
            sprite.name = name;
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();
            return sprite;
        }
    }
}
