using System.IO;
using CityX.World;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Draws the city's concrete from scratch: a tiling facade, a road and a
    /// pavement, each with a matching normal map.
    ///
    /// WHY THE TILE IS THE SIZE IT IS. The mesh builder writes UVs in METRES,
    /// so a texture covering exactly FacadeTileMetres of wall lines up with the
    /// building's real floors and window columns everywhere, on every face, at
    /// any building size, with no per-building UV work. That only holds if the
    /// tile is a whole number of floors and columns -- so it is derived from
    /// CellLayout rather than picked, and building heights are snapped to whole
    /// floors so the top row is never sliced.
    ///
    /// WHY 4 FLOORS AND NOT 1. A one-floor tile puts a window in every single
    /// cell, which reads as a curtain-wall office block. The reference is the
    /// opposite -- sparse punched openings with long blank runs of concrete. A
    /// four-by-eight cell tile is big enough to hold a pattern with gaps in it,
    /// and 12.8m is far enough apart that the repeat does not announce itself.
    /// </summary>
    public static class CityFacadeTextures
    {
        private const string Directory_ = "Assets/CityX/View/Textures";

        /// <summary>
        /// Cells across the tile: 8 floors tall, 16 window columns wide.
        ///
        /// Deliberately square in METRES -- 8 floors of 3.2m and 16 columns of
        /// 1.6m are both 25.6m -- so one uniform tiling number works on every
        /// face regardless of which way the wall runs.
        ///
        /// Started at 4x8 (12.8m) and the repeat was visible across a big wall:
        /// you could see the same cluster of windows step across it. Doubling
        /// it costs a 2048px texture and buys both a longer repeat and enough
        /// room in the pattern for real blank runs of concrete.
        /// </summary>
        private const int TileFloors = 8;

        private const int TileColumns = 16;

        /// <summary>Pixels per metre.</summary>
        private const int PixelsPerMetre = 80;

        public static float FacadeTileHeight => TileFloors * CellLayout.NominalFloorMetres;

        public static float FacadeTileWidth => TileColumns * CellLayout.NominalColumnMetres;

        /// <summary>
        /// Base concrete lightness.
        ///
        /// Was 0.62, which under a bright sky rendered as near-white and made
        /// the city read as painted apartment blocks rather than as concrete.
        /// Poured concrete is a mid grey and wants to sit well below white
        /// before any light touches it.
        /// </summary>
        private const float ConcreteBase = 0.44f;

        [MenuItem("CityX/Rebuild City Textures")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Directory_);

            // The walls no longer use this tiling texture: they use the facade texture arrays (FacadeTextureBuilder). Not drawn any more, so
            // 11 MB of unused PNG is not rebuilt on every scene build; BuildFacade is kept for reference.
            // BuildFacade();

            // Slightly warm rather than neutral. A neutral dark grey lit only
            // by a blue sky comes out blue, and the first render had streets
            // that read as canal water.
            BuildGroundTexture("CityRoad", new Color(0.235f, 0.228f, 0.220f), 0.045f, 4.0f);
            BuildGroundTexture("CityPavement", new Color(0.46f, 0.455f, 0.445f), 0.070f, 1.2f);
            BuildGroundTexture("CityRoof", new Color(0.38f, 0.375f, 0.365f), 0.085f, 2.5f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"CityX: textures written to {Directory_}");
        }

        /// <summary>
        /// Poured concrete and the floor lines across it. NO WINDOWS.
        ///
        /// It used to paint them, and that was safe because it was the only
        /// place in the city anything window-shaped was drawn. Now the geometry
        /// cuts real openings on <see cref="CellLayout"/>'s grid, and this tile
        /// is anchored to world metres -- a 19.8m wall has twelve 1.65m columns
        /// and this tile has 1.6m ones. Two grids drawing windows on one wall
        /// is precisely what CellLayout's own comment records going wrong in
        /// the Godot build: black windows and transparent windows combined, and
        /// walls overlapping each other. One system draws windows, and it is no
        /// longer this one.
        ///
        /// What stays is what a wall still needs from a texture and cannot get
        /// from geometry: the concrete itself, and a floor line at every
        /// storey. That banding is what carries the brutalist read at distance,
        /// where individual windows are already too small to see.
        /// </summary>
        private static void BuildFacade()
        {
            int width = Mathf.RoundToInt(FacadeTileWidth * PixelsPerMetre);
            int height = Mathf.RoundToInt(FacadeTileHeight * PixelsPerMetre);

            var albedo = new Color[width * height];
            // Height is kept separately and converted to a normal map at the
            // end. Deriving normals from the albedo instead would turn every
            // dirt streak into a bump.
            var relief = new float[width * height];

            int cellHeight = height / TileFloors;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    float concrete = ConcreteTone(x, y, width, height);
                    albedo[index] = new Color(concrete, concrete, concrete * 0.99f);
                    relief[index] = 0.5f + (concrete - ConcreteBase) * 0.35f;

                    int inCellY = y - y / cellHeight * cellHeight;

                    // The slab edge: a dark line with a lighter lip above it.
                    if (inCellY < 3)
                    {
                        float shade = concrete * 0.72f;
                        albedo[index] = new Color(shade, shade, shade);
                        relief[index] = 0.18f;
                    }
                    else if (inCellY < 7)
                    {
                        float shade = Mathf.Min(1.0f, concrete * 1.12f);
                        albedo[index] = new Color(shade, shade, shade);
                        relief[index] = 0.86f;
                    }
                }
            }

            SaveTexture("CityFacade", width, height, albedo, srgb: true);
            SaveNormalMap("CityFacadeNormal", width, height, relief, strength: 3.0f);
        }

        /// <summary>
        /// Poured concrete: broad blotching from the pour, fine aggregate
        /// speckle, and vertical weather streaking. The streaks are what stop
        /// large blank walls reading as flat grey paint.
        /// </summary>
        private static float ConcreteTone(int x, int y, int width, int height)
        {
            float blotch = TilingNoise(x, y, width, height, 4) * 0.075f;
            float fine = TilingNoise(x, y, width, height, 32) * 0.045f;
            float speckle = (Hash01(x, y) - 0.5f) * 0.05f;

            // Streaks run down from each floor line and fade, so weathering
            // reads as coming off the slab edges rather than floating.
            float streakSeed = TilingNoise(x, 0, width, 1, 26);
            float downFromBand = (y % (height / TileFloors)) / (float)(height / TileFloors);
            float streak = -Mathf.Clamp01(streakSeed - 0.55f) * 0.55f * Mathf.Clamp01(1.0f - downFromBand * 1.6f);

            return Mathf.Clamp(ConcreteBase + blotch + fine + speckle + streak, 0.05f, 1.0f);
        }

        private static void BuildGroundTexture(string name, Color baseColour, float grain, float blotchScale)
        {
            const int size = 512;
            var albedo = new Color[size * size];
            var relief = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    float n = (TilingNoise(x, y, size, size, Mathf.RoundToInt(blotchScale * 4)) - 0.5f) * 2.0f;
                    float speckle = (Hash01(x + 91, y + 17) - 0.5f) * 2.0f;
                    float delta = n * grain + speckle * grain * 0.6f;

                    albedo[index] = new Color(
                        Mathf.Clamp01(baseColour.r + delta),
                        Mathf.Clamp01(baseColour.g + delta),
                        Mathf.Clamp01(baseColour.b + delta));
                    relief[index] = 0.5f + delta * 2.0f;
                }
            }

            SaveTexture(name, size, size, albedo, srgb: true);
            // Weak. Ground is nearly flat, and at facade strength the grain
            // turned the road into a rippling surface that read as water.
            SaveNormalMap(name + "Normal", size, size, relief, strength: 0.6f);
        }

        // --- noise ---

        /// <summary>
        /// Value noise that wraps at the tile edges.
        ///
        /// The wrap is the whole point and the easy thing to get wrong: a
        /// non-tiling noise leaves a visible seam every 12.8m across every
        /// building in the world, which looks like a texture bug rather than
        /// like concrete.
        /// </summary>
        private static float TilingNoise(int x, int y, int width, int height, int cells)
        {
            float fx = x / (float)width * cells;
            float fy = y / (float)height * cells;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = Smooth(fx - x0);
            float ty = Smooth(fy - y0);

            float v00 = Hash01(Mod(x0, cells), Mod(y0, cells));
            float v10 = Hash01(Mod(x0 + 1, cells), Mod(y0, cells));
            float v01 = Hash01(Mod(x0, cells), Mod(y0 + 1, cells));
            float v11 = Hash01(Mod(x0 + 1, cells), Mod(y0 + 1, cells));

            float a = Mathf.Lerp(v00, v10, tx);
            float b = Mathf.Lerp(v01, v11, tx);
            return Mathf.Lerp(a, b, ty);
        }

        private static float Smooth(float t)
        {
            return t * t * (3.0f - 2.0f * t);
        }

        private static int Mod(int value, int modulus)
        {
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }

        private static float Hash01(int x, int y)
        {
            return (float)WorldHash.Unit01(x, y, 0x54455854UL, 0x9E37UL);
        }

        // --- writing ---

        private static void SaveTexture(string name, int width, int height, Color[] pixels, bool srgb)
        {
            string path = $"{Directory_}/{name}.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Converts a height field to a tangent-space normal map by central
        /// difference, wrapping at the edges so the normals tile along with the
        /// albedo.
        /// </summary>
        private static void SaveNormalMap(string name, int width, int height, float[] relief, float strength)
        {
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float left = relief[y * width + Mod(x - 1, width)];
                    float right = relief[y * width + Mod(x + 1, width)];
                    float down = relief[Mod(y - 1, height) * width + x];
                    float up = relief[Mod(y + 1, height) * width + x];

                    Vector3 n = new Vector3((left - right) * strength, (down - up) * strength, 1.0f).normalized;
                    pixels[y * width + x] = new Color(
                        n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f);
                }
            }

            string path = $"{Directory_}/{name}.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }
    }
}
