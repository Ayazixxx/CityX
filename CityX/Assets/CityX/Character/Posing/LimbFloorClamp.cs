using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Holds a pose on the ground it is on, for clips made on another body and on a level floor: the get-up pushes off the floor with arms
    /// longer than the ones it was made for and kneels on hips set too low for this body, and its fingers, seat and soles went up to 25 cm
    /// under the floor; and the ground under a body that has fallen is not level (owner, 2026-11-10: "use hand and foot ik to interact with
    /// the terrain it is standing on properly"). The hips are lifted clear of the ground under them first; then each hand and foot is put
    /// back where the clip had it, moved up or down by as much as the ground under it is above or below the clip's own floor while the clip
    /// has it on or near that floor, and no lower than its skin allows, the elbow or knee bowing up off the ground (<see cref="TwoBoneIK"/>);
    /// a hand whose fingertips still reach under the ground is raised by as much.
    ///
    /// The clearances are the body's own (2.6 m, measured on its skin through the get-up): how far under each bone its skin reaches.
    /// </summary>
    public static class LimbFloorClamp
    {
        /// <summary>The height of the ground under a point in the world.</summary>
        public delegate float FloorUnder(Vector3 at);

        /// <summary>How far over the floor the hips bone is kept: the seat's skin, sitting back on the heels, is this far under it.</summary>
        public const float HipsClearance = 0.26f;

        /// <summary>How far over the floor the spine bone is kept: lying face down, the chest's skin is this far under it.</summary>
        public const float SpineClearance = 0.32f;

        /// <summary>How far over the floor an ankle is kept: the sole is this far under it, the foot flat or rolled onto its toes.</summary>
        public const float AnkleClearance = 0.21f;

        /// <summary>How far over the floor a wrist is kept: the heel of the hand is under it.</summary>
        public const float WristClearance = 0.07f;

        /// <summary>How far over the floor an elbow or knee is kept.</summary>
        public const float JointClearance = 0.08f;

        /// <summary>How far over the floor a fingertip (a finger's last bone, carried on by its own length) is kept: the finger's thickness.</summary>
        public const float FingertipClearance = 0.05f;

        /// <summary>
        /// A hand or foot the clip has within <see cref="OnTheFloor"/> of its own floor is on it, and follows the ground under it in full;
        /// above <see cref="OffTheFloor"/> it is in the air and is left where the clip has it; between, part of the way. Metres.
        /// </summary>
        public const float OnTheFloor = 0.3f;

        public const float OffTheFloor = 0.7f;

        /// <summary>
        /// Applies the clamp to <paramref name="animator"/>'s pose on a level floor at height <paramref name="floorY"/>, at
        /// <paramref name="weight"/> (0 leaves the pose as it is, 1 clamps it in full). After the Animator.
        /// </summary>
        public static void Apply(Animator animator, float floorY, float weight = 1.0f)
        {
            Apply(animator, at => floorY, floorY, weight);
        }

        /// <summary>
        /// Applies the clamp to <paramref name="animator"/>'s pose on the ground <paramref name="floor"/> tells of, the clip's own floor (where
        /// its body's root stands) at height <paramref name="clipFloor"/>, at <paramref name="weight"/>. After the Animator.
        /// </summary>
        public static void Apply(Animator animator, FloorUnder floor, float clipFloor, float weight = 1.0f)
        {
            if (animator == null || !animator.isHuman || weight <= 0.0f)
            {
                return;
            }
            if (weight < 1.0f)
            {
                ApplyPartly(animator, floor, clipFloor, weight);
                return;
            }
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || leftHand == null || rightHand == null || leftFoot == null || rightFoot == null)
            {
                return;
            }
            // Where the clip put the hands and feet, before the hips move them.
            Vector3 leftHandAt = leftHand.position, rightHandAt = rightHand.position, leftFootAt = leftFoot.position, rightFootAt = rightFoot.position;
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            float lift = Mathf.Max(floor(hips.position) + HipsClearance - hips.position.y,
                spine != null ? floor(spine.position) + SpineClearance - spine.position.y : 0.0f);
            bool lifted = lift > 0.0f;
            if (lifted)
            {
                hips.position += Vector3.up * lift;
            }
            Limb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, leftFoot, leftFootAt, AnkleClearance, floor, clipFloor, lifted, false);
            Limb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, rightFoot, rightFootAt, AnkleClearance, floor, clipFloor, lifted,
                false);
            Limb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, leftHand, leftHandAt, WristClearance, floor, clipFloor, lifted, true);
            Limb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, rightHand, rightHandAt, WristClearance, floor, clipFloor, lifted,
                true);
        }

        /// <summary>
        /// How far the ground under <paramref name="at"/> moves a hand or foot the clip has there: all of the ground's own rise or fall from
        /// the clip's floor while the clip has it on that floor, none once it is well off it.
        /// </summary>
        public static float Follows(Vector3 at, float groundThere, float clipFloor)
        {
            float onIt = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(OnTheFloor, OffTheFloor, at.y - clipFloor));
            return (groundThere - clipFloor) * onIt;
        }

        /// <summary>The bones the clamp moves: the hips, then each limb's upper bone, lower bone and hand or foot.</summary>
        private static readonly HumanBodyBones[] Moved =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        };

        /// <summary>The clamp in full, then each moved bone eased back toward where it was by 1 - <paramref name="weight"/>.</summary>
        private static void ApplyPartly(Animator animator, FloorUnder floor, float clipFloor, float weight)
        {
            var bones = new Transform[Moved.Length];
            var rotations = new Quaternion[Moved.Length];
            for (int i = 0; i < Moved.Length; i++)
            {
                bones[i] = animator.GetBoneTransform(Moved[i]);
                rotations[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            }
            Vector3 hipsAt = bones[0] != null ? bones[0].localPosition : Vector3.zero;
            Apply(animator, floor, clipFloor);
            if (bones[0] != null)
            {
                bones[0].localPosition = Vector3.Lerp(hipsAt, bones[0].localPosition, weight);
            }
            for (int i = 0; i < Moved.Length; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].localRotation = Quaternion.Slerp(rotations[i], bones[i].localRotation, weight);
                }
            }
        }

        /// <summary>
        /// Puts <paramref name="end"/> back on <paramref name="endAt"/> (where the clip had it) as the ground under it has moved it, no lower
        /// than <paramref name="clearance"/> over that ground, the joint bowing up where it would go under it; then, for a hand, raises it by
        /// as much as its fingers still reach under.
        /// </summary>
        private static void Limb(Animator animator, HumanBodyBones upperBone, HumanBodyBones lowerBone, Transform end, Vector3 endAt, float clearance,
            FloorUnder floor, float clipFloor, bool lifted, bool hand)
        {
            Transform upper = animator.GetBoneTransform(upperBone), lower = animator.GetBoneTransform(lowerBone);
            if (upper == null || lower == null)
            {
                return;
            }
            float groundThere = floor(endAt);
            float follows = Follows(endAt, groundThere, clipFloor);
            float lowestEnd = groundThere + clearance;
            bool endMoves = endAt.y + follows < lowestEnd || Mathf.Abs(follows) > 1e-4f;
            bool jointUnder = lower.position.y < floor(lower.position) + JointClearance;
            if (!lifted && !endMoves && !jointUnder && (!hand || FingersUnder(end, groundThere) <= 0.0f))
            {
                return;
            }
            Vector3 target = new Vector3(endAt.x, Mathf.Max(endAt.y + follows, lowestEnd), endAt.z);
            Solve(upper, lower, end, target, jointUnder);
            float fingersUnder = hand ? FingersUnder(end, groundThere) : 0.0f;
            if (fingersUnder > 0.0f)
            {
                Solve(upper, lower, end, target + Vector3.up * fingersUnder, jointUnder);
            }
        }

        private static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, bool jointUnder)
        {
            // The elbow or knee bows the way it already does, turned up off the floor where it pointed into it.
            Vector3 bow = lower.position - 0.5f * (upper.position + end.position);
            if (bow.sqrMagnitude < 1e-8f)
            {
                bow = Vector3.up;
            }
            if (jointUnder || bow.y < 0.0f)
            {
                bow.y = Mathf.Abs(bow.y) + bow.magnitude;
            }
            TwoBoneIK.Solve(upper, lower, end, target, bow.normalized);
        }

        /// <summary>
        /// How far the hand's lowest fingertip reaches under its clearance over the floor (0 or less when clear). The fingers are read off the
        /// hand's own children (the avatar names only their first bones): each joint, and past each last bone its own length again.
        /// </summary>
        private static float FingersUnder(Transform hand, float floorY)
        {
            float lowest = Lowest(hand, float.MaxValue);
            return lowest < float.MaxValue ? floorY + FingertipClearance - lowest : 0.0f;
        }

        private static float Lowest(Transform bone, float lowest)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                lowest = Mathf.Min(lowest, child.position.y);
                lowest = child.childCount > 0 ? Lowest(child, lowest) : Mathf.Min(lowest, (2.0f * child.position - bone.position).y);
            }
            return lowest;
        }
    }
}
