using CityX.Core;
using CityX.World;
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
    // EARLIER THAN DEFAULT, ON PURPOSE -- Bug, found live: Unity
    // does not guarantee LateUpdate order between separate MonoBehaviours
    // on the same GameObject (this project's own history already hit this
    // once for GrappleSwing, see SuppressImplausibleArmSnaps's own doc
    // comment). PoseDistortionWatchdog is meant to observe this class's
    // OWN corrected output, never a pre-correction raw value -- if the
    // watchdog's LateUpdate happened to run BEFORE this one on some frame,
    // it would record the raw, uncorrected bone rotation as its own
    // "previous," and the FOLLOWING frame's comparison (now against the
    // real, corrected value) could then read as a fresh implausible jump
    // that was never actually visible to a player -- a false positive
    // indistinguishable, from the log alone, from a real bug. Forcing this
    // class to run first guarantees the watchdog only ever sees this
    // class's own already-corrected result.
    [DefaultExecutionOrder(-100)]
    public sealed partial class PlayerLocomotion : MonoBehaviour
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

        [Tooltip("How fast the body turns to face the camera while moving (camera-facing movement), in degrees per second.")]
        public float TurnDegreesPerSecond = 720.0f;

        /// <summary>
        /// GTA style (true, MovementTurn): out of an aim, a lock-on or a cast the body turns to where it goes. False: the old camera-facing
        /// movement, where the keys side-step and back-step (/movement classic, to compare; owner, 2026-11-01).
        /// </summary>
        public static bool FaceMovement = true;

        /// <summary>The body faces the camera's yaw, whatever the keys: aiming, firing, casting (a spell, a staff or a wand), or locked on.</summary>
        private bool FacesTheCamera(LocomotionInput input) => AimsWithTheCamera(input) || input.LockedOn || _castPoseFraction > 0.0f;

        private bool AimsWithTheCamera(LocomotionInput input) =>
            input.Aiming || _gunFireHold > 0.0f || (StaffCasting && Staff != null) || WandPointing;

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

        /// <summary>
        /// The share of HorizontalAcceleration the feet can actually use on
        /// this ground, 1 when dry. WET GROUND COSTS TRACTION -- GTA's wet
        /// roads, Breath of the Wild's slick rock: in the rain, and for a good
        /// while after it (the wetness is the city's, and dries slowly), the
        /// body takes longer to get up to speed and longer to stop, which reads
        /// as a slide. Only on the ground (in the air the same easing is air
        /// control), and softened under cover where the ground is barely wet.
        /// </summary>
        private float WetTraction()
        {
            if (_controller == null || !_controller.isGrounded ||
                (!CurrentWeather.IsRaining && CurrentWeather.Wetness <= 0.0f))
            {
                return 1.0f;
            }
            return Weather.GroundTraction(CurrentWeather.SurfaceWetness(transform.position));
        }

        [Header("Scale")]
        [Tooltip(
            "Standing height in metres. The default is the CITY X body's own measured height, " +
            "floor to the crown of the head sphere. Change it to scale the whole character -- " +
            "this is the one number that decides whether the city feels right.")]
        public float StandingHeight = StandingHeightDefault;

        /// <summary>
        /// How many times its own size the body is (Grow and Shrink, BodySizeAbility: its transform's scale; 1 otherwise). The capsule and the
        /// model scale with the transform by themselves; the numbers here that are metres or metres a second are scaled by it where they are used.
        /// </summary>
        public float BodyScale => Mathf.Max(0.01f, transform.lossyScale.y);

        /// <summary>The body's height in the world: StandingHeight times BodyScale (a giant's chest is 10 times as high).</summary>
        public float WorldHeight => StandingHeight * BodyScale;

        /// <summary>
        /// How much faster a resized body moves and jumps: the square root of its size (BodySize.SpeedFactor's reasoning: gravity is the same
        /// for a giant, so its natural pace goes with the square root of its length). The walk and run cycles play that much slower
        /// (BodySizeAbility's StrideRate), so the feet keep to the ground.
        /// </summary>
        public float SizeSpeedFactor => Mathf.Sqrt(BodyScale);

        /// <summary>How much of its pace the body keeps, 0 to 1: a chill slows it, a freeze stops it (Chill, docs/FROST.md). On foot and flying.</summary>
        public float SlowFactor { get; set; } = 1.0f;

        /// <summary>True while Grow or Shrink has the body at another size: climbing, vaulting and foot placement, tuned in metres, rest.</summary>
        public bool IsResized => Mathf.Abs(BodyScale - 1.0f) > BodySize.NormalTolerance;

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
        private Transform _leftUpperArm;
        private Transform _head;
        private Transform _neck;
        private Transform _rightLowerArm;
        private Transform _leftLowerArm;
        private Transform _leftUpperLeg;
        private Transform _rightUpperLeg;
        private Transform _leftLowerLeg;
        private Transform _rightLowerLeg;
        private MeleeAttacker _melee;
        private SoulsSwordCombat _soulsSword;
        private float _verticalVelocity;

        private float _appliedHeight = -1.0f;

        /// <summary>The smoothed horizontal velocity HorizontalAcceleration eases toward its target -- see that field's own doc comment.</summary>
        private Vector3 _horizontalVelocity;

        /// <summary>How fast the body is turning toward where it goes, degrees per second (MovementTurn.Step).</summary>
        private float _turnVelocity;

        /// <summary>Whether a player drives this body (it has a PlayerInputReader, which enemies do not), so its turns ease round.</summary>
        private bool _easesTurns;

        /// <summary>Set every Tick from LocomotionInput.Aiming/AimPitchDegrees, applied later by ApplyGunAimPose.</summary>
        private bool _isAimingGun;

        /// <summary>
        /// Set every Tick from LocomotionInput.GunEquipped -- see
        /// ApplyGunAimPose's own doc comment on why the HEAD, specifically,
        /// needs to know this separately from _isAimingGun: a pistol stays
        /// in hand (and its own walk clip's head cant stays visible) for as
        /// long as it is equipped, whether or not right-click also happens
        /// to be held.
        /// </summary>
        private bool _gunEquipped;

        /// <summary>
        /// A sword swing PLAYED FROM A CLIP is a torso movement first and an arm movement second, so while one (or a guard clip) plays the
        /// spine and hips follow the clip instead of being held at identity (see LateUpdate). Walking and standing keep the lock the
        /// locomotion clips need, which is why this is not simply "sword equipped".
        ///
        /// NOT WHILE THE ATTACK IS POSED (SwordStrikes, every attack since 2026-10-13): the pose turns the hips and the spine itself, and the
        /// clip under it is only the sword walk/idle, whose spine is bent forward over the sword. Following it added that stoop to every swing
        /// and guard, a hunchback bowing into each cut (owner, 2026-10-16: "make the character is not turned in any awkward way").
        /// </summary>
        public bool TorsoFollowsClip
        {
            get
            {
                if (_souls == null)
                {
                    _souls = GetComponent<SoulsSwordCombat>();   // added on the first equip
                }
                return _souls != null && _souls.IsEquipped && (_souls.IsCommitted || _souls.IsBlocking) && !SwordPoseActive;
            }
        }

        /// <summary>
        /// A clip that moves the whole body is playing (a dance, push-ups, a spell's whole-body move): the procedural sword hold and stance
        /// stand aside for it.
        /// </summary>
        public bool ClipOwnsTheBody
        {
            get
            {
                if (_animator == null || !_animator.enabled || _animator.runtimeAnimatorController == null)
                {
                    return false;
                }
                return _animator.GetBool(DancingParameter) || _animator.GetBool(BreakdancingParameter) || PushUpOwnsTheBody || SpellClipOwnsTheTorso;
            }
        }

        /// <summary>
        /// /pushups (2026-10-15): the whole body follows the clips -- the hips pitch down to a plank and back up, which the lock that holds the
        /// hips upright for walking would otherwise undo, leaving the body standing with only its arms pumping (seen in PushUpCapture). True
        /// from going down until the body is back in Locomotion, on this body and on other players' copies (the phase is sent).
        /// </summary>
        public bool PushUpOwnsTheBody
        {
            get
            {
                if (_animator == null || !_animator.enabled || _animator.runtimeAnimatorController == null)
                {
                    return false;
                }
                if (_animator.GetInteger(PushUpPhaseParameter) != 0)
                {
                    return true;
                }
                AnimatorStateInfo now = _animator.GetCurrentAnimatorStateInfo(0);
                return now.IsName("PushUpEnter") || now.IsName("PushUpLoop") || now.IsName("PushUpExit");
            }
        }

        /// <summary>
        /// Self Smite (2026-10-15): its clip throws the arms up and slams them down into a crouch, a whole-body move, so the torso follows the
        /// clip as it does a sword swing's (the same share, TorsoFollowStrength) while that state plays, on this body and on other players' copies.
        /// </summary>
        public bool SpellClipOwnsTheTorso
        {
            get
            {
                if (_animator == null || !_animator.enabled || _animator.runtimeAnimatorController == null)
                {
                    return false;
                }
                return _animator.GetCurrentAnimatorStateInfo(0).IsName(SelfSmiteMove.StateName)
                       || (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName(SelfSmiteMove.StateName));
            }
        }

        private SoulsSwordCombat _souls;
        private float _torsoFollowBlend;
        private float _locomotionSpineBlend;

        /// <summary>
        /// True while the plain walk/run/idle blend is playing: those are the Godot character's own baked clips (GodotMotionBaker), made for this one-spine-bone body,
        /// so the spine keeps their forward lean and twist instead of being held upright. (The hips stay locked: the Godot clips carry no hips rotation, as
        /// character.gd's retarget() guaranteed.) Every other state still plays a retargeted Mixamo clip that spreads its lean over three spine bones this rig does not
        /// have, and keeps the lock -- measured, one light swing bowed this bone 64 degrees.
        /// </summary>
        public bool LocomotionClipsOwnTheSpine
        {
            get
            {
                if (_animator == null || !_animator.enabled || _animator.runtimeAnimatorController == null)
                {
                    return false;
                }
                bool nowLocomotion = _animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");
                return nowLocomotion && (!_animator.IsInTransition(0) || _animator.GetNextAnimatorStateInfo(0).IsName("Locomotion"));
            }
        }

        /// <summary>
        /// Standing in the plain walk/idle cycle, all but still, with nothing posed over it: no fists up, no flinch, no sword hold, no climbing
        /// limbs, no baked move, no gun aimed or carried, not sitting, no clip owning the body (a limp ragdoll switches the Animator off, so
        /// it is never this). The HUD portrait keeps a pose only when the body is in it (owner, 2026-10-24: "there should be no fists up pose
        /// in the portrait or any poses besides the plain one").
        /// </summary>
        public bool ShowsPlainPose =>
            LocomotionClipsOwnTheSpine && CurrentSpeed < PlainPoseMostSpeed
            && !FistPoseActive && !FlinchPoseActive && !SwordPoseActive && !ClimbLimbsActive && BakedPoseWeight <= 0.001f
            && !IsAimingGun && !HoldsRifle && !IsSitting && !ClipOwnsTheBody && !TorsoFollowsClip;

        /// <summary>The most a body may be moving, m/s, and still count as standing in <see cref="ShowsPlainPose"/>.</summary>
        public const float PlainPoseMostSpeed = 0.3f;

        /// <summary>How fast the torso takes up (and gives up) the clip's own hips/spine motion, per second.</summary>
        public const float TorsoFollowBlendPerSecond = 10.0f;

        /// <summary>
        /// OFF since the body became separate tubes (2026-10-10): the torso is one rigid tube on the hips, and a spine leaning with the walk
        /// swung the arms and head away from it (and, with the torso on the spine instead, swung the torso's bottom out behind the legs: owner,
        /// "the legs aren't attached to the torso ... the butt is sticking out too much"). The whole upper body now moves with the hips, as one.
        /// </summary>
        public const bool LocomotionSpineLeans = false;

        /// <summary>
        /// How much of the clip's torso motion a swing keeps, 0..1. The rig has ONE spine bone where Mixamo's
        /// skeleton spreads the same lean over three, and the bone is handed the clip's share for itself alone --
        /// measured, a light swing bows it 64 degrees, which reads as a deep stoop. Two thirds keeps the weight
        /// of the swing without the bow. Lives in the WeaponGripSettings asset (a slider on it, live in Play Mode).
        /// </summary>
        public static float TorsoFollowStrength
        {
            get => WeaponGripSettings.Current.TorsoFollowStrength;
            set => WeaponGripSettings.Current.TorsoFollowStrength = Mathf.Clamp01(value);
        }

        /// <summary>How far, in metres, the support palm is from where it is meant to be (0 when nothing is held).</summary>
        public float SupportHandError
        {
            get
            {
                if (_supportAnchor == null || !TryGetSupportPalm(out Vector3 palm))
                {
                    return 0.0f;
                }
                return Vector3.Distance(palm, _supportAnchor.TransformPoint(_supportLocalPoint));
            }
        }

        private float _lastTickDeltaTime;

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
        private static readonly int Sword1HEquippedParameter = Animator.StringToHash("Sword1HEquipped");
        private static readonly int GunEquippedParameter = Animator.StringToHash("GunEquipped");
        private static readonly int JetpackThrustingParameter = Animator.StringToHash("JetpackThrusting");
        private static readonly int DancingParameter = Animator.StringToHash("Dancing");
        private static readonly int BreakdancingParameter = Animator.StringToHash("Breakdancing");
        private static readonly int PushUpPhaseParameter = Animator.StringToHash("PushUpPhase");

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

        /// <summary>On the ground, or being lifted onto a step (PlayerLocomotion.StepUp.cs): a stride, not a jump.</summary>
        public bool IsGrounded => _controller != null && (_controller.isGrounded || (_steppingUp && _controller.enabled));

        /// <summary>
        /// CharacterController.Move's own return value from the most recent
        /// Tick -- discarded everywhere else in this file, kept here so a
        /// caller (PlayerInputReader's own jetpack-crash detection) can ask
        /// "did this frame's move actually get blocked sideways" without
        /// re-deriving it from a position-delta comparison the way
        /// GrappleSwing's own wall-stick fix does. CollisionFlags.Sides
        /// specifically -- Above/Below are the floor/ceiling a jetpack
        /// flying under its own thrust brushes against constantly and
        /// harmlessly; Sides is an actual wall.
        /// </summary>
        public CollisionFlags LastMoveCollisionFlags { get; private set; }

        /// <summary>
        /// A real, direct contact against a wall-like surface (a building,
        /// a window -- anything roughly vertical) during THIS frame's own
        /// Move -- set from OnControllerColliderHit's own per-contact
        /// normal, not derived from CollisionFlags. Bug report:
        /// "the jetpack sideways fall damage thing still hasnt worked...
        /// make it do it upon my playermodel coming into contact with the
        /// wall or object" -- CollisionFlags.Sides (LastMoveCollisionFlags
        /// above) is documented by Unity as an approximate summary of the
        /// whole Move sweep and proved unreliable for this in real play
        /// (a glancing or corner hit can move the capsule sideways without
        /// ever setting the Sides bit). OnControllerColliderHit instead
        /// fires once per actual contact the capsule makes, with that
        /// contact's own real surface normal, which is the more direct
        /// signal for "what did the body really touch." Moving this read
        /// to before OnControllerColliderHit's existing rigidbody-null
        /// early return also matters on its own: a building has no
        /// Rigidbody at all, so that early return used to skip ordinary
        /// static geometry entirely for whatever ran after it.
        /// </summary>
        public bool HitWallThisFrame { get; private set; }

        /// <summary>The world-space contact normal from HitWallThisFrame's own hit, valid only when it is true.</summary>
        public Vector3 LastWallHitNormal { get; private set; }

        /// <summary>
        /// Below this, a contact's own normal is closer to "floor/ceiling"
        /// than "wall" -- Vector3.up dot a vertical wall's normal is 0;
        /// dot a level floor's is 1. 0.5 is roughly a 60 degree surface,
        /// comfortably steeper than any real floor/ramp this project builds
        /// (TerrainField.MaxSlope tops out well under 45 degrees) while
        /// still catching a near-vertical wall approached at a shallow
        /// glancing angle.
        /// </summary>
        public const float WallNormalMaxUpDot = 0.5f;

        /// <summary>
        /// A hard floor no legitimate ground ever sits below --
        /// TerrainField.SeaLevel is 0 and TotalRelief tops out at 72, so
        /// nothing in this world's own terrain, streets or building podiums
        /// is ever built anywhere near this low. Bug report,
        /// repeated after extensive investigation could not pin a single
        /// reproducible cause on any of four different real-flow tests
        /// (direct Tick, the real PlayerInputReader dispatch, a real
        /// single-mode scene load with an existing character, and one with
        /// a brand new character through the full intro cutscene -- all
        /// four settled on the ground cleanly): "make me fucking spawn
        /// above ground its still happening." This is the safety net
        /// instead of the diagnosis -- whatever the real cause turns out to
        /// be (a streaming race the tests never happened to hit, a stale
        /// scene, something neither investigation found), a body this far
        /// below the world gets rescued every single Tick rather than left
        /// to fall forever.
        /// </summary>
        public const float FallThroughWorldY = -20.0f;

        /// <summary>
        /// Snaps back onto the real ground directly above wherever the body
        /// currently is, using TerrainField.ContinuousHeightAt -- a pure
        /// function of world coordinates that answers correctly regardless
        /// of whether CityStreamer has actually built that chunk's own mesh
        /// yet, unlike a downward raycast, which would find nothing at all
        /// during exactly the kind of streaming-race window this exists to
        /// recover from. Reads CurrentWorld rather than the streamer
        /// directly -- CityX.Character has no reference to CityX.View (see
        /// that assembly's own .asmdef), so CurrentWorld is the one thing
        /// bridging the two, the same shape CityX.Core.PlaySession already
        /// uses to bridge the title and world scenes.
        /// </summary>
        private void RecoverFromFallingThroughTheWorld()
        {
            TerrainField terrain = CurrentWorld.Instance?.Terrain;
            if (terrain == null)
            {
                return;
            }

            Vector3 position = transform.position;
            float safeY = (float)terrain.ContinuousHeightAt(position.x, position.z) + 2.0f;

            _controller.enabled = false;
            transform.position = new Vector3(position.x, safeY, position.z);
            _controller.enabled = true;
            // The CharacterController desync bug: a raw transform.position
            // write is invisible to the very next Move() call without this
            // -- see IntroCutscene.Begin's own identical comment.
            Physics.SyncTransforms();
            _verticalVelocity = 0.0f;
        }

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

        /// <summary>The body's own vertical velocity (up positive), as the last Tick left it.</summary>
        public float VerticalVelocity => _verticalVelocity;

        /// <summary>
        /// Multiplies gravity while the body is in the air: 1 is normal. The aerial sword spin turns it down (as Link's does in Smash,
        /// where it is 0.55) so the spin climbs and then hangs rather than rising and falling like a jump. Whoever sets it must put
        /// it back to 1.
        /// </summary>
        public float GravityScale { get; set; } = 1.0f;

        /// <summary>
        /// While true and in the air, horizontal velocity is kept rather than eased toward the keys' target, and the keys only steer it (see
        /// the horizontal step in Tick). The aerial sword spin sets it so the body keeps flowing the way it was going, and clears it on landing.
        /// </summary>
        public bool KeepsAirMomentum { get; set; }

        /// <summary>While true the keys ask for no movement (a rooted wind-up, Octaslash's charge); gravity and the rest still apply.</summary>
        public bool MovementRooted { get; set; }

        /// <summary>While true the body moves at exactly <see cref="ForcedVelocity"/>, gravity off and the keys ignored (Octaslash's dash).</summary>
        public bool HasForcedVelocity { get; private set; }

        public Vector3 ForcedVelocity { get; private set; }

        public void ForceVelocity(Vector3 velocity)
        {
            ForcedVelocity = velocity;
            HasForcedVelocity = true;
        }

        public void ReleaseForcedVelocity()
        {
            HasForcedVelocity = false;
            ForcedVelocity = Vector3.zero;
        }

        /// <summary>How hard the keys can push a body that is keeping its momentum, in metres per second squared (a full stop from a sprint takes about half a second).</summary>
        public const float AirMomentumSteerAcceleration = 14.0f;

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
            FireTrigger(DashParameter);
        }

        /// <summary>
        /// Raised every time this body fires a one-shot Animator trigger
        /// (Jump, Dash) -- the hook NetworkedAnimatorSync listens to so the
        /// same trigger can be replayed on every other player's copy of this
        /// body. Remote copies never tick PlayerLocomotion (their
        /// PlayerInputReader is disabled), so this never fires for a copy
        /// that is merely mirroring someone else.
        /// </summary>
        public event System.Action<int> AnimatorTriggerFired;

        /// <summary>Sets the trigger locally and tells any listener, in one place so the two can't drift apart.</summary>
        private void FireTrigger(int parameterHash)
        {
            if (_animator != null)
            {
                _animator.SetTrigger(parameterHash);
            }
            AnimatorTriggerFired?.Invoke(parameterHash);
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
            CityX.World.GlassFracture.LetThrough(_controller);   // broken glass is walked through, not over
            _easesTurns = GetComponent<PlayerInputReader>() != null;
            _animator = GetComponentInChildren<Animator>();
            _hips = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            _spine = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            _leftFoot = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            _rightFoot = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            _handL = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            _handR = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            _rightUpperArm = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            _leftUpperArm = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            _head = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            _neck = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            _rightLowerArm = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            _leftLowerArm = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            _leftUpperLeg = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg) : null;
            _rightUpperLeg = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) : null;
            _leftLowerLeg = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg) : null;
            _rightLowerLeg = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) : null;
            _melee = GetComponent<MeleeAttacker>();
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
        ///
        /// THE HIPS/SPINE IDENTITY LOCK STAYS UNCONDITIONAL -- a plain
        /// absolute assignment (= identity) every frame, never a read of
        /// the bone's own current value, so it cannot compound and was
        /// never the failure mode described below. It is also load-bearing
        /// WHILE FROZEN, by original design (see GrappleSwing.Attach's own
        /// comment: "PlayerLocomotion itself stays enabled... it owns
        /// feet-clamp/hips-freeze even then") -- a mid-stride clip's own
        /// hips retargeting wiggle is exactly what this exists to hold
        /// still, and that wiggle does not stop needing to be held still
        /// just because the Animator stopped refreshing it. AN EARLIER
        /// VERSION OF THIS METHOD GATED THE WHOLE BODY, THIS INCLUDED, ON
        /// the Animator being enabled -- confirmed live as a real
        /// regression: it silently let hips/spine sit at whatever
        /// off-identity wiggle the last live frame happened to leave them
        /// at for the WHOLE of a swing or console freeze, instead of
        /// locked flat, which is a real, if subtle, source of exactly the
        /// "distorted" look reported live.
        ///
        /// EVERYTHING ELSE HERE STAYS GATED ON THE ANIMATOR ITSELF BEING
        /// ENABLED, not on any one caller remembering to suspend any one
        /// piece of it. TWO SEPARATE REAL BUGS were found living in this
        /// exact method (ClampFeetAboveGround's own doc comment;
        /// ApplyCastingPose's own doc comment) with the identical root
        /// shape: something here reads a bone's CURRENT value and
        /// recomputes/re-layers onto it every frame, which is only ever
        /// safe because the Animator overwrites that same bone with a
        /// clean pose earlier in the very same frame, every frame, while
        /// it is enabled. A live Animator is what makes THOSE corrections
        /// well-defined in the first place -- without one refreshing the
        /// pose, "clamp the feet to the ground" and "raise the cast arm
        /// off its current rotation" are not stale, they are MEANINGLESS,
        /// since there is no live pose left to correct.
        /// </summary>
        private void LateUpdate()
        {
            // A copy of another player's body is never Ticked (nothing drives it): its impact freeze is counted down here instead.
            if (_impactFrozen && _freezeTickedFrame != Time.frameCount)
            {
                TickImpactFreeze(Time.deltaTime);
            }
            // ...and its crouch follows the owner's (PlayerLocomotion.Crouch.cs).
            if (_crouchTickedFrame != Time.frameCount)
            {
                FollowRemoteCrouch(Time.deltaTime);
                ShowCrouch();
            }

            // Only a live Animator has a torso pose worth following; a frozen or ragdolled one is held flat.
            bool animatorLive = _animator != null && _animator.enabled;
            float torsoTarget = !animatorLive ? 0.0f
                : PushUpOwnsTheBody ? 1.0f
                : TorsoFollowsClip || SpellClipOwnsTheTorso ? Mathf.Clamp01(TorsoFollowStrength)
                : 0.0f;
            _torsoFollowBlend = Mathf.MoveTowards(_torsoFollowBlend, torsoTarget, TorsoFollowBlendPerSecond * Mathf.Max(_lastTickDeltaTime, 0.0f));
            _locomotionSpineBlend = Mathf.MoveTowards(
                _locomotionSpineBlend, (animatorLive && LocomotionSpineLeans && LocomotionClipsOwnTheSpine) ? 1.0f : 0.0f,
                TorsoFollowBlendPerSecond * Mathf.Max(_lastTickDeltaTime, 0.0f));
            if (!InCarSeat && !IsSitting)
            {
                if (_hips != null)
                {
                    // Crouched, the hips take the crouch's lean (the whole upper body with them, as one piece): its weight is the duck's.
                    float hipsFollow = Mathf.Max(_torsoFollowBlend, animatorLive ? _crouch.Shown : 0.0f);
                    _hips.localRotation = Quaternion.Slerp(Quaternion.identity, _hips.localRotation, hipsFollow);
                }
                if (_spine != null)
                {
                    _spine.localRotation = Quaternion.Slerp(Quaternion.identity, _spine.localRotation, Mathf.Max(_torsoFollowBlend, _locomotionSpineBlend));
                }
            }

            ApplyBodyLean(animatorLive);

            if (_animator == null || !_animator.enabled)
            {
                return;
            }

            // Only guarded while the Animator is actually live: the whole
            // point is to stop a bad Mecanim transition from ever reaching
            // the bone in the first place, so a subsequent console/grapple
            // freeze captures the good, corrected pose. Bug, found
            // live TWICE: an unconditional placement (matching hips/spine
            // above) was tried both to keep tracking data fresh through an
            // ownership window and to let a synthetic test-injected snap
            // survive -- but "tracking" a bone while GrappleSwing/
            // MeleeAttacker are the ones actively driving it captures THEIR
            // pose, not the Animator's, which is the wrong reference frame
            // entirely for judging a future Animator transition (broke
            // RepeatedlyFiringAndReleasingNeverDriftsTheCapturedArmBase: a
            // rapid re-fire's freshly re-evaluated Animator pose looked
            // "implausible" only because it was being compared against the
            // FIRST swing's own leftover reach pose). Gating the call here
            // instead keeps this simple and correct: nothing legitimately
            // moves these bones frame-to-frame while the Animator is
            // disabled and GrappleSwing does NOT own them (a plain console
            // freeze), so there is nothing useful to track in that window
            // either -- only SuppressImplausibleArmSnapsNow, called
            // explicitly at the one moment (Attach/Open's own forced
            // Update(0f)) that needs protection despite the Animator being
            // about to go dark, needs to run outside this gate.
            SuppressImplausibleArmSnaps();

            if (!InCarSeat && !IsSitting)
            {
                ClampFeetAboveGround();
            }
            // In a push-up, the body tipped up about the toes until the palms are on the floor (PlayerLocomotion.PushUps.cs).
            ApplyPushUpGrounding();
            // Moving at an angle to the facing, the legs turned toward the travel and the chest kept on the aim (PlayerLocomotion.Warp.cs).
            ApplyOrientationWarp();
            // A sword cut's hips turn first (the legs are re-solved under them by the foot IK, right after).
            ApplySwordHips();
            // The feet on the real ground (hip drop, locks, sole tilt, a sword fight's stepping): right after the clamp, which it builds on,
            // and before every pose that only touches the upper body. See PlayerLocomotion.FootIK.cs.
            ApplyFootIK();
            ApplyHeadForward();
            ApplySwordHeadCarriage();
            // Thrown by a blow (HitReactions): the spine and head, before the arm poses are solved from the thrown shoulders.
            ApplyFlinchPose();
            ApplyDrivingWheelPose();
            ApplyCastingPose();
            ApplyGunAimPose();
            // The sword attacks (SwordStrikes): the sword arm, blade and torso; before the support hand, which follows the grip they move.
            ApplySwordPose();
            ApplySupportHand();
            // The sword arms round the body, not through it (ElbowClearance): after the support hand has its grip.
            KeepArmsOutOfTorso();
            ApplyBakedPose();
            ApplyFistPose();
            // A raised shield has the left arm (PlayerLocomotion.Shield.cs); lowered, the shield rides the forearm wherever it is.
            ApplyShieldPose();
            // A staff in the right hand: carried upright, thrust out to cast (PlayerLocomotion.Staff.cs).
            ApplyStaffPose();
            // A flashlight in the right hand: carried low, held out and pointed where the player looks when lit (PlayerLocomotion.Torch.cs).
            ApplyTorchPose();
            // A wand in the right hand: carried low, pointed at the crosshair to shoot or cast (PlayerLocomotion.Wand.cs).
            ApplyWandPose();
            ApplyClimbLimbs();
            ApplyClimbWallLimbs();
            ApplyLedgeHands();
            ApplyStraightWrists();
            // Both hands pushed out flat against a raised Arcane Barrier (PlayerLocomotion.Barrier.cs): after the straight wrists, which it bends.
            ApplyBarrierPose();
            // A bow in the left hand: the bow arm out along the aim, the string hand nocking and drawing (PlayerLocomotion.Bow.cs).
            ApplyBowPose();
            // The fingers close round whatever each hand holds (HandGrip), last: they hang off the posed hands.
            ApplyFingerGrips();
            if (IsSitting)
            {
                ApplySittingPose();
            }
        }

        /// <summary>
        /// EVERY ARM BENDS AT THE ELBOW AND NOWHERE ELSE: an empty hand is held straight along its forearm. The clips turn the wrist through tens of degrees, and on this
        /// body (a hand is just the last stretch of a tube) that reads as a second and third bend in the arm: a kinked, snaking limb in walks, runs and above all punches.
        /// The owner asked for one bend only. A hand that holds something keeps the clip's wrist (a pistol or a sword is aimed through it); the leaf fingertip bone is the
        /// hand's only child when it is empty.
        /// </summary>
        private void ApplyStraightWrists()
        {
            // "Empty" means nothing MOUNTED on the hand: the finger bones and the fingertip leaf are the rig's own (PlayerLocomotion.Fingers.cs).
            // A baked pose that turns the wrists itself (a reload's free hand, cupping a magazine) keeps them, by its weight.
            float keep = _bakedPoseWeight;
            if (_handL != null && !LeftHandHolds)
            {
                _handL.localRotation = keep > 0.0f ? Quaternion.Slerp(Quaternion.identity, _handL.localRotation, keep) : Quaternion.identity;
            }
            if (_handR != null && !RightHandHolds)
            {
                _handR.localRotation = keep > 0.0f ? Quaternion.Slerp(Quaternion.identity, _handR.localRotation, keep) : Quaternion.identity;
            }
        }

        /// <summary>
        /// Bug report: opening the console mid-jump "plays the
        /// rest of the jump animation after the console freezes the rest
        /// of the game" -- a real, if cosmetic, side effect of no longer
        /// disabling the Animator on console-open at all (see
        /// DevConsoleView.Open's own doc comment for why that freeze was
        /// removed entirely). With the Animator left live but InputReader
        /// disabled, nothing calls Tick any more to update Speed/Grounded/
        /// etc, so whatever action was already playing (a jump, a fall)
        /// keeps running to its own natural end while the rest of the game
        /// -- movement, physics, everything else -- is genuinely paused,
        /// reading as visibly out of sync. Called once, the instant the
        /// console opens: forces the SAME parameters an ordinary standing-
        /// still frame would have (zero speed, zero move axes, grounded),
        /// so Mecanim's OWN normal blend tree eases into idle exactly the
        /// way it would if the player had simply let go of every key --
        /// an ordinary, safe transition, not a freeze, so the one-frame-
        /// implausible-snap risk this whole file's history is about never
        /// applies to it.
        /// </summary>
        public void SettleAnimatorToIdle()
        {
            if (_animator == null)
            {
                return;
            }
            _animator.SetFloat(SpeedParameter, 0.0f);
            _animator.SetFloat(MoveXParameter, 0.0f);
            _animator.SetFloat(MoveZParameter, 0.0f);
            _animator.SetBool(GroundedParameter, true);
            _animator.SetBool(ChargingJumpParameter, false);
        }

        /// <summary>
        /// Feeds the exact same Speed/MoveX/MoveZ/Grounded parameters
        /// ordinary WASD movement drives at the end of Tick, just computed
        /// from a velocity this class did not itself produce -- what lets
        /// GrappleSwing's own rope physics show up as a normal run/fall
        /// pose through Mecanim's already-proven blend tree, without
        /// GrappleSwing (or this class) ever touching a bone directly.
        ///
        /// THIS REPLACED DIRECT BONE ROTATION AS THE SWING'S WHOLE POSING
        /// STRATEGY -- see GrappleSwing's own class doc comment for the
        /// eight rounds of live pose-distortion bugs that came from the
        /// old approach (disabling the Animator and rotating arm/head/leg
        /// bones by hand every frame) and none from the rope physics
        /// itself. Reusing the Animator's own normal drive path instead
        /// means a swing carries zero new pose-related risk -- PROVIDED it
        /// is fed the same way ordinary movement feeds it. It very nearly
        /// was not: see ALWAYS FED AS STRAIGHT FORWARD's own doc comment
        /// just below for the real bug two earlier, more "faithful"
        /// versions of this method both had.
        /// </summary>
        public void DriveAnimatorFromExternalVelocity(Vector3 worldVelocity, bool grounded, float deltaTime)
        {
            if (_animator == null)
            {
                return;
            }

            // ALWAYS FED AS STRAIGHT FORWARD IN LOCAL SPACE -- never the
            // swing's own true direction, even smoothed and magnitude-
            // clamped. Bug, found live by PoseDistortionWatchdogPlayTests.
            // ARealVariedSwingNeverTriggersTheWatchdog, through TWO earlier
            // versions of this method that both still failed it: the first
            // fed the raw world velocity straight to Speed/MoveX/MoveZ; the
            // second eased it through _horizontalVelocity AND clamped its
            // magnitude to SprintSpeed. Neither was enough, and a control
            // test pinned down why: an ordinary long fall fed the exact
            // same smooth, continuous input this test uses (a sine-wave
            // move direction under a steadily rotating yaw) through
            // ordinary PlayerLocomotion.Tick -- never touching GrappleSwing
            // at all -- never tripped the watchdog, because ordinary Tick
            // also TURNS THE BODY to face wherever it is moving (see this
            // method's own "ONLY WHILE MOVING" block), which keeps the
            // LOCAL-space MoveX/MoveZ this Animator sees roughly stable
            // (mostly forward, small side variance) even while the WORLD
            // direction sweeps around. GrappleSwing never turns the body at
            // all -- there is no reason to while a rope decides where the
            // body actually goes -- so with the body held at a fixed
            // orientation, a swing's own steadily rotating world-space
            // velocity translates into MoveX/MoveZ sweeping through EVERY
            // local direction in a slow, continuous circle: forward, to
            // strafe-right, to backward, to strafe-left, and round again.
            // That is a local movement pattern no real WASD player's own
            // body-follows-movement behaviour can ever produce, and
            // Mecanim's own directional blend tree has at least one seam
            // somewhere in that full circle where crossing it snaps to a
            // visibly different pose within a frame or two (logged: 2680,
            // then 3655, then 7132 deg/s against the watchdog's own 1500
            // limit, each WORSE than the last across the two failed
            // attempts -- a strong sign the sweep itself, not the
            // magnitude or the smoothing rate, was always the actual
            // cause). The swing already looks and feels like flying
            // through the air at speed from the actual CharacterController
            // motion; the animation only ever needs to read as "moving
            // forward," not visually reconstruct the swing's true
            // direction, so always feeding local-forward costs nothing
            // real and removes the sweep -- and the seam it crosses --
            // entirely, the same way the SprintSpeed clamp removed the
            // out-of-range magnitude without needing to know exactly which
            // blend sample was misbehaving.
            float targetSpeed = Mathf.Min(
                new Vector3(worldVelocity.x, 0.0f, worldVelocity.z).magnitude, SprintSpeed);
            Vector3 targetHorizontal = transform.forward * targetSpeed;

            // EASED THROUGH _horizontalVelocity, THE SAME SMOOTHED FIELD
            // ORDINARY Tick USES -- so a swing starting or ending never has
            // to resync anything; Tick and this method are always easing
            // the exact same persistent value, at the same
            // HorizontalAcceleration ordinary WASD movement already trusts.
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetHorizontal, HorizontalAcceleration * deltaTime);

            Vector3 local = transform.InverseTransformDirection(_horizontalVelocity);
            _animator.SetFloat(SpeedParameter, _horizontalVelocity.magnitude);
            _animator.SetFloat(MoveXParameter, local.x);
            _animator.SetFloat(MoveZParameter, local.z);
            _animator.SetBool(GroundedParameter, grounded);
        }

        private BloodrageAbility _bloodrage;
        private bool _bloodrageLooked;

        /// <summary>This body's vitals, for the Movement Speed stat (looked up the first time it is needed).</summary>
        private PlayerVitals _statsVitals;

        /// <summary>
        /// Rotates `bone` in world space so the vector from its own
        /// position to `childPosition` (its child bone's CURRENT position,
        /// wherever the Animator/parent rotation just left it) points at
        /// `worldTargetDirection` instead of wherever it currently points.
        /// An ABSOLUTE target, not a relative delta -- see
        /// ApplySittingPose's own doc comment for why that matters here.
        /// </summary>
        private static void BendLimbToward(Transform bone, Vector3 childPosition, Vector3 worldTargetDirection)
        {
            Vector3 current = (childPosition - bone.position).normalized;
            Quaternion delta = Quaternion.FromToRotation(current, worldTargetDirection.normalized);
            bone.rotation = delta * bone.rotation;
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
        ///
        /// USED TO NEED SUSPENDING DURING A GRAPPLE SWING -- the old
        /// GrappleSwing directly rotated the leg bones every frame while a
        /// rope was attached, and this method's own "reset to zero, then
        /// recompute from the live foot position" pattern turned that
        /// continuous leg sway into a genuine runaway feedback loop (a real
        /// bug report's "distorted to lengths that shouldn't be possible":
        /// the visual rig's own offset climbed past 1000 METRES within
        /// about 20 frames). GrappleSwing was rebuilt from square one to
        /// never touch a bone at all (see its own class doc comment), so
        /// there is no more leg sway for this to runaway against -- a
        /// swing now looks exactly like an ordinary fall as far as this
        /// method is concerned, which it already handles safely every day.
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
                // NEVER TRUSTED UNCLAMPED -- see this method's own doc
                // comment on the runaway this guards against. 2m is well
                // past the worst calibration error ever actually measured
                // (charged_jump's own "over a metre") with real headroom,
                // while a hundred-metre-plus correction is never a
                // legitimate clip fix, only a symptom of something else
                // gone wrong.
                _animator.transform.localPosition = new Vector3(0.0f, Mathf.Min(penetration, MaxGroundClampCorrection), 0.0f);
            }
        }

        private const float MaxGroundClampCorrection = 2.0f;

        /// <summary>A body that moves further than this in one frame was put there, not run there.</summary>
        private const float TeleportMetres = 3.0f;

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
            // Big enough to walk up a kerb or a stair without jumping. Taller
            // steps, up to a quarter of the body, are the step up's
            // (PlayerLocomotion.StepUp.cs), which also cuts this to the room
            // over the head under a low ceiling.
            _controller.stepOffset = RestingStepOffset;

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
            // Used by ApplyGunAimPose (LateUpdate) to ease its own blend at
            // the SAME cadence Tick is actually being driven at -- Time.
            // deltaTime would silently disagree with it in any test (or
            // real caller) that drives Tick with an explicit, non-real-time
            // deltaTime, the same class of mismatch this file's own history
            // already warns about elsewhere.
            _lastTickDeltaTime = deltaTime;
            if (_gunFireHold > 0.0f)
            {
                _gunFireHold = Mathf.Max(0.0f, _gunFireHold - deltaTime);
            }

            // An impact holds the whole body still (PlayerLocomotion.Impact.cs): nothing below runs until it lets go.
            _freezeTickedFrame = Time.frameCount;
            if (TickImpactFreeze(deltaTime))
            {
                return;
            }

            // See FallThroughWorldY's own doc comment. Checked first, every
            // Tick, unconditionally -- cheap when it never fires (which is
            // every ordinary frame), and it must run before anything below
            // that assumes the body is somewhere real.
            if (transform.position.y < FallThroughWorldY)
            {
                RecoverFromFallingThroughTheWorld();
            }

            // Reset before this frame's own Move -- OnControllerColliderHit
            // (called synchronously during Move, below) is what actually
            // sets these back to true/valid for a real contact.
            HitWallThisFrame = false;
            LastWallHitNormal = Vector3.zero;

            ApplyHeight();
            // A blow still throwing the body back (PlayerLocomotion.Impact.cs), under whatever the keys ask for.
            ApplyShove(deltaTime);
            // The water at the feet (PlayerLocomotion.Water.cs): wading slows, deep water floats.
            SenseWater();

            Vector3 direction = input.WorldDirection();
            bool moving = direction.sqrMagnitude > 0.0001f;
            bool flying = input.FlightSpeed > 0.0f;

            // GTA STYLE (MovementTurn): out of an aim, a lock-on or a cast the body turns toward the keys at a rate that falls with speed and
            // goes where it faces, so it curves round instead of side-stepping; a sharp turn brakes it (below, on the speed).
            bool facesMovement = FaceMovement && !FacesTheCamera(input) && !flying && !_facingOverride && _dashRemaining <= 0.0f;
            float turnOff = 0.0f;
            if (facesMovement && moving && !MovementRooted)
            {
                Quaternion wanted = Quaternion.LookRotation(direction);
                turnOff = Quaternion.Angle(transform.rotation, wanted);
                float pace = _horizontalVelocity.magnitude / SizeSpeedFactor;
                // Only a body someone plays eases round. A brain steering for a spot needs the steady rate, whose lag brakes it (SpeedShare): eased, it
                // settled into a circle round the spot at a run and never arrived (a Shoota round his cover, ShootaPlayTests).
                transform.rotation = _easesTurns
                    ? Quaternion.Euler(0.0f, MovementTurn.Step(transform.eulerAngles.y, wanted.eulerAngles.y, ref _turnVelocity, pace, WalkSpeed, SprintSpeed, deltaTime), 0.0f)
                    : Quaternion.RotateTowards(transform.rotation, wanted, MovementTurn.DegreesPerSecond(pace, WalkSpeed, SprintSpeed) * deltaTime);
                direction = transform.forward * direction.magnitude;
            }
            else
            {
                _turnVelocity = 0.0f;
            }

            // CROUCHING, Source's way (PlayerLocomotion.Crouch.cs): on the ground the body goes down over 0.4 s, in the air its feet come up.
            TickCrouch(input.Crouch, _controller.isGrounded || _steppingUp, deltaTime);

            // SPRINT ONLY APPLIES GOING FORWARD, which is player.gd's own rule
            // and not a simplification. There is no running-backward or
            // running-sideways clip, so a 7.5 m/s strafe would be shown by a
            // 4 m/s walk clip -- feet sliding across the ground, which is the
            // exact moonwalk this rig's blend thresholds already exist to
            // prevent. Godot ties the speed to the direction for the same
            // reason it ties the clip to it.
            // Facing where it goes, every way is forward.
            bool forward = facesMovement || (input.Move.y > 0.0f && input.Move.y >= Mathf.Abs(input.Move.x));
            // CROUCH WINS OVER SPRINT, as in Source: a crouched body cannot run (and under a low ceiling it is crouched whatever is held).
            bool sprinting = input.Sprint && moving && forward && !IsCrouching;
            IsSprinting = sprinting;

            // FLYING, THE GEAR'S SPEED IS THE SPEED (LocomotionInput.FlightSpeed: a jetpack's or wings', in every direction, no stamina): none
            // of the walking rules below apply to it.
            float speed = flying ? input.FlightSpeed : sprinting ? SprintSpeed : WalkSpeed;
            speed *= MovementTurn.SpeedShare(turnOff);

            // AIMING DOWN SIGHTS SLOWS YOU (Fortnite, Gears, The Division all
            // do): you trade speed for a steady, zoomed aim. Not while
            // flying -- that is not walking.
            if (input.Aiming && !flying)
            {
                speed *= AimMoveSpeedMultiplier;
            }
            // BLOODRAGE: +25% movement speed on foot (a flight's own speed is its own business).
            if (!flying)
            {
                if (!_bloodrageLooked)
                {
                    _bloodrage = GetComponent<BloodrageAbility>();
                    _bloodrageLooked = true;
                }
                if (_bloodrage != null)
                {
                    speed *= _bloodrage.SpeedFactor;
                }
                // MOVEMENT SPEED (the stat, 2026-10-14): walking and running, capped so the city can stream in ahead of you.
                if (_statsVitals == null)
                {
                    _statsVitals = GetComponent<PlayerVitals>();
                }
                speed *= PlayerVitals.SpeedOf(_statsVitals, StatId.MovementSpeed);
                speed = WaterSpeed(speed);
                speed *= CrouchSpeedFactor(_controller.isGrounded || _steppingUp);
                if (input.ShieldRaised && Shield != null)
                {
                    speed *= ShieldMoveSpeedShare;
                }
            }
            // GROW AND SHRINK: a giant covers ground 3.2 times as fast, a tiny body a third as fast (SizeSpeedFactor). A CHILL slows it.
            speed *= SizeSpeedFactor * SlowFactor;
            Vector3 targetHorizontal = MovementRooted ? Vector3.zero : direction * speed;
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
                horizontal = _dashDirection * DashSpeed * SizeSpeedFactor *
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
            else if (KeepsAirMomentum && !_controller.isGrounded)
            {
                // MOMENTUM CARRIES (the aerial sword spin, until the body lands). The ordinary easing below pulls the velocity toward
                // what the keys ask for at 60 m/s^2, and a spin asks for nothing -- so the body used to brake to a dead stop in mid-air,
                // twice: when the spin took the keys away, and again when it handed them back with none held. Here the velocity is
                // simply kept, and the keys only STEER it: they add acceleration (AirMomentumSteerAcceleration), can slow it by
                // opposing it, and cannot build it past what it already was (or a walk, if it was slower).
                Vector3 carried = _horizontalVelocity;
                Vector3 steered = carried + direction * (AirMomentumSteerAcceleration * deltaTime);
                float cap = Mathf.Max(carried.magnitude, WalkSpeed * SizeSpeedFactor);
                if (steered.sqrMagnitude > cap * cap)
                {
                    steered = steered.normalized * cap;
                }
                _horizontalVelocity = steered;
                horizontal = steered;
            }
            else if (facesMovement && !MovementRooted && (_controller.isGrounded || _steppingUp))
            {
                // On foot and going where it faces: speed builds and falls with the body's weight. An aim, a lock-on or a blow stays quick (below).
                _horizontalVelocity = MovementWeight.Step(_horizontalVelocity, targetHorizontal, SizeSpeedFactor * WetTraction(), deltaTime);
                horizontal = _horizontalVelocity;
            }
            else
            {
                // A flight has its own inertia (LocomotionInput.FlightAcceleration): the body carries, and the keys lean it round.
                float acceleration = flying && input.FlightAcceleration > 0.0f && !_controller.isGrounded
                    ? input.FlightAcceleration
                    : HorizontalAcceleration * WetTraction();
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetHorizontal, acceleration * deltaTime);
                horizontal = _horizontalVelocity;
            }

            CurrentSpeed = horizontal.magnitude;

            bool grounded = _controller.isGrounded;

            // A STEP UP (PlayerLocomotion.StepUp.cs) is a stride, not a hop: while the body is lifted onto a step it is still on its feet, so
            // it can jump from it.
            bool onFeet = grounded || _steppingUp;

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
                _verticalVelocity -= Gravity * GravityScale * deltaTime;
            }

            // A resized body's jump is launched SizeSpeedFactor times as fast: under the same gravity it climbs BodyScale times as high,
            // the same jump in proportion (a giant's takes 3.2 times as long: heavy).
            if (onFeet && input.Jump && !IsFloating)
            {
                _verticalVelocity = JumpVelocity * SizeSpeedFactor * WaterJumpShare;
                FireTrigger(JumpParameter);
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
                if (onFeet && !IsFloating)
                {
                    float heightMultiplier = Mathf.Lerp(
                        1.0f, ChargedJumpHeightMultiplier, Mathf.Clamp01(input.JumpChargeFraction));
                    _verticalVelocity = JumpVelocity * SizeSpeedFactor * Mathf.Sqrt(heightMultiplier) * WaterJumpShare;
                }
                FireTrigger(JumpParameter);
            }

            // FLOATING (deep water, PlayerLocomotion.Water.cs) takes the place of gravity and the jump: sinking slowly, rising with jump held.
            if (IsFloating)
            {
                _verticalVelocity = Wading.FloatingClimb(WaterDepthShare, WorldHeight, input.JumpHeld);
            }

            // _residualVelocity rides on top of the WASD-driven horizontal
            // speed rather than replacing it -- carried momentum from a
            // released swing plus fresh air-control input should add, the
            // same way a real body's own inertia would layer under
            // whatever new push it is given rather than being overridden
            // by it.
            // A FORCED VELOCITY (Octaslash's dash) is the whole of the motion: no keys, no gravity, no carried momentum. The body's own
            // velocities are set to it, so the step after it ends carries on from where the dash really was.
            if (HasForcedVelocity)
            {
                horizontal = new Vector3(ForcedVelocity.x, 0.0f, ForcedVelocity.z);
                _horizontalVelocity = horizontal;
                _verticalVelocity = ForcedVelocity.y;
                _residualVelocity = Vector3.zero;
                CurrentSpeed = horizontal.magnitude;
            }
            Vector3 motion = horizontal + _residualVelocity + (HasForcedVelocity ? Vector3.zero : WaterCarry());
            motion.y = _verticalVelocity;
            if (HasForcedVelocity || input.Jump || input.JumpReleased)
            {
                EndStepUp();
            }
            else
            {
                motion.y = TickStepUp(motion, moving, grounded, MovementRooted, motion.y, deltaTime);
            }
            FitStepUnderCeiling(motion * deltaTime);
            Velocity = motion;
            LastMoveCollisionFlags = _controller.Move(motion * deltaTime);
            AfterStepUpMove(deltaTime);

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
            //
            // AIMING SNAPS INSTANTLY INSTEAD, EVEN WHILE MOVING -- CHECKED
            // FIRST, ahead of "moving". Bug report: "fix the pistol
            // make it so you hold it straight crosshair centered character
            // not at weird angle," later repeated specifically for walking
            // while holding right-click: "he is rotated to the left." The
            // gun is rigidly parented to the right hand (PistolWeapon.
            // OnEquip), so it only ever points wherever the BODY faces. This
            // used to be an else-if under "moving" above, which meant two
            // separate bugs from the same root cause: (1) stopping to aim
            // (stop, aim, shoot) left the body frozen facing wherever it
            // last walked, since standing-still took neither branch at all;
            // (2) walking WHILE aiming used the eased TurnDegreesPerSecond
            // turn instead of snapping, so the body visibly lagged the
            // camera for as long as it kept turning -- read as "angled off"
            // for anyone strafing or looking around while aiming, which is
            // most of the time aiming is actually used. A real shooter's aim
            // has to track the camera THE INSTANT it starts, moving or not --
            // a lagging catch-up reads as "at a weird angle" the whole time
            // it is catching up.
            if (AimsWithTheCamera(input))
            {
                transform.rotation = Quaternion.Euler(0.0f, input.YawDegrees + AimYawOffsetDegrees, 0.0f);
            }
            else if (input.LockedOn)
            {
                // LOCKED ON: face the camera's yaw (which is steering at the
                // target) moving or not -- the fight faces itself, and WASD
                // becomes strafing. Snapped, because the camera's own yaw
                // already eases toward the target; easing the body as well
                // would double-lag it.
                transform.rotation = Quaternion.Euler(0.0f, input.YawDegrees, 0.0f);
            }
            else if (moving && !facesMovement)
            {
                Quaternion facing = Quaternion.Euler(0.0f, input.YawDegrees, 0.0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, facing, TurnDegreesPerSecond * deltaTime);
            }

            TickFacingOverride(deltaTime);

            Vector3 local = transform.InverseTransformDirection(horizontal);
            LocalVelocity = new Vector2(local.x, local.z);

            // Read here, applied later in LateUpdate (ApplyGunAimPose) --
            // same "carry the answer, apply it once the Animator has had
            // its turn" shape every other procedural pose in this file uses.
            _isAimingGun = input.Aiming;
            _gunEquipped = input.GunEquipped;
            _gunAimCameraPosition = input.AimCameraPosition;
            _gunAimCameraForward = input.AimCameraForward;
            _gunAimPoint = input.GunAimPoint;
            _hasGunAimPoint = input.HasGunAimPoint;

            if (_animator != null)
            {
                // In the body's own terms: a giant's 12.6 m/s walk is a walk (its cycle slowed by BodySizeAbility's StrideRate).
                float ownPace = 1.0f / SizeSpeedFactor;
                _animator.SetFloat(SpeedParameter, CurrentSpeed * ownPace);
                // Only the nearest straight way: the hips are turned the rest after the Animator (PlayerLocomotion.Warp.cs).
                Vector2 shown = _orientationWarp.Shown(LocalVelocity);
                _animator.SetFloat(MoveXParameter, shown.x * ownPace);
                _animator.SetFloat(MoveZParameter, shown.y * ownPace);
                // A floating body treads water: the walk and idle, not the fall.
                _animator.SetBool(GroundedParameter, _controller.isGrounded || _steppingUp || IsFloating);
                _animator.SetBool(CrouchingParameter, IsCrouching);
                _animator.SetBool(ShieldRaisedParameter, input.ShieldRaised && Shield != null);
                ShowCrouch();
                _animator.SetBool(ChargingJumpParameter, input.ChargingJump);
                _animator.SetBool(AimingParameter, input.Aiming);
                _animator.SetBool(SwordEquippedParameter, input.SwordEquipped);
                _animator.SetBool(Sword1HEquippedParameter, input.Sword1HEquipped);
                _animator.SetBool(GunEquippedParameter, input.GunEquipped);
                _animator.SetBool(JetpackThrustingParameter, input.JetpackThrusting);
                _animator.SetBool(DancingParameter, input.Dancing);
                _animator.SetBool(BreakdancingParameter, input.Breakdancing);
                _animator.SetInteger(PushUpPhaseParameter, input.PushUpPhase);
            }
        }

        /// <summary>
        /// How hard walking into a real, non-kinematic body (a ragdoll or
        /// a thrown prop) shoves it. Purely a feel constant, tuned by eye --
        /// there is no ported value to match, CharacterController has no
        /// built-in push at all.
        /// </summary>
        public const float PushForce = 3.5f;

        /// <summary>
        /// CharacterController NEVER PUSHES A RIGIDBODY ON ITS OWN -- it is
        /// a kinematic capsule that solves its own collisions by sliding,
        /// the exact same reason PlayerLocomotion uses one at all (see this
        /// class's own doc comment). Unity calls this once per real contact
        /// during Move, and without it a real Rigidbody in the world --
        /// a ragdoll's own limp body, in particular -- simply blocks the
        /// player like a wall instead of swinging away like something that
        /// actually has mass, which is exactly what "walk into it and it
        /// should react" needs and nothing in this project provided before
        /// now.
        /// </summary>

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // A REAL, DIRECT CONTACT CHECK, independent of the rigidbody
            // push below -- ordinary city geometry (a building, a window)
            // has no Rigidbody at all, so the early return just past this
            // would otherwise skip it entirely. Keeps whichever contact
            // this frame is MOST wall-like if Move reports more than one
            // (Unity calls this once per contact), rather than just the
            // last one, since a glancing hit alongside a squarer one
            // should not overwrite the squarer one's normal.
            if (Mathf.Abs(hit.normal.y) < WallNormalMaxUpDot &&
                (!HitWallThisFrame || Mathf.Abs(hit.normal.y) < Mathf.Abs(LastWallHitNormal.y)))
            {
                HitWallThisFrame = true;
                LastWallHitNormal = hit.normal;
            }


            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic)
            {
                return;
            }

            // ONLY THE HORIZONTAL PUSH, and only while moving roughly level
            // into it -- a hit registered while falling onto something (a
            // steep downward moveDirection) is landing on it, not shoving
            // it, and pushing straight down would just launch a ragdoll's
            // own body into the ground.
            if (hit.moveDirection.y < -0.3f)
            {
                return;
            }
            Vector3 push = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);
            body.AddForceAtPosition(push * PushForce, hit.point, ForceMode.Impulse);
        }
    }
}
