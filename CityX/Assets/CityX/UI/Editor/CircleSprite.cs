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
    /// </summary>
    public static class CircleSprite
    {
        private const int Resolution = 256;

        private const string TexturePath = "Assets/CityX/UI/CircleMaskTexture.asset";

        public static Sprite Ensure()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float radius = Resolution * 0.5f;
            var pixels = new Color32[Resolution * Resolution];
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // One-pixel antialiased falloff at the boundary rather than
                    // a hard cutoff, which reads as jagged at the HUD's own
                    // on-screen size.
                    float alpha = Mathf.Clamp01(radius - dist);
                    pixels[y * Resolution + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            AssetDatabase.CreateAsset(texture, TexturePath);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Resolution, Resolution),
                new Vector2(0.5f, 0.5f), Resolution);
            sprite.name = "CircleMask";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();
            return sprite;
        }
    }
}
