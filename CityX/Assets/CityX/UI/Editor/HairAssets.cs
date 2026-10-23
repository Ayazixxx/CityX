using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.UIEditor
{
    /// <summary>
    /// The hair meshes every builder wires onto a head, in one place, in
    /// <see cref="CharacterHair.ValidStyles"/> order (after "none").
    ///
    /// The creator, the character list and the in-game player all need the same
    /// list. A new style added here shows up in all of them at once.
    /// </summary>
    public static class HairAssets
    {
        public const string Folder = "Assets/CityX/Character/Hair";
        public const string Hair1Source = Folder + "/Hair1.glb";
        public const string Hair1Baked = Folder + "/Hair1Mesh.asset";
        public const string DannySource = Folder + "/Danny.glb";
        public const string DannyBaked = Folder + "/DannyMesh.asset";
        public const string AnimeHairSource = Folder + "/AnimeHair.glb";
        public const string AnimeHairBaked = Folder + "/AnimeHairMesh.asset";
        public const string ChadHairSource = Folder + "/ChadHair.glb";
        public const string ChadHairBaked = Folder + "/ChadHairMesh.asset";
        public const string OldManHairSource = Folder + "/OldManHair.glb";
        public const string OldManHairBaked = Folder + "/OldManHairMesh.asset";
        public const string SpikeXSource = Folder + "/SpikeX.glb";
        public const string SpikeXBaked = Folder + "/SpikeXMesh.asset";
        public const string SwipeCutSource = Folder + "/SwipeCut.glb";
        public const string SwipeCutBaked = Folder + "/SwipeCutMesh.asset";
        public const string IdkNeedsWorkSource = Folder + "/IdkNeedsWork.glb";
        public const string IdkNeedsWorkBaked = Folder + "/IdkNeedsWorkMesh.asset";
        public const string ChadHair2Source = Folder + "/ChadHair2.glb";
        public const string ChadHair2Baked = Folder + "/ChadHair2Mesh.asset";
        public const string HydeSource = Folder + "/Hyde.glb";
        public const string HydeBaked = Folder + "/HydeMesh.asset";
        public const string DreadsSource = Folder + "/Dreads.glb";
        public const string DreadsBaked = Folder + "/DreadsMesh.asset";

        /// <summary>Where the saved hand-tuned fits live: a Resources folder, so every head reads them with nothing wired.</summary>
        public const string FitSettingsPath = HairFitSettings.AssetPath;

        /// <summary>
        /// The saved-fits asset, made empty the first time. An empty one changes nothing:
        /// every style then uses its automatic fit until somebody saves one.
        /// </summary>
        public static HairFitSettings EnsureFitSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HairFitSettings>(FitSettingsPath);
            if (existing != null)
            {
                return existing;
            }
            System.IO.Directory.CreateDirectory("Assets/CityX/Character/Resources");
            var created = ScriptableObject.CreateInstance<HairFitSettings>();
            AssetDatabase.CreateAsset(created, FitSettingsPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>
        /// Puts a CharacterHair on a body's head bone with every style wired, for the
        /// bodies that did not have one -- the player in the world. The head bone is
        /// the one named "head", like every other place hair is built.
        /// </summary>
        public static CharacterHair AddTo(GameObject body)
        {
            EnsureFitSettings();
            Transform head = null;
            foreach (Transform child in body.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "head")
                {
                    head = child;
                    break;
                }
            }
            if (head == null)
            {
                Debug.LogWarning("CityX: no head bone on the body -- it will not wear hair.");
                return null;
            }

            var hair = body.GetComponent<CharacterHair>() ?? body.AddComponent<CharacterHair>();
            hair.Head = head;
            hair.StyleMeshes = StyleMeshes();
            hair.HairMaterial = CharacterCreatorBuilder.HairMaterial();
            return hair;
        }

        /// <summary>
        /// The mesh of every real style, in ValidStyles order after "none". A missing one is null.
        ///
        /// Built by walking ValidStyles rather than listing meshes by hand, so a style added to
        /// that list cannot end up with another style's mesh (or none) because the two lists
        /// were edited in a different order.
        /// </summary>
        public static Mesh[] StyleMeshes()
        {
            // Every builder that wires hair gets the saved-fits asset too, so the Inspector's Save
            // button has somewhere to write whichever scene is opened first.
            EnsureFitSettings();

            var meshes = new Mesh[CharacterHair.ValidStyles.Length - 1];
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i] = MeshFor(CharacterHair.ValidStyles[i + 1]);
            }
            return meshes;
        }

        /// <summary>The mesh a style wears, or null (with a warning) if its model is missing.</summary>
        public static Mesh MeshFor(string style)
        {
            switch (style)
            {
                case CharacterHair.Hair1: return Hair1();
                case CharacterHair.Danny: return Danny();
                case CharacterHair.AnimeHair: return AnimeHair();
                case CharacterHair.ChadHair: return BakedFromModel(ChadHairSource, ChadHairBaked, "ChadHairMesh");
                case CharacterHair.OldManHair: return BakedFromModel(OldManHairSource, OldManHairBaked, "OldManHairMesh");
                case CharacterHair.SpikeX: return BakedFromModel(SpikeXSource, SpikeXBaked, "SpikeXMesh");
                case CharacterHair.SwipeCut: return BakedFromModel(SwipeCutSource, SwipeCutBaked, "SwipeCutMesh");
                case CharacterHair.IdkNeedsWork: return BakedFromModel(IdkNeedsWorkSource, IdkNeedsWorkBaked, "IdkNeedsWorkMesh");
                case CharacterHair.ChadHair2: return BakedFromModel(ChadHair2Source, ChadHair2Baked, "ChadHair2Mesh");
                case CharacterHair.Hyde: return BakedFromModel(HydeSource, HydeBaked, "HydeMesh");
                case CharacterHair.Dreads: return BakedFromModel(DreadsSource, DreadsBaked, "DreadsMesh");
                default:
                    Debug.LogWarning($"CityX: no model is wired for the hairstyle \"{style}\" -- add it to HairAssets.MeshFor.");
                    return null;
            }
        }

        /// <summary>Hair1's mesh, baked (see <see cref="BakedFromModel"/>).</summary>
        public static Mesh Hair1() => BakedFromModel(Hair1Source, Hair1Baked, "Hair1Mesh");

        /// <summary>
        /// Danny's mesh, baked (see <see cref="BakedFromModel"/>). Same single-mesh-under-one-node shape as
        /// Hair1 (Blender's own glTF exporter, one node carrying a scale and a lift), but a much denser mesh --
        /// 37,478 vertices and 73,675 triangles against Hair1's ~800, worth knowing before this style shows up
        /// on every head in a crowd (see the standing "keep asset size small" practice).
        /// </summary>
        public static Mesh Danny() => BakedFromModel(DannySource, DannyBaked, "DannyMesh");

        /// <summary>AnimeHair's mesh, baked (see <see cref="BakedFromModel"/>) -- 2026-09-27, a separate model from
        /// the old, removed AnimeHair0 despite the similar name.</summary>
        public static Mesh AnimeHair() => BakedFromModel(AnimeHairSource, AnimeHairBaked, "AnimeHairMesh");

        /// <summary>
        /// A hair model's mesh, with the glTF node's own transform BAKED into it.
        ///
        /// A hair model is one mesh under one node that scales and lifts it (Hair1: 1.41x and
        /// 1.32m) -- so the same file, dropped into a scene as a prefab, sits somewhere very
        /// different from where its raw vertices are. A hair mesh has to be the same thing
        /// whichever way it is loaded, so the node's transform is applied to the vertices once,
        /// here, and saved as a plain mesh asset. From then on the mesh is in the model's own
        /// metre-sized space and CharacterHair's fit does the rest.
        ///
        /// Made once and kept; delete the baked asset to bake it again from a changed model.
        /// </summary>
        private static Mesh BakedFromModel(string sourcePath, string bakedPath, string meshName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(bakedPath);
            if (existing != null)
            {
                return existing;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (model == null)
            {
                Debug.LogWarning($"CityX: {sourcePath} is missing -- that hairstyle will not be available.");
                return null;
            }

            MeshFilter filter = model.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning($"CityX: {sourcePath} has no mesh -- that hairstyle will not be available.");
                return null;
            }

            // The mesh node's OWN transform, all the way up: when the file has a single node the
            // imported root IS that node, so measuring the mesh relative to the root would cancel
            // the very scale and lift that has to be baked in.
            Matrix4x4 bake = filter.transform.localToWorldMatrix;
            Mesh baked = Bake(filter.sharedMesh, bake);
            baked.name = meshName;
            if (bakedPath == DreadsBaked)
            {
                AddScalp(baked, CharacterHair.Dreads);
            }
            else if (bakedPath == HydeBaked)
            {
                FillWithCurl(baked, CharacterHair.Hyde, HydeCurlFrom, HydeCurlTo, HydeCurlTop, HydeCurlTurn);
            }
            AssetDatabase.CreateAsset(baked, bakedPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: baked {bakedPath} -- bounds {baked.bounds.min} to {baked.bounds.max}");
            return baked;
        }

        /// <summary>The dreads baked again (with their scalp cap, placed by their saved fit) into the same asset, so every head wearing them keeps it.</summary>
        [MenuItem("CityX/Rebake Dreads")]
        public static void RebakeDreads() => Rebake(DreadsSource, DreadsBaked, "DreadsMesh");

        /// <summary>HYDE baked again into the same asset: the model alone, no scalp cap (owner, 2026-10-23: "remove the scalp cap entirely").</summary>
        [MenuItem("CityX/Rebake Hyde")]
        public static void RebakeHyde() => Rebake(HydeSource, HydeBaked, "HydeMesh");

        private static void Rebake(string sourcePath, string bakedPath, string meshName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(bakedPath);
            string temporary = bakedPath.Replace(".asset", "Rebake.asset");
            if (existing != null)
            {
                AssetDatabase.MoveAsset(bakedPath, temporary);
            }
            Mesh fresh = BakedFromModel(sourcePath, bakedPath, meshName);
            if (existing != null && fresh != null)
            {
                EditorUtility.CopySerialized(fresh, existing);
                AssetDatabase.DeleteAsset(bakedPath);
                AssetDatabase.MoveAsset(temporary, bakedPath);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// The notch in Hyde's lower edge on the left (the owner's gap: the edge jumps from 29 to 10 degrees under the head's middle between
        /// 264 and 268 degrees round): filled with the curl behind it, 254-263 degrees, its lower part turned this far forward into it.
        /// </summary>
        private const float HydeCurlFrom = 251.0f;
        private const float HydeCurlTo = 264.0f;
        private const float HydeCurlTop = 10.0f;
        private const float HydeCurlTurn = 8.5f;

        /// <summary>A curl of a style copied round the head (HairEdgeFill.CopyCurl), in the head's space as the saved fit wears it.</summary>
        private static void FillWithCurl(Mesh mesh, string style, float fromTurn, float toTurn, float topHeight, float byTurn)
        {
            if (!EnsureFitSettings().TryGet(style, out CharacterHair.Fit fit))
            {
                Debug.LogWarning($"CityX: \"{style}\" has no saved fit -- its edge is left as it is.");
                return;
            }
            Matrix4x4 worn = Matrix4x4.TRS(fit.Position, Quaternion.Euler(fit.Euler), fit.Scale);
            Matrix4x4 back = worn.inverse;
            var vertices = new System.Collections.Generic.List<Vector3>(mesh.vertices);
            var normals = new System.Collections.Generic.List<Vector3>(mesh.normals);
            var uvs = new System.Collections.Generic.List<Vector2>(mesh.uv);
            var triangles = new System.Collections.Generic.List<int>(mesh.triangles);
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = worn.MultiplyPoint3x4(vertices[i]);
                normals[i] = worn.MultiplyVector(normals[i]).normalized;
            }
            int copied = HairEdgeFill.CopyCurl(vertices, normals, uvs.Count > 0 ? uvs : null, triangles, FusedBodyShape.HeadRadius, fromTurn, toTurn,
                topHeight, byTurn);
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = back.MultiplyPoint3x4(vertices[i]);
                normals[i] = back.MultiplyVector(normals[i]).normalized;
            }
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : mesh.indexFormat;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            if (uvs.Count > 0)
            {
                mesh.SetUVs(0, uvs);
            }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Debug.Log($"CityX: {style}: {copied} triangles of the curl at {fromTurn:0}-{toTurn:0} degrees copied {byTurn:0} degrees round");
        }

        /// <summary>
        /// A thin cap of hair over the scalp (HairScalp) merged into <paramref name="mesh"/>: made in the head's space and brought into the
        /// mesh's own through <paramref name="style"/>'s saved fit, so it lies on the head wherever the fit puts the hair.
        /// </summary>
        private static void AddScalp(Mesh mesh, string style)
        {
            if (!EnsureFitSettings().TryGet(style, out CharacterHair.Fit fit))
            {
                Debug.LogWarning($"CityX: \"{style}\" has no saved fit -- its scalp cap is left off.");
                return;
            }
            Matrix4x4 intoMesh = Matrix4x4.TRS(fit.Position, Quaternion.Euler(fit.Euler), fit.Scale).inverse;
            var capVertices = new System.Collections.Generic.List<Vector3>();
            var capNormals = new System.Collections.Generic.List<Vector3>();
            var capTriangles = new System.Collections.Generic.List<int>();
            HairScalp.Build(FusedBodyShape.HeadRadius, capVertices, capNormals, capTriangles);

            var vertices = new System.Collections.Generic.List<Vector3>(mesh.vertices);
            var normals = new System.Collections.Generic.List<Vector3>(mesh.normals);
            var uvs = new System.Collections.Generic.List<Vector2>(mesh.uv);
            var triangles = new System.Collections.Generic.List<int>(mesh.triangles);
            int first = vertices.Count;
            for (int i = 0; i < capVertices.Count; i++)
            {
                vertices.Add(intoMesh.MultiplyPoint3x4(capVertices[i]));
                normals.Add(intoMesh.MultiplyVector(capNormals[i]).normalized);
                if (uvs.Count > 0)
                {
                    uvs.Add(Vector2.zero);
                }
            }
            foreach (int t in capTriangles)
            {
                triangles.Add(first + t);
            }
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : mesh.indexFormat;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            if (uvs.Count > 0)
            {
                mesh.SetUVs(0, uvs);
            }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>A copy of a mesh with a transform applied to its vertices (and its normals turned to match).</summary>
        public static Mesh Bake(Mesh source, Matrix4x4 transform)
        {
            var mesh = new Mesh { indexFormat = source.indexFormat };

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = transform.MultiplyPoint3x4(vertices[i]);
            }
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = transform.MultiplyVector(normals[i]).normalized;
            }

            mesh.vertices = vertices;
            if (normals.Length == vertices.Length)
            {
                mesh.normals = normals;
            }
            mesh.uv = source.uv;
            // ONE sub-mesh, whatever the model had. Hair is drawn by a renderer with a single shared material, so a
            // model exported with several materials (AnimeHair.glb: a tiny first primitive and the real hair second)
            // would show only its first primitive -- a bald head. All sub-meshes share one vertex buffer, so merging
            // is just joining the index lists.
            var triangles = new System.Collections.Generic.List<int>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                triangles.AddRange(source.GetTriangles(s));
            }
            mesh.subMeshCount = 1;
            mesh.SetTriangles(triangles, 0);
            if (normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
