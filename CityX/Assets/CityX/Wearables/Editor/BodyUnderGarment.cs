using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Which of the body's triangles a garment hides (SkinnedGarment.HiddenBodyTriangles), as games hide the body under clothes: what is not
    /// drawn cannot poke through the cloth when a limb moves further than the cloth follows. A triangle is hidden only when it is skinned to
    /// bones the garment moves with (no legs: they come out of a coat's slit) and the garment covers it from every side: rays out along its
    /// normal and tilted round it all hit the garment, in the bind pose both share. So what shows through an opening stays.
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

        /// <summary>
        /// The mask (one bit per body triangle, in the body mesh's order) of the triangles <paramref name="garment"/> hides, of those skinned to
        /// <paramref name="hideable"/>, and the body's triangle count it was made for; null when there is no body to measure.
        /// </summary>
        public static byte[] HiddenTriangles(Mesh garment, HumanBodyBones[] hideable, out int bodyTriangles)
        {
            bodyTriangles = 0;
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            if (body == null)
            {
                Debug.LogError($"CityX: no body at {BodyMeshPath} -- rebuild the scene first; the body under {garment.name} stays whole.");
                return null;
            }
            // The body's bone order is FusedBodyBuilder's: each bone the first time BodyRigBuilder.Segments names it.
            var order = new List<HumanBodyBones>();
            foreach (FusedBodyShape.Segment segment in BodyRigBuilder.Segments())
            {
                if (!order.Contains(segment.Bone))
                {
                    order.Add(segment.Bone);
                }
            }
            var hideableIndex = new HashSet<int>();
            for (int i = 0; i < order.Count; i++)
            {
                if (System.Array.IndexOf(hideable, order[i]) >= 0)
                {
                    hideableIndex.Add(i);
                }
            }

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
                    float share = (Share(weights[triangles[t * 3]], hideableIndex) + Share(weights[triangles[t * 3 + 1]], hideableIndex) +
                        Share(weights[triangles[t * 3 + 2]], hideableIndex)) / 3.0f;
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (share < HideableShare || normal.sqrMagnitude < 1e-12f || !Covered((a + b + c) / 3.0f, normal.normalized))
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

        private static float Share(BoneWeight w, HashSet<int> bones) =>
            (bones.Contains(w.boneIndex0) ? w.weight0 : 0.0f) + (bones.Contains(w.boneIndex1) ? w.weight1 : 0.0f) +
            (bones.Contains(w.boneIndex2) ? w.weight2 : 0.0f) + (bones.Contains(w.boneIndex3) ? w.weight3 : 0.0f);
    }
}
