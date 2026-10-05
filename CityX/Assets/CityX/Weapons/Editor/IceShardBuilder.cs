using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The ice shard prefab: a faceted crystal 0.9 m long, flat-shaded (every face its own normal, so it glitters rather than reads as a
    /// smooth rod), a hexagonal section squashed thin on one axis so it is a blade of ice rather than a pencil, pointed at the front and
    /// chamfered off flat at the back. The prefab's ORIGIN IS THE TIP and it points down +Z, so where it strikes is where its origin goes.
    /// Pale blue, glossy, with a cold glow of its own so it still reads in the dark, and a short additive frost trail while it flies.
    /// </summary>
    public static class IceShardBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/IceShard.prefab";
        private const string MeshPath = "Assets/CityX/Weapons/IceShardMesh.asset";
        private const string MaterialPath = "Assets/CityX/Weapons/IceShard.mat";
        private const string TrailMaterialPath = "Assets/CityX/Weapons/IceShardTrail.mat";

        public static readonly Color IceColour = new Color(0.8f, 0.93f, 1.0f, 1.0f);
        public static readonly Color GlowColour = new Color(0.35f, 0.7f, 1.0f, 1.0f);

        [MenuItem("CityX/Rebuild Ice Shard")]
        public static GameObject Build()
        {
            var root = new GameObject("IceShard");
            try
            {
                var crystal = new GameObject("Crystal");
                crystal.transform.SetParent(root.transform, false);
                crystal.AddComponent<MeshFilter>().sharedMesh = BuildMesh();
                MeshRenderer renderer = crystal.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = EnsureIceMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                // The frost streaming off the back while it flies (stopped when it sticks).
                var trailObject = new GameObject("Trail");
                trailObject.transform.SetParent(root.transform, false);
                trailObject.transform.localPosition = new Vector3(0.0f, 0.0f, -0.75f);
                TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = EnsureTrailMaterial();
                trail.time = 0.22f;
                trail.minVertexDistance = 0.08f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));
                trail.widthMultiplier = 0.2f;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.alignment = LineAlignment.View;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(new Color(0.85f, 0.95f, 1.0f), 0.0f), new GradientColorKey(new Color(0.35f, 0.7f, 1.0f), 1.0f) },
                    new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
                trail.colorGradient = gradient;
                trail.emitting = true;

                root.AddComponent<IceShardProjectile>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Four rings along the length: the tip (a point), the shoulder (0.2 back, full width), the body (0.62 back, a touch wider) and
        /// the butt (0.9 back, narrowed). Each ring is a hexagon with alternating radii so no two faces match. Flat-shaded.
        /// </summary>
        private const string ModelPath = "Assets/CityX/Weapons/IceShardModel.glb";

        /// <summary>The shard's length in metres (tip to butt) when it is made from the model.</summary>
        private const float ModelLengthMetres = 1.1f;

        /// <summary>Vertices closer than this (metres, in the model's own space after scaling) are welded: the model is 21,000 vertices and there can be dozens of shards out at once.</summary>
        private const float WeldMetres = 0.012f;

        /// <summary>
        /// THE USER'S MODEL (Ice shard.glb) if it is in the project, else the mesh already baked from it, else the procedural crystal below. The
        /// model's long axis is found, its SHARP end (the end whose last 15% is the narrower) is made the front, so it hits first, with the TIP AT THE ORIGIN
        /// pointing down +Z (the shard's convention: where it strikes is where its origin goes), scaled to a length, welded down to a lighter mesh,
        /// and baked into IceShardMesh.asset so the glb itself is not needed at runtime.
        /// </summary>
        private static Mesh BuildMesh()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            MeshFilter source = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
            if (source != null && source.sharedMesh != null)
            {
                return BakeModel(source.sharedMesh);
            }
            var baked = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (baked != null && baked.vertexCount > 200)
            {
                return baked;   // already made from the model on an earlier build
            }
            return BuildProceduralMesh();
        }

        private static Mesh BakeModel(Mesh source)
        {
            Vector3[] vertices = source.vertices;
            int[] triangles = source.triangles;
            Vector3 size = source.bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
            float low = source.bounds.min[axis], span = size[axis];

            // The sharp end is the narrower one: compare how wide the vertices in the last 15% at each end spread.
            float WidthAt(bool high)
            {
                float min1 = float.MaxValue, max1 = float.MinValue, min2 = float.MaxValue, max2 = float.MinValue;
                foreach (Vector3 p in vertices)
                {
                    float t = (p[axis] - low) / span;
                    if (high ? t < 0.85f : t > 0.15f) continue;
                    min1 = Mathf.Min(min1, p[a1]); max1 = Mathf.Max(max1, p[a1]);
                    min2 = Mathf.Min(min2, p[a2]); max2 = Mathf.Max(max2, p[a2]);
                }
                return (max1 - min1) * (max2 - min2);
            }
            bool tipIsHigh = WidthAt(true) < WidthAt(false);
            float tipCoordinate = tipIsHigh ? low + span : low;
            float sign = tipIsHigh ? -1.0f : 1.0f;   // distance back from the tip, as a negative Z
            float scale = ModelLengthMetres / span;
            // Centre the cross-section on the long axis.
            float centre1 = source.bounds.center[a1], centre2 = source.bounds.center[a2];

            // Weld by snapping to a grid (averaging the positions in each cell): fewer vertices, same shape.
            var cellOf = new System.Collections.Generic.Dictionary<Vector3Int, int>();
            var sums = new System.Collections.Generic.List<Vector3>();
            var counts = new System.Collections.Generic.List<int>();
            var remap = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                // x, y across, z along (tip 0, body negative); a cyclic permutation keeps the handedness, and a flip of the direction along is paired with a flip of x.
                Vector3 q = new Vector3((p[a1] - centre1) * (tipIsHigh ? -1.0f : 1.0f), p[a2] - centre2, (p[axis] - tipCoordinate) * (tipIsHigh ? 1.0f : -1.0f)) * scale;
                var cell = new Vector3Int(Mathf.RoundToInt(q.x / WeldMetres), Mathf.RoundToInt(q.y / WeldMetres), Mathf.RoundToInt(q.z / WeldMetres));
                if (!cellOf.TryGetValue(cell, out int index))
                {
                    index = sums.Count;
                    cellOf[cell] = index;
                    sums.Add(Vector3.zero);
                    counts.Add(0);
                }
                sums[index] += q;
                counts[index]++;
                remap[i] = index;
            }
            var welded = new Vector3[sums.Count];
            for (int i = 0; i < welded.Length; i++)
            {
                welded[i] = sums[i] / counts[i];
            }
            var kept = new System.Collections.Generic.List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = remap[triangles[t]], b = remap[triangles[t + 1]], c = remap[triangles[t + 2]];
                if (a == b || b == c || a == c)
                {
                    continue;
                }
                kept.Add(a); kept.Add(b); kept.Add(c);
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            mesh.Clear();
            mesh.name = "IceShardMesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = welded;
            mesh.triangles = kept.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            Debug.Log($"CityX: ice shard model baked: {source.vertexCount} -> {welded.Length} vertices, {source.triangles.Length / 3} -> {kept.Count / 3} triangles; tip end was the {(tipIsHigh ? "high" : "low")} end of axis {axis}; bounds {mesh.bounds.size:0.00}");
            return mesh;
        }

        private static Mesh BuildProceduralMesh()
        {
            const int sides = 6;
            float[] z = { 0.0f, -0.20f, -0.62f, -0.90f };
            float[] radius = { 0.0f, 0.075f, 0.09f, 0.05f };
            float thin = 0.55f;   // squashed on X

            Vector3 Point(int ring, int side)
            {
                if (ring == 0)
                {
                    return new Vector3(0.0f, 0.0f, z[0]);
                }
                float angle = (side + (ring == 2 ? 0.35f : 0.0f)) * Mathf.PI * 2.0f / sides;
                float r = radius[ring] * (side % 2 == 0 ? 1.0f : 0.78f);
                return new Vector3(Mathf.Cos(angle) * r * thin, Mathf.Sin(angle) * r, z[ring]);
            }

            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            // The faces below are written counter-clockwise looking from outside; Unity's front faces are clockwise, so each is added reversed.
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.Add(a);
                vertices.Add(c);
                vertices.Add(b);
                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 2);
            }

            for (int s = 0; s < sides; s++)
            {
                int n = (s + 1) % sides;
                // Tip to shoulder.
                Tri(Point(0, 0), Point(1, n), Point(1, s));
                // Shoulder to body, body to butt: two triangles a face.
                for (int ring = 1; ring <= 2; ring++)
                {
                    Vector3 a = Point(ring, s), b = Point(ring, n), c = Point(ring + 1, n), d = Point(ring + 1, s);
                    Tri(a, b, c);
                    Tri(a, c, d);
                }
                // The butt, a flat cap.
                Tri(new Vector3(0.0f, 0.0f, z[3]), Point(3, s), Point(3, n));
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            mesh.Clear();
            mesh.name = "IceShardMesh";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();   // each triangle has its own three vertices, so this is flat shading
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material EnsureIceMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", IceColour);
            material.SetFloat("_Smoothness", 0.95f);
            material.SetFloat("_Metallic", 0.0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", GlowColour * 1.0f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureTrailMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                AssetDatabase.CreateAsset(material, TrailMaterialPath);
            }
            material.shader = Shader.Find("Legacy Shaders/Particles/Additive");
            // This shader draws 2 x tint x vertex colour, so the tint is half the wanted brightness.
            material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 1.0f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
