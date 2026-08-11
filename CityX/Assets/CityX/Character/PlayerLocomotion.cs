using UnityEngine;
using UnityEngine.InputSystem;

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
    /// health, no networking. Those come back when there is a reason for them
    /// beyond "the old one had it".
    ///
    /// CharacterController rather than Rigidbody, matching Godot's
    /// CharacterBody3D: both are kinematic, both resolve collisions by sliding,
    /// and both leave gravity to the caller -- so the ported numbers mean the
    /// same thing here that they meant there.
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

        [Tooltip("player.gd GRAVITY. Note this is much gentler than Earth's 9.81 by design.")]
        public float Gravity = 9.8f;

        [Tooltip("How fast the body turns to face the direction it is moving, in degrees per second.")]
        public float TurnDegreesPerSecond = 720.0f;

        [Header("Scale")]
        [Tooltip(
            "Standing height in metres. 2.2 matches the Godot build's own body geometry " +
            "(fused_body.gd puts shoulders at 1.9m with bounds to 2.35m). The Mixamo skeleton " +
            "the animations came from is 1.78m, so try that for comparison -- this is the one " +
            "number that decides whether the city feels right.")]
        public float StandingHeight = 2.2f;

        private CharacterController _controller;
        private Animator _animator;
        private Transform _cameraTransform;
        private float _verticalVelocity;
        private float _appliedHeight = -1.0f;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int JumpParameter = Animator.StringToHash("Jump");

        /// <summary>Horizontal speed this frame, in metres per second.</summary>
        public float CurrentSpeed { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            ApplyHeight();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _controller != null)
            {
                ApplyHeight();
            }
        }

        /// <summary>
        /// Resizes the capsule and the visible body together.
        ///
        /// One method for both, because the interesting experiment is changing
        /// StandingHeight and running around again -- and a capsule that no
        /// longer matches the model is a subtly wrong answer rather than an
        /// obviously broken one.
        /// </summary>
        private void ApplyHeight()
        {
            if (Mathf.Approximately(_appliedHeight, StandingHeight))
            {
                return;
            }
            _appliedHeight = StandingHeight;

            _controller.height = StandingHeight;
            _controller.radius = Mathf.Min(0.35f * (StandingHeight / 1.78f), StandingHeight * 0.3f);
            _controller.center = new Vector3(0.0f, StandingHeight * 0.5f, 0.0f);
            // A step this size lets the body walk up a kerb without a jump, and
            // is well under a terrace step, so terraces still have to be gone
            // round or climbed.
            _controller.stepOffset = Mathf.Min(0.4f, StandingHeight * 0.25f);

            if (_animator != null)
            {
                // The Mixamo rig is 1.78m tall; scale it to whatever height is
                // being tested.
                float scale = StandingHeight / 1.78f;
                _animator.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        private void Start()
        {
            _cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void Update()
        {
            ApplyHeight();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1.0f;
            if (keyboard.sKey.isPressed) input.y -= 1.0f;
            if (keyboard.dKey.isPressed) input.x += 1.0f;
            if (keyboard.aKey.isPressed) input.x -= 1.0f;
            if (input.sqrMagnitude > 1.0f)
            {
                input.Normalize();
            }

            // Movement is relative to where the camera is looking, which is what
            // every third-person game does and what player.gd did.
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (_cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            }

            Vector3 wish = forward * input.y + right * input.x;
            bool sprinting = keyboard.leftShiftKey.isPressed && input.y > 0.0f;
            float speed = sprinting ? SprintSpeed : WalkSpeed;
            Vector3 horizontal = wish * speed;
            CurrentSpeed = horizontal.magnitude;

            if (_controller.isGrounded)
            {
                // Slightly negative rather than zero: exactly zero lets the
                // controller drift off the ground on the frame after a step and
                // report not-grounded, which makes jumping unreliable.
                _verticalVelocity = -2.0f;

                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    _verticalVelocity = JumpVelocity;
                    if (_animator != null)
                    {
                        _animator.SetTrigger(JumpParameter);
                    }
                }
            }
            else
            {
                _verticalVelocity -= Gravity * Time.deltaTime;
            }

            Vector3 motion = horizontal;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            if (horizontal.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, TurnDegreesPerSecond * Time.deltaTime);
            }

            if (_animator != null)
            {
                _animator.SetFloat(SpeedParameter, CurrentSpeed);
                _animator.SetBool(GroundedParameter, _controller.isGrounded);
            }
        }
    }
}
