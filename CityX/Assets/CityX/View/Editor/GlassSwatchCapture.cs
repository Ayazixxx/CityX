using System.IO;
using CityX.View;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Renders a sheet of window glass through the real glass shader: the eight tints (top row), the eight kinds of light a lit window
    /// can show (middle row), and a blind drawn down by different amounts (bottom row), once by day and once lit at night.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod CityX.ViewEditor.GlassSwatchCapture.Capture
    ///
    /// Writes Logs/CityShots/glass-swatches.png (needs a real graphics device: no -nographics).
    /// </summary>
    public static class GlassSwatchCapture
    {
        private const int Width = 1920;
        private const int Height = 900;

        [MenuItem("CityX/Capture Glass Swatches")]
        public static void Capture()
        {
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);

            var lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityGlassLit.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityGlassDark.mat");
            var wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(FacadeTextureBuilder.MaterialPath);
            if (lit == null || dark == null || lit.shader.name != "CityX/Glass")
            {
                Debug.LogError("CityX: the glass material is not the glass shader -- rebuild the massing scene first.");
                return;
            }

            var root = new GameObject("GlassRig");
            try
            {
                var litBuffer = new MeshBuffer();
                var wallBuffer = new MeshBuffer();
                const float w = 1.3f, h = 1.9f, gap = 0.5f;
                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 8; column++)
                    {
                        float x0 = column * (w + gap);
                        float y0 = (2 - row) * (h + gap);
                        int tint = row == 0 ? column : 0;
                        int light = row == 1 ? column : 0;
                        float blind = row == 0 ? 0.35f : (row == 1 ? 0.0f : column / 8.0f * 0.9f);
                        litBuffer.Look = SurfaceLook.PackGlass(tint, light, blind);
                        litBuffer.AddQuad(new Vector3(x0, y0, 0), new Vector3(x0 + w, y0, 0), new Vector3(x0 + w, y0 + h, 0), new Vector3(x0, y0 + h, 0),
                            Vector3.back, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
                        // The wall behind it, so the glass is seen against something.
                        wallBuffer.Look = SurfaceLook.Pack(new Color(0.8f, 0.8f, 0.8f), (int)CityX.World.WallMaterial.Concrete, 0.2f);
                        wallBuffer.AddQuad(new Vector3(x0 - gap * 0.5f, y0 - gap * 0.5f, 0.05f), new Vector3(x0 + w + gap * 0.5f, y0 - gap * 0.5f, 0.05f),
                            new Vector3(x0 + w + gap * 0.5f, y0 + h + gap * 0.5f, 0.05f), new Vector3(x0 - gap * 0.5f, y0 + h + gap * 0.5f, 0.05f),
                            Vector3.back, new Vector2(x0, y0), new Vector2(x0 + w, y0), new Vector2(x0 + w, y0 + h), new Vector2(x0, y0 + h));
                    }
                }

                MakeObject(root, "Wall", wallBuffer, wallMaterial);
                MakeObject(root, "Glass", litBuffer, lit);

                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 0.4f;
                sunObject.transform.rotation = Quaternion.Euler(28.0f, 38.0f, 0.0f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.18f);

                // Lit at night, as the cycle would set it.
                lit.SetColor("_EmissionColor", new Color(0.988f, 0.796f, 0.443f) * 2.4f);

                float total = 8 * (w + gap) - gap;
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = (float)Width / Height;
                camera.orthographicSize = Mathf.Max(3.0f * (h + gap) * 0.5f + 0.4f, total * 0.5f / camera.aspect + 0.3f);
                camera.transform.position = new Vector3(total * 0.5f, 3.0f * (h + gap) * 0.5f - gap * 0.5f, -10.0f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 50.0f;

                RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(directory, "glass-swatches.png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                    lit.SetColor("_EmissionColor", Color.black);
                }
                Debug.Log("CityX: wrote glass-swatches.png");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void MakeObject(GameObject root, string name, MeshBuffer buffer, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(buffer.Vertices);
            mesh.SetNormals(buffer.Normals);
            mesh.SetUVs(0, buffer.Uvs);
            mesh.SetColors(buffer.Colors);
            mesh.SetTriangles(buffer.Triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
