using System;
using UnityEngine;

namespace CityX.MapImport
{
    /// <summary>
    /// Draws tiling surface textures from scratch.
    ///
    /// Every pixel here is computed, not sampled from anything. That is the
    /// point: the map's material ASSIGNMENTS are the mapper's work and worth
    /// keeping, but the images belong to Valve. So this reads "this wall is
    /// brick" and paints its own bricks.
    ///
    /// These will read as clean and slightly stylised rather than photoreal,
    /// which is honest about what procedural generation gives you cheaply --
    /// and is arguably the right register for a game whose art direction is
    /// still being decided.
    ///
    /// EVERYTHING MUST TILE. A texture with a visible seam is worse than a
    /// flat colour, because the eye locks onto the repeat instantly. All the
    /// noise below wraps, and every pattern uses modular arithmetic so the
    /// right edge continues into the left.
    /// </summary>
    public static class ProceduralTextureForge
    {
        public const int Size = 512;

        public static Texture2D Create(MaterialFamily family, string materialName)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[Size * Size];
            int seed = StableSeed(materialName);

            switch (family)
            {
                case MaterialFamily.Brick:
                    PaintBrick(pixels, seed, materialName);
                    break;
                case MaterialFamily.Concrete:
                    PaintConcrete(pixels, seed, materialName);
                    break;
                case MaterialFamily.MetalWall:
                    PaintMetalPanel(pixels, seed);
                    break;
                case MaterialFamily.MetalFloor:
                    PaintMetalTread(pixels, seed);
                    break;
                case MaterialFamily.MetalGrate:
                    PaintGrate(pixels, seed);
                    break;
                case MaterialFamily.Wood:
                case MaterialFamily.WoodPlank:
                    PaintWood(pixels, seed, family == MaterialFamily.Wood);
                    break;
                case MaterialFamily.Stone:
                    PaintStone(pixels, seed);
                    break;
                case MaterialFamily.Glass:
                    PaintGlass(pixels, seed);
                    break;
                case MaterialFamily.GrassDirt:
                    PaintGrassDirt(pixels, seed);
                    break;
                case MaterialFamily.DevGrid:
                    PaintDevGrid(pixels);
                    break;
                default:
                    PaintConcrete(pixels, seed, materialName);
                    break;
            }

            tex.SetPixels32(pixels);
            tex.Apply(true);
            return tex;
        }

        // --- surfaces ---

        private static void PaintBrick(Color32[] px, int seed, string materialName)
        {
            const int Rows = 16;
            int brickH = Size / Rows;
            int brickW = brickH * 2;
            const int Mortar = 3;

            Color baseColour = MaterialFamilyClassifier.MentionsBeige(materialName)
                ? new Color(0.74f, 0.68f, 0.56f)
                : new Color(0.52f, 0.28f, 0.22f);
            Color mortarColour = new Color(0.70f, 0.69f, 0.66f);

            for (int y = 0; y < Size; y++)
            {
                int row = y / brickH;
                // Running bond: every other row offset by half a brick. The
                // offset wraps with the texture, so the pattern is continuous
                // across the seam.
                int offset = (row % 2) * (brickW / 2);

                for (int x = 0; x < Size; x++)
                {
                    int bx = Mod(x + offset, brickW);
                    int by = y % brickH;

                    bool isMortar = bx < Mortar || by < Mortar;
                    Color c;
                    if (isMortar)
                    {
                        c = mortarColour * (0.92f + Noise(x, y, seed, 64) * 0.16f);
                    }
                    else
                    {
                        // One stable jitter per brick, so individual bricks
                        // differ rather than the whole wall being flat.
                        int brickId = (row * 7919) ^ (((x + offset) / brickW) * 104729) ^ seed;
                        float jitter = 0.82f + Hash01(brickId) * 0.36f;
                        float grain = 0.94f + Noise(x, y, seed + 1, 24) * 0.12f;
                        c = baseColour * jitter * grain;
                    }
                    px[y * Size + x] = ToColor32(c);
                }
            }
        }

        private static void PaintConcrete(Color32[] px, int seed, string materialName)
        {
            Color baseColour = MaterialFamilyClassifier.MentionsBlue(materialName)
                ? new Color(0.46f, 0.52f, 0.60f)
                : new Color(0.60f, 0.60f, 0.58f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Layered wrapping noise: broad blotches, medium mottling,
                    // fine grain. Concrete with only one frequency reads as
                    // television static.
                    float broad = Noise(x, y, seed, 8);
                    float medium = Noise(x, y, seed + 1, 32);
                    float fine = Noise(x, y, seed + 2, 128);
                    float value = 0.86f + broad * 0.16f + medium * 0.08f + fine * 0.05f;

                    // Occasional darker staining, the thing that stops a
                    // concrete texture looking like flat grey paper.
                    float stain = Noise(x, y, seed + 3, 6);
                    if (stain > 0.72f)
                    {
                        value *= 0.88f;
                    }
                    px[y * Size + x] = ToColor32(baseColour * value);
                }
            }
        }

        private static void PaintMetalPanel(Color32[] px, int seed)
        {
            Color baseColour = new Color(0.46f, 0.48f, 0.51f);
            const int PanelSize = 128;
            const int Seam = 3;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int pxm = x % PanelSize;
                    int pym = y % PanelSize;
                    bool seam = pxm < Seam || pym < Seam;

                    // Vertical brushing: high-frequency across, smooth along.
                    float brush = 0.95f + Noise(x, y / 8, seed, 256) * 0.10f;
                    float value = brush;

                    if (seam)
                    {
                        value *= 0.72f;
                    }

                    // Rivets near the panel corners.
                    int rx = Math.Min(pxm, PanelSize - pxm);
                    int ry = Math.Min(pym, PanelSize - pym);
                    if (rx > 6 && rx < 14 && ry > 6 && ry < 14)
                    {
                        value *= 1.22f;
                    }

                    px[y * Size + x] = ToColor32(baseColour * value);
                }
            }
        }

        private static void PaintMetalTread(Color32[] px, int seed)
        {
            Color baseColour = new Color(0.42f, 0.43f, 0.45f);
            const int Cell = 64;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float value = 0.94f + Noise(x, y, seed, 96) * 0.12f;

                    // Diamond tread plate: two diagonal bars per cell.
                    int cx = x % Cell;
                    int cy = y % Cell;
                    bool bar = Mod(cx + cy, Cell) < 10 || Mod(cx - cy, Cell) < 10;
                    if (bar)
                    {
                        value *= 1.28f;
                    }
                    px[y * Size + x] = ToColor32(baseColour * value);
                }
            }
        }

        private static void PaintGrate(Color32[] px, int seed)
        {
            Color barColour = new Color(0.38f, 0.39f, 0.41f);
            const int Cell = 32;
            const int Bar = 7;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool isBar = (x % Cell) < Bar || (y % Cell) < Bar;
                    if (isBar)
                    {
                        float value = 0.92f + Noise(x, y, seed, 64) * 0.16f;
                        px[y * Size + x] = ToColor32(barColour * value);
                    }
                    else
                    {
                        // Genuinely transparent holes, so you can see through
                        // a catwalk the way you could in the original.
                        px[y * Size + x] = new Color32(20, 20, 22, 0);
                    }
                }
            }
        }

        private static void PaintWood(Color32[] px, int seed, bool crate)
        {
            Color baseColour = crate
                ? new Color(0.62f, 0.45f, 0.26f)
                : new Color(0.48f, 0.35f, 0.22f);
            int plankH = crate ? 64 : 42;
            const int Gap = 3;

            for (int y = 0; y < Size; y++)
            {
                int plank = y / plankH;
                bool gap = (y % plankH) < Gap;

                for (int x = 0; x < Size; x++)
                {
                    float value;
                    if (gap)
                    {
                        value = 0.55f;
                    }
                    else
                    {
                        // Grain runs along the plank: stretched heavily in x
                        // so streaks read as wood fibre rather than noise.
                        float grain = Noise(x / 6, y * 3, seed + plank, 128);
                        float plankTone = 0.85f + Hash01(plank * 6151 + seed) * 0.28f;
                        value = plankTone * (0.90f + grain * 0.20f);
                    }
                    px[y * Size + x] = ToColor32(baseColour * value);
                }
            }
        }

        private static void PaintStone(Color32[] px, int seed)
        {
            Color baseColour = new Color(0.60f, 0.58f, 0.53f);
            const int BlockH = 64;
            const int BlockW = 128;
            const int Mortar = 4;

            for (int y = 0; y < Size; y++)
            {
                int row = y / BlockH;
                int offset = (row % 2) * (BlockW / 3);
                for (int x = 0; x < Size; x++)
                {
                    int bx = Mod(x + offset, BlockW);
                    int by = y % BlockH;
                    bool mortar = bx < Mortar || by < Mortar;

                    float value;
                    if (mortar)
                    {
                        value = 0.74f;
                    }
                    else
                    {
                        int blockId = (row * 3079) ^ (((x + offset) / BlockW) * 8191) ^ seed;
                        value = (0.88f + Hash01(blockId) * 0.22f) * (0.93f + Noise(x, y, seed, 48) * 0.14f);
                    }
                    px[y * Size + x] = ToColor32(baseColour * value);
                }
            }
        }

        private static void PaintGlass(Color32[] px, int seed)
        {
            Color tint = new Color(0.72f, 0.82f, 0.86f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float streak = 0.96f + Noise(x / 3, y, seed, 64) * 0.08f;
                    Color c = tint * streak;
                    Color32 c32 = ToColor32(c);
                    c32.a = 70;   // mostly transparent
                    px[y * Size + x] = c32;
                }
            }
        }

        private static void PaintGrassDirt(Color32[] px, int seed)
        {
            Color grass = new Color(0.30f, 0.42f, 0.20f);
            Color dirt = new Color(0.42f, 0.33f, 0.22f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float patch = Noise(x, y, seed, 10);
                    float detail = Noise(x, y, seed + 1, 90);
                    Color c = Color.Lerp(dirt, grass, Mathf.Clamp01(patch * 1.4f - 0.1f));
                    c *= 0.90f + detail * 0.20f;
                    px[y * Size + x] = ToColor32(c);
                }
            }
        }

        private static void PaintDevGrid(Color32[] px)
        {
            // The classic measurement grid. A grid of squares with rulings is
            // a generic dev-texture idea rather than anyone's artwork, and
            // keeping it visually distinct is genuinely useful: it marks the
            // surfaces the mapper had not finished dressing.
            Color a = new Color(0.78f, 0.42f, 0.12f);
            Color b = new Color(0.30f, 0.30f, 0.32f);
            const int Cell = 64;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool checker = ((x / Cell) + (y / Cell)) % 2 == 0;
                    Color c = checker ? a : b;
                    if (x % Cell < 2 || y % Cell < 2)
                    {
                        c *= 0.65f;
                    }
                    px[y * Size + x] = ToColor32(c);
                }
            }
        }

        // --- helpers ---

        /// <summary>
        /// Value noise that WRAPS. Lattice coordinates are taken modulo the
        /// cell count, so the pattern is continuous across the texture edge.
        /// Non-wrapping noise is the usual reason a "seamless" texture has a
        /// visible line down it.
        /// </summary>
        private static float Noise(int x, int y, int seed, int cells)
        {
            float fx = x / (float)Size * cells;
            float fy = y / (float)Size * cells;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            float v00 = Hash01(Mod(x0, cells) * 73856093 ^ Mod(y0, cells) * 19349663 ^ seed);
            float v10 = Hash01(Mod(x0 + 1, cells) * 73856093 ^ Mod(y0, cells) * 19349663 ^ seed);
            float v01 = Hash01(Mod(x0, cells) * 73856093 ^ Mod(y0 + 1, cells) * 19349663 ^ seed);
            float v11 = Hash01(Mod(x0 + 1, cells) * 73856093 ^ Mod(y0 + 1, cells) * 19349663 ^ seed);

            tx = tx * tx * (3.0f - 2.0f * tx);
            ty = ty * ty * (3.0f - 2.0f * ty);
            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint h = (uint)value;
                h ^= h >> 16;
                h *= 0x7feb352dU;
                h ^= h >> 15;
                h *= 0x846ca68bU;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0xffffff;
            }
        }

        private static int StableSeed(string name)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in name)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }

        private static int Mod(int value, int modulus)
        {
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }

        private static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)(Mathf.Clamp01(c.r) * 255),
                (byte)(Mathf.Clamp01(c.g) * 255),
                (byte)(Mathf.Clamp01(c.b) * 255),
                255);
        }
    }
}
