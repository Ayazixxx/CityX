using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// A garment made and skinned in Blender (SourceArt/&lt;Garment&gt;/*_build.py) and written as JSON in this game's frame, turned into a skinned
    /// mesh bound to the arms-down skeleton every garment is bound to, plus its one textured material.
    /// </summary>
    public static class BlenderGarmentMesh
    {
        /// <summary>
        /// The bones the Blender scripts weight to, in SkinnedGarment.Bones' order (their indices are into this), then GarmentHelperBones.Helpers'.
        /// </summary>
        private static readonly string[] TargetBoneNames =
        {
            "hips", "spine", "neck", "head",
            "arm_l", "forearm_l", "hand_l",
            "arm_r", "forearm_r", "hand_r",
            "thigh_l", "shin_l",
            "thigh_r", "shin_r",
        };

        /// <summary>The Blender script's output, flat arrays in this game's frame.</summary>
        [System.Serializable]
        private sealed class Source
        {
            public float[] positions;
            public float[] normals;
            public float[] uvs;
            public int[] triangles;
            public int[] boneIndices;
            public float[] boneWeights;
        }

        /// <summary>The garment's mesh saved at <paramref name="meshPath"/> (into the asset already there, so prefabs keep it); null when the JSON is missing.</summary>
        public static Mesh Bake(string jsonPath, string meshName, string meshPath)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (json == null)
            {
                Debug.LogError($"CityX: {jsonPath} missing -- run its SourceArt build script; no {meshName} built.");
                return null;
            }
            Source source = JsonUtility.FromJson<Source>(json.text);
            int count = source.positions.Length / 3;
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var uvs = new Vector2[count];
            var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(source.positions[i * 3], source.positions[i * 3 + 1], source.positions[i * 3 + 2]);
                normals[i] = new Vector3(source.normals[i * 3], source.normals[i * 3 + 1], source.normals[i * 3 + 2]).normalized;
                uvs[i] = new Vector2(source.uvs[i * 2], source.uvs[i * 2 + 1]);
                weights[i] = new BoneWeight
                {
                    boneIndex0 = source.boneIndices[i * 4], weight0 = source.boneWeights[i * 4],
                    boneIndex1 = source.boneIndices[i * 4 + 1], weight1 = source.boneWeights[i * 4 + 1],
                    boneIndex2 = source.boneIndices[i * 4 + 2], weight2 = source.boneWeights[i * 4 + 2],
                    boneIndex3 = source.boneIndices[i * 4 + 3], weight3 = source.boneWeights[i * 4 + 3],
                };
            }

            GameObject skeletonRoot = BodyRigBuilder.BuildSkeleton(meshName + "BindSkeleton", out Dictionary<string, Transform> named, BodyRig.RestBones());
            try
            {
                SkinnedGarmentBuilder.PoseBindSkeletonArmsDown(named);
                var bindPoses = new Matrix4x4[TargetBoneNames.Length + GarmentHelperBones.Helpers.Length];
                for (int i = 0; i < TargetBoneNames.Length; i++)
                {
                    bindPoses[i] = named[TargetBoneNames[i]].worldToLocalMatrix * skeletonRoot.transform.localToWorldMatrix;
                }
                // Each helper bound on its joint with its parent's orientation, as GarmentHelperBones holds it at rest.
                for (int i = 0; i < GarmentHelperBones.Helpers.Length; i++)
                {
                    Transform parent = named[TargetBoneNames[System.Array.IndexOf(SkinnedGarment.Bones, GarmentHelperBones.Helpers[i].parent)]];
                    Transform child = named[TargetBoneNames[System.Array.IndexOf(SkinnedGarment.Bones, GarmentHelperBones.Helpers[i].child)]];
                    var helper = new GameObject(GarmentHelperBones.Helpers[i].name).transform;
                    helper.SetParent(parent, false);
                    helper.SetPositionAndRotation(child.position, parent.rotation);
                    bindPoses[TargetBoneNames.Length + i] = helper.worldToLocalMatrix * skeletonRoot.transform.localToWorldMatrix;
                }
                var baked = new Mesh
                {
                    name = meshName,
                    indexFormat = count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                    vertices = positions,
                    normals = normals,
                    uv = uvs,
                    boneWeights = weights,
                    bindposes = bindPoses,
                    triangles = source.triangles,
                };
                baked.RecalculateBounds();
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(baked, existing);
                    return existing;
                }
                AssetDatabase.CreateAsset(baked, meshPath);
                return baked;
            }
            finally
            {
                Object.DestroyImmediate(skeletonRoot);
            }
        }

        /// <summary>The garment's own textured URP material, both faces drawn (an open front and the cuffs show the inside).</summary>
        public static Material TexturedMaterial(string materialPath, string texturePath, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.0f);
            material.SetFloat("_Cull", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
