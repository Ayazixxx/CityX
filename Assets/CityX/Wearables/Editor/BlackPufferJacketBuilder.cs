using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the black puffer jacket from blackpuffer.glb -- a genuinely
    /// new situation among this project's wearables. Every prior garment
    /// (hooded_cloak.glb, skelli_jeans.glb, midnight_patchwork_coat.glb)
    /// arrived with SOME bone weights already on it, even when (like the
    /// coat) they belonged to a completely different, real-human-scale
    /// rig that needed remapping. This source has NONE at all -- confirmed
    /// directly by parsing its own glTF JSON: zero skins, no JOINTS_0/
    /// WEIGHTS_0 attributes, a single static mesh. There is nothing to
    /// remap, so weights are assigned from vertex GEOMETRY alone instead,
    /// against BodyRig.RestBones()'s own arms-down joint positions --
    /// the same reference every other garment binds against, just reached
    /// by measuring distance-to-joint instead of reading an existing rig.
    ///
    /// PLACEMENT IS A MANUAL KNOB, NOT A DERIVED GUESS -- there is no
    /// source skeleton to cross-check against the way
    /// MidnightPatchworkCoatBuilder could measure its own source's real
    /// Hips/LeftShoulder bones. A first automatic calibration (align the
    /// source's own raw top to the shoulder line) rendered far too small
    /// and sitting up around the collar -- confirmed directly by
    /// capturing it, not assumed. See ManualScale/ManualVerticalOffset's
    /// own doc comment: real placement is now set by hand against the
    /// actual render, the same way it would be nudged into place in
    /// Blender, rather than guessed again in code. The WEIGHTING
    /// heuristic below stays fully automatic and re-adapts to wherever
    /// ManualScale/ManualVerticalOffset put the mesh, since it reads the
    /// already-transformed vertex position -- only PLACEMENT needed a
    /// human eye, not which bone a vertex leans toward.
    /// </summary>
    public static class BlackPufferJacketBuilder
    {
        private const string GlbPath = "Assets/CityX/Wearables/Models/blackpuffer.glb";
        private const string MeshPath = "Assets/CityX/Wearables/BlackPufferJacketMesh.asset";
        private const string PrefabPath = "Assets/CityX/Wearables/BlackPufferJacket.prefab";

        // Matches MidnightPatchworkCoat's own ground-pose numbers -- a
        // similarly bulky torso garment, tipped the same way so it reads
        // as a dropped jacket rather than half an invisible person.
        private const float GroundScale = 0.45f;
        private const float GroundPitchDegrees = -78.0f;
        private const float GroundLift = 0.15f;

        private static readonly Color JacketColour = new Color(0.045f, 0.045f, 0.05f);

        /// <summary>
        /// MANUAL PLACEMENT -- the first automatic calibration (align the
        /// source's own raw top to BodyRig.ShoulderY + a fixed collar lift)
        /// rendered far too small and sitting up around the collar instead
        /// of covering the torso. Rather than keep guessing blind, these
        /// two numbers are the direct knobs: change one, run "CityX/
        /// Rebuild Black Puffer Jacket" from the menu, and look at
        /// Logs/CityShots/jacket-front.png (CaptureWearables) to see the
        /// result -- no other code changes needed for a placement tweak.
        ///
        /// Applied as: finalPosition = rawPosition * ManualScale +
        /// (0, ManualVerticalOffset, 0), evaluated in the SAME space
        /// BodyRig's own joints live in (metres, hips at y=0.9, shoulders
        /// at y=1.9) -- so "bigger" is a larger ManualScale and "lower on
        /// the body" is a smaller (more negative) ManualVerticalOffset.
        /// The weight heuristic below (WeightFor) reads the ALREADY-
        /// transformed position, so changing these never breaks which bone
        /// a vertex leans toward -- only where the mesh itself sits.
        /// </summary>
        private const float ManualScale = 1.0f;

        /// <summary>Shifts the whole jacket up (positive) or down (negative) after ManualScale is applied.</summary>
        private const float ManualVerticalOffset = 0.0f;

        [MenuItem("CityX/Rebuild Black Puffer Jacket")]
        public static GameObject Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (source == null)
            {
                Debug.LogError($"CityX: {GlbPath} did not load as a GameObject -- " +
                                "check that com.unity.cloud.gltfast is installed and the file reimported.");
                return null;
            }

            MeshFilter sourceFilter = source.GetComponentInChildren<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                Debug.LogError($"CityX: {GlbPath} has no MeshFilter -- " +
                                "if it now HAS a skeleton, this should use SkinnedGarmentBuilder's " +
                                "own Build<T> path instead, not this one.");
                return null;
            }
            if (source.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                Debug.LogWarning($"CityX: {GlbPath} now has a real SkinnedMeshRenderer -- " +
                                   "this builder was written for a source with NO existing rig at all " +
                                   "and ignores any skin the file now carries. Switch to " +
                                   "SkinnedGarmentBuilder.Build<T> to actually use it.");
            }

            Mesh bakedMesh = RigFromGeometry(sourceFilter.sharedMesh, MeshPath);
            if (bakedMesh == null)
            {
                return null;
            }

            // A plain flat colour, not the source's own embedded textures --
            // matches MidnightPatchworkCoat/FarmerHat's own precedent: this
            // project's glTFast import has twice silently stubbed a Meshy-
            // baked embedded texture into something unusable, and a flat
            // "black puffer jacket" reads exactly as well as one anyway.
            Material material = CharacterSkin.Ensure("BlackPufferJacketFabric", JacketColour);
            material.SetFloat("_Smoothness", 0.35f);

            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<BlackPufferJacket>(
                bakedMesh, material, PrefabPath, "BlackPufferJacket",
                GroundScale, GroundPitchDegrees, GroundLift);
            if (saved != null)
            {
                Debug.Log($"CityX: BlackPufferJacket written to {PrefabPath} -- " +
                          $"{bakedMesh.vertexCount} vertices, geometry-rigged (no source skeleton).");
            }
            return saved;
        }

        /// <summary>
        /// Scales/recentres the raw source, assigns bone weights purely
        /// from each (already repositioned) vertex's own position relative
        /// to BodyRig.RestBones' own joints, then bakes bindposes the exact
        /// same way SkinnedGarmentBuilder.BakeMesh does for every other
        /// garment -- so the result binds against this rig correctly
        /// regardless of how its weights were arrived at.
        /// </summary>
        private static Mesh RigFromGeometry(Mesh source, string path)
        {
            Vector3[] rawVertices = source.vertices;
            Vector3[] rawNormals = source.normals;
            Vector2[] rawUVs = source.uv;
            int vertexCount = rawVertices.Length;

            if (source.bounds.size.y < 0.0001f)
            {
                Debug.LogError("CityX: blackpuffer.glb's own mesh has no real vertical extent.");
                return null;
            }

            // See ManualScale/ManualVerticalOffset's own doc comment --
            // direct, tunable knobs rather than a derived guess.
            Vector3 Reposition(Vector3 v) =>
                new Vector3(v.x * ManualScale, v.y * ManualScale + ManualVerticalOffset, v.z * ManualScale);

            float elbowY = GeometryRig.ElbowY();

            var newPositions = new Vector3[vertexCount];
            var newWeights = new BoneWeight[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 p = Reposition(rawVertices[i]);
                newPositions[i] = p;
                newWeights[i] = GeometryRig.TorsoAndSleeveWeight(p, elbowY);
            }

            GameObject skeletonRoot = BodyRigBuilder.BuildSkeleton(
                "JacketBindSkeleton", out Dictionary<string, Transform> named, BodyRig.RestBones());
            try
            {
                SkinnedGarmentBuilder.PoseBindSkeletonArmsDown(named);

                var bindPoses = new Matrix4x4[SkinnedGarment.Bones.Length];
                for (int i = 0; i < SkinnedGarment.Bones.Length; i++)
                {
                    string boneName = BoneNameFor(SkinnedGarment.Bones[i]);
                    Transform bone = named[boneName];
                    bindPoses[i] = bone.worldToLocalMatrix * skeletonRoot.transform.localToWorldMatrix;
                }

                var baked = new Mesh { name = "BlackPufferJacketMesh" };
                baked.indexFormat = vertexCount > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                baked.SetVertices(newPositions);
                baked.SetNormals(rawNormals.Length == vertexCount ? rawNormals : new Vector3[vertexCount]);
                baked.SetUVs(0, rawUVs.Length == vertexCount ? rawUVs : new Vector2[vertexCount]);
                baked.boneWeights = newWeights;
                baked.bindposes = bindPoses;
                baked.SetTriangles(source.triangles, 0);
                baked.RecalculateBounds();

                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                AssetDatabase.CreateAsset(baked, path);
                AssetDatabase.SaveAssets();
                return baked;
            }
            finally
            {
                Object.DestroyImmediate(skeletonRoot);
            }
        }

        /// <summary>SkinnedGarment.Bones (HumanBodyBones) -> BodyRig.RestBones' own string names.</summary>
        private static string BoneNameFor(HumanBodyBones human)
        {
            switch (human)
            {
                case HumanBodyBones.Hips: return "hips";
                case HumanBodyBones.Spine: return "spine";
                case HumanBodyBones.Neck: return "neck";
                case HumanBodyBones.Head: return "head";
                case HumanBodyBones.LeftUpperArm: return "arm_l";
                case HumanBodyBones.LeftLowerArm: return "forearm_l";
                case HumanBodyBones.LeftHand: return "hand_l";
                case HumanBodyBones.RightUpperArm: return "arm_r";
                case HumanBodyBones.RightLowerArm: return "forearm_r";
                case HumanBodyBones.RightHand: return "hand_r";
                case HumanBodyBones.LeftUpperLeg: return "thigh_l";
                case HumanBodyBones.LeftLowerLeg: return "shin_l";
                case HumanBodyBones.RightUpperLeg: return "thigh_r";
                case HumanBodyBones.RightLowerLeg: return "shin_r";
                default: return null;
            }
        }
    }
}
