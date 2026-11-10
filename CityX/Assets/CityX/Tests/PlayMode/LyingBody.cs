using CityX.Character;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>A limp body laid down a chosen way up, and ground that is not level to lay it on: what the get-up's tests and pictures share.</summary>
    public static class LyingBody
    {
        /// <summary>The stairs' riser and tread, metres.</summary>
        public const float Riser = 0.3f;

        public const float Tread = 0.6f;

        /// <summary>
        /// Turns the limp body, all of it as one, to lie just over the ground with its head toward +X (up the slope and the stairs) and its
        /// chest facing <paramref name="chestFaces"/>. A physics step after it went limp.
        /// </summary>
        public static void LayDown(Ragdoll ragdoll, Animator animator, Vector3 chestFaces)
        {
            Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position, neck = animator.GetBoneTransform(HumanBodyBones.Neck).position;
            Vector3 front = FallFlail.Front(hips, neck, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
            Quaternion turn = Quaternion.LookRotation(chestFaces, Vector3.right) * Quaternion.Inverse(Quaternion.LookRotation(front, (neck - hips).normalized));
            Vector3 pivot = ragdoll.Torso.position;
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                body.position = pivot + Vector3.up * 0.4f + turn * (body.position - pivot);
                body.rotation = turn * body.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Which way the chest faces (FallFlail.Front).</summary>
        public static Vector3 ChestFront(Animator animator)
        {
            return FallFlail.Front(animator.GetBoneTransform(HumanBodyBones.Hips).position, animator.GetBoneTransform(HumanBodyBones.Neck).position,
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position, animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
        }

        /// <summary>Level ground ("level"), a 15 degree slope ("slope") or a flight of stairs ("stairs") rising toward +X, through the origin.</summary>
        public static GameObject BuildGround(string kind)
        {
            var root = new GameObject("Ground");
            if (kind == "stairs")
            {
                for (int i = -12; i <= 12; i++)
                {
                    var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    step.transform.SetParent(root.transform, false);
                    step.transform.localScale = new Vector3(Tread, 1.0f, 5.0f);
                    step.transform.position = new Vector3(i * Tread, -0.5f + i * Riser, 0.0f);
                }
                return root;
            }
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.transform.SetParent(root.transform, false);
            slab.transform.localScale = new Vector3(50.0f, 1.0f, kind == "slope" ? 5.0f : 50.0f);
            slab.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            if (kind == "slope")
            {
                root.transform.rotation = Quaternion.Euler(0.0f, 0.0f, 15.0f);
            }
            return root;
        }

        /// <summary>The height of the ground under <paramref name="at"/>, <paramref name="body"/> looked through; NaN where there is none.</summary>
        public static float GroundUnder(Vector3 at, Transform body)
        {
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(at + Vector3.up * 2.0f, Vector3.down, 6.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(body) && hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }
            return nearest < float.MaxValue ? at.y + 2.0f - nearest : float.NaN;
        }
    }
}
