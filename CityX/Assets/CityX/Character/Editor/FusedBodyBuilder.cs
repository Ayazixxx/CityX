using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the CITY X body mesh and binds it to the Mixamo skeleton.
    ///
    /// This is what replaces the imported mannequin. The animations stay --
    /// they are retargeted onto a humanoid avatar and do not care what shape
    /// hangs off the bones -- but the shape becomes the game's own again:
    /// blended capsules and a sphere head.
    ///
    /// THE SKELETON'S BIND POSE IS THE SOURCE OF THE PROPORTIONS. Reading the
    /// bone positions out of the rig, rather than using the Godot build's own
    /// hardcoded joint coordinates, is what gives human arms and legs on a
    /// body whose thicknesses are still the ones that were drawn. It also
    /// means the skin fits the skeleton exactly, so nothing has to be nudged
    /// afterwards to stop a shoulder poking through.
    /// </summary>
    public static class FusedBodyBuilder
    {
        private const string MeshPath = "Assets/CityX/Character/FusedBody.asset";

        [MenuItem("CityX/Rebuild Body Mesh")]
        public static void BuildMenu()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Animations/idle.fbx");
            if (model == null)
            {
                Debug.LogError("CityX: idle.fbx not found.");
                return;
            }

            GameObject instance = Object.Instantiate(model);
            try
            {
                Build(instance.GetComponent<Animator>());
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Builds the mesh against <paramref name="animator"/>'s skeleton and
        /// saves it. Returns null, having logged, if the rig is unusable.
        /// </summary>
        public static Mesh Build(Animator animator)
        {
            return Build(animator, out _);
        }

        /// <summary>
        /// Builds the mesh and reports the bones it was skinned against, IN
        /// ORDER. The order matters: a SkinnedMeshRenderer matches its bone
        /// array to the mesh's bind poses by index, so a renderer given the
        /// same bones in a different order produces a character whose arm is
        /// driven by its thigh. Handing the list back is the only way to be
        /// sure the two agree.
        /// </summary>
        public static Mesh Build(Animator animator, out List<Transform> skinnedBones)
        {
            skinnedBones = new List<Transform>();

            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("CityX: no humanoid animator -- cannot build the body.");
                return null;
            }

            Transform root = animator.transform;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (head == null || foot == null)
            {
                Debug.LogError("CityX: the skeleton is missing a head or a foot.");
                return null;
            }

            // Everything is built in the MODEL'S OWN space, so the mesh is
            // independent of whatever scale the character is displayed at.
            Vector3 Position(HumanBodyBones bone)
            {
                Transform t = animator.GetBoneTransform(bone);
                return t == null ? Vector3.zero : root.InverseTransformPoint(t.position);
            }

            // The head bone sits at the base of the skull, so the crown is
            // about one head-radius further up -- the same correction the
            // import measurement makes.
            float height = Position(HumanBodyBones.Head).y - Position(HumanBodyBones.LeftFoot).y;
            height *= 1.13f;

            List<FusedBodyShape.Segment> segments = FusedBodyShape.Segments(Position, height);
            float cellSize = FusedBodyShape.CellSize * (height / FusedBodyShape.ReferenceHeight);
            Bounds bounds = FusedBodyShape.BoundsOf(segments);

            FusedBodyMesh.Extract(segments, bounds, cellSize,
                out List<Vector3> vertices, out List<int> triangles);

            if (vertices.Count == 0 || triangles.Count == 0)
            {
                Debug.LogError("CityX: the body extracted to nothing -- check the bounds.");
                return null;
            }

            Vector3[] normals = FusedBodyMesh.Normals(segments, vertices, cellSize * 0.5f);

            // --- skinning. Every vertex belongs wholly to its nearest capsule's
            // --- bone, which is what the original does.
            var bones = new List<Transform>();
            var boneIndex = new Dictionary<HumanBodyBones, int>();
            foreach (FusedBodyShape.Segment segment in segments)
            {
                if (boneIndex.ContainsKey(segment.Bone))
                {
                    continue;
                }
                Transform bone = animator.GetBoneTransform(segment.Bone);
                if (bone == null)
                {
                    continue;
                }
                boneIndex[segment.Bone] = bones.Count;
                bones.Add(bone);
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

            var bindPoses = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                // The bind pose maps from the mesh's space into the bone's, so
                // the bone's own transform undoes it exactly at rest.
                bindPoses[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
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
                existing.boneWeights = weights;
                existing.bindposes = bindPoses;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }

            AssetDatabase.SaveAssets();
            skinnedBones = bones;
            Debug.Log($"CityX: body mesh built -- {vertices.Count} vertices, " +
                      $"{triangles.Count / 3} triangles, {bones.Count} bones, " +
                      $"rig height {height:0.00}");
            return mesh;
        }

        /// <summary>
        /// Replaces the imported mannequin's meshes with the fused body.
        ///
        /// The Mixamo renderers are DISABLED rather than deleted, because they
        /// belong to the imported FBX: deleting them from a prefab instance is
        /// an override that a reimport can undo, and a character wearing both
        /// bodies at once is a confusing thing to debug.
        /// </summary>
        public static void ApplyTo(GameObject body, Animator animator, Material material)
        {
            Mesh mesh = Build(animator, out List<Transform> bones);
            if (mesh == null)
            {
                return;
            }

            foreach (SkinnedMeshRenderer imported in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                imported.enabled = false;
            }

            var holder = new GameObject("FusedBody");
            holder.transform.SetParent(animator.transform, false);

            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones.ToArray();
            renderer.rootBone = animator.GetBoneTransform(HumanBodyBones.Hips);
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
        }
    }
}
