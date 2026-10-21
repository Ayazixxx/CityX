using System.IO;
using CityX.View;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Bakes the eight procedural wall materials (View/Buildings/FacadeTextures.cs) to two PNG atlases, sets them to import as 2D texture arrays,
    /// and makes the facade material that reads them.
    ///
    ///   Assets/CityX/View/Textures/FacadeAlbedo.png   4 x 3 grid of 512 px tiles: albedo rgb, smoothness in alpha
    ///   Assets/CityX/View/Textures/FacadeNormal.png   the same grid, tangent-space normals
    ///
    /// THESE ARE PLAIN IMAGES, SO THEY CAN BE REPLACED. A hand-made or bought set drawn on the same grid (slice order: concrete, brick,
    /// plaster, metal, stone, tile, spandrel, roof, paving, asphalt, earth, setts, left to right then top to bottom) can simply overwrite the PNGs; this builder never
    /// overwrites an existing atlas unless asked ("Regenerate Facade Textures"), and it only re-applies the import settings.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod CityX.ViewEditor.FacadeTextureBuilder.Generate
    /// </summary>
    public static class FacadeTextureBuilder
    {
        public const string Directory_ = "Assets/CityX/View/Textures";
        public const string AlbedoPath = Directory_ + "/FacadeAlbedo.png";
        public const string NormalPath = Directory_ + "/FacadeNormal.png";
        public const string MaterialPath = "Assets/CityX/View/Materials/CityConcrete.mat";
        public const string ShaderName = "CityX/Facade";

        [MenuItem("CityX/Generate Facade Textures")]
        public static void Generate()
        {
            Build(false);
        }

        [MenuItem("CityX/Regenerate Facade Textures (overwrites)")]
        public static void Regenerate()
        {
            Build(true);
        }

        public static void Build(bool overwrite)
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, Directory_));

            bool missing = !HasExpectedLayout(AlbedoPath) || !HasExpectedLayout(NormalPath);
            if (overwrite || missing)
            {
                FacadeTextures.BuildAtlases(FacadeTextures.Size, out Color32[] albedo, out Color32[] normal);
                int width = FacadeTextures.Size * FacadeTextures.Columns;
                int height = FacadeTextures.Size * FacadeTextures.Rows;
                WritePng(AlbedoPath, albedo, width, height);
                WritePng(NormalPath, normal, width, height);
                Debug.Log($"CityX: facade textures drawn ({FacadeTextures.Columns * FacadeTextures.Rows} slices of {FacadeTextures.Size} px).");
            }
            else
            {
                Debug.Log("CityX: facade textures already exist -- keeping them (use Regenerate to redraw).");
            }

            AssetDatabase.Refresh();
            Configure(AlbedoPath, false);
            Configure(NormalPath, true);
            EnsureMaterial();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Whether an atlas exists and is laid out as the shader expects (a grid of Columns x Rows tiles of Size px). One that is not -- the
        /// layout grew from 8 slices to 12 when the paving, road and ground joined the walls -- cannot be used, and is redrawn.
        /// </summary>
        private static bool HasExpectedLayout(string assetPath)
        {
            string path = Absolute(assetPath);
            if (!File.Exists(path))
            {
                return false;
            }
            using (FileStream stream = File.OpenRead(path))
            {
                var header = new byte[24];
                if (stream.Read(header, 0, 24) < 24)
                {
                    return false;
                }
                int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                bool ok = width == FacadeTextures.Size * FacadeTextures.Columns && height == FacadeTextures.Size * FacadeTextures.Rows;
                if (!ok)
                {
                    Debug.LogWarning($"CityX: {assetPath} is {width} x {height}, not the {FacadeTextures.Size * FacadeTextures.Columns} x " +
                                     $"{FacadeTextures.Size * FacadeTextures.Rows} the shader expects -- redrawing it.");
                }
                return ok;
            }
        }

        private static string Absolute(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        }

        private static void WritePng(string assetPath, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Absolute(assetPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>Makes the atlas import as a 2D texture array: the 4 x 2 grid becomes eight slices.</summary>
        private static void Configure(string assetPath, bool normalMap)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.alphaSource = normalMap ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureShape = TextureImporterShape.Texture2DArray;
            settings.flipbookColumns = FacadeTextures.Columns;
            settings.flipbookRows = FacadeTextures.Rows;
            settings.wrapMode = TextureWrapMode.Repeat;
            settings.filterMode = FilterMode.Trilinear;
            settings.aniso = 8;
            settings.mipmapEnabled = true;
            settings.readable = false;
            importer.SetTextureSettings(settings);

            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath);
            if (array == null || array.depth != FacadeTextures.Columns * FacadeTextures.Rows)
            {
                Debug.LogError($"CityX: {assetPath} did not import as a {FacadeTextures.Columns * FacadeTextures.Rows}-slice texture array " +
                               $"(got {(array == null ? "nothing" : array.depth + " slices")}).");
            }
        }

        /// <summary>The wall material (named CityConcrete, as it always was, so every reference to it still holds), now the facade shader.</summary>
        public static Material EnsureMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"CityX: shader {ShaderName} not found -- did the shader fail to compile?");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_FacadeAlbedo", AssetDatabase.LoadAssetAtPath<Texture2DArray>(AlbedoPath));
            material.SetTexture("_FacadeNormal", AssetDatabase.LoadAssetAtPath<Texture2DArray>(NormalPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
