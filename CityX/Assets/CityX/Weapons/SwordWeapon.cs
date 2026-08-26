using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Weapons
{
    /// <summary>
    /// A two-handed sword. No Godot equivalent to faithfully port -- the
    /// explicit ask was to make sword combat BETTER than
    /// weapons/two_handed_sword.gd's own, not to reproduce it, so this is
    /// new-built on the same footing as Jetpack.cs rather than translated
    /// line for line. See ComboChain.Sword's own doc comment for the actual
    /// mechanical improvement (a real multi-stage chain through
    /// MeleeAttacker's delta-driven timing, not Godot's own hand-rolled
    /// _sword_swing_in_progress flag and its twice-rediscovered stuck-flag
    /// bug).
    ///
    /// NO DURABILITY, NO THROW -- unlike the bottle, a sword is not
    /// consumed by use and was never described as throwable. CanThrow is
    /// false and Throw is a no-op that PlayerInputReader's own throw
    /// dispatch (gated on CanThrow first) will never actually reach.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class SwordWeapon : MonoBehaviour, IEquippableItem, IEquippableWeapon
    {
        /// <summary>
        /// Heavier hit than a bottle's own 15/22 -- a two-handed blade
        /// should read as hitting harder than furniture, before the
        /// chain's own DamageScale multiplies it further (28 on the
        /// finisher, 32 on a held heavy swing).
        /// </summary>
        public const float BaseMeleeDamage = 20.0f;

        /// <summary>
        /// Slower than the bottle's 0.5s -- a two-handed swing has real
        /// weight to it. Used for every sword swing (chain stages and the
        /// heavy variant alike); there is no separate weapon-heavy-duration
        /// concept in MeleeAttacker today, matching how HeavySwingSeconds
        /// itself only ever applied to the unarmed haymaker.
        /// </summary>
        public const float MeleeCooldownSecondsConst = 0.6f;

        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees)

        /// <summary>
        /// A two-handed grip in the right hand, blade angled up and
        /// forward -- tuned by eye against a rendered capture the same way
        /// BottleWeapon's/JetpackBuilder's own grip constants were.
        /// </summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.02f, -0.02f, 0.10f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(-8.0f, 4.0f, 82.0f);

        private bool _equipped;
        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;

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
            PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
            if (vitals != null)
            {
                _nearbyPlayer = vitals.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform == _nearbyPlayer || (other.GetComponentInParent<PlayerVitals>() != null))
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

            Vector3 toPlayer = transform.position - player.position;
            if (toPlayer.sqrMagnitude > InteractRange * InteractRange)
            {
                return false;
            }

            Transform lookSource = PistolWeapon.LookSourceFor(player);
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
            if (inventory.AddItem(ItemRegistry.Sword, 1))
            {
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        public float GetMeleeDamage() => BaseMeleeDamage;

        public float MeleeCooldownSeconds => MeleeCooldownSecondsConst;

        public string MovesetId => ComboChain.Sword;

        public ComboStage? HeavyStage => CombatTuning.SwordHeavyAttack;

        /// <summary>Nothing to track between hits -- a sword doesn't degrade with use.</summary>
        public void RegisterLandedMeleeHit()
        {
        }

        public bool CanThrow() => false;

        public float ThrowSpeed => 0.0f;

        /// <summary>Never actually reached -- CanThrow is false, and PlayerInputReader checks that first.</summary>
        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripHandPosition;
                transform.localEulerAngles = GripHandRotationEuler;
            }

            _equipped = true;
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

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
