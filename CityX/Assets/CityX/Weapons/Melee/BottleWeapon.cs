using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Weapons
{
    /// <summary>
    /// The glass bottle: a melee weapon that gets more dangerous as it
    /// breaks, and a throwable while it's still intact. Ported from
    /// weapons/glass_bottle.gd.
    ///
    /// READS Keyboard.current DIRECTLY IN ITS OWN Update(), matching
    /// GrabCarry's own established pattern of every interactable asking for
    /// the key itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BottleWeapon : MonoBehaviour, IEquippableItem, IEquippableWeapon, ICosmeticMountable
    {
        public enum DurabilityState
        {
            Intact,
            Broken,
            FullyBroken,
        }

        public const float IntactMeleeDamage = 15.0f;
        public const float BrokenMeleeDamage = 22.0f;
        public const float FullyBrokenMeleeDamage = 0.0f;
        public const int BrokenHitsToFullyBroken = 10;
        public const float MeleeCooldownSecondsConst = 0.5f;
        public const float ThrowSpeedConst = 16.0f;
        public const float ThrowDamage = 18.0f;
        public const float BottleScale = 2.4f;
        public const float BottleMass = 0.4f;
        public const float BottleBounce = 0.25f;
        public const float BottleFriction = 0.7f;
        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees)

        public static readonly Vector3 GripHandRotationEuler = new Vector3(-15.0f, 10.0f, 5.0f);
        public static readonly Vector3 GripHandPosition = new Vector3(0.0f, -0.04f, 0.06f);

        [Tooltip("Carries BottleScale -- never the root itself, which is the live grip anchor while equipped.")]
        public Transform VisualRoot;
        public GameObject IntactVisual;
        public GameObject BrokenVisual;
        public GameObject ThrownBottlePrefab;

        public DurabilityState State { get; private set; } = DurabilityState.Intact;

        private int _brokenHitCount;
        private bool _equipped;
        private Rigidbody _rigidbody;
        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _pickupTrigger = GetComponent<SphereCollider>();
            RefreshVisual();
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
            Keyboard keyboard = Keyboard.current;
            bool ePressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            TryPickup(_nearbyPlayer, ePressed);
        }

        /// <summary>
        /// See PistolWeapon.TryPickup's own comment -- same split, same
        /// reason: Keyboard.current is null outside a real Input System
        /// device, so a test drives this with an explicit flag instead.
        /// </summary>
        public bool TryPickup(Transform player, bool ePressed)
        {
            if (!ePressed)
            {
                return false;
            }

            // See PistolWeapon.TryPickup's own comment -- range has to come
            // from the player's own body, not the camera InteractionReach.LookSource falls
            // back to, which trails Distance behind the player under the
            // real ThirdPersonCamera and made this permanently unreachable.
            Vector3 toPlayer = transform.position - player.position;
            if (toPlayer.sqrMagnitude > InteractRange * InteractRange)
            {
                return false;
            }

            Transform lookSource = InteractionReach.LookSource(player);
            Vector3 toSelf = transform.position - lookSource.position;
            if (toSelf.sqrMagnitude > 0.0001f &&
                Vector3.Dot(lookSource.forward, toSelf.normalized) < LookDotThreshold)
            {
                return false;
            }

            var inventory = player.GetComponentInParent<Inventory>();
            if (inventory == null)
            {
                return false;
            }
            var instanceData = new ItemInstanceData { HasDurability = true, Durability = (int)State };
            if (inventory.PickUp(ItemRegistry.GlassBottle, instanceData))
            {
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        public float GetMeleeDamage()
        {
            switch (State)
            {
                case DurabilityState.Intact: return IntactMeleeDamage;
                case DurabilityState.Broken: return BrokenMeleeDamage;
                default: return FullyBrokenMeleeDamage;
            }
        }

        public float MeleeCooldownSeconds => MeleeCooldownSecondsConst;

        // No chain, no heavy variant -- exactly what a bottle already did
        // before IEquippableWeapon grew these two members for the sword.
        public string MovesetId => null;
        public ComboStage? HeavyStage => null;

        public void RegisterLandedMeleeHit()
        {
            switch (State)
            {
                case DurabilityState.Intact:
                    State = DurabilityState.Broken;
                    _brokenHitCount = 0;
                    RefreshVisual();
                    break;
                case DurabilityState.Broken:
                    _brokenHitCount++;
                    if (_brokenHitCount >= BrokenHitsToFullyBroken)
                    {
                        State = DurabilityState.FullyBroken;
                        RefreshVisual();
                    }
                    break;
                default:
                    break; // fully broken is inert
            }
        }

        public bool CanThrow() => State == DurabilityState.Intact;

        public float ThrowSpeed => ThrowSpeedConst;

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            if (instanceData.HasDurability)
            {
                State = (DurabilityState)Mathf.Clamp(instanceData.Durability, 0, 2);
                RefreshVisual();
            }

            var animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripHandPosition;
                transform.localEulerAngles = GripHandRotationEuler;
            }

            _equipped = true;
            // GetComponent RATHER THAN THE CACHED FIELD: Awake() isn't
            // guaranteed to have run yet when a caller (a test, or
            // Inventory.EquipItemId in the same frame as Instantiate)
            // reaches this before Unity's own lifecycle has caught up.
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            _rigidbody.isKinematic = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

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
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() =>
            new ItemInstanceData { HasDurability = true, Durability = (int)State };

        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
            if (ThrownBottlePrefab != null)
            {
                var vitals = player.GetComponent<PlayerVitals>();
                int strength = vitals != null ? vitals.Strength : 0;
                float penetration = vitals != null ? vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
                var colliders = player.GetComponentsInChildren<Collider>();
                ThrownBottle.Spawn(ThrownBottlePrefab, origin, direction.normalized * ThrowSpeedConst, strength, colliders, penetration);
            }

            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }

            var inventory = player.GetComponent<Inventory>();
            inventory?.ClearEquipSlotWithoutReturning(inventory.ActiveWeaponSlot);

            Destroy(gameObject);
        }

        private void RefreshVisual()
        {
            if (IntactVisual != null) IntactVisual.SetActive(State == DurabilityState.Intact);
            if (BrokenVisual != null) BrokenVisual.SetActive(State != DurabilityState.Intact);
        }
    }
}
