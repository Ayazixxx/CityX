using CityX.View;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Builds the radio prefab from the boombox model Godot uses.
    ///
    /// THE MODEL IS NOT IN REAL-WORLD UNITS. Meshy exports with the longest
    /// dimension at exactly 1.0, whatever the object is -- so it arrives the
    /// size of a small car. It is scaled by measuring its own bounds and
    /// dividing, rather than by a magic number, so re-exporting the asset at a
    /// different size cannot silently break it.
    /// </summary>
    public static class RadioBuilder
    {
        private const string PrefabPath = "Assets/CityX/Radio/Radio.prefab";
        private const string ModelPath = "Assets/CityX/Radio/Models/CrimsonBoombox.obj";
        private const string TexturePath = "Assets/CityX/Radio/Models/CrimsonBoombox.png";
        private const string MaterialPath = "Assets/CityX/Radio/Models/CrimsonBoombox.mat";

        /// <summary>A plausible boombox width. Godot's own number.</summary>
        public const float TargetLength = 0.55f;

        /// <summary>How heavy it is. A portable boombox, not an appliance.</summary>
        public const float Mass = 1.6f;

        [MenuItem("CityX/Rebuild Radio")]
        public static GameObject Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"CityX: {ModelPath} missing -- no radio built.");
                return null;
            }

            var root = new GameObject("Radio");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Boombox";
                visual.transform.SetParent(root.transform, false);

                // Measured rather than assumed. The renderer bounds are in the
                // model's own units, so the longest axis over the length we
                // want IS the scale, whatever the exporter decided.
                Bounds bounds = Measure(visual);
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (longest > 0.0001f)
                {
                    visual.transform.localScale = Vector3.one * (TargetLength / longest);
                }

                Material skin = EnsureMaterial();
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = skin;
                }

                // A REAL BODY, not a static prop. Godot changed this one from
                // static to rigid because it could not be picked up otherwise,
                // and the same will be true here the moment carrying lands.
                var body = root.AddComponent<Rigidbody>();
                body.mass = Mass;

                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(TargetLength, TargetLength * 0.6f, TargetLength * 0.4f);
                box.center = new Vector3(0.0f, TargetLength * 0.3f, 0.0f);

                Radio radio = root.AddComponent<Radio>();
                radio.Playlist = new[]
                {
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/CityX/Radio/Audio/ooga_booga.mp3"),
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/CityX/Radio/Audio/agriculture.mp3"),
                };

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: radio written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Bounds Measure(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        private static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP Lit missing -- the radio will render magenta.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.3f);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
