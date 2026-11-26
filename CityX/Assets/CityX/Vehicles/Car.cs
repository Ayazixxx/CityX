using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Vehicles
{
    /// <summary>
    /// GTA-style arcade vehicle -- ported from vehicles/car.gd. One
    /// Rigidbody, raycast-only wheels (no simulated suspension springs),
    /// velocity decomposition + partial lateral-velocity cancellation
    /// ("grip") as the core handling/drift mechanic -- deliberately NOT a
    /// tire-simulation racer, matching the Godot original's own explicit
    /// scope.
    ///
    /// UNITY'S FORWARD IS +Z LOCALLY; GODOT'S IS -Z. Every local-space
    /// offset ported from car.gd (wheel positions, seat positions, the
    /// exit-side offset) has its Z sign flipped from the source for exactly
    /// this reason -- the fraction and the reasoning behind each one are
    /// unchanged, only which physical axis "the nose" sits on.
    ///
    /// PHYSICS (BodySize, wheel rays, grip, engine force, drag, angular
    /// response) all live in FixedUpdate, reading CarPhysicsMath's own pure
    /// functions -- see that class's own doc comment for why the actual
    /// numbers are factored out there instead of inline here. SEAT
    /// OCCUPANCY/ENTER-EXIT (this class's own _process/_begin_enter/
    /// _complete_enter/_begin_exit/_complete_exit equivalents) lives in
    /// Update, matching car.gd's own split between _integrate_forces and
    /// _process.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Car : MonoBehaviour
    {
        [Header("Tunables (car.gd's own @export defaults)")]
        public float MaxSpeed = 40.0f;
        public float Acceleration = 20.0f;
        public float BrakingForce = 35.0f;
        public float ReverseSpeed = 14.0f;
        public float DragCoefficient = 0.02f;
        public float RollingResistance = 2.5f;

        [Tooltip("Degrees, max wheel angle.")]
        public float TurnRate = 32.0f;

        [Tooltip("Degrees/second, how fast the wheel angle itself moves.")]
        public float TurnSpeed = 200.0f;

        public float GripFront = 0.95f;
        public float GripRear = 0.88f;
        public float HandbrakeGripRear = 0.12f;

        [Header("Measured at build time by CarBuilder")]
        [Tooltip("The visual mesh's own measured, already-normalized extents -- see CarBuilder.Build.")]
        public Vector3 BodySize = new Vector3(1.9f, 1.3f, 4.8f);

        public Transform DriverSeat;
        public Transform PassengerSeat;

        // --- Implementation-level constants -- see car.gd's own identically
        // named constants for the reasoning behind each fraction. ---
        public const float RearBiasFraction = 0.12f;
        public const float WheelTrackFraction = 0.85f;
        public const float WheelBaseFraction = 0.75f;
        public const float WheelRayLength = 0.9f;
        public const float RayStartClearance = 0.2f;
        public const int MinGroundedRays = 2;
        public const float InteractRange = 4.0f;
        public const float SeatXFraction = 0.32f;

        /// <summary>
        /// Requested: "make it so when you sit in the car your butt is
        /// in the seat hands on the steering wheel and your character is
        /// stuck there but cant collide with the car." Investigated live:
        /// the "stuck"/"no collision" halves were already correct
        /// (CompleteEnter already parents onto DriverSeat and disables the
        /// CharacterController) -- what actually needed fixing was the pose
        /// itself, and the real cause turned out to be a genuine SCALE
        /// MISMATCH, not a bad seat offset: this project's own player rig
        /// stands roughly 2.6m tall (a deliberate stylized proportion, see
        /// [[character_proportions_decision]]), while Mustang.prefab is
        /// only a real-world car scaled up 1.3x (CarBuilder.
        /// TargetLengthMeters) -- nowhere near enough cabin headroom for a
        /// rider that tall. Explicitly asked the user whether to scale the
        /// car up further, shrink the character while seated, or just tune
        /// the pose as close as it can get and accept some clipping --
        /// they chose the third option.
        ///
        /// -0.32 -> -0.46, LOWERED not raised -- a real render comparison
        /// across several candidate offsets (0, -0.3, -0.5, -0.7m, on top
        /// of the original) showed head-through-roof is the more visually
        /// jarring artifact than hips/shoulders sitting low relative to an
        /// implied cushion, so this trades toward head clearance: at the
        /// old fraction the head reached into the roof/window line; -0.3m
        /// (this new fraction) clears it while the torso still reads as
        /// sitting rather than sunk. -0.5m and beyond made the shoulders
        /// visibly sink INTO the seat/floor geometry -- picked the least
        /// bad point on the curve, not a perfect fit, since no seat
        /// position can be a perfect fit at these relative scales.
        /// </summary>
        public const float SeatYFraction = -0.46f;

        public const float SeatZFraction = -0.04f;
        public const float ExitSideMargin = 1.0f;

        private Rigidbody _rb;
        private Vector3[] _wheelLocalOffsets;

        private float _currentWheelAngle;
        private float _effectiveGrip;
        private bool _isDrifting;
        private bool _isGrounded;

        private PlayerInputReader _driver;
        private PlayerInputReader _passenger;
        private PlayerInputReader _nearbyPlayer;

        private bool _transitioning;
        private PlayerInputReader _transitioningPlayer;
        private Transform _transitioningSeat;
        private bool _transitioningIsEnter;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 1400.0f; // Mustang.tscn's own mass
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            // DISCRETE, NOT car.gd's own continuous_cd -- confirmed
            // directly (CarPhysicsPlayTests) that ContinuousDynamic
            // produces a violent, physically nonsensical launch-and-flip
            // the instant steering is combined with throttle (fine on its
            // own; only misbehaves once the body is actually rotating
            // while in continuous contact with a large static collider).
            // A car resting on and driving across an ordinary street is
            // exactly the case CCD exists to protect fast, thin,
            // rarely-touching objects from tunneling through geometry --
            // not this one, which is heavy, boxy, and in near-constant
            // contact with the ground it drives on. Discrete is the
            // correct mode for that shape of problem in Unity regardless
            // of what Godot's own solver does differently.
            _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            // NEGATIVE, not car.gd's own positive offset -- Godot's own
            // comment is explicit that ITS +Z is the rear ("since forward
            // is -Z"). Unity's forward is +Z (see this class's own header
            // comment), so "toward the rear" is -Z here.
            _rb.centerOfMass = new Vector3(0.0f, 0.0f, -BodySize.z * RearBiasFraction);

            _wheelLocalOffsets = BuildWheelLocalOffsets(BodySize);

            _effectiveGrip = GripRear;
        }

        /// <summary>
        /// The four wheel raycast start points, in local space -- see
        /// car.gd's own _build_wheel_rays for the exact reasoning behind
        /// RayStartClearance and each fraction. Order: FL, FR, RL, RR.
        /// UNITY'S NOSE IS +Z (see this class's own header comment), so FL/
        /// FR sit at +halfZ here where car.gd's own FL/FR sit at -halfZ.
        /// </summary>
        public static Vector3[] BuildWheelLocalOffsets(Vector3 bodySize)
        {
            float halfX = bodySize.x * 0.5f * WheelTrackFraction;
            float halfZ = bodySize.z * 0.5f * WheelBaseFraction;
            float wheelY = -bodySize.y * 0.5f + RayStartClearance;
            return new[]
            {
                new Vector3(-halfX, wheelY, halfZ), // FL
                new Vector3(halfX, wheelY, halfZ), // FR
                new Vector3(-halfX, wheelY, -halfZ), // RL
                new Vector3(halfX, wheelY, -halfZ), // RR
            };
        }

        /// <summary>DriverSeat/PassengerSeat local positions -- see car.gd's own _build_seats for the fraction reasoning.</summary>
        public static void BuildSeatLocalPositions(Vector3 bodySize, out Vector3 driverSeat, out Vector3 passengerSeat)
        {
            float seatX = bodySize.x * 0.5f * SeatXFraction;
            float seatY = bodySize.y * SeatYFraction;
            // POSITIVE, not car.gd's own negative SeatZFraction -- Unity's
            // nose is +Z, so "a little forward of centre" is a positive
            // local Z offset here.
            float seatZ = bodySize.z * 0.5f * SeatZFraction;
            driverSeat = new Vector3(-seatX, seatY, seatZ);
            passengerSeat = new Vector3(seatX, seatY, seatZ);
        }

        private void FixedUpdate()
        {
            float steerInput = 0.0f;
            float throttleInput = 0.0f;
            bool handbrakeInput = false;

            // ONLY THE SEATED DRIVER'S INPUT EVER DRIVES THE CAR, and only
            // once they're actually fully seated (not still mid-transition)
            // -- matches car.gd's own _integrate_forces gate, fixing the
            // exact "works like an RC car" bug its own comment describes:
            // a car with nobody in it should not respond to a bystander's
            // WASD at all.
            if (_driver != null && !_transitioning)
            {
                // WASD or the left stick steers and drives; on a controller the triggers drive too (RT on, LT back), and A is the handbrake.
                GameControls controls = GameControls.Now;
                steerInput = controls.Move.x;
                throttleInput = Mathf.Clamp(controls.Move.y + controls.Throttle, -1.0f, 1.0f);
                handbrakeInput = controls.Jump.Held;
            }

            TickDriving(steerInput, throttleInput, handbrakeInput, Time.fixedDeltaTime);
        }

        /// <summary>
        /// The actual physics tick, factored out from FixedUpdate so a test
        /// can drive it with explicit values -- Keyboard.current is null
        /// outside a real Input System device (nothing in this project's
        /// own tests exercises it directly), matching PistolWeapon.
        /// TryPickup's own established split between "reads the device"
        /// and "makes the decision."
        /// </summary>
        public void TickDriving(float steerInput, float throttleInput, bool handbrakeInput, float deltaTime)
        {
            Vector3 forwardFlat = new Vector3(transform.forward.x, 0.0f, transform.forward.z).normalized;
            Vector3 rightFlat = new Vector3(transform.right.x, 0.0f, transform.right.z).normalized;
            Vector3 velocityFlat = new Vector3(_rb.linearVelocity.x, 0.0f, _rb.linearVelocity.z);
            float forwardSpeed = Vector3.Dot(velocityFlat, forwardFlat);
            float lateralSpeed = Vector3.Dot(velocityFlat, rightFlat);
            float speedFraction = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / MaxSpeed);

            UpdateGrounded();

            // One-frame-stale _effectiveGrip is fine here (imperceptible)
            // and lets IsDrifting be computed once, up front -- see
            // CarPhysicsMath.IsDrifting's own doc comment.
            _isDrifting = CarPhysicsMath.IsDrifting(handbrakeInput, _effectiveGrip, GripRear, forwardSpeed);

            UpdateSteering(steerInput, speedFraction, deltaTime);
            UpdateDriverHands();

            if (_isGrounded)
            {
                ApplyEngineForce(throttleInput, forwardFlat, forwardSpeed);
                ApplyDrag(velocityFlat);
                ApplyGrip(rightFlat, lateralSpeed, handbrakeInput, deltaTime);
                ApplyAngular(speedFraction, deltaTime);
            }
        }

        /// <summary>
        /// Straight-down raycasts near the four corners of the measured
        /// body -- used for exactly one thing (Stage 1 scope, matching
        /// car.gd's own): airborne detection. Fewer than MinGroundedRays of
        /// 4 hitting suppresses engine force/grip/steering-driven rotation
        /// entirely.
        /// </summary>
        private void UpdateGrounded()
        {
            int groundedCount = 0;
            foreach (Vector3 localOffset in _wheelLocalOffsets)
            {
                Vector3 origin = transform.TransformPoint(localOffset);
                if (Physics.Raycast(origin, Vector3.down, RayStartClearance + WheelRayLength))
                {
                    groundedCount++;
                }
            }
            _isGrounded = groundedCount >= MinGroundedRays;
        }

        private void UpdateSteering(float steerInput, float speedFraction, float deltaTime)
        {
            float targetAngle = CarPhysicsMath.SteerTargetAngleRadians(steerInput, speedFraction, _isDrifting, TurnRate);
            _currentWheelAngle = CarPhysicsMath.MoveWheelAngleRadians(_currentWheelAngle, targetAngle, TurnSpeed, deltaTime);
        }

        /// <summary>
        /// Drives PlayerLocomotion's own SetDrivingWheelAngle every tick so
        /// the seated driver's hands visibly turn with the wheel -- max
        /// angle uses the BASE TurnRate (not the drift-boosted effective
        /// rate CarPhysicsMath.SteerTargetAngleRadians computes internally),
        /// matching car.gd's own _update_driver_hands comment: the hand
        /// pose always maps against the same fixed range regardless of
        /// whether a drift happens to be boosting the wheel's own
        /// mechanical limit at this exact instant.
        /// </summary>
        private void UpdateDriverHands()
        {
            if (_driver == null || _transitioning)
            {
                return;
            }
            PlayerLocomotion locomotion = _driver.GetComponent<PlayerLocomotion>();
            locomotion?.SetDrivingWheelAngle(_currentWheelAngle, TurnRate * Mathf.Deg2Rad);
        }

        private void ApplyEngineForce(float throttleInput, Vector3 forwardFlat, float forwardSpeed)
        {
            float forceMagnitude = CarPhysicsMath.EngineForceAlongForward(
                throttleInput, forwardSpeed, MaxSpeed, ReverseSpeed, Acceleration, BrakingForce);
            if (Mathf.Abs(forceMagnitude) < 0.0001f)
            {
                return;
            }
            _rb.AddForce(forwardFlat * forceMagnitude * _rb.mass, ForceMode.Force);
        }

        private void ApplyDrag(Vector3 velocityFlat)
        {
            Vector3 dragForce = CarPhysicsMath.DragForce(velocityFlat, DragCoefficient, RollingResistance);
            if (dragForce.sqrMagnitude < 0.0000001f)
            {
                return;
            }
            _rb.AddForce(dragForce * _rb.mass, ForceMode.Force);
        }

        private void ApplyGrip(Vector3 rightFlat, float lateralSpeed, bool handbrakeInput, float deltaTime)
        {
            _effectiveGrip = CarPhysicsMath.NextEffectiveGrip(
                _effectiveGrip, handbrakeInput, GripFront, GripRear, HandbrakeGripRear, deltaTime);
            float cancelFraction = CarPhysicsMath.LateralCancelFraction(_effectiveGrip, deltaTime);
            _rb.linearVelocity -= rightFlat * lateralSpeed * cancelFraction;
        }

        /// <summary>
        /// Uprighting rate for the roll/pitch damping ApplyAngular applies
        /// below -- not ported from car.gd, which has no equivalent (see
        /// this method's own comment on why). Reasonably brisk (settles
        /// most of a stray tumble in well under a second) without being
        /// instant, which would read as the car being invisibly glued flat
        /// rather than merely well-behaved.
        /// </summary>
        public const float UprightingLerpSpeed = 8.0f;

        private void ApplyAngular(float speedFraction, float deltaTime)
        {
            float responseCurve = CarPhysicsMath.AngularResponseCurve(speedFraction);
            float targetAngularY = CarPhysicsMath.TargetAngularY(_currentWheelAngle, responseCurve);
            float lerpSpeed = _isDrifting ? CarPhysicsMath.DriftAngularLerpSpeed : CarPhysicsMath.NormalAngularLerpSpeed;
            Vector3 angularVelocity = _rb.angularVelocity;
            angularVelocity.y = Mathf.Lerp(angularVelocity.y, targetAngularY, Mathf.Clamp01(lerpSpeed * deltaTime));
            // DAMPS ROLL/PITCH TOWARD ZERO, LEAVING YAW ALONE -- there are
            // no simulated suspension springs anywhere in this controller
            // (see this class's own header comment), so nothing else exists
            // to settle a small residual roll/pitch left over from an
            // imperfect landing. Rigidbody.angularDamping was tried here
            // first and rejected: it damps all three axes UNIFORMLY, which
            // measurably fought this very method's own explicit yaw control
            // on the same field (confirmed directly: with it set, a full
            // steering input barely curved the car's path at all).
            // Gated the same way engine force/drag/grip already are -- see
            // TickDriving's own `if (_isGrounded)` -- so a genuinely
            // airborne tumble (a jump, a ramp) is left alone rather than
            // fought.
            float uprightFraction = Mathf.Clamp01(UprightingLerpSpeed * deltaTime);
            angularVelocity.x = Mathf.Lerp(angularVelocity.x, 0.0f, uprightFraction);
            angularVelocity.z = Mathf.Lerp(angularVelocity.z, 0.0f, uprightFraction);
            _rb.angularVelocity = angularVelocity;
        }

        // --- Seats / enter-exit (car.gd's own _process/_begin_enter/
        // _complete_enter/_begin_exit/_complete_exit). ---

        private void Update()
        {
            if (_transitioning)
            {
                TickTransition();
                return;
            }

            if (!GameControls.Now.Interact.Pressed)
            {
                return;
            }

            if (_driver != null)
            {
                BeginExit(_driver, DriverSeat);
                return;
            }
            if (_passenger != null)
            {
                BeginExit(_passenger, PassengerSeat);
                return;
            }
            if (_nearbyPlayer == null)
            {
                return;
            }
            Transform seat = NearestFreeSeat(_nearbyPlayer.transform);
            if (seat != null)
            {
                BeginEnter(_nearbyPlayer, seat);
            }
        }

        /// <summary>Whichever of DriverSeat/PassengerSeat is closer AND not already occupied -- null if both are taken.</summary>
        private Transform NearestFreeSeat(Transform player)
        {
            bool driverFree = _driver == null;
            bool passengerFree = _passenger == null;
            if (!driverFree && !passengerFree)
            {
                return null;
            }
            if (driverFree && !passengerFree)
            {
                return DriverSeat;
            }
            if (passengerFree && !driverFree)
            {
                return PassengerSeat;
            }
            float toDriver = Vector3.Distance(player.position, DriverSeat.position);
            float toPassenger = Vector3.Distance(player.position, PassengerSeat.position);
            return toDriver <= toPassenger ? DriverSeat : PassengerSeat;
        }

        private void BeginEnter(PlayerInputReader player, Transform seat)
        {
            PlayerLocomotion locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion == null)
            {
                return;
            }
            _transitioning = true;
            _transitioningPlayer = player;
            _transitioningSeat = seat;
            _transitioningIsEnter = true;

            player.BeginCarTransition(gameObject);
            player.transform.rotation = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
            locomotion.PlayEnterCarAnimation();
        }

        private void BeginExit(PlayerInputReader player, Transform seat)
        {
            PlayerLocomotion locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion == null)
            {
                return;
            }
            _transitioning = true;
            _transitioningPlayer = player;
            _transitioningSeat = seat;
            _transitioningIsEnter = false;
            locomotion.PlayExitCarAnimation();
        }

        private void TickTransition()
        {
            PlayerLocomotion locomotion = _transitioningPlayer != null
                ? _transitioningPlayer.GetComponent<PlayerLocomotion>()
                : null;
            string stateName = _transitioningIsEnter ? "EnteringCar" : "ExitingCar";
            if (locomotion != null && !locomotion.IsCarAnimatorStateFinished(stateName))
            {
                return;
            }

            if (_transitioningIsEnter)
            {
                CompleteEnter(_transitioningPlayer, _transitioningSeat);
            }
            else
            {
                CompleteExit(_transitioningPlayer, _transitioningSeat);
            }
            _transitioning = false;
            _transitioningPlayer = null;
            _transitioningSeat = null;
        }

        private void CompleteEnter(PlayerInputReader player, Transform seat)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
            player.transform.SetParent(seat, false);
            player.transform.localPosition = Vector3.zero;
            player.transform.localRotation = Quaternion.identity;

            if (seat == DriverSeat)
            {
                _driver = player;
            }
            else
            {
                _passenger = player;
            }
        }

        /// <summary>
        /// Steps the player out to the side of whichever seat they were in
        /// (driver exits the driver's own side, passenger the other),
        /// at the car's own current position/facing. Yaw is derived from
        /// the car's own FLATTENED forward vector, not a raw Euler Y
        /// component -- a real reported bug in the Godot source this
        /// ports: a Rigidbody is very rarely PERFECTLY level, and grabbing
        /// only the Y Euler angle while X/Z reset to 0 on reparent can
        /// produce an inconsistent triple that isn't "upright, facing the
        /// car's own direction" at all. See car.gd's own _complete_exit
        /// comment for the full reasoning.
        /// </summary>
        private void CompleteExit(PlayerInputReader player, Transform seat)
        {
            if (seat == DriverSeat)
            {
                _driver = null;
            }
            else
            {
                _passenger = null;
            }

            float sideSign = seat == DriverSeat ? -1.0f : 1.0f;
            Vector3 exitOffset = transform.right * sideSign * (BodySize.x * 0.5f + ExitSideMargin);
            Vector3 exitPosition = seat.position + exitOffset;

            Vector3 forwardFlat = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
            float yaw = forwardFlat.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forwardFlat, Vector3.up).eulerAngles.y
                : transform.eulerAngles.y;

            player.transform.SetParent(transform.parent, false);
            player.transform.position = exitPosition;
            player.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = true;
            }

            PlayerLocomotion locomotion = player.GetComponent<PlayerLocomotion>();
            locomotion?.ClearDrivingWheelPose();

            player.EndCarTransition();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_nearbyPlayer != null)
            {
                return;
            }
            var reader = other.GetComponentInParent<PlayerInputReader>();
            if (reader != null)
            {
                _nearbyPlayer = reader;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!CityX.Character.InteractionReach.IsBodyLeaving(other))
            {
                return;   // a limb's collider swinging out of the trigger is not the player walking away
            }
            var reader = other.GetComponentInParent<PlayerInputReader>();
            if (reader != null && reader == _nearbyPlayer)
            {
                _nearbyPlayer = null;
            }
        }
    }
}
