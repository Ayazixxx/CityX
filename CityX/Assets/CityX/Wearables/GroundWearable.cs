using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Something worn rigidly that lies on the ground until this machine's player, in reach, presses E (a hat, a helmet, a pair of shoes): it
    /// goes into the inventory and the model on the ground goes. Worn, it stops looking for a player (<see cref="MarkEquipped"/>).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public abstract class GroundWearable : MonoBehaviour, IWornItem
    {
        public const float InteractRange = 2.5f;

        string IWornItem.WornItemId => ItemId;

        bool IWornItem.IsWorn => Equipped;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;

        /// <summary>The registry id the pickup adds to the inventory.</summary>
        protected abstract string ItemId { get; }

        /// <summary>Worn (by the player or another player's copy): no longer something to pick up.</summary>
        protected bool Equipped { get; private set; }

        protected virtual void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (!Equipped)
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

        /// <summary>Put on: the pickup trigger off and the pickup no longer looked for.</summary>
        protected void MarkEquipped()
        {
            Equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }
        }
    }
}
