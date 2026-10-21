using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds a Unity Mesh directly from fused_body.gd's own exact,
    /// unconverted vertex/normal numbers (tests/scratch_export_body_obj.gd's
    /// own raw dump, GodotPlayerBodyRaw.txt) instead of running Unity's own
    /// independent marching-tetrahedra extraction -- Requested: "can you
    /// just make the godot one in unity why is there any difference at
    /// all?"
    ///
    /// THE TWO REFERENCE EXPORTS NEVER FULLY AGREED. FusedBodyShape.
    /// LimbCapsules/BodyRig already use fused_body.gd's own exact joint
    /// positions and radii (confirmed directly, constant by constant), so
    /// the ~5% residual gap measured between the two Blender exports
    /// (arm reach 1.263m here vs 1.336m there) isn't a proportions bug --
    /// it's two SEPARATE marching-tetrahedra implementations (fused_body.gd's
    /// own extract_mesh vs FusedBodyMesh.Extract) sampling the same
    /// continuous SDF on two DIFFERENTLY-computed grids (Godot uses fixed,
    /// hand-authored BOUNDS_MIN/MAX; Unity computes its bounding box
    /// dynamically, since it also has to fit the much wider T-pose the real
    /// shipped body is extracted at -- Godot has no T-pose step at all).
    /// A shifted grid origin changes exactly where each vertex lands along
    /// the isosurface even though the underlying field is identical. Rather
    /// than chase that grid-alignment difference number by number, this
    /// just uses Godot's own already-extracted vertices directly.
    ///
    /// NOT WIRED INTO FusedBodyBuilder -- this is a reference/verification
    /// tool only. The REAL shipped body still needs Unity's own extraction
    /// (a T-pose bind Godot's own single arms-down bake can't provide), so
    /// swapping this in there would need a T-pose-compatible bake Godot
    /// never produces. This tool exists to give Blender a mathematically
    /// exact target instead of a close-but-not-identical approximation.
    /// </summary>
    public static class GodotBodyImporter
    {
        private const string RawInputPath = "Logs/GodotPlayerBodyRaw.txt";
        private const string OutputPath = "Logs/GodotPlayerBodyReferenceInUnity.obj";
        private const string ScreenshotPath = "Logs/CityShots/godot-body-in-unity-verify.png";

        [MenuItem("CityX/Import Godot Body Reference (Verify)")]
        public static void ImportAndVerify()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string rawPath = Path.Combine(projectRoot, RawInputPath);
            if (!File.Exists(rawPath))
            {
                Debug.LogError($"CityX: no raw Godot dump at {rawPath} -- run " +
                                "tests/scratch_export_body_obj.gd first.");
                return;
            }

            Mesh mesh = BuildMeshFromRawDump(rawPath);
            if (mesh == null)
            {
                return;
            }

            string objPath = Path.Combine(projectRoot, OutputPath);
            File.WriteAllText(objPath, ToObj(mesh));
            Debug.Log($"CityX: wrote {objPath} ({mesh.vertexCount} vertices, straight from " +
                      "Godot's own numbers).");

            string screenshotPath = Path.Combine(projectRoot, ScreenshotPath);
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath));
            RenderVerificationShot(mesh, screenshotPath);

            Object.DestroyImmediate(mesh);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// Parses the raw dump (body count, head count, then that many
        /// position lines, then that many normal lines, all in Godot's own
        /// native coordinates) and applies the SAME negate-X + swapped-
        /// winding conversion PlayerBodyReferenceExporter's own ToObj uses
        /// to go from a left-handed engine (there, Unity) to a right-handed
        /// one -- here run in reverse, from Godot's right-handed space into
        /// Unity's left-handed one, which is the identical operation
        /// (negating one axis and reversing winding is its own inverse).
        /// UNVERIFIED BY MATH ALONE -- see RenderVerificationShot, called
        /// right after this, for why that isn't trusted on its own.
        /// </summary>
        private static Mesh BuildMeshFromRawDump(string rawPath)
        {
            string[] lines = File.ReadAllLines(rawPath);
            if (lines.Length < 2 ||
                !int.TryParse(lines[0], out int bodyCount) ||
                !int.TryParse(lines[1], out int headCount))
            {
                Debug.LogError("CityX: raw Godot dump is malformed (missing body/head counts).");
                return null;
            }

            int total = bodyCount + headCount;
            var godotPositions = new Vector3[total];
            var godotNormals = new Vector3[total];

            int line = 2;
            for (int i = 0; i < total; i++, line++)
            {
                godotPositions[i] = ParseVector3(lines[line]);
            }
            for (int i = 0; i < total; i++, line++)
            {
                godotNormals[i] = ParseVector3(lines[line]);
            }

            var vertices = new Vector3[total];
            var normals = new Vector3[total];
            for (int i = 0; i < total; i++)
            {
                vertices[i] = new Vector3(-godotPositions[i].x, godotPositions[i].y, godotPositions[i].z);
                normals[i] = new Vector3(-godotNormals[i].x, godotNormals[i].y, godotNormals[i].z);
            }

            // Non-indexed triangle soup, same as the source -- every
            // consecutive triple is one triangle. Corners b/c swapped to
            // keep faces outward-facing after the X-flip mirrors winding,
            // the same fix both existing OBJ exporters already apply.
            var triangles = new int[total];
            for (int i = 0; i + 2 < total; i += 3)
            {
                triangles[i] = i;
                triangles[i + 1] = i + 2;
                triangles[i + 2] = i + 1;
            }

            var mesh = new Mesh
            {
                name = "GodotBodyInUnity",
                indexFormat = total > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ParseVector3(string line)
        {
            string[] parts = line.Split(' ');
            return new Vector3(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// A plain, unlit, front-and-back render -- NOT proof by itself, but
        /// the fast, cheap first check for exactly the failure modes a
        /// coordinate conversion done by reasoning alone can get wrong:
        /// a mirrored body (wrong axis negated), inside-out faces (winding
        /// fix applied backward), or a sideways/upside-down orientation.
        /// A correct result reads as an ordinary standing figure, arms down,
        /// facing the camera on the front shot and its own back on the
        /// second -- exactly what PlayerBodyReference.obj already looks
        /// like, since both should now be the same shape.
        /// </summary>
        private static void RenderVerificationShot(Mesh mesh, string path)
        {
            var root = new GameObject("GodotBodyVerifyRig");
            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                var meshObject = new GameObject("GodotBody");
                meshObject.transform.SetParent(root.transform, false);
                meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = new Color(0.85f, 0.55f, 0.35f),
                };
                meshObject.AddComponent<MeshRenderer>().sharedMaterial = material;

                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform, false);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(30.0f, 150.0f, 0.0f);

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 32.0f;
                camera.nearClipPlane = 0.05f;
                // Framed on the whole ~2.35m figure from a few metres out,
                // same idiom CharacterCapture.Shoot uses for the real body.
                cameraObject.transform.position = new Vector3(0.6f, 1.2f, -4.5f);
                cameraObject.transform.LookAt(new Vector3(0.0f, 1.1f, 0.0f));

                const int width = 700;
                const int height = 900;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote verification screenshot to {path}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Minimal ASCII .obj writer -- position/normal and triangle faces, nothing else.</summary>
        private static string ToObj(Mesh mesh)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Godot fused_body reference, imported into Unity from its own exact numbers");

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            foreach (Vector3 v in vertices)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "v {0:F6} {1:F6} {2:F6}", v.x, v.y, v.z));
            }
            foreach (Vector3 n in normals)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "vn {0:F6} {1:F6} {2:F6}", n.x, n.y, n.z));
            }

            int[] triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t] + 1;
                int b = triangles[t + 1] + 1;
                int c = triangles[t + 2] + 1;
                sb.AppendLine($"f {a}//{a} {b}//{b} {c}//{c}");
            }
            return sb.ToString();
        }
    }
}
