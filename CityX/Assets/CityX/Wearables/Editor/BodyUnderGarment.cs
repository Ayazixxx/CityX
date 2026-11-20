using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Which of the body's triangles a garment hides (SkinnedGarment.HiddenBodyTriangles), as games hide the body under clothes: what is not
    /// drawn cannot poke through the cloth when a limb moves further than the cloth follows. Some bones' skin the garment always hides (a coat:
    /// the torso and upper arms); some only where it covers them from every side (and skin shared with an always-hidden bone with them): rays
    /// out along the triangle's normal and tilted round it all hit the garment, in the bind pose both share, so what shows through an opening stays.
    /// </summary>
    public static class BodyUnderGarment
    {
        private const string BodyMeshPath = "Assets/CityX/Character/FusedBody.asset";

        /// <summary>How far out the garment may sit over the skin it covers.</summary>
        private const float Reach = 0.3f;

        /// <summary>How far round from the normal the side rays lean (tan of the angle: 0.7 is 35 degrees).</summary>
        private const float Lean = 0.7f;

        /// <summary>The shares of a triangle's skinning past which it is the garment's to hide.</summary>
        private const float HideableShare = 0.7f;

        /// <summary>The share of a triangle's skinning past which it belongs to the bones the garment always hides (half: up to the joint).</summary>
        private const float AlwaysShare = 0.5f;

        /// <summary>
        /// The mask (one bit per body triangle, in the body mesh's order) of the triangles <paramref name="garment"/> hides: those skinned to
        /// <paramref name="always"/> (but inside <paramref name="opening"/>, the bind-pose box round an opening such as a coat's open front,
        /// only where it covers them, so the skin seen through it stays), and those skinned to <paramref name="hideable"/> that it covers; with
        /// the body's triangle count it was made for. Bones in <paramref name="alwaysWithin"/> count as <paramref name="always"/> inside
        /// <paramref name="within"/> (a coat's back: the hips, which its open front shows). Triangles whose bind-pose centre <paramref name="shown"/>
        /// accepts are never hidden (the skin a garment leaves bare: inside a crew neck, below a short sleeve). Null when there is no body.
        /// </summary>
        public static byte[] HiddenTriangles(Mesh garment, HumanBodyBones[] always, HumanBodyBones[] alwaysWithin, Bounds within,
            HumanBodyBones[] hideable, Bounds opening, out int bodyTriangles, System.Predicate<Vector3> shown = null)
        {
            bodyTriangles = 0;
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            if (body == null)
            {
                Debug.LogError($"CityX: no body at {BodyMeshPath} -- rebuild the scene first; the body under {garment.name} stays whole.");
                return null;
            }
            List<HumanBodyBones> order = BodyRigBuilder.SkinBoneOrder();
            HashSet<int> alwaysIndex = Indices(order, always);
            HashSet<int> withinIndex = Indices(order, alwaysWithin);
            // Skin shared between a bone always hidden and one hidden where covered (the waist, between the spine and the hips) counts both:
            // neither alone was enough to hide it, and a band of it showed through a shirt as the torso twisted.
            HashSet<int> hideableIndex = Indices(order, hideable);
            hideableIndex.UnionWith(alwaysIndex);

            var cover = new GameObject("GarmentCover");
            bool hitBackfaces = Physics.queriesHitBackfaces;
            try
            {
                cover.AddComponent<MeshCollider>().sharedMesh = garment;
                Physics.SyncTransforms();
                Physics.queriesHitBackfaces = true;
                Vector3[] vertices = body.vertices;
                BoneWeight[] weights = body.boneWeights;
                int[] triangles = body.triangles;
                bodyTriangles = triangles.Length / 3;
                var mask = new byte[(bodyTriangles + 7) / 8];
                int hidden = 0;
                for (int t = 0; t < bodyTriangles; t++)
                {
                    Vector3 a = vertices[triangles[t * 3]], b = vertices[triangles[t * 3 + 1]], c = vertices[triangles[t * 3 + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    Vector3 centre = (a + b + c) / 3.0f;
                    bool alwaysBone = TriangleShare(weights, triangles, t, alwaysIndex) >= AlwaysShare ||
                        (within.Contains(centre) && TriangleShare(weights, triangles, t, withinIndex) >= AlwaysShare);
                    bool covered = normal.sqrMagnitude > 1e-12f && Covered(centre, normal.normalized);
                    bool hide = alwaysBone ? covered || !opening.Contains(centre)
                        : TriangleShare(weights, triangles, t, hideableIndex) >= HideableShare && covered;
                    if (!hide || shown != null && shown(centre))
                    {
                        continue;
                    }
                    mask[t / 8] |= (byte)(1 << (t % 8));
                    hidden++;
                }
                Debug.Log($"CityX: {garment.name} hides {hidden} of the body's {bodyTriangles} triangles");
                return mask;
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackfaces;
                Object.DestroyImmediate(cover);
            }
        }

        private static bool Covered(Vector3 centre, Vector3 normal)
        {
            Vector3 across = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 along = Vector3.Cross(normal, across);
            Vector3 origin = centre + normal * 0.002f;
            foreach (Vector3 lean in new[] { Vector3.zero, across, -across, along, -along })
            {
                if (!Physics.Raycast(origin, (normal + lean * Lean).normalized, Reach))
                {
                    return false;
                }
            }
            return true;
        }

        private static HashSet<int> Indices(List<HumanBodyBones> order, HumanBodyBones[] bones)
        {
            var indices = new HashSet<int>();
            for (int i = 0; i < order.Count; i++)
            {
                if (System.Array.IndexOf(bones, order[i]) >= 0)
                {
                    indices.Add(i);
                }
            }
            return indices;
        }

        private static float TriangleShare(BoneWeight[] weights, int[] triangles, int t, HashSet<int> bones) =>
            (Share(weights[triangles[t * 3]], bones) + Share(weights[triangles[t * 3 + 1]], bones) + Share(weights[triangles[t * 3 + 2]], bones)) / 3.0f;

        private static float Share(BoneWeight w, HashSet<int> bones) =>
            (bones.Contains(w.boneIndex0) ? w.weight0 : 0.0f) + (bones.Contains(w.boneIndex1) ? w.weight1 : 0.0f) +
            (bones.Contains(w.boneIndex2) ? w.weight2 : 0.0f) + (bones.Contains(w.boneIndex3) ? w.weight3 : 0.0f);
    }
}
