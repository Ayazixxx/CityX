using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A DIVER'S TUCK, laid over whatever the Animator posed (the Recovery flip, Ragdoll.Recovery.cs): the back rounded, the chin down, the knees
    /// pulled up to the chest with the heels under them, the hands on the shins. Each bone is turned to point where the tuck has it, in the
    /// body's own frame (x right, y up, z forward), from the hips out so every child is aimed from where its parent now is; a weight between 0
    /// and 1 turns it part of the way, so the flip curls in and opens out.
    /// </summary>
    public static class TuckPose
    {
        private readonly struct Aim
        {
            public readonly HumanBodyBones Bone;
            public readonly HumanBodyBones Toward;
            public readonly HumanBodyBones OrToward;
            public readonly Vector3 Direction;

            public Aim(HumanBodyBones bone, HumanBodyBones toward, Vector3 direction, HumanBodyBones orToward = HumanBodyBones.LastBone)
            {
                Bone = bone;
                Toward = toward;
                OrToward = orToward;
                Direction = direction.normalized;
            }
        }

        /// <summary>Where each bone points in the tuck, parents first. Left is -x.</summary>
        private static readonly Aim[] Aims =
        {
            new Aim(HumanBodyBones.Hips, HumanBodyBones.Spine, new Vector3(0.0f, 1.0f, 0.15f)),
            new Aim(HumanBodyBones.Spine, HumanBodyBones.Chest, new Vector3(0.0f, 0.85f, 0.5f)),
            new Aim(HumanBodyBones.Chest, HumanBodyBones.UpperChest, new Vector3(0.0f, 0.6f, 0.8f), HumanBodyBones.Neck),
            new Aim(HumanBodyBones.UpperChest, HumanBodyBones.Neck, new Vector3(0.0f, 0.5f, 0.86f)),
            new Aim(HumanBodyBones.Neck, HumanBodyBones.Head, new Vector3(0.0f, 0.45f, 0.9f)),
            new Aim(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, new Vector3(-0.15f, 0.55f, 0.82f)),
            new Aim(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, new Vector3(0.15f, 0.55f, 0.82f)),
            new Aim(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, new Vector3(-0.03f, -0.95f, -0.3f)),
            new Aim(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, new Vector3(0.03f, -0.95f, -0.3f)),
            new Aim(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, new Vector3(0.0f, -0.6f, 0.8f)),
            new Aim(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, new Vector3(0.0f, -0.6f, 0.8f)),
            new Aim(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-0.45f, -0.8f, 0.4f)),
            new Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(0.45f, -0.8f, 0.4f)),
            new Aim(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(0.45f, -0.75f, -0.45f)),
            new Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(-0.45f, -0.75f, -0.45f)),
        };

        /// <summary>Turns the body <paramref name="weight"/> of the way into the tuck, its frame <paramref name="body"/>'s.</summary>
        public static void Apply(Animator animator, Transform body, float weight)
        {
            if (weight <= 0.0f || animator == null || !animator.isHuman || body == null)
            {
                return;
            }
            weight = Mathf.Min(weight, 1.0f);
            foreach (Aim aim in Aims)
            {
                Transform bone = animator.GetBoneTransform(aim.Bone);
                Transform toward = animator.GetBoneTransform(aim.Toward);
                if (toward == null && aim.OrToward != HumanBodyBones.LastBone)
                {
                    toward = animator.GetBoneTransform(aim.OrToward);
                }
                if (bone == null || toward == null)
                {
                    continue;
                }
                Vector3 now = toward.position - bone.position;
                if (now.sqrMagnitude < 1e-8f)
                {
                    continue;
                }
                Vector3 wanted = Vector3.Slerp(now.normalized, body.TransformDirection(aim.Direction), weight);
                bone.rotation = Quaternion.FromToRotation(now, wanted) * bone.rotation;
            }
        }
    }
}
