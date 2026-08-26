using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A basic jetpack: hold Space to thrust straight up (with WASD still
    /// steering horizontally, exactly as ordinary falling already lets you),
    /// let go to fall normally. No Godot equivalent to port -- an explicit
    /// new-mechanic ask, same footing as PlayerLocomotion's own
    /// ChargedJumpHeightMultiplier.
    ///
    /// REPLACES THE CHARGED JUMP ENTIRELY WHILE EQUIPPED, not layered on top
    /// of it -- PlayerInputReader checks EquippedJetpack before calling
    /// TickJumpCharge at all, matching how a gun/grapple hook already take
    /// Space/click over from the ordinary combat dispatch the instant they
    /// are equipped. Space meaning two different things depending on what
    /// is in your hands is already this project's own convention (aim vs.
    /// melee vs. gun trigger); this is the same idea for the jump key.
    ///
    /// INFINITE FUEL, on purpose -- the explicit ask says so outright
    /// ("for now just make the jetpack have infinite fuel"), so there is no
    /// resource to track or drain here at all, matching how ChargedJump
    /// itself carries no stamina cost either.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Jetpack : MonoBehaviour, IEquippableItem, IJetpack
    {
        public string ItemId => ItemRegistry.Jetpack;

        public const float InteractRange = 2.5f;

        /// <summary>
        /// Faster than PlayerLocomotion.JumpVelocity (4.5) -- a jump is a
        /// brief arc, this has to read as sustained powered flight for as
        /// long as Space stays down, which needs more than jump-height
        /// speed to feel like thrust rather than a very long hang time.
        /// </summary>
        public const float ThrustSpeed = 7.0f;

        /// <summary>
        /// Sits flush against the spine, between the shoulder blades --
        /// pushed out to -0.34, not just past the torso's own ~0.26 radius
        /// (FusedBodyShape.TorsoRadius), confirmed necessary by rendering
        /// it at -0.20 first and finding the tanks visibly sunk into the
        /// back rather than resting on it.
        /// </summary>
        public static readonly Vector3 BackPosition = new Vector3(0.0f, 0.02f, -0.34f);

        public static readonly Vector3 BackRotationEuler = Vector3.zero;

        [Tooltip("Set by JetpackBuilder -- plays while thrusting, stopped otherwise.")]
        public ParticleSystem FlameEffect;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;

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
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }
            if (Vector3.Distance(transform.position, _nearbyPlayer.position) > InteractRange)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.AddItem(ItemRegistry.Jetpack, 1))
            {
                Destroy(gameObject);
            }
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform spine = animator != null ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            if (spine != null)
            {
                transform.SetParent(spine, false);
                transform.localPosition = BackPosition;
                transform.localEulerAngles = BackRotationEuler;
            }

            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            _locomotion = player.GetComponent<PlayerLocomotion>();
            _vitals = player.GetComponent<PlayerVitals>();

            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedJetpack = this;
            }

            if (FlameEffect != null)
            {
                FlameEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedJetpack, this))
            {
                input.EquippedJetpack = null;
            }
            Destroy(gameObject);
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>
        /// Called every frame while equipped, in place of the ordinary
        /// charged-jump handling. Sets the body's own vertical velocity
        /// directly rather than adding an impulse -- the same
        /// SetVerticalVelocity hook GrappleSwing's release and
        /// PlayerClimbing's hop-off already use, called BEFORE
        /// PlayerLocomotion.Tick runs this same frame so its own gravity
        /// integration (which always fires first inside Tick) only ever
        /// shaves one frame's worth off the thrust speed rather than
        /// stacking a whole extra frame of unopposed fall first.
        /// </summary>
        public void Tick(bool thrustHeld, float deltaTime)
        {
            if (thrustHeld)
            {
                _locomotion?.SetVerticalVelocity(ThrustSpeed);
                // See PlayerVitals.ResetFallTracking's own doc comment --
                // this is the whole mechanism behind "negates fall damage
                // from the last height it was on."
                if (_vitals != null && _locomotion != null)
                {
                    _vitals.ResetFallTracking(_locomotion.transform.position.y);
                }
            }

            if (FlameEffect != null)
            {
                if (thrustHeld && !FlameEffect.isEmitting)
                {
                    FlameEffect.Play();
                }
                else if (!thrustHeld && FlameEffect.isEmitting)
                {
                    FlameEffect.Stop();
                }
            }
        }
    }
}
