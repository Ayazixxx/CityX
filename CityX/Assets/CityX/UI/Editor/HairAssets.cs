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
            AssetDatabase.CreateAsset(baked, bakedPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: baked {bakedPath} -- bounds {baked.bounds.min} to {baked.bounds.max}");
            return baked;
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
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                mesh.SetTriangles(source.GetTriangles(s), s);
            }
            if (normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
