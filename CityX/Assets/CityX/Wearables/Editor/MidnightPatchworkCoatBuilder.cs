using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Midnight Patchwork Coat from a third-party Meshy AI biped
    /// export (midnight_patchwork_coat.glb) -- a full character, not a
    /// pre-cut garment the way hooded_cloak.glb/skelli_jeans.glb already
    /// are, so unlike SkinnedGarmentBuilder.BakeMesh this has to do its own
    /// cutting before it can reuse that file's own bind-and-scaffold
    /// machinery (BuildPrefabFromMesh, PoseBindSkeletonArmsDown).
    ///
    /// THREE PROBLEMS THIS SOURCE FILE HAS THAT hooded_cloak.glb/
    /// skelli_jeans.glb DO NOT, confirmed by instantiating it and dumping
    /// its own bones and per-vertex dominant weights rather than assumed:
    ///
    /// 1. IT IS A WHOLE CHARACTER, ONE MESH, ONE MATERIAL -- head, hands,
    ///    feet and a full pair of legs are baked into the exact same
    ///    139,443-vertex "char1" mesh as the coat itself, with nothing to
    ///    key off but bone weight (see StripAndRebind). Cut by whichever
    ///    bone most influences each vertex, keeping only the torso
    ///    (Hips/Spine/Spine01/Spine02/neck) plus the two shoulder bones
    ///    (LeftShoulder/RightShoulder -- the only surviving attachment for
    ///    the coat's own shoulder seam once the sleeve itself, LeftArm and
    ///    everything below it, is stripped along with the rest of the
    ///    body). Removing head/arms/legs was the user's own explicit
    ///    request, not a limitation being worked around.
    ///
    /// 2. IT IS REAL-HUMAN SCALE, NOT THIS RIG'S SCALE. hooded_cloak.glb and
    ///    skelli_jeans.glb were authored specifically for fused_body.gd's
    ///    own proportions, so BakeMesh could bind their vertices straight
    ///    against this rig's bones with no scale correction. This mesh's
    ///    own Hips-to-shoulder span is 0.54m; this rig's is ShoulderY-HipY
    ///    = 1.0m. Skipped, a coat authored for a real body reads shrink-
    ///    wrapped onto a fraction of this one's much taller torso. Fixed
    ///    with a single scale+recentre applied to every kept vertex before
    ///    binding, calibrated on exactly that span (see StripAndRebind) --
    ///    the legs/head/arms being stripped anyway is what makes a single
    ///    torso-span scale factor the right one, rather than needing this
    ///    rig's own very different (long-armed, big-headed) full-body
    ///    proportions to reconcile too.
    ///
    /// 3. IT IS ALREADY CORRECTLY HANDED -- checked, not assumed, because
    ///    hooded_cloak.glb/skelli_jeans.glb both needed SkinnedGarmentBuilder's
    ///    own MirrorX for exactly this. This file's LeftShoulder/LeftArm sit
    ///    at negative X, matching this rig's own arm_l -- so MirrorX is
    ///    deliberately NOT applied here; doing it anyway on an
    ///    already-correct mesh would introduce the exact bug it exists to
    ///    fix elsewhere.
    /// </summary>
    public static class MidnightPatchworkCoatBuilder
    {
        private const string GlbPath = "Assets/CityX/Wearables/Models/midnight_patchwork_coat.glb";
        private const string MeshPath = "Assets/CityX/Wearables/MidnightPatchworkCoatMesh.asset";
        private const string PrefabPath = "Assets/CityX/Wearables/MidnightPatchworkCoat.prefab";
        private const string MaterialPath = "Assets/CityX/Wearables/MidnightPatchworkCoatFabric.mat";

        // Matches HoodedCloak's own ground-pose numbers -- a similarly
        // bulky garment, tipped the same way so it reads as a dropped coat
        // rather than half an invisible person.
        private const float GroundScale = 0.45f;
        private const float GroundPitchDegrees = -78.0f;
        private const float GroundLift = 0.15f;

        /// <summary>
        /// Index into this array IS the BoneWeight index the baked mesh
        /// uses -- matches SkinnedGarment.Bones exactly, so the runtime
        /// rebind in SkinnedGarment.OnEquip (which builds boneTransforms in
        /// THIS SAME order via Animator.GetBoneTransform) lines up with
        /// bindposes computed here without either file needing to know
        /// about the other's indices.
        /// </summary>
        private static readonly string[] TargetBoneNames =
        {
            "hips", "spine", "neck", "head",
            "arm_l", "forearm_l", "hand_l",
            "arm_r", "forearm_r", "hand_r",
            "thigh_l", "shin_l",
            "thigh_r", "shin_r",
        };

        private const int Hips = 0, Spine = 1, Neck = 2;

        /// <summary>
        /// The source biped's own 24 joint names, mapped to a slot in
        /// TargetBoneNames above -- everything NOT listed here (Head,
        /// head_end, headfront, the whole LeftArm/RightArm chain, the whole
        /// leg chain) is the strip set, checked by absence rather than a
        /// second list, so the two can never drift out of sync.
        ///
        /// LeftShoulder/RightShoulder MAP TO SPINE, NOT ARM_L/ARM_R -- tried
        /// first and confirmed wrong by an actual in-game screenshot: over a
        /// third of this mesh's kept vertices (12650 of 37400) are
        /// shoulder-dominant, and arm_l/arm_r is the one bone in this whole
        /// bind that swings through a real rotation during ordinary
        /// animation (hips/spine are frozen rotation-identity every frame
        /// by PlayerLocomotion.LateUpdate -- see its own doc comment). Bound
        /// to arm_l/arm_r, any difference between the live pose's actual
        /// shoulder rotation and PoseBindSkeletonArmsDown's own fixed
        /// 78-degree bind assumption swings every shoulder vertex through
        /// that same angular error -- and for a flat panel sitting well out
        /// from the shoulder pivot, a small angular error becomes a large
        /// positional one, which is exactly what showed up: the coat's own
        /// shoulder fabric bunched up near the collar instead of lying
        /// against the torso. Spine is frozen every frame, so binding here
        /// instead trades "the shoulder cap tracks the arm's own swing" (it
        /// never did, this coat has no sleeves) for "the shoulder cap stays
        /// glued to the torso regardless of what the arm is doing," which is
        /// what a coat missing its sleeves actually needs.
        /// </summary>
        private static readonly Dictionary<string, int> KeepBoneTarget = new Dictionary<string, int>
        {
            ["Hips"] = Hips,
            ["Spine"] = Spine, ["Spine01"] = Spine, ["Spine02"] = Spine,
            ["neck"] = Neck,
            ["LeftShoulder"] = Spine,
            ["RightShoulder"] = Spine,
        };

        [MenuItem("CityX/Rebuild Midnight Patchwork Coat")]
        public static GameObject Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (source == null)
            {
                Debug.LogError($"CityX: {GlbPath} did not load as a GameObject -- " +
                                "check that com.unity.cloud.gltfast is installed and the file reimported.");
                return null;
            }

            var sourceRenderer = source.GetComponentInChildren<SkinnedMeshRenderer>();
            if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
            {
                Debug.LogError($"CityX: {GlbPath} has no SkinnedMeshRenderer.");
                return null;
            }

            Mesh bakedMesh = StripAndRebind(sourceRenderer, MeshPath);
            if (bakedMesh == null)
            {
                return null;
            }

            Material material = BuildMaterial();

            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<MidnightPatchworkCoat>(
                bakedMesh, material, PrefabPath, "MidnightPatchworkCoat",
                GroundScale, GroundPitchDegrees, GroundLift);
            if (saved != null)
            {
                Debug.Log($"CityX: MidnightPatchworkCoat written to {PrefabPath} -- " +
                          $"{bakedMesh.vertexCount} vertices (from {sourceRenderer.sharedMesh.vertexCount} source)");
            }
            return saved;
        }

        /// <summary>
        /// A plain URP Lit material off the embedded texture, not glTFast's
        /// own imported material -- same reasoning BuildJeansMaterial gives:
        /// this source's own material (emissiveTexture set to the SAME
        /// image as the base colour, emissiveFactor 1/1/1, a specular
        /// factor of 2) is Meshy's own baked-lighting export convention,
        /// not something that reads correctly as a real-time PBR material
        /// -- imported literally it would self-illuminate and blow out
        /// under this game's own directional lighting.
        /// </summary>
        private static Material BuildMaterial()
        {
            Texture2D baseMap = AssetDatabase.LoadAllAssetsAtPath(GlbPath)
                .OfType<Texture2D>()
                .FirstOrDefault();
            if (baseMap == null)
            {
                Debug.LogWarning($"CityX: no embedded texture found in {GlbPath} -- " +
                                  "the coat will render untextured.");
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "MidnightPatchworkCoatFabric" };
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0.0f);
            // The armholes, hem and collar are all freshly-cut open
            // boundaries -- back-face culled (Unity's own default) they
            // read as literal holes into nothing, the same fix BuildCloak
            // and BuildJeansMaterial both already needed for their own
            // open seams.
            material.SetFloat("_Cull", 0.0f);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(MaterialPath);
            }
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Mesh StripAndRebind(SkinnedMeshRenderer source, string path)
        {
            Transform[] sourceBones = source.bones;
            Transform hips = System.Array.Find(sourceBones, b => b != null && b.name == "Hips");
            Transform leftShoulder = System.Array.Find(sourceBones, b => b != null && b.name == "LeftShoulder");
            Transform rightShoulder = System.Array.Find(sourceBones, b => b != null && b.name == "RightShoulder");
            if (hips == null || leftShoulder == null || rightShoulder == null)
            {
                Debug.LogError("CityX: coat source is missing Hips/LeftShoulder/RightShoulder -- " +
                                "cannot calibrate its scale against this rig.");
                return null;
            }

            // Calibrated on hip-to-shoulder span only, not the whole body --
            // this mesh's own head/arms/legs are being discarded, so only
            // the torso the coat actually keeps needs to land at the right
            // size. See this file's own class comment for the real numbers.
            Vector3 hipsWorld = hips.position;
            float sourceSpan = ((leftShoulder.position.y + rightShoulder.position.y) * 0.5f) - hipsWorld.y;
            float scale = (BodyRig.ShoulderY - BodyRig.HipY) / sourceSpan;
            var targetHips = new Vector3(0.0f, BodyRig.HipY, 0.0f);
            Vector3 Reposition(Vector3 v) => (v - hipsWorld) * scale + targetHips;

            var targetIndexForSourceBone = new int[sourceBones.Length];
            for (int i = 0; i < sourceBones.Length; i++)
            {
                string name = sourceBones[i] != null ? sourceBones[i].name : null;
                targetIndexForSourceBone[i] = (name != null && KeepBoneTarget.TryGetValue(name, out int t)) ? t : -1;
            }

            Mesh sourceMesh = source.sharedMesh;
            Vector3[] positions = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            Vector2[] uvs = sourceMesh.uv;
            BoneWeight[] weights = sourceMesh.boneWeights;
            int[] triangles = sourceMesh.triangles;

            int vertexCount = positions.Length;
            var kept = new bool[vertexCount];
            var oldToNew = new int[vertexCount];
            var newPositions = new List<Vector3>(vertexCount / 3);
            var newNormals = new List<Vector3>(vertexCount / 3);
            var newUVs = new List<Vector2>(vertexCount / 3);
            var newWeights = new List<BoneWeight>(vertexCount / 3);

            for (int i = 0; i < vertexCount; i++)
            {
                BoneWeight w = weights[i];
                int dominantSource = DominantBoneIndex(w);
                int dominantTarget = (dominantSource >= 0 && dominantSource < targetIndexForSourceBone.Length)
                    ? targetIndexForSourceBone[dominantSource]
                    : -1;
                if (dominantTarget < 0)
                {
                    continue;   // head, an arm, or a leg -- stripped per the user's own request
                }

                var slot = new float[TargetBoneNames.Length];
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex0, w.weight0);
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex1, w.weight1);
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex2, w.weight2);
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex3, w.weight3);

                float total = 0.0f;
                for (int s = 0; s < slot.Length; s++) total += slot[s];
                if (total <= 0.0001f)
                {
                    continue;   // every real weight this vertex had pointed at a stripped bone
                }
                for (int s = 0; s < slot.Length; s++) slot[s] /= total;

                kept[i] = true;
                oldToNew[i] = newPositions.Count;
                newPositions.Add(Reposition(positions[i]));
                // A uniform scale never changes a normal's direction, only
                // its magnitude -- copied as-is rather than rescaled.
                newNormals.Add((normals != null && normals.Length == vertexCount) ? normals[i] : Vector3.up);
                newUVs.Add((uvs != null && uvs.Length == vertexCount) ? uvs[i] : Vector2.zero);
                newWeights.Add(PackTop4(slot));
            }

            var newTriangles = new List<int>(triangles.Length / 2);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                // Only a triangle with all three corners kept -- a partial
                // one would stretch a single vertex across the cut boundary
                // into whatever the stripped side used to be.
                if (kept[a] && kept[b] && kept[c])
                {
                    newTriangles.Add(oldToNew[a]);
                    newTriangles.Add(oldToNew[b]);
                    newTriangles.Add(oldToNew[c]);
                }
            }

            GameObject skeletonRoot = BodyRigBuilder.BuildSkeleton(
                "CoatBindSkeleton", out Dictionary<string, Transform> named, BodyRig.RestBones());
            try
            {
                // Same arms-down correction the cloak/jeans bind needs, for
                // the same reason -- the shoulder bones this coat still
                // carries weight on (LeftShoulder/RightShoulder -> arm_l/
                // arm_r) have to be bound at the pose the live rig actually
                // rests in, not the T-pose BuildSkeleton starts from.
                SkinnedGarmentBuilder.PoseBindSkeletonArmsDown(named);

                var bindPoses = new Matrix4x4[TargetBoneNames.Length];
                for (int i = 0; i < TargetBoneNames.Length; i++)
                {
                    Transform bone = named[TargetBoneNames[i]];
                    bindPoses[i] = bone.worldToLocalMatrix * skeletonRoot.transform.localToWorldMatrix;
                }

                var baked = new Mesh { name = "MidnightPatchworkCoatMesh" };
                baked.indexFormat = newPositions.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                baked.SetVertices(newPositions);
                baked.SetNormals(newNormals);
                baked.SetUVs(0, newUVs);
                baked.boneWeights = newWeights.ToArray();
                baked.bindposes = bindPoses;
                baked.SetTriangles(newTriangles, 0);
                baked.RecalculateBounds();

                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                AssetDatabase.CreateAsset(baked, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: coat strip kept {newPositions.Count}/{vertexCount} vertices, " +
                          $"{newTriangles.Count / 3} triangles.");
                return baked;
            }
            finally
            {
                Object.DestroyImmediate(skeletonRoot);
            }
        }

        private static int DominantBoneIndex(BoneWeight w)
        {
            int dominant = w.boneIndex0;
            float best = w.weight0;
            if (w.weight1 > best) { best = w.weight1; dominant = w.boneIndex1; }
            if (w.weight2 > best) { best = w.weight2; dominant = w.boneIndex2; }
            if (w.weight3 > best) { dominant = w.boneIndex3; }
            return dominant;
        }

        private static void Accumulate(float[] slot, int[] targetIndexForSourceBone, int sourceBoneIndex, float weight)
        {
            if (weight <= 0.0f || sourceBoneIndex < 0 || sourceBoneIndex >= targetIndexForSourceBone.Length)
            {
                return;
            }
            int target = targetIndexForSourceBone[sourceBoneIndex];
            if (target < 0)
            {
                return;   // this vertex's small bleed onto a stripped bone -- dropped, not kept
            }
            slot[target] += weight;
        }

        /// <summary>Up to the four largest nonzero slots, descending -- BoneWeight only ever carries four.</summary>
        private static BoneWeight PackTop4(float[] slot)
        {
            int i0 = 0, i1 = 0, i2 = 0, i3 = 0;
            float w0 = 0.0f, w1 = 0.0f, w2 = 0.0f, w3 = 0.0f;
            for (int s = 0; s < slot.Length; s++)
            {
                float v = slot[s];
                if (v <= 0.0f)
                {
                    continue;
                }
                if (v > w0) { w3 = w2; i3 = i2; w2 = w1; i2 = i1; w1 = w0; i1 = i0; w0 = v; i0 = s; }
                else if (v > w1) { w3 = w2; i3 = i2; w2 = w1; i2 = i1; w1 = v; i1 = s; }
                else if (v > w2) { w3 = w2; i3 = i2; w2 = v; i2 = s; }
                else if (v > w3) { w3 = v; i3 = s; }
            }
            return new BoneWeight
            {
                boneIndex0 = i0, weight0 = w0,
                boneIndex1 = i1, weight1 = w1,
                boneIndex2 = i2, weight2 = w2,
                boneIndex3 = i3, weight3 = w3,
            };
        }
    }
}
