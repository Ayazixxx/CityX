using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A SHIELD HELD IN THE LEFT HAND (docs/SHIELD.md), by a centre grip. It is the body's shield while worn (PlayerVitals.Shield,
    /// PlayerLocomotion.Shield), of its own kind (<see cref="Traits"/>): right-click held raises it, it covers the front, and the body places it
    /// each frame -- at its side, or up before the chest (PlayerLocomotion.Shield.cs). Another player's copy wears it the same way and raises it
    /// with them.
    /// </summary>
    public abstract class HeldShield : GroundWearable, IEquippableItem, ICosmeticMountable
    {
        public const string ModelName = "Model";

        /// <summary>What kind of shield it is: told to the body with it.</summary>
        protected abstract ShieldTraits Traits { get; }

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            StrapOn(player);
            _vitals = player.GetComponent<PlayerVitals>();
            if (_vitals != null)
            {
                _vitals.Shield = transform;
                _vitals.ShieldTraits = Traits;
            }
        }

        public void OnUnequip(GameObject player)
        {
            Release();
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            StrapOn(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
            Release();
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        private void OnDestroy()
        {
            Release();
        }

        /// <summary>Into the left hand (whose fingers then close on it), the body told it carries a shield (it places it from here on).</summary>
        private void StrapOn(GameObject body)
        {
            var animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            Transform hand = animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
            }
            MarkEquipped();
            _locomotion = body != null ? body.GetComponent<PlayerLocomotion>() : null;
            if (_locomotion != null)
            {
                _locomotion.Shield = transform;
                _locomotion.ShieldTraits = Traits;
            }
        }

        private void Release()
        {
            if (_vitals != null && _vitals.Shield == transform)
            {
                _vitals.Shield = null;
                _vitals.ShieldTraits = ShieldTraits.Round;
            }
            if (_locomotion != null && _locomotion.Shield == transform)
            {
                _locomotion.Shield = null;
                _locomotion.ShieldTraits = ShieldTraits.Round;
            }
        }
    }
}
