using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Wall climbing: hold sprint+W against a wall to grab on, W/S to climb
    /// up/down once attached, Space hops off with an outward push. Ported
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

        /// <summary>
        /// Checked every frame NOT already climbing (see PlayerInputReader's
        /// own dispatch) -- grabs on the instant sprint+W are BOTH held,
        /// stamina remains, and a forward raycast finds a real wall within
        /// arm's reach. Deliberately requires sprint (not just W) so
        /// brushing up against a building while walking normally never
        /// accidentally starts a climb -- matches player.gd's own comment on
        /// _try_start_climb ("hold shift and W... to climb").
        /// </summary>
        public bool TryStart(bool sprintHeld, bool forwardHeld, float stamina)
        {
            if (IsClimbing || IsToppingOut || !sprintHeld || !forwardHeld || stamina <= 0.0f)
            {
                return false;
            }

            float originHeight = (_locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight)
                * GrabOriginHeightFraction;
            Vector3 origin = transform.position + Vector3.up * originHeight;
            if (!WallInDirection(origin, transform.forward, out Vector3 normal))
            {
                return false;
            }

            IsClimbing = true;
            _wallNormal = normal;
            _anchorX = transform.position.x;
            _anchorZ = transform.position.z;
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
        public void Tick(bool jumpPressed, bool climbUpHeld, bool climbDownHeld, PlayerVitals vitals, float deltaTime)
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
            if (climbUpHeld && !climbDownHeld)
            {
                // Reached the top? Checked BEFORE moving this frame: a
                // forward raycast from above the player's own current grip,
                // same technique TryStart used to grab on in the first
                // place, just from a higher vantage point -- once IT stops
                // finding the wall, there is no more surface left to climb.
                if (!WallInDirection(transform.position + Vector3.up * TopCheckHeight, -_wallNormal, out _))
                {
                    BeginTopOut();
                    return;
                }
                direction = 1;
            }
            else if (climbDownHeld && !climbUpHeld)
            {
                direction = -1;
            }

            Vector3 move = new Vector3(
                _anchorX - transform.position.x,
                direction * ClimbSpeed * deltaTime - slip * deltaTime,
                _anchorZ - transform.position.z);
            _controller.Move(move);

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

        /// <summary>
        /// Reached the top of a climbable wall (see Tick's own check) --
        /// freezes movement and fires the one-shot mantle trigger.
        /// character.gd's own play_climb_top_out carries real hip-position
        /// root motion that visually sells the transition; PlayerInputReader
        /// polls <see cref="TickTopOut"/> every frame this is true, which
        /// watches the Animator directly for that same clip finishing --
        /// the closest Unity equivalent to player.gd's own
        /// anim_player.animation_finished listener -- to know the exact
        /// moment to actually relocate the real CharacterController.
        /// </summary>
        private void BeginTopOut()
        {
            IsClimbing = false;
            IsToppingOut = true;
            _locomotion?.SetVerticalVelocity(0.0f);
            if (_animator != null)
            {
                _animator.SetBool(ClimbingParameter, false);
                _animator.SetTrigger(ClimbTopOutParameter);
            }
            else
            {
                CompleteTopOut();
            }
        }

        /// <summary>
        /// Called every frame by PlayerInputReader while IsToppingOut --
        /// no-ops until the ClimbTopOut animator state has actually finished
        /// playing (normalizedTime reaching 1), then relocates the body.
        /// </summary>
        public void TickTopOut()
        {
            if (!IsToppingOut || _animator == null)
            {
                return;
            }
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName("ClimbTopOut") || info.normalizedTime < 1.0f)
            {
                return;
            }
            CompleteTopOut();
        }

        /// <summary>
        /// Actually relocates the real CharacterController up onto the
        /// ledge once the mantle clip finishes -- MantleUp/MantleForward are
        /// a starting estimate, not trusted blindly (roof/ledge thickness
        /// varies by building), so a real downward raycast from that
        /// estimate snaps the final position onto whatever surface is
        /// actually there. Direct transform writes, not CharacterController
        /// .Move() -- matches player.gd's own _complete_climb_top_out doing
        /// two raw global_position assignments rather than a further
        /// move_and_slide, so a physics collision resolving the estimate
        /// away from where the raycast actually snaps it never happens.
        ///
        /// TOP-OUT TRIGGERS CLIMB_TOP_CHECK_HEIGHT ABOVE THE CURRENT GRIP,
        /// not the player's own current height -- a real bug player.gd's own
        /// comment names outright: an earlier version only added MantleUp
        /// (a small ledge-clearance nudge) to the CURRENT position, landing
        /// well short of the actual ledge in open air, and the downward
        /// raycast then found nothing to snap onto.
        /// </summary>
        private void CompleteTopOut()
        {
            IsToppingOut = false;
            Vector3 mantleTarget = transform.position
                + Vector3.up * (TopCheckHeight + MantleUp)
                - _wallNormal * MantleForward;
            transform.position = mantleTarget;

            if (Physics.Raycast(mantleTarget + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3.5f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 snapped = transform.position;
                snapped.y = hit.point.y;
                transform.position = snapped;
            }

            _locomotion?.SetVerticalVelocity(0.0f);
            _wallNormal = Vector3.zero;
        }
    }
}
