using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE FRAG GRENADE in the hand (owner, 2026-10-29: "work on explosions try making a grenade that looks really good research video game
    /// grenades and make it look AAA game level"; docs/GRENADE.md). The Godot game's grenade (weapons/grenade.gd): thrown with the bottle's
    /// gesture (hold right-click to aim, click to throw; a plain click throws too, a grenade being no club), it leaves the hand as a
    /// <see cref="ThrownGrenade"/> with its fuse lit, and the next grenade of the stack comes into the hand. Drawn in code (GrenadeBuilder):
    /// an olive egg with a grey fuse and spoon. Seen in other players' hands (ICosmeticMountable); the throw and the blast reach them through
    /// the body's <see cref="GrenadeThrower"/>.
    /// </summary>
    public sealed class GrenadeItem : MonoBehaviour, IEquippableItem, IEquippableWeapon, ICosmeticMountable
    {
        public string ItemId => ItemRegistry.Grenade;

        /// <summary>In the fist, the spoon along the fingers: as the bottle sits.</summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.02f, -0.02f, 0.06f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(-15.0f, 10.0f, 5.0f);

        [Tooltip("What leaves the hand: the grenade with its fuse lit (ThrownGrenade on it). Set by GrenadeBuilder.")]
        public GameObject ThrownPrefab;

        public float GetMeleeDamage() => 0.0f;

        public float MeleeCooldownSeconds => 0.5f;

        public void RegisterLandedMeleeHit()
        {
        }

        public bool CanThrow() => ThrownPrefab != null;

        public float ThrowSpeed => GrenadeRules.ThrowSpeed;

        public string MovesetId => null;

        public ComboStage? HeavyStage => null;

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Hold(player);
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null)
            {
                melee.EquippedWeapon = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.RightHand, GripHandPosition, GripHandRotationEuler, 1.0f);
            DisableColliders();
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>
        /// Thrown from <paramref name="origin"/> along <paramref name="direction"/> (lifted a little: a lob): the grenade leaves the hand with its
        /// fuse lit (the body's GrenadeThrower tells other players), and the next of the stack comes into the hand, if there is one.
        /// </summary>
        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
            Vector3 velocity = GrenadeRules.ThrowVelocity(direction);
            var thrower = player != null ? player.GetComponent<GrenadeThrower>() : null;
            if (thrower != null)
            {
                thrower.Throw(ThrownPrefab, origin, velocity);
            }
            else
            {
                ThrownGrenade.Spawn(ThrownPrefab, origin, velocity, player, false);
            }
            var melee = player != null ? player.GetComponent<MeleeAttacker>() : null;
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }
            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (inventory != null)
            {
                inventory.ClearEquipSlotWithoutReturning(inventory.ActiveWeaponSlot);
                if (inventory.RemoveFromStorage(ItemRegistry.Grenade, 1, out ItemInstanceData next))
                {
                    inventory.EquipItemId(ItemRegistry.Grenade, player, next);
                }
            }
            Destroy(gameObject);
        }

        private void Hold(GameObject player)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripHandPosition;
                transform.localEulerAngles = GripHandRotationEuler;
            }
            DisableColliders();
        }

        private void DisableColliders()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }
        }
    }
}
