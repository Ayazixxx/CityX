using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the CITY X body mesh and binds it to the CITY X skeleton.
    ///
    /// This is what replaces the imported mannequin: blended capsules, hands,
    /// feet, a neck and a sphere head, the shape the Godot build drew and more.
    /// The Mixamo files survive only as animation clips, retargeted through a
    /// humanoid avatar, which do not care what shape hangs off the bones.
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

        /// <summary>
        /// "Hands, feet and a collar only" -- fused_body.gd's own stub_mesh(),
        /// swapped in by a full-length garment so there is no independently
        /// posed limb left for rigid cloth to fail to cover. See
        /// FusedBodyShape.IsStubPoint's own doc comment.
        /// </summary>
        private const string StubMeshPath = "Assets/CityX/Character/FusedBodyStub.asset";

        private const string HeadPath = "Assets/CityX/Character/Head.asset";

        /// <summary>The child of the head bone that carries the skull.</summary>
        public const string HeadObjectName = BodySurface.HeadName;

        /// <summary>The step of the central difference the normals come from: smaller than a finger's radius, or a finger shades as a blob.</summary>
        private const float NormalStep = 0.004f;

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
        /// ONE SMOOTH SURFACE (owner, 2026-10-13: "a smooth body all throughout ... transitions between limbs smoother, there shouldnt be
        /// visible lines or lumps at the hips"). The whole field -- torso, pelvis, neck, arms, legs, feet, hands -- is extracted as one
        /// watertight surface, so every joint is a fillet of the smooth union rather than two tubes overlapping (which was the body from
        /// 2026-10-10: a line at every elbow, wrist and knee, and the thigh caps standing out of the hips). The skin is then weighted across each
        /// joint by fused_body.gd's own rule (FusedBodyShape.WeightsAt), narrow where a limb meets the torso and wide across an elbow or knee.
        ///
        /// THE HANDS ARE EXTRACTED FINER AND STITCHED ON. A finger is 4 cm across; on the body's 3 cm grid it would be a lump. Each hand (with
        /// a stretch of forearm) is extracted from the same field on a grid a third the size, both meshes are cut on one plane across the
        /// forearm, and the two cut edges are zipped together (FusedBodyMesh.ClipByPlane, Zip): no seam, since both trace the same field.
        ///
        /// THE POSITIONS COME FROM BodyRig, NOT FROM THE TRANSFORMS. The transforms are needed only to bind the skin to.
        /// </summary>
        public static Mesh Build(Transform root, Dictionary<HumanBodyBones, Transform> bones,
            out List<Transform> skinnedBones)
        {
            skinnedBones = new List<Transform>();

            // TWO DIFFERENT PIECE LISTS, DELIBERATELY. segments is split per BONE (an elbow, a wrist, a knee, the spine) -- right for deciding
            // which bone owns a vertex, wrong for deciding where the surface sits: unioning per-bone pieces back together bulges every internal
            // seam outward (FusedBodyShape.LimbCapsules' own comment). shape is one unbroken piece per limb -- what the surface is extracted
            // and shaded from. Only Unwrap() and WeightsAt() use segments; everything touching the actual shape uses shape.
            List<FusedBodyShape.Segment> segments = BodyRigBuilder.Segments();
            List<FusedBodyShape.Segment> shape = BodyRigBuilder.LimbCapsules();
            float cellSize = FusedBodyShape.CellSize;

            FusedBodyMesh.ExtractBody(shape, BodyRig.ShapePositionOf, cellSize, FusedBodyShape.HandCellSize,
                out List<Vector3> welded, out List<int> weldedTriangles, Debug.LogWarning);
            if (welded.Count == 0 || weldedTriangles.Count == 0)
            {
                Debug.LogError("CityX: the body extracted to nothing -- check the bounds.");
                return null;
            }

            // Unwelded with paint UVs (FusedBodyMesh.Unwrap, which needs triangle soup for its seam fix), then welded back wherever position and
            // UV agree, which is everywhere but the seams.
            FusedBodyMesh.Unwrap(segments, welded, weldedTriangles, out List<Vector3> soupVertices, out List<int> soupTriangles, out List<Vector2> soupUvs);
            FusedBodyMesh.Weld(soupVertices, soupTriangles, soupUvs, out List<Vector3> vertices, out List<int> triangles, out List<Vector2> uvs);
            Vector3[] normals = FusedBodyMesh.Normals(shape, vertices, NormalStep);

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
                    Debug.LogWarning($"CityX: the rig has no {segment.Bone} -- the geometry for it will be welded to the hips instead.");
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
                int used = FusedBodyShape.WeightsAt(segments, vertices[i], influenceBones, influenceWeights);
                // Sorted heaviest first: Unity treats slot 0 as the dominant bone and some quality settings drop the later ones.
                for (int a = 0; a < used - 1; a++)
                {
                    for (int b = a + 1; b < used; b++)
                    {
                        if (influenceWeights[b] > influenceWeights[a])
                        {
                            (influenceWeights[a], influenceWeights[b]) = (influenceWeights[b], influenceWeights[a]);
                            (influenceBones[a], influenceBones[b]) = (influenceBones[b], influenceBones[a]);
                        }
                    }
                }
                var w = new BoneWeight();
                float total = 0.0f;
                for (int slot = 0; slot < used; slot++)
                {
                    if (!boneIndex.TryGetValue(influenceBones[slot], out int index))
                    {
                        index = 0;
                    }
                    switch (slot)
                    {
                        case 0: w.boneIndex0 = index; w.weight0 = influenceWeights[slot]; break;
                        case 1: w.boneIndex1 = index; w.weight1 = influenceWeights[slot]; break;
                        case 2: w.boneIndex2 = index; w.weight2 = influenceWeights[slot]; break;
                        default: w.boneIndex3 = index; w.weight3 = influenceWeights[slot]; break;
                    }
                    total += influenceWeights[slot];
                }
                if (total > 0.0f && Mathf.Abs(total - 1.0f) > 1e-5f)
                {
                    w.weight0 /= total; w.weight1 /= total; w.weight2 /= total; w.weight3 /= total;
                }
                weights[i] = w;
            }

            // BOUND ARMS-DOWN, NOT WHERE THE TRANSFORMS HAPPEN TO START. The mesh is modelled in fused_body.gd's own rest (arms hanging), and
            // the bind pose maps that into each bone's space, so a clip that hangs the arms about as far as that bends the skin by almost
            // nothing -- see BodyRig.RestBindMatrix.
            var boundHuman = new HumanBodyBones[skinBones.Count];
            foreach (KeyValuePair<HumanBodyBones, int> entry in boneIndex)
            {
                boundHuman[entry.Value] = entry.Key;
            }
            var bindPoses = new Matrix4x4[skinBones.Count];
            for (int i = 0; i < skinBones.Count; i++)
            {
                bindPoses[i] = BodyRig.RestBindMatrix(boundHuman[i]).inverse;
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

            BuildStub(mesh, shape);
            return mesh;
        }

        /// <summary>
        /// Filters the just-built body mesh down to "hands, feet and a
        /// collar only" and saves it alongside the full body, updated in
        /// place the same way Build() itself updates FusedBody.asset -- see
        /// FusedBodyShape.IsStubPoint's own doc comment for what survives.
        ///
        /// A TRIANGLE SURVIVES OR IT DOES NOT, decided by its own centroid --
        /// matches fused_body.gd's own stub_mesh() exactly, including
        /// duplicating fresh vertices per surviving triangle rather than
        /// trying to reuse the source's indices, since a source triangle
        /// that fails the test can share a vertex with one that passes.
        /// </summary>
        private static void BuildStub(Mesh source, List<FusedBodyShape.Segment> limbCapsules)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            BoneWeight[] weights = source.boneWeights;
            int[] triangles = source.triangles;

            var outVertices = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outWeights = new List<BoneWeight>();
            var outTriangles = new List<int>();

            int triCount = triangles.Length / 3;
            for (int tri = 0; tri < triCount; tri++)
            {
                int i0 = triangles[tri * 3];
                int i1 = triangles[tri * 3 + 1];
                int i2 = triangles[tri * 3 + 2];
                Vector3 centroid = (vertices[i0] + vertices[i1] + vertices[i2]) / 3.0f;
                if (!FusedBodyShape.IsStubPoint(centroid, limbCapsules, FusedBodyShape.ReferenceHeight))
                {
                    continue;
                }

                foreach (int vi in new[] { i0, i1, i2 })
                {
                    outTriangles.Add(outVertices.Count);
                    outVertices.Add(vertices[vi]);
                    outNormals.Add(normals[vi]);
                    outUvs.Add(uvs[vi]);
                    outWeights.Add(weights[vi]);
                }
            }

            var stub = new Mesh
            {
                name = "FusedBodyStub",
                indexFormat = outVertices.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            stub.SetVertices(outVertices);
            stub.SetTriangles(outTriangles, 0);
            stub.SetNormals(outNormals);
            stub.SetUVs(0, outUvs);
            stub.boneWeights = outWeights.ToArray();
            stub.bindposes = source.bindposes;
            stub.RecalculateBounds();

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(StubMeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(stub, StubMeshPath);
            }
            else
            {
                existing.Clear();
                existing.indexFormat = stub.indexFormat;
                existing.SetVertices(outVertices);
                existing.SetTriangles(outTriangles, 0);
                existing.SetNormals(outNormals);
                existing.SetUVs(0, outUvs);
                existing.boneWeights = outWeights.ToArray();
                existing.bindposes = source.bindposes;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(stub);
            }
            AssetDatabase.SaveAssets();

            Debug.Log($"CityX: body stub built -- kept {outTriangles.Count / 3} of {triCount} triangles");
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
            // The shared posture laid over every animation (head up, legs back): BodyPostureSettings, tunable live.
            root.AddComponent<BodyPosture>();
            EnsurePostureAsset();

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

            var holder = new GameObject(BodySurface.SkinName);
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

        /// <summary>Makes the posture asset if there is none, so there is always something to drag the sliders of.</summary>
        private static void EnsurePostureAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<BodyPostureSettings>(BodyPostureSettings.AssetPath) != null)
            {
                return;
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BodyPostureSettings.AssetPath));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BodyPostureSettings>(), BodyPostureSettings.AssetPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Hangs the skull off the head bone as its own mesh.
        ///
        /// A PLAIN MESH ON THE BONE, NOT SKINNED INTO THE BODY, which is what
        /// character.gd does: a BoneAttachment3D following the head bone with a
        /// MeshInstance3D under it. It follows any animation for free, it can
        /// turn without dragging a shared skin around with it, and it leaves the
        /// body's own shape field with no head in it, exactly as fused_body.gd's
        /// limb_defs() has always had. The neck column now runs up into it.
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
