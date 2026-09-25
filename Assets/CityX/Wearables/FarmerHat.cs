using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Wearables
{
    /// <summary>
    /// A wide-brimmed straw hat, worn on the head. Mirrors TopHat's own
    /// shape exactly -- a rigid single piece parented straight onto the
    /// Head bone on equip, no SkinnedGarment deformation needed -- but the
    /// mesh itself is cut out of a real Meshy AI character export
    /// (FarmerHatBuilder) rather than TopHat's own procedural cylinders.
    /// The source .glb was a WHOLE CHARACTER wearing the hat, not a
    /// standalone hat -- see FarmerHatBuilder's own class comment for how
    /// the hat geometry was isolated and why the embedded texture isn't
    /// used.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class FarmerHat : MonoBehaviour, IEquippableItem
    {
        public const float InteractRange = 2.5f;

        /// <summary>
        /// Local offset from the Head bone -- see TopHat.HeadPosition's own
        /// doc comment for why this rig's Head bone sits at the head
        /// sphere's CENTRE rather than Godot's "neck-top" convention.
        /// Confirmed by an actual render of the cut, normalized mesh worn
        /// on the real Player rig's Head bone -- sits cleanly on top of the
        /// head sphere, centred, with no clipping.
        /// </summary>
        public static readonly Vector3 HeadPosition = new Vector3(0.0f, 0.30f, 0.0f);

        public static readonly Vector3 HeadRotationEuler = Vector3.zero;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (_equipped)
            {
                return;
            }
            TickPickup();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerVitals>() != null)
            {
                _nearbyPlayer = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform == _nearbyPlayer || other.GetComponentInParent<PlayerVitals>() != null)
            {
                _nearbyPlayer = null;
            }
        }

        /// <summary>Proximity + E, matching every other pickup item's own convention.</summary>
        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }
            if (Vector3.Distance(transform.position, _nearbyPlayer.position) > InteractRange)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.AddItem(ItemRegistry.FarmerHat, 1))
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// NO ANIMATOR, OR AN ANIMATOR WITH NO AVATAR, BOTH TOLERATED --
        /// see TopHat.OnEquip's own doc comment for why GetBoneTransform
        /// needs this guard.
        /// </summary>
        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.avatar != null
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
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
        }

        public void OnUnequip(GameObject player)
        {
            Destroy(gameObject);
        }

        /// <summary>Plain clothing carries no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
