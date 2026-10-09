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

        /// <summary>Raised when a mantle begins, so another player's copy of this body can play the pull-up too.</summary>
        public event System.Action MantleStarted;

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

            BeginClimb(wall, intent);
            return true;
        }

        private void BeginClimb(WallProbe.Result wall, GrabIntent intent)
        {
            IsClimbing = true;
            _intent = intent;
            intent?.Consume();
            _autoPress = 0.0f;
            _wallNormal = new Vector3(wall.Normal.x, 0.0f, wall.Normal.z).normalized;

            // Hang against the face itself, the capsule's own radius off it.
            float standoff = (_controller != null ? _controller.radius : 0.3f) + HangStandoffMetres;
            Vector3 hang = wall.Point + _wallNormal * standoff;
            _anchorX = hang.x;
            _anchorZ = hang.z;
            _locomotion?.SetVerticalVelocity(0.0f);
        }

        // ---- AUTOMATIC CLIMB: run (Shift + W) into a wall ----------------------------------------------------------------------------------------
        //
        // Holding run and forward into a tall wall starts the climb with no further input, as Assassin's Creed's free-running does. Shift + W is also how the
        // player simply RUNS, which is why it used to fire running up a staircase, through a doorway and out of a window; so it is guarded:
        //   * the wall must be a real one (WallProbe: a wide, tall, flat face, no gaps), faced squarely (within 30 degrees, not grazed);
        //   * it must be pressed into for a moment (AutoDwellSeconds), so brushing past a wall as you turn a corner does nothing;
        //   * not while climbing stairs or a ramp (the ground has been rising under the feet in the last second), where the wall is the top landing's;
        //   * not within DoorRadiusMetres of a door (the doorway's jambs and the walls round it);
        //   * not on the ground only: it has to be grounded, and after a climb ends the run keys must be released before another can start.
        // Holding Space at a wall still grabs it from anywhere, as before.

        /// <summary>How long run + forward must be pressed into the wall before it is grabbed, in seconds.</summary>
        public const float AutoDwellSeconds = 0.2f;

        /// <summary>The wall must be faced within this many degrees.</summary>
        public const float AutoMaxFacingDegrees = 30.0f;

        /// <summary>No automatic climb within this distance of a door, in metres.</summary>
        public const float DoorRadiusMetres = 3.0f;

        /// <summary>The standing height rising faster than this (m/s) means a staircase or a steep ramp.</summary>
        public const float StairRiseSpeed = 0.8f;

        /// <summary>How long after the last stair-like rise an automatic climb stays off, in seconds.</summary>
        public const float StairMemorySeconds = 1.0f;

        private float _autoPress;
        private float _stairTimer;
        private float _lastGroundedY;
        private float _lastGroundedTime = -10.0f;
        private bool _autoNeedsRelease;
        private static readonly Collider[] DoorOverlaps = new Collider[16];

        /// <summary>True while the recent walk has been up stairs or a ramp (so an automatic climb is held off).</summary>
        public bool RecentlyOnStairs => _stairTimer > 0.0f;

        /// <summary>Call once per frame the player is on foot (not already climbing): tracks the ground under the feet to spot stairs and ramps.</summary>
        public void TrackGround(bool grounded, float deltaTime)
        {
            _stairTimer = Mathf.Max(0.0f, _stairTimer - deltaTime);
            if (!grounded)
            {
                return;
            }
            // Compared with a reference height from 0.15 to 0.6 s ago (not the last frame, which is noise, and running up steps the controller is only
            // "grounded" some frames): how fast has the standing height been rising?
            float now = Time.time;
            float y = transform.position.y;
            float since = now - _lastGroundedTime;
            if (since >= 0.15f)
            {
                if (since < 0.6f && (y - _lastGroundedY) / since > StairRiseSpeed)
                {
                    MarkStairs();
                }
                _lastGroundedTime = now;
                _lastGroundedY = y;
            }
        }

        /// <summary>
        /// The ground has been rising like a staircase: no automatic climb for a moment, AND the run keys must be let go and pressed again first, so running
        /// up a flight and carrying on into the wall at its top never turns into a climb by itself. (Also a test hook.)
        /// </summary>
        public void MarkStairs()
        {
            _stairTimer = StairMemorySeconds;
            _autoNeedsRelease = true;
        }

        /// <summary>
        /// Called every frame on foot. Starts a climb when run + forward have been pressed into a real wall (see the guards above). Returns true on the frame it starts.
        /// </summary>
        public bool TryStartAuto(bool forwardHeld, bool runHeld, bool grounded, float stamina, float deltaTime, Vector3 direction = default)
        {
            TrackGround(grounded, deltaTime);
            if (!forwardHeld || !runHeld)
            {
                _autoNeedsRelease = false;   // let go of the run keys: the next press is a new attempt
                _autoPress = 0.0f;
                return false;
            }
            if (IsClimbing || IsToppingOut || _autoNeedsRelease || !grounded || stamina <= 0.0f || _stairTimer > 0.0f)
            {
                _autoPress = 0.0f;
                return false;
            }
            float standingHeight = _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;
            Vector3 run = direction.sqrMagnitude > 0.0001f ? direction : transform.forward;   // the way the player is trying to go, not the way the body happens to face
            if (!WallProbe.TryFind(transform.position, run, standingHeight, out WallProbe.Result wall))
            {
                _autoPress = 0.0f;
                return false;
            }
            Vector3 flatNormal = new Vector3(wall.Normal.x, 0.0f, wall.Normal.z);
            Vector3 flatForward = new Vector3(run.x, 0.0f, run.z);
            if (flatNormal.sqrMagnitude < 1e-6f || flatForward.sqrMagnitude < 1e-6f || Vector3.Angle(flatForward, -flatNormal) > AutoMaxFacingDegrees || NearDoor())
            {
                _autoPress = 0.0f;
                return false;
            }
            _autoPress += deltaTime;
            if (_autoPress < AutoDwellSeconds)
            {
                return false;
            }
            _autoPress = 0.0f;
            BeginClimb(wall, null);
            return true;
        }

        /// <summary>True when a door is within <see cref="DoorRadiusMetres"/> of the player (doors have trigger volumes, so triggers count).</summary>
        private bool NearDoor()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, DoorRadiusMetres, DoorOverlaps, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (DoorOverlaps[i] != null && DoorOverlaps[i].GetComponentInParent<IOpenableDoor>() != null)
                {
                    return true;
                }
            }
            return false;
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

            // Same pool sprinting drains, same exhaustion latch -- see this class's own doc comment. It is DRAINED ONLY WHILE MOVING (climbing up or
            // down, or sliding on a wet wall): hanging still is free, so a rest on the wall costs nothing. (Charged after the direction is known, below.)
            //
            // A WET LADDER COSTS MORE AND SLIPS -- see WallStick.Tick's own comment on rain and grip. Nothing changes on a dry or covered wall.
            float wet = CurrentWeather.SurfaceWetness(transform.position);
            float slip = Weather.WallSlipSpeed(wet);

            int direction = 0;
            float standingHeight = _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;
            float radius = _controller != null ? _controller.radius : 0.3f;
            bool ledgeAhead = LedgeProbe.TryFind(transform.position, _wallNormal, standingHeight, radius, transform, out LedgeProbe.Ledge ledge);

            // LEAN ROUND WHAT JUTS FROM THE WALL (ClimbJut): a course, a sill or a cornice alongside the body, or coming up over the head (down under the
            // feet when climbing down), pushes the hang point out by its depth; past it, the body settles back. Measured from the plane the climb holds.
            float standoff = radius + HangStandoffMetres;
            var planePoint = new Vector3(_anchorX, transform.position.y, _anchorZ) - _wallNormal * standoff;
            float lookBelow = climbDownHeld && !climbUpHeld ? ClimbJut.LookAhead : 0.1f;   // up: until the feet are clear of its top
            float lookAbove = climbUpHeld && !climbDownHeld ? ClimbJut.LookAhead : 0.1f;
            _lean = ClimbJut.LeanFor(ClimbJut.Measure(transform.position, standingHeight, lookBelow, lookAbove, planePoint, _wallNormal, transform));
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
                // (Looked for as far beyond the usual reach as the body is leaning out round a jut.)
                if (!ledgeAhead && !WallInDirection(transform.position + Vector3.up * (standingHeight * 0.85f), -_wallNormal, out _,
                        GrabCheckDistance + _lean))
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

            bool moving = direction != 0 || slip > 0.0f;
            if (moving && vitals != null && !vitals.TickStamina(true, deltaTime * Weather.WallStaminaDrain(wet)))
            {
                EndClimb(false, deltaTime);
                return;
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
                new Vector2(_anchorX + _wallNormal.x * _lean - transform.position.x, _anchorZ + _wallNormal.z * _lean - transform.position.z),
                SnapToWallSpeed * deltaTime);
            Vector3 move = new Vector3(toAnchor.x, direction * ClimbSpeed * deltaTime - slip * deltaTime, toAnchor.y);
            float yBefore = transform.position.y;
            _controller.Move(move);
            UpdateWallPlane();

            // STUCK GUARD: asked to climb but not rising (jammed on a lip or geometry): let go rather than hang there for ever.
            if (direction > 0 && deltaTime > 0.0f)
            {
                float rose = transform.position.y - yBefore;
                _stuckSeconds = rose < ClimbSpeed * deltaTime * 0.2f ? _stuckSeconds + deltaTime : 0.0f;
                if (_stuckSeconds > StuckReleaseSeconds)
                {
                    _stuckSeconds = 0.0f;
                    // Jammed under something overhead (an entrance canopy, a lip): drop clear of it as the grip goes, rather than let go with
                    // the head still pressed into it (found climbing up a door's canopy, seed 87650022).
                    _controller.Move(Vector3.down * StuckDropMetres);
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
        private static bool WallInDirection(Vector3 origin, Vector3 direction, out Vector3 normal, float distance = GrabCheckDistance)
        {
            normal = Vector3.zero;
            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance,
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
            _lean = 0.0f;
            _handsWeight = 0.0f;
            _locomotion?.SetLedgeHands(0.0f, Vector3.zero, Vector3.zero);
            _locomotion?.SetClimbWall(Vector3.zero, Vector3.forward, 0.0f);
            _intent?.BeginCooldown();   // a hop-off is not undone by the next frame's grab
            _autoNeedsRelease = true;   // and the run keys must be let go before an automatic climb can start again
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

        private Vector3 _planePoint;
        private Vector3 _planeNormal;

        /// <summary>Finds the wall face next to the chest and gives it to the body, so the limbs are kept out of it (PlayerLocomotion.ApplyClimbWallLimbs).</summary>
        private void UpdateWallPlane()
        {
            if (_locomotion == null || _wallNormal.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float height = _locomotion.StandingHeight;
            Vector3 origin = transform.position + Vector3.up * (height * 0.5f) + _wallNormal * 0.8f;
            if (LedgeProbe.SolidHit(origin, -_wallNormal, 1.6f, transform, out RaycastHit hit) && Mathf.Abs(hit.normal.y) < 0.5f)
            {
                _planePoint = hit.point;
                _planeNormal = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
            }
            else
            {
                _planePoint = transform.position - _wallNormal * ((_controller != null ? _controller.radius : 0.3f) + HangStandoffMetres);
                _planeNormal = _wallNormal;
            }
            _locomotion.SetClimbWall(_planePoint, _planeNormal, 1.0f);
        }

        // ---- THE MANTLE: up and over a real ledge, warped to where the ledge actually is ------------------------------------------------
        //
        // Researched (Unity ledge/parkour write-ups, Dynamic Parkour System's feature list, Unity's MatchTarget): a mantle authored for one height
        // looks broken on any other, so the standard answer is MOTION WARPING -- play the animation, but drive the body along a path computed from
        // the real ledge (hand-hold to the standing spot), and put the hands ON the ledge while it happens. The old code waited for a clip to end
        // and then teleported the body up a fixed 2.2 m and 0.6 m INTO the wall.

        /// <summary>How far above the feet the ledge may be for the mantle to start, as a fraction of standing height (the hands' reach).</summary>
        public const float MantleReachFraction = 1.2f;

        /// <summary>The whole mantle takes this long, in seconds.</summary>
        public const float MantleSeconds = 1.2f;

        /// <summary>Climbing up while not rising for this long lets go (jammed).</summary>
        public const float StuckReleaseSeconds = 0.8f;

        /// <summary>How far a jammed climb drops as it lets go, to come clear of whatever overhead stopped it.</summary>
        public const float StuckDropMetres = 0.15f;

        /// <summary>Of the mantle, the first part is the pull straight up the wall; the rest is stepping forward onto the ledge.</summary>
        private const float RiseFraction = 0.6f;

        /// <summary>The capsule radius the mantle path clears a jutting ledge by (the real radius is the controller's).</summary>
        private const float BodyRadius = 0.3f;

        private LedgeProbe.Ledge _mantle;
        private Vector3 _mantleStart;
        private float _mantleElapsed;
        private float _handsWeight;
        private float _stuckSeconds;

        /// <summary>How far out the body is hanging this frame to clear what juts from the wall (ClimbJut); 0 on a flat wall.</summary>
        private float _lean;

        /// <summary>How far out from the wall the climb is leaning round a jut right now (0 on a flat wall). For tests and the pose.</summary>
        public float Lean => _lean;

        private float _animatorSpeedBefore = 1.0f;

        /// <summary>Where the mantle is, 0 (hanging) to 1 (standing on the ledge). Meaningless outside a mantle.</summary>
        public float MantleProgress => Mathf.Clamp01(_mantleElapsed / MantleSeconds);

        private void BeginMantle(LedgeProbe.Ledge ledge)
        {
            IsClimbing = false;
            _lean = 0.0f;
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
            MantleStarted?.Invoke();
            _animatorSpeedBefore = _animator.speed;
            if (_controller != null)
            {
                _controller.enabled = false;   // the path is driven directly; the validated end spot is what keeps it out of the wall
            }
        }

        /// <summary>The mantle never lowers the body more than this by itself; past a taller parapet it ends in the air beyond the far edge and the controller drops the rest.</summary>
        public const float MaxMantleDropMetres = 0.8f;

        /// <summary>The height the mantle's last frame puts the feet at: the stand spot's, or at most <see cref="MaxMantleDropMetres"/> below the ledge.</summary>
        public static float MantleEndY(LedgeProbe.Ledge ledge) => Mathf.Max(ledge.Stand.y, ledge.Height - MaxMantleDropMetres);

        /// <summary>The body's position along the mantle at <paramref name="u"/> (0 to 1): pull up the wall, then step over the lip onto the ledge.</summary>
        public static Vector3 MantlePath(Vector3 start, LedgeProbe.Ledge ledge, float u)
        {
            u = Mathf.Clamp01(u);
            // A ledge that juts out past the wall (a cornice) is climbed OVER ITS OUTER EDGE: the body first moves out until it is clear of it, and only
            // then rises (otherwise it goes up through the slab it is hanging under).
            float shift = Mathf.Max(0.0f, Vector3.Dot(ledge.Lip + ledge.Normal * (BodyRadius + 0.03f) - start, ledge.Normal));
            float outFraction = shift > 0.02f ? 0.15f : 0.0f;
            Vector3 clear = start + ledge.Normal * (shift * Mathf.SmoothStep(0.0f, 1.0f, outFraction > 0.0f ? Mathf.Clamp01(u / outFraction) : 1.0f));
            clear.y = start.y;
            float rise = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((u - outFraction) / (RiseFraction - outFraction)));
            float step = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((u - RiseFraction) / (1.0f - RiseFraction)));
            Vector3 hang = new Vector3(clear.x, Mathf.Lerp(start.y, ledge.Height, rise), clear.z);
            Vector3 over = new Vector3(Mathf.Lerp(clear.x, ledge.Stand.x, step), hang.y, Mathf.Lerp(clear.z, ledge.Stand.z, step));
            // A small lift while stepping, so the feet clear the lip instead of dragging along it.
            // A thin-topped ledge (a parapet) has its standing ground BELOW the top: come down only once the feet are past the far edge.
            over.y = Mathf.Lerp(over.y, MantleEndY(ledge), Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((step - 0.78f) / 0.22f)));
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
            _locomotion?.SetClimbWall(_planePoint, _planeNormal, u < RiseFraction ? 1.0f : 0.0f);
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
            _autoNeedsRelease = true;
            Vector3 stand = _mantle.Stand;
            if (MantleEndY(_mantle) <= _mantle.Stand.y + 0.001f
                && Physics.Raycast(stand + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                stand.y = hit.point.y;
            }
            stand.y = MantleEndY(_mantle) > _mantle.Stand.y + 0.001f ? MantleEndY(_mantle) : stand.y;
            transform.position = stand;
            if (_controller != null)
            {
                _controller.enabled = true;
            }
            Physics.SyncTransforms();

            _handsWeight = 0.0f;
            _locomotion?.SetClimbWall(Vector3.zero, Vector3.forward, 0.0f);
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
