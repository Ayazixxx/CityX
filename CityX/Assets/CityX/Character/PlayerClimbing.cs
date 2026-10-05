using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Wall climbing: HOLD SPACE (a tap is a jump) with W facing a real wall
    /// to grab on -- see GrabIntent and WallProbe, and docs/CLIMBING.md for
    /// why it is no longer "sprint+W near anything" -- W/S to climb
    /// up/down once attached, a fresh Space press hops off with an outward push. Ported
    /// from player.gd's own _try_start_climb/_process_climbing/CLIMB_*
    /// consts and character.gd's start_climb_up/start_climb_down/hold_climb/
    /// end_climb/play_climb_top_out, numbers included. The Unity port's own
    /// PlayerLocomotion class doc comment named this as deliberately
    /// missing ("no climbing... comes back when there is a reason beyond
    /// 'the old one had it'") and SkybridgeLayout's own doc comment already
    /// anticipated it ("wall-climbing was built for fun; a city meant to be
    /// traversed vertically is the world it was made for") -- this is that.
    ///
    /// OWNS THE WHOLE FRAME WHILE ACTIVE, matching GrappleSwing's own
    /// pattern (itself matching player.gd's _physics_process early-return
    /// into _process_grapple_swing): PlayerInputReader checks
    /// <see cref="IsClimbing"/>/<see cref="IsToppingOut"/> before its normal
    /// WASD-to-locomotion dispatch and, while either is true, calls
    /// <see cref="Tick"/>/<see cref="TickTopOut"/> here instead of
    /// PlayerLocomotion.Tick. A swing and a wall-climb are mutually
    /// exclusive movement modes in player.gd too (its own comment: "a swing
    /// and a wall-climb are mutually exclusive... `_try_start_climb()` can
    /// fire on the same held W that's pumping a swing"), so
    /// PlayerInputReader never offers a climb grab while GrappleSwing.IsActive.
    ///
    /// STAMINA IS THE SAME POOL SPRINTING DRAINS, not a second one --
    /// player.gd's own comment says so outright, and PlayerVitals.TickStamina
    /// is called here exactly the way PlayerInputReader already calls it for
    /// sprinting, so the same _exhausted latch that blocks re-sprinting at 0
    /// stamina also applies here, and infinite-stamina dev mode covers both
    /// for free.
    ///
    /// WORLD GEOMETRY, NOT A LAYER MASK -- matches GrapplingHookItem.FindAnchor's
    /// own "all layers, but not a character" rule (Godot's collision_mask=1
    /// has no Unity layer-scheme equivalent anywhere else in this project to
    /// port, so this follows the established Unity-side convention instead):
    /// any collider without an IDamageable in its parent chain counts as a
    /// climbable wall, filtered only by how vertical its hit normal is.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerClimbing : MonoBehaviour
    {
        // --- player.gd's own CLIMB_* constants, unchanged. ---

        public const float ClimbSpeed = 2.5f;
        public const float GrabCheckDistance = 1.0f;
        public const float WallMaxSlopeDot = 0.3f;
        public const float TopCheckHeight = 1.8f;
        public const float HopOffOutwardSpeed = 3.5f;
        public const float HopOffUpwardSpeed = 2.0f;
        public const float MantleUp = 0.4f;
        public const float MantleForward = 0.6f;

        /// <summary>
        /// player.gd PUNCH_ORIGIN_HEIGHT (1.5), re-expressed as a fraction of
        /// standing height the same way MeleeAttacker.OriginHeightFraction
        /// already does (1.5 / 2.2 = 0.68) -- so the grab raycast scales
        /// with StandingHeight instead of assuming Godot's own 2.2m rig.
        /// </summary>
        public const float GrabOriginHeightFraction = 0.68f;

        private static readonly int ClimbingParameter = Animator.StringToHash("Climbing");
        private static readonly int ClimbDirectionParameter = Animator.StringToHash("ClimbDirection");
        private static readonly int ClimbTopOutParameter = Animator.StringToHash("ClimbTopOut");

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        private Vector3 _wallNormal;
        private float _anchorX;
        private float _anchorZ;

        public bool IsClimbing { get; private set; }

        /// <summary>True only during climb_top_out's own one-shot mantle -- see BeginTopOut.</summary>
        public bool IsToppingOut { get; private set; }

        /// <summary>Exposed for tests -- the raw push _end_climb would have applied, push or no push.</summary>
        public Vector3 LastEndVelocity { get; private set; }

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Finds the controller/animator/locomotion. Public because Awake
        /// never runs in EditMode -- same reasoning PlayerLocomotion.Bind
        /// and GrappleSwing.Bind both already give. Calling it twice is
        /// harmless.
        /// </summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        /// <summary>How far in front of the wall face the body hangs, on top of the capsule's own radius.</summary>
        public const float HangStandoffMetres = 0.05f;

        /// <summary>How fast the body is drawn onto the wall face when it grabs on (it used to hang wherever it was, up to a metre off).</summary>
        public const float SnapToWallSpeed = 6.0f;

        /// <summary>How fast the body turns to face the wall it grabbed, degrees per second.</summary>
        public const float FaceWallDegreesPerSecond = 540.0f;

        private GrabIntent _intent;

        /// <summary>
        /// Checked every frame NOT already climbing (see PlayerInputReader's own dispatch). Grabs on only when
        /// the player MEANS to: the grab key has been held (<see cref="GrabIntent"/>), W is held toward the wall,
        /// stamina remains, and there is a real wall in front (<see cref="WallProbe"/>: a wide, tall, flat face --
        /// not a doorway's jamb, a window frame or the wall at the top of a stair).
        ///
        /// It used to be Shift+W within a metre of ANY surface -- which is just running -- so it fired running
        /// up a staircase, jumping out of a window and running through a door. Assassin's Creed and the Unity
        /// ledge systems both separate "moving" from "asking to climb"; this does the same.
        /// </summary>
        public bool TryStart(GrabIntent intent, bool forwardHeld, float stamina)
        {
            if (IsClimbing || IsToppingOut || !forwardHeld || stamina <= 0.0f || intent == null || !intent.Ready)
            {
                return false;
            }

            float standingHeight = _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;
            if (!WallProbe.TryFind(transform.position, transform.forward, standingHeight, out WallProbe.Result wall))
            {
                return false;
            }

            IsClimbing = true;
            _intent = intent;
            intent.Consume();
            _wallNormal = new Vector3(wall.Normal.x, 0.0f, wall.Normal.z).normalized;

            // Hang against the face itself, the capsule's own radius off it.
            float standoff = (_controller != null ? _controller.radius : 0.3f) + HangStandoffMetres;
            Vector3 hang = wall.Point + _wallNormal * standoff;
            _anchorX = hang.x;
            _anchorZ = hang.z;
            _locomotion?.SetVerticalVelocity(0.0f);
            return true;
        }

        /// <summary>
        /// Owns the whole frame while attached to a wall -- W climbs up, S
        /// climbs down, neither holds the current grip in place, Space hops
        /// off, and running out of stamina releases the grip the same way
        /// hopping off does, just without the deliberate push. Mirrors
        /// player.gd's own _process_climbing exactly, including its order
        /// of checks.
        /// </summary>
        public void Tick(bool jumpPressed, bool climbUpHeld, bool climbDownHeld, PlayerVitals vitals, float deltaTime, bool dropPressed = false)
        {
            if (!IsClimbing)
            {
                return;
            }

            if (jumpPressed)
            {
                EndClimb(true, deltaTime);
                return;
            }
            // CONTROL drops: let go and fall straight down, no push (Space hops off outward; the Dynamic Parkour System this was modelled on
            // uses C for drop, moved to Control here).
            if (dropPressed)
            {
                EndClimb(false, deltaTime);
                return;
            }

            // Same pool sprinting drains, same exhaustion latch -- see this
            // class's own doc comment.
            //
            // A WET LADDER COSTS MORE AND SLIPS -- see WallStick.Tick's own
            // comment on rain and grip. Nothing changes on a dry or covered wall.
            float wet = CurrentWeather.SurfaceWetness(transform.position);
            bool stillHasStamina = vitals == null
                || vitals.TickStamina(true, deltaTime * Weather.WallStaminaDrain(wet));
            if (!stillHasStamina)
            {
                EndClimb(false, deltaTime);
                return;
            }
            float slip = Weather.WallSlipSpeed(wet);

            int direction = 0;
            float standingHeight = _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;
            float radius = _controller != null ? _controller.radius : 0.3f;
            bool ledgeAhead = LedgeProbe.TryFind(transform.position, _wallNormal, standingHeight, radius, transform, out LedgeProbe.Ledge ledge);
            if (climbUpHeld && !climbDownHeld)
            {
                // REACHED A REAL LEDGE? Not "the wall stopped": a gap in a wall (a window, a recess, a balcony) is not an edge, and mantling
                // there put the body inside the wall. A ledge is a validated flat, deep, wide top with room to stand (LedgeProbe), and it is
                // mantled once it is within reach of the hands.
                if (ledgeAhead && ledge.Height - transform.position.y <= standingHeight * MantleReachFraction)
                {
                    BeginMantle(ledge);
                    return;
                }
                // Neither a ledge nor wall left at head height: there is nothing to climb into. Hold the grip rather than climb up into thin air.
                if (!ledgeAhead && !WallInDirection(transform.position + Vector3.up * (standingHeight * 0.85f), -_wallNormal, out _))
                {
                    direction = 0;
                }
                else
                {
                    direction = 1;
                }
            }
            else if (climbDownHeld && !climbUpHeld)
            {
                direction = -1;
            }

            // Hands go to the ledge as it comes within reach (an easing weight), so they are on it when the mantle starts.
            if (_locomotion != null)
            {
                bool near = ledgeAhead && ledge.Height - transform.position.y <= standingHeight * (MantleReachFraction + 0.25f);
                _handsWeight = Mathf.MoveTowards(_handsWeight, near ? 1.0f : 0.0f, 4.0f * deltaTime);
                _locomotion.SetLedgeHands(_handsWeight, ledge.Lip, ledge.Normal);
            }

            // Drawn onto the wall face and turned to look at it, rather than snapped or left where the grab
            // happened. Both are eased: a grab is a reach, not a teleport.
            Vector2 toAnchor = Vector2.ClampMagnitude(
                new Vector2(_anchorX - transform.position.x, _anchorZ - transform.position.z),
                SnapToWallSpeed * deltaTime);
            Vector3 move = new Vector3(toAnchor.x, direction * ClimbSpeed * deltaTime - slip * deltaTime, toAnchor.y);
            float yBefore = transform.position.y;
            _controller.Move(move);

            // STUCK GUARD: asked to climb but not rising (jammed on a lip or geometry): let go rather than hang there for ever.
            if (direction > 0 && deltaTime > 0.0f)
            {
                float rose = transform.position.y - yBefore;
                _stuckSeconds = rose < ClimbSpeed * deltaTime * 0.2f ? _stuckSeconds + deltaTime : 0.0f;
                if (_stuckSeconds > StuckReleaseSeconds)
                {
                    _stuckSeconds = 0.0f;
                    EndClimb(false, deltaTime);
                    return;
                }
            }
            else
            {
                _stuckSeconds = 0.0f;
            }

            if (_wallNormal.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(-_wallNormal, Vector3.up), FaceWallDegreesPerSecond * deltaTime);
            }

            if (_animator != null)
            {
                _animator.SetBool(ClimbingParameter, true);
                _animator.SetInteger(ClimbDirectionParameter, direction);
            }

            // Climbed all the way back down to solid ground -- release
            // normally (no push, same as running out of stamina) rather
            // than leaving the player stuck holding an invisible grip below
            // floor level.
            if ((direction < 0 || slip > 0.0f) && _controller.isGrounded)
            {
                EndClimb(false, deltaTime);
            }
        }

        /// <summary>
        /// A forward raycast from origin toward direction, into the wall --
        /// reused both to grab on in the first place (from the player's own
        /// current height, direction = facing) and to detect the top edge
        /// while climbing (from above the player's own current grip,
        /// direction = back into the wall the grip is already on).
        /// </summary>
        private static bool WallInDirection(Vector3 origin, Vector3 direction, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, GrabCheckDistance,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            // Not a person -- matches GrapplingHookItem.FindAnchor's own
            // "world geometry only" rule, the closest existing Unity-side
            // convention for what player.gd's collision_mask=1 meant.
            if (hit.collider.GetComponentInParent<IDamageable>() != null)
            {
                return false;
            }
            if (Mathf.Abs(hit.normal.y) > WallMaxSlopeDot)
            {
                return false;   // too close to horizontal to be a real climbable wall
            }
            normal = hit.normal;
            return true;
        }

        /// <summary>
        /// Releases the wall for any reason OTHER than reaching the top
        /// (hopping off with Space, or running out of stamina). Computes
        /// the same push player.gd's own _end_climb does either way (tests
        /// check LastEndVelocity directly, matching test_climbing_check.gd's
        /// own choice to call _end_climb straight rather than fight real
        /// Space-press timing) but only hands PlayerLocomotion the vertical
        /// component -- same accepted simplification GrappleSwing.Release
        /// already documents for its own release: ordinary WASD air control
        /// recomputes horizontal velocity from scratch every
        /// PlayerLocomotion.Tick, so there is nothing here for a horizontal
        /// component to persist into. One immediate Move() with the FULL
        /// push (horizontal included) gives the hop a visible outward pop
        /// this same frame regardless, the same "does not sit frozen for a
        /// frame" choice GrappleSwing.Release already makes for its own
        /// release velocity.
        /// </summary>
        private void EndClimb(bool applyPush, float deltaTime)
        {
            IsClimbing = false;
            _handsWeight = 0.0f;
            _locomotion?.SetLedgeHands(0.0f, Vector3.zero, Vector3.zero);
            _intent?.BeginCooldown();   // a hop-off is not undone by the next frame's grab
            if (_animator != null)
            {
                _animator.SetBool(ClimbingParameter, false);
            }

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

        // ---- THE MANTLE: up and over a real ledge, warped to where the ledge actually is ------------------------------------------------
        //
        // Researched (Unity ledge/parkour write-ups, Dynamic Parkour System's feature list, Unity's MatchTarget): a mantle authored for one height
        // looks broken on any other, so the standard answer is MOTION WARPING -- play the animation, but drive the body along a path computed from
        // the real ledge (hand-hold to the standing spot), and put the hands ON the ledge while it happens. The old code waited for a clip to end
        // and then teleported the body up a fixed 2.2 m and 0.6 m INTO the wall.

        /// <summary>How far above the feet the ledge may be for the mantle to start, as a fraction of standing height (the hands' reach).</summary>
        public const float MantleReachFraction = 1.0f;

        /// <summary>The whole mantle takes this long, in seconds.</summary>
        public const float MantleSeconds = 1.2f;

        /// <summary>Climbing up while not rising for this long lets go (jammed).</summary>
        public const float StuckReleaseSeconds = 0.8f;

        /// <summary>Of the mantle, the first part is the pull straight up the wall; the rest is stepping forward onto the ledge.</summary>
        private const float RiseFraction = 0.6f;

        private LedgeProbe.Ledge _mantle;
        private Vector3 _mantleStart;
        private float _mantleElapsed;
        private float _handsWeight;
        private float _stuckSeconds;
        private float _animatorSpeedBefore = 1.0f;

        /// <summary>Where the mantle is, 0 (hanging) to 1 (standing on the ledge). Meaningless outside a mantle.</summary>
        public float MantleProgress => Mathf.Clamp01(_mantleElapsed / MantleSeconds);

        private void BeginMantle(LedgeProbe.Ledge ledge)
        {
            IsClimbing = false;
            IsToppingOut = true;
            _mantle = ledge;
            _mantleStart = transform.position;
            _mantleElapsed = 0.0f;
            _locomotion?.SetVerticalVelocity(0.0f);
            if (_animator == null)
            {
                CompleteMantle();   // no animator (a bare test body): the mantle is instant
                return;
            }
            _animator.SetBool(ClimbingParameter, false);
            _animator.SetTrigger(ClimbTopOutParameter);
            _animatorSpeedBefore = _animator.speed;
            if (_controller != null)
            {
                _controller.enabled = false;   // the path is driven directly; the validated end spot is what keeps it out of the wall
            }
        }

        /// <summary>The body's position along the mantle at <paramref name="u"/> (0 to 1): pull up the wall, then step over the lip onto the ledge.</summary>
        public static Vector3 MantlePath(Vector3 start, LedgeProbe.Ledge ledge, float u)
        {
            u = Mathf.Clamp01(u);
            float rise = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(u / RiseFraction));
            float step = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((u - RiseFraction) / (1.0f - RiseFraction)));
            Vector3 hang = new Vector3(start.x, Mathf.Lerp(start.y, ledge.Height, rise), start.z);
            Vector3 over = new Vector3(Mathf.Lerp(start.x, ledge.Stand.x, step), hang.y, Mathf.Lerp(start.z, ledge.Stand.z, step));
            // A small lift while stepping, so the feet clear the lip instead of dragging along it.
            over.y += Mathf.Sin(step * Mathf.PI) * 0.12f;
            return over;
        }

        /// <summary>Called every frame by PlayerInputReader while IsToppingOut: advances the mantle along its warped path.</summary>
        public void TickTopOut()
        {
            TickMantle(Time.deltaTime);
        }

        /// <summary>Advances the mantle by <paramref name="deltaTime"/>; public so a test can step it without frames.</summary>
        public void TickMantle(float deltaTime)
        {
            if (!IsToppingOut)
            {
                return;
            }
            _mantleElapsed += deltaTime;
            float u = MantleProgress;

            // The clip is fitted to the mantle's own length (once the state is playing), so its pull-up and its step-over line up with the path.
            if (_animator != null)
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                if (info.IsName("ClimbTopOut") && info.length > 0.01f)
                {
                    _animator.speed = info.length / MantleSeconds;
                }
            }

            transform.position = MantlePath(_mantleStart, _mantle, u);
            if (_mantle.Normal.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-_mantle.Normal, Vector3.up),
                    FaceWallDegreesPerSecond * deltaTime);
            }
            // Hands stay on the lip through the pull-up and let go as the body steps over it.
            _locomotion?.SetLedgeHands(u < RiseFraction ? 1.0f : Mathf.Clamp01(1.0f - (u - RiseFraction) / ((1.0f - RiseFraction) * 0.6f)),
                _mantle.Lip, _mantle.Normal);

            if (u >= 1.0f)
            {
                CompleteMantle();
            }
        }

        /// <summary>Puts the feet on the ledge's standing spot (snapped to the real surface), hands the body back to the controller and to locomotion.</summary>
        private void CompleteMantle()
        {
            IsToppingOut = false;
            _intent?.BeginCooldown();
            Vector3 stand = _mantle.Stand;
            if (Physics.Raycast(stand + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                stand.y = hit.point.y;
            }
            transform.position = stand;
            if (_controller != null)
            {
                _controller.enabled = true;
            }
            Physics.SyncTransforms();

            _handsWeight = 0.0f;
            _locomotion?.SetLedgeHands(0.0f, Vector3.zero, Vector3.zero);
            _locomotion?.SetVerticalVelocity(0.0f);
            if (_animator != null)
            {
                _animator.speed = _animatorSpeedBefore;
                if (_animator.HasState(0, Animator.StringToHash("Locomotion")))
                {
                    _animator.CrossFadeInFixedTime("Locomotion", 0.2f, 0);
                }
            }
            _wallNormal = Vector3.zero;
        }
    }
}
