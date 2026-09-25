using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// One-off inspection of blackpuffer.glb before committing to a build
    /// pipeline for it -- see MidnightPatchworkCoatBuilder's own class
    /// comment for why "whole character" vs "pre-cut garment" changes
    /// everything about how much work this is. Deleted once the real
    /// answer is known; not meant to survive as a permanent tool.
    /// </summary>
    public static class BlackPufferDiagnostic
    {
        private const string GlbPath = "Assets/CityX/Wearables/Models/blackpuffer.glb";

        [MenuItem("CityX/Diagnose Black Puffer Jacket")]
        public static void Run()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (source == null)
            {
                Debug.LogError($"CityX: {GlbPath} did not load as a GameObject.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var skinned = source.GetComponentInChildren<SkinnedMeshRenderer>();
            var staticFilter = source.GetComponentInChildren<MeshFilter>();

            if (skinned == null && staticFilter == null)
            {
                Debug.LogError("CityX: no MeshFilter or SkinnedMeshRenderer found at all.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (skinned == null)
            {
                Mesh m = staticFilter.sharedMesh;
                int triCount = 0;
                for (int sub = 0; sub < m.subMeshCount; sub++)
                {
                    triCount += m.GetTriangles(sub).Length / 3;
                }
                var renderer = staticFilter.GetComponent<MeshRenderer>();
                Debug.Log($"CityX PUFFER DIAGNOSTIC: STATIC mesh only, no skeleton/bone weights. " +
                          $"{m.vertexCount} vertices, {triCount} triangles (~{triCount / 2} quad-equivalent), " +
                          $"{m.subMeshCount} submesh(es), bounds size {m.bounds.size}, bounds centre {m.bounds.center}, " +
                          $"material '{renderer?.sharedMaterial?.name}'.");
                Screenshot(source, "blackpuffer-raw");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            Mesh mesh = skinned.sharedMesh;
            Transform[] bones = skinned.bones;
            Debug.Log($"CityX PUFFER DIAGNOSTIC: SKINNED mesh, {mesh.vertexCount} vertices, " +
                      $"{bones.Length} bones, material '{skinned.sharedMaterial?.name}', " +
                      $"bounds size {mesh.bounds.size}, bounds centre {mesh.bounds.center}.");

            var names = new System.Text.StringBuilder();
            foreach (Transform b in bones)
            {
                names.Append(b != null ? b.name : "<null>").Append(", ");
            }
            Debug.Log($"CityX PUFFER DIAGNOSTIC bone names: {names}");

            // Dominant-bone histogram -- tells us whether this is "mostly
            // one or two bones" (a real standalone jacket bound simply) or
            // "spread across head/arms/legs/hips evenly" (a whole character).
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            BoneWeight[] weights = mesh.boneWeights;
            foreach (BoneWeight w in weights)
            {
                int dominant = w.boneIndex0;
                float best = w.weight0;
                if (w.weight1 > best) { best = w.weight1; dominant = w.boneIndex1; }
                if (w.weight2 > best) { best = w.weight2; dominant = w.boneIndex2; }
                if (w.weight3 > best) { dominant = w.boneIndex3; }
                string name = (dominant >= 0 && dominant < bones.Length && bones[dominant] != null)
                    ? bones[dominant].name : $"<index {dominant}>";
                counts.TryGetValue(name, out int c);
                counts[name] = c + 1;
            }
            foreach (var kv in counts)
            {
                Debug.Log($"CityX PUFFER DIAGNOSTIC dominant-bone count: {kv.Key} = {kv.Value}");
            }

            // Texture status -- FarmerHat/MidnightPatchworkCoat's own
            // documented gotcha: glTFast sometimes silently stubs an
            // embedded texture to 4x4.
            Material mat = skinned.sharedMaterial;
            if (mat != null && mat.mainTexture != null)
            {
                Debug.Log($"CityX PUFFER DIAGNOSTIC texture: {mat.mainTexture.width}x{mat.mainTexture.height}");
            }
            else
            {
                Debug.Log("CityX PUFFER DIAGNOSTIC texture: none / no material.");
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>A raw, unlit-ish render of the source straight on -- see this file's own class comment.</summary>
        private static void Screenshot(GameObject sourcePrefab, string name)
        {
            string outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("PufferDiagnosticRig");
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.5f;
                sun.transform.rotation = Quaternion.Euler(40.0f, 130.0f, 0.0f);

                var fillObject = new GameObject("Fill");
                fillObject.transform.SetParent(root.transform);
                Light fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.8f;
                fill.transform.rotation = Quaternion.Euler(20.0f, -60.0f, 0.0f);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
                instance.transform.SetParent(root.transform);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                var bounds = new Bounds(instance.transform.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in renderers)
                {
                    if (!any) { bounds = r.bounds; any = true; }
                    else { bounds.Encapsulate(r.bounds); }
                }
                float radius = any ? bounds.extents.magnitude : 1.0f;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 40.0f;
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = radius * 20.0f + 5.0f;
                Vector3 camDir = new Vector3(0.3f, 0.15f, -1.0f).normalized;
                cameraObject.transform.position = bounds.center - camDir * (radius * 2.2f + 0.5f);
                cameraObject.transform.LookAt(bounds.center);

                target = new RenderTexture(900, 1100, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(900, 1100, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 1100), 0, 0);
                image.Apply();

                string path = Path.Combine(outputDirectory, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
        }
    }
}
