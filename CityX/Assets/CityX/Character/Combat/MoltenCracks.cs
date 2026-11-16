using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// MOLTEN CRACKS (docs/FIRE.md, "The fire spells"): what a fireball's burst burns into the surface it hits, laid over its black scorch --
    /// cracks running out from where it struck, glowing white-hot, then yellow, orange and red as they cool over a few seconds, and gone. The
    /// reference's ground fissures (achrefelouafi's LinearAbiltyCastingThreeJS, MIT: GroundFissures, a white-hot core in a wide orange
    /// underglow, arms that meander out and shed branches) painted into a picture once, instead of grown as ribbons over the floor: a
    /// fireball's cracks are a metre across, not a crater's five. The painting and the cooling are pure; <see cref="CoolingCracks"/> shows them.
    /// </summary>
    public static class MoltenCracks
    {
        public const int TextureSize = 128;
        public const int Variants = 3;

        /// <summary>
        /// Main cracks from the middle, how far each runs (a share of the disc), how hard it jags each step and how far it bends round in a
        /// slow curl, radians a unit walked. Barely veering (1.8 a unit, no curl), the six arms were straight spokes: a star, not cracks
        /// (2026-11-15).
        /// </summary>
        public const int Arms = 6;

        public const float ShortestArm = 0.5f;
        public const float LongestArm = 0.86f;
        public const float Wander = 9.0f;
        public const float Curl = 5.0f;

        /// <summary>The chance at each step of an arm that a branch splits off it, and how long a branch runs against what is left of its arm.</summary>
        public const float BranchChance = 0.07f;

        public const float BranchShare = 0.45f;

        /// <summary>A crack's width where it starts, a share of the disc, narrowing to a third at its tip; its glow spreads this many widths.</summary>
        public const float Width = 0.045f;

        public const float GlowWidths = 3.5f;

        /// <summary>Nothing is painted past this share of the disc, so the quad's square edge never shows.</summary>
        public const float Reach = 0.94f;

        private const float StepLength = 0.03f;

        /// <summary>How long they take to cool to nothing, seconds, and the heat they start with (white-hot).</summary>
        public const float CoolSeconds = 4.5f;

        /// <summary>The underglow round a crack, before the cooling tints it: an orange that goes red as it cools.</summary>
        public static readonly Color GlowColour = new Color(1.0f, 0.5f, 0.15f);

        /// <summary>Their brightness at their hottest, past 1 so the bloom catches them.</summary>
        public const float Brightness = 2.2f;

        private static Texture2D[] _textures;

        /// <summary>How hot the cracks are <paramref name="seconds"/> after the burst, 1 down to 0 (fast at first, then slowly).</summary>
        public static float HeatAt(float seconds) => Mathf.Pow(1.0f - Mathf.Clamp01(seconds / CoolSeconds), 1.4f);

        /// <summary>The cracks' tint then: the fire spells' band of that heat (SpellFire.Band), bright while hot, dim as it cools.</summary>
        public static Color TintAt(float seconds)
        {
            float heat = HeatAt(seconds);
            Color band = SpellFire.Band(heat);
            float bright = Mathf.Lerp(0.35f, Brightness, heat);
            return new Color(band.r * bright, band.g * bright, band.b * bright, band.a);
        }

        /// <summary>The cracks' pictures, made once (a few milliseconds): white-hot cracks in an orange glow, alpha their strength.</summary>
        public static Texture2D Texture(int variant)
        {
            if (_textures == null)
            {
                _textures = new Texture2D[Variants];
                for (int v = 0; v < Variants; v++)
                {
                    var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
                    {
                        name = "MoltenCracks" + v, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
                    };
                    texture.SetPixels(Paint(TextureSize, 811 + v * 97));
                    texture.Apply(false, true);
                    _textures[v] = texture;
                }
            }
            return _textures[Mathf.Abs(variant) % Variants];
        }

        /// <summary>
        /// The cracks painted on a <paramref name="size"/>-square picture from <paramref name="seed"/>: rgb white in a crack, orange in its glow,
        /// alpha how much shows.
        /// </summary>
        public static Color[] Paint(int size, int seed)
        {
            var core = new float[size * size];
            var glow = new float[size * size];
            var random = new System.Random(seed);
            for (int arm = 0; arm < Arms; arm++)
            {
                float angle = (arm + (float)random.NextDouble() * 0.6f) * Mathf.PI * 2.0f / Arms;
                float length = Mathf.Lerp(ShortestArm, LongestArm, (float)random.NextDouble());
                Walk(core, glow, size, random, new Vector2(0.0f, 0.0f), angle, length, Width, true);
            }
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float c = Mathf.Clamp01(core[i]);
                float g = Mathf.Clamp01(glow[i]);
                Color colour = Color.Lerp(GlowColour, Color.white, c);
                colour.a = Mathf.Max(c, g * 0.45f);
                pixels[i] = colour;
            }
            return pixels;
        }

        /// <summary>One crack: a walk from <paramref name="from"/> (the disc's -1..1) that veers as it goes, painted as it walks, shedding branches.</summary>
        private static void Walk(float[] core, float[] glow, int size, System.Random random, Vector2 from, float angle, float length, float width, bool branches)
        {
            Vector2 at = from;
            float walked = 0.0f;
            float bend = ((float)random.NextDouble() - 0.5f) * 2.0f * Curl;
            while (walked < length)
            {
                bend = Mathf.Lerp(bend, ((float)random.NextDouble() - 0.5f) * 2.0f * Curl, 0.15f);
                angle += (bend + ((float)random.NextDouble() - 0.5f) * 2.0f * Wander) * StepLength;
                Vector2 next = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * StepLength;
                if (next.magnitude > Reach)
                {
                    break;
                }
                float taper = Mathf.Lerp(1.0f, 0.33f, walked / length);
                Segment(core, glow, size, at, next, width * taper);
                at = next;
                walked += StepLength;
                if (branches && random.NextDouble() < BranchChance)
                {
                    float side = random.NextDouble() < 0.5 ? -1.0f : 1.0f;
                    float turn = Mathf.Lerp(0.5f, 1.1f, (float)random.NextDouble()) * side;
                    Walk(core, glow, size, random, at, angle + turn, (length - walked) * BranchShare + StepLength, width * taper * 0.7f, false);
                }
            }
        }

        /// <summary>Paints one short stretch of crack: solid within half its width, its glow falling off over <see cref="GlowWidths"/>.</summary>
        private static void Segment(float[] core, float[] glow, int size, Vector2 a, Vector2 b, float width)
        {
            float reach = width * GlowWidths;
            float half = size * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach) * half + half));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach) * half + half));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach) * half + half));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach) * half + half));
            Vector2 ab = b - a;
            float lengthSquared = Mathf.Max(1e-8f, ab.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f - half) / half, (y + 0.5f - half) / half);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                    float d = Vector2.Distance(p, a + ab * t);
                    int i = y * size + x;
                    float solid = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(width * 0.35f, width * 0.6f, d));
                    core[i] = Mathf.Max(core[i], solid);
                    float fall = d / reach;
                    glow[i] = Mathf.Max(glow[i], Mathf.Exp(-fall * fall * 3.0f));
                }
            }
        }
    }

    /// <summary>Cracks laid on a scorch, cooling: tinted by <see cref="MoltenCracks.TintAt"/> each frame until they are cold, then hidden.</summary>
    public sealed class CoolingCracks : MonoBehaviour
    {
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _age;

        public float Age => _age;

        public bool Glowing => _renderer != null && _renderer.enabled;

        public void Begin(MeshRenderer renderer, Texture2D texture)
        {
            _renderer = renderer;
            _block = new MaterialPropertyBlock();
            _block.SetTexture("_BaseMap", texture);
            _block.SetTexture("_MainTex", texture);
            Tint();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= MoltenCracks.CoolSeconds)
            {
                _renderer.enabled = false;
                enabled = false;
                return;
            }
            Tint();
        }

        private void Tint()
        {
            _block.SetColor(BaseColour, MoltenCracks.TintAt(_age));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
