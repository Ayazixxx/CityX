using System.IO;
using CityX.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Renders a sheet of the eight wall materials, clean on the top row and weathered on the bottom, through the real facade shader.
    /// The quickest way to see whether the shader, the texture array order and the textures themselves are right.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod CityX.ViewEditor.FacadeSwatchCapture.Capture
    ///
    /// Writes Logs/CityShots/facade-swatches.png (needs a real graphics device: no -nographics).
    /// </summary>
    public static class FacadeSwatchCapture
    {
        private const int Width = 1920;
        private const int Height = 720;

        /// <summary>Redraws the textures, then renders the sheet: one batch run to see a texture change.</summary>
        public static void RegenerateAndCapture()
        {
            FacadeTextureBuilder.Build(true);
            Capture();
        }

        [MenuItem("CityX/Capture Facade Swatches")]
        public static void Capture()
        {
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);

            var material = AssetDatabase.LoadAssetAtPath<Material>(FacadeTextureBuilder.MaterialPath);
            if (material == null || material.shader.name != FacadeTextureBuilder.ShaderName)
            {
                Debug.LogError("CityX: the facade material is not set up -- run CityX/Generate Facade Textures first.");
                return;
            }

            var root = new GameObject("SwatchRig");
            try
            {
                // Eight materials across, clean above and worn below; each swatch is 3 m square so a tile is plainly visible.
                var buffer = new MeshBuffer();
                const float size = 3.0f;
                const float gap = 0.15f;
                for (int row = 0; row < 2; row++)
                {
                    for (int slice = 0; slice < (FacadeTextures.Columns * FacadeTextures.Rows); slice++)
                    {
                        float x0 = slice * (size + gap);
                        float y0 = (1 - row) * (size + gap);
                        buffer.Look = SurfaceLook.Pack(Color.white, slice, row == 0 ? 0.0f : 0.8f);
                        buffer.AddQuad(new Vector3(x0, y0, 0), new Vector3(x0 + size, y0, 0),
                            new Vector3(x0 + size, y0 + size, 0), new Vector3(x0, y0 + size, 0), Vector3.back,
                            new Vector2(0, y0), new Vector2(size, y0), new Vector2(size, y0 + size), new Vector2(0, y0 + size));
                    }
                }

                var mesh = new Mesh { name = "swatches" };
                mesh.SetVertices(buffer.Vertices);
                mesh.SetNormals(buffer.Normals);
                mesh.SetUVs(0, buffer.Uvs);
                mesh.SetColors(buffer.Colors);
                mesh.SetTriangles(buffer.Triangles, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();

                var wall = new GameObject("Swatches");
                wall.transform.SetParent(root.transform);
                wall.AddComponent<MeshFilter>().sharedMesh = mesh;
                wall.AddComponent<MeshRenderer>().sharedMaterial = material;

                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 2.0f;
                sun.shadows = LightShadows.None;
                sunObject.transform.rotation = Quaternion.Euler(28.0f, 38.0f, 0.0f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                float totalWidth = (FacadeTextures.Columns * FacadeTextures.Rows) * (size + gap) - gap;
                camera.orthographicSize = (2 * size + gap) * 0.5f + 0.3f;
                camera.transform.position = new Vector3(totalWidth * 0.5f, (2 * size + gap) * 0.5f, -10.0f);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 50.0f;
                camera.aspect = (float)Width / Height;
                camera.orthographicSize = Mathf.Max(camera.orthographicSize, totalWidth * 0.5f / camera.aspect + 0.2f);

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
                    File.WriteAllBytes(Path.Combine(directory, "facade-swatches.png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                }
                Debug.Log("CityX: wrote facade-swatches.png");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
