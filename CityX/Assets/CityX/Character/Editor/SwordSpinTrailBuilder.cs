using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The sword spin's swoosh prefab: three flat arcs of light, one inside the other, thin and fading at the tail and wide and bright
    /// where the blade is, sharing the sword wave's streak texture so they read as the same kind of light. Each lies in the plane
    /// of the sword arm, in the body's own space with the blade at +X (the right) -- the swoosh trails round behind it -- and is a
    /// shallow dish rather than a flat sheet so it still shows when the camera is level with it. White for now, additive, no collider.
    /// </summary>
    public static class SwordSpinTrailBuilder
    {
        private const string PrefabPath = "Assets/CityX/Character/SwordSpinTrail.prefab";
        private const string MaterialPath = "Assets/CityX/Character/SwordSpinTrail.mat";
        private const string TexturePath = "Assets/CityX/Weapons/SwordWaveStreaks.png";

        public static readonly Color TrailColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        private const float Intensity = 1.1f;

        [MenuItem("CityX/Rebuild Sword Spin Trail")]
        public static GameObject Build()
        {
            var root = new GameObject("SwordSpinTrail");
            try
            {
                Material material = EnsureMaterial();
                AddLayer(root.transform, "Main", BuildArc(2.75f, 1.75f, 235.0f, 0.12f, 0.0f, "SwordSpinTrailMain"), material);
                AddLayer(root.transform, "Inner", BuildArc(2.6f, 1.0f, 170.0f, 0.12f, 0.06f, "SwordSpinTrailInner"), material);
                AddLayer(root.transform, "Faint", BuildArc(2.5f, 0.6f, 120.0f, 0.12f, -0.06f, "SwordSpinTrailFaint"), material);
                root.AddComponent<SwordSpinTrail>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddLayer(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;   // the trail component shows it
        }

        /// <summary>
        /// An arc of ring about the up axis. The blade end is at angle 0 (+X); the arc runs back from there through
        /// <paramref name="sweepDegrees"/> (toward -Z). u runs from the faded tail (0) to the blade (1), v from the inner edge (0) to the
        /// outer leading edge (1); the band is narrow at the tail and <paramref name="width"/> wide at the blade, and lifts toward its
        /// outer edge by <paramref name="dish"/> metres per metre.
        /// </summary>
        private static Mesh BuildArc(float outerRadius, float width, float sweepDegrees, float dish, float height, string name)
        {
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 12];
            float sweep = sweepDegrees * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = -sweep * (1.0f - u);
                float w = width * Mathf.Pow(u, 1.2f);
                float inner = outerRadius - w;
                Vector3 outerPoint = new Vector3(Mathf.Cos(angle) * outerRadius, height + dish * (outerRadius - 1.0f), Mathf.Sin(angle) * outerRadius);
                Vector3 innerPoint = new Vector3(Mathf.Cos(angle) * inner, height + dish * (inner - 1.0f), Mathf.Sin(angle) * inner);
                vertices[i * 2] = innerPoint;
                vertices[i * 2 + 1] = outerPoint;
                uvs[i * 2] = new Vector2(u, 0.0f);
                uvs[i * 2 + 1] = new Vector2(u, 1.0f);
            }
            int k = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
            }

            string path = $"Assets/CityX/Character/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.name = name;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find("Legacy Shaders/Particles/Additive");
            var streaks = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (streaks != null)
            {
                material.SetTexture("_MainTex", streaks);
            }
            // This shader draws 2 x tint x texture, so the tint is half the wanted brightness.
            material.SetColor("_TintColor", new Color(TrailColour.r * Intensity * 0.5f, TrailColour.g * Intensity * 0.5f, TrailColour.b * Intensity * 0.5f, 1.0f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
