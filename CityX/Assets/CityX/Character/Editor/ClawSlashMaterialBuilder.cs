using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The light of a claw swipe's streaks (ClawTrail), after the owner's picture of three slashes (2026-11-25): additive, white in the middle
    /// of each streak and glowing peach out to its edges. The cross-section is drawn here into a small picture (across the streak; nothing
    /// along it, which the streak's own taper does), so there is no painted texture to keep.
    /// </summary>
    public static class ClawSlashMaterialBuilder
    {
        public const string MaterialPath = "Assets/CityX/Character/ClawSlash.mat";
        public const string TexturePath = "Assets/CityX/Character/ClawSlashGlow.png";

        /// <summary>The sparks': a round glowing dot, which a spark's speed draws out into a short dash.</summary>
        public const string SparkMaterialPath = "Assets/CityX/Character/ClawSpark.mat";
        public const string SparkTexturePath = "Assets/CityX/Character/ClawSparkDot.png";
        private const int SparkSize = 16;
        private const string ShaderName = "Legacy Shaders/Particles/Additive";

        /// <summary>The glow's colour, and how far across the streak the white core reaches (a share of the half-width).</summary>
        public static readonly Color Glow = new Color(1.0f, 0.64f, 0.52f);

        public const float CoreShare = 0.24f;

        private const float Intensity = 1.25f;
        private const int Across = 64;
        private const int Along = 4;

        public static Material Ensure()
        {
            var pixels = new Color[Along * Across];
            for (int y = 0; y < Across; y++)
            {
                Color at = CrossSection(y / (float)(Across - 1));
                for (int x = 0; x < Along; x++)
                {
                    pixels[y * Along + x] = at;
                }
            }
            return Additive(MaterialPath, SaveTexture(TexturePath, Along, Across, pixels));
        }

        /// <summary>The sparks' material: a round dot, white in the middle, glowing peach out to nothing.</summary>
        public static Material EnsureSpark()
        {
            var pixels = new Color[SparkSize * SparkSize];
            for (int y = 0; y < SparkSize; y++)
            {
                for (int x = 0; x < SparkSize; x++)
                {
                    float dx = (x + 0.5f) / SparkSize * 2.0f - 1.0f, dy = (y + 0.5f) / SparkSize * 2.0f - 1.0f;
                    pixels[y * SparkSize + x] = CrossSection(0.5f + 0.5f * Mathf.Min(1.0f, Mathf.Sqrt(dx * dx + dy * dy)));
                }
            }
            return Additive(SparkMaterialPath, SaveTexture(SparkTexturePath, SparkSize, SparkSize, pixels));
        }

        private static Material Additive(string path, Texture2D picture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(ShaderName);
            material.SetTexture("_MainTex", picture);
            // This shader draws 2 x tint x texture, so the tint is half the wanted brightness.
            material.SetColor("_TintColor", new Color(Intensity * 0.5f, Intensity * 0.5f, Intensity * 0.5f, 1.0f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The cross-section at <paramref name="across"/> (0 one edge, 0.5 the middle, 1 the other edge): its colour, the light it adds by its alpha.</summary>
        public static Color CrossSection(float across)
        {
            float fromMiddle = Mathf.Abs(Mathf.Clamp01(across) - 0.5f) * 2.0f;
            float core = Mathf.Pow(Mathf.Clamp01(1.0f - fromMiddle / CoreShare), 1.5f);
            float glow = Mathf.Pow(1.0f - fromMiddle, 2.4f) * 0.85f;
            Color colour = Color.Lerp(Glow, Color.white, core);
            return new Color(colour.r, colour.g, colour.b, Mathf.Max(core, glow));
        }

        private static Texture2D SaveTexture(string path, int width, int height, Color[] pixels)
        {
            var picture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            picture.SetPixels(pixels);
            picture.Apply();
            string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(file, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
