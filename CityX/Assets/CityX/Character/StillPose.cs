using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>Fixed poses for a body shown without its Animator: the character select's pedestals and the weapon builders' grip renders.</summary>
    public static class StillPose
    {
        /// <summary>Arms relaxed at the sides rather than the bind pose's T-pose.</summary>
        public static void ArmsDown(Transform root)
        {
            SetLocalRotation(root, "arm_l", new Vector3(0.0f, 0.0f, 78.0f));
            SetLocalRotation(root, "forearm_l", new Vector3(0.0f, 0.0f, 12.0f));
            SetLocalRotation(root, "arm_r", new Vector3(0.0f, 0.0f, -78.0f));
            SetLocalRotation(root, "forearm_r", new Vector3(0.0f, 0.0f, -12.0f));
        }

        private static void SetLocalRotation(Transform root, string boneName, Vector3 euler)
        {
            Transform bone = TransformSearch.FindDeep(root, boneName);
            if (bone != null)
            {
                bone.localRotation = Quaternion.Euler(euler);
            }
        }
    }
}
