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
    public sealed class SwordWeapon : MonoBehaviour, IEquippableItem, IEquippableWeapon, ISwordBlade, ICosmeticMountable
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

        /// <summary>Which inventory item this sword is (the original greatsword, or a model the owner brought in, e.g. the spike sword).</summary>
        [SerializeField] private string _itemId = ItemRegistry.Sword;

        public string ItemId { get => _itemId; set => _itemId = value; }

        /// <summary>
        /// A ONE-HANDED sword (the owner's combat sword, 2026-10-13): held across the palm the way a hand grips a handle, the other hand free,
        /// and fought with the one-handed attacks and guard (SwordStrikes). False is the two-handed greatsword hold with the left hand on the grip.
        /// </summary>
        [SerializeField] private bool _oneHanded;

        /// <summary>The blade's length from the guard to the tip in the sword root's own space: what a swing sweeps. The greatsword's by default.</summary>
        [SerializeField] private float _bladeLength = BladeLocalLength;

        public bool OneHanded { get => _oneHanded; set => _oneHanded = value; }

        /// <summary>ISwordBlade: the combat picks its moveset by this.</summary>
        public bool IsOneHanded => _oneHanded;

        public float BladeLength { get => _bladeLength; set => _bladeLength = Mathf.Max(0.1f, value); }

        /// <summary>Where the blade meets the grip, in world space.</summary>
        public Vector3 BladeBaseWorld => transform.position;

        /// <summary>The blade's tip, in world space -- what a swing actually sweeps.</summary>
        public Vector3 BladeTipWorld => transform.TransformPoint(new Vector3(0.0f, _bladeLength, 0.0f));

        /// <summary>
        /// The cutting edge faces local -X: SwordBuilder's blade profile puts the sharp slice (no thickness) at edge_x - half the width, the
        /// thick spine at edge_x + half, and curves the tip toward -X (a kopis: the edge is on the inside of the curve).
        /// </summary>
        public Vector3 EdgeDirectionWorld => transform.TransformDirection(Vector3.left);

        // ---- THE HOLD ----
        //
        // DERIVED FROM THE MOCAP by default. The Godot mount (position (-0.055,-0.197,-0.343), rotation
        // (103,95,120)) was tuned against Godot's own clips; against the Mixamo idle this project uses now it
        // held the sword across the chest with neither hand on the hilt (measured: hands 43cm and 46cm from
        // the grip; the blade axis 87 degrees off the line between the hands). So the default is rebuilt
        // from what the animation says: in 2hsw_idle the right hand is the upper hand and the left the lower,
        // 39cm apart on one line, and that line in the right hand's frame is the blade's axis. The handle
        // passes through the palm, and the left hand is reached onto it (PlayerLocomotion.SetSupportHand).
        //
        // TUNED BY HAND in the Unity editor when it needs to be: see WeaponGripSettings and the Inspector on
        // this component. A saved hold replaces the derived one below.

        /// <summary>The blade's direction (hilt to tip) in the right-hand bone's own frame: from the lower hand to the upper.</summary>
        public static readonly Vector3 DefaultBladeAxisInHand = new Vector3(0.84f, -0.25f, 0.48f).normalized;

        /// <summary>Spin of the blade about its own axis in the hand (which way the edge faces), in degrees: 270 has it forward.</summary>
        public const float DefaultRollDegrees = 270.0f;

        /// <summary>The palm's centre in the right-hand bone's frame: along the forearm (measured (0.949,0.141,-0.283)), 8cm out.</summary>
        public static Vector3 PalmInHand => new Vector3(0.949f, 0.141f, -0.283f).normalized * PlayerLocomotion.PalmOffsetMetres;

        /// <summary>How far down the handle from the guard the right (upper) hand holds, in the sword's own space.</summary>
        public const float DefaultPrimaryGripDown = 0.12f;

        /// <summary>How far down the handle from the guard the left (lower) hand holds: 20cm below the right.</summary>
        public const float DefaultSupportGripDown = 0.32f;

        public static Quaternion DefaultGripRotation =>
            Quaternion.FromToRotation(Vector3.up, DefaultBladeAxisInHand) * Quaternion.AngleAxis(DefaultRollDegrees, Vector3.up);

        /// <summary>The sword root's position in the right-hand bone's frame: the handle at the primary grip under the palm.</summary>
        public static Vector3 DefaultGripPosition => PalmInHand + DefaultBladeAxisInHand * DefaultPrimaryGripDown;

        /// <summary>Where the left palm goes, in the sword's own space (the blade runs along +Y from the guard).</summary>
        public static Vector3 DefaultSupportPoint => new Vector3(0.0f, -DefaultSupportGripDown, 0.0f);

        /// <summary>The sword's position in the right hand: the saved hold if there is one, else the derived one.</summary>
        public static Vector3 GripPosition =>
            WeaponGripSettings.Current.SwordTuned ? WeaponGripSettings.Current.SwordPosition : DefaultGripPosition;

        public static Quaternion GripRotation =>
            WeaponGripSettings.Current.SwordTuned ? Quaternion.Euler(WeaponGripSettings.Current.SwordEuler) : DefaultGripRotation;

        /// <summary>Where the left palm holds, in the sword's space.</summary>
        public static Vector3 SupportPoint =>
            WeaponGripSettings.Current.SwordTuned ? WeaponGripSettings.Current.SwordLeftHand : DefaultSupportPoint;

        // ---- THE ONE-HANDED HOLD ----
        //
        // DERIVED FROM THE HAND'S OWN SHAPE (FusedBodyShape.AddHand), not from a clip: in the right-hand bone's frame +X runs down the arm to the
        // fingertips, -Y is the palm's side (toward the body with the arm hanging) and +Z is the thumb's. A gripped handle lies across the palm,
        // so the blade runs out past the thumb (+Z) tilted a little toward the fingers, with the edge in line with the knuckles (+X), and the
        // guard sits just beyond the palm's surface. Tuned by hand the same way as the two-handed hold (WeaponGripSettings.OneHand*).

        /// <summary>The blade's direction (hilt to tip) in the right-hand bone's frame for a one-handed hold: out past the thumb, 15 degrees toward the fingers.</summary>
        public static readonly Vector3 OneHandBladeAxisInHand = new Vector3(0.26f, 0.0f, 0.966f).normalized;

        /// <summary>How far past the palm's surface the handle's axis lies: half the palm's thickness and the handle's own radius.</summary>
        public const float OneHandHandleBelowPalm = FusedBodyShape.PalmHalfThickness + 0.03f;

        /// <summary>How far the guard sits from the palm's centre along the blade: the hand just below the guard.</summary>
        public const float OneHandGripDown = 0.09f;

        /// <summary>The sword's rotation in the right hand for a one-handed hold: its +Y along <see cref="OneHandBladeAxisInHand"/>, its edge (-X) toward the knuckles.</summary>
        public static Quaternion DefaultOneHandGripRotation => Quaternion.LookRotation(Vector3.up, OneHandBladeAxisInHand);

        /// <summary>The sword root's position in the right-hand bone's frame for a one-handed hold.</summary>
        public static Vector3 DefaultOneHandGripPosition =>
            new Vector3(PlayerLocomotion.PalmOffsetMetres, -OneHandHandleBelowPalm, 0.0f) + OneHandBladeAxisInHand * OneHandGripDown;

        public static Vector3 OneHandGripPosition =>
            WeaponGripSettings.Current.OneHandTuned ? WeaponGripSettings.Current.OneHandPosition : DefaultOneHandGripPosition;

        public static Quaternion OneHandGripRotation =>
            WeaponGripSettings.Current.OneHandTuned ? Quaternion.Euler(WeaponGripSettings.Current.OneHandEuler) : DefaultOneHandGripRotation;

        /// <summary>This sword's position in the right hand: the hold for its kind (saved or derived).</summary>
        public Vector3 HoldPosition => _oneHanded ? OneHandGripPosition : GripPosition;

        public Quaternion HoldRotation => _oneHanded ? OneHandGripRotation : GripRotation;

        /// <summary>
        /// The marker the left hand reaches for, a child of the sword. Select it while the game runs and
        /// drag it in the Scene view: the left hand follows. Null while the sword is not held.
        /// </summary>
        public Transform LeftHandGrip => _leftHandGrip;

        private Transform _leftHandGrip;

        /// <summary>Puts the sword and the left-hand marker where the current hold (saved or derived) says.</summary>
        public void ApplyGrip()
        {
            transform.localPosition = HoldPosition;
            transform.localRotation = HoldRotation;
            if (_leftHandGrip != null)
            {
                _leftHandGrip.localPosition = SupportPoint;
            }
        }

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
            if (!_equipped)
            {
                TickPickup();
            }
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
            if (!CityX.Character.InteractionReach.IsBodyLeaving(other))
            {
                return;   // a limb's collider swinging out of the trigger is not the player walking away
            }
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
            if (inventory.AddItem(_itemId, 1))
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
            }

            // The left hand reaches for a marker on the sword, so the marker can be dragged in the editor
            // while the game runs and the hand follows (see WeaponGripSettings). A one-handed sword leaves it free.
            if (_leftHandGrip == null && !_oneHanded)
            {
                _leftHandGrip = GripMarker.Create(transform, "LeftHandGrip", "left hand", new Color(1.0f, 0.55f, 0.1f));
            }
            ApplyGrip();

            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion != null && !_oneHanded)
            {
                // The left hand holds the handle too, wherever the swing takes the sword.
                locomotion.SetSupportHand(_leftHandGrip, Vector3.zero);
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
                locomotion.ClearSupportHand(_leftHandGrip);
            }
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

        // ---- on another player's copy: the model in the right hand and the free hand on the grip, nothing else ----
        public void MountCosmetic(GameObject body)
        {
            Animator animator = body.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
            }
            if (_leftHandGrip == null && !_oneHanded)
            {
                _leftHandGrip = GripMarker.Create(transform, "LeftHandGrip", "left hand", new Color(1.0f, 0.55f, 0.1f));
            }
            ApplyGrip();
            if (!_oneHanded)
            {
                body.GetComponent<PlayerLocomotion>()?.SetSupportHand(_leftHandGrip, Vector3.zero);
            }
        }

        public void UnmountCosmetic(GameObject body)
        {
            body.GetComponent<PlayerLocomotion>()?.ClearSupportHand(_leftHandGrip);
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
