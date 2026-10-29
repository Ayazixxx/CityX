using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The grappling hook's swing: aim, fire, hang from a rigid rope and
    /// pump it for height. Ported from player.gd's own start_grapple/
    /// _process_grapple_swing/release_grapple, numbers included.
    ///
    /// REBUILT FROM SQUARE ONE, after eight rounds of fixes across more
    /// than a month never fully eliminated a live pose-distortion report
    /// ("REMOVE THE GRAPPLE ENTIRELY IF YOU HAVE TO" -- see
    /// grapple_swing_deprioritized memory for the whole saga). Every one of
    /// those eight rounds fixed a real, verified bug in the OLD version's
    /// held pose -- it disabled the Animator the instant a rope attached
    /// and drove the head/arm/leg bones by hand every frame, then blended
    /// back to the Animator on release. The rope physics itself (this
    /// file's rigid distance constraint, gravity, air control, reel/
    /// payout, wall-stick reconciliation) was NEVER the suspect in any of
    /// those eight rounds and stayed correct throughout -- it is kept here
    /// almost verbatim. What changed is the pose: THIS VERSION NEVER
    /// DISABLES THE ANIMATOR OR TOUCHES A SINGLE BONE. It drives the
    /// SAME Speed/MoveX/MoveZ/Grounded parameters ordinary WASD movement
    /// already drives (see PlayerLocomotion.DriveAnimatorFromExternalVelocity),
    /// so Mecanim's own already-proven blend tree produces the character's
    /// pose throughout -- a swing looks like falling/running through the
    /// air, exactly as safe as an ordinary jump, because it uses the exact
    /// same mechanism. PlayerLocomotion's own LateUpdate (feet clamp,
    /// casting pose, gun-aim pose, arm-snap suppression) also keeps
    /// running completely normally the whole time, since none of it is
    /// gated on anything this class does any more -- it only ever had to
    /// stand down for the old version's own bone-owning pose.
    ///
    /// NO RAGDOLL, EVER -- unchanged, unrelated to this rebuild.
    /// Ragdoll.Activate already refuses to ragdoll out of an active swing
    /// (see its own doc comment: "remove the ragdoll function completely
    /// and revert back to him just being still") and nothing here changes
    /// that.
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
    public sealed partial class GrappleSwing : MonoBehaviour
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
        /// track. Ordinary swinging never gets remotely close to this
        /// ceiling -- held taut, pushing the whole time settles to roughly
        /// SwingAccel / RopeDamping = 16/0.015 ~ 18 m/s on its own -- so
        /// this only ever clips the exploit, not the normal feel.
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
        /// Bug report, confirmed directly by measurement: a real
        /// varied swing anchored on a real nearby building brought the
        /// swinging hand within 1.23m of the camera -- every grapple
        /// anchor is on a wall (see ThirdPersonCamera.MinDistance's own
        /// comment), so the camera is ALSO already pulled toward its own
        /// MinDistance floor by the nearby geometry while this happens,
        /// stacking both effects. Pulled back to a fixed distance for the
        /// swing's whole duration, the same fixed-distance-for-the-duration
        /// idiom PlayerInputReader.BeginCarTransition/EndCarTransition
        /// already uses for DrivingCameraDistance.
        /// </summary>
        public const float SwingCameraDistance = 6.5f;

        private float _savedCameraDistance;

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;

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
        /// anywhere. Gives every swing a real, if brief, window to prove
        /// itself airborne before the low-speed check can end it at all --
        /// the grounded-TIMEOUT release (MaxGroundSeconds) is untouched
        /// and still the guaranteed backstop for a swing that lands and
        /// stays down.
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
        /// Finds the controller/locomotion. Public because Awake never
        /// runs in EditMode -- see PlayerLocomotion's own Bind for the same
        /// reasoning -- and the rope's own physics (no-stretch, keeps
        /// tangential speed, momentum on release, reel clamps to minimum)
        /// is exactly the kind of thing that needs to be testable without a
        /// scene. Calling it twice is harmless.
        /// </summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            BindRope();
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
            // The ragdoll swing (GrappleSwing.Rope.cs): off the ground with the rope taut, the body goes limp on a rope tied to its hand and physics
            // does the rest; hooked from the ground it runs with the rope as before, and hangs once it swings off it (Tick). Before IsActive: the
            // ragdoll's own Activate releases an active swing instead of going limp.
            if (RopeDue(anchor))
            {
                TryAttachRope(anchor, _velocity);
            }
            IsActive = true;
            _groundTime = 0.0f;
            _elapsedSeconds = 0.0f;

            EquippedHook?.ShowRope(anchor);

            // See SwingCameraDistance's own doc comment.
            if (OrbitCamera != null)
            {
                _savedCameraDistance = OrbitCamera.Distance;
                OrbitCamera.Distance = SwingCameraDistance;
            }
        }

        /// <summary>
        /// Lets go. <paramref name="boosted"/> adds the upward kick that
        /// makes releasing at the top of an arc worth timing; a release
        /// forced by something else (death, ragdoll, unequipping) passes
        /// false and just drops the rope.
        /// </summary>
        public void Release(bool boosted)
        {
            if (!IsActive)
            {
                return;
            }
            if (IsRoped)
            {
                ReleaseRope(boosted);
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
            if (OrbitCamera != null)
            {
                OrbitCamera.Distance = _savedCameraDistance;
            }
            // NOTHING ELSE TO HAND BACK -- the Animator was never disabled
            // and PlayerLocomotion's own LateUpdate never stopped running,
            // so ordinary control resumes the instant PlayerInputReader's
            // dispatch next calls PlayerLocomotion.Tick, exactly as if the
            // player had just landed a jump.
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
            if (IsRoped || (RopeDue(_anchor) && TryAttachRope(_anchor, _velocity)))
            {
                TickRoped(moveInput, yawDegrees, boostedReleasePressed, plainReleasePressed, reelHeld, payoutHeld, deltaTime);
                return;
            }

            _elapsedSeconds += deltaTime;

            // A rope's own length IS the exact distance measured at the
            // instant of Attach, so it starts flush against its own taut
            // threshold -- the constraint below only ever does anything
            // once distance > _ropeLength, strictly. Firing while standing
            // on the ground (the normal case) means gravity can't actually
            // move the body down at all -- the ground blocks it -- so
            // distance from the anchor never grows past that threshold and
            // the rope constraint never gets a chance to engage. Matches
            // PlayerLocomotion.Tick's own identical fix for the identical
            // problem: only while already falling or level, so an upward
            // release/rope-pull already in progress is never eaten.
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
                // between Move() calls, which would let the body drift
                // further from the anchor every frame instead of being
                // pulled back.
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

            // THE WALL-STICK FIX -- firing at a wall a real player can
            // actually reach freezes the body's own position
            // (CharacterController.Move is correctly blocked by real
            // collision) while _velocity, with nothing here ever checking
            // whether the Move it just asked for actually happened, keeps
            // climbing every single frame forever. Scaled down by however
            // much of THIS frame's own intended motion actually happened --
            // fully blocked reduces it to zero (an immovable wall kills the
            // swing's momentum outright, the same way a real body's would),
            // fully free leaves it untouched, and ordinary taut-rope
            // correction (which always fully succeeds with nothing in the
            // way) never triggers this at all.
            //
            // SKIPPED ENTIRELY WHILE ALREADY GROUNDED GOING INTO THIS MOVE --
            // firing the grapple while standing on the ground (the normal
            // case) has gravity pulling _velocity.y down every frame while
            // the ground legitimately blocks that SAME downward component,
            // exactly like a wall blocks a horizontal one. The
            // reconciliation above cannot tell "genuinely stuck in every
            // direction" apart from "perfectly free horizontally, blocked
            // only downward by the ground under your own feet" -- it only
            // compares overall magnitudes -- so without this guard it would
            // scale the WHOLE velocity vector down every single grounded
            // frame, including the horizontal component that should have
            // been completely free to build into a swing.
            Vector3 achievedMotion = transform.position - beforeMove;
            float intendedDistance = intendedMotion.magnitude;
            if (!wasGroundedBeforeThisMove && intendedDistance > 0.0001f)
            {
                _velocity *= Mathf.Clamp01(achievedMotion.magnitude / intendedDistance);
            }

            _locomotion?.DriveAnimatorFromExternalVelocity(_velocity, _controller.isGrounded, deltaTime);

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
    }
}
