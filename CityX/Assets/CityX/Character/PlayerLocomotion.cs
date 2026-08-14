using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Walk, sprint and jump, ported from the Godot build's player.gd.
    ///
    /// WHAT THIS IS FOR, WHICH SHAPES WHAT IS MISSING. It exists so the city
    /// can be judged at human scale -- a flycam at 24 m/s tells you nothing
    /// about whether a 14m street feels narrow. So it carries the Godot build's
    /// movement NUMBERS exactly, because those are what decide the feel, and
    /// deliberately none of its systems: no climbing, no crouch, no combat, no
    /// health. Those come back when there is a reason beyond "the old one had
    /// it".
    ///
    /// CharacterController rather than Rigidbody, matching Godot's
    /// CharacterBody3D: both are kinematic, both resolve collisions by sliding,
    /// and both leave gravity to the caller -- so the ported numbers mean the
    /// same thing here that they meant there.
    ///
    /// It takes a <see cref="LocomotionInput"/> rather than reading the
    /// keyboard, so it can be driven by a test and, later, by a replicated
    /// network input without changing.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        [Header("Movement (ported from player.gd)")]
        [Tooltip("player.gd SPEED")]
        public float WalkSpeed = 4.0f;

        [Tooltip("player.gd SPRINT_SPEED")]
        public float SprintSpeed = 7.5f;

        [Tooltip("player.gd JUMP_VELOCITY")]
        public float JumpVelocity = 4.5f;

        [Tooltip("player.gd GRAVITY. Deliberately gentler than Earth's 9.81.")]
        public float Gravity = 9.8f;

        [Tooltip("How fast the body turns to face where it is going, in degrees per second.")]
        public float TurnDegreesPerSecond = 720.0f;

        [Header("Scale")]
        [Tooltip(
            "Standing height in metres. The default is the CITY X body's own measured height, " +
            "floor to the crown of the head sphere. Change it to scale the whole character -- " +
            "this is the one number that decides whether the city feels right.")]
        public float StandingHeight = StandingHeightDefault;

        /// <summary>
        /// The body's height, from the floor to the crown of the head sphere.
        ///
        /// DERIVED FROM THE RIG rather than typed. It was 2.2 while the body
        /// was being fitted to an imported skeleton; the real CITY X figure
        /// measures its own height out of its own joint coordinates, and this
        /// follows it so the capsule can never drift from the body inside it.
        /// </summary>
        public static readonly float StandingHeightDefault = BodyRig.StandingHeight;

        private CharacterController _controller;
        private Animator _animator;
        private float _verticalVelocity;
        private float _appliedHeight = -1.0f;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveXParameter = Animator.StringToHash("MoveX");
        private static readonly int MoveZParameter = Animator.StringToHash("MoveZ");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int CrouchingParameter = Animator.StringToHash("Crouching");
        private static readonly int DashParameter = Animator.StringToHash("Dash");
        private static readonly int JumpParameter = Animator.StringToHash("Jump");

        /// <summary>Horizontal speed after the last tick, in metres per second.</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>
        /// Where the body is travelling RELATIVE TO ITS OWN FACING, in metres
        /// per second: x across, y along. Strafing right at walking pace is
        /// (4, 0); backing up is (0, -4).
        ///
        /// This is the whole point of separating facing from travel. While the
        /// two were the same thing this was always (0, speed) and the walk_left,
        /// walk_right and walk_backward clips could never be reached.
        /// </summary>
        public Vector2 LocalVelocity { get; private set; }

        /// <summary>
        /// The full motion vector from the last step, in metres per second.
        ///
        /// Kept because a ragdoll needs it: a body that dies mid-sprint and
        /// drops straight down reads as the game hitching and a corpse
        /// appearing, whereas the same body carrying its momentum reads as
        /// having been killed while running. CurrentSpeed alone cannot say
        /// which way it was going.
        /// </summary>
        public Vector3 Velocity { get; private set; }

        public bool IsGrounded => _controller != null && _controller.isGrounded;

        /// <summary>
        /// Whether the last step was actually a sprint -- which is narrower
        /// than "the sprint key is down", because sprinting only applies going
        /// forward. Exposed so the footstep audio can pick the run loop by the
        /// same rule the animator picks the run clip, rather than by a second
        /// copy of the rule that can drift out of step with it.
        /// </summary>
        public bool IsSprinting { get; private set; }

        /// <summary>Whether the body is actually crouched, sprint override applied.</summary>
        public bool IsCrouching { get; private set; }

        // --- the dash. player.gd's own numbers.

        /// <summary>
        /// Faster than a sprint, deliberately. A dash should read as a burst
        /// rather than as "sprint, briefly". Godot doubled this from 12 rather
        /// than doubling the duration, so covering twice the ground still reads
        /// as a quicker lunge instead of a longer boost.
        /// </summary>
        public const float DashSpeed = 24.0f;

        /// <summary>
        /// Short enough to read as an instant lunge. 24 x 0.2 covers 4.8m.
        /// </summary>
        public const float DashSeconds = 0.2f;

        /// <summary>Godot's 25% of the FULL bar, not of whatever is left in it.</summary>
        public const float DashStaminaFraction = 0.25f;

        private float _dashRemaining;
        private Vector3 _dashDirection;

        public bool IsDashing => _dashRemaining > 0.0f;

        /// <summary>
        /// Sends the body into a dash along <paramref name="direction"/>.
        ///
        /// THE DIRECTION IS LOCKED IN HERE rather than read live each step.
        /// That is what makes a dash a committed lunge: turning mid-burst
        /// would let it be steered, which is a different move.
        /// </summary>
        public void StartDash(Vector3 direction)
        {
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            _dashDirection = direction.normalized;
            _dashRemaining = DashSeconds;
            if (_animator != null)
            {
                _animator.SetTrigger(DashParameter);
            }
        }

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Finds the controller and the animator.
        ///
        /// PUBLIC BECAUSE AWAKE IS NOT AVAILABLE OUTSIDE PLAY MODE. An EditMode
        /// test can build this component but never gets Awake run on it, so
        /// without a way in by hand the movement rules -- the things that
        /// decide how the game feels -- would be the one part with no coverage.
        /// Calling it twice is harmless.
        /// </summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            ApplyHeight();
        }

        /// <summary>
        /// Resizes the capsule and the visible body together.
        ///
        /// One method for both, because the interesting experiment is changing
        /// StandingHeight and running around again -- and a capsule that no
        /// longer matches the model is a subtly wrong answer rather than an
        /// obviously broken one.
        /// </summary>
        public void ApplyHeight()
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }
            if (Mathf.Approximately(_appliedHeight, StandingHeight))
            {
                return;
            }
            _appliedHeight = StandingHeight;

            _controller.height = StandingHeight;
            // From the body's own widest point -- the torso capsule -- rather
            // than a fraction of an imported rig's height. A capsule narrower
            // than the shoulders lets them clip into walls.
            _controller.radius = Mathf.Min(
                FusedBodyShape.TorsoRadius * (StandingHeight / BodyRig.StandingHeight),
                StandingHeight * 0.3f);
            _controller.center = new Vector3(0.0f, StandingHeight * 0.5f, 0.0f);
            // Big enough to walk up a kerb without jumping, well under a
            // terrace step so terraces still have to be gone round.
            _controller.stepOffset = Mathf.Min(0.4f, StandingHeight * 0.25f);

            // ZERO, NOT UNITY'S DEFAULT 0.001. That default silently DISCARDS
            // any Move shorter than a millimetre, and a per-frame step is
            // exactly that once the frame rate is high enough: at 4 m/s a
            // 0.2ms frame asks for 0.0008m and gets nothing at all. The
            // character then stands still while the animation walks, on fast
            // machines only. Found by a PlayMode test where the batch-mode
            // frame rate made it happen every time.
            _controller.minMoveDistance = 0.0f;

            if (_animator != null)
            {
                // The body is BUILT at its real size now, so the only scaling
                // left is the deliberate experiment of changing StandingHeight
                // away from the rig's own height. At the default that is
                // exactly 1 and the model is untouched. It used to divide by
                // the Mixamo rig's 1.78m, which was right while the body was
                // fitted to an imported skeleton and would now shrink the
                // character to 68% -- the imported skeleton is gone.
                float scale = StandingHeight / BodyRig.StandingHeight;
                _animator.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        /// <summary>
        /// Advances the body by one step. Everything that decides how movement
        /// feels lives here and nothing reads the keyboard, so a test can drive
        /// it directly.
        /// </summary>
        public void Tick(LocomotionInput input, float deltaTime)
        {
            ApplyHeight();

            Vector3 direction = input.WorldDirection();
            bool moving = direction.sqrMagnitude > 0.0001f;

            // SPRINT ONLY APPLIES GOING FORWARD, which is player.gd's own rule
            // and not a simplification. There is no running-backward or
            // running-sideways clip, so a 7.5 m/s strafe would be shown by a
            // 4 m/s walk clip -- feet sliding across the ground, which is the
            // exact moonwalk this rig's blend thresholds already exist to
            // prevent. Godot ties the speed to the direction for the same
            // reason it ties the clip to it.
            bool forward = input.Move.y > 0.0f && input.Move.y >= Mathf.Abs(input.Move.x);
            bool sprinting = input.Sprint && moving && forward;
            IsSprinting = sprinting;

            // SPRINT WINS OVER CROUCH, from player.gd. Not a tie-break for its
            // own sake: there is no crouch-sprint clip, so holding both would
            // play a crouch walk over a body already travelling at 7.5 m/s.
            IsCrouching = input.Crouch && !input.Sprint;

            float speed = sprinting ? SprintSpeed : WalkSpeed;
            Vector3 horizontal = direction * speed;

            // THE DASH OVERRIDES WASD ENTIRELY for its burst, along the
            // direction it was started with. Placed here so everything below --
            // the animator's speed, the footsteps, the facing -- sees the real
            // travel rather than the input that is being ignored.
            //
            // SCALED BY HOW MUCH OF THE BURST THIS STEP ACTUALLY USES. Applying
            // the full speed for a step longer than the dash has left lets one
            // slow frame carry the body past where the dash should have ended:
            // a 0.2s dash on a 0.3s hitch travels half again as far. That turns
            // a frame-rate dip into extra distance, which is exactly the kind
            // of thing that makes a dodge feel unreliable.
            if (_dashRemaining > 0.0f)
            {
                float used = Mathf.Min(_dashRemaining, deltaTime);
                _dashRemaining -= used;
                horizontal = _dashDirection * DashSpeed *
                    (deltaTime > 0.0f ? used / deltaTime : 1.0f);
            }

            CurrentSpeed = horizontal.magnitude;

            bool grounded = _controller.isGrounded;

            // THE `_verticalVelocity <= 0` GUARD IS LOAD-BEARING. Resetting on
            // grounded alone eats the jump: one step at 4.5 m/s only lifts the
            // body 75mm, which is inside the controller's own ground tolerance,
            // so the very next step still reports grounded and clobbers the
            // upward velocity back to -2. The result is a jump that twitches a
            // couple of centimetres and stops. Only stick the body down when it
            // is not already going up.
            if (grounded && _verticalVelocity <= 0.0f)
            {
                // Slightly negative rather than zero: exactly zero lets the
                // controller drift off the ground the step after a stair and
                // report not-grounded, which makes jumping unreliable.
                _verticalVelocity = -2.0f;
            }
            else
            {
                _verticalVelocity -= Gravity * deltaTime;
            }

            if (grounded && input.Jump)
            {
                _verticalVelocity = JumpVelocity;
                if (_animator != null)
                {
                    _animator.SetTrigger(JumpParameter);
                }
            }

            Vector3 motion = horizontal;
            motion.y = _verticalVelocity;
            Velocity = motion;
            _controller.Move(motion * deltaTime);

            // THE BODY FACES THE CAMERA, NOT WHERE IT IS GOING.
            //
            // It used to face its own velocity, which is the obvious thing and
            // is wrong: it makes travel direction and facing the same vector,
            // so "walking backwards" cannot exist -- the body simply turns
            // round and walks forwards. Every directional clip in the folder
            // was unreachable for that one reason. player.gd instead folds the
            // camera's yaw into the body whenever it is moving, and moves
            // relative to the result, which is what LocomotionInput has always
            // described: Move is a stick reading relative to YawDegrees.
            //
            // ONLY WHILE MOVING, also from player.gd. Turning the body to the
            // camera while standing still means looking around spins the
            // character on the spot.
            //
            // One deliberate difference: player.gd's fold is instant, because
            // it is bookkeeping between a pivot and its parent rather than a
            // turn. Here it is a real turn at TurnDegreesPerSecond, so a fast
            // flick of the mouse lets the body lag the camera for a few frames
            // instead of snapping.
            if (moving)
            {
                Quaternion facing = Quaternion.Euler(0.0f, input.YawDegrees, 0.0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, facing, TurnDegreesPerSecond * deltaTime);
            }

            Vector3 local = transform.InverseTransformDirection(horizontal);
            LocalVelocity = new Vector2(local.x, local.z);

            if (_animator != null)
            {
                _animator.SetFloat(SpeedParameter, CurrentSpeed);
                _animator.SetFloat(MoveXParameter, LocalVelocity.x);
                _animator.SetFloat(MoveZParameter, LocalVelocity.y);
                _animator.SetBool(GroundedParameter, _controller.isGrounded);
                _animator.SetBool(CrouchingParameter, IsCrouching);
            }
        }
    }
}
