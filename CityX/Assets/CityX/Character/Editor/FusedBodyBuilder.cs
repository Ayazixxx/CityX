using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the CITY X body mesh and binds it to the CITY X skeleton.
    ///
    /// This is what replaces the imported mannequin: blended capsules and a
    /// sphere head, exactly the shape the Godot build drew. The Mixamo files
    /// survive only as animation clips, retargeted through a humanoid avatar,
    /// which do not care what shape hangs off the bones.
    ///
    /// THE PROPORTIONS COME FROM <see cref="BodyRig"/>, AND THE SKELETON IS
    /// BUILT TO MATCH THEM. The first attempt did it the other way round --
    /// read the joint positions out of the imported rig and fitted the body to
    /// those -- and lost both the proportions and, because Mixamo bends
    /// through spine and clavicle bones this body has no geometry for, the
    /// skinning as well.
    /// </summary>
    public static class FusedBodyBuilder
    {
        private const string MeshPath = "Assets/CityX/Character/FusedBody.asset";

        private const string HeadPath = "Assets/CityX/Character/Head.asset";

        /// <summary>The child of the head bone that carries the skull.</summary>
        public const string HeadObjectName = "Head";

        [MenuItem("CityX/Rebuild Body Mesh")]
        public static void BuildMenu()
        {
            GameObject body = BuildBody(
                CharacterSkin.Ensure("PlayerSkin", CharacterSkin.PlayerColour), null);
            Object.DestroyImmediate(body);
        }

        /// <summary>
        /// Builds the body against a rig laid out to <see cref="BodyRig"/>.
        ///
        /// THE POSITIONS COME FROM BodyRig, NOT FROM THE TRANSFORMS. That is
        /// the whole point of this rewrite: the body's proportions are the
        /// original's, stated once in one place, and the skeleton is built to
        /// match them rather than the body being fitted to somebody else's
        /// skeleton. The transforms are needed only to bind the skin to.
        /// </summary>
        public static Mesh Build(Transform root, Dictionary<HumanBodyBones, Transform> bones,
            out List<Transform> skinnedBones)
        {
            skinnedBones = new List<Transform>();

            List<FusedBodyShape.Segment> segments = BodyRigBuilder.Segments();
            float cellSize = FusedBodyShape.CellSize;
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, cellSize,
                out List<Vector3> welded, out List<int> weldedTriangles);

            if (welded.Count == 0 || weldedTriangles.Count == 0)
            {
                Debug.LogError("CityX: the body extracted to nothing -- check the bounds.");
                return null;
            }

            // UNWELDED, WITH PAINT UVs. The character is paintable, and the
            // seam unwrap that makes the unwrap usable is only safe on triangle
            // soup -- see FusedBodyMesh.Unwrap. It costs three times the
            // vertices for a mesh built once at import time.
            FusedBodyMesh.Unwrap(segments, welded, weldedTriangles,
                out List<Vector3> vertices, out List<int> triangles, out List<Vector2> uvs);

            Vector3[] normals = FusedBodyMesh.Normals(segments, vertices, cellSize * 0.5f);

            // --- skinning, blended across up to four bones per vertex.
            var skinBones = new List<Transform>();
            var boneIndex = new Dictionary<HumanBodyBones, int>();
            foreach (FusedBodyShape.Segment segment in segments)
            {
                if (boneIndex.ContainsKey(segment.Bone))
                {
                    continue;
                }
                if (!bones.TryGetValue(segment.Bone, out Transform bone) || bone == null)
                {
                    Debug.LogWarning($"CityX: the rig has no {segment.Bone} -- the geometry for " +
                                     "it will be welded to the hips instead.");
                    continue;
                }
                boneIndex[segment.Bone] = skinBones.Count;
                skinBones.Add(bone);
            }

            var weights = new BoneWeight[vertices.Count];
            var influenceBones = new HumanBodyBones[4];
            var influenceWeights = new float[4];

            for (int i = 0; i < vertices.Count; i++)
            {
                int used = FusedBodyShape.WeightsAt(
                    segments, vertices[i], influenceBones, influenceWeights);

                // Sorted heaviest first, because Unity treats slot 0 as the
                // dominant bone and some quality settings drop the later ones.
                for (int a = 0; a < used - 1; a++)
                {
                    for (int b = a + 1; b < used; b++)
                    {
                        if (influenceWeights[b] > influenceWeights[a])
                        {
                            (influenceWeights[a], influenceWeights[b]) =
                                (influenceWeights[b], influenceWeights[a]);
                            (influenceBones[a], influenceBones[b]) =
                                (influenceBones[b], influenceBones[a]);
                        }
                    }
                }

                var weight = new BoneWeight();
                for (int slot = 0; slot < used; slot++)
                {
                    int index = boneIndex.TryGetValue(influenceBones[slot], out int found)
                        ? found
                        : 0;
                    switch (slot)
                    {
                        case 0: weight.boneIndex0 = index; weight.weight0 = influenceWeights[0]; break;
                        case 1: weight.boneIndex1 = index; weight.weight1 = influenceWeights[1]; break;
                        case 2: weight.boneIndex2 = index; weight.weight2 = influenceWeights[2]; break;
                        case 3: weight.boneIndex3 = index; weight.weight3 = influenceWeights[3]; break;
                    }
                }
                weights[i] = weight;
            }

            var bindPoses = new Matrix4x4[skinBones.Count];
            for (int i = 0; i < skinBones.Count; i++)
            {
                // The bind pose maps from the mesh's space into the bone's, so
                // the bone's own transform undoes it exactly at rest.
                bindPoses[i] = skinBones[i].worldToLocalMatrix * root.localToWorldMatrix;
            }

            var mesh = new Mesh
            {
                name = "FusedBody",
                indexFormat = vertices.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;
            mesh.RecalculateBounds();

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else
            {
                // Updated in place so every prefab already pointing at it keeps
                // pointing at it across a rebuild.
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.SetVertices(vertices);
                existing.SetTriangles(triangles, 0);
                existing.SetNormals(normals);
                existing.SetUVs(0, uvs);
                existing.boneWeights = weights;
                existing.bindposes = bindPoses;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }

            AssetDatabase.SaveAssets();
            skinnedBones = skinBones;
            Debug.Log($"CityX: body mesh built -- {vertices.Count} vertices, " +
                      $"{triangles.Count / 3} triangles, {skinBones.Count} bones, " +
                      $"standing {BodyRig.StandingHeight:0.00}m");
            return mesh;
        }

        /// <summary>
        /// Builds a complete CITY X body: its own skeleton, a humanoid avatar
        /// for it, the fused mesh skinned to it, and an Animator wired up.
        ///
        /// Returns the root, ready to be parented into a character.
        /// </summary>
        public static GameObject BuildBody(Material material, RuntimeAnimatorController controller)
        {
            GameObject root = BodyRigBuilder.BuildSkeleton(
                "Body", out Dictionary<string, Transform> named);

            Animator animator = root.AddComponent<Animator>();
            animator.avatar = BodyRigBuilder.BuildAvatar(root, named);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var bones = new Dictionary<HumanBodyBones, Transform>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (bone.Human != HumanBodyBones.LastBone)
                {
                    bones[bone.Human] = named[bone.Name];
                }
            }

            Mesh mesh = Build(root.transform, bones, out List<Transform> skinned);
            if (mesh == null)
            {
                return root;
            }

            var holder = new GameObject("Skin");
            holder.transform.SetParent(root.transform, false);

            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = skinned.ToArray();
            renderer.rootBone = named["hips"];
            renderer.sharedMaterial = material;
            // Four bones per vertex, explicitly. The default follows a quality
            // setting that can be lowered to two or one, and dropping to one
            // silently restores exactly the rigid weighting that made the
            // shoulders tear.
            renderer.quality = SkinQuality.Bone4;
            // The body's own bounds move a long way from the root when it
            // ragdolls, and a renderer culled by its rest bounds vanishes at
            // exactly the moment somebody is watching it fall.
            renderer.updateWhenOffscreen = true;

            AttachHead(named["head"], material);
            return root;
        }

        /// <summary>
        /// Hangs the skull off the head bone as its own mesh.
        ///
        /// A PLAIN MESH ON THE BONE, NOT SKINNED INTO THE BODY, which is what
        /// character.gd does: a BoneAttachment3D following the head bone with a
        /// MeshInstance3D under it. It follows any animation for free, it can
        /// turn without dragging a shared skin around with it, and -- the part
        /// this port got wrong -- it leaves the body's own shape field with a
        /// torso, two arms, two legs and a neck in it, exactly as
        /// fused_body.gd's limb_defs() has always had.
        /// </summary>
        public static void AttachHead(Transform headBone, Material material)
        {
            if (headBone == null)
            {
                Debug.LogError("CityX: no head bone -- the character has no skull.");
                return;
            }

            Transform existing = headBone.Find(HeadObjectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var head = new GameObject(HeadObjectName);
            head.transform.SetParent(headBone, false);
            head.transform.localPosition = HeadMesh.CentreOnBone;
            head.transform.localRotation = Quaternion.identity;

            head.AddComponent<MeshFilter>().sharedMesh = BuildHeadMesh();
            MeshRenderer renderer = head.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        /// <summary>
        /// The head mesh asset, rebuilt in place so every prefab already
        /// pointing at it keeps pointing at it -- same reasoning as the body's.
        /// </summary>
        public static Mesh BuildHeadMesh()
        {
            Mesh built = HeadMesh.Build();

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(HeadPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, HeadPath);
                AssetDatabase.SaveAssets();
                return built;
            }

            existing.Clear();
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetUVs(0, built.uv);
            existing.SetTriangles(built.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            AssetDatabase.SaveAssets();
            return existing;
        }
    }
}
