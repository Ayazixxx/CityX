using System.Collections.Generic;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the farmer hat from a Meshy AI "Straw Hat Wanderer" export --
    /// A WHOLE CHARACTER, NOT JUST A HAT, confirmed by rendering the raw
    /// source directly: a plain blob-limbed body wearing the hat, both
    /// baked into the same single 1,613,954-vertex mesh/material. This is
    /// exactly the situation the user's own request anticipated ("separate
    /// this hat from the model") -- an earlier pass at this file assumed a
    /// single-node glTF meant nothing to separate, which the render proved
    /// wrong.
    ///
    /// CUT BY HEIGHT, NOT BONE WEIGHT -- unlike MidnightPatchworkCoatBuilder's
    /// own source, this mesh carries no skin at all (boneWeights.Length == 0,
    /// confirmed directly), so there is no per-vertex dominant-bone signal to
    /// key off. A connected-component pass was tried first and rejected: it
    /// produced 2122 components of a few thousand vertices apiece across the
    /// WHOLE model, body included, meaning this mesh's vertices are split
    /// along UV-chart seams, not semantic parts -- connectivity tracks the
    /// texture atlas here, not "hat vs body". A per-Y-band histogram of the
    /// maximum horizontal radius found the real signal instead: a sharp
    /// cliff at y=0.416-0.448 in the source's own local space (radius drops
    /// 0.567 -> 0.214, vertex density drops 6.5x) -- the hat's brim and
    /// straw fringe sit entirely above it, the head sphere and body entirely
    /// below. CutY sits in the middle of that gap.
    ///
    /// THE EMBEDDED TEXTURE IS UNUSABLE FOR THE SAME REASON
    /// MidnightPatchworkCoatBuilder's own source's was -- confirmed directly:
    /// glTFast imports this file's "base_color" texture as a silently
    /// stubbed 4x4 Texture2D, not the real (verified valid, extracted and
    /// viewed directly) 4096x4096 JPEG the source .glb actually carries. So
    /// this uses a flat, untextured URP Lit material instead, colour-matched
    /// to that real JPEG's own dominant straw/khaki tone (extracted and
    /// eyeballed directly, not guessed) rather than trusting Unity's import
    /// of it.
    /// </summary>
    public static class FarmerHatBuilder
    {
        private const string GlbPath = "Assets/CityX/Wearables/Models/FarmerHat.glb";
        private const string MeshPath = "Assets/CityX/Wearables/FarmerHatMesh.asset";
        private const string PrefabPath = "Assets/CityX/Wearables/FarmerHat.prefab";
        private const string MaterialPath = "Assets/CityX/Wearables/FarmerHatStraw.mat";

        /// <summary>
        /// In the source .glb's own local space -- see this file's class
        /// comment for how this was found. Anything at or above this height
        /// is the hat; everything below it is the body being discarded.
        /// </summary>
        private const float CutY = 0.43f;

        /// <summary>
        /// Brim-to-brim diameter, scaled up the same proportion TopHat's own
        /// 0.8m brim diameter is scaled up from a real top hat's ~0.35m --
        /// this rig's whole body already reads oversized relative to a real
        /// human, and a hat sized to reality would look undersized on it. A
        /// wide-brimmed straw hat's own real diameter (~0.4-0.5m) is
        /// somewhat larger than a top hat's, so this is somewhat larger too.
        /// </summary>
        public const float TargetLength = 0.95f;

        private static readonly Color StrawColour = new Color(0.55f, 0.45f, 0.22f);

        [MenuItem("CityX/Rebuild Farmer Hat")]
        public static GameObject Build()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (model == null)
            {
                Debug.LogError($"CityX: {GlbPath} missing -- no farmer hat built.");
                return null;
            }

            MeshFilter sourceFilter = model.GetComponentInChildren<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                Debug.LogError($"CityX: {GlbPath} has no MeshFilter.");
                return null;
            }

            Mesh cutMesh = CutHatAboveY(sourceFilter.sharedMesh, MeshPath);
            if (cutMesh == null)
            {
                return null;
            }

            Material material = BuildMaterial();

            var root = new GameObject("FarmerHat");
            try
            {
                var visual = new GameObject("Straw Hat");
                visual.transform.SetParent(root.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = cutMesh;
                visual.AddComponent<MeshRenderer>().sharedMaterial = material;

                // Longest HORIZONTAL dimension only (X or Z, not Y) -- a
                // hat's own crown height shouldn't factor into normalizing
                // its brim-to-brim size, matching CarBuilder's own maxf(x, z)
                // reasoning for a car's length.
                Bounds bounds = cutMesh.bounds;
                float longestHorizontal = Mathf.Max(bounds.size.x, bounds.size.z);
                float normalizeScale = longestHorizontal > 0.0001f ? TargetLength / longestHorizontal : 1.0f;
                visual.transform.localScale = Vector3.one * normalizeScale;

                // Re-centred on the mesh's own horizontal middle, with the
                // hat's own lowest point (the brim's underside, since
                // everything below CutY was just discarded) landing at
                // root's own y=0 -- the point FarmerHat.HatOnHead
                // actually positions, same convention TopHatBuilder's
                // procedural pieces already use.
                visual.transform.localPosition = new Vector3(
                    -bounds.center.x * normalizeScale,
                    -bounds.min.y * normalizeScale,
                    -bounds.center.z * normalizeScale);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = FarmerHat.InteractRange;

                root.AddComponent<FarmerHat>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: farmer hat written to {PrefabPath} -- " +
                          $"{cutMesh.vertexCount} vertices (from {sourceFilter.sharedMesh.vertexCount} source)");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh CutHatAboveY(Mesh source, string path)
        {
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            int[] triangles = source.triangles;
            int vertexCount = positions.Length;

            var oldToNew = new int[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                oldToNew[i] = -1;
            }

            var newPositions = new List<Vector3>(vertexCount / 4);
            var newNormals = new List<Vector3>(vertexCount / 4);
            var newUVs = new List<Vector2>(vertexCount / 4);
            var newTriangles = new List<int>(triangles.Length / 4);

            int Remap(int oldIndex)
            {
                int mapped = oldToNew[oldIndex];
                if (mapped >= 0)
                {
                    return mapped;
                }
                mapped = newPositions.Count;
                oldToNew[oldIndex] = mapped;
                newPositions.Add(positions[oldIndex]);
                newNormals.Add((normals != null && normals.Length == vertexCount) ? normals[oldIndex] : Vector3.up);
                newUVs.Add((uvs != null && uvs.Length == vertexCount) ? uvs[oldIndex] : Vector2.zero);
                return mapped;
            }

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                // Only a triangle with all three corners above the cut --
                // a partial one would stretch a single vertex down across
                // the boundary into the discarded body, matching
                // MidnightPatchworkCoatBuilder's own "all three kept" rule.
                if (positions[a].y > CutY && positions[b].y > CutY && positions[c].y > CutY)
                {
                    newTriangles.Add(Remap(a));
                    newTriangles.Add(Remap(b));
                    newTriangles.Add(Remap(c));
                }
            }

            if (newTriangles.Count == 0)
            {
                Debug.LogError($"CityX: cutting {GlbPath} above y={CutY} left nothing -- the source geometry moved.");
                return null;
            }

            var mesh = new Mesh { name = "FarmerHatMesh" };
            mesh.indexFormat = newPositions.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(newPositions);
            mesh.SetNormals(newNormals);
            mesh.SetUVs(0, newUVs);
            mesh.SetTriangles(newTriangles, 0);
            mesh.RecalculateBounds();

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static Material BuildMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "FarmerHatStraw" };
            material.SetColor("_BaseColor", StrawColour);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0.0f);
            // The cut waist is a freshly-opened boundary -- back-face
            // culled (Unity's own default) it would read as a hole looking
            // up into the crown's own underside, the same fix
            // MidnightPatchworkCoatBuilder's own open seams already needed.
            material.SetFloat("_Cull", 0.0f);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(MaterialPath);
            }
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
