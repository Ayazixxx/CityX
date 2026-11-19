using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A PLAYER WHO DIES DROPS THE WEAPON IN THEIR HAND (owner, 2026-10-17): it leaves their inventory and falls from the hand as an item anyone
    /// can pick up, shared with the other players (DroppedItems). It runs where the death is decided, on the owner's own body: another player's
    /// copy only shows their health and never dies itself. The enemies drop theirs the same way (SwordsmanBrain, ShootaBrain).
    /// </summary>
    public sealed class DropsWeaponOnDeath : MonoBehaviour
    {
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnEnable()
        {
            if (_vitals != null)
            {
                _vitals.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_vitals != null)
            {
                _vitals.Died -= OnDied;
            }
        }

        private void OnDied()
        {
            // Looked for now, not on waking: a body may be given its inventory after its input reader added this.
            var inventory = GetComponent<Inventory>();
            Vector3 bodyVelocity = _locomotion != null ? _locomotion.Velocity : Vector3.zero;
            // Both of a pair of pistols (docs/DUAL_PISTOLS.md): the left hand's, then the right's.
            for (int hand = 0; hand < 2 && inventory != null; hand++)
            {
                if (!inventory.TakeWeaponInHand(gameObject, out string itemId, out ItemInstanceData data, out Pose pose))
                {
                    return;
                }
                DroppedItems.Shared.Drop(DroppedItem.LetGo(itemId, data, pose, transform, bodyVelocity, _vitals.DeathImpulse), shared: true, gameObject,
                    inventory.PrefabForItem(itemId));
            }
        }
    }
}
