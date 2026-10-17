using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.MapImport
{
    /// <summary>
    /// Converts a Source 1 <c>.bsp</c> into a Unity prefab.
    ///
    /// Geometry only, and deliberately so: Source stores material NAMES, not
    /// the images, and those belong to Valve. What is imported is the SHAPE of
    /// the map -- the part the mapper actually authored -- ready to be
    /// re-skinned with your own materials.
    /// </summary>
    public static class SourceBspImporter
    {
        /// <summary>
        /// Source units to metres.
        ///
        /// 16 units to a foot, so one unit is 0.75 inches. Skip this and the
        /// map imports about 50x too big -- which is the sort of mistake that
        /// looks like "the map is broken" rather than "a constant is wrong",
        /// because at that size you spawn inside a wall and see nothing.
        ///
        /// Sanity anchor: a standing Source player is 72 units, which comes
        /// out at 1.37m here. That is deliberately NOT 1.8m -- Source players
        /// are famously stubby relative to their world, and matching the
        /// world's real scale matters more than matching a nominal human.
        /// </summary>
        public const float UnitsToMetres = 0.01905f;

        [MenuItem("CityX/Import Source .bsp Map...")]
        public static void ImportWithDialog()
        {
            string path = EditorUtility.OpenFilePanel("Select a Source 1 .bsp", "", "bsp");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            Import(path, "Assets/CityX/MapImport/Imported");
        }

        /// <summary>
        /// Headless entry point. Reads -bspPath and -bspOut from the command
        /// line so this can run from the test runner script without the editor
        /// UI.
        /// </summary>
        public static void ImportFromCommandLine()
        {
            string path = null;
            string outFolder = "Assets/CityX/MapImport/Imported";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-bspPath")
                {
                    path = args[i + 1];
                }
                else if (args[i] == "-bspOut")
                {
                    outFolder = args[i + 1];
                }
            }
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[CityX.MapImport] no -bspPath given");
                return;
            }
            Import(path, outFolder);
        }

        public static void Import(string bspPath, string outFolder)
        {
            SourceBsp bsp = SourceBsp.Load(bspPath);
            string mapName = Path.GetFileNameWithoutExtension(bspPath);

            EnsureFolder(outFolder);

            // Model 0 is the world. Models 1+ are brush entities -- doors,
            // breakables -- which move independently in the original game, so
            // they become separate children rather than being welded into the
            // world mesh.
            GameObject root = new GameObject(mapName);
            int totalTris = 0;
            int totalVerts = 0;
            int skippedTool = 0;

            List<Mesh> meshes = new List<Mesh>();

            MaterialLibrary materials = new MaterialLibrary(outFolder, mapName);

            for (int m = 0; m < bsp.Models.Length; m++)
            {
                Mesh mesh = BuildModelMesh(bsp, m, out int tris, out int skipped, out List<string> usedMaterials);
                skippedTool += skipped;
                if (mesh == null)
                {
                    continue;
                }

                mesh.name = m == 0 ? $"{mapName}_world" : $"{mapName}_model{m}";
                meshes.Add(mesh);
                totalTris += tris;
                totalVerts += mesh.vertexCount;

                GameObject go = new GameObject(m == 0 ? "World" : $"BrushEntity_{m}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = SourceToUnity(bsp.Models[m].Origin);

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();

                // One material per surface type, in submesh order. This is
                // what carries the mapper's intent across: his decision that
                // this wall is brick and that catwalk is grating survives even
                // though none of Valve's images do.
                Material[] slots = new Material[usedMaterials.Count];
                for (int s = 0; s < usedMaterials.Count; s++)
                {
                    slots[s] = materials.GetOrCreate(usedMaterials[s]);
                }
                renderer.sharedMaterials = slots;

                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            materials.SaveAll();

            // Spawn points, so there is somewhere sensible to stand.
            AddSpawnMarkers(bsp, root);

            string meshAssetPath = $"{outFolder}/{mapName}_meshes.asset";
            if (meshes.Count > 0)
            {
                AssetDatabase.CreateAsset(meshes[0], meshAssetPath);
                for (int i = 1; i < meshes.Count; i++)
                {
                    AssetDatabase.AddObjectToAsset(meshes[i], meshAssetPath);
                }
            }

            string prefabPath = $"{outFolder}/{mapName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Bounds bounds = meshes.Count > 0 ? meshes[0].bounds : new Bounds();
            Debug.Log(
                $"[CityX.MapImport] imported '{mapName}' (BSP v{bsp.Version})\n" +
                $"  models:     {bsp.Models.Length}\n" +
                $"  vertices:   {totalVerts:N0}\n" +
                $"  triangles:  {totalTris:N0}\n" +
                $"  tool faces skipped: {skippedTool:N0}\n" +
                $"  world size: {bounds.size.x:0.0} x {bounds.size.y:0.0} x {bounds.size.z:0.0} m\n" +
                $"  prefab:     {prefabPath}");
        }

        private static Mesh BuildModelMesh(SourceBsp bsp, int modelIndex, out int triangleCount,
            out int skippedTool, out List<string> usedMaterials)
        {
            SourceBsp.Model model = bsp.Models[modelIndex];
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            // Triangles are bucketed by material name rather than pooled into
            // one list, so each surface type becomes its own submesh and can
            // take its own material.
            Dictionary<string, List<int>> trianglesByMaterial = new Dictionary<string, List<int>>();
            usedMaterials = new List<string>();
            skippedTool = 0;

            for (int f = model.FirstFace; f < model.FirstFace + model.NumFaces; f++)
            {
                if (f < 0 || f >= bsp.Faces.Length)
                {
                    continue;
                }
                SourceBsp.Face face = bsp.Faces[f];

                if (bsp.IsFaceInvisible(face))
                {
                    skippedTool++;
                    continue;
                }
                // Displacements are terrain patches with their own subdivision
                // data. Skipping them loses sculpted ground; including them
                // half-parsed would produce garbage triangles, which is worse.
                if (face.DispInfo >= 0)
                {
                    continue;
                }
                if (face.NumEdges < 3)
                {
                    continue;
                }

                List<int> perimeter = bsp.FacePerimeter(face);
                int baseIndex = vertices.Count;

                SourceBsp.TexInfo texInfo = bsp.TexInfos[face.TexInfo];
                float texWidth = 128.0f;
                float texHeight = 128.0f;
                if (texInfo.TexData >= 0 && texInfo.TexData < bsp.TexDatas.Length)
                {
                    SourceBsp.TexData td = bsp.TexDatas[texInfo.TexData];
                    if (td.Width > 0) { texWidth = td.Width; }
                    if (td.Height > 0) { texHeight = td.Height; }
                }

                // Flat-shade every face by giving it its own vertices. Sharing
                // them and calling RecalculateNormals would smooth across the
                // hard corners a brush-built map is entirely made of, turning
                // crisp architecture into something melted.
                foreach (int vi in perimeter)
                {
                    Vector3 sourcePos = bsp.Vertices[vi];
                    vertices.Add(SourceToUnity(sourcePos));

                    float u = (Vector3.Dot(sourcePos, new Vector3(texInfo.TextureVecS.x, texInfo.TextureVecS.y, texInfo.TextureVecS.z))
                               + texInfo.TextureVecS.w) / texWidth;
                    float v = (Vector3.Dot(sourcePos, new Vector3(texInfo.TextureVecT.x, texInfo.TextureVecT.y, texInfo.TextureVecT.z))
                               + texInfo.TextureVecT.w) / texHeight;
                    uvs.Add(new Vector2(u, -v));
                }

                // Fan-triangulate. Brush faces are convex by construction, so
                // a fan is exact here rather than an approximation.
                string materialName = bsp.TextureNameFor(face);
                if (!trianglesByMaterial.TryGetValue(materialName, out List<int> bucket))
                {
                    bucket = new List<int>();
                    trianglesByMaterial[materialName] = bucket;
                    usedMaterials.Add(materialName);
                }
                for (int i = 1; i < perimeter.Count - 1; i++)
                {
                    bucket.Add(baseIndex);
                    bucket.Add(baseIndex + i);
                    bucket.Add(baseIndex + i + 1);
                }

                Vector3 faceNormal = ComputeNormal(vertices, baseIndex, perimeter.Count);
                for (int i = 0; i < perimeter.Count; i++)
                {
                    normals.Add(faceNormal);
                }
            }

            triangleCount = 0;
            foreach (KeyValuePair<string, List<int>> kvp in trianglesByMaterial)
            {
                triangleCount += kvp.Value.Count / 3;
            }
            if (triangleCount == 0)
            {
                return null;
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = usedMaterials.Count;
            for (int s = 0; s < usedMaterials.Count; s++)
            {
                mesh.SetTriangles(trianglesByMaterial[usedMaterials[s]], s);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates one material per Source material name, backed by a texture
        /// drawn from scratch by ProceduralTextureForge.
        ///
        /// Cached per name so the five brick materials each get their own
        /// asset -- they differ, because the forge seeds itself from the name,
        /// which is how "BRICK_G_TINTBEIGE" comes out visibly different from
        /// "BRICKWALL008A" with nobody hand-authoring the difference.
        /// </summary>
        private sealed class MaterialLibrary
        {
            private readonly string _folder;
            private readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

            public MaterialLibrary(string outFolder, string mapName)
            {
                _folder = $"{outFolder}/{mapName}_materials";
                EnsureFolder(_folder);
            }

            public Material GetOrCreate(string sourceName)
            {
                if (_cache.TryGetValue(sourceName, out Material cached))
                {
                    return cached;
                }

                MaterialFamily family = MaterialFamilyClassifier.Classify(sourceName);
                string safe = sourceName.Replace('/', '_').Replace('\\', '_');
                if (string.IsNullOrEmpty(safe))
                {
                    safe = "unnamed";
                }

                Texture2D texture = ProceduralTextureForge.Create(family, sourceName);
                texture.name = safe;
                string texPath = $"{_folder}/{safe}.png";
                System.IO.File.WriteAllBytes(texPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texPath);

                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.alphaIsTransparency = family == MaterialFamily.MetalGrate || family == MaterialFamily.Glass;
                    importer.SaveAndReimport();
                }
                Texture2D loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material material = new Material(shader);
                material.name = safe;
                material.mainTexture = loaded;
                ApplyFamilyProperties(material, family);

                string matPath = $"{_folder}/{safe}.mat";
                AssetDatabase.CreateAsset(material, matPath);
                _cache[sourceName] = material;
                return material;
            }

            public void SaveAll()
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            /// <summary>
            /// Surface response per family. A brick wall and a metal panel can
            /// share a colour and still read completely differently, purely
            /// from how they catch light -- so smoothness and metallic carry
            /// as much of the impression here as the texture does.
            /// </summary>
            private static void ApplyFamilyProperties(Material material, MaterialFamily family)
            {
                float smoothness = 0.1f;
                float metallic = 0.0f;

                switch (family)
                {
                    case MaterialFamily.MetalWall:
                    case MaterialFamily.MetalFloor:
                    case MaterialFamily.MetalGrate:
                        smoothness = 0.55f;
                        metallic = 0.85f;
                        break;
                    case MaterialFamily.Glass:
                        smoothness = 0.95f;
                        metallic = 0.0f;
                        SetTransparent(material);
                        break;
                    case MaterialFamily.Brick:
                    case MaterialFamily.Stone:
                    case MaterialFamily.Concrete:
                        smoothness = 0.08f;
                        break;
                    case MaterialFamily.Wood:
                    case MaterialFamily.WoodPlank:
                        smoothness = 0.20f;
                        break;
                    case MaterialFamily.GrassDirt:
                        smoothness = 0.05f;
                        break;
                }

                if (material.HasProperty("_Smoothness")) { material.SetFloat("_Smoothness", smoothness); }
                if (material.HasProperty("_Glossiness")) { material.SetFloat("_Glossiness", smoothness); }
                if (material.HasProperty("_Metallic")) { material.SetFloat("_Metallic", metallic); }

                if (family == MaterialFamily.MetalGrate)
                {
                    SetAlphaClip(material);
                }
            }

            private static void SetTransparent(Material material)
            {
                material.SetFloat("_Surface", 1.0f);
                material.SetFloat("_Blend", 0.0f);
                material.renderQueue = 3000;
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            private static void SetAlphaClip(Material material)
            {
                // Cut-out rather than blended: a grate's holes are either
                // there or not, and alpha blending them would sort badly
                // against everything seen through the catwalk.
                material.SetFloat("_AlphaClip", 1.0f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;
            }
        }

        private static Vector3 ComputeNormal(List<Vector3> vertices, int start, int count)
        {
            // Newell's method: robust for polygons that are nearly degenerate,
            // where picking any three points can give a zero or wildly wrong
            // cross product.
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Vector3 current = vertices[start + i];
                Vector3 next = vertices[start + (i + 1) % count];
                normal.x += (current.y - next.y) * (current.z + next.z);
                normal.y += (current.z - next.z) * (current.x + next.x);
                normal.z += (current.x - next.x) * (current.y + next.y);
            }
            return normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
        }

        /// <summary>
        /// Source is Z-up and right-handed; Unity is Y-up and left-handed.
        /// Swapping Y and Z does both conversions at once -- the axis swap
        /// flips handedness, which is exactly what is needed, and it leaves
        /// triangle winding correct so faces are not all inside out.
        /// </summary>
        public static Vector3 SourceToUnity(Vector3 source)
        {
            return new Vector3(source.x, source.z, source.y) * UnitsToMetres;
        }

        private static void AddSpawnMarkers(SourceBsp bsp, GameObject root)
        {
            GameObject spawns = new GameObject("Spawns");
            spawns.transform.SetParent(root.transform, false);

            foreach (System.Text.RegularExpressions.Match block in
                System.Text.RegularExpressions.Regex.Matches(bsp.EntityText, @"\{[^{}]*\}"))
            {
                string body = block.Value;
                var classMatch = System.Text.RegularExpressions.Regex.Match(body, "\"classname\"\\s*\"([^\"]+)\"");
                if (!classMatch.Success)
                {
                    continue;
                }
                string className = classMatch.Groups[1].Value;
                if (className != "info_player_terrorist" && className != "info_player_counterterrorist")
                {
                    continue;
                }

                var originMatch = System.Text.RegularExpressions.Regex.Match(body, "\"origin\"\\s*\"([^\"]+)\"");
                if (!originMatch.Success)
                {
                    continue;
                }
                string[] parts = originMatch.Groups[1].Value.Split(' ');
                if (parts.Length < 3)
                {
                    continue;
                }
                Vector3 origin = new Vector3(
                    float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));

                GameObject marker = new GameObject(className);
                marker.transform.SetParent(spawns.transform, false);
                marker.transform.localPosition = SourceToUnity(origin);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
