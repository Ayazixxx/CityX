using System.IO;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Prints a <see cref="ShirtPrint"/> onto the T-Shirt's texture: a PNG next to the print's asset (<see cref="TexturePathFor"/>), the
    /// cloth's colour with the picture laid over it where the print says.
    ///
    /// Where a place on the cloth lands on the texture is read off the shirt's own mesh: around the middle of each side (the height
    /// tshirt_build.py's template marks), its points' texture coordinates against their place on the body, fitted with a straight line.
    /// The front and back pieces lie upright on the texture as seen from their side, at one scale (docs/TSHIRT.md).
    /// </summary>
    public static class ShirtPrintBaker
    {
        /// <summary>Texels across the printed texture: about 960 a metre on the shirt, so a picture 34 cm wide is printed 330 texels wide.</summary>
        public const int TextureSize = 2048;

        /// <summary>Where the front and back pieces lie on the texture (tshirt_build.py's PIECE_BOXES).</summary>
        private static readonly Rect FrontPiece = new Rect(0.0f, 0.4f, 0.5f, 0.6f);
        private static readonly Rect BackPiece = new Rect(0.5f, 0.4f, 0.5f, 0.6f);

        /// <summary>The middle of the chest and of the back, bind pose (tshirt_build.py's TEMPLATE_MARKS).</summary>
        private const float MiddleHeight = 1.62f;

        /// <summary>The cloth around the middle a side's placement is fitted to: this far either side of it, and up and down.</summary>
        private const float FitAcross = 0.2f;
        private const float FitUpDown = 0.2f;

        private const float CentimetresPerMetre = 100.0f;

        /// <summary>The printed texture of a print: "&lt;print&gt;Shirt.png" next to its asset.</summary>
        public static string TexturePathFor(ShirtPrint print)
        {
            string assetPath = AssetDatabase.GetAssetPath(print);
            return Path.ChangeExtension(assetPath, null) + "Shirt.png";
        }

        /// <summary>Prints <paramref name="print"/> on <paramref name="shirt"/>'s texture in <paramref name="cloth"/>, and imports it.</summary>
        public static Texture2D Bake(ShirtPrint print, Mesh shirt, Color cloth)
        {
            string texturePath = TexturePathFor(print);
            Color32[] texels = Print(print, shirt, cloth);
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(texels);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, texturePath), texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            SetImport(texturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        /// <summary>The printed texture's texels, the cloth's colour where the picture is not.</summary>
        public static Color32[] Print(ShirtPrint print, Mesh shirt, Color cloth)
        {
            bool front = print.Side == ShirtPrint.ShirtSide.Front;
            FitSide(shirt, front, out Vector2 middleUv, out float uvPerMetre);
            Vector2 centre = ShirtPrintLayout.CentreUv(middleUv, uvPerMetre, print.RightCm / CentimetresPerMetre, print.UpCm / CentimetresPerMetre);
            if (print.Logo == null)
            {
                Debug.LogWarning($"CityX: {print.name} has no picture -- the shirt is printed plain.");
                return ShirtPrintLayout.Print(new[] { new Color32(0, 0, 0, 0) }, 1, 1, TextureSize, cloth, centre, 0.0f, 0.0f);
            }
            var logo = new Texture2D(2, 2);
            try
            {
                // Read from the file, so the picture's own import settings (not readable, compressed, resized) do not matter.
                logo.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    AssetDatabase.GetAssetPath(print.Logo))));
                return ShirtPrintLayout.Print(logo.GetPixels32(), logo.width, logo.height, TextureSize, cloth, centre,
                    print.WidthCm / CentimetresPerMetre * uvPerMetre, print.RotationDegrees);
            }
            finally
            {
                Object.DestroyImmediate(logo);
            }
        }

        /// <summary>
        /// Where the middle of the front (or back) lies on the texture, and how far one metre along the cloth goes on it: straight lines
        /// through the texture coordinates of the side's points round its middle, u against the distance to the right as seen from that side,
        /// v against the height.
        /// </summary>
        public static void FitSide(Mesh shirt, bool front, out Vector2 middleUv, out float uvPerMetre)
        {
            Vector3[] vertices = shirt.vertices;
            Vector2[] uvs = shirt.uv;
            Rect piece = front ? FrontPiece : BackPiece;
            float sr = 0, su = 0, srr = 0, sru = 0, sy = 0, sv = 0, syy = 0, syv = 0;
            int n = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float right = front ? -p.x : p.x;   // bind pose, +z ahead: seen from in front, the wearer's left is on the right
                if (!piece.Contains(uvs[i]) || (p.z > 0.0f) != front || Mathf.Abs(right) > FitAcross || Mathf.Abs(p.y - MiddleHeight) > FitUpDown)
                {
                    continue;
                }
                sr += right;
                su += uvs[i].x;
                srr += right * right;
                sru += right * uvs[i].x;
                sy += p.y;
                sv += uvs[i].y;
                syy += p.y * p.y;
                syv += p.y * uvs[i].y;
                n++;
            }
            if (n < 3)
            {
                Debug.LogError($"CityX: too few points of the T-Shirt's {(front ? "front" : "back")} round its middle to place a print on it.");
                middleUv = piece.center;
                uvPerMetre = 0.0f;
                return;
            }
            float uSlope = (n * sru - sr * su) / (n * srr - sr * sr);
            float vSlope = (n * syv - sy * sv) / (n * syy - sy * sy);
            middleUv = new Vector2((su - uSlope * sr) / n, (sv - vSlope * sy) / n + vSlope * MiddleHeight);
            uvPerMetre = 0.5f * (uSlope + vSlope);
            if (uSlope <= 0.0f || vSlope <= 0.0f)
            {
                Debug.LogError($"CityX: the T-Shirt's {(front ? "front" : "back")} piece does not lie upright on its texture as seen from its side " +
                               $"(u {uSlope:0.000}, v {vSlope:0.000} a metre); a print on it lands mirrored.");
            }
        }

        private static void SetImport(string texturePath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            if (importer.maxTextureSize == TextureSize && importer.wrapMode == TextureWrapMode.Clamp && importer.alphaSource == TextureImporterAlphaSource.None)
            {
                return;
            }
            importer.maxTextureSize = TextureSize;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }
}
