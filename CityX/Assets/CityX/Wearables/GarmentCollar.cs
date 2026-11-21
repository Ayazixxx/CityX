using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A garment's HIGH COLLAR, read off its own mesh, which hair is tucked under (HairPush.TuckUnder; owner, 2026-11-15: "make it so the kick hair doesnt
    /// clip through the midnight coat ... figure out what techniques would be best to visually attain a natural look because right now the back spikes are
    /// clipping through"). The collar is the part skinned to the collar helper bone (GarmentHelperBones); its rim is where its top is behind the neck, on
    /// the neck's line, and it is given in that bone's frame, so it turns and rises with the collar as it is drawn.
    /// </summary>
    public static class GarmentCollar
    {
        /// <summary>A vertex at least this much on the collar bone is the collar's.</summary>
        public const float CollarWeight = 0.5f;

        /// <summary>The garment's bone index the collar helper is skinned to: after the body's own bones, in GarmentHelperBones' order.</summary>
        public static int CollarBone
        {
            get
            {
                for (int i = 0; i < GarmentHelperBones.Helpers.Length; i++)
                {
                    if (GarmentHelperBones.Helpers[i].kind == GarmentHelperBones.Kind.Collar)
                    {
                        return SkinnedGarment.Bones.Length + i;
                    }
                }
                return -1;
            }
        }

        private static readonly Dictionary<(Mesh, float), (bool found, Vector3 rim)> Measured = new Dictionary<(Mesh, float), (bool, Vector3)>();

        /// <summary>
        /// The rim in the garment's own space (the body's at rest: up +Y, forward +Z): on the line up the middle of the collar, at the height of its
        /// highest point behind the neck, or, given <paramref name="halfWidth"/>, its highest straight behind the head, within that of the middle line.
        /// False when nothing of the garment is on the collar bone.
        /// </summary>
        public static bool TryMeasure(IReadOnlyList<Vector3> vertices, IReadOnlyList<BoneWeight> weights, int bone, out Vector3 rim,
            float halfWidth = float.PositiveInfinity)
        {
            rim = Vector3.zero;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            int count = 0;
            for (int i = 0; i < vertices.Count && i < weights.Count; i++)
            {
                if (WeightOn(weights[i], bone) < CollarWeight)
                {
                    continue;
                }
                Vector3 v = vertices[i];
                minX = Mathf.Min(minX, v.x);
                maxX = Mathf.Max(maxX, v.x);
                minZ = Mathf.Min(minZ, v.z);
                maxZ = Mathf.Max(maxZ, v.z);
                count++;
            }
            if (count == 0)
            {
                return false;
            }
            float middleX = (minX + maxX) * 0.5f, middleZ = (minZ + maxZ) * 0.5f;
            float top = float.MinValue;
            for (int i = 0; i < vertices.Count && i < weights.Count; i++)
            {
                if (vertices[i].z < middleZ && Mathf.Abs(vertices[i].x - middleX) <= halfWidth && WeightOn(weights[i], bone) >= CollarWeight)
                {
                    top = Mathf.Max(top, vertices[i].y);
                }
            }
            if (top == float.MinValue)
            {
                return false;
            }
            rim = new Vector3(middleX, top, middleZ);
            return true;
        }

        /// <summary>The garment's collar rim, measured once a mesh.</summary>
        public static bool TryMeasure(Mesh mesh, out Vector3 rim, float halfWidth = float.PositiveInfinity)
        {
            if (mesh == null || !mesh.isReadable)
            {
                rim = Vector3.zero;
                return false;
            }
            if (!Measured.TryGetValue((mesh, halfWidth), out (bool found, Vector3 rim) measured))
            {
                bool found = TryMeasure(mesh.vertices, mesh.boneWeights, CollarBone, out Vector3 at, halfWidth);
                measured = (found, at);
                Measured[(mesh, halfWidth)] = measured;
            }
            rim = measured.rim;
            return measured.found;
        }

        /// <summary>
        /// Tucks the hair of the body under the garment's collar (HairPush.TuckUnder), the rim put in the collar bone's frame by the garment's own bindpose.
        /// False when the garment has no collar or the body no collar bone.
        /// </summary>
        public static bool TuckHairUnder(Animator body, Mesh garment, float halfWidth = float.PositiveInfinity)
        {
            int bone = CollarBone;
            Transform neck = body != null && body.isHuman ? body.GetBoneTransform(HumanBodyBones.Neck) : null;
            Transform collar = neck != null ? neck.Find(GarmentHelperBones.Helpers[bone - SkinnedGarment.Bones.Length].name) : null;
            if (collar == null || garment == null || garment.bindposes.Length <= bone || !TryMeasure(garment, out Vector3 rim, halfWidth))
            {
                return false;
            }
            Matrix4x4 toBone = garment.bindposes[bone];
            HairPush.TuckUnder(body, collar, toBone.MultiplyPoint3x4(rim), toBone.MultiplyVector(Vector3.up), toBone.MultiplyVector(Vector3.forward));
            return true;
        }

        private static float WeightOn(BoneWeight weight, int bone)
        {
            float on = 0.0f;
            if (weight.boneIndex0 == bone) on += weight.weight0;
            if (weight.boneIndex1 == bone) on += weight.weight1;
            if (weight.boneIndex2 == bone) on += weight.weight2;
            if (weight.boneIndex3 == bone) on += weight.weight3;
            return on;
        }
    }
}
