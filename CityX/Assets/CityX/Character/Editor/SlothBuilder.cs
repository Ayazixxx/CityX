using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the sloth prefab out of primitives, matching npc/sloth.gd's own
    /// _build_model.
    ///
    /// EVERY LIMB IS PLACED BY ITS OWN TWO REAL 3D ENDPOINTS, not by
    /// hand-picked Euler rotation degrees -- Godot's own reasoning, confirmed
    /// there as the right call after a rotation-based first attempt produced
    /// visibly wrong limbs (see sloth.gd's own class comment). A capsule
    /// spanning two explicit points cannot have that bug, however steep or
    /// shallow the limb is.
    ///
    /// EVERY Z COORDINATE IS MIRRORED FROM THE GODOT SOURCE, same as
    /// WolfBuilder -- Godot's forward is -Z, Unity's is +Z. X coordinates are
    /// carried across unchanged, matching WolfBuilder's own convention.
    /// </summary>
    public static class SlothBuilder
    {
        private const string PrefabPath = "Assets/CityX/Character/Sloth.prefab";
        private const string MaterialDirectory = "Assets/CityX/Character/Materials";

        private static readonly Vector3 PelvisPos = new Vector3(0.0f, 2.7f, 0.15f);
        private static readonly Vector3 ShoulderPos = new Vector3(0.0f, 6.5f, 2.05f);
        private static readonly Vector3 HeadPos = new Vector3(0.0f, 6.05f, 3.85f);

        [MenuItem("CityX/Rebuild Sloth")]
        public static GameObject Build()
        {
            var root = new GameObject("Sloth");
            try
            {
                Material body = Skin("SlothBody", Sloth.BodyColour, false);
                Material dark = Skin("SlothDark", Sloth.DarkColour, false);
                Material underside = Skin("SlothUnderside", Sloth.UndersideColour, false);
                Material eye = Skin("SlothEye", Sloth.EyeColour, true);

                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);

                // Pelvis -- a rounded mass roughly where the legs plant from.
                Sphere(model.transform, PelvisPos, 1.05f, body);

                // Spine -- leans forward and up from the pelvis (the "hunch").
                CapsuleBetween(model.transform, PelvisPos, ShoulderPos, 1.05f, body);

                // Shoulder hump.
                Sphere(model.transform, ShoulderPos, 1.1f, body);

                // Head and muzzle.
                Box(model.transform, HeadPos, new Vector3(1.35f, 1.2f, 1.4f), body);
                Box(model.transform, HeadPos + new Vector3(0.0f, -0.15f, 0.85f),
                    new Vector3(0.85f, 0.7f, 0.7f), underside);

                // Ears -- small, tucked close.
                Ear(model.transform, HeadPos + new Vector3(-0.62f, 0.68f, -0.35f), dark);
                Ear(model.transform, HeadPos + new Vector3(0.62f, 0.68f, -0.35f), dark);

                // Eyes -- glowing red, unshaded.
                Eye(model.transform, HeadPos + new Vector3(-0.38f, 0.15f, 0.75f), eye);
                Eye(model.transform, HeadPos + new Vector3(0.38f, 0.15f, 0.75f), eye);

                // Arms -- knuckle-dragging, well forward of the feet.
                const float armRadius = 0.42f;
                foreach (float sideX in new[] { -1.55f, 1.55f })
                {
                    Vector3 shoulder = ShoulderPos + new Vector3(sideX, -0.3f, -0.15f);
                    var fist = new Vector3(sideX * 1.2f, 0.8f, 2.7f);
                    CapsuleBetween(model.transform, shoulder, fist, armRadius, dark);
                    Sphere(model.transform, fist, 0.55f, dark);
                }

                // Legs -- short, thick, nearly vertical.
                const float legRadius = 0.62f;
                foreach (float sideX in new[] { -0.75f, 0.75f })
                {
                    Vector3 hip = PelvisPos + new Vector3(sideX * 0.9f, -0.3f, -0.05f);
                    var foot = new Vector3(sideX, 0.0f, -0.55f);
                    CapsuleBetween(model.transform, hip, foot, legRadius, dark);
                }

                // Collision -- a single tall capsule standing upright, covering
                // the hunched silhouette's own vertical footprint.
                CharacterController controller = root.AddComponent<CharacterController>();
                controller.radius = 1.6f;
                controller.height = 7.7f;
                controller.center = new Vector3(0.0f, controller.height * 0.5f, 1.4f);
                controller.slopeLimit = 80.0f;
                controller.stepOffset = 0.5f;

                Sloth sloth = root.AddComponent<Sloth>();
                sloth.Model = model.transform;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: sloth written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A capsule spanning exactly the two given world-local points.
        /// Unity's capsule primitive's own local +Y is its long axis by
        /// default, so this aligns +Y to the from-to direction directly
        /// rather than deriving Euler angles that would produce the same
        /// alignment by hand.
        /// </summary>
        private static void CapsuleBetween(Transform parent, Vector3 from, Vector3 to,
            float radius, Material material)
        {
            Vector3 diff = to - from;
            float length = diff.magnitude;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Strip(go, parent, material);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, diff.normalized);
            float height = Mathf.Max(length, radius * 2.0f + 0.01f);
            go.transform.localScale = new Vector3(radius * 2.0f, height * 0.5f, radius * 2.0f);
        }

        private static void Box(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Strip(go, parent, material);
            go.transform.localPosition = position;
            go.transform.localScale = size;
        }

        private static void Sphere(Transform parent, Vector3 position, float radius, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Strip(go, parent, material);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * (radius * 2.0f);
        }

        /// <summary>A wedge-ish sliver, same approximation WolfBuilder's own Ear uses in place of Godot's PrismMesh.</summary>
        private static void Ear(Transform parent, Vector3 position, Material material)
        {
            Box(parent, position, new Vector3(0.32f, 0.42f, 0.18f), material);
        }

        private static void Eye(Transform parent, Vector3 position, Material material)
        {
            Sphere(parent, position, 0.11f, material);
        }

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
                    Debug.LogError("CityX: URP shaders missing -- the sloth will render magenta.");
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
