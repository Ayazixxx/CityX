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
    /// list, and each builder used to load embermane.obj by path itself. A new
    /// style added here shows up in all of them at once.
    /// </summary>
    public static class HairAssets
    {
        public const string Folder = "Assets/CityX/Character/Hair";
        public const string EmbermanePath = Folder + "/embermane.obj";
        public const string AnimeHair0Source = Folder + "/AnimeHair0.glb";
        public const string AnimeHair0Baked = Folder + "/AnimeHair0Mesh.asset";

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

        /// <summary>The mesh of every real style, in ValidStyles order after "none". A missing one is null.</summary>
        public static Mesh[] StyleMeshes()
        {
            // Every builder that wires hair gets the saved-fits asset too, so the Inspector's Save
            // button has somewhere to write whichever scene is opened first.
            EnsureFitSettings();
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<Mesh>(EmbermanePath),
                AnimeHair0(),
            };
        }

        /// <summary>Everything after the first style -- for the one place that takes the first mesh separately.</summary>
        public static Mesh[] MoreThanEmbermane()
        {
            Mesh[] all = StyleMeshes();
            var rest = new Mesh[all.Length - 1];
            System.Array.Copy(all, 1, rest, 0, rest.Length);
            return rest;
        }

        /// <summary>
        /// AnimeHair0's mesh, with the glTF node's own transform BAKED into it.
        ///
        /// The model file is one mesh under one node that scales it by 1.74 and lifts
        /// it 1.1m -- so the same file, dropped into a scene as a prefab, sits
        /// somewhere very different from where its raw vertices are. A hair mesh
        /// has to be the same thing whichever way it is loaded, so the node's
        /// transform is applied to the vertices once, here, and saved as a plain
        /// mesh asset. From then on the mesh is in the model's own metre-sized
        /// space and CharacterHair's fit does the rest.
        ///
        /// Made once and kept; delete AnimeHair0Mesh.asset to bake it again from a
        /// changed model.
        /// </summary>
        public static Mesh AnimeHair0()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(AnimeHair0Baked);
            if (existing != null)
            {
                return existing;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AnimeHair0Source);
            if (model == null)
            {
                Debug.LogWarning($"CityX: {AnimeHair0Source} is missing -- the anime hairstyle will not be available.");
                return null;
            }

            MeshFilter filter = model.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning($"CityX: {AnimeHair0Source} has no mesh -- the anime hairstyle will not be available.");
                return null;
            }

            // The mesh node's OWN transform, all the way up: when the file has a single node the
            // imported root IS that node, so measuring the mesh relative to the root would cancel
            // the very scale and lift that has to be baked in.
            Matrix4x4 bake = filter.transform.localToWorldMatrix;
            Mesh baked = Bake(filter.sharedMesh, bake);
            baked.name = "AnimeHair0Mesh";
            AssetDatabase.CreateAsset(baked, AnimeHair0Baked);
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: baked {AnimeHair0Baked} -- bounds {baked.bounds.min} to {baked.bounds.max}");
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
