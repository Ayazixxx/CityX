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
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public abstract class HeadWearable : MonoBehaviour, IEquippableItem
    {
        public const float InteractRange = 2.5f;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;
        private CharacterHair _coveredHair;

        /// <summary>The registry id the pickup adds to the inventory.</summary>
        protected abstract string ItemId { get; }

        /// <summary>Where the item's root sits in the Head bone's space.</summary>
        protected abstract Vector3 HeadPosition { get; }

        protected virtual Vector3 HeadRotationEuler => Vector3.zero;

        /// <summary>A closed helmet the hair would poke through: the wearer goes bald while it is on (CharacterHair.Cover).</summary>
        protected virtual bool HidesHair => false;

        protected virtual void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (!_equipped)
            {
                TickPickup();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform body = InteractionReach.LocalPlayerBody(other);
            if (body != null)
            {
                _nearbyPlayer = body;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (InteractionReach.IsLeaving(other, _nearbyPlayer))
            {
                _nearbyPlayer = null;
            }
        }

        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            if (!InteractKey.Tapped)
            {
                return;
            }
            if (Vector3.Distance(transform.position, _nearbyPlayer.position) > InteractRange)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.PickUp(ItemId))
            {
                Destroy(gameObject);
            }
        }

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

            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }
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

        /// <summary>Plain headwear carries no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        private static Transform HeadBone(GameObject body)
        {
            var animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            return animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        }
    }
}
