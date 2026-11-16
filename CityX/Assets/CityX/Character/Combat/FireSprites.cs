using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FIRE'S MATERIALS AND PICTURES (docs/FIRE.md). The flames and the smoke are the CityX/Fire shader (View/Shaders/CityXFire.shader):
    /// every flame worked out per pixel from a noise texture made here once a session (tileable, so it scrolls up through the flames forever
    /// without a seam: R a fine noise, G a coarse one). The shader's materials (the flames, the smoke, the fire spells' burning gas) come from
    /// Resources (FireMaterialsBuilder makes them, so a player build keeps the shader) and are copied before the noise goes on, never changed in
    /// place. The glow and the embers are URP's particle shader with a soft dot.
    /// </summary>
    public static class FireSprites
    {
        public const string ShaderName = "CityX/Fire";
        public const string FlameMaterialName = "FireFlames";
        public const string SmokeMaterialName = "FireSmoke";
        public const string GasMaterialName = "FireGas";

        public const int NoiseSize = 256;
        public const int DotSize = 64;

        /// <summary>The fine noise's lattice cells across the texture, and its octaves; the coarse one's.</summary>
        public const int FineCells = 8;

        public const int FineOctaves = 4;
        public const int CoarseCells = 3;
        public const int CoarseOctaves = 3;

        private static Texture2D _noise;
        private static System.Threading.Tasks.Task<Color[]> _noisePixels;
        private static Texture2D _dot;
        private static Material _flames;
        private static Material _smoke;
        private static Material _gas;

        /// <summary>The flames' material (CityX/Fire, mode 0), the noise on it.</summary>
        public static Material Flames()
        {
            if (_flames == null)
            {
                _flames = FireMaterial(FlameMaterialName, 0.0f, 2);
            }
            return _flames;
        }

        /// <summary>The smoke's material (CityX/Fire, mode 1), the noise on it.</summary>
        public static Material Smoke()
        {
            if (_smoke == null)
            {
                _smoke = FireMaterial(SmokeMaterialName, 1.0f, 0);
            }
            return _smoke;
        }

        /// <summary>The fire spells' burning gas (CityX/Fire, mode 2: docs/FIRE.md "The fire spells"), the noise on it.</summary>
        public static Material Gas()
        {
            if (_gas == null)
            {
                _gas = FireMaterial(GasMaterialName, 2.0f, 2);
                _gas.SetFloat("_Occlusion", GasOcclusion);
            }
            return _gas;
        }

        /// <summary>
        /// How much a tongue of burning gas hides what is behind it: nearly all, unlike a ground flame's 0.6 (a window or the sky showing through
        /// the hottest band turned it a pale grey-blue).
        /// </summary>
        public const float GasOcclusion = 0.92f;

        /// <summary>The flames' brightness: past 1 at the core so the bloom catches it, not so far that the whole fire goes white by day.</summary>
        public const float FlameBrightness = 1.5f;

        private static Material FireMaterial(string name, float mode, int queueOffset)
        {
            Material asset = Resources.Load<Material>(name);
            Material material = asset != null ? new Material(asset) : new Material(Shader.Find(ShaderName));
            material.name = name;
            material.SetFloat("_Mode", mode);
            material.SetFloat("_Brightness", FlameBrightness);
            material.SetTexture("_NoiseTex", Noise());
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
            return material;
        }

        /// <summary>The noise the fire is made of: R fine, G coarse, both tiling seamlessly, each stretched to fill 0..1.</summary>
        public static Texture2D Noise()
        {
            if (_noise == null)
            {
                _noise = BuildNoise();
            }
            return _noise;
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

        /// <summary>
        /// The noise worked out on another thread as the game starts (pure maths, about a tenth of a second of it), so the first fire to be
        /// lit does not stop the game while it is made; the texture itself is made from it on first use.
        /// </summary>
        public static bool NoiseWorkedOut => _noisePixels != null && _noisePixels.IsCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartNoise()
        {
            _noisePixels ??= System.Threading.Tasks.Task.Run(NoisePixels);
        }

        private static Texture2D BuildNoise()
        {
            StartNoise();
            Color[] pixels = _noisePixels.Result;   // waits only for a fire lit before the noise is done
            var texture = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, true, true)
            {
                name = "FireNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Color[] NoisePixels()
        {
            int[] permutation = Permutation(20261029);
            var fine = new float[NoiseSize * NoiseSize];
            var coarse = new float[NoiseSize * NoiseSize];
            for (int y = 0; y < NoiseSize; y++)
            {
                for (int x = 0; x < NoiseSize; x++)
                {
                    float u = (x + 0.5f) / NoiseSize;
                    float v = (y + 0.5f) / NoiseSize;
                    fine[y * NoiseSize + x] = Fbm(u, v, FineCells, FineOctaves, permutation);
                    coarse[y * NoiseSize + x] = Fbm(u + 0.37f, v + 0.61f, CoarseCells, CoarseOctaves, permutation);
                }
            }
            Stretch(fine);
            Stretch(coarse);
            var pixels = new Color[NoiseSize * NoiseSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(fine[i], coarse[i], 0.0f, 1.0f);
            }
            return pixels;
        }

        /// <summary>Fractal noise that tiles over 0..1: each octave a gradient noise whose lattice wraps at its own cell count.</summary>
        public static float Fbm(float u, float v, int cells, int octaves, int[] permutation)
        {
            float sum = 0.0f;
            float amplitude = 1.0f;
            float total = 0.0f;
            int period = cells;
            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * Gradient(u * period, v * period, period, permutation);
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            return sum / total;
        }

        /// <summary>Perlin's gradient noise at (x, y), its lattice repeating every <paramref name="period"/> cells.</summary>
        private static float Gradient(float x, float y, int period, int[] permutation)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            int x0 = Wrap(xi, period), x1 = Wrap(xi + 1, period);
            int y0 = Wrap(yi, period), y1 = Wrap(yi + 1, period);
            float a = Dot(permutation, x0, y0, fx, fy);
            float b = Dot(permutation, x1, y0, fx - 1.0f, fy);
            float c = Dot(permutation, x0, y1, fx, fy - 1.0f);
            float d = Dot(permutation, x1, y1, fx - 1.0f, fy - 1.0f);
            float sx = fx * fx * fx * (fx * (fx * 6.0f - 15.0f) + 10.0f);
            float sy = fy * fy * fy * (fy * (fy * 6.0f - 15.0f) + 10.0f);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private static float Dot(int[] permutation, int x, int y, float dx, float dy)
        {
            Vector2 gradient = Gradients[permutation[(permutation[x & 255] + y) & 255]];
            return gradient.x * dx + gradient.y * dy;
        }

        /// <summary>The 256 directions a lattice point's gradient can take, worked out once (a sine and a cosine per lookup were most of the noise's cost).</summary>
        private static readonly Vector2[] Gradients = MakeGradients();

        private static Vector2[] MakeGradients()
        {
            var gradients = new Vector2[256];
            for (int hash = 0; hash < 256; hash++)
            {
                float angle = hash * (Mathf.PI * 2.0f / 256.0f);
                gradients[hash] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return gradients;
        }

        private static int Wrap(int value, int period)
        {
            int wrapped = value % period;
            return wrapped < 0 ? wrapped + period : wrapped;
        }

        private static int[] Permutation(int seed)
        {
            var random = new System.Random(seed);
            var table = new int[256];
            for (int i = 0; i < 256; i++)
            {
                table[i] = i;
            }
            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (table[i], table[j]) = (table[j], table[i]);
            }
            return table;
        }

        /// <summary>Stretches the values to fill 0..1, so the shader's thresholds mean the same whatever the noise's own range.</summary>
        private static void Stretch(float[] values)
        {
            float low = float.MaxValue, high = float.MinValue;
            foreach (float value in values)
            {
                low = Mathf.Min(low, value);
                high = Mathf.Max(high, value);
            }
            float span = Mathf.Max(1e-5f, high - low);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (values[i] - low) / span;
            }
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
        /// URP's particle shader, see-through and SOFT (it fades where it meets the street instead of cutting into it): additive for the
        /// fire's own light (the glow, the embers), alpha-blended for the jerry can's stream. The keywords are the ones Self Smite's glow asset
        /// carries, so a build has the variants. <paramref name="brightness"/> above 1 lets the game's bloom catch it.
        /// </summary>
        public static Material Particles(string name, Texture2D texture, ParticleBlend blend, int queueOffset, float brightness = 1.0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = name, color = new Color(brightness, brightness, brightness, 1.0f),
            };
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            if (!material.HasProperty("_Surface"))
            {
                return material;
            }
            bool additive = blend == ParticleBlend.Additive;
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", additive ? 2.0f : 0.0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
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
    }
}
