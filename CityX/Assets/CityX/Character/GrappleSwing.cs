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
    /// THE HELD POSE IS PURE PROCEDURAL BONE ROTATION, NOT PHYSICS -- see
    /// UpdateSwingPose's own doc comment. THREE earlier versions of this
    /// file tried real rigidbody/joint ragdoll physics for "the body
    /// flails while swinging" (a kinematic Chest forced through the
    /// swing's own motion; a bounded force pulling a fully dynamic rig),
    /// and every one of them was reported broken live -- stretching apart,
    /// limbs flying off, the body distorting wildly. Real constraint-based
    /// physics driven by a script that ALSO forces the character through
    /// arbitrary, fast, curving motion is fundamentally fragile in ways
    /// that are hard to fully predict or verify without live iteration
    /// (joint solving under continuous forced motion, and colliders on
    /// every ragdoll body part now able to snag on world geometry the
    /// CharacterController itself slides past). Directly rotating bones --
    /// the same technique PlayerLocomotion.ApplyCastingPose/
    /// ApplyDrivingWheelPose and Jetpack.TickTilt already use, all proven
    /// stable -- cannot explode, stretch, or get stuck on anything, because
    /// nothing is being simulated at all.
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
        /// A HARD CEILING ON THE SWING'S OWN VELOCITY -- without this,
        /// holding a movement key while also holding payout keeps the rope
        /// permanently longer than the distance to the anchor, so it never
        /// goes taut and RopeDamping/the outward-cancel below never engage.
        /// SwingAccel then adds speed every single frame with nothing ever
        /// bleeding it back off -- given enough real playtime that reaches
        /// speeds the CharacterController and camera were never built to
        /// track, which reads live as the body rocketing through the camera
        /// and vanishing: the reported "stretches the character out into
        /// being invisible" bug. Ordinary swinging never gets remotely
        /// close to this ceiling -- held taut, pushing the whole time
        /// settles to roughly SwingAccel / RopeDamping = 16/0.015 ≈ 18 m/s
        /// on its own -- so this only ever clips the exploit, not the
        /// normal feel.
        /// </summary>
        public const float MaxSwingSpeed = 35.0f;

        /// <summary>
        /// How long the player may stay grounded mid-swing before the rope
        /// cuts on its own. Long enough that briefly touching down while
        /// skimming a rooftop keeps the swing alive, short enough that
        /// actually landing ends it before it reads as being stuck.
        /// </summary>
        public const float MaxGroundSeconds = 0.35f;

        /// <summary>Set by GrapplingHookItem.OnEquip/OnUnequip -- null means nothing is equipped to fire or draw a rope with.</summary>
        public IGrappleLauncher EquippedHook;

        [Tooltip("Gravity while swinging. Defaults to PlayerLocomotion's own, read once in Awake so the two never drift apart.")]
        public float Gravity = 9.8f;

        [Tooltip("Pulled back while swinging -- see SwingCameraDistance's own doc comment. Optional: a null reference just skips the pull-back.")]
        public ThirdPersonCamera OrbitCamera;

        /// <summary>
        /// EXPLICIT BUG REPORT, confirmed by direct measurement: a real
        /// varied swing anchored on a real nearby building brought the
        /// swinging hand within 1.23m of the camera (open-air swinging with
        /// the same input only ever got it to 4.48m) -- every grapple
        /// anchor is on a wall (see ThirdPersonCamera.MinDistance's own
        /// comment), so the camera is ALSO already pulled toward its own
        /// MinDistance floor by the nearby geometry while this happens,
        /// stacking both effects. A hand or arm a little over a metre from
        /// a normal-FOV camera reads as a huge, warped, foreground shape
        /// that dominates the frame -- exactly the "stretched/contorted"
        /// screenshots this was found from, and exactly why it only ever
        /// happens near buildings. Not a pose bug at all: the reach pose
        /// itself is a normal, bounded arm extension (see
        /// RightUpperArmReachDegrees) -- the camera was simply sitting too
        /// close to begin with for that arm to ever have room to move
        /// through. Same fixed-distance-for-the-duration idiom
        /// PlayerInputReader.BeginCarTransition/EndCarTransition already
        /// uses for DrivingCameraDistance, applied here from Attach/Release
        /// instead so it covers every release path (boosted, plain,
        /// grounded-auto, max-ground-time-auto, double-tap bail) through
        /// the one place they all funnel through already.
        /// </summary>
        public const float SwingCameraDistance = 6.5f;

        private float _savedCameraDistance;

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private IFireballCaster _fireballCaster;
        private Animator _animator;

        // --- bones the swing pose touches. Head/legs/left-arm flail off
        // --- the swing's own velocity; the right arm eases toward a fixed
        // --- reach pose instead (see UpdateSwingPose's own doc comment).
        private Transform _headBone;
        private Transform _leftUpperArmBone;
        private Transform _leftLowerArmBone;
        private Transform _rightUpperArmBone;
        private Transform _rightLowerArmBone;
        private Transform _leftUpperLegBone;
        private Transform _rightUpperLegBone;

        /// <summary>
        /// Each bone's OWN local rotation the instant the Animator was
        /// switched off, captured in Attach -- the flailing bones sway
        /// ADDITIVELY on top of this (base * sway), the same "layer on top
        /// of whatever pose was already there" idiom
        /// PlayerLocomotion.ApplyCastingPose already uses, rather than
        /// assuming a fixed bind pose that a mid-stride Attach would not
        /// actually be in.
        /// </summary>
        private Quaternion _headBase = Quaternion.identity;

        private Quaternion _leftUpperArmBase = Quaternion.identity;
        private Quaternion _leftLowerArmBase = Quaternion.identity;
        private Quaternion _leftUpperLegBase = Quaternion.identity;
        private Quaternion _rightUpperLegBase = Quaternion.identity;
        private Quaternion _rightUpperArmBase = Quaternion.identity;
        private Quaternion _rightLowerArmBase = Quaternion.identity;

        /// <summary>The right arm's own CURRENT local rotation, eased toward a reach pose layered on _rightUpperArmBase/_rightLowerArmBase every frame -- see RightUpperArmReachDegrees's own doc comment for why this is relative to that captured base rather than an absolute target.</summary>
        private Quaternion _rightUpperArmCurrent = Quaternion.identity;

        private Quaternion _rightLowerArmCurrent = Quaternion.identity;

        /// <summary>The left arm's own current local rotation -- eases toward LeftUpperArmCastPose/LeftLowerArmCastPose while actually casting, or back toward its own flailing sway otherwise.</summary>
        private Quaternion _leftUpperArmCurrent = Quaternion.identity;

        private Quaternion _leftLowerArmCurrent = Quaternion.identity;

        /// <summary>The swing's own velocity, smoothed, in the BODY'S OWN local space -- what the flail actually reacts to. Smoothed rather than read raw so a single jittery frame of rope-constraint correction does not read as a limb twitch.</summary>
        private Vector3 _localVelocitySmoothed;

        private Vector3 _localVelocitySmoothVelocity;

        /// <summary>How long the local-velocity smoothing takes to settle, in seconds.</summary>
        private const float SwaySmoothTime = 0.18f;

        /// <summary>The local speed, in m/s, at which the flail reaches its own full swing -- not a hard cap on speed itself, just where the sway maxes out.</summary>
        private const float SwayReferenceSpeed = 8.0f;

        /// <summary>How fast the right/left arm eases toward its own target pose, in degrees per second.</summary>
        private const float ArmPoseDegreesPerSecond = 240.0f;

        /// <summary>
        /// How far the right arm reaches for the rope, layered ADDITIVELY
        /// on top of the pose captured at Attach (_rightUpperArmBase) --
        /// NOT an absolute fixed orientation, which is what this file
        /// originally used and which rendered as a grotesquely stretched
        /// limb (confirmed directly with GrapplingHookBuilder.CaptureSwing,
        /// a live bug report: "the body stays still and starts
        /// stretching"). This rig's skin is bound relative to whatever
        /// pose a bone is already in, the same reason
        /// PlayerLocomotion.ApplyCastingPose layers its own raise onto the
        /// CURRENT rotation instead of replacing it outright -- a fixed
        /// absolute target overwrites the Animator's own X/Y shoulder
        /// positioning entirely and can land on an orientation this rig's
        /// simple capsule-limb skin weights were never bound for, which is
        /// exactly what stretches. Z-only (Vector3.forward), the same axis
        /// CastRaiseDegrees's own doc comment already found safe on this
        /// rig -- local X there turned out to be a twist/roll axis.
        /// Positive, matching CastRaiseDegrees's own sign for the right arm
        /// (raises the hand); the left arm's own cast pose mirrors it
        /// negative, matching ApplyCastingPose's left/right sign flip.
        /// </summary>
        private const float RightUpperArmReachDegrees = 150.0f;

        private const float RightLowerArmReachDegrees = 60.0f;
        private const float LeftUpperArmCastDegrees = -150.0f;
        private const float LeftLowerArmCastDegrees = 60.0f;

        private Vector3 _velocity;
        private Vector3 _anchor;
        private float _ropeLength;
        private float _groundTime;
        private float _elapsedSeconds;

        /// <summary>
        /// THE ACTUAL BUG behind a live report ("the character stays in
        /// place... the more you move around... the more contorted he
        /// gets"): firing the grapple while standing on the ground -- an
        /// entirely normal thing to do, not an edge case -- starts the
        /// swing at whatever velocity the player already had, which for
        /// someone standing still is near zero. The grounded+low-speed
        /// auto-release below had NO grace period at all (faithfully
        /// ported from player.gd's own identical check, which never
        /// surfaced this because Godot's own swing just plays a "jump"
        /// clip throughout -- an instant no-op attach+release is nearly
        /// invisible there), so it fired on literally the FIRST tick after
        /// Attach, before the rope ever had a chance to pull anyone
        /// anywhere. Confirmed directly, live, through the real
        /// PlayerInputReader dispatch: IsActive went true then false again
        /// one frame later with zero player movement in between. Repeated
        /// firing attempts against that (a real player naturally keeps
        /// trying when a grapple visibly does nothing) toggle the Animator
        /// off and back on every couple of frames, each time capturing a
        /// fresh, never-fully-settled base pose for GrappleSwing's own
        /// relative arm poses -- which is what actually produces a
        /// creeping, worsening contortion the longer someone keeps trying,
        /// even though no single Attach/Release pair is unbounded on its
        /// own. Gives every swing a real, if brief, window to prove itself
        /// airborne before the low-speed check can end it at all -- the
        /// grounded-TIMEOUT release (MaxGroundSeconds) is untouched and
        /// still the guaranteed backstop for a swing that lands and stays
        /// down.
        /// </summary>
        public const float AttachGraceSeconds = 0.2f;

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
        /// Finds the controller/animator/locomotion/bones. Public because
        /// Awake never runs in EditMode -- see PlayerLocomotion's own Bind
        /// for the same reasoning -- and the rope's own physics (no-stretch,
        /// keeps tangential speed, momentum on release, reel clamps to
        /// minimum) is exactly the kind of thing that needs to be testable
        /// without a scene. Calling it twice is harmless.
        /// </summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _fireballCaster = GetComponent<IFireballCaster>();
            _animator = GetComponentInChildren<Animator>();
            if (_locomotion != null)
            {
                Gravity = _locomotion.Gravity;
            }

            // isHuman, NOT just != null -- GetBoneTransform throws on an
            // Animator with no Avatar (or a non-Humanoid one) rather than
            // returning null, and unlike a proper Player.prefab, a bare
            // Animator with no Avatar is exactly what a test fixture (or a
            // half-built body mid-construction) can legitimately have.
            if (_animator != null && _animator.isHuman)
            {
                _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
                _leftUpperArmBone = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _leftLowerArmBone = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                _rightUpperArmBone = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _rightLowerArmBone = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _leftUpperLegBone = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _rightUpperLegBone = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
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
            _elapsedSeconds = 0.0f;
            _localVelocitySmoothed = Vector3.zero;
            _localVelocitySmoothVelocity = Vector3.zero;

            EquippedHook?.ShowRope(anchor);

            // See SwingCameraDistance's own doc comment.
            if (OrbitCamera != null)
            {
                _savedCameraDistance = OrbitCamera.Distance;
                OrbitCamera.Distance = SwingCameraDistance;
            }

            // THE HELD POSE IS THE PROCEDURAL FLAIL FROM HERE, not the
            // Animator -- see this class's own doc comment for why. Nothing
            // else should be posing these bones while the Animator is off,
            // matching how PlayerLocomotion.ApplyHeight is the sole owner
            // of its own localScale.
            if (_animator != null)
            {
                _animator.enabled = false;
            }
            // PlayerLocomotion itself stays enabled and running throughout
            // a swing (it owns feet-clamp/hips-freeze even then), which
            // means its own LateUpdate keeps calling ApplyCastingPose every
            // frame regardless of GrappleSwing's own state -- if a ground
            // cast's raise was still fading out (CastPoseSeconds has not
            // fully decayed) right as a grapple fires, that leftover
            // fraction would fight UpdateSwingPose for the exact same arm
            // bone. Cleared here so the swing's own pose is never sharing
            // an arm with a stale ground-cast raise.
            _locomotion?.SetCastPose(0.0f);
            // See PlayerLocomotion.ClampFeetAboveGround's own doc comment --
            // that correction has no job while dangling from a rope and,
            // confirmed live, actively runs away into the thousands of
            // metres if left running during a swing's own continuous leg
            // sway.
            _locomotion?.SetGroundClampSuspended(true);
            CaptureBase(_headBone, ref _headBase);
            CaptureBase(_leftUpperArmBone, ref _leftUpperArmBase);
            CaptureBase(_leftLowerArmBone, ref _leftLowerArmBase);
            CaptureBase(_leftUpperLegBone, ref _leftUpperLegBase);
            CaptureBase(_rightUpperLegBone, ref _rightUpperLegBase);
            CaptureBase(_rightUpperArmBone, ref _rightUpperArmBase);
            CaptureBase(_rightLowerArmBone, ref _rightLowerArmBase);
            _rightUpperArmCurrent = _rightUpperArmBone != null ? _rightUpperArmBone.localRotation : Quaternion.identity;
            _rightLowerArmCurrent = _rightLowerArmBone != null ? _rightLowerArmBone.localRotation : Quaternion.identity;
            _leftUpperArmCurrent = _leftUpperArmBone != null ? _leftUpperArmBone.localRotation : Quaternion.identity;
            _leftLowerArmCurrent = _leftLowerArmBone != null ? _leftLowerArmBone.localRotation : Quaternion.identity;
        }

        private static void CaptureBase(Transform bone, ref Quaternion baseRotation)
        {
            if (bone != null)
            {
                baseRotation = bone.localRotation;
            }
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
            _locomotion?.SetGroundClampSuspended(false);
            EquippedHook?.HideRope();
            if (OrbitCamera != null)
            {
                OrbitCamera.Distance = _savedCameraDistance;
            }
            // Landed, let go, or dashed out -- every one of those calls
            // Release, so restoring full animated control has exactly one
            // place to live rather than being duplicated at each call site.
            if (_animator != null)
            {
                _animator.enabled = true;
                // FORCED SYNCHRONOUSLY, NOT LEFT FOR THE NEXT REAL FRAME --
                // confirmed live with a repeated-fire diagnostic: a player
                // re-firing the grapple soon after a release (the entirely
                // normal "that didn't work, try again" case) can call
                // Attach again before Unity's own Animator system has ever
                // actually evaluated a frame since `enabled` flipped back
                // to true. CaptureBase then reads whatever this swing's OWN
                // UpdateSwingPose last left the bone at -- not a clean
                // animated pose -- and eases the reach rotation a FURTHER
                // ~120 degrees on top of that already-rotated value.
                // Measured directly: 40 rapid attach/release cycles with
                // zero real frames between them drifted the right arm's own
                // captured base by 120 degrees EVERY cycle, cycling through
                // 120/240/360 without ever actually returning to a clean
                // pose -- the live "gets more distorted the more I move
                // around" report, reproduced exactly. A zero-delta manual
                // Update forces the Animator to write its OWN current
                // state's pose to every bone right now, synchronously, so
                // the very next Attach -- even one called this same frame
                // -- always captures a genuinely fresh pose regardless of
                // when Unity's own Animator phase would otherwise have run.
                _animator.Update(0.0f);
            }
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

            _elapsedSeconds += deltaTime;

            // THE THIRD BUG behind the same live report: a rope's own
            // length IS the exact distance measured at the instant of
            // Attach, so it starts flush against its own taut threshold --
            // the constraint below only ever does anything once
            // distance > _ropeLength, strictly. Firing while standing on
            // the ground (again, the normal case) means gravity can't
            // actually move the body down at all -- the ground blocks
            // it -- so distance from the anchor never grows past that
            // threshold and the rope constraint never gets a single
            // chance to engage. With nothing here recognising that,
            // _velocity.y just kept accumulating more and more negative
            // forever, every frame spent touching the ground, while the
            // body visibly went nowhere. Matches PlayerLocomotion.Tick's
            // own identical fix for the identical problem, same guard and
            // same reasoning: only while already falling or level, so an
            // upward release/rope-pull already in progress is never eaten.
            if (_controller.isGrounded && _velocity.y <= 0.0f)
            {
                _velocity.y = -2.0f;
            }
            else
            {
                _velocity.y -= Gravity * deltaTime;
            }

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

            // See MaxSwingSpeed's own doc comment -- clamped here, before
            // release even gets a chance to read _velocity, so a runaway
            // speed can never be "banked" into SetResidualVelocity/
            // SetVerticalVelocity on release either.
            if (_velocity.magnitude > MaxSwingSpeed)
            {
                _velocity = _velocity.normalized * MaxSwingSpeed;
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

            Vector3 beforeMove = transform.position;
            Vector3 intendedMotion = _velocity * deltaTime + correction;
            // Read BEFORE Move() call below overwrites it for this frame --
            // see the wall-stick reconciliation's own doc comment for why
            // this matters.
            bool wasGroundedBeforeThisMove = _controller.isGrounded;
            _controller.Move(intendedMotion);

            // THE WALL-STICK FIX -- confirmed directly, live: firing at a
            // wall a real player can actually reach freezes the body's own
            // position (CharacterController.Move is correctly blocked by
            // real collision) while _velocity, with nothing here ever
            // checking whether the Move it just asked for actually
            // happened, keeps climbing every single frame forever -- a
            // 90-frame test firing point-blank at a wall showed position
            // frozen from frame ~60 on while Velocity.z climbed past 17 m/s
            // with no ceiling until MaxSwingSpeed's own cap eventually
            // caught it. That runaway then poisons two other things reading
            // _velocity: UpdateSwingPose's own sway pins the head/legs to
            // their maximum tilt and holds them there (the reported "gets
            // distorted to lengths that shouldn't be possible" -- a real,
            // if bounded, symptom of a swing whose own internal state has
            // completely decoupled from where the body actually is), and a
            // release right after hitting the wall would launch the body at
            // a phantom speed it was never actually travelling at. Scaled
            // down by however much of THIS frame's own intended motion
            // actually happened -- fully blocked reduces it to zero (an
            // immovable wall kills the swing's momentum outright, the same
            // way a real body's would), fully free leaves it untouched, and
            // ordinary taut-rope correction (which always fully succeeds
            // with nothing in the way) never triggers this at all.
            //
            // SKIPPED ENTIRELY WHILE ALREADY GROUNDED GOING INTO THIS
            // MOVE -- a second, real bug this same reconciliation
            // introduced, confirmed live through the real
            // PlayerInputReader dispatch (every earlier check of this fix
            // called GrappleSwing.Tick directly from a player spawned
            // already airborne, which never exercises this at all): firing
            // the grapple while standing on the ground -- the completely
            // normal case, not an edge case -- has gravity pulling
            // _velocity.y down every frame while the ground legitimately
            // blocks that SAME downward component, exactly like a wall
            // blocks a horizontal one. The reconciliation above cannot
            // tell "genuinely stuck in every direction" apart from
            // "perfectly free horizontally, blocked only downward by the
            // ground under your own feet" -- it only compares overall
            // magnitudes -- so it was scaling the WHOLE velocity vector
            // down every single grounded frame, including the horizontal
            // component that should have been completely free to build
            // into a swing. The character could never accelerate enough to
            // actually leave the ground: "the character stays in place...
            // the more you move around... the more contorted he gets".
            Vector3 achievedMotion = transform.position - beforeMove;
            float intendedDistance = intendedMotion.magnitude;
            if (!wasGroundedBeforeThisMove && intendedDistance > 0.0001f)
            {
                _velocity *= Mathf.Clamp01(achievedMotion.magnitude / intendedDistance);
            }

            UpdateSwingPose(deltaTime);

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
            else if (_elapsedSeconds >= AttachGraceSeconds && _controller.isGrounded &&
                     new Vector2(_velocity.x, _velocity.z).magnitude < 1.0f)
            {
                Release(false);
            }
        }

        /// <summary>
        /// Drives the swing's own held pose every frame -- see this class's
        /// own doc comment for why this is plain bone rotation rather than
        /// physics. Head/legs/free-arm sway off the swing's own smoothed
        /// local velocity, reading as a loose flail without ever touching a
        /// Rigidbody or a joint; the right arm (and the left, while
        /// actually casting) eases toward a reach/cast pose layered onto
        /// its own captured base instead -- see RightUpperArmReachDegrees's
        /// own doc comment for why that base matters.
        /// </summary>
        private void UpdateSwingPose(float deltaTime)
        {
            if (_animator == null)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(_velocity);
            _localVelocitySmoothed = Vector3.SmoothDamp(
                _localVelocitySmoothed, localVelocity, ref _localVelocitySmoothVelocity,
                SwaySmoothTime, Mathf.Infinity, deltaTime);

            float sideways = Mathf.Clamp(_localVelocitySmoothed.x / SwayReferenceSpeed, -1.0f, 1.0f);
            float forwardBack = Mathf.Clamp(_localVelocitySmoothed.z / SwayReferenceSpeed, -1.0f, 1.0f);

            ApplySway(_headBone, _headBase, new Vector3(0.0f, 0.0f, -sideways * 12.0f));
            ApplySway(_leftUpperLegBone, _leftUpperLegBase, new Vector3(forwardBack * -20.0f, 0.0f, sideways * 10.0f));
            ApplySway(_rightUpperLegBone, _rightUpperLegBase, new Vector3(forwardBack * -20.0f, 0.0f, -sideways * 10.0f));

            float maxDelta = ArmPoseDegreesPerSecond * deltaTime;

            // The right arm always reaches for the rope -- see
            // RightUpperArmReachDegrees's own doc comment for why the
            // target is layered on _rightUpperArmBase/_rightLowerArmBase
            // rather than a fixed absolute orientation.
            Quaternion rightUpperTarget = _rightUpperArmBase * Quaternion.Euler(0.0f, 0.0f, RightUpperArmReachDegrees);
            Quaternion rightLowerTarget = _rightLowerArmBase * Quaternion.Euler(RightLowerArmReachDegrees, 0.0f, 0.0f);
            _rightUpperArmCurrent = Quaternion.RotateTowards(_rightUpperArmCurrent, rightUpperTarget, maxDelta);
            _rightLowerArmCurrent = Quaternion.RotateTowards(_rightLowerArmCurrent, rightLowerTarget, maxDelta);
            if (_rightUpperArmBone != null) _rightUpperArmBone.localRotation = _rightUpperArmCurrent;
            if (_rightLowerArmBone != null) _rightLowerArmBone.localRotation = _rightLowerArmCurrent;

            // THE GRAPPLE GUN OWNS THE RIGHT HAND, so a cast while swinging
            // holds the LEFT arm in its own cast pose instead of letting it
            // flail -- explicit ask: "it still works just comes from your
            // left hand". Otherwise the left arm sways loosely like the
            // head and legs.
            bool casting = _fireballCaster != null && _fireballCaster.IsCasting;
            Quaternion leftUpperTarget = casting
                ? _leftUpperArmBase * Quaternion.Euler(0.0f, 0.0f, LeftUpperArmCastDegrees)
                : _leftUpperArmBase * Quaternion.Euler(forwardBack * -30.0f, 0.0f, sideways * 25.0f);
            Quaternion leftLowerTarget = casting
                ? _leftLowerArmBase * Quaternion.Euler(LeftLowerArmCastDegrees, 0.0f, 0.0f)
                : _leftLowerArmBase * Quaternion.Euler(Mathf.Abs(forwardBack) * 20.0f, 0.0f, 0.0f);
            _leftUpperArmCurrent = Quaternion.RotateTowards(_leftUpperArmCurrent, leftUpperTarget, maxDelta);
            _leftLowerArmCurrent = Quaternion.RotateTowards(_leftLowerArmCurrent, leftLowerTarget, maxDelta);
            if (_leftUpperArmBone != null) _leftUpperArmBone.localRotation = _leftUpperArmCurrent;
            if (_leftLowerArmBone != null) _leftLowerArmBone.localRotation = _leftLowerArmCurrent;
        }

        private static void ApplySway(Transform bone, Quaternion baseRotation, Vector3 swayEuler)
        {
            if (bone != null)
            {
                bone.localRotation = baseRotation * Quaternion.Euler(swayEuler);
            }
        }
    }
}
