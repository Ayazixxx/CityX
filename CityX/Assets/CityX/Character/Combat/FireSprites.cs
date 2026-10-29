using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FIRE'S PICTURES, drawn in code once a session (docs/FIRE.md; no texture asset, as the blood splats' are): a flipbook of
    /// <see cref="Frames"/> flames -- a tongue of fire that sways, wobbles at its edge and breaks into tongues at the top, white-yellow at its
    /// core and red at its edge -- that loops seamlessly because its time runs round a circle in noise space; and a soft dot for the embers,
    /// the smoke and the glow. A real flame sheet is what makes particle fire read as fire instead of as a pile of orange blobs.
    /// </summary>
    public static class FireSprites
    {
        public const int Tiles = 4;
        public const int Frames = Tiles * Tiles;
        public const int FrameSize = 128;
        public const int DotSize = 64;

        /// <summary>How far round the noise the loop runs: wider is more change between frames.</summary>
        private const float LoopRadius = 0.9f;

        private static readonly Color Red = new Color(1.0f, 0.12f, 0.02f);
        private static readonly Color Orange = new Color(1.0f, 0.45f, 0.06f);
        private static readonly Color Yellow = new Color(1.0f, 0.85f, 0.35f);
        private static readonly Color White = new Color(1.0f, 0.97f, 0.8f);

        private static Texture2D _flames;
        private static Texture2D _dot;

        /// <summary>The flame flipbook, <see cref="Tiles"/> by <see cref="Tiles"/> frames, the first at the top left as a particle sheet counts them.</summary>
        public static Texture2D Flames()
        {
            if (_flames == null)
            {
                _flames = BuildFlames();
            }
            return _flames;
        }

        /// <summary>A soft round dot, white, fading to nothing at its edge.</summary>
        public static Texture2D Dot()
        {
            if (_dot == null)
            {
                _dot = BuildDot();
            }
            return _dot;
        }

        private static Texture2D BuildFlames()
        {
            int size = Tiles * FrameSize;
            var pixels = new Color[size * size];
            for (int frame = 0; frame < Frames; frame++)
            {
                int tileX = frame % Tiles;
                int tileY = frame / Tiles;
                float angle = frame / (float)Frames * Mathf.PI * 2.0f;
                float ct = Mathf.Cos(angle) * LoopRadius;
                float st = Mathf.Sin(angle) * LoopRadius;
                for (int py = 0; py < FrameSize; py++)
                {
                    for (int px = 0; px < FrameSize; px++)
                    {
                        float u = (px + 0.5f) / FrameSize;
                        float v = (py + 0.5f) / FrameSize;
                        // A sheet counts its tiles from the top left; a texture's rows start at the bottom.
                        int x = tileX * FrameSize + px;
                        int y = (Tiles - 1 - tileY) * FrameSize + py;
                        pixels[y * size + x] = FlamePixel(u, v, ct, st);
                    }
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "FireFlames", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>One pixel of a flame frame: <paramref name="u"/> across, <paramref name="v"/> up the frame, the noise looked at round (<paramref name="ct"/>, <paramref name="st"/>).</summary>
        private static Color FlamePixel(float u, float v, float ct, float st)
        {
            // The flame's centre line sways, more toward the top.
            float sway = (Mathf.PerlinNoise(v * 1.6f + 3.1f + ct * 0.5f, st * 0.5f + 0.7f) - 0.5f) * 0.28f * v;
            float centre = 0.5f + sway;
            float across = Mathf.Abs(u - centre);
            // Wide at the base, a tip at the top, its edge wobbling.
            float wobble = (Mathf.PerlinNoise(u * 3.0f + ct * 0.8f + 11.0f, v * 2.6f + st * 0.8f + 5.0f) - 0.5f) * 0.14f;
            float width = 0.30f * Mathf.Pow(1.0f - v, 0.55f) + wobble * (0.3f + 0.7f * v);
            if (width <= 0.004f || across >= width)
            {
                return Color.clear;
            }
            float inside = (width - across) / width;   // 1 on the centre line, 0 at the edge
            float edge = Mathf.Clamp01(inside / 0.12f);
            // The top breaks into tongues along the noise; the base is whole.
            float tongue = Mathf.PerlinNoise(u * 4.5f + ct + 21.0f, v * 3.5f + st + 9.0f);
            float split = Mathf.Clamp01((tongue - 0.45f + (1.0f - v) * 0.9f) / 0.2f);
            float alpha = edge * split * Mathf.Clamp01(v / 0.08f) * (0.7f + 0.3f * (1.0f - v));   // the tips a little see-through
            if (alpha <= 0.002f)
            {
                return Color.clear;
            }
            // Hot at the centre of the base, cooling outward and upward, with licks of brighter and darker fire through it.
            float lick = 0.7f + 0.3f * Mathf.PerlinNoise(u * 6.0f + ct * 1.5f + 31.0f, v * 4.0f + st * 1.5f + 17.0f);
            float core = Mathf.Clamp01(inside * 1.3f) * (1.0f - 0.5f * v) * lick;
            Color rgb = core < 0.25f ? Color.Lerp(Red, Orange, core / 0.25f)
                : core < 0.5f ? Color.Lerp(Orange, Yellow, (core - 0.25f) / 0.25f)
                : Color.Lerp(Yellow, White, Mathf.Clamp01((core - 0.5f) / 0.4f));
            return new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        private static Texture2D BuildDot()
        {
            var pixels = new Color32[DotSize * DotSize];
            float half = DotSize * 0.5f;
            for (int y = 0; y < DotSize; y++)
            {
                for (int x = 0; x < DotSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1.0f - Mathf.Sqrt(dx * dx + dy * dy)), 1.6f);
                    pixels[y * DotSize + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
            var texture = new Texture2D(DotSize, DotSize, TextureFormat.RGBA32, false)
            {
                name = "FireDot", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// The particle shader, see-through and SOFT (it fades where it meets the street instead of cutting into it): additive for the fire's
        /// own light, alpha-blended for the smoke's dark, premultiplied for the flames (they cover what is behind them where they are solid and
        /// add their light at their edges, so they stay orange against a bright sky where additive ones washed out pink). The soft-particle
        /// keyword is the one Self Smite's glow asset carries, so a build has that variant; the premultiplied one is made here only.
        /// </summary>
        public static Material Particles(string name, Texture2D texture, ParticleBlend blend, int queueOffset)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = name, color = Color.white };
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            if (!material.HasProperty("_Surface"))
            {
                return material;
            }
            bool additive = blend == ParticleBlend.Additive;
            bool premultiplied = blend == ParticleBlend.Premultiplied;
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", additive ? 2.0f : premultiplied ? 1.0f : 0.0f);
            material.SetFloat("_SrcBlend", (float)(premultiplied ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (premultiplied)
            {
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            material.SetFloat("_SoftParticlesEnabled", 1.0f);
            material.SetFloat("_SoftParticlesNearFadeDistance", 0.0f);
            material.SetFloat("_SoftParticlesFarFadeDistance", SoftFadeMetres);
            material.SetVector("_SoftParticleFadeParams", new Vector4(0.0f, 1.0f / SoftFadeMetres, 0.0f, 0.0f));
            material.EnableKeyword("_SOFTPARTICLES_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
            return material;
        }

        /// <summary>How deep behind it a particle fades out where it meets something.</summary>
        public const float SoftFadeMetres = 0.6f;
    }

    /// <summary>How a particle material lays its colour over what is behind it.</summary>
    public enum ParticleBlend
    {
        Additive,
        Alpha,
        Premultiplied,
    }
}
