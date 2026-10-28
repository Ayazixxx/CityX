using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// SOMETHING ON THE BACK THAT FLIES THE BODY (docs/FLIGHT.md): the jetpack and the angel wings. What they share is here; each gives its
    /// numbers (<see cref="Tuning"/>), its lean (<see cref="LeanTarget"/>) and what it shows (a flame, a wingbeat).
    /// <list type="bullet">
    /// <item>Worn on the Spine bone (<see cref="MountPosition"/>); on another player's copy the same, as a model only (ICosmeticMountable).</item>
    /// <item>The flight itself is <see cref="FlightModel"/>: Space takes off in the air, then the body hovers, climbs, sinks and flies about with
    /// inertia until it lands. Each frame the model's vertical velocity is set on the body (before PlayerLocomotion integrates it) and its
    /// horizontal speed handed to PlayerInputReader for LocomotionInput; the height flown from never counts as a fall.</item>
    /// <item>The body leans (the Animator's root turned as one rigid piece) toward the lean the gear asks for, eased by SmoothDamp.</item>
    /// <item>The camera may look straight up and down while the gear is worn, and reacts to speed (FlightFeel: a wider view, a pull-back, the
    /// streaks of air): the Flight Speed stat scales every speed.</item>
    /// </list>
    /// </summary>
    public abstract class FlightGear : MonoBehaviour, IEquippableItem, IFlightGear, ICosmeticMountable
    {
        public const float InteractRange = 2.5f;

        /// <summary>How far the camera may look while flight gear is worn: straight up and straight down, so a dive or a climb can go all the way.</summary>
        public const float MinPitchDegrees = -90.0f;

        public const float MaxPitchDegrees = 90.0f;

        /// <summary>Roughly how long the lean takes to settle into a new target, seconds (SmoothDamp, so both ends of the motion are gradual).</summary>
        public const float TiltSmoothTime = 0.25f;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private FlightModel _model;
        private ThirdPersonCamera _orbitCamera;
        private float _savedMinPitch;
        private float _savedMaxPitch;
        private Vector2 _currentTilt;
        private Vector2 _tiltVelocity;

        // --- flight feel (FlightFeel): how the camera reacts to speed ---
        private float _feelFov;
        private float _feelFovVelocity;
        private float _feelDistance;
        private float _feelDistanceVelocity;
        private float _feelKickAge = 1.0e9f;
        private bool _feelWasBoosting;
        private JetpackSpeedLines _speedLines;

        public abstract string ItemId { get; }

        /// <summary>Where it sits on the Spine bone, metres, and how it is turned.</summary>
        protected abstract Vector3 MountPosition { get; }

        protected virtual Vector3 MountRotationEuler => Vector3.zero;

        /// <summary>The gear's speeds (asked once it is worn: a jetpack's follow the body's own sprint).</summary>
        protected abstract FlightTuning Tuning { get; }

        /// <summary>Where the body's lean is headed while flying, degrees: x pitch (forward positive), y roll (a right strafe negative).</summary>
        protected abstract Vector2 LeanTarget(in FlightInput input);

        /// <summary>Every frame while worn, after the flight has been decided: the gear's own effects (the flame, the beat).</summary>
        protected virtual void OnFlightTick(in FlightInput input, float deltaTime)
        {
        }

        /// <summary>The body went limp or the gear came off: whatever shows must stop at once.</summary>
        protected virtual void OnEffectsStopped()
        {
        }

        protected virtual void OnWorn(GameObject player)
        {
        }

        protected virtual void OnTakenOff(GameObject player)
        {
        }

        protected virtual void OnMounted(GameObject body)
        {
        }

        /// <summary>The vertical velocity to set on the body this frame: the model's, or the gear's own shape of it (the wings' beat pulses it).</summary>
        protected virtual float VerticalVelocityFor(float modelVertical, float deltaTime) => modelVertical;

        /// <summary>The body wearing it (null on a copy, or on the ground as a pickup).</summary>
        protected PlayerLocomotion Locomotion { get; private set; }

        protected PlayerVitals Vitals { get; private set; }

        /// <summary>The visual root under the body root, leaned as one rigid piece (nothing else turns it: root motion is off).</summary>
        protected Transform AnimatorTransform { get; private set; }

        public bool IsEquipped { get; private set; }

        /// <summary>The flight, as it stands: created from <see cref="Tuning"/> when first needed.</summary>
        public FlightModel Model => _model ??= new FlightModel(Tuning);

        public bool IsFlying => _model != null && _model.Flying;

        public float HorizontalSpeed => IsFlying ? _model.HorizontalSpeed : 0.0f;

        public float HorizontalAcceleration => IsFlying ? _model.Tuning.Acceleration : 0.0f;

        /// <summary>The Flight Speed stat's scale on every speed (1 for a body without stats).</summary>
        public float SpeedScale => PlayerVitals.SpeedOf(Vitals, StatId.FlightSpeed);

        /// <summary>Current eased lean, x pitch, y roll, degrees.</summary>
        public Vector2 CurrentTilt => _currentTilt;

        /// <summary>The camera's current flight widening, degrees, and pull-back, metres -- for tests and tuning.</summary>
        public float FeelFovBonus => _feelFov;

        public float FeelDistanceBonus => _feelDistance;

        /// <summary>The camera the flight feel drives. Set on equip; a test can point it at its own.</summary>
        public ThirdPersonCamera FeelCamera
        {
            get => _orbitCamera;
            set => _orbitCamera = value;
        }

        protected virtual void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        protected virtual void Update()
        {
            if (!IsEquipped)
            {
                TickPickup();
            }
        }

        // --- a pickup on the ground: proximity and E, every item's own convention ---

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
            if (_nearbyPlayer == null || !InteractKey.Tapped || Vector3.Distance(transform.position, _nearbyPlayer.position) > InteractRange)
            {
                return;
            }
            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.PickUp(ItemId))
            {
                Destroy(gameObject);
            }
        }

        // --- worn ---

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform spine = animator != null ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            if (spine != null)
            {
                transform.SetParent(spine, false);
                transform.localPosition = MountPosition;
                transform.localEulerAngles = MountRotationEuler;
            }
            AnimatorTransform = animator != null ? animator.transform : null;
            _currentTilt = Vector2.zero;
            _tiltVelocity = Vector2.zero;

            IsEquipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }
            Locomotion = player.GetComponent<PlayerLocomotion>();
            Vitals = player.GetComponent<PlayerVitals>();
            _model = new FlightModel(Tuning);

            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedFlightGear = this;
                _orbitCamera = input.OrbitCamera;
            }
            // Widened for as long as the gear is worn, not just in flight: a player looks where they are about to climb or dive first.
            if (_orbitCamera != null)
            {
                _savedMinPitch = _orbitCamera.MinPitch;
                _savedMaxPitch = _orbitCamera.MaxPitch;
                _orbitCamera.MinPitch = MinPitchDegrees;
                _orbitCamera.MaxPitch = MaxPitchDegrees;
                _speedLines = JetpackSpeedLines.Attach(_orbitCamera.transform);
            }
            OnWorn(player);
        }

        public void OnUnequip(GameObject player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedFlightGear, this))
            {
                input.EquippedFlightGear = null;
            }
            if (_orbitCamera != null)
            {
                _orbitCamera.MinPitch = _savedMinPitch;
                _orbitCamera.MaxPitch = _savedMaxPitch;
                // And the camera's own angle back inside the ordinary range, or one left looking straight down stays there on foot.
                _orbitCamera.Pitch = Mathf.Clamp(_orbitCamera.Pitch, _savedMinPitch, _savedMaxPitch);
                _orbitCamera.FlightFovBonus = 0.0f;
                _orbitCamera.FlightDistanceBonus = 0.0f;
            }
            if (_speedLines != null)
            {
                Destroy(_speedLines.gameObject);
                _speedLines = null;
            }
            // Otherwise the body stays leaned from however it was flying the instant the gear came off.
            if (AnimatorTransform != null)
            {
                AnimatorTransform.localRotation = Quaternion.identity;
            }
            _model?.Stop();
            OnTakenOff(player);
            Destroy(gameObject);
        }

        // --- on a copy ---

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.Spine, MountPosition, MountRotationEuler, 1.0f);
            OnMounted(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public abstract void SetCosmeticActive(bool active);

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        // --- the flight ---

        public void Tick(in FlightInput input, float deltaTime)
        {
            FlightModel model = Model;
            model.Tick(input, SpeedScale);
            if (model.Flying)
            {
                // Set BEFORE PlayerLocomotion.Tick integrates the frame, whose gravity would shave a frame's worth off (and sink a hover
                // 0.16 m/s): that frame's gravity is added back here, so the body moves at exactly the velocity asked for.
                if (Locomotion != null)
                {
                    float gravityThisFrame = Locomotion.Gravity * Locomotion.GravityScale * deltaTime;
                    Locomotion.SetVerticalVelocity(VerticalVelocityFor(model.VerticalVelocity, deltaTime) + gravityThisFrame);
                }
                // The height flown from never hurts the landing (PlayerVitals.ResetFallTracking).
                if (Vitals != null && Locomotion != null)
                {
                    Vitals.ResetFallTracking(Locomotion.transform.position.y);
                }
            }
            TickTilt(input, model.Flying, deltaTime);
            if (Locomotion != null)
            {
                // Speed is the whole body's, so a fast fall reads as fast too; on the ground it is off.
                TickFlightFeel(Locomotion.Velocity, !Locomotion.IsGrounded, model.Flying && model.Boosting, deltaTime);
            }
            OnFlightTick(input, deltaTime);
        }

        public void StopThrustEffects()
        {
            ResetFlightFeel();
            _model?.Stop();
            OnEffectsStopped();
        }

        // --- the lean ---

        private void TickTilt(in FlightInput input, bool flying, float deltaTime)
        {
            Vector2 target = flying ? LeanTarget(input) : Vector2.zero;
            _currentTilt = EaseTilt(_currentTilt, target, ref _tiltVelocity, deltaTime);
            if (AnimatorTransform != null)
            {
                AnimatorTransform.localRotation = TiltRotation(_currentTilt);
            }
        }

        /// <summary>Snaps the lean back to upright at once, bypassing the easing: for the moment a ragdoll takes over, not for ordinary flight.</summary>
        public void ResetTilt()
        {
            _currentTilt = Vector2.zero;
            _tiltVelocity = Vector2.zero;
            if (AnimatorTransform != null)
            {
                AnimatorTransform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Eases the lean toward its target one axis at a time (letting go of one of two held keys drops only that axis), by SmoothDamp, whose
        /// rate of change is carried between calls in <paramref name="velocity"/>.
        /// </summary>
        public static Vector2 EaseTilt(Vector2 current, Vector2 target, ref Vector2 velocity, float deltaTime)
        {
            return new Vector2(
                Mathf.SmoothDamp(current.x, target.x, ref velocity.x, TiltSmoothTime, Mathf.Infinity, deltaTime),
                Mathf.SmoothDamp(current.y, target.y, ref velocity.y, TiltSmoothTime, Mathf.Infinity, deltaTime));
        }

        /// <summary>
        /// A (pitch, roll) lean as the visual root's local rotation: the roll about the fixed, un-pitched forward (the real direction of travel,
        /// the parent already faces it), THEN the pitch -- so a body pitched flat still banks like a plane rather than yawing.
        /// </summary>
        public static Quaternion TiltRotation(Vector2 tilt)
        {
            Quaternion pitch = Quaternion.AngleAxis(tilt.x, Vector3.right);
            Quaternion roll = Quaternion.AngleAxis(tilt.y, Vector3.forward);
            return roll * pitch;
        }

        // --- flight feel: how the camera reacts to speed ---

        /// <summary>
        /// One frame of the flight feel (<see cref="FlightFeel"/>): springs the camera's widening and pull-back toward the speed, punches both
        /// the moment a boost begins, and feeds the speed streaks. Public so a test can drive it with a made-up speed.
        /// </summary>
        public void TickFlightFeel(Vector3 velocity, bool airborne, bool boosting, float deltaTime)
        {
            bool boostBegan = boosting && !_feelWasBoosting;
            _feelWasBoosting = boosting;
            _feelKickAge = boostBegan ? 0.0f : _feelKickAge + deltaTime;

            float fraction = airborne ? FlightFeel.SpeedFraction(velocity.magnitude) : 0.0f;
            float kick = airborne ? FlightFeel.KickEnvelope(_feelKickAge) : 0.0f;

            _feelFov = Mathf.SmoothDamp(_feelFov, FlightFeel.FovBonus(fraction, kick),
                ref _feelFovVelocity, FlightFeel.SmoothSeconds, Mathf.Infinity, deltaTime);
            _feelDistance = Mathf.SmoothDamp(_feelDistance, FlightFeel.DistanceBonus(fraction, kick),
                ref _feelDistanceVelocity, FlightFeel.SmoothSeconds * 1.3f, Mathf.Infinity, deltaTime);

            if (_orbitCamera != null)
            {
                _orbitCamera.FlightFovBonus = _feelFov;
                _orbitCamera.FlightDistanceBonus = _feelDistance;
            }
            if (_speedLines != null)
            {
                _speedLines.Set(fraction, velocity);
            }
            OnFlightFeel(fraction);
        }

        /// <summary>The speed's share of the feel's range this frame, 0..1 (the jetpack's wind quickens with it).</summary>
        protected virtual void OnFlightFeel(float speedFraction)
        {
        }

        /// <summary>Snaps the flight feel back to nothing: no widening, no pull-back, no streaks.</summary>
        public void ResetFlightFeel()
        {
            _feelFov = 0.0f;
            _feelFovVelocity = 0.0f;
            _feelDistance = 0.0f;
            _feelDistanceVelocity = 0.0f;
            _feelKickAge = 1.0e9f;
            _feelWasBoosting = false;
            if (_orbitCamera != null)
            {
                _orbitCamera.FlightFovBonus = 0.0f;
                _orbitCamera.FlightDistanceBonus = 0.0f;
            }
            if (_speedLines != null)
            {
                _speedLines.Set(0.0f, Vector3.zero);
            }
        }
    }
}
