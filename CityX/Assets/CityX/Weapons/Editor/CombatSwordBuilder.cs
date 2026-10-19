using CityX.CharacterEditor;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The combat sword (owner, 2026-10-13: "use the model attached as a 1 handed sword", COMBATSWORDX.glb): the first ONE-HANDED sword. Held
    /// across the palm with the other hand free, and fought with SwordStrikes' one-handed attacks (a horizontal slash first, as asked).
    ///
    /// Same frame as every sword here: the guard at the root, the handle down -Y, the blade up +Y. The model is 1.9 m long in its own metres (a
    /// longsword on this 2.6 m body) and is kept at that size: the blade from the guard to the tip is 1.39 m, which the SwordWeapon is told, so
    /// its sweeps reach exactly as far as the model does.
    /// </summary>
    public static class CombatSwordBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/CombatSword.prefab";
        private const string SourcePath = "Assets/CityX/Weapons/CombatSword.glb";
        private const string MeshPath = "Assets/CityX/Weapons/CombatSwordMesh.asset";

        /// <summary>The middle of the guard in the glb's own metres (measured: the round handle runs from y -0.95 up to -0.48, the guard spans -0.48..-0.40).</summary>
        private const float GuardY = -0.44f;

        /// <summary>The glb's tip.</summary>
        private const float TipY = 0.95f;

        /// <summary>The stretch of handle the grip's centre line is measured over, in the glb's metres.</summary>
        private const float HandleBandBottom = -0.90f;

        private const float HandleBandTop = -0.55f;

        public const float Scale = 1.0f;

        /// <summary>The blade from the guard to the tip, in the sword's own (scaled) space.</summary>
        public static float BladeLength => (TipY - GuardY) * Scale;

        private const string ColourPath = "Assets/CityX/Weapons/CombatSword.png";
        private const string MetalPath = "Assets/CityX/Weapons/CombatSwordMetal.png";

        [MenuItem("CityX/Rebuild Combat Sword")]
        public static GameObject Build()
        {
            Mesh mesh = BakedMesh();
            var root = new GameObject("CombatSword");
            try
            {
                // The handle's centre line, from the baked mesh itself (the glTF import's own mirror and rotation are already in it).
                Vector3 handleCentre = HandleCentre(mesh);

                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * Scale;
                model.transform.localPosition = new Vector3(-handleCentre.x, -GuardY, -handleCentre.z) * Scale;
                model.AddComponent<MeshFilter>().sharedMesh = mesh;

                model.AddComponent<MeshRenderer>().sharedMaterial = PaintedSteel();

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, BladeLength * 0.5f, 0.0f);

                SwordWeapon sword = root.AddComponent<SwordWeapon>();
                sword.ItemId = ItemRegistry.CombatSword;
                sword.OneHanded = true;
                sword.BladeLength = BladeLength;
                sword.EdgeOnPlusX = true;   // the model's thin side (SwordWeapon.EdgeOnPlusX)

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
        /// The sword's paint (owner, 2026-10-19: Meshy's "White Sword", the same sword textured): white steel and a black wrapped grip, baked
        /// onto this mesh by SourceArt/CombatSword/combat_sword_bake.py as one 512 colour map and one 256 metallic map (metal in red,
        /// smoothness in alpha), so the steel shines and the wrapping does not.
        /// </summary>
        private static Material PaintedSteel()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(MetalPath);
            if (importer != null && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;   // amounts, not colours
                importer.SaveAndReimport();
            }
            Material steel = CharacterSkin.Ensure("CombatSwordSteel", Color.white);
            steel.SetColor("_BaseColor", Color.white);
            steel.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ColourPath));
            steel.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MetalPath));
            steel.EnableKeyword("_METALLICSPECGLOSSMAP");
            steel.SetFloat("_Metallic", 1.0f);
            steel.SetFloat("_Smoothness", 1.0f);   // scales the map's smoothness
            steel.SetFloat("_Cull", 0.0f);         // the glb is double-sided
            EditorUtility.SetDirty(steel);
            return steel;
        }

        /// <summary>The average of the handle's vertices (sideways and front-to-back), in the baked mesh's own frame.</summary>
        private static Vector3 HandleCentre(Mesh mesh)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 v in mesh.vertices)
            {
                if (v.y >= HandleBandBottom && v.y <= HandleBandTop)
                {
                    sum += v;
                    count++;
                }
            }
            if (count == 0)
            {
                Debug.LogWarning("CityX: the combat sword's handle band has no vertices -- is the model the right way up?");
                return Vector3.zero;
            }
            Vector3 centre = sum / count;
            centre.y = 0.0f;
            return centre;
        }

        private static Mesh BakedMesh()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            MeshFilter filter = model.GetComponentInChildren<MeshFilter>(true);
            Mesh source = filter.sharedMesh;
            Matrix4x4 bake = filter.transform.localToWorldMatrix;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = bake.MultiplyPoint3x4(vertices[i]);
                normals[i] = bake.MultiplyVector(normals[i]).normalized;
            }
            var mesh = new Mesh { name = "CombatSwordMesh", vertices = vertices, normals = normals, uv = source.uv, triangles = source.triangles };
            mesh.RecalculateBounds();
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }
    }
}
