using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The sword wave prefab: a SWOOSH, not a solid crescent -- a curved ribbon whose texture is bright streaks of broken brush strokes running along
    /// it, thin and trailing at one end, wide and blunt and blazing at the other, with two thinner copies trailing behind it for motion. Unlit and
    /// additive so it glows (white for now; the colour is one number on the material), a trigger on a gravity-free rigid body, and a small light
    /// riding with it.
    /// </summary>
    public static class SwordWaveBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/SwordWave.prefab";
        private const string TexturePath = "Assets/CityX/Weapons/SwordWaveStreaks.png";
        private const string MaterialPath = "Assets/CityX/Weapons/SwordWave.mat";

        /// <summary>The wave's colour: white for now. Above 1 per channel so the post-process bloom makes it glow.</summary>
        public static readonly Color WaveColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        private const float Intensity = 1.7f;

        [MenuItem("CityX/Rebuild Sword Wave")]
        public static GameObject Build()
        {
            var root = new GameObject("SwordWave");
            try
            {
                Material material = EnsureMaterial(EnsureTexture());

                // A little diagonal, like a slash that came down across the body, not a perfectly upright arc.
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 18.0f);

                AddLayer(visual.transform, "Main", BuildSwoosh(1.9f, 74.0f, 0.95f, 0.0f, "SwordWaveMain"), material);
                AddLayer(visual.transform, "TrailA", BuildSwoosh(1.8f, 66.0f, 0.55f, 0.45f, "SwordWaveTrailA"), material);
                AddLayer(visual.transform, "TrailB", BuildSwoosh(1.7f, 58.0f, 0.35f, 0.95f, "SwordWaveTrailB"), material);

                var lightObject = new GameObject("Glow");
                lightObject.transform.SetParent(root.transform, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.9f, 0.95f, 1.0f);
                light.range = 7.0f;
                light.intensity = 3.0f;
                light.shadows = LightShadows.None;

                var collider = root.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(0.5f, 3.2f, 0.7f);
                collider.center = new Vector3(0.0f, 0.0f, -0.1f);

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                root.AddComponent<SwordWaveProjectile>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: sword wave written to {PrefabPath}");
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
        }

        /// <summary>
        /// A ribbon along an arc standing upright in the YZ plane, the arc bulging along +Z (the way it flies). u runs along it from the thin trailing
        /// tip (0) to the blunt blazing head (1); v across it from the inner edge (0) to the leading outer edge (1). The ribbon is nothing at the
        /// tip and widest at the head, where it stops dead.
        /// </summary>
        private static Mesh BuildSwoosh(float radius, float halfAngleDegrees, float width, float setBack, string name)
        {
            const int segments = 36;
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 12];   // both faces
            float half = halfAngleDegrees * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = Mathf.Lerp(-half, half, u);
                float w = width * Mathf.Pow(u, 1.15f);   // thin tail, wide head
                var edge = new Vector3(0.0f, radius * Mathf.Sin(angle), radius * Mathf.Cos(angle) - radius * 0.6f - setBack);
                vertices[i * 2] = edge - new Vector3(0.0f, 0.0f, w);   // inner
                vertices[i * 2 + 1] = edge;                            // outer, leading
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

            string path = $"Assets/CityX/Weapons/{name}.asset";
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

        // ---- the streaks ----

        /// <summary>The usual smoothstep: 0 below edge0, 1 above edge1, smooth between (Mathf.SmoothStep is a blend between two values, not this).</summary>
        private static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0f - 2.0f * t);
        }

        private static float Hash(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFF) / 65535.0f;
            }
        }

        /// <summary>Smooth 1D value noise, so a stroke's breaks are soft rather than random pixels.</summary>
        private static float Noise(float x, int salt)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            f = f * f * (3.0f - 2.0f * f);
            return Mathf.Lerp(Hash(i * 31 + salt), Hash((i + 1) * 31 + salt), f);
        }

        private static Texture2D EnsureTexture()
        {
            const int width = 512, height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;

                    // Three bold bands (the stripes of the blunt head), each broken into brush strokes along its length.
                    float bands = 0.0f;
                    for (int band = 0; band < 3; band++)
                    {
                        float centre = 0.22f + 0.28f * band;
                        float thickness = 0.075f + 0.02f * band;
                        float across = Mathf.Exp(-Mathf.Pow((v - centre) / thickness, 2.0f));
                        float stroke = Edge(0.30f, 0.55f, Noise(u * 7.0f + band * 11.0f, 5 + band));
                        // Strokes are longer and more solid toward the head.
                        stroke = Mathf.Lerp(stroke, 1.0f, Edge(0.55f, 1.0f, u));
                        bands += across * stroke;
                    }

                    // Fine hairlines racing along the length.
                    float hair = Edge(0.62f, 0.9f, Noise(v * 38.0f, 91)) * Edge(0.35f, 0.7f, Noise(u * 13.0f + v * 5.0f, 17)) * 0.55f;

                    // The bright leading edge, continuous from tip to head.
                    float rim = Mathf.Exp(-Mathf.Pow((v - 0.965f) / 0.028f, 2.0f)) * 0.8f;

                    // The head blazes.
                    float head = Edge(0.8f, 1.0f, u) * (0.12f + 0.5f * v);

                    float value = Mathf.Pow(Mathf.Clamp01(bands + hair + rim + head), 2.2f);   // contrast: dark gaps between the bright strokes

                    // Soft at the inner edge, and gone toward the thin tail so it trails off rather than ending.
                    value *= Edge(0.0f, 0.08f, v) * Edge(0.0f, 0.18f, u);
                    pixels[y * width + x] = new Color(1.0f, 1.0f, 1.0f, value);
                }
            }
            float sum = 0.0f, max = 0.0f;
            foreach (Color pixel in pixels) { sum += pixel.a; max = Mathf.Max(max, pixel.a); }
            Debug.Log($"CityX: sword wave streak texture alpha average {sum / pixels.Length:0.000}, brightest {max:0.00}");
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material EnsureMaterial(Texture2D streaks)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            // Additive: the glow is added to what is behind. (This shader draws 2 x tint x texture, so the tint is half the wanted brightness.)
            material.shader = Shader.Find("Legacy Shaders/Particles/Additive");
            material.SetTexture("_MainTex", streaks);
            var tint = new Color(WaveColour.r * Intensity * 0.5f, WaveColour.g * Intensity * 0.5f, WaveColour.b * Intensity * 0.5f, 1.0f);
            material.SetColor("_TintColor", tint);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
