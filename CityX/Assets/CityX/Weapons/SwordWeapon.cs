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
    public sealed class SwordWeapon : MonoBehaviour, IEquippableItem, IEquippableWeapon, ISwordBlade
    {
        /// <summary>
        /// Godot has no separate sword damage constant at all -- player.gd's
        /// own _punch() runs sword swings through the exact same PUNCH_DAMAGE
        /// (10.0) every bare-fisted jab uses, then applies the chain's own
        /// damage_scale (1.0 for both sword_attack and sword_attack2 -- see
        /// ComboChain.Sword's own doc comment) on top, same as fists. An
        /// earlier version of this file invented its own 20.0 base here,
        /// never checked against player.gd -- confirmed wrong by reading it
        /// directly.
        /// </summary>
        public const float BaseMeleeDamage = CombatTuning.PunchDamage;

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
        /// Blade length in the sword root's own space: SwordBuilder's
        /// BladeLength (1.05) times its ModelScale (2.0). The blade runs
        /// along the root's local +Y from the grip (SwordBuilder centres the
        /// pickup trigger at half this height on +Y).
        /// </summary>
        public const float BladeLocalLength = 2.1f;

        /// <summary>Where the blade meets the grip, in world space.</summary>
        public Vector3 BladeBaseWorld => transform.position;

        /// <summary>The blade's tip, in world space -- what a swing actually sweeps.</summary>
        public Vector3 BladeTipWorld => transform.TransformPoint(new Vector3(0.0f, BladeLocalLength, 0.0f));

        // ---- THE GRIP, DERIVED FROM THE MOCAP ITSELF ----
        //
        // The Godot mount (position (-0.055,-0.197,-0.343), rotation (103,95,120)) was tuned against
        // Godot's own clips. Against the Mixamo idle this project uses now it holds the sword across
        // the chest with the blade over the shoulder and NEITHER hand on the hilt (measured: hands
        // 43cm and 46cm from the grip; the blade axis 87 degrees off the line between the hands).
        //
        // So the mount is rebuilt from what the animation says. In 2hsw_idle the right hand is the
        // upper hand and the left the lower, 39cm apart on the same line (0.99 dot across the idle
        // and the guard): that line, in the right hand's own frame, is the blade's axis. The handle
        // then passes through the palm, and the left hand is reached onto it (PlayerLocomotion.
        // SetSupportHand). Every number here is a measurement, not a guess -- see WeaponHandsPlayTests.

        /// <summary>The blade's direction (hilt to tip) in the right-hand bone's own frame: from the lower hand to the upper.</summary>
        public static Vector3 BladeAxisInHand = new Vector3(0.84f, -0.25f, 0.48f).normalized;

        /// <summary>Spin of the blade about its own axis in the hand (which way the edge faces), in degrees.</summary>
        public static float GripRollDegrees = 270.0f;

        /// <summary>The palm's centre in the right-hand bone's frame: along the forearm (measured (0.949,0.141,-0.283)), 8cm out.</summary>
        public static Vector3 PalmInHand => new Vector3(0.949f, 0.141f, -0.283f).normalized * PlayerLocomotion.PalmOffsetMetres;

        /// <summary>How far down the handle from the guard the right (upper) hand holds, in the sword's own space.</summary>
        public static float PrimaryGripDown = 0.12f;

        /// <summary>How far down the handle from the guard the left (lower) hand holds: 20cm below the right.</summary>
        public static float SupportGripDown = 0.32f;

        /// <summary>Where the support palm goes, in the sword's own space (the blade runs along +Y from the guard).</summary>
        public static Vector3 SupportGripLocal => new Vector3(0.0f, -SupportGripDown, 0.0f);

        public static Quaternion GripHandRotation =>
            Quaternion.FromToRotation(Vector3.up, BladeAxisInHand) * Quaternion.AngleAxis(GripRollDegrees, Vector3.up);

        /// <summary>The sword root's position in the right-hand bone's frame: the handle at <see cref="PrimaryGripDown"/> under the palm.</summary>
        public static Vector3 GripHandPosition => PalmInHand + BladeAxisInHand * PrimaryGripDown;

        public static Vector3 GripHandRotationEuler => GripHandRotation.eulerAngles;

        private bool _equipped;
        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private Animator _animator;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (_equipped)
            {
                UpdateHeldPose();
                return;
            }
            TickPickup();
        }

        /// <summary>
        /// Runs every frame once equipped, so a live change to the grip (the tuning fields above)
        /// shows at once. One mount for every clip: the support hand is reached onto the handle, so
        /// the swing needs no per-clip grip the way Godot's own heavy attack did.
        /// </summary>
        private void UpdateHeldPose()
        {
            transform.localPosition = GripHandPosition;
            transform.localRotation = GripHandRotation;
            if (_wielder != null)
            {
                _wielder.SetSupportHand(transform, SupportGripLocal);
            }
        }

        private PlayerLocomotion _wielder;

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
            _animator = player.GetComponentInChildren<Animator>();
            Transform hand = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripHandPosition;
                transform.localRotation = GripHandRotation;
            }

            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion != null)
            {
                // The left hand holds the handle too, wherever the swing takes the sword.
                _wielder = locomotion;
                locomotion.SetSupportHand(transform, SupportGripLocal);
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

            // THE SWORD FIGHTS THE SOULSLIKE WAY (SoulsSwordCombat): committed
            // attacks with startup/active/recovery, a stamina cost, buffered
            // chaining and a swept blade hitbox. Added on equip rather than
            // baked into Player.prefab so nothing else about the body changes.
            var souls = player.GetComponent<SoulsSwordCombat>();
            if (souls == null)
            {
                souls = player.AddComponent<SoulsSwordCombat>();
            }
            souls.Equip(this);
        }

        public void OnUnequip(GameObject player)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion != null)
            {
                locomotion.ClearSupportHand(transform);
            }
            _wielder = null;
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }
            var souls = player.GetComponent<SoulsSwordCombat>();
            if (souls != null)
            {
                souls.Unequip(this);
            }
            Destroy(gameObject);
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
