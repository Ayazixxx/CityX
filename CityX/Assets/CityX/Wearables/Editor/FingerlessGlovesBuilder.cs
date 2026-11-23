using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds every colourway of the fingerless gloves (FingerlessGloveColourways, docs/FINGERLESS_GLOVES.md): a material in its colour and a
    /// prefab each, all lying on the ground as one shared model, the gloves made on the player's own hands and laid out side by side, palms
    /// down. Worn, a pair is made again on its wearer's hands (FingerlessGloves), so nothing here is what is worn.
    /// </summary>
    public static class FingerlessGlovesBuilder
    {
        public const string Folder = "Assets/CityX/Wearables/FingerlessGloves";
        public const string GroundMeshPath = Folder + "/FingerlessGlovesOnGround.asset";
        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";

        /// <summary>Leather: a little sheen.</summary>
        private const float Smoothness = 0.3f;

        /// <summary>The two gloves this far apart on the ground (their wrists'), metres.</summary>
        private const float GroundSpacing = 0.34f;

        public static string MaterialPath(string itemId) => $"{Folder}/{itemId}.mat";

        /// <summary>Every colourway's prefab, for an inventory's list (the scene builder's).</summary>
        [MenuItem("CityX/Rebuild Fingerless Gloves")]
        public static void BuildAllFromMenu() => BuildAll();

        public static Inventory.ItemPrefabEntry[] BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Wearables", "FingerlessGloves");
            }
            Mesh onGround = BuildGroundMesh();
            var entries = new List<Inventory.ItemPrefabEntry>();
            foreach (FingerlessGloveColourways.Colourway colourway in FingerlessGloveColourways.All)
            {
                GameObject prefab = Save(colourway, onGround);
                if (prefab != null)
                {
                    entries.Add(new Inventory.ItemPrefabEntry { ItemId = colourway.ItemId, Prefab = prefab });
                }
            }
            AssetDatabase.SaveAssets();
            return entries.ToArray();
        }

        private static GameObject Save(FingerlessGloveColourways.Colourway colourway, Mesh onGround)
        {
            Material material = BlenderGarmentMesh.PlainMaterial(MaterialPath(colourway.ItemId), colourway.Colour, Smoothness);
            var root = new GameObject(colourway.ItemId);
            try
            {
                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;
                var model = new GameObject("OnGround");
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = onGround;
                model.AddComponent<MeshRenderer>().sharedMaterial = material;
                root.AddComponent<FingerlessGloves>().Setup(colourway.ItemId, material, model);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, FingerlessGloveColourways.PrefabPath(colourway.ItemId));
                Debug.Log($"CityX: {colourway.DisplayName} written to {FingerlessGloveColourways.PrefabPath(colourway.ItemId)}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The gloves made on the player's hands, each moved off its hand to lie flat, palm down, fingers ahead, beside the other.</summary>
        private static Mesh BuildGroundMesh()
        {
            var player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            try
            {
                Transform skinObject = CityX.Core.TransformSearch.FindDeep(player.transform, BodySurface.SkinName);
                Mesh worn = FingerlessGloves.GlovesFor(skinObject.GetComponent<SkinnedMeshRenderer>());
                Vector3[] source = worn.vertices, sourceNormals = worn.normals;
                var vertices = (Vector3[])source.Clone();
                var normals = (Vector3[])sourceNormals.Clone();
                foreach (bool left in new[] { true, false })
                {
                    Vector3 wrist = BodyRig.RestBindMatrix(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).GetColumn(3);
                    Vector3 elbow = BodyRig.RestBindMatrix(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm).GetColumn(3);
                    Vector3 towardFingers = (wrist - elbow).normalized;
                    // The palm faces the body (arms down): turned to face the ground.
                    Vector3 palm = left ? Vector3.right : Vector3.left;
                    Quaternion ahead = Quaternion.FromToRotation(towardFingers, Vector3.forward);
                    Vector3 palmAhead = ahead * palm;
                    palmAhead = (palmAhead - Vector3.forward * Vector3.Dot(palmAhead, Vector3.forward)).normalized;
                    Quaternion laid = Quaternion.FromToRotation(palmAhead, Vector3.down) * ahead;
                    var place = new Vector3(left ? -GroundSpacing * 0.5f : GroundSpacing * 0.5f, 0.0f, 0.0f);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if ((source[i].x < 0.0f) == left)
                        {
                            vertices[i] = place + laid * (source[i] - wrist);
                            normals[i] = laid * sourceNormals[i];
                        }
                    }
                }
                float lowest = float.MaxValue;
                foreach (Vector3 v in vertices)
                {
                    lowest = Mathf.Min(lowest, v.y);
                }
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].y -= lowest;
                }

                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);
                if (mesh == null)
                {
                    mesh = new Mesh();
                    AssetDatabase.CreateAsset(mesh, GroundMeshPath);
                }
                mesh.Clear();
                mesh.name = "FingerlessGlovesOnGround";
                mesh.indexFormat = worn.indexFormat;
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.triangles = worn.triangles;
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
