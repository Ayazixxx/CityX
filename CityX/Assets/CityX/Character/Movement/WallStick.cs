using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Sticky-feet wall running. Requested: "add another ability where
    /// you can walk and run up walls like the bottom of your feet are
    /// sticky." Leap at a wall while sprinting and your feet grip it, then
    /// WASD moves freely across the WHOLE surface -- up, down, and
    /// sideways -- unlike PlayerClimbing's own fixed straight-up-a-ladder
    /// grip (that one locks the two axes across the wall and only ever
    /// moves along the third).
    ///
    /// GATED ON AIRBORNE, DELIBERATELY, so it can never fire from the exact
    /// "sprint+W into a wall" gesture PlayerClimbing.TryStart already
    /// claims while grounded -- the two abilities coexist by occupying
    /// different starting conditions rather than one checking the other's
    /// state. Checked AFTER PlayerClimbing in PlayerInputReader's own
    /// dispatch: an airborne sprint+W into a wall shallow enough for
    /// Climbing's own narrower grip still climbs a ladder exactly as
    /// before; this only ever fires where Climbing's own check didn't
    /// already claim the frame (a non-forward direction, or Climbing
    /// simply not being present).
    ///
    /// OWNS THE WHOLE FRAME WHILE ACTIVE, matching PlayerClimbing/
    /// GrappleSwing's own pattern -- PlayerInputReader calls Tick here
    /// instead of its ordinary WASD-to-locomotion dispatch while
    /// <see cref="IsSticking"/> is true.
    ///
    /// STAMINA IS THE SAME POOL SPRINTING/CLIMBING DRAIN, not a third one --
    /// same PlayerVitals.TickStamina call PlayerClimbing.Tick already makes.
    ///
    /// NO NEW ANIMATION STATES. Rather than author a dedicated wall-run
    /// clip set (what PlayerClimbing did for its own ladder grip), this
    /// reuses the ordinary locomotion Speed/MoveX/MoveZ/Grounded parameters
    /// the walk/run blend tree already drives from, while the whole body
    /// is reoriented so the wall reads as the new "floor" -- the existing
    /// run cycle plays as normal, just against a vertical surface.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class WallStick : MonoBehaviour
    {
        public const float GrabCheckDistance = 1.0f;
        public const float WallMaxSlopeDot = 0.5f;
        public const float WalkSpeed = 2.5f;
        public const float RunSpeed = 4.5f;
        public const float ClingClearance = 0.55f;
        public const float HopOffOutwardSpeed = 3.5f;
        public const float HopOffUpwardSpeed = 2.0f;

        /// <summary>Same fraction PlayerClimbing.GrabOriginHeightFraction uses, for the same reason.</summary>
        public const float GrabOriginHeightFraction = 0.68f;

        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveXParameter = Animator.StringToHash("MoveX");
        private static readonly int MoveZParameter = Animator.StringToHash("MoveZ");

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        private Vector3 _wallNormal;
        private Vector3 _wallPoint;
        private GrabIntent _intent;

        public bool IsSticking { get; private set; }

        /// <summary>Exposed for tests -- the raw push EndStick would have applied, push or no push.</summary>
        public Vector3 LastEndVelocity { get; private set; }

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Finds the controller/animator/locomotion. Public because Awake
        /// never runs in EditMode -- same reasoning PlayerClimbing.Bind
        /// gives. Calling it twice is harmless.
        /// </summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        /// <summary>
        /// Checked every frame not already sticking (see PlayerInputReader's
        /// own dispatch) -- grips the instant sprint is held, the player is
        /// airborne, stamina remains, and a forward raycast finds a real
        /// wall within reach. Airborne is the deliberate trigger (a leap at
        /// the wall), matching how the ability reads: you jump at it and
        /// your feet stick, rather than it grabbing you off an ordinary walk.
        /// </summary>
        public bool TryStart(GrabIntent intent, bool sprintHeld, bool grounded, float stamina)
        {
            if (IsSticking || grounded || !sprintHeld || stamina <= 0.0f || intent == null || !intent.Ready)
            {
                return false;
            }

            // The same deliberate hold (GrabIntent) and the same "is that really a wall" check (WallProbe) the
            // ladder climb uses: leaping past a window frame or brushing a door jamb while sprinting must not
            // stick you to it.
            float standingHeight = _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;
            if (!WallProbe.TryFind(transform.position, transform.forward, standingHeight, out WallProbe.Result wall, transform))
            {
                return false;
            }
            Vector3 normal = wall.Normal;
            Vector3 point = wall.Point;

            IsSticking = true;
            _intent = intent;
            intent.Consume();
            _wallNormal = normal;
            _wallPoint = point;
            _locomotion?.SetVerticalVelocity(0.0f);
            FaceWall();
            return true;
        }

        /// <summary>
        /// Owns the whole frame while stuck. `moveInput.y` is forward/back
        /// (climbs up/down the wall), `moveInput.x` is left/right
        /// (traverses sideways along it) -- both free at once, unlike
        /// PlayerClimbing's single up/down axis. Space hops off with an
        /// outward push, matching PlayerClimbing's own hop-off; running out
        /// of stamina or running off the wall's own edge releases plainly.
        /// </summary>
        public void Tick(bool jumpPressed, Vector2 moveInput, PlayerVitals vitals, float deltaTime)
        {
            if (!IsSticking)
            {
                return;
            }

            if (jumpPressed)
            {
                EndStick(true, deltaTime);
                return;
            }

            // Same pool sprinting/climbing drains, same exhaustion latch --
            // draining unconditionally while stuck, matching
            // PlayerClimbing.Tick's own "true" argument: sticking to a wall
            // is itself the exertion, not something only sprinting adds.
            //
            // A WET WALL COSTS MORE AND SLIPS -- Breath of the Wild's rain: hands
            // and feet lose their grip, so the same hold burns stamina faster
            // and the body creeps down the wall unless it is climbing. Zero
            // effect on a dry wall or one under cover.
            float wet = CurrentWeather.SurfaceWetness(transform.position);
            bool stillHasStamina = vitals == null
                || vitals.TickStamina(true, deltaTime * Weather.WallStaminaDrain(wet));
            if (!stillHasStamina)
            {
                EndStick(false, deltaTime);
                return;
            }

            // The wall's own local "up" and "right" -- a real tangent basis
            // for the surface, so forward/back and left/right both move
            // freely across it instead of being locked to one axis the way
            // PlayerClimbing's fixed anchor deliberately is.
            Vector3 wallUp = Vector3.up - _wallNormal * Vector3.Dot(Vector3.up, _wallNormal);
            wallUp.Normalize();
            Vector3 wallRight = Vector3.Cross(_wallNormal, wallUp).normalized;

            bool moving = moveInput.sqrMagnitude > 0.0001f;
            float speed = moving ? RunSpeed : WalkSpeed;
            Vector3 tangentMove = (wallRight * moveInput.x + wallUp * moveInput.y) * speed * deltaTime
                - wallUp * (Weather.WallSlipSpeed(wet) * deltaTime);

            // Self-corrects drift off the cling distance every frame, the
            // same idiom PlayerClimbing's own `_anchorX - transform.position.x`
            // uses to lock its own perpendicular axis -- generalised here to
            // an arbitrary wall normal instead of a world axis, since a real
            // wall is not necessarily axis-aligned.
            float currentDistance = Vector3.Dot(transform.position - _wallPoint, _wallNormal);
            Vector3 clingCorrection = _wallNormal * (ClingClearance - currentDistance);

            _controller.Move(tangentMove + clingCorrection);
            FaceWall();

            if (_animator != null)
            {
                _animator.SetBool(GroundedParameter, true);
                _animator.SetFloat(SpeedParameter, moveInput.magnitude * speed);
                _animator.SetFloat(MoveXParameter, moveInput.x);
                _animator.SetFloat(MoveZParameter, moveInput.y);
            }

            // Ran off the wall's own edge -- a fresh raycast into it from
            // the current position no longer finds anything within reach,
            // meaning there is no more surface left underfoot. FaceWall
            // above already keeps transform.forward pointing INTO the wall
            // (-_wallNormal), so this is the exact same check TryStart used
            // to grab on in the first place. Released plainly (no push),
            // the same as PlayerClimbing reaching solid ground at the
            // bottom of a climb.
            float originHeight = (_locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight)
                * GrabOriginHeightFraction;
            Vector3 recheckOrigin = transform.position + Vector3.up * originHeight;
            if (!WallInDirection(recheckOrigin, transform.forward, out _, out _))
            {
                EndStick(false, deltaTime);
            }
        }

        private void FaceWall()
        {
            // Standing on the wall as its own new "floor" -- up points away
            // from the surface, forward keeps whatever facing the wall
            // normal implies (there is no meaningful "camera forward" once
            // gravity itself has been reoriented this way).
            transform.rotation = Quaternion.LookRotation(-_wallNormal, Vector3.up);
        }

        /// <summary>
        /// Releases the wall for any reason other than a deliberate hop --
        /// running out of stamina or reaching the edge push nothing; Space
        /// pushes outward and up, the same shape PlayerClimbing.EndClimb
        /// uses for its own hop-off (tests check LastEndVelocity directly).
        /// </summary>
        private void EndStick(bool applyPush, float deltaTime)
        {
            IsSticking = false;
            _intent?.BeginCooldown();
            transform.rotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized, Vector3.up);

            Vector3 push = applyPush
                ? _wallNormal * HopOffOutwardSpeed + Vector3.up * HopOffUpwardSpeed
                : Vector3.zero;
            LastEndVelocity = push;
            _locomotion?.SetVerticalVelocity(push.y);
            if (applyPush)
            {
                _controller.Move(push * deltaTime);
            }
            _wallNormal = Vector3.zero;
        }

        /// <summary>
        /// A forward raycast from origin toward direction, into a wall --
        /// same shape as PlayerClimbing.WallInDirection (world geometry
        /// only, filtered by how vertical the hit normal is), plus the hit
        /// point itself so Tick can anchor its own cling-distance
        /// correction to a fixed point on the surface.
        /// </summary>
        private static bool WallInDirection(Vector3 origin, Vector3 direction, out Vector3 normal, out Vector3 point)
        {
            normal = Vector3.zero;
            point = Vector3.zero;
            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, GrabCheckDistance,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (hit.collider.GetComponentInParent<IDamageable>() != null)
            {
                return false;
            }
            if (Mathf.Abs(hit.normal.y) > WallMaxSlopeDot)
            {
                return false;   // too close to horizontal to be a real stickable wall
            }
            normal = hit.normal;
            point = hit.point;
            return true;
        }
    }
}
