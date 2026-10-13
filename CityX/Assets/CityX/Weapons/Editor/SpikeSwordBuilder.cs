using CityX.CharacterEditor;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The spike sword (owner, 2026-10-13: "put this in as a two handed sword", Sharpspikesword.glb): a second two-handed sword that plays
    /// exactly like the greatsword -- same swings, guard, roll, same hold -- with the owner's own model.
    ///
    /// The glb's mesh is baked into a plain mesh asset (no textures in it; one plain steel material), then placed so it sits in the SAME
    /// frame as SwordBuilder's sword: the guard at the root, the handle running down -Y, the blade up +Y and 2.1 m long
    /// (<see cref="SwordWeapon.BladeLocalLength"/>). That is what lets it share the greatsword's tuned grip (WeaponGripSettings).
    /// </summary>
    public static class SpikeSwordBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/SpikeSword.prefab";
        private const string SourcePath = "Assets/CityX/Weapons/SpikeSword.glb";
        private const string MeshPath = "Assets/CityX/Weapons/SpikeSwordMesh.asset";

        /// <summary>Where the model's guard (top of the handle) is, in the glb's own metres: the handle runs from y -0.92 up to here.</summary>
        private const float GuardY = -0.62f;

        /// <summary>The handle's sideways centre in the glb (Blender measured -0.06; glTF import mirrors X).</summary>
        private const float HandleX = 0.06f;

        /// <summary>The glb's tip, so the blade can be scaled to the greatsword's 2.1 m reach.</summary>
        private const float TipY = 0.95f;

        private static readonly Color SteelColour = new Color(0.55f, 0.57f, 0.6f);

        [MenuItem("CityX/Rebuild Spike Sword")]
        public static GameObject Build()
        {
            Mesh mesh = BakedMesh();
            var root = new GameObject("SpikeSword");
            try
            {
                float scale = SwordWeapon.BladeLocalLength / (TipY - GuardY);
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = new Vector3(-HandleX, -GuardY, 0.0f) * scale;
                model.AddComponent<MeshFilter>().sharedMesh = mesh;

                Material steel = CharacterSkin.Ensure("SpikeSwordSteel", SteelColour);
                steel.SetFloat("_Metallic", 0.8f);
                steel.SetFloat("_Smoothness", 0.55f);
                steel.SetFloat("_Cull", 0.0f); // the glb is double-sided
                model.AddComponent<MeshRenderer>().sharedMaterial = steel;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, SwordWeapon.BladeLocalLength * 0.5f, 0.0f);

                root.AddComponent<SwordWeapon>().ItemId = ItemRegistry.SpikeSword;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
            var mesh = new Mesh { name = "SpikeSwordMesh", vertices = vertices, normals = normals, triangles = source.triangles };
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
