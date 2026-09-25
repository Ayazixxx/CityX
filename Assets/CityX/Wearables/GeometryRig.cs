using CityX.Character;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Bone weights from vertex position alone, against BodyRig.RestBones'
    /// own arms-down joint positions -- shared between
    /// BlackPufferJacketBuilder's own editor-time permanent bake and
    /// BlackPufferJacket's own runtime live-placement re-tuning (the
    /// /jacket console command), so the two can never silently drift into
    /// two different formulas for the same rig. Lives here, outside
    /// Editor/, because the console needs to call it at real runtime, not
    /// just at build time. See BlackPufferJacketBuilder's own class
    /// comment for why this exists at all (no source skeleton to remap
    /// weights from).
    /// </summary>
    public static class GeometryRig
    {
        /// <summary>
        /// Vertical reach, below BodyRig.HipY, that still counts as "torso
        /// hem" rather than "sleeve cuff" for the hips/spine blend below --
        /// a puffer jacket's own hem typically falls at or a little past
        /// the hip, not the full reach to the hand BodyRig.RestBones' own
        /// hand_l/hand_r sit at.
        /// </summary>
        private const float HemBlendBand = 0.3f;

        /// <summary>Indices into SkinnedGarment.Bones -- see that array's own doc comment on why order is load-bearing.</summary>
        public static class BoneIndex
        {
            public const int Hips = 0, Spine = 1, ArmL = 4, ForearmL = 5, ArmR = 7, ForearmR = 8;
        }

        /// <summary>Where the sleeve's own arm-to-forearm blend switches over, in BodyRig.RestBones' own rest pose.</summary>
        public static float ElbowY() => Mathf.Lerp(BodyRig.ShoulderY, BodyRig.HandY, BodyRig.ElbowT);

        /// <summary>
        /// Purely geometric: within the torso's own radius, hips/spine by
        /// height (see HemBlendBand); beyond it, a spine-to-arm blend by
        /// horizontal distance (the same shape MidnightPatchworkCoatBuilder.
        /// SmoothShoulderWeights already uses for a real, measured seam)
        /// composed with an arm-to-forearm blend by height along the
        /// sleeve. No hand/neck/head/leg influence at all -- a jacket never
        /// reaches any of them.
        /// </summary>
        public static BoneWeight TorsoAndSleeveWeight(Vector3 p, float elbowY)
        {
            float absX = Mathf.Abs(p.x);

            if (absX <= FusedBodyShape.TorsoRadius)
            {
                float hipsT = Mathf.Clamp01((BodyRig.HipY - p.y) / HemBlendBand);
                return TwoBone(BoneIndex.Hips, hipsT, BoneIndex.Spine, 1.0f - hipsT);
            }

            float spineToArm = Mathf.SmoothStep(0.0f, 1.0f,
                Mathf.InverseLerp(FusedBodyShape.TorsoRadius, BodyRig.ShoulderX, absX));
            float armToForearm = Mathf.Clamp01((BodyRig.ShoulderY - p.y) / (BodyRig.ShoulderY - elbowY));

            bool left = p.x < 0.0f;
            int armIndex = left ? BoneIndex.ArmL : BoneIndex.ArmR;
            int forearmIndex = left ? BoneIndex.ForearmL : BoneIndex.ForearmR;

            float spineWeight = 1.0f - spineToArm;
            float armWeight = spineToArm * (1.0f - armToForearm);
            float forearmWeight = spineToArm * armToForearm;

            return ThreeBone(BoneIndex.Spine, spineWeight, armIndex, armWeight, forearmIndex, forearmWeight);
        }

        private static BoneWeight TwoBone(int i0, float w0, int i1, float w1)
        {
            return new BoneWeight { boneIndex0 = i0, weight0 = w0, boneIndex1 = i1, weight1 = w1 };
        }

        private static BoneWeight ThreeBone(int i0, float w0, int i1, float w1, int i2, float w2)
        {
            return new BoneWeight
            {
                boneIndex0 = i0, weight0 = w0,
                boneIndex1 = i1, weight1 = w1,
                boneIndex2 = i2, weight2 = w2,
            };
        }
    }
}
