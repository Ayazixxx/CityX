using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The CITY X skeleton and its proportions, taken straight from
    /// character/fused_body.gd. Fourteen bones, and every length is the
    /// original's.
    ///
    /// WHY NOT JUST USE THE MIXAMO SKELETON, which is what the first attempt
    /// did. Two reasons, and both showed up on screen.
    ///
    /// The proportions were wrong. Deriving limb lengths from the imported rig
    /// gave a human figure wearing CITY X thicknesses, and the character's own
    /// silhouette -- long arms reaching past the hips, short legs, a big head
    /// sphere -- is most of what makes it look like itself.
    ///
    /// And the skin tore. Mixamo's rig bends through Spine1, Spine2 and a
    /// clavicle at each shoulder. The body has no geometry for any of those, so
    /// nothing was weighted to them -- and every time an animation rotated one,
    /// the surface between the bones that WERE weighted got pulled apart.
    /// Smoothing the weights helped and could not fix it, because the problem
    /// is a joint with no skin attached to it at all. This rig has no such
    /// bones: every joint that moves has geometry on it.
    ///
    /// LAID OUT IN A T-POSE, unlike the Godot original, which rests with its
    /// arms down. Unity's humanoid retargeting measures a rig against a T-pose
    /// to work out what its muscles mean, so the bind pose has to be one. The
    /// LENGTHS are what carry the proportions, and those are unchanged -- the
    /// idle animation puts the arms back down where they belong.
    /// </summary>
    public static class BodyRig
    {
        // --- fused_body.gd's own joint coordinates ---

        public const float HipY = 0.9f;
        public const float ShoulderY = 1.9f;
        public const float HipX = 0.17f;
        public const float ShoulderX = 0.35f;
        public const float HandX = 0.58f;
        public const float HandY = 0.88f;

        public const float LegRadius = 0.135f;

        /// <summary>
        /// fused_body.gd FOOT_Y. Set to exactly the leg's radius so the
        /// capsule's rounded bottom cap lands on y=0 rather than sinking the
        /// feet through the floor by that much.
        /// </summary>
        public const float FootY = LegRadius;

        public const float ElbowT = 0.5f;
        public const float WristT = 0.85f;
        public const float KneeT = 0.5f;
        public const float SpineT = 0.5f;

        /// <summary>
        /// Where the head sphere's centre sits above the shoulders.
        ///
        /// EXPLICIT ASK: "make the head size and placement exactly the same
        /// as it was in godot." character.gd's own _define_parts() pivots
        /// the head at FusedBody.NECK_Y (== ShoulderY + NeckLength, not
        /// ShoulderY alone) and offsets it up by the sphere's own radius on
        /// top of that -- this was missing the NeckLength term (a 1mm
        /// difference, previously judged close enough at "hud.gd puts it
        /// around 2.25"), now added for an exact match.
        /// </summary>
        public const float HeadCentreY = ShoulderY + FusedBodyShape.NeckLength + FusedBodyShape.HeadRadius;

        /// <summary>
        /// Arm length, shoulder to fingertip, measured on the ORIGINAL resting
        /// layout -- the arm hangs down and outward there, so its length is the
        /// diagonal, not the drop. Getting this from the rest pose and then
        /// laying it out horizontally is what keeps the reach identical while
        /// still giving Unity the T-pose it needs.
        /// </summary>
        public static readonly float ArmLength = new Vector2(
            HandX - ShoulderX, ShoulderY - HandY).magnitude;

        public static readonly float LegLength = HipY - FootY;

        /// <summary>One bone: its name, its parent, and where it sits at rest.</summary>
        public readonly struct Bone
        {
            public readonly string Name;
            public readonly string Parent;
            public readonly Vector3 Position;
            public readonly HumanBodyBones Human;

            public Bone(string name, string parent, Vector3 position, HumanBodyBones human)
            {
                Name = name;
                Parent = parent;
                Position = position;
                Human = human;
            }
        }

        /// <summary>
        /// Every bone, parents before children. fused_body.gd's own BONES and
        /// BONE_PARENT, plus a foot at each ankle: Unity requires feet for a
        /// humanoid avatar and the original rig has none, its shin capsule's
        /// rounded end being the foot. They carry no geometry.
        /// </summary>
        public static IReadOnlyList<Bone> Bones()
        {
            float elbowX = ShoulderX + ArmLength * ElbowT;
            float wristX = ShoulderX + ArmLength * WristT;
            float tipX = ShoulderX + ArmLength;

            float kneeY = HipY - LegLength * KneeT;

            return new[]
            {
                new Bone("hips", null, new Vector3(0.0f, HipY, 0.0f), HumanBodyBones.Hips),
                new Bone("spine", "hips",
                    new Vector3(0.0f, Mathf.Lerp(HipY, ShoulderY, SpineT), 0.0f),
                    HumanBodyBones.Spine),
                new Bone("neck", "spine", new Vector3(0.0f, ShoulderY, 0.0f),
                    HumanBodyBones.Neck),
                new Bone("head", "neck", new Vector3(0.0f, HeadCentreY, 0.0f),
                    HumanBodyBones.Head),

                new Bone("arm_l", "spine", new Vector3(-ShoulderX, ShoulderY, 0.0f),
                    HumanBodyBones.LeftUpperArm),
                new Bone("forearm_l", "arm_l", new Vector3(-elbowX, ShoulderY, 0.0f),
                    HumanBodyBones.LeftLowerArm),
                new Bone("hand_l", "forearm_l", new Vector3(-wristX, ShoulderY, 0.0f),
                    HumanBodyBones.LeftHand),
                new Bone("fingertip_l", "hand_l", new Vector3(-tipX, ShoulderY, 0.0f),
                    HumanBodyBones.LastBone),

                new Bone("arm_r", "spine", new Vector3(ShoulderX, ShoulderY, 0.0f),
                    HumanBodyBones.RightUpperArm),
                new Bone("forearm_r", "arm_r", new Vector3(elbowX, ShoulderY, 0.0f),
                    HumanBodyBones.RightLowerArm),
                new Bone("hand_r", "forearm_r", new Vector3(wristX, ShoulderY, 0.0f),
                    HumanBodyBones.RightHand),
                new Bone("fingertip_r", "hand_r", new Vector3(tipX, ShoulderY, 0.0f),
                    HumanBodyBones.LastBone),

                new Bone("thigh_l", "hips", new Vector3(-HipX, HipY, 0.0f),
                    HumanBodyBones.LeftUpperLeg),
                new Bone("shin_l", "thigh_l", new Vector3(-HipX, kneeY, 0.0f),
                    HumanBodyBones.LeftLowerLeg),
                new Bone("foot_l", "shin_l", new Vector3(-HipX, FootY, 0.0f),
                    HumanBodyBones.LeftFoot),

                new Bone("thigh_r", "hips", new Vector3(HipX, HipY, 0.0f),
                    HumanBodyBones.RightUpperLeg),
                new Bone("shin_r", "thigh_r", new Vector3(HipX, kneeY, 0.0f),
                    HumanBodyBones.RightLowerLeg),
                new Bone("foot_r", "shin_r", new Vector3(HipX, FootY, 0.0f),
                    HumanBodyBones.RightFoot),
            };
        }

        /// <summary>
        /// The arm-and-torso-relevant project bones at fused_body.gd's OWN
        /// rest: arms hanging down and outward, not the T-pose Bones() lays
        /// out for Unity's own humanoid retargeting (see that method's own
        /// doc comment for why the two have to differ for the LIVE rig).
        ///
        /// The wearable meshes -- hooded_cloak.glb, skelli_jeans.glb -- were
        /// authored against exactly THIS pose, since Godot's own body never
        /// had a T-pose to begin with. Binding them against Bones() instead
        /// (as SkinnedGarmentBuilder originally did, on the mistaken analogy
        /// that "whatever rest skeleton the body itself binds against" was
        /// the right choice for every mesh) leaves every arm-weighted
        /// garment vertex off by however far T-pose moved that bone from
        /// its actual arms-down rest -- upwards of a metre at the wrist --
        /// which is a skinning error the Animator's own arms-down pose never
        /// cancels out, because the whole point of a bindpose is to record
        /// where a bone was WHEN the mesh's own vertices were authored, and
        /// these vertices were never anywhere near T-pose.
        ///
        /// Only the arm chain actually needs its own numbers: the legs
        /// already hang straight down in both conventions, and ElbowT/
        /// WristT are exactly the fractions fused_body.gd itself measures
        /// the elbow and wrist at -- interpolated here along the real
        /// shoulder-to-hand diagonal instead of Bones()'s own flattened-to-
        /// horizontal one. Feet and fingertips are Unity-only additions
        /// Bones() needs for a valid humanoid avatar; no garment vertex is
        /// ever weighted to them, so this omits both rather than inventing
        /// rest positions nothing reads.
        /// </summary>
        public static IReadOnlyList<Bone> RestBones()
        {
            var shoulderL = new Vector3(-ShoulderX, ShoulderY, 0.0f);
            var handL = new Vector3(-HandX, HandY, 0.0f);
            var shoulderR = new Vector3(ShoulderX, ShoulderY, 0.0f);
            var handR = new Vector3(HandX, HandY, 0.0f);
            float kneeY = HipY - LegLength * KneeT;

            return new[]
            {
                new Bone("hips", null, new Vector3(0.0f, HipY, 0.0f), HumanBodyBones.Hips),
                new Bone("spine", "hips",
                    new Vector3(0.0f, Mathf.Lerp(HipY, ShoulderY, SpineT), 0.0f),
                    HumanBodyBones.Spine),
                new Bone("neck", "spine", new Vector3(0.0f, ShoulderY, 0.0f),
                    HumanBodyBones.Neck),
                new Bone("head", "neck", new Vector3(0.0f, HeadCentreY, 0.0f),
                    HumanBodyBones.Head),

                new Bone("arm_l", "spine", shoulderL, HumanBodyBones.LeftUpperArm),
                new Bone("forearm_l", "arm_l", Vector3.Lerp(shoulderL, handL, ElbowT),
                    HumanBodyBones.LeftLowerArm),
                new Bone("hand_l", "forearm_l", Vector3.Lerp(shoulderL, handL, WristT),
                    HumanBodyBones.LeftHand),

                new Bone("arm_r", "spine", shoulderR, HumanBodyBones.RightUpperArm),
                new Bone("forearm_r", "arm_r", Vector3.Lerp(shoulderR, handR, ElbowT),
                    HumanBodyBones.RightLowerArm),
                new Bone("hand_r", "forearm_r", Vector3.Lerp(shoulderR, handR, WristT),
                    HumanBodyBones.RightHand),

                new Bone("thigh_l", "hips", new Vector3(-HipX, HipY, 0.0f),
                    HumanBodyBones.LeftUpperLeg),
                new Bone("shin_l", "thigh_l", new Vector3(-HipX, kneeY, 0.0f),
                    HumanBodyBones.LeftLowerLeg),

                new Bone("thigh_r", "hips", new Vector3(HipX, HipY, 0.0f),
                    HumanBodyBones.RightUpperLeg),
                new Bone("shin_r", "thigh_r", new Vector3(HipX, kneeY, 0.0f),
                    HumanBodyBones.RightLowerLeg),
            };
        }

        /// <summary>Where a bone sits at rest, by its humanoid role.</summary>
        public static Vector3 PositionOf(HumanBodyBones human)
        {
            foreach (Bone bone in Bones())
            {
                if (bone.Human == human)
                {
                    return bone.Position;
                }
            }
            return Vector3.zero;
        }

        /// <summary>
        /// The crown of the head: the top of the sphere. Used as the body's
        /// height, which every radius is expressed as a fraction of.
        /// </summary>
        public static float StandingHeight => HeadCentreY + FusedBodyShape.HeadRadius;
    }
}
