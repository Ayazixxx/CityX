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
        /// A two-handed grip in the right hand, blade angled up and
        /// forward. Starting point taken from two_handed_sword.gd's own
        /// GRIP_HAND_ROTATION (a plain degree conversion of its radians,
        /// (103.1, 94.8, 120.4)) then tuned by eye against a rendered
        /// capture of THIS rig -- Godot's own rotation convention does not
        /// carry over as a literal number even after unit conversion (see
        /// PistolWeapon.GripHandRotationEuler's own doc comment for why),
        /// only the position axis needs its Z sign flipped for this rig's
        /// own +Z-forward convention (Godot's own GRIP_HAND_POSITION is
        /// (-0.055, -0.197, 0.343)).
        /// </summary>
        public static readonly Vector3 GripHandPosition = new Vector3(-0.055f, -0.197f, -0.343f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(103.1f, 94.8f, 120.4f);

        /// <summary>
        /// The grip pose shown ONLY while "sword_charged_attack" is the
        /// current animator state -- two_handed_sword.gd's own
        /// HEAVY_ATTACK_GRIP_ROTATION exists because a real mocap grip is
        /// not a perfectly rigid two-point hold: hand_r's own relationship
        /// to the blade measurably differs between idle and the charged
        /// swing's peak strike frame. Same starting-point-then-tune
        /// approach as GripHandPosition/GripHandRotationEuler above.
        /// Godot's own HEAVY_ATTACK_GRIP_ROTATION is
        /// (-0.07405, 0.168671, -2.315635) radians -> (-4.2, 9.7, -132.7)
        /// degrees, and its own position is computed from that rotation
        /// via a fixed formula rather than hand-tuned (see
        /// two_handed_sword.gd's own _grip_position_for) -- ported here as
        /// a literal value instead since the formula depends on Godot's own
        /// rotation convention in a way that does not carry over.
        /// </summary>
        public static readonly Vector3 HeavyAttackGripPosition = new Vector3(-0.03f, -0.10f, -0.05f);

        public static readonly Vector3 HeavyAttackGripRotationEuler = new Vector3(-4.2f, 9.7f, -132.7f);

        /// <summary>
        /// Matches PlayerRigBuilder.AddSwordStates' own state name for
        /// CombatTuning.SwordHeavyAttack.Animation -- checked by name
        /// rather than caching a reference, the same "ask the Animator
        /// what it's actually doing right now" approach
        /// PlayerLocomotion.IsCarAnimatorStateFinished already uses.
        /// </summary>
        private const string HeavyAttackStateName = "sword_charged_attack";

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
        /// Runs every frame once equipped -- picks HeavyAttackGripPosition/
        /// HeavyAttackGripRotationEuler only while sword_charged_attack is
        /// actually the current animator state, the ordinary grip pose
        /// otherwise (idle, walk, regular attacks, block, ...). Matches
        /// two_handed_sword.gd's own _update_held_pose exactly -- see
        /// HeavyAttackGripPosition's own doc comment for why this needs to
        /// switch at all instead of being one fixed transform.
        /// </summary>
        private void UpdateHeldPose()
        {
            bool heavy = _animator != null &&
                _animator.GetCurrentAnimatorStateInfo(0).IsName(HeavyAttackStateName);
            transform.localPosition = heavy ? HeavyAttackGripPosition : GripHandPosition;
            transform.localEulerAngles = heavy ? HeavyAttackGripRotationEuler : GripHandRotationEuler;
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
            _animator = player.GetComponentInChildren<Animator>();
            Transform hand = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
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
