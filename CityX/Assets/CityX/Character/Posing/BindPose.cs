using System.Linq;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A skinned body put back in the pose it was bound in: standing, arms down (BodyRig binds it so), whatever its Animator last did. The HUD
    /// portrait is taken in it (owner, 2026-10-22: "my portrait is still doing poses just lock him in default position").
    /// </summary>
    public static class BindPose
    {
        /// <summary>Every bone of <paramref name="skin"/> where its bind pose puts it, about the skin's own place now; parents first.</summary>
        public static void Apply(SkinnedMeshRenderer skin)
        {
            if (skin == null || skin.sharedMesh == null)
            {
                return;
            }
            Transform[] bones = skin.bones;
            Matrix4x4[] binds = skin.sharedMesh.bindposes;
            Matrix4x4 skinToWorld = skin.transform.localToWorldMatrix;
            foreach (int i in Enumerable.Range(0, Mathf.Min(bones.Length, binds.Length)).OrderBy(i => Depth(bones[i])))
            {
                if (bones[i] == null)
                {
                    continue;
                }
                Matrix4x4 world = skinToWorld * binds[i].inverse;
                bones[i].SetPositionAndRotation(world.GetColumn(3), world.rotation);
            }
        }

        private static int Depth(Transform t)
        {
            int depth = 0;
            for (; t != null; t = t.parent)
            {
                depth++;
            }
            return depth;
        }
    }
}
