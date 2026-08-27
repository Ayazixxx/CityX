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
        /// Vertical speed while boosting with no WASD direction held --
        /// "if not holding wasd keys then just make it go up faster",
        /// from the explicit ask. Only applies with nothing to lean
        /// toward; holding a direction spends the boost on lean instead
        /// (see TickTilt's own doc comment).
        /// </summary>
        public const float BoostThrustSpeed = 11.0f;

        /// <summary>
        /// Sits flush against the spine, between the shoulder blades --
        /// pushed out to -0.34, not just past the torso's own ~0.26 radius
        /// (FusedBodyShape.TorsoRadius), confirmed necessary by rendering
        /// it at -0.20 first and finding the tanks visibly sunk into the
        /// back rather than resting on it.
        /// </summary>
        public static readonly Vector3 BackPosition = new Vector3(0.0f, 0.02f, -0.34f);

        public static readonly Vector3 BackRotationEuler = Vector3.zero;

        /// <summary>
        /// How far the body leans at full WASD deflection, in degrees --
        /// "tilts SLIGHTLY", from the explicit ask, so this stays well
        /// short of anything that would read as falling over.
        /// </summary>
        public const float TiltMaxDegrees = 12.0f;

        /// <summary>
        /// How far the body leans instead, while boosting with a WASD
        /// direction held -- "tilt a little further in whatever direction
        /// you are holding", from the explicit ask. Still short of anything
        /// reading as falling over, just more pronounced than the plain lean.
        /// </summary>
        public const float BoostTiltMaxDegrees = 20.0f;

        /// <summary>
        /// How fast the lean eases toward its target, in degrees per
        /// second -- fast enough that tapping a direction reads as an
        /// immediate lean rather than a sluggish drift, matching the same
        /// "fast enough to stay invisible on a stopwatch" reasoning
        /// PlayerLocomotion.HorizontalAcceleration's own doc comment gives.
        /// At this rate the full 12-degree lean is reached in 12/90 = 0.13s.
        /// </summary>
        public const float TiltDegreesPerSecond = 90.0f;

        [Tooltip("Set by JetpackBuilder -- plays while thrusting, stopped otherwise.")]
        public ParticleSystem FlameEffect;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Transform _animatorTransform;

        /// <summary>Current eased lean, X = pitch (forward/back), Z = roll (strafe).</summary>
        private Vector2 _currentTilt;

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
            // The whole visual body, leaned as one rigid piece -- nothing
            // else drives this transform's own localRotation (root motion
            // is off, so animation clips only move bones under it), the
            // same reason PlayerLocomotion's own ApplyHeight is free to
            // drive its localScale unopposed.
            _animatorTransform = animator != null ? animator.transform : null;
            _currentTilt = Vector2.zero;

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
            // Otherwise the body stays leaned from whatever WASD was held
            // the instant the jetpack came off.
            if (_animatorTransform != null)
            {
                _animatorTransform.localRotation = Quaternion.identity;
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
        ///
        /// BOOST SPENDS ON EITHER LEAN OR CLIMB, NEVER BOTH -- "if not
        /// holding wasd keys then just make it go up faster", from the
        /// explicit ask, which only makes sense read as an else: WASD held
        /// means there is a direction to lean toward, so the boost goes
        /// into TickTilt's own amplified lean instead of also stacking a
        /// faster climb on top of it.
        /// </summary>
        public void Tick(bool thrustHeld, Vector2 moveInput, bool boostHeld, float deltaTime)
        {
            bool boostingStraightUp = boostHeld && moveInput.sqrMagnitude < 0.0001f;
            if (thrustHeld)
            {
                _locomotion?.SetVerticalVelocity(boostingStraightUp ? BoostThrustSpeed : ThrustSpeed);
                // See PlayerVitals.ResetFallTracking's own doc comment --
                // this is the whole mechanism behind "negates fall damage
                // from the last height it was on."
                if (_vitals != null && _locomotion != null)
                {
                    _vitals.ResetFallTracking(_locomotion.transform.position.y);
                }
            }

            TickTilt(moveInput, boostHeld, deltaTime);

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

        /// <summary>
        /// Where the lean is headed for a given WASD reading, in degrees --
        /// X = pitch (forward/back), Y = roll (strafe). Pure and static so
        /// the direction/scale of the lean is testable without a real
        /// Animator, the same reasoning PlayerInputReader.TickJumpCharge and
        /// PlayerVoiceCapture.ShouldCapture split their own decisions out
        /// for.
        ///
        /// BOTH SIGNS CHECKED DIRECTLY, not derived by hand and trusted --
        /// a mirror-image sign error here would be invisible in a still
        /// render and only show up as "leans backwards from what you'd
        /// expect" once actually played. Confirmed with a scratch script
        /// printing Quaternion.Euler(10,0,0)*forward = (0, -0.17, 0.98) and
        /// Euler(0,0,10)*right = (0.98, 0.17, 0): a POSITIVE local-X angle
        /// carries forward DOWN (a forward dive/pitch), and a POSITIVE
        /// local-Z angle carries the right side UP (a bank toward the
        /// character's own left).
        ///
        /// PITCH MATCHES SIGN ON moveInput.y (forward/back): a positive
        /// angle is exactly the "dive into the direction of travel" lean
        /// pressing W should produce, confirmed above -- no negation
        /// needed.
        ///
        /// ROLL IS NEGATED ON moveInput.x (strafe): a positive angle banks
        /// left (confirmed above), so strafing RIGHT (positive x) needs a
        /// NEGATIVE angle to bank the right side down -- leaning into the
        /// strafe direction rather than away from it.
        /// </summary>
        public static Vector2 TiltTarget(Vector2 moveInput, bool boosting = false)
        {
            float maxDegrees = boosting ? BoostTiltMaxDegrees : TiltMaxDegrees;
            return new Vector2(moveInput.y * maxDegrees, -moveInput.x * maxDegrees);
        }

        /// <summary>
        /// Eases the current lean toward its target, one axis at a time --
        /// pure and static for the same testability reason TiltTarget is.
        /// Independent per-axis MoveTowards, not a single blended lerp, so
        /// releasing just one of two held keys (e.g. W+D, then letting go
        /// of D) drops only that axis back to zero rather than re-easing
        /// the whole lean vector toward a new diagonal target.
        /// </summary>
        public static Vector2 EaseTilt(Vector2 current, Vector2 target, float deltaTime)
        {
            float maxDelta = TiltDegreesPerSecond * deltaTime;
            return new Vector2(
                Mathf.MoveTowards(current.x, target.x, maxDelta),
                Mathf.MoveTowards(current.y, target.y, maxDelta));
        }

        /// <summary>
        /// Boosting only amplifies the lean while there is a direction to
        /// lean toward -- with no WASD held, Tick spends the same boost on
        /// a faster climb instead (see its own doc comment), so this must
        /// not also treat "boosting straight up" as "lean further toward
        /// nothing," which would just widen dead-zone jitter around zero.
        /// </summary>
        private void TickTilt(Vector2 moveInput, bool boostHeld, float deltaTime)
        {
            bool amplify = boostHeld && moveInput.sqrMagnitude > 0.0001f;
            _currentTilt = EaseTilt(_currentTilt, TiltTarget(moveInput, amplify), deltaTime);
            if (_animatorTransform != null)
            {
                _animatorTransform.localRotation = Quaternion.Euler(_currentTilt.x, 0.0f, _currentTilt.y);
            }
        }
    }
}
