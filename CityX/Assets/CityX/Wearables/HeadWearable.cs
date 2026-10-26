using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Anything worn rigidly on the head (a hat, a helmet): lying on the ground it is picked up with E, worn it rides the Head bone at the
    /// item's own offset. Nothing about headwear bends with the body, so it is parented to one bone like the jetpack, not merged into the
    /// skinned body like clothing. Slot replacement (a second hat evicting the first) is Inventory.EquipItemId's job.
    ///
    /// The Head bone's pivot is the head sphere's CENTRE (BodyRig.HeadCentreY), so an offset of FusedBodyShape.HeadRadius up reaches the
    /// top of the head.
    ///
    /// Other players see it on their copy of the wearer (ICosmeticMountable; the Knight Helmet first, every hat since 2026-10-22).
    /// </summary>
    public abstract class HeadWearable : GroundWearable, IEquippableItem, ICosmeticMountable
    {
        private CharacterHair _coveredHair;

        /// <summary>Where the item's root sits in the Head bone's space.</summary>
        protected abstract Vector3 HeadPosition { get; }

        protected virtual Vector3 HeadRotationEuler => Vector3.zero;

        /// <summary>A closed helmet the hair would poke through: the wearer goes bald while it is on (CharacterHair.Cover).</summary>
        protected virtual bool HidesHair => false;

        /// <summary>
        /// No Animator, or an Animator with no avatar, are both tolerated: GetBoneTransform THROWS when the Animator has no avatar at all
        /// (it only returns null for an avatar lacking that bone).
        /// </summary>
        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Transform head = HeadBone(player);
            if (head != null)
            {
                transform.SetParent(head, false);
                transform.localPosition = HeadPosition;
                transform.localEulerAngles = HeadRotationEuler;
            }

            MarkEquipped();
            CoverHairOf(player);
        }

        /// <summary>Hides <paramref name="body"/>'s hair while this is worn, when this headwear hides hair; once, however often it is called.</summary>
        protected void CoverHairOf(GameObject body)
        {
            if (!HidesHair || _coveredHair != null || body == null)
            {
                return;
            }
            _coveredHair = body.GetComponentInChildren<CharacterHair>(true);
            _coveredHair?.Cover();
        }

        /// <summary>Gives the hair back (taken off, or this model destroyed with its wearer's copy).</summary>
        protected void UncoverHair()
        {
            if (_coveredHair != null)
            {
                _coveredHair.Uncover();
                _coveredHair = null;
            }
        }

        private void OnDestroy()
        {
            UncoverHair();
        }

        public void OnUnequip(GameObject player)
        {
            Destroy(gameObject);
        }

        /// <summary>Worn by another player's copy of the wearer: on its head where the wearer has it, its hair covered the same.</summary>
        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.Head, HeadPosition, HeadRotationEuler, 1.0f);
            CoverHairOf(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
            UncoverHair();
        }

        public void SetCosmeticActive(bool active)
        {
        }

        /// <summary>Plain headwear carries no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        private static Transform HeadBone(GameObject body)
        {
            var animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            return animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        }
    }
}
