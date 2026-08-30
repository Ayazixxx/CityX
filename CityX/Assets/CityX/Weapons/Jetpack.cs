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
        /// Roughly how long the lean takes to settle into a new target, in
        /// seconds, via Mathf.SmoothDamp -- EXPLICIT CORRECTION: this used
        /// to be a plain constant-rate Mathf.MoveTowards ramp
        /// (TiltDegreesPerSecond, 90 degrees/second), which reaches its
        /// target at a constant angular velocity and then stops dead the
        /// instant it arrives -- a real discontinuity in speed, not just a
        /// fast one, which is exactly what reads as the body "jumping into
        /// place" rather than settling into the lean. SmoothDamp's own
        /// critically-damped curve eases out approaching the target the
        /// same way it eases in leaving the last one, so both ends of the
        /// motion are gradual. Also slower than the old rate on top of
        /// that, per the explicit ask -- the old constant rate reached the
        /// full 12-degree lean in 12/90 = 0.13s; this settles close to a
        /// target in roughly 2-3x TiltSmoothTime, noticeably more relaxed
        /// without reading as sluggish.
        /// </summary>
        public const float TiltSmoothTime = 0.25f;

        [Tooltip("Set by JetpackBuilder -- plays while thrusting, stopped otherwise.")]
        public ParticleSystem FlameEffect;

        [Tooltip("Set by JetpackBuilder (jetpack_thrust_loop.mp3) -- loops while thrusting, stopped otherwise.")]
        public AudioSource ThrustAudio;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Transform _animatorTransform;

        /// <summary>Current eased lean, X = pitch (forward/back), Z = roll (strafe).</summary>
        private Vector2 _currentTilt;

        /// <summary>SmoothDamp's own persistent per-axis rate of change -- see EaseTilt's own doc comment for why this has to be carried between calls rather than derived fresh each frame.</summary>
        private Vector2 _tiltVelocity;

        /// <summary>Exposed for testing without a real Animator -- see TickTilt's own doc comment.</summary>
        public Vector2 CurrentTilt => _currentTilt;

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
            _tiltVelocity = Vector2.zero;

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

            TickTilt(thrustHeld, moveInput, boostHeld, deltaTime);

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

            if (ThrustAudio != null)
            {
                if (thrustHeld && !ThrustAudio.isPlaying)
                {
                    ThrustAudio.Play();
                }
                else if (!thrustHeld && ThrustAudio.isPlaying)
                {
                    ThrustAudio.Stop();
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
        /// pure and static for the same testability reason TiltTarget is,
        /// just with the smoothing's own persistent rate-of-change threaded
        /// through explicitly (<paramref name="velocity"/>) rather than
        /// hidden in an instance field, so a test can still drive this with
        /// nothing but plain values. Independent per-axis SmoothDamp, not a
        /// single blended one, so releasing just one of two held keys (e.g.
        /// W+D, then letting go of D) drops only that axis back to zero
        /// rather than re-easing the whole lean vector toward a new
        /// diagonal target.
        ///
        /// SmoothDamp OVER MoveTowards -- see TiltSmoothTime's own doc
        /// comment for why a constant-rate ramp reads as snapping into
        /// place the instant it arrives. SmoothDamp needs a velocity to
        /// carry between calls (that is what makes it decelerate into the
        /// target instead of stopping dead), which is the whole reason this
        /// is no longer quite as stateless as a plain MoveTowards call was.
        /// </summary>
        public static Vector2 EaseTilt(Vector2 current, Vector2 target, ref Vector2 velocity, float deltaTime)
        {
            return new Vector2(
                Mathf.SmoothDamp(current.x, target.x, ref velocity.x, TiltSmoothTime, Mathf.Infinity, deltaTime),
                Mathf.SmoothDamp(current.y, target.y, ref velocity.y, TiltSmoothTime, Mathf.Infinity, deltaTime));
        }

        /// <summary>
        /// ONLY LEANS WHILE ACTUALLY THRUSTING -- an explicit correction:
        /// this used to lean off WASD alone regardless of whether Space was
        /// held, so simply air-steering a fall with the jetpack equipped
        /// (but not thrusting) leaned the body as if flying. With thrust
        /// not held the target is forced to level, easing the body back to
        /// neutral the same way letting go of a single WASD key already
        /// eases that axis back (see EaseTiltStraightensOutOnceTheKeyIsReleased),
        /// so the model's own pose reads as idle/falling rather than banked
        /// the instant thrust actually stops.
        ///
        /// Boosting only amplifies the lean while there is a direction to
        /// lean toward -- with no WASD held, Tick spends the same boost on
        /// a faster climb instead (see its own doc comment), so this must
        /// not also treat "boosting straight up" as "lean further toward
        /// nothing," which would just widen dead-zone jitter around zero.
        /// </summary>
        private void TickTilt(bool thrustHeld, Vector2 moveInput, bool boostHeld, float deltaTime)
        {
            bool amplify = boostHeld && moveInput.sqrMagnitude > 0.0001f;
            Vector2 target = thrustHeld ? TiltTarget(moveInput, amplify) : Vector2.zero;
            _currentTilt = EaseTilt(_currentTilt, target, ref _tiltVelocity, deltaTime);
            if (_animatorTransform != null)
            {
                _animatorTransform.localRotation = Quaternion.Euler(_currentTilt.x, 0.0f, _currentTilt.y);
            }
        }
    }
}
