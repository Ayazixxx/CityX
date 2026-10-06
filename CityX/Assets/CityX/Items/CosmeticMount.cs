using UnityEngine;

namespace CityX.Items
{
    /// <summary>The one piece of mounting every cosmetic item shares: hang a model on a bone of another player's copy at a grip.</summary>
    public static class CosmeticMount
    {
        public static void ToBone(Transform item, GameObject body, HumanBodyBones bone, Vector3 localPosition, Vector3 localEuler, float scale)
        {
            Animator animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            Transform target = animator != null ? animator.GetBoneTransform(bone) : null;
            if (target == null)
            {
                return;
            }
            item.SetParent(target, false);
            item.localPosition = localPosition;
            item.localEulerAngles = localEuler;
            item.localScale = Vector3.one * scale;
        }
    }
}
