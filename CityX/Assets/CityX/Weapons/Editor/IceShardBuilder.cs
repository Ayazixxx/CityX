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
        private static Mesh BuildMesh()
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
