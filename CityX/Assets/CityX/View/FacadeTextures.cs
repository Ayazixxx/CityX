using System;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The eight wall materials of the city, drawn procedurally: poured concrete, brick, plaster, metal cladding, stone, ceramic tile, a
    /// glazed spandrel panel and a roof membrane. Each is a square, seamlessly tiling tile that stands for a real size in metres
    /// (<see cref="TileMetresX"/>, <see cref="TileMetresY"/>), so joints, bricks and seams are the size they are on a real building
    /// wherever the shader puts them.
    ///
    /// PROCEDURAL ON PURPOSE (standing practice: keep assets small). The builder (View/Editor/FacadeTextureBuilder) bakes the result to
    /// two PNG atlases once; they are plain images, so a hand-made set can simply replace them -- the importer turns the 4x2 grid of
    /// 512 px tiles into a texture array whatever drew it. Slice order is the order of <see cref="SliceNames"/>, reading left to
    /// right, top to bottom.
    ///
    /// A pixel is shaded from four things the same way for every material: albedo, smoothness, and a relief height in MILLIMETRES, from
    /// which the normal map is derived. Everything is built from periodic noise and wrapped distances, so a tile meets itself.
    /// </summary>
    public static class FacadeTextures
    {
        public const int Size = 512;
        public const int Columns = 4;
        public const int Rows = 2;

        public static readonly string[] SliceNames =
            { "concrete", "brick", "plaster", "metal", "stone", "tile", "spandrel", "roof" };

        /// <summary>What a tile covers, in metres, across and up. Brick is not square: nine bricks across, twenty-eight courses up.</summary>
        public static readonly float[] TileMetresX = { 4.0f, 2.025f, 3.0f, 2.0f, 4.0f, 1.6f, 3.2f, 3.0f };

        public static readonly float[] TileMetresY = { 4.0f, 2.1f, 3.0f, 2.0f, 4.0f, 1.6f, 3.2f, 3.0f };

        /// <summary>How strongly relief becomes slope in the normal map, per slice.</summary>
        private static readonly float[] NormalStrength = { 1.0f, 1.3f, 0.8f, 1.1f, 0.9f, 1.0f, 0.6f, 1.2f };

        /// <summary>One drawn tile: colour and smoothness per pixel, and relief height in millimetres.</summary>
        public sealed class Tile
        {
            public readonly int Size;
            public readonly float[] R, G, B, Smooth, Height;

            public Tile(int size)
            {
                Size = size;
                R = new float[size * size];
                G = new float[size * size];
                B = new float[size * size];
                Smooth = new float[size * size];
                Height = new float[size * size];
            }
        }

        public static Tile Generate(int slice, int size = Size)
        {
            var tile = new Tile(size);
            float tx = TileMetresX[slice];
            float ty = TileMetresY[slice];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float r, g, b, s, h;
                    switch (slice)
                    {
                        case 0: Concrete(u, v, x, y, tx, ty, out r, out g, out b, out s, out h); break;
                        case 1: Brick(u, v, x, y, out r, out g, out b, out s, out h); break;
                        case 2: Plaster(u, v, x, y, out r, out g, out b, out s, out h); break;
                        case 3: Metal(u, v, x, y, tx, ty, out r, out g, out b, out s, out h); break;
                        case 4: Stone(u, v, x, y, tx, ty, out r, out g, out b, out s, out h); break;
                        case 5: Ceramic(u, v, x, y, tx, ty, out r, out g, out b, out s, out h); break;
                        case 6: Spandrel(u, v, x, y, tx, ty, out r, out g, out b, out s, out h); break;
                        default: Roof(u, v, x, y, tx, ty, out r, out g, out b, out s, out h); break;
                    }
                    int i = y * size + x;
                    tile.R[i] = Mathf.Clamp01(r);
                    tile.G[i] = Mathf.Clamp01(g);
                    tile.B[i] = Mathf.Clamp01(b);
                    tile.Smooth[i] = Mathf.Clamp01(s);
                    tile.Height[i] = h;
                }
            }
            return tile;
        }

        /// <summary>The tangent-space normal of a tile at one pixel, from its relief, wrapping at the edges.</summary>
        public static Vector3 NormalAt(Tile tile, int slice, int x, int y)
        {
            int n = tile.Size;
            float mmPerPixelX = TileMetresX[slice] * 1000.0f / n;
            float mmPerPixelY = TileMetresY[slice] * 1000.0f / n;
            float hl = tile.Height[y * n + (x + n - 1) % n];
            float hr = tile.Height[y * n + (x + 1) % n];
            float hd = tile.Height[((y + n - 1) % n) * n + x];
            float hu = tile.Height[((y + 1) % n) * n + x];
            float dx = (hr - hl) / (2.0f * mmPerPixelX) * NormalStrength[slice];
            float dy = (hu - hd) / (2.0f * mmPerPixelY) * NormalStrength[slice];
            return new Vector3(-dx, -dy, 1.0f).normalized;
        }

        /// <summary>
        /// All eight tiles as two atlases, <see cref="Columns"/> x <see cref="Rows"/> tiles: albedo (rgb) with smoothness in alpha, and
        /// the tangent-space normal. Slice 0 is the top left tile and they run left to right, top to bottom, which is the order the
        /// texture importer's array layout reads them in. Texture rows run bottom to top, so the top row of tiles is the last rows.
        /// </summary>
        public static void BuildAtlases(int size, out Color32[] albedo, out Color32[] normal)
        {
            int width = size * Columns;
            int height = size * Rows;
            albedo = new Color32[width * height];
            normal = new Color32[width * height];
            for (int slice = 0; slice < Columns * Rows; slice++)
            {
                Tile tile = Generate(slice, size);
                int column = slice % Columns;
                int rowFromTop = slice / Columns;
                int originX = column * size;
                int originY = (Rows - 1 - rowFromTop) * size;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        int o = (originY + y) * width + originX + x;
                        albedo[o] = new Color32(ToByte(tile.R[i]), ToByte(tile.G[i]), ToByte(tile.B[i]), ToByte(tile.Smooth[i]));
                        Vector3 n = NormalAt(tile, slice, x, y);
                        normal[o] = new Color32(ToByte(n.x * 0.5f + 0.5f), ToByte(n.y * 0.5f + 0.5f), ToByte(n.z * 0.5f + 0.5f), 255);
                    }
                }
            }
        }

        private static byte ToByte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255.0f), 0, 255);
        }

        // =====================================================================================================================
        //  The eight materials. u, v are 0..1 across the tile; tx, ty are the tile's size in metres.
        // =====================================================================================================================

        /// <summary>Poured concrete: mottled, board-marked, with formwork joints every metre across and two up, and tie holes.</summary>
        private static void Concrete(float u, float v, int px, int py, float tx, float ty,
            out float r, out float g, out float b, out float s, out float h)
        {
            float mott = Fbm(u, v, 4, 4, 5, 11);
            float grain = Hash(px, py, 3);
            float streak = Fbm(u, v, 28, 3, 3, 23);
            float tone = 0.50f + (mott - 0.5f) * 0.20f + (grain - 0.5f) * 0.06f - Mathf.Max(0.0f, streak - 0.55f) * 0.16f;
            r = tone * 1.00f;
            g = tone * 0.99f;
            b = tone * 0.96f;
            s = 0.16f + (mott - 0.5f) * 0.10f;
            h = (grain - 0.5f) * 0.6f + (mott - 0.5f) * 1.5f;

            // Formwork joints: vertical every 1 m, horizontal every 2 m, each a groove a centimetre wide.
            float dxm = EdgeDist(u * tx) * 1.0f;                       // metres from the nearest 1 m line
            float dym = EdgeDist(v * ty * 0.5f) * 2.0f;                // metres from the nearest 2 m line
            float groove = 1.0f - Smooth(0.0035f, 0.0095f, Mathf.Min(dxm, dym));
            r *= 1.0f - 0.38f * groove;
            g *= 1.0f - 0.38f * groove;
            b *= 1.0f - 0.38f * groove;
            h -= 6.0f * groove;

            // Tie holes: two across each metre panel, three up each two-metre panel.
            float xm = Mathf.Repeat(u * tx, 0.5f) - 0.25f;
            float ym = Mathf.Repeat(v * ty, 2.0f);
            float dy = Mathf.Min(Mathf.Abs(ym - 0.4f), Mathf.Min(Mathf.Abs(ym - 1.0f), Mathf.Abs(ym - 1.6f)));
            float hole = 1.0f - Smooth(0.011f, 0.017f, Mathf.Sqrt(xm * xm + dy * dy));
            r *= 1.0f - 0.55f * hole;
            g *= 1.0f - 0.55f * hole;
            b *= 1.0f - 0.55f * hole;
            h -= 9.0f * hole;

            // Air pits.
            float pit = Dots(u, v, 110, 0.20f, 0.30f, 31);
            r *= 1.0f - 0.35f * pit;
            g *= 1.0f - 0.35f * pit;
            b *= 1.0f - 0.35f * pit;
            h -= 2.0f * pit;
        }

        /// <summary>Running-bond brick, 215 x 65 mm with a 10 mm joint: nine bricks across, twenty-eight courses up.</summary>
        private static void Brick(float u, float v, int px, int py,
            out float r, out float g, out float b, out float s, out float h)
        {
            const int bricksAcross = 9;
            const int courses = 28;
            float cv = v * courses;
            int course = Mathf.FloorToInt(cv);
            float fv = cv - course;
            float uu = u * bricksAcross + ((course & 1) != 0 ? 0.5f : 0.0f);
            int brick = FloorMod(Mathf.FloorToInt(uu), bricksAcross);
            float fu = uu - Mathf.Floor(uu);

            float dx = Mathf.Min(fu, 1.0f - fu) * 0.225f;     // metres to the nearest vertical joint line
            float dy = Mathf.Min(fv, 1.0f - fv) * 0.075f;     // metres to the nearest bed joint line
            float d = Mathf.Min(dx, dy);
            float mortar = 1.0f - Smooth(0.0042f, 0.0062f, d);

            float hb = Hash(course, brick, 41);
            float hb2 = Hash(course, brick, 77);
            float grain = Hash(px, py, 5);
            float mott = Fbm(u, v, 3, 3, 6, 17);

            float fire = hb > 0.90f ? 0.70f : (hb < 0.12f ? 1.12f : 1.0f);          // a few burnt and a few pale bricks
            float br = Mathf.Lerp(0.45f, 0.58f, hb) * fire;
            float bg = Mathf.Lerp(0.23f, 0.30f, hb) * fire;
            float bb = Mathf.Lerp(0.18f, 0.23f, hb) * fire;
            float shade = 0.94f + 0.10f * hb2 + (grain - 0.5f) * 0.10f + (mott - 0.5f) * 0.10f;
            br *= shade;
            bg *= shade;
            bb *= shade;

            float mortarTone = 0.60f + (grain - 0.5f) * 0.10f + (mott - 0.5f) * 0.08f;
            r = Mathf.Lerp(br, mortarTone, mortar);
            g = Mathf.Lerp(bg, mortarTone * 0.97f, mortar);
            b = Mathf.Lerp(bb, mortarTone * 0.92f, mortar);
            s = Mathf.Lerp(0.14f, 0.07f, mortar) + (hb2 - 0.5f) * 0.04f;
            h = Mathf.Lerp(0.0f, -5.0f, mortar) + (grain - 0.5f) * 0.8f + Smooth(0.0045f, 0.012f, d) * 1.2f;
        }

        /// <summary>Painted render: fine sand grain, soft trowel mottling, and hairline cracks in patches.</summary>
        private static void Plaster(float u, float v, int px, int py,
            out float r, out float g, out float b, out float s, out float h)
        {
            float mott = Fbm(u, v, 3, 3, 5, 13);
            float trowel = Fbm(u, v, 8, 8, 3, 29);
            float grain = Hash(px, py, 9);
            float tone = 0.74f + (mott - 0.5f) * 0.10f + (trowel - 0.5f) * 0.05f + (grain - 0.5f) * 0.05f;

            // Cracks: thin ridges of a noise field, only where a second, slower field is high.
            float ridge = 1.0f - Mathf.Abs(Fbm(u, v, 6, 6, 4, 61) * 2.0f - 1.0f);
            float patch = Smooth(0.58f, 0.72f, Fbm(u, v, 2, 2, 2, 67));
            float crack = Smooth(0.990f, 0.9985f, ridge) * patch;
            tone *= 1.0f - 0.12f * crack;

            r = tone;
            g = tone * 0.985f;
            b = tone * 0.93f;
            s = 0.13f + (trowel - 0.5f) * 0.05f;
            h = (grain - 0.5f) * 0.9f + (trowel - 0.5f) * 1.6f - 1.5f * crack;
        }

        /// <summary>Standing-seam metal cladding: 500 mm panels with raised seams, a joint every metre, faint dents and streaks.</summary>
        private static void Metal(float u, float v, int px, int py, float tx, float ty,
            out float r, out float g, out float b, out float s, out float h)
        {
            float dent = Fbm(u, v, 2, 2, 4, 19);
            float brushed = Fbm(u, v, 3, 90, 2, 37);
            float grain = Hash(px, py, 7);
            float tone = 0.46f + (dent - 0.5f) * 0.08f + (brushed - 0.5f) * 0.05f + (grain - 0.5f) * 0.02f;

            float seamDist = EdgeDist(u * tx / 0.5f) * 0.5f;                  // metres from a 500 mm seam
            float seam = 1.0f - Smooth(0.004f, 0.010f, seamDist);
            float jointDist = EdgeDist(v * ty / 1.0f) * 1.0f;                // metres from a 1 m horizontal joint
            float joint = 1.0f - Smooth(0.002f, 0.005f, jointDist);

            float rust = Smooth(0.84f, 0.95f, Fbm(u, v, 9, 9, 3, 53)) * 0.25f;
            r = tone * (1.0f + rust * 0.55f) * (1.0f - 0.25f * joint) + seam * 0.04f;
            g = tone * (1.0f + rust * 0.15f) * (1.0f - 0.25f * joint) + seam * 0.04f;
            b = tone * (1.0f - rust * 0.25f) * (1.0f - 0.25f * joint) + seam * 0.04f;
            s = 0.52f - rust * 0.3f + (brushed - 0.5f) * 0.1f - joint * 0.1f;
            h = seam * 4.0f - joint * 2.0f + (dent - 0.5f) * 1.2f;
        }

        /// <summary>Cut stone cladding in 1 m x 0.5 m blocks, each its own tone, speckled, with a 6 mm joint.</summary>
        private static void Stone(float u, float v, int px, int py, float tx, float ty,
            out float r, out float g, out float b, out float s, out float h)
        {
            const int across = 4;
            const int rowsUp = 8;
            float cv = v * rowsUp;
            int row = Mathf.FloorToInt(cv);
            float fv = cv - row;
            float uu = u * across + ((row & 1) != 0 ? 0.5f : 0.0f);
            int block = FloorMod(Mathf.FloorToInt(uu), across);
            float fu = uu - Mathf.Floor(uu);

            float dx = Mathf.Min(fu, 1.0f - fu) * (tx / across);
            float dy = Mathf.Min(fv, 1.0f - fv) * (ty / rowsUp);
            float chip = (Fbm(u, v, 40, 40, 2, 71) - 0.5f) * 0.0015f;
            float d = Mathf.Min(dx, dy) + chip;
            float joint = 1.0f - Smooth(0.0022f, 0.0050f, d);

            float hb = Hash(row, block, 83);
            float speck = Hash(px * 3 + 1, py * 3 + 2, 13);
            float speck2 = Hash(px / 2, py / 2, 17);
            float vein = Mathf.Abs(Fbm(u + hb, v, 5, 5, 3, 89) * 2.0f - 1.0f);
            float tone = Mathf.Lerp(0.52f, 0.64f, hb) + (speck - 0.5f) * 0.06f + (speck2 - 0.5f) * 0.04f - (1.0f - Smooth(0.0f, 0.06f, vein)) * 0.02f;
            r = tone * 1.0f;
            g = tone * 0.96f;
            b = tone * 0.90f;
            s = Mathf.Lerp(0.30f, 0.45f, hb2(hb)) + (speck - 0.5f) * 0.08f;
            r = Mathf.Lerp(r, 0.30f, joint);
            g = Mathf.Lerp(g, 0.29f, joint);
            b = Mathf.Lerp(b, 0.27f, joint);
            s = Mathf.Lerp(s, 0.08f, joint);
            h = -4.0f * joint + (speck - 0.5f) * 0.7f + Smooth(0.003f, 0.01f, d) * 0.8f;
        }

        private static float hb2(float hb)
        {
            return Mathf.Repeat(hb * 7.31f, 1.0f);
        }

        /// <summary>Small glazed ceramic tiles, 100 mm square with a 3 mm grout line: glossy, each a hair different, a few chipped.</summary>
        private static void Ceramic(float u, float v, int px, int py, float tx, float ty,
            out float r, out float g, out float b, out float s, out float h)
        {
            const int count = 16;
            float cu = u * count;
            float cv = v * count;
            int tu = FloorMod(Mathf.FloorToInt(cu), count);
            int tvv = FloorMod(Mathf.FloorToInt(cv), count);
            float fu = cu - Mathf.Floor(cu);
            float fv = cv - Mathf.Floor(cv);
            float d = Mathf.Min(Mathf.Min(fu, 1.0f - fu) * (tx / count), Mathf.Min(fv, 1.0f - fv) * (ty / count));
            float grout = 1.0f - Smooth(0.0010f, 0.0022f, d);

            float ht = Hash(tu, tvv, 91);
            float chipped = ht > 0.992f ? 1.0f : 0.0f;
            float tone = 0.86f + (ht - 0.5f) * 0.07f - chipped * 0.06f;
            float glaze = Fbm(u, v, 5, 5, 3, 97);
            tone += (glaze - 0.5f) * 0.03f;
            r = Mathf.Lerp(tone, 0.52f, grout);
            g = Mathf.Lerp(tone, 0.52f, grout);
            b = Mathf.Lerp(tone * 0.98f, 0.50f, grout);
            s = Mathf.Lerp(0.66f - chipped * 0.35f, 0.10f, grout) + (ht - 0.5f) * 0.05f;
            h = -2.5f * grout + (ht - 0.5f) * 0.3f - chipped * 0.6f;
        }

        /// <summary>A glazed spandrel panel: dark, glossy, in 1.6 m panels with thin aluminium joints.</summary>
        private static void Spandrel(float u, float v, int px, int py, float tx, float ty,
            out float r, out float g, out float b, out float s, out float h)
        {
            float haze = Fbm(u, v, 2, 2, 3, 101);
            float grain = Hash(px, py, 15);
            float tone = 0.22f + (haze - 0.5f) * 0.07f + (grain - 0.5f) * 0.012f;
            float dx = EdgeDist(u * tx / 1.6f) * 1.6f;
            float dy = EdgeDist(v * ty / 1.6f) * 1.6f;
            float seam = 1.0f - Smooth(0.006f, 0.014f, Mathf.Min(dx, dy));
            r = Mathf.Lerp(tone * 0.92f, 0.55f, seam);
            g = Mathf.Lerp(tone * 1.02f, 0.56f, seam);
            b = Mathf.Lerp(tone * 1.15f, 0.58f, seam);
            s = Mathf.Lerp(0.88f, 0.45f, seam);
            h = -1.5f * seam;
        }

        /// <summary>Roof: bitumen membrane with gravel grain, welded seams every metre, lighter patches and damp stains.</summary>
        private static void Roof(float u, float v, int px, int py, float tx, float ty,
            out float r, out float g, out float b, out float s, out float h)
        {
            float grain = Hash(px, py, 21);
            float coarse = Hash(px / 3, py / 3, 23);
            float damp = Fbm(u, v, 2, 2, 4, 107);
            float patchCell = Hash(Mathf.FloorToInt(u * tx / 0.5f), Mathf.FloorToInt(v * ty / 0.5f), 109);
            float patch = patchCell > 0.85f ? (patchCell - 0.85f) * 0.5f : 0.0f;
            float tone = 0.15f + (grain - 0.5f) * 0.05f + (coarse - 0.5f) * 0.03f + patch - (damp - 0.5f) * 0.02f;
            float speck = grain > 0.985f ? 0.18f : 0.0f;

            float seamX = EdgeDist(u * tx / 1.0f) * 1.0f;
            float seamY = EdgeDist(v * ty / 1.0f) * 1.0f;
            float seam = 1.0f - Smooth(0.004f, 0.010f, Mathf.Min(seamX, seamY));
            tone += seam * 0.05f + speck;
            r = tone * 1.0f;
            g = tone * 1.0f;
            b = tone * 1.04f;
            s = 0.16f + Smooth(0.60f, 0.80f, damp) * 0.15f;
            h = (grain - 0.5f) * 1.2f + coarse * 0.8f + seam * 1.5f;
        }

        // =====================================================================================================================
        //  Noise and shaping helpers. Everything wraps, so a tile meets itself.
        // =====================================================================================================================

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0f - 2.0f * t);
        }

        /// <summary>Distance from x to the nearest whole number (0 .. 0.5).</summary>
        private static float EdgeDist(float x)
        {
            float f = x - Mathf.Floor(x);
            return Mathf.Min(f, 1.0f - f);
        }

        private static int FloorMod(int a, int n)
        {
            int m = a % n;
            return m < 0 ? m + n : m;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFFu) / 16777216.0f;
            }
        }

        /// <summary>Value noise on a lattice of periodX x periodY cells that wraps.</summary>
        private static float ValueNoise(float u, float v, int periodX, int periodY, int seed)
        {
            float fx = u * periodX;
            float fy = v * periodY;
            int ix = Mathf.FloorToInt(fx);
            int iy = Mathf.FloorToInt(fy);
            float tx = fx - ix;
            float ty = fy - iy;
            tx = tx * tx * tx * (tx * (tx * 6.0f - 15.0f) + 10.0f);
            ty = ty * ty * ty * (ty * (ty * 6.0f - 15.0f) + 10.0f);
            float a = Hash(FloorMod(ix, periodX), FloorMod(iy, periodY), seed);
            float b = Hash(FloorMod(ix + 1, periodX), FloorMod(iy, periodY), seed);
            float c = Hash(FloorMod(ix, periodX), FloorMod(iy + 1, periodY), seed);
            float d = Hash(FloorMod(ix + 1, periodX), FloorMod(iy + 1, periodY), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>Fractal noise, 0..1, whose first octave has periodX x periodY cells and each later one twice as many.</summary>
        private static float Fbm(float u, float v, int periodX, int periodY, int octaves, int seed)
        {
            float sum = 0.0f;
            float amplitude = 0.5f;
            float total = 0.0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u, v, periodX << o, periodY << o, seed + o * 31) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
            }
            return sum / total;
        }

        /// <summary>
        /// Sparse round dots, 0..1 (1 inside a dot). The tile is divided into cells x cells; a share of the cells hold one dot of
        /// a radius a fraction of the cell, kept inside its own cell so nothing has to look at a neighbour.
        /// </summary>
        private static float Dots(float u, float v, int cells, float chance, float radiusFraction, int seed)
        {
            float fx = u * cells;
            float fy = v * cells;
            int ix = Mathf.FloorToInt(fx);
            int iy = Mathf.FloorToInt(fy);
            if (Hash(ix, iy, seed) > chance)
            {
                return 0.0f;
            }
            float radius = radiusFraction * (0.4f + 0.6f * Hash(ix, iy, seed + 1));
            float cx = radius + (1.0f - 2.0f * radius) * Hash(ix, iy, seed + 2);
            float cy = radius + (1.0f - 2.0f * radius) * Hash(ix, iy, seed + 3);
            float dx = fx - ix - cx;
            float dy = fy - iy - cy;
            return 1.0f - Smooth(radius * 0.6f, radius, Mathf.Sqrt(dx * dx + dy * dy));
        }
    }
}
