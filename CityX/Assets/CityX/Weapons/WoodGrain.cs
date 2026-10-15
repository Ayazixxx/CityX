using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE BAT'S WOOD (owner, 2026-10-15: "give the baseball bat a wooden texture"): pale ash with darker grain lines running along the bat,
    /// drawn in code the first time a bat appears, so the game carries no image for it. The bat's mesh is unwrapped round its length
    /// (BaseballBatBuilder: U round the bat, V along it), so each grain line is a column of the texture, wandering a little sideways as it runs.
    /// One texture and one material are shared by every bat, held or lying on the ground, this player's or another's.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class WoodGrain : MonoBehaviour
    {
        /// <summary>The texture round the bat, pixels.</summary>
        public const int Width = 64;

        /// <summary>The texture along the bat, pixels.</summary>
        public const int Height = 256;

        /// <summary>Grain lines round the bat (whole, so the texture wraps round seamlessly).</summary>
        public const int Lines = 9;

        /// <summary>The pale wood between the lines (ash).</summary>
        public static readonly Color EarlyWood = new Color(0.86f, 0.71f, 0.49f);

        /// <summary>The dark band at the end of each line.</summary>
        public static readonly Color LateWood = new Color(0.55f, 0.37f, 0.21f);

        private static Texture2D _texture;
        private static Material _material;
        private static Material _from;

        private void Awake()
        {
            var renderer = GetComponent<MeshRenderer>();
            Material source = renderer.sharedMaterial;
            if (source == null)
            {
                return;
            }
            if (_material == null || _from != source)
            {
                _from = source;
                _material = new Material(source) { name = source.name + " (grain)" };
                _material.SetTexture("_BaseMap", Texture());
                _material.SetColor("_BaseColor", Color.white);
            }
            renderer.sharedMaterial = _material;
        }

        /// <summary>The grain texture, drawn once.</summary>
        public static Texture2D Texture()
        {
            if (_texture != null)
            {
                return _texture;
            }
            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    pixels[y * Width + x] = Colour((x + 0.5f) / Width, (y + 0.5f) / Height, x, y);
                }
            }
            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true)
            {
                name = "BatWoodGrain",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(true, true);
            return _texture;
        }

        /// <summary>The wood at <paramref name="u"/> round the bat and <paramref name="v"/> along it, both 0..1 (U wraps round).</summary>
        public static Color32 Colour(float u, float v, int x, int y)
        {
            // Each line wanders sideways along the bat: noise in V only, so the texture still wraps round in U.
            float wander = (Mathf.PerlinNoise(v * 2.3f, 0.41f) - 0.5f) * 0.18f + (Mathf.PerlinNoise(v * 9.0f, 3.7f) - 0.5f) * 0.04f;
            float across = Mathf.Repeat((u + wander) * Lines, 1.0f);
            // Early wood darkening into a narrow band of late wood that ends sharply: a growth ring seen from the side.
            float late = Mathf.Pow(across, 6.0f);
            // Slow light and dark streaks round the bat (whole cycles, so they wrap), and a faint fleck per pixel.
            float streak = Mathf.Sin((u * 3.0f + Mathf.PerlinNoise(v * 1.7f, 8.2f) * 0.6f) * Mathf.PI * 2.0f) * 0.07f;
            float fleck = Hash(x, y) * 0.05f - 0.025f;
            Color c = Color.Lerp(EarlyWood, LateWood, Mathf.Clamp01(late * 0.85f + streak));
            c.r = Mathf.Clamp01(c.r + fleck);
            c.g = Mathf.Clamp01(c.g + fleck);
            c.b = Mathf.Clamp01(c.b + fleck);
            return c;
        }

        /// <summary>A repeatable 0..1 from a pixel's place.</summary>
        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0f;
            }
        }
    }
}
