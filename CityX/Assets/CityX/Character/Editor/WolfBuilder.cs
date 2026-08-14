using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the wolf prefab out of primitives, matching npc/wolf.gd's own
    /// _build_model.
    ///
    /// EVERY MEASUREMENT IS A NAMED CONSTANT rather than a transform typed into
    /// a scene file. That is Godot's own reasoning here and it holds: a wolf
    /// described as "body 0.95 long, head 0.62 in front of centre" can be
    /// adjusted; the same wolf described as a list of Transform values cannot
    /// be read at all.
    /// </summary>
    public static class WolfBuilder
    {
        private const string PrefabPath = "Assets/CityX/Character/Wolf.prefab";
        private const string MaterialDirectory = "Assets/CityX/Character/Materials";

        private const float BodyLength = 0.95f;
        private const float BodyRadius = 0.22f;
        private const float BodyY = 0.42f;

        /// <summary>
        /// The head sits at NEGATIVE Z, which is Godot's convention and not
        /// Unity's -- a look_at body faces -Z there. In Unity forward is +Z, so
        /// every one of these is mirrored from the original.
        /// </summary>
        private const float HeadZ = 0.62f;

        private const float HeadY = 0.5f;
        private const float SnoutZ = HeadZ + 0.28f;

        [MenuItem("CityX/Rebuild Wolf")]
        public static GameObject Build()
        {
            var root = new GameObject("Wolf");
            try
            {
                Material body = Skin("WolfBody", Wolf.BodyColour, false);
                Material dark = Skin("WolfDark", Wolf.DarkColour, false);
                Material tooth = Skin("WolfTooth", Wolf.ToothColour, false);
                // Unshaded, so the eyes read as menacing whatever the scene
                // lighting is doing -- which at midnight is very little.
                Material eye = Skin("WolfEye", Wolf.EyeColour, true);

                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);

                Capsule(model.transform, new Vector3(0.0f, BodyY, 0.0f),
                    new Vector3(90.0f, 0.0f, 0.0f), BodyRadius, BodyLength, body);

                Box(model.transform, new Vector3(0.0f, HeadY, HeadZ),
                    new Vector3(0.28f, 0.26f, 0.34f), body);
                Box(model.transform, new Vector3(0.0f, HeadY - 0.04f, SnoutZ),
                    new Vector3(0.16f, 0.16f, 0.26f), dark);

                // Ears, on top of the head and slightly behind its front.
                Ear(model.transform, new Vector3(-0.09f, HeadY + 0.16f, HeadZ - 0.05f), dark);
                Ear(model.transform, new Vector3(0.09f, HeadY + 0.16f, HeadZ - 0.05f), dark);

                // A row of four fangs under the snout's tip.
                float teethZ = SnoutZ + 0.1f;
                for (int i = 0; i < 4; i++)
                {
                    float x = Mathf.Lerp(-0.06f, 0.06f, i / 3.0f);
                    Tooth(model.transform, new Vector3(x, HeadY - 0.11f, teethZ), tooth);
                }

                Eye(model.transform, new Vector3(-0.09f, HeadY + 0.04f, HeadZ + 0.05f), eye);
                Eye(model.transform, new Vector3(0.09f, HeadY + 0.04f, HeadZ + 0.05f), eye);

                const float legRadius = 0.06f;
                const float legLength = 0.4f;
                const float legY = 0.22f;
                const float legX = 0.16f;
                foreach (float x in new[] { -legX, legX })
                {
                    foreach (float z in new[] { 0.3f, -0.32f })
                    {
                        Capsule(model.transform, new Vector3(x, legY, z), Vector3.zero,
                            legRadius, legLength, dark);
                    }
                }

                // The tail, angled up and back.
                Capsule(model.transform, new Vector3(0.0f, 0.55f, -0.58f),
                    new Vector3(35.0f, 0.0f, 0.0f), 0.06f, 0.4f, body);

                // Sized to cover the body and legs together rather than the
                // body alone, so a wolf cannot be walked through at the knees.
                CharacterController capsule = root.AddComponent<CharacterController>();
                capsule.radius = BodyRadius + 0.06f;
                capsule.height = BodyY + BodyRadius + 0.2f;
                capsule.center = new Vector3(0.0f, capsule.height * 0.5f, 0.0f);
                capsule.slopeLimit = 80.0f;
                capsule.stepOffset = 0.35f;

                Wolf wolf = root.AddComponent<Wolf>();
                wolf.Model = model.transform;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: wolf written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Capsule(Transform parent, Vector3 position, Vector3 euler,
            float radius, float length, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Strip(go, parent, material);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            // Unity's capsule primitive is 2 units tall and 1 across, so its
            // scale is diameter on the flat axes and total height on the long
            // one, halved.
            go.transform.localScale = new Vector3(radius * 2.0f, length * 0.5f, radius * 2.0f);
        }

        private static void Box(Transform parent, Vector3 position, Vector3 size,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Strip(go, parent, material);
            go.transform.localPosition = position;
            go.transform.localScale = size;
        }

        /// <summary>
        /// An ear. Godot uses a prism; Unity has no prism primitive, so this is
        /// a cube squeezed to a wedge-ish sliver. It is 9cm of grey on top of a
        /// wolf's head seen from three metres away, and the difference between
        /// a prism and a thin box at that size is not visible.
        /// </summary>
        private static void Ear(Transform parent, Vector3 position, Material material)
        {
            Box(parent, position, new Vector3(0.09f, 0.14f, 0.05f), material);
        }

        private static void Tooth(Transform parent, Vector3 position, Material material)
        {
            Box(parent, position, new Vector3(0.025f, 0.05f, 0.025f), material);
        }

        private static void Eye(Transform parent, Vector3 position, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Strip(go, parent, material);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.07f;
        }

        /// <summary>
        /// Parents a primitive and takes its collider off. THIRTY-ODD COLLIDERS
        /// otherwise, one per ear and tooth, all of them inside the character
        /// controller that already covers the whole animal.
        /// </summary>
        private static void Strip(GameObject go, Transform parent, Material material)
        {
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material Skin(string name, Color colour, bool unlit)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(unlit
                    ? "Universal Render Pipeline/Unlit"
                    : "Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP shaders missing -- the wolf will render magenta.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.color = colour;
            if (!unlit)
            {
                material.SetFloat("_Smoothness", 0.18f);
                material.SetFloat("_Metallic", 0.0f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
