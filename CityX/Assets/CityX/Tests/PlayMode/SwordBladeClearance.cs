using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// How far a held blade stays out of the body holding it, metres (below 0, inside): measured along the blade, from
    /// <c>fromShare</c> of the way from the hand to the tip, against the body's own shape (FusedBodyShape.Segments) at the size the body is,
    /// all but the arms on the handle (the sword arm's forearm, and with two hands both arms), and against the head.
    /// </summary>
    internal static class SwordBladeClearance
    {
        public static float Measure(Animator animator, ISwordBlade blade, bool oneHanded, float fromShare, out string where)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            float bodyScale = Vector3.Distance(hips.position, neck.position)
                / Vector3.Distance(BodyRig.ShapePositionOf(HumanBodyBones.Hips), BodyRig.ShapePositionOf(HumanBodyBones.Neck));
            List<FusedBodyShape.Segment> body = FusedBodyShape.Segments(b => animator.GetBoneTransform(b).position,
                FusedBodyShape.ReferenceHeight * bodyScale);
            body.RemoveAll(s => s.Bone == HumanBodyBones.RightLowerArm || s.Bone == HumanBodyBones.RightHand
                || (!oneHanded && (s.Bone == HumanBodyBones.RightUpperArm || s.Bone == HumanBodyBones.LeftUpperArm
                    || s.Bone == HumanBodyBones.LeftLowerArm || s.Bone == HumanBodyBones.LeftHand))
                // (A spear's front hand holds the shaft itself, ahead of the rear one: its fingers are round what is measured.)
                || (blade.IsSpear && s.Bone >= HumanBodyBones.LeftThumbProximal && s.Bone <= HumanBodyBones.LeftLittleDistal));
            float worst = float.MaxValue;
            where = "";
            for (float along = fromShare; along <= 1.0001f; along += 0.1f)
            {
                Vector3 point = Vector3.Lerp(blade.BladeBaseWorld, blade.BladeTipWorld, along);
                float inBody = FusedBodyShape.Distance(body, point);
                float inHead = Vector3.Distance(point, head.position) - FusedBodyShape.HeadRadius * bodyScale;
                float clear = Mathf.Min(inBody, inHead);
                if (clear < worst)
                {
                    worst = clear;
                    where = $"{(inBody < inHead ? FusedBodyShape.NearestBone(body, point).ToString() : "head")}, {along:0.0} along the blade";
                }
            }
            return worst;
        }
    }
}
