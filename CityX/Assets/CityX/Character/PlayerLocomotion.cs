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

        /// <summary>
        /// How many times higher than an ordinary jump a FULLY charged
        /// release goes -- Saints Row 4's own charge jump, not ported from
        /// anywhere in player.gd (there is no equivalent there). An explicit
        /// first test value the user asked for, not yet tuned against
        /// anything, and meant to move once this is a real skill rather than
        /// a flat constant.
        /// </summary>
        [Tooltip("How many times higher than an ordinary jump a full charge-release goes.")]
        public float ChargedJumpHeightMultiplier = 15.0f;

        [Tooltip("player.gd GRAVITY. Deliberately gentler than Earth's 9.81.")]
        public float Gravity = 9.8f;

        [Tooltip("How fast the body turns to face where it is going, in degrees per second.")]
        public float TurnDegreesPerSecond = 720.0f;

        /// <summary>
        /// How quickly horizontal speed catches up to its target, in
        /// metres per second squared -- not ported from player.gd, which
        /// has none (WASD there is the same instant direction*speed snap
        /// this file's own previous version carried over verbatim). An
        /// explicit smoothness ask, same footing as ChargedJumpHeightMultiplier:
        /// a real feel change, not a faithfulness one.
        ///
        /// FAST ENOUGH TO STAY INVISIBLE ON A STOPWATCH, not slow enough to
        /// read as sluggish -- reaching SprintSpeed (7.5) from a dead stop
        /// takes 7.5/60 = 0.125s at this value, well under the existing
        /// speed tests' own measurement windows (WalkingCoversGroundAtTheStatedSpeed
        /// averages over 2 real seconds; the ramp costs a few percent of
        /// that at most, comfortably inside their own tolerance). What it
        /// actually fixes is the abrupt teleport-to-speed snap on the
        /// first frame of any new direction -- starting from a stand,
        /// stopping dead, or reversing all used to hit full speed the very
        /// same frame the key changed, which reads as weightless rather
        /// than as a body with any real momentum.
        /// </summary>
        [Tooltip("How quickly horizontal speed catches up to its target, in metres per second squared.")]
        public float HorizontalAcceleration = 60.0f;

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
        private Transform _hips;
        private Transform _spine;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _handL;
        private Transform _handR;
        private Transform _rightUpperArm;
        private float _verticalVelocity;

        /// <summary>
        /// True from the moment a car's entering_car clip starts until
        /// exiting_car finishes -- set by PlayerInputReader's own
        /// BeginCarTransition/EndCarTransition, called by Vehicles/Car.cs.
        /// Ported from character.gd's own special-casing of exactly these
        /// two clips (see LateUpdate's own comment on why hips/spine are
        /// normally frozen every frame): entering_car/exiting_car are the
        /// only two clips in this whole rig with a real hips POSITION+
        /// ROTATION track, and freezing hips to identity while either plays
        /// -- or while frozen on entering_car's own held last frame for the
        /// whole rest of the drive, which IS the seated driving pose --
        /// would flatten out the very seat-entry lean/twist they exist to
        /// show. The moment this flips back to false, the very next
        /// LateUpdate already re-freezes hips/spine on its own -- the same
        /// end result character.gd's own one-time reset-on-finish achieves,
        /// with no separate "reset now" call needed.
        /// </summary>
        public bool InCarSeat;

        /// <summary>
        /// First-pass guess, not yet checked against a real hands-on-a-
        /// wheel pose -- same footing as character.gd's own identically
        /// named DRIVING_HAND_TURN_DEGREES, whose own doc comment already
        /// calls this "meant to be corrected the same hands-on way [the
        /// sword/gun grips] were, once actually seen turning in-game."
        /// Ported at the same 30 degrees since nothing about Unity's own
        /// Humanoid retargeting gives a reason to guess differently yet.
        /// </summary>
        public const float DrivingHandTurnDegrees = 30.0f;

        private bool _drivingWheelActive;
        private float _drivingWheelAngle;
        private float _drivingWheelMaxAngle = 1.0f;
        private bool _drivingHandBaseCaptured;
        private Quaternion _drivingHandBaseL = Quaternion.identity;
        private Quaternion _drivingHandBaseR = Quaternion.identity;
        private float _appliedHeight = -1.0f;

        /// <summary>
        /// How far the right arm lifts at full cast, in degrees around the
        /// arm's own local Z (Vector3.forward) -- NOT local X, which turned
        /// out to be this rig's own twist/roll axis: an equal-magnitude
        /// rotation there left the hand's own WORLD POSITION almost
        /// unchanged (checked directly, since a static Edit Mode render
        /// cannot show a skinned mesh's pose at all -- Unity only refreshes
        /// skin buffers on a real player-loop frame tick, which a
        /// synchronous out-of-band Camera.Render() call outside Play Mode
        /// never gets). The AXIS was picked by comparing six axis/sign
        /// candidates at 80 degrees against hand_r's own real-Play-Mode
        /// world position -- +forward was the clear winner (hand moves
        /// mostly up, with a natural outward swing and only a slight
        /// forward lean, unlike every other candidate, which crossed the
        /// hand over the body instead). That same real-pose measurement
        /// showed 80 degrees only lifting the hand ~0.21m from its resting,
        /// arm-hanging height -- a bit subtle for "raises his right hand" --
        /// so the MAGNITUDE here was then raised past that to 130, verified
        /// against PlayerLocomotionCastPosePlayTests' own >0.3m threshold.
        /// </summary>
        public const float CastRaiseDegrees = 130.0f;

        private float _castPoseFraction;

        /// <summary>The smoothed horizontal velocity HorizontalAcceleration eases toward its target -- see that field's own doc comment.</summary>
        private Vector3 _horizontalVelocity;

        /// <summary>
        /// Extra horizontal velocity riding on top of whatever WASD/
        /// HorizontalAcceleration computes -- real carried momentum from
        /// something that briefly owned the whole movement frame itself,
        /// currently only GrappleSwing's own Release (see
        /// SetResidualVelocity's own doc comment). Kept as its OWN field
        /// rather than folded into _horizontalVelocity because
        /// _horizontalVelocity is rewritten from scratch every Tick by the
        /// WASD blend above -- with no input that blend eases it to zero
        /// within a quarter second (60 m/s^2), which is fine for ordinary
        /// ground/air control but is exactly the "let go of the rope and
        /// instantly stop" bug this exists to fix. Left untouched by that
        /// blend entirely; cleared only on landing, so it behaves like real
        /// momentum for as long as the body is actually in the air.
        /// </summary>
        private Vector3 _residualVelocity;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveXParameter = Animator.StringToHash("MoveX");
        private static readonly int MoveZParameter = Animator.StringToHash("MoveZ");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int CrouchingParameter = Animator.StringToHash("Crouching");
        private static readonly int DashParameter = Animator.StringToHash("Dash");
        private static readonly int JumpParameter = Animator.StringToHash("Jump");
        private static readonly int ChargingJumpParameter = Animator.StringToHash("ChargingJump");
        private static readonly int AimingParameter = Animator.StringToHash("Aiming");
        private static readonly int SwordEquippedParameter = Animator.StringToHash("SwordEquipped");
        private static readonly int JetpackThrustingParameter = Animator.StringToHash("JetpackThrusting");
        private static readonly int DancingParameter = Animator.StringToHash("Dancing");
        private static readonly int BreakdancingParameter = Animator.StringToHash("Breakdancing");

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
        /// <summary>
        /// Hands the body's own persistent vertical velocity to whatever a
        /// caller just computed, without touching anything else about how
        /// the next Tick runs.
        ///
        /// The one thing GrappleSwing's release needs from here: its own
        /// upward release kick is worth carrying into ordinary falling the
        /// same way a jump's own upward velocity already does, since both
        /// end up decaying under this same Gravity every Tick after. See
        /// <see cref="SetResidualVelocity"/> for the horizontal half of the
        /// same release.
        /// </summary>
        public void SetVerticalVelocity(float verticalVelocity)
        {
            _verticalVelocity = verticalVelocity;
        }

        /// <summary>
        /// Adds a burst of horizontal momentum that persists untouched
        /// until the body actually lands -- the horizontal half of a
        /// GrappleSwing release. An EARLIER version of this file deliberately
        /// did not do this, on the reasoning that WASD air control already
        /// overwrites horizontal velocity from scratch every Tick (jump
        /// included), so there was "nothing here for it to carry into" --
        /// that was a real design choice, not an oversight, but it made
        /// letting go of the rope feel like hitting a wall: the body kept
        /// its vertical arc but its horizontal flight died within a quarter
        /// second (HorizontalAcceleration's own 60 m/s^2 easing toward a
        /// stationary target). Explicitly asked to change: a released swing
        /// should keep flying at the speed and direction it let go at, the
        /// same way a real body's momentum would, not snap back to a dead
        /// stop the instant WASD stops actively pushing it. See
        /// _residualVelocity's own doc comment for why this rides on top of
        /// the ordinary WASD blend rather than replacing it.
        /// </summary>
        public void SetResidualVelocity(Vector3 horizontalVelocity)
        {
            horizontalVelocity.y = 0.0f;
            _residualVelocity = horizontalVelocity;
        }

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
            _hips = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            _spine = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            _leftFoot = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            _rightFoot = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            _handL = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            _handR = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            _rightUpperArm = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            ApplyHeight();
        }

        /// <summary>
        /// FREEZES THE HIPS AND SPINE BONES' OWN ROTATION EVERY FRAME --
        /// character.gd's retarget() hard-ports the exact same rule for
        /// hips: every walk/run/punch clip is retargeted with the hips
        /// bone's calibrated global basis forced to a bare identity (see
        /// its own "our game's own walk/run/punch animations already
        /// follow this convention" comment), so the raw Mixamo mocap's
        /// natural pelvis sway/roll never reaches the skeleton at all --
        /// body facing comes entirely from this GameObject's own
        /// transform, never from an animation track. Unity's Humanoid
        /// import has no equivalent per-clip "drop this bone's track"
        /// switch, so the retargeted pose kept that sway on the actual
        /// Hips joint, which read as the reported "butt wiggles side to
        /// side."
        ///
        /// SPINE JOINED IT AFTER FREEZING HIPS ALONE MOVED THE WADDLE UP,
        /// NOT OUT. Godot only special-cases hips in its own retargeter;
        /// it does not need to touch spine because Godot's own bone-by-bone
        /// FK composition apparently keeps the source mocap's spine sway
        /// small enough not to read as a wobble. Unity's muscle-space
        /// retargeting of the identical Mixamo curves onto this rig's very
        /// different proportions (see BodyRig's own Mixamo-vs-native height
        /// mismatch) does not carry that same guarantee, confirmed live:
        /// with only hips frozen, the reported wiggle just moved to "the
        /// top of the torso." Matches this project's own stride-exaggeration
        /// precedent (PlayerRigBuilder.ExaggerateStride) of the same lesson
        /// -- proportion mismatches between the two retargeting pipelines
        /// do not fix themselves just because the source clip is identical.
        ///
        /// Reset AFTER the Animator evaluates the frame's pose (LateUpdate
        /// runs after the Animation phase), so this overwrites whatever the
        /// clip just wrote rather than being overwritten by it. Position is
        /// untouched -- only rotation carried the sway; nothing here should
        /// fight a genuine hips-height clip like standing_to_crouch, and
        /// nothing here touches the neck/head, which still turn freely.
        ///
        /// Automatically stops the instant Ragdoll disables this component
        /// (see Ragdoll.cs's own "_locomotion.enabled = false") -- Unity
        /// never calls LateUpdate on a disabled MonoBehaviour, so a limp
        /// body's hips and spine stay free for physics to actually throw
        /// around.
        /// </summary>
        private void LateUpdate()
        {
            if (!InCarSeat)
            {
                if (_hips != null)
                {
                    _hips.localRotation = Quaternion.identity;
                }
                if (_spine != null)
                {
                    _spine.localRotation = Quaternion.identity;
                }
                ClampFeetAboveGround();
            }
            ApplyDrivingWheelPose();
            ApplyCastingPose();
        }

        /// <summary>
        /// Called every physics tick by Vehicles/Car.cs while a real driver
        /// is seated -- mirrors character.gd's own set_driving_wheel_angle,
        /// down to the units (angle/maxAngle are the SAME radians Car's own
        /// wheel-steer angle already uses).
        ///
        /// DEFERRED TO LateUpdate RATHER THAN APPLIED HERE DIRECTLY. Car.cs
        /// calls this from its own FixedUpdate, which runs BEFORE the
        /// Animator evaluates the frame's pose -- writing the bone rotation
        /// at call time would just be overwritten the instant the Animator
        /// runs, the same reason this file's own hips/spine freeze already
        /// has to live in LateUpdate rather than wherever else it might be
        /// convenient to write.
        /// </summary>
        public void SetDrivingWheelAngle(float angle, float maxAngle)
        {
            _drivingWheelActive = true;
            _drivingWheelAngle = angle;
            _drivingWheelMaxAngle = maxAngle;
        }

        /// <summary>
        /// Called once when a drive ends (Vehicles/Car.cs's own
        /// _complete_exit) so the NEXT drive re-captures a fresh base pose
        /// instead of layering onto whatever roll offset the last drive
        /// left behind -- matches character.gd's own clear_driving_wheel_
        /// pose exactly.
        /// </summary>
        public void ClearDrivingWheelPose()
        {
            _drivingWheelActive = false;
            _drivingHandBaseCaptured = false;
        }

        /// <summary>
        /// Sets/clears the Animator triggers PlayerRigBuilder's own
        /// AddCarSeatStates wires up -- see that method's own comment for
        /// why these are Triggers, not Bools (auto-consumed, so nothing
        /// here has to remember to clear them again).
        /// </summary>
        public void PlayEnterCarAnimation()
        {
            _animator?.SetTrigger("EnteringCar");
        }

        public void PlayExitCarAnimation()
        {
            _animator?.SetTrigger("ExitingCar");
        }

        /// <summary>
        /// Polled every frame by Vehicles/Car.cs while a seat transition is
        /// in progress -- the closest Unity equivalent to Godot's own
        /// anim_player.animation_finished signal, matching PlayerClimbing.
        /// TickTopOut's own established shape for the exact same problem.
        /// True immediately if there is no animator at all, so a body with
        /// no rig never blocks a transition forever waiting on a clip that
        /// can never play.
        /// </summary>
        public bool IsCarAnimatorStateFinished(string stateName)
        {
            if (_animator == null)
            {
                return true;
            }
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(stateName) && info.normalizedTime >= 1.0f;
        }

        /// <summary>
        /// Layers a small additive roll onto each hand bone's own captured
        /// base pose, scaled by how far the wheel is currently turned --
        /// ported from character.gd's own set_driving_wheel_angle. The base
        /// pose is captured once per drive (entering_car is a one-shot
        /// clip frozen on its own last frame for the whole drive, so the
        /// base never moves again until ClearDrivingWheelPose resets it for
        /// the next one), then this rotates FROM that one fixed reference
        /// every frame instead of compounding drift onto whatever the
        /// previous frame already left the bone at.
        /// </summary>
        private void ApplyDrivingWheelPose()
        {
            if (!_drivingWheelActive || _handL == null || _handR == null)
            {
                return;
            }
            if (!_drivingHandBaseCaptured)
            {
                _drivingHandBaseL = _handL.localRotation;
                _drivingHandBaseR = _handR.localRotation;
                _drivingHandBaseCaptured = true;
            }
            float fraction = _drivingWheelMaxAngle > 0.0001f
                ? Mathf.Clamp(_drivingWheelAngle / _drivingWheelMaxAngle, -1.0f, 1.0f)
                : 0.0f;
            Quaternion offset = Quaternion.AngleAxis(DrivingHandTurnDegrees * fraction, Vector3.right);
            _handL.localRotation = _drivingHandBaseL * offset;
            _handR.localRotation = _drivingHandBaseR * offset;
        }

        /// <summary>
        /// Called every frame by PlayerInputReader with FireballCaster's own
        /// CastFraction -- the explicit ask was "an animation where he
        /// raises his right hand" for the fireball spell, so this layers an
        /// additive raise onto the RightUpperArm bone rather than swapping
        /// in a whole separate Animator state the way gun-aiming's own
        /// pistol_aim_in does, matching ApplyDrivingWheelPose's own
        /// procedural-pose shape for the same "no real animation clip fits"
        /// reason.
        /// </summary>
        public void SetCastPose(float fraction)
        {
            _castPoseFraction = Mathf.Clamp01(fraction);
        }

        /// <summary>
        /// Reads the arm's rotation FRESH every frame rather than capturing
        /// a one-time base the way ApplyDrivingWheelPose does -- a cast can
        /// happen mid-walk or mid-run, and the raise is meant to layer on
        /// top of whatever the current locomotion pose is doing with that
        /// arm, not freeze it to a snapshot from the moment the cast began.
        /// </summary>
        private void ApplyCastingPose()
        {
            if (_rightUpperArm == null || _castPoseFraction <= 0.0f)
            {
                return;
            }
            Quaternion offset = Quaternion.AngleAxis(CastRaiseDegrees * _castPoseFraction, Vector3.forward);
            _rightUpperArm.localRotation = _rightUpperArm.localRotation * offset;
        }

        /// <summary>
        /// THE GENERAL FLOOR CLAMP -- confirmed live on charged_jump: its
        /// hips carries a real, large local position drop (measured at
        /// roughly -0.31m on this rig, on top of an already-crouched pose),
        /// which Humanoid retargeting inherited faithfully from the source
        /// FBX's own proportions but which lands well BELOW ground once
        /// mapped onto this rig's own taller legs -- confirmed by sampling
        /// the real feet bones mid-charge and finding them over a metre
        /// under the floor. That is a per-clip calibration problem, and
        /// there is no reason to assume charged_jump is the only clip that
        /// will ever have it (a user-supplied FBX, unlike the vetted Mixamo
        /// set everything else in this rig retargets from).
        ///
        /// Rather than hand-tune each offending clip's own position curves
        /// one at a time as they turn up, this is the same fix applied
        /// generally: after the Animator has evaluated the frame's pose,
        /// if the lower of the two feet bones would sit below the
        /// CharacterController's own ground contact (see ApplyHeight's own
        /// centre/height maths for why transform.position.y IS that contact
        /// point), lift the WHOLE VISUAL RIG -- not the collider, and not
        /// any individual bone -- by exactly enough to put it back on the
        /// surface. Moving the rig's own root rather than clamping a bone
        /// preserves the pose's internal shape exactly (a crouch still
        /// looks like a crouch, just standing on the ground instead of
        /// through it) instead of distorting the leg chain by forcing one
        /// joint's position independently of the others.
        /// </summary>
        private void ClampFeetAboveGround()
        {
            if (_animator == null || _leftFoot == null || _rightFoot == null)
            {
                return;
            }
            _animator.transform.localPosition = Vector3.zero;
            float lowestFootY = Mathf.Min(_leftFoot.position.y, _rightFoot.position.y);
            float penetration = transform.position.y - lowestFootY;
            if (penetration > 0.0f)
            {
                _animator.transform.localPosition = new Vector3(0.0f, penetration, 0.0f);
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

            // JETPACK BOOSTING REACHES THE SAME SPEED AS SPRINT, but through
            // its own flag rather than folding into `sprinting` above -- see
            // LocomotionInput.JetpackBoosting's own doc comment for why: no
            // stamina cost, and no forward-only restriction either.
            float speed = (sprinting || input.JetpackBoosting) ? SprintSpeed : WalkSpeed;
            Vector3 targetHorizontal = direction * speed;
            Vector3 horizontal;

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
                // Kept in sync with the dash's own instant speed, not left
                // wherever it was before the dash started -- otherwise the
                // very first post-dash frame would ease FROM whatever
                // ordinary walk speed was already smoothed to, undoing the
                // dash's own burst for one frame instead of decelerating
                // out of it. See HorizontalAcceleration's own doc comment
                // for why this whole smoothing exists in the first place.
                _horizontalVelocity = horizontal;
            }
            else
            {
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity, targetHorizontal, HorizontalAcceleration * deltaTime);
                horizontal = _horizontalVelocity;
            }

            CurrentSpeed = horizontal.magnitude;

            bool grounded = _controller.isGrounded;

            // Landing is what "spends" carried momentum -- the moment the
            // body actually touches back down, ordinary ground movement
            // takes back over cleanly rather than leaving a permanent
            // speed boost layered underneath every future WASD input. See
            // SetResidualVelocity's own doc comment for where this comes
            // from in the first place.
            if (grounded)
            {
                _residualVelocity = Vector3.zero;
            }

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

            // THE CHARGED RELEASE. Height is proportional to the SQUARE of
            // launch velocity (h = v^2 / 2g), so reaching
            // ChargedJumpHeightMultiplier times the height only takes its
            // SQUARE ROOT times the velocity, not the multiplier itself.
            // Scales continuously with JumpChargeFraction rather than being
            // a flat "charged or not" flag -- a near-instant release reports
            // a fraction near 0, which this already resolves to an ordinary
            // JumpVelocity, so a quick tap and a full hold are one
            // mechanic, not two.
            //
            // THE TRIGGER FIRES EVEN WHEN NOT GROUNDED, deliberately split
            // from the velocity boost below it. PlayerInputReader's own
            // TickJumpCharge still reports JumpReleased when a charge is
            // cancelled by the ground disappearing out from under it (walking
            // off an edge mid-charge), purely so the animator has a way back
            // out of the ChargedJump pose -- the same Jump trigger every
            // ordinary jump already uses to leave any other state. Only the
            // velocity actually pays out on solid ground; a charge that
            // walked off an edge should not also grant a boost once it is
            // already falling.
            if (input.JumpReleased)
            {
                if (grounded)
                {
                    float heightMultiplier = Mathf.Lerp(
                        1.0f, ChargedJumpHeightMultiplier, Mathf.Clamp01(input.JumpChargeFraction));
                    _verticalVelocity = JumpVelocity * Mathf.Sqrt(heightMultiplier);
                }
                if (_animator != null)
                {
                    _animator.SetTrigger(JumpParameter);
                }
            }

            // _residualVelocity rides on top of the WASD-driven horizontal
            // speed rather than replacing it -- carried momentum from a
            // released swing plus fresh air-control input should add, the
            // same way a real body's own inertia would layer under
            // whatever new push it is given rather than being overridden
            // by it.
            Vector3 motion = horizontal + _residualVelocity;
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
                _animator.SetBool(ChargingJumpParameter, input.ChargingJump);
                _animator.SetBool(AimingParameter, input.Aiming);
                _animator.SetBool(SwordEquippedParameter, input.SwordEquipped);
                _animator.SetBool(JetpackThrustingParameter, input.JetpackThrusting);
                _animator.SetBool(DancingParameter, input.Dancing);
                _animator.SetBool(BreakdancingParameter, input.Breakdancing);
            }
        }
    }
}
