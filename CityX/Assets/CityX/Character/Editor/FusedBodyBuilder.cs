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

        /// <summary>
        /// "Hands, feet and a collar only" -- fused_body.gd's own stub_mesh(),
        /// swapped in by a full-length garment so there is no independently
        /// posed limb left for rigid cloth to fail to cover. See
        /// FusedBodyShape.IsStubPoint's own doc comment.
        /// </summary>
        private const string StubMeshPath = "Assets/CityX/Character/FusedBodyStub.asset";

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

            // SEPARATE TUBES, ONE PER BONE (owner, 2026-10-10: "the model is clearly stretched around the torso ... make it so each limb is a
            // separate", "I just want the torso and arms to be tubes not stretched like that"; separate limbs are also what dismemberment will need).
            // The body used to be ONE surface fused across every joint and skinned with weights blended between bones, so turning a joint (a
            // T-pose's 77 degree shoulder above all) dragged the skin of the torso along with the arm. Now every per-bone capsule -- pelvis,
            // torso, neck, upper arm, forearm, hand, thigh, shin -- is its own closed rounded tube, extracted on its own and bound 100% to its
            // own bone: a joint turns a tube as a whole and nothing stretches. The rounded ends overlap at every joint, so no gap opens as it
            // bends. Same capsules, same radii, same proportions as before (FusedBodyShape.Segments); only the joins are no longer blended.
            List<FusedBodyShape.Segment> segments = OneTorso(BodyRigBuilder.Segments());
            float cellSize = FusedBodyShape.CellSize;

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
                    Debug.LogWarning($"CityX: the rig has no {segment.Bone} -- its tube will hang off the hips instead.");
                    continue;
                }
                boneIndex[segment.Bone] = skinBones.Count;
                skinBones.Add(bone);
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var normalList = new List<Vector3>();
            var weightList = new List<BoneWeight>();
            var one = new List<FusedBodyShape.Segment>(1);
            foreach (FusedBodyShape.Segment segment in segments)
            {
                one.Clear();
                one.Add(segment);
                Bounds bounds = FusedBodyShape.BoundsOf(one);
                bounds.SetMinMax(FusedBodyShape.PhaseAlignedOrigin(bounds.min, cellSize), bounds.max);
                FusedBodyMesh.Extract(one, bounds, cellSize, out List<Vector3> welded, out List<int> weldedTriangles);
                if (welded.Count == 0 || weldedTriangles.Count == 0)
                {
                    Debug.LogError($"CityX: the {segment.Bone} tube extracted to nothing.");
                    continue;
                }
                // Unwelded with paint UVs (FusedBodyMesh.Unwrap), the tube's own bone deciding its island.
                FusedBodyMesh.Unwrap(one, welded, weldedTriangles,
                    out List<Vector3> partVertices, out List<int> partTriangles, out List<Vector2> partUvs);
                Vector3[] partNormals = FusedBodyMesh.Normals(one, partVertices, cellSize * 0.5f);

                int first = vertices.Count;
                int index = boneIndex.TryGetValue(segment.Bone, out int found) ? found : 0;
                var rigid = new BoneWeight { boneIndex0 = index, weight0 = 1.0f };
                vertices.AddRange(partVertices);
                uvs.AddRange(partUvs);
                normalList.AddRange(partNormals);
                foreach (int t in partTriangles)
                {
                    triangles.Add(first + t);
                }
                for (int i = 0; i < partVertices.Count; i++)
                {
                    weightList.Add(rigid);
                }
            }

            if (vertices.Count == 0)
            {
                Debug.LogError("CityX: the body extracted to nothing -- check the bounds.");
                return null;
            }
            Vector3[] normals = normalList.ToArray();
            BoneWeight[] weights = weightList.ToArray();
            List<FusedBodyShape.Segment> limbCapsules = BodyRigBuilder.LimbCapsules();

            // BOUND ARMS-DOWN, NOT WHERE THE TRANSFORMS HAPPEN TO START. The
            // mesh is modelled in fused_body.gd's own rest (arms hanging), and
            // the bind pose maps that into each bone's space, so a clip that
            // hangs the arms about as far as that bends the skin by almost
            // nothing -- see BodyRig.RestBindMatrix. Reading the live T-pose
            // skeleton here (what this did) bound a hanging mesh to bones
            // lying out sideways.
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

            BuildStub(mesh, limbCapsules);
            return mesh;
        }

        /// <summary>
        /// The torso as ONE tube, on the spine. It was two (the pelvis on the hips, which PlayerLocomotion holds still, and the chest on the
        /// spine, which leans with the walk), and walking or strafing moved the top half while the bottom stayed frozen (owner, 2026-10-10:
        /// "the top half of the torso moves while the bottom stays frozen ... make the whole thing move as a unit"). On the spine the whole
        /// torso leans and sways together; the legs' tops overlap its bottom.
        /// </summary>
        private static List<FusedBodyShape.Segment> OneTorso(List<FusedBodyShape.Segment> segments)
        {
            int hips = segments.FindIndex(s => s.Bone == HumanBodyBones.Hips);
            int spine = segments.FindIndex(s => s.Bone == HumanBodyBones.Spine);
            if (hips < 0 || spine < 0)
            {
                return segments;
            }
            var torso = new FusedBodyShape.Segment(segments[hips].A, segments[spine].B, segments[spine].Radius, HumanBodyBones.Spine);
            var result = new List<FusedBodyShape.Segment>(segments.Count - 1) { torso };
            for (int i = 0; i < segments.Count; i++)
            {
                if (i != hips && i != spine)
                {
                    result.Add(segments[i]);
                }
            }
            return result;
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
