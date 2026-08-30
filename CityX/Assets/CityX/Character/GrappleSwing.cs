using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The grappling hook's swing: aim, fire, hang from a rigid rope and
    /// pump it for height. Ported from player.gd's own start_grapple/
    /// _process_grapple_swing/release_grapple, numbers included.
    ///
    /// OWNS THE WHOLE FRAME WHILE ACTIVE, matching player.gd's own
    /// _physics_process early-return into _process_grapple_swing --
    /// PlayerInputReader checks <see cref="IsActive"/> before its normal
    /// combat/movement dispatch and, while true, calls <see cref="Tick"/>
    /// here instead of PlayerLocomotion.Tick. That is what lets a rigid
    /// rope constraint and camera-relative air control replace ordinary
    /// WASD movement for exactly as long as the rope is attached.
    ///
    /// THE ROPE IS A RIGID DISTANCE CONSTRAINT, SOLVED THE STANDARD WAY,
    /// not a spring: once the player is further from the anchor than the
    /// rope is long, the outward component of velocity (the part trying to
    /// stretch the rope) is cancelled and the position is snapped onto the
    /// sphere -- leaving the tangential part untouched, which is what a
    /// pendulum swings on. A spring would need tuning to avoid sagging or
    /// oscillating and would still let the rope visibly stretch.
    ///
    /// A PERSISTENT VELOCITY FIELD OF ITS OWN, unlike PlayerLocomotion's
    /// per-frame `direction * speed`: Godot's CharacterBody3D carries one
    /// velocity across the whole body, but PlayerLocomotion recomputes
    /// horizontal velocity from WASD input fresh every call and only
    /// carries `_verticalVelocity` across frames for gravity. A rope
    /// constraint has nothing to hold onto without a real velocity that
    /// persists between ticks, so this keeps its own for the swing's
    /// duration and hands the WHOLE thing back to PlayerLocomotion on
    /// release (see <see cref="Release"/>) -- vertical through
    /// SetVerticalVelocity, horizontal through SetResidualVelocity, so a
    /// released swing keeps flying at the speed and direction it let go
    /// at instead of getting cut down to whatever WASD is doing that
    /// instant.
    ///
    /// DEATH IS POLLED, NOT EVENT-DRIVEN -- see GrabCarry's own doc comment
    /// for why an OnEnable subscription to PlayerVitals.Died is unsafe in
    /// this codebase's own scene-builder field-assignment order.
    /// PlayerInputReader checks Vitals.IsDead and calls Release(false)
    /// itself before anything else runs, matching player.gd's own _die()
    /// doing exactly that so a dead player is never left visibly tethered.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class GrappleSwing : MonoBehaviour
    {
        // --- player.gd's own GRAPPLE_* constants, unchanged. ---

        public const float MinRopeLength = 2.5f;
        public const float MaxRopeLength = 60.0f;
        public const float MaxRange = 45.0f;

        /// <summary>Air control while swinging -- what lets you PUMP a swing to build height instead of just decaying into a pendulum at rest.</summary>
        public const float SwingAccel = 16.0f;

        public const float ReelSpeed = 9.0f;
        public const float PayoutSpeed = 7.0f;

        /// <summary>Bled off per constraint-frame. Small, but without any loss a frictionless pendulum swings forever and every landing overshoots.</summary>
        public const float RopeDamping = 0.015f;

        /// <summary>Upward kick on a deliberate release, so letting go at the top of an arc converts into real height rather than immediately dropping.</summary>
        public const float ReleaseBoost = 3.5f;

        /// <summary>
        /// How long the player may stay grounded mid-swing before the rope
        /// cuts on its own. Long enough that briefly touching down while
        /// skimming a rooftop keeps the swing alive, short enough that
        /// actually landing ends it before it reads as being stuck.
        /// </summary>
        public const float MaxGroundSeconds = 0.35f;

        private static readonly int JumpParameter = Animator.StringToHash("Jump");

        /// <summary>Set by GrapplingHookItem.OnEquip/OnUnequip -- null means nothing is equipped to fire or draw a rope with.</summary>
        public IGrappleLauncher EquippedHook;

        [Tooltip("Gravity while swinging. Defaults to PlayerLocomotion's own, read once in Awake so the two never drift apart.")]
        public float Gravity = 9.8f;

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        private Vector3 _velocity;
        private Vector3 _anchor;
        private float _ropeLength;
        private float _groundTime;

        public bool IsActive { get; private set; }

        /// <summary>The swing's own persistent velocity -- see this class's own doc comment on why it keeps one separately from PlayerLocomotion.</summary>
        public Vector3 Velocity => _velocity;

        public float RopeLength => _ropeLength;

        /// <summary>Exposed for tests -- the release's own final velocity, boost included.</summary>
        public Vector3 LastReleaseVelocity { get; private set; }

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Finds the controller/animator/locomotion. Public because Awake
        /// never runs in EditMode -- see PlayerLocomotion's own Bind for the
        /// same reasoning -- and the rope's own physics (no-stretch, keeps
        /// tangential speed, momentum on release, reel clamps to minimum)
        /// is exactly the kind of thing that needs to be testable without a
        /// scene. Calling it twice is harmless.
        /// </summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _locomotion = GetComponent<PlayerLocomotion>();
            if (_locomotion != null)
            {
                Gravity = _locomotion.Gravity;
            }
        }

        /// <summary>
        /// Fires at whatever <paramref name="origin"/>/<paramref name="direction"/>
        /// (the camera, in practice) is pointing at. Returns whether it
        /// attached -- world geometry only, matching
        /// GrapplingHookItem.FindAnchor's own "not other characters" rule.
        /// </summary>
        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (EquippedHook == null || IsActive)
            {
                return false;
            }
            if (!EquippedHook.FindAnchor(origin, direction, MaxRange, out Vector3 anchor))
            {
                return false;
            }
            Attach(anchor);
            return true;
        }

        /// <summary>
        /// Attaches the rope and hands movement over to <see cref="Tick"/>.
        ///
        /// NAMED Attach, NOT Start -- Start is a reserved MonoBehaviour
        /// message; a public method of that exact name taking parameters
        /// silently produces a Unity script warning ("Start() can not take
        /// parameters") rather than a compile error, easy to miss in a
        /// batch log full of unrelated noise.
        ///
        /// THE ROPE'S LENGTH IS WHATEVER THE DISTANCE TO THE ANCHOR ALREADY
        /// IS at the moment it catches, not a fixed value -- a grapple fired
        /// at a distant rooftop swings you in a wide arc, while one fired at
        /// the wall beside you barely moves you. Clamped only so a rope can
        /// never start shorter than it can ever be reeled to.
        ///
        /// <paramref name="initialVelocity"/> defaults to whatever
        /// PlayerLocomotion's own Velocity already was the instant before
        /// firing -- carrying a run or a fall straight into the swing
        /// rather than starting it dead in the air. Overridable so a test
        /// can seed a known velocity without a PlayerLocomotion at all.
        /// </summary>
        public void Attach(Vector3 anchor, Vector3? initialVelocity = null)
        {
            _anchor = anchor;
            _ropeLength = Mathf.Clamp(
                Vector3.Distance(transform.position, anchor), MinRopeLength, MaxRopeLength);
            _velocity = initialVelocity ?? (_locomotion != null ? _locomotion.Velocity : Vector3.zero);
            IsActive = true;
            _groundTime = 0.0f;

            EquippedHook?.ShowRope(anchor);
            // The held pose for the whole swing. Reuses the same "Jump"
            // trigger PlayerLocomotion's own jump already fires: an upright,
            // non-looping airborne clip that settles on its last frame and
            // holds there, exactly what a fixed swing pose needs -- see this
            // class's own doc comment on why nothing here re-touches
            // rotation to correct a tilted pose the way player.gd had to.
            _animator?.SetTrigger(JumpParameter);
        }

        /// <summary>
        /// Lets go. <paramref name="boosted"/> adds the upward kick that
        /// makes releasing at the top of an arc worth timing; a release
        /// forced by something else (death, unequipping) passes false and
        /// just drops the rope.
        /// </summary>
        public void Release(bool boosted)
        {
            if (!IsActive)
            {
                return;
            }
            IsActive = false;
            _groundTime = 0.0f;
            if (boosted)
            {
                _velocity.y += ReleaseBoost;
            }
            LastReleaseVelocity = _velocity;
            // The WHOLE velocity carries into ordinary falling -- vertical
            // through the same channel a jump already uses, horizontal
            // through SetResidualVelocity so it survives PlayerLocomotion's
            // own WASD-blend deceleration instead of dying within a
            // quarter second of letting go (see this class's own doc
            // comment).
            _locomotion?.SetVerticalVelocity(_velocity.y);
            _locomotion?.SetResidualVelocity(_velocity);
            EquippedHook?.HideRope();
        }

        /// <summary>
        /// The whole movement frame while swinging: gravity, camera-relative
        /// air control, reel/pay out, release, then the rope constraint.
        /// </summary>
        public void Tick(Vector2 moveInput, float yawDegrees, bool boostedReleasePressed,
            bool plainReleasePressed, bool reelHeld, bool payoutHeld, float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            _velocity.y -= Gravity * deltaTime;

            Vector3 push = new LocomotionInput { Move = moveInput, YawDegrees = yawDegrees }.WorldDirection();
            if (push.sqrMagnitude > 0.0001f)
            {
                _velocity += push * SwingAccel * deltaTime;
            }

            // Reel in to climb the rope (this is how you get onto a roof);
            // pay out to drop lower without letting go.
            if (reelHeld)
            {
                _ropeLength = Mathf.Max(MinRopeLength, _ropeLength - ReelSpeed * deltaTime);
            }
            else if (payoutHeld)
            {
                _ropeLength = Mathf.Min(MaxRopeLength, _ropeLength + PayoutSpeed * deltaTime);
            }

            // Both release inputs still take one more real step below with
            // the fresh (now released, possibly boosted) velocity before
            // handing off, matching player.gd's own release_grapple() +
            // move_and_slide() pairing in this same spot -- the body should
            // not sit frozen for a frame waiting on PlayerLocomotion.
            if (boostedReleasePressed)
            {
                Release(true);
                _controller.Move(_velocity * deltaTime);
                return;
            }
            if (plainReleasePressed)
            {
                Release(false);
                _controller.Move(_velocity * deltaTime);
                return;
            }

            Vector3 toAnchor = _anchor - transform.position;
            float distance = toAnchor.magnitude;
            Vector3 correction = Vector3.zero;
            if (distance > _ropeLength && distance > 0.001f)
            {
                Vector3 direction = toAnchor / distance;
                // Velocity's own outward component, i.e. the part trying to
                // stretch the rope. Cancelled; everything perpendicular
                // (what a pendulum actually swings on) survives untouched.
                float outward = Vector3.Dot(_velocity, -direction);
                if (outward > 0.0f)
                {
                    _velocity += direction * outward;
                }
                _velocity *= 1.0f - RopeDamping;
                // The displacement needed to land exactly on the rope's
                // sphere, folded into this frame's own Move() below rather
                // than a separate direct transform.position write --
                // CharacterController does not reliably resync its own
                // internal position cache against a bare transform write
                // between Move() calls, which let the body drift further
                // from the anchor every frame instead of being pulled back
                // (confirmed directly: a 60-frame swing stretched to 31m on
                // a 9m rope before this fix).
                Vector3 target = _anchor - direction * _ropeLength;
                correction = target - transform.position;
            }

            EquippedHook?.UpdateRope(_anchor);

            _controller.Move(_velocity * deltaTime + correction);

            // Landing on your feet ends the swing on its own. TWO ways out,
            // because a speed test alone isn't enough: this function keeps
            // applying air control every frame INCLUDING while grounded, so
            // holding a movement key would hold the player above a
            // speed-only threshold indefinitely. The grounded TIMER is the
            // guaranteed escape; the speed test stays because it ends a
            // spent swing instantly instead of after a delay.
            if (_controller.isGrounded)
            {
                _groundTime += deltaTime;
            }
            else
            {
                _groundTime = 0.0f;
            }

            if (_groundTime >= MaxGroundSeconds)
            {
                Release(false);
            }
            else if (_controller.isGrounded &&
                     new Vector2(_velocity.x, _velocity.z).magnitude < 1.0f)
            {
                Release(false);
            }
        }
    }
}
