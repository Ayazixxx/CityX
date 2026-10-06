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
    // EARLIER THAN DEFAULT, ON PURPOSE -- EXPLICIT BUG, found live: Unity
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
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        [Header("Movement (ported from player.gd)")]
        [Tooltip("player.gd SPEED")]
        public float WalkSpeed = 4.0f;

        [Tooltip("player.gd SPRINT_SPEED")]
        public float SprintSpeed = 7.5f;

        /// <summary>
        /// How much faster than SprintSpeed a full W+shift jetpack dive
        /// travels -- EXPLICIT ASK: "the jetpack goes twice as fast" for
        /// that specific hold. Applied on top of the jetpack's own already-
        /// boosted SprintSpeed rather than WalkSpeed, since JetpackDiving
        /// only ever fires alongside JetpackBoosting (see LocomotionInput.
        /// JetpackDiving's own doc comment) -- "twice as fast" reads as
        /// twice however fast the jetpack was already making you go, not a
        /// number pulled from the ordinary on-foot walk speed.
        /// </summary>
        [Tooltip("Multiplier on SprintSpeed while diving (W+shift on the jetpack).")]
        public float JetpackDiveSpeedMultiplier = 2.0f;

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
        private Transform _rightLowerArm;
        private Transform _leftLowerArm;
        private Transform _leftUpperLeg;
        private Transform _rightUpperLeg;
        private Transform _leftLowerLeg;
        private Transform _rightLowerLeg;
        private MeleeAttacker _melee;
        private SoulsSwordCombat _soulsSword;
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
        /// Sitting on a BenchSeat -- see that class's own doc comment.
        /// Gated into LateUpdate's hips/spine reset and ClampFeetAboveGround
        /// exactly like InCarSeat: ApplySittingPose bends the real leg
        /// bones into a seated shape every frame, and freezing hips to
        /// identity or clamping feet to the ground would fight that shape
        /// exactly the way it would fight a car's own seated pose.
        /// </summary>
        public bool IsSitting;

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

        /// <summary>Which arm ApplyCastingPose raises -- see SetCastPose's own doc comment.</summary>
        private bool _castUsesLeftArm;

        /// <summary>
        /// The raising arm's own animated pose, BEFORE this frame's cast
        /// offset is layered onto it -- see ApplyCastingPose's own doc
        /// comment for why this has to be refreshed only while the
        /// Animator is actually live, not read back off the bone itself
        /// every frame.
        /// </summary>
        private Quaternion _castBaseRotation = Quaternion.identity;

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
        /// A sword swing is a torso movement first and an arm movement second, so while a committed
        /// attack or a guard is playing the spine and hips follow the clip instead of being held at
        /// identity (see LateUpdate). Set by SoulsSwordCombat; walking and standing keep the lock the
        /// locomotion clips need, which is why this is not simply "sword equipped".
        /// </summary>
        public bool TorsoFollowsClip
        {
            get
            {
                if (_souls == null)
                {
                    _souls = GetComponent<SoulsSwordCombat>();   // added on the first equip
                }
                return _souls != null && _souls.IsEquipped && (_souls.IsCommitted || _souls.IsBlocking);
            }
        }

        private SoulsSwordCombat _souls;
        private float _torsoFollowBlend;

        /// <summary>How fast the torso takes up (and gives up) the clip's own hips/spine motion, per second.</summary>
        public const float TorsoFollowBlendPerSecond = 10.0f;

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

        private Vector3 _gunAimCameraPosition;
        private Vector3 _gunAimCameraForward = Vector3.forward;
        private Vector3 _gunAimPoint;
        private bool _hasGunAimPoint;
        private Vector3 _aimedPoint;
        private bool _hasAimedPoint;

        /// <summary>
        /// The equipped gun's muzzle (set by the gun's OnEquip, cleared on unequip).
        /// With it the arm aims the BARREL at the target, not just the upper arm.
        /// </summary>
        public Transform GunMuzzle;

        // ---- the support hand -------------------------------------------------------------------
        // A two-handed weapon is held by the hand the animation places (the right) and steadied by
        // the other, which is reached onto the weapon here every frame. See TwoBoneIK for why.

        private Transform _supportAnchor;
        private Vector3 _supportLocalPoint;
        private float _supportBlend;
        private bool _supportOnlyWhenAiming;

        /// <summary>How fast the support hand takes hold (and lets go), per second: a grab, not a snap.</summary>
        public const float SupportHandBlendPerSecond = 9.0f;

        /// <summary>The palm's centre, this far along the forearm from the wrist bone.</summary>
        public const float PalmOffsetMetres = 0.08f;

        /// <summary>
        /// Tells the rig to hold <paramref name="weapon"/> with the LEFT hand as well, palm at
        /// <paramref name="weaponLocalPoint"/> (the weapon's own space, so it follows the weapon through a swing).
        /// </summary>
        /// <param name="onlyWhenAiming">
        /// True for a gun: the free hand comes up to steady it only as the gun is raised to aim (or fire), and
        /// swings free while it is carried. False for a sword, held in both hands all the time.
        /// </param>
        public void SetSupportHand(Transform weapon, Vector3 weaponLocalPoint, bool onlyWhenAiming = false)
        {
            _supportAnchor = weapon;
            _supportLocalPoint = weaponLocalPoint;
            _supportOnlyWhenAiming = onlyWhenAiming;
        }

        /// <summary>Lets go -- only if <paramref name="weapon"/> is still the weapon being held (a swap must not drop the new one).</summary>
        public void ClearSupportHand(Transform weapon)
        {
            if (_supportAnchor == weapon)
            {
                _supportAnchor = null;
            }
        }

        /// <summary>The support hand's palm, in world space (for tests and the tuning console); null when nothing is held.</summary>
        public bool TryGetSupportPalm(out Vector3 palm)
        {
            palm = Vector3.zero;
            if (_handL == null || _leftLowerArm == null)
            {
                return false;
            }
            palm = _handL.position + (_handL.position - _leftLowerArm.position).normalized * PalmOffsetMetres;
            return true;
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

        private void ApplySupportHand()
        {
            float deltaTime = Mathf.Max(_lastTickDeltaTime, 0.0f);
            _supportBlend = Mathf.MoveTowards(_supportBlend, _supportAnchor != null ? 1.0f : 0.0f, SupportHandBlendPerSecond * deltaTime);
            // The sword spin lets go of the handle: the sword arm goes out alone and the free arm balances it (ApplySwordSpinPose).
            float weight = _supportBlend * (_supportOnlyWhenAiming ? EasedGunAimBlend : 1.0f) * (1.0f - _spinPoseBlend);
            if (weight <= 0.0f || _supportAnchor == null || _leftUpperArm == null || _leftLowerArm == null || _handL == null)
            {
                return;
            }

            Vector3 point = _supportAnchor.TransformPoint(_supportLocalPoint);
            // Elbow down and a little out, the way a person braces a heavy weapon.
            Vector3 elbowHint = -transform.up - transform.right * 0.6f;

            // The wrist bone is not the palm: aim the PALM at the point. Two passes, since turning the
            // forearm turns the palm's direction too.
            Quaternion upperStart = _leftUpperArm.rotation;
            Quaternion lowerStart = _leftLowerArm.rotation;
            for (int pass = 0; pass < 2; pass++)
            {
                Vector3 palmDirection = (_handL.position - _leftLowerArm.position).normalized;
                TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, point - palmDirection * PalmOffsetMetres, elbowHint);
            }
            if (weight < 1.0f)
            {
                // Blend the finished solution in, rather than blending each pass (which would compound).
                _leftUpperArm.rotation = Quaternion.Slerp(upperStart, _leftUpperArm.rotation, weight);
                _leftLowerArm.rotation = Quaternion.Slerp(lowerStart, _leftLowerArm.rotation, weight);
            }
        }

        // ---- the sword spin's pose -----------------------------------------------------------------
        // Link's spin has the sword arm straight out to the side with the blade carrying on the line of the arm, the free arm
        // out the other way for balance, and the whole body turning. No clip does that (the swing clips are all two-handed
        // chops), and a hand-posed clip is far harder to keep right than three IK calls, so the arms are placed here, after the
        // Animator, the way the gun-aim pose and the support hand are.

        /// <summary>How fast the spin pose takes hold and lets go, as a fraction per second: about a seventh of a second.</summary>
        public const float SpinPoseBlendPerSecond = 7.0f;

        private float _spinPoseTarget;
        private float _spinPoseBlend;
        private float _spinBladeBackDegrees;
        private float _spinBladePitchDegrees;
        private ISwordBlade _spinBlade;

        /// <summary>How far the spin pose has taken hold, 0 to 1.</summary>
        public float SwordSpinPoseBlend => _spinPoseBlend;

        /// <summary>The shoulder of the sword arm (the right), or null on a body without one.</summary>
        public Transform SwordArmShoulder => _rightUpperArm;

        /// <summary>
        /// Asks for the spin pose (<paramref name="target"/> 1) or to let it go (0). <paramref name="bladeBackDegrees"/> swings the
        /// blade from straight out to the right toward the back (a wind-up), <paramref name="bladePitchDegrees"/> tips it up.
        /// <paramref name="blade"/> is the sword in hand, so the hand can be turned to lay it along the arm; null keeps the hand's own pose.
        /// </summary>
        public void SetSwordSpinPose(float target, float bladeBackDegrees = 0.0f, float bladePitchDegrees = 0.0f, ISwordBlade blade = null)
        {
            _spinPoseTarget = Mathf.Clamp01(target);
            _spinBladeBackDegrees = bladeBackDegrees;
            _spinBladePitchDegrees = bladePitchDegrees;
            _spinBlade = blade;
        }

        // ---- hands on a ledge --------------------------------------------------------------------------------------
        // While climbing toward a ledge and while pulling up over it, both palms are put ON the ledge's lip (two-bone IK, after the Animator), so a
        // climb reads as gripping the edge rather than as a generic clip whose hands float wherever it was authored.

        // ---- UNARMED STRIKES: the fists and torso (see FistStrikes / FistPoseDriver) ------------------------------------------------------------

        private FistFrame _fistFrame;

        public void SetFistPose(in FistFrame frame)
        {
            _fistFrame = frame;
        }

        public void ClearFistPose()
        {
            _fistFrame = default;
        }

        /// <summary>True while a fist pose is being applied.</summary>
        public bool FistPoseActive => _fistFrame.Active;

        // THE FACING A BLOW TURNS THE BODY TO. A punch goes where the body faces, and the body only turned to the camera while moving: standing still
        // after turning the camera, the punch went the way you USED to face. A blow asks for a quick turn to the camera (or to a target near it).
        private bool _facingOverride;
        private float _facingOverrideYaw;
        private float _facingOverrideRate;
        private float _facingOverrideRemaining;

        /// <summary>Turn the body to <paramref name="yawDegrees"/> at <paramref name="degreesPerSecond"/> for the next <paramref name="seconds"/>, whatever it is otherwise doing.</summary>
        public void SetFacingOverride(float yawDegrees, float degreesPerSecond, float seconds)
        {
            _facingOverride = true;
            _facingOverrideYaw = yawDegrees;
            _facingOverrideRate = degreesPerSecond;
            _facingOverrideRemaining = seconds;
        }

        private void TickFacingOverride(float deltaTime)
        {
            if (!_facingOverride)
            {
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0.0f, _facingOverrideYaw, 0.0f), _facingOverrideRate * deltaTime);
            _facingOverrideRemaining -= deltaTime;
            if (_facingOverrideRemaining <= 0.0f)
            {
                _facingOverride = false;
            }
        }

        /// <summary>After the Animator: turn and lean the torso into the blow, then put each fist where the frame says (two-bone IK), the other held in the guard.</summary>
        private void ApplyFistPose()
        {
            if (!_fistFrame.Active)
            {
                return;
            }
            if (_spine != null && (Mathf.Abs(_fistFrame.TwistDegrees) > 0.01f || Mathf.Abs(_fistFrame.LeanDegrees) > 0.01f))
            {
                Quaternion twist = Quaternion.AngleAxis(_fistFrame.TwistDegrees, transform.up);
                Quaternion lean = Quaternion.AngleAxis(_fistFrame.LeanDegrees, transform.right);
                _spine.rotation = twist * lean * _spine.rotation;
            }
            PlaceFist(_rightUpperArm, _rightLowerArm, _handR, _fistFrame.RightHand, _fistFrame.RightWeight, _fistFrame.RightElbowOut, 1.0f);
            PlaceFist(_leftUpperArm, _leftLowerArm, _handL, _fistFrame.LeftHand, _fistFrame.LeftWeight, _fistFrame.LeftElbowOut, -1.0f);
        }

        private void PlaceFist(Transform upper, Transform lower, Transform hand, Vector3 offset, float weight, float elbowOut, float side)
        {
            if (weight <= 0.001f || upper == null || lower == null || hand == null)
            {
                return;
            }
            float length = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            Vector3 target = upper.position + (transform.right * offset.x + transform.up * offset.y + transform.forward * offset.z) * length;
            // The elbow goes down and out; a hook swings it right out to the side.
            Vector3 hint = -transform.up * (1.0f - 0.8f * elbowOut) + transform.right * side * (0.55f + 0.9f * elbowOut);
            TwoBoneIK.Solve(upper, lower, hand, target, hint, weight);
        }

        private float _climbWallWeight;
        private Vector3 _climbWallPoint;
        private Vector3 _climbWallNormal = Vector3.forward;

        /// <summary>Hands and feet keep at least this far off the wall plane while climbing, in metres.</summary>
        public const float HandWallClearance = 0.09f;

        public const float FootWallClearance = 0.11f;

        /// <summary>The wall plane the limbs are being kept off (read back so a network copy can be told it).</summary>
        public float ClimbWallWeight => _climbWallWeight;

        public Vector3 ClimbWallPoint => _climbWallPoint;

        public Vector3 ClimbWallNormal => _climbWallNormal;

        /// <summary>Where the hands are being put on a ledge (read back so a network copy can be told it).</summary>
        public Vector3 LedgeLip => _ledgeLip;

        public Vector3 LedgeNormal => _ledgeNormal;
        /// <summary>
        /// While climbing, tells the body where the wall is (a point on its face and its outward normal) so that no hand, foot, elbow or knee can be
        /// animated THROUGH it: the climb clip's limbs swing wherever it likes, and the body hangs a capsule's radius from the wall.
        /// </summary>
        public void SetClimbWall(Vector3 point, Vector3 normal, float weight)
        {
            _climbWallWeight = Mathf.Clamp01(weight);
            _climbWallPoint = point;
            _climbWallNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
        }

        /// <summary>
        /// After the Animator: any hand or foot (or elbow / knee) that the animation put behind the wall plane is moved out to just in front of it and
        /// its limb is solved to reach there (two-bone IK), so the hands and feet TOUCH the wall and never go through it. A limb already in front is not
        /// touched, so the animation is only corrected where it is wrong.
        /// </summary>
        private void ApplyClimbWallLimbs()
        {
            if (_climbWallWeight <= 0.01f)
            {
                return;
            }
            Vector3 n = _climbWallNormal;
            Vector3 elbowHint = n * 0.8f + Vector3.down * 0.2f;
            Vector3 kneeHint = n * 0.5f + Vector3.up * 0.5f;
            for (int pass = 0; pass < 2; pass++)   // a second pass takes out what the first one left (an arm at full stretch, a blend that moved)
            {
                KeepOffWall(_rightUpperArm, _rightLowerArm, _handR, HandWallClearance, elbowHint);
                KeepOffWall(_leftUpperArm, _leftLowerArm, _handL, HandWallClearance, elbowHint);
                KeepOffWall(_rightUpperLeg, _rightLowerLeg, _rightFoot, FootWallClearance, kneeHint);
                KeepOffWall(_leftUpperLeg, _leftLowerLeg, _leftFoot, FootWallClearance, kneeHint);
            }
        }

        private void KeepOffWall(Transform upper, Transform lower, Transform end, float clearance, Vector3 hint)
        {
            if (upper == null || lower == null || end == null)
            {
                return;
            }
            float endDepth = Vector3.Dot(end.position - _climbWallPoint, _climbWallNormal);
            float midDepth = Vector3.Dot(lower.position - _climbWallPoint, _climbWallNormal);
            float push = Mathf.Max(clearance - endDepth, 0.0f);
            if (push <= 0.001f && midDepth >= 0.02f)
            {
                return;
            }
            Vector3 target = end.position + _climbWallNormal * push;
            TwoBoneIK.Solve(upper, lower, end, target, hint, _climbWallWeight);
        }

        private float _ledgeHandsWeight;
        private Vector3 _ledgeLip;
        private Vector3 _ledgeNormal;

        /// <summary>The ledge the hands reach for: 0 weight lets go. <paramref name="lip"/> is the wall's top edge, <paramref name="outward"/> the wall's normal.</summary>
        public void SetLedgeHands(float weight, Vector3 lip, Vector3 outward)
        {
            _ledgeHandsWeight = Mathf.Clamp01(weight);
            _ledgeLip = lip;
            _ledgeNormal = outward;
        }

        /// <summary>How strongly the hands are on a ledge right now (0 to 1).</summary>
        public float LedgeHandsWeight => _ledgeHandsWeight;

        private void ApplyLedgeHands()
        {
            if (_ledgeHandsWeight <= 0.01f || _rightUpperArm == null || _rightLowerArm == null || _handR == null
                || _leftUpperArm == null || _leftLowerArm == null || _handL == null)
            {
                return;
            }
            Vector3 across = Vector3.Cross(Vector3.up, _ledgeNormal).normalized;
            // Palms on top of the lip, a shoulder's width apart, a little back from the edge.
            Vector3 centre = _ledgeLip - _ledgeNormal * 0.06f + Vector3.up * 0.03f;
            Vector3 hint = _ledgeNormal * 0.6f - Vector3.up;
            float weight = Mathf.SmoothStep(0.0f, 1.0f, _ledgeHandsWeight);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, centre - across * 0.28f, hint, weight);   // facing the wall (-normal), the right hand is on the -across side
            TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, centre + across * 0.28f, hint, weight);
        }

        /// <summary>Where the sword arm's hand is put, relative to the shoulder, as a fraction of the arm's length: all the way out.</summary>
        private const float SpinArmReach = 0.99f;

        private void ApplySwordSpinPose()
        {
            float deltaTime = _lastTickDeltaTime > 0.0f ? _lastTickDeltaTime : Time.deltaTime;
            _spinPoseBlend = Mathf.MoveTowards(_spinPoseBlend, _spinPoseTarget, SpinPoseBlendPerSecond * deltaTime);
            if (_spinPoseBlend <= 0.0f || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                return;
            }
            float weight = Mathf.SmoothStep(0.0f, 1.0f, _spinPoseBlend);

            // The sword arm: straight out to the side (and back, and up, as asked), elbow down so it does not buckle upward.
            Vector3 outward = Quaternion.AngleAxis(_spinBladeBackDegrees, transform.up) * transform.right;
            float pitch = _spinBladePitchDegrees * Mathf.Deg2Rad;
            outward = outward * Mathf.Cos(pitch) + transform.up * Mathf.Sin(pitch);
            float armLength = Vector3.Distance(_rightUpperArm.position, _rightLowerArm.position)
                + Vector3.Distance(_rightLowerArm.position, _handR.position);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, _rightUpperArm.position + outward * armLength * SpinArmReach,
                -transform.up + transform.forward * 0.2f, weight);

            // The blade carries on the line of the arm: turn the hand about the wrist until the blade lies along it.
            if (_spinBlade != null)
            {
                Vector3 bladeNow = _spinBlade.BladeTipWorld - _spinBlade.BladeBaseWorld;
                if (bladeNow.sqrMagnitude > 1e-6f)
                {
                    Quaternion turn = Quaternion.FromToRotation(bladeNow, outward);
                    _handR.rotation = Quaternion.Slerp(_handR.rotation, turn * _handR.rotation, weight);
                }

                // And roll it about its own length so the CUTTING EDGE leads, the way the body is going to turn: the sword sweeps
                // forward across the front (the body turns the other way round to a clock seen from above), so its edge faces forward
                // and the flat of the blade up and down. Without this the blade is a paddle, edge down, slapping flat-first.
                Vector3 edge = _spinBlade.EdgeDirectionWorld;
                Vector3 axis = _spinBlade.BladeTipWorld - _spinBlade.BladeBaseWorld;
                if (edge.sqrMagnitude > 1e-6f && axis.sqrMagnitude > 1e-6f)
                {
                    Vector3 edgeFlat = Vector3.ProjectOnPlane(edge, axis);
                    Vector3 leading = Vector3.ProjectOnPlane(transform.forward, axis);
                    if (edgeFlat.sqrMagnitude > 1e-6f && leading.sqrMagnitude > 1e-6f)
                    {
                        float roll = Vector3.SignedAngle(edgeFlat, leading, axis);
                        _handR.rotation = Quaternion.AngleAxis(roll * weight, axis) * _handR.rotation;
                    }
                }
            }

            // The free arm balances it: out the other side, a little down and back.
            if (_leftUpperArm != null && _leftLowerArm != null && _handL != null)
            {
                float leftLength = Vector3.Distance(_leftUpperArm.position, _leftLowerArm.position)
                    + Vector3.Distance(_leftLowerArm.position, _handL.position);
                Vector3 balance = (-transform.right * 0.85f - transform.up * 0.4f - transform.forward * 0.2f).normalized;
                TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, _leftUpperArm.position + balance * leftLength * SpinArmReach,
                    -transform.up - transform.forward * 0.3f, weight);
            }
        }

        /// <summary>The most the barrel correction may turn the arm, so a wild clip pose cannot fling it.</summary>
        public const float GunBarrelCorrectionMaxDegrees = 60.0f;

        /// <summary>Correction passes per frame (see ApplyGunAimPose).</summary>
        public const int GunBarrelCorrectionPasses = 4;

        /// <summary>Seconds left of the FIRE POSE: after a shot the arm aims down the barrel and the body faces the camera, aimed or not.</summary>
        private float _gunFireHold;

        /// <summary>How long the fire pose lasts after a shot. Long enough to cover a burst, short enough to lower between shots.</summary>
        public const float GunFirePoseSeconds = 0.5f;

        /// <summary>
        /// A shot was just fired: raise the arm to point down the barrel (and turn
        /// the body to the camera) for GunFirePoseSeconds even though right-click is
        /// not held. Hip fire used to leave the gun wherever the walk cycle carried
        /// it, so the bullet tracer left the muzzle at an angle unrelated to the
        /// barrel -- the reported "going in the wrong direction".
        /// </summary>
        public void NotifyGunFired()
        {
            _gunFireHold = GunFirePoseSeconds;
        }
        private float _lastTickDeltaTime;

        /// <summary>
        /// Eases toward 1 while aiming, 0 otherwise -- see ApplyGunAimPose's
        /// own doc comment for why this can't just snap straight to full
        /// strength the instant Aiming flips true.
        /// </summary>
        private float _gunAimBlend;

        /// <summary>
        /// The aim blend as an S-curve. The blend itself moves at a constant rate, so the arm started and
        /// stopped moving abruptly -- most visibly when a hip-fired shot swings the arm up in a few frames,
        /// which reads as a twitch. Easing the weight it is applied with keeps the same overall time but starts
        /// and ends smoothly.
        /// </summary>
        private float EasedGunAimBlend => Mathf.SmoothStep(0.0f, 1.0f, _gunAimBlend);

        /// <summary>
        /// Eases toward 1 while the gun is equipped but NOT being aimed
        /// (hip-fire movement), 0 otherwise -- see ApplyGunAimPose's own
        /// doc comment. Deliberately excludes the aiming case: that already
        /// gets the stronger camera-converged head correction via
        /// _gunAimBlend, and the two must never both drive the head in the
        /// same frame.
        /// </summary>
        private float _gunWalkHeadBlend;
        private bool _castBaseIsLeftArm;

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
        private static readonly int GunEquippedParameter = Animator.StringToHash("GunEquipped");
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
        /// normal, not derived from CollisionFlags. EXPLICIT BUG REPORT:
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
        /// is ever built anywhere near this low. EXPLICIT BUG REPORT,
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
            // Only a live Animator has a torso pose worth following; a frozen or ragdolled one is held flat.
            bool animatorLive = _animator != null && _animator.enabled;
            _torsoFollowBlend = Mathf.MoveTowards(
                _torsoFollowBlend, (animatorLive && TorsoFollowsClip) ? Mathf.Clamp01(TorsoFollowStrength) : 0.0f,
                TorsoFollowBlendPerSecond * Mathf.Max(_lastTickDeltaTime, 0.0f));
            if (!InCarSeat && !IsSitting)
            {
                if (_hips != null)
                {
                    _hips.localRotation = Quaternion.Slerp(Quaternion.identity, _hips.localRotation, _torsoFollowBlend);
                }
                if (_spine != null)
                {
                    _spine.localRotation = Quaternion.Slerp(Quaternion.identity, _spine.localRotation, _torsoFollowBlend);
                }
            }

            if (_animator == null || !_animator.enabled)
            {
                return;
            }

            // Only guarded while the Animator is actually live: the whole
            // point is to stop a bad Mecanim transition from ever reaching
            // the bone in the first place, so a subsequent console/grapple
            // freeze captures the good, corrected pose. EXPLICIT BUG, found
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
            ApplyDrivingWheelPose();
            ApplyCastingPose();
            ApplyGunAimPose();
            ApplySupportHand();
            ApplySwordSpinPose();
            ApplyFistPose();
            ApplyClimbWallLimbs();
            ApplyLedgeHands();
            if (IsSitting)
            {
                ApplySittingPose();
            }
        }

        /// <summary>
        /// How fast a watched bone may rotate, in degrees per second, before
        /// SuppressImplausibleArmSnaps holds it at last frame's own real
        /// pose instead of accepting the new one -- the SAME generous bound
        /// PoseDistortionWatchdog.MaxBoneDegreesPerSecond already uses to
        /// flag this exact class of glitch (a real swing's own reach eases
        /// at 240 deg/s; this is meant to catch an instantaneous jump, not
        /// flag ordinary fast movement).
        /// </summary>
        private const float MaxPlausibleArmDegreesPerSecond = 1500.0f;

        private Quaternion _previousHeadRotation = Quaternion.identity;
        private Quaternion _previousRightUpperArmRotation = Quaternion.identity;
        private Quaternion _previousRightLowerArmRotation = Quaternion.identity;
        private Quaternion _previousLeftUpperArmRotation = Quaternion.identity;
        private Quaternion _previousLeftLowerArmRotation = Quaternion.identity;

        /// <summary>
        /// Belongs ONLY to the ordinary per-frame LateUpdate path -- the
        /// separate, on-demand capture path (SuppressImplausibleArmSnapsNow,
        /// used by GrappleSwing.Attach/DevConsoleView.Open) never reads or
        /// writes these, by design; see SuppressImplausibleArmSnapsNowCore's
        /// own doc comment for why sharing this exact state was a real,
        /// shipped bug. If a bone was held last frame, THIS frame accepts
        /// whatever the Animator is doing now unconditionally instead of
        /// re-checking it against the same stale reference, so a real
        /// glitch (self-corrects within a frame or two) still gets hidden,
        /// but a legitimate fast transition can never lock up permanently.
        /// </summary>
        private bool _headSnapHeldLastFrame;
        private bool _rightUpperArmSnapHeldLastFrame;
        private bool _rightLowerArmSnapHeldLastFrame;
        private bool _leftUpperArmSnapHeldLastFrame;
        private bool _leftLowerArmSnapHeldLastFrame;

        /// <summary>True once _previousXxx has been populated for real at least once -- the only case with nothing yet to plausibly compare against.</summary>
        private bool _haveEverCapturedArmSnapBaseline;

        /// <summary>
        /// EXPLICIT BUG REPORT, with real screenshots: "the console and
        /// grapple still wildly stretches the body" -- arms twisted into an
        /// X, arms flung up and out. ROOT-CAUSED DIRECTLY: a new PlayMode
        /// test (LocomotionAnimatorStabilityPlayTests) drove realistic,
        /// changing movement input with the Animator completely live and
        /// unmolested -- no console, no grapple, nothing ever disabling
        /// anything -- and caught the locomotion Animator's OWN blend tree
        /// snapping RightLowerArm 10627 degrees/s in a single frame on its
        /// own, specifically while airborne (jump/fall) with changing
        /// input, i.e. mid-transition between locomotion states. Unlike
        /// hips/spine, NOTHING anywhere in this codebase ever corrects an
        /// arm or head bone after the Animator writes it, so a bad
        /// transitional frame like this is exactly what would render --
        /// invisible for the one single frame it lasts during ordinary
        /// unfrozen play, but exactly what a console/grapple freeze
        /// disabling the Animator on that same frame would catch and hold
        /// for as long as the freeze lasts. This is what actually explains
        /// "random" (depends on exact freeze timing vs. the transition's
        /// own timing), "movement-influenced" (only a moving, possibly-
        /// airborne body is ever mid-transition), and "snaps back
        /// instantly" (the very next live Animator.Update recomputes a
        /// clean pose from the current, correct state).
        ///
        /// FIXED HERE RATHER THAN IN THE ANIMATOR CONTROLLER ITSELF: the
        /// bad frame is a Mecanim blend-tree/transition artifact, not a
        /// wrong C# value, so there is no single clip or transition setting
        /// to correct -- the same class of fix hips/spine already uses
        /// (LateUpdate holds a bone to a known-good value every frame,
        /// unconditionally) is reused here instead: if a watched bone
        /// rotates further in one frame than any real intentional motion
        /// plausibly would, HOLD it at its own last real pose rather than
        /// accepting the implausible jump. A real player is never meant to
        /// see the difference either way -- a legitimate fast motion never
        /// approaches this threshold (see MaxPlausibleArmDegreesPerSecond's
        /// own doc comment), so this only ever intervenes on a frame that
        /// was already a glitch, holding the PREVIOUS good frame instead of
        /// however many further frames the same glitch might otherwise
        /// have compounded across.
        ///
        /// SKIPPED WHILE A REAL MELEE SWING IS IN PROGRESS -- a sword
        /// swing's own crossfade genuinely can move an arm fast over a
        /// short blend, and this project has no measured, trustworthy
        /// upper bound on that the way PoseDistortionWatchdog's own 1500
        /// deg/s figure is measured against ordinary locomotion/swing-pose
        /// motion specifically -- suppressing a real swing's own arm speed
        /// would be a new, self-inflicted regression, not a fix.
        ///
        /// SKIPPED ENTIRELY WHILE MeleeAttacker/GrappleSwing OWN THESE SAME
        /// BONES -- EXPLICIT BUG, found live (broke two established
        /// GrappleSwing tests the first time this shipped): Unity does not
        /// guarantee this component's own LateUpdate runs before or after
        /// GrappleSwing's, and if this one ran FIRST, WRITING a bone back
        /// to "last frame's value" here is exactly what GrappleSwing's own
        /// release blend then read back as ITS OWN blend target, gluing
        /// the blend in place instead of letting it ease toward the live
        /// animator pose.
        ///
        /// TRACKING (updating _previousXxx without correcting, rather than
        /// skipping outright) WAS TRIED AND REVERTED -- EXPLICIT BUG,
        /// found live via a new randomised PlayMode test: while
        /// GrappleSwing/MeleeAttacker actively drive these bones, the
        /// value sitting on them is THEIR OWN pose, not the Animator's --
        /// a fundamentally different reference frame. Tracking it as if it
        /// were "the Animator's last plausible value" broke
        /// RepeatedlyFiringAndReleasingNeverDriftsTheCapturedArmBase: a
        /// rapid re-fire's freshly re-evaluated Animator pose looked
        /// "implausible" only because it was being compared against the
        /// FIRST swing's own leftover reach pose. Skipping outright avoids
        /// this entirely -- there is nothing useful to compare a future
        /// Animator transition against here anyway, since nothing legitimately
        /// moves these bones frame-to-frame while the Animator is disabled
        /// and nothing else owns them (a plain console freeze) either.
        /// GetComponent, not a cached field bound once in Bind() --
        /// GrappleSwing is added/removed dynamically as the hook is
        /// equipped/unequipped, well after Bind() ever runs.
        /// </summary>
        private void SuppressImplausibleArmSnaps()
        {
            if (_melee != null && _melee.IsSwinging)
            {
                return;
            }

            // THE SOULSLIKE SWORD DRIVES THE SAME ARM BONES THROUGH ITS OWN
            // (time-stretched) swing clips, for the same reason a
            // MeleeAttacker swing is exempt above. Looked up lazily because
            // SwordWeapon.OnEquip adds it at runtime, long after Bind().
            if (_soulsSword == null)
            {
                _soulsSword = GetComponent<SoulsSwordCombat>();
            }
            if (_soulsSword != null && _soulsSword.IsCommitted)
            {
                return;
            }

            // GrappleSwing used to need an exclusion here too -- it owned
            // these same bones directly while a rope was attached. Rebuilt
            // from square one to never touch a bone at all (see its own
            // class doc comment), so a swing is just ordinary Animator
            // output now, exactly like a fall, and this correction applies
            // to it unconditionally like everything else.

            // NOTE: gun-aiming does NOT get a blanket exclusion here the
            // way swing/melee do -- see SuppressImplausibleArmSnapsCore's
            // own doc comment for why a blanket skip was tried and
            // reverted, and why only the ONE bone ApplyGunAimPose actually
            // touches (_rightUpperArm) is excluded, inside Core, instead.
            SuppressImplausibleArmSnapsCore();
        }

        /// <summary>
        /// EXPLICIT GAP, found live after the user reported the distortion
        /// still happening post-fix: SuppressImplausibleArmSnaps only ever
        /// runs from THIS component's own LateUpdate, but GrappleSwing.
        /// Attach and DevConsoleView.Open both force an EXTRA, synchronous
        /// zero-delta `Animator.Update(0f)` evaluation of their own --
        /// called from Update(), well before this frame's own LateUpdate
        /// would otherwise run -- specifically to warm up an
        /// never-evaluated Animator out of its raw bind pose (see either
        /// method's own doc comment). That forced evaluation can land on
        /// the exact same kind of implausible mid-transition blend-tree
        /// snap SuppressImplausibleArmSnaps exists to catch, and both
        /// GrappleSwing's CaptureBase and the console's freeze read the
        /// bone straight off it with no correction ever applied -- meaning
        /// a swing's ENTIRE held pose (or the console's whole frozen
        /// pose) could be built on top of one bad snap for as long as it
        /// stays engaged, not just a single invisible frame. Called
        /// manually, right after that forced Update(0f) and before
        /// anything reads the bones it just wrote.
        ///
        /// COMPARES AGAINST THE PERSISTENT _previousXxx STATE, NEVER A
        /// FRESH READ TAKEN RIGHT BEFORE THE FORCED UPDATE -- a
        /// `[UnityTest]` coroutine (and, by the same reasoning,
        /// PlayerInputReader's own real Update dispatch) resumes AFTER
        /// that frame's own automatic Mecanim evaluation but BEFORE that
        /// frame's own LateUpdate (see LocomotionAnimatorStabilityPlayTests's
        /// own doc comment for the full, separately-confirmed story) --
        /// meaning a bone read at the exact moment Attach/Open gets called
        /// can ALREADY be sitting on this frame's own raw, not-yet-corrected
        /// transitional value. _previousXxx is safe from this: it is only
        /// ever written from inside SuppressImplausibleArmSnaps's own
        /// LateUpdate call, i.e. AFTER a frame's Animation phase has
        /// already been corrected, so it always holds a genuinely safe,
        /// already-rendered reference from the last ordinary frame
        /// (SuppressImplausibleArmSnaps skips entirely while ownership is
        /// elsewhere, so this can occasionally be a few frames old across
        /// a swing -- acceptable here, since Attach/Open only calls this
        /// once, right at the ownership boundary, not every frame).
        ///
        /// SKIPS THE MeleeAttacker/GrappleSwing OWNERSHIP CHECKS ON
        /// PURPOSE, unlike the LateUpdate version -- this runs at the
        /// exact moment GrappleSwing is ABOUT to take ownership (IsActive
        /// is already true by the time Attach calls this) and needs a
        /// clean bone to capture as its own base, not during its own
        /// ongoing ownership.
        /// </summary>
        public void SuppressImplausibleArmSnapsNow()
        {
            SuppressImplausibleArmSnapsNowCore();
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: opening the console mid-jump "plays the
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
            // clamped. EXPLICIT BUG, found live by PoseDistortionWatchdogPlayTests.
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

        private void RememberCurrentArmRotationsWithoutCorrecting()
        {
            _previousHeadRotation = _head != null ? _head.localRotation : Quaternion.identity;
            _previousRightUpperArmRotation = _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity;
            _previousRightLowerArmRotation = _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity;
            _previousLeftUpperArmRotation = _leftUpperArm != null ? _leftUpperArm.localRotation : Quaternion.identity;
            _previousLeftLowerArmRotation = _leftLowerArm != null ? _leftLowerArm.localRotation : Quaternion.identity;
            _headSnapHeldLastFrame = false;
            _rightUpperArmSnapHeldLastFrame = false;
            _rightLowerArmSnapHeldLastFrame = false;
            _leftUpperArmSnapHeldLastFrame = false;
            _leftLowerArmSnapHeldLastFrame = false;
            _haveEverCapturedArmSnapBaseline = true;
        }

        /// <summary>
        /// Shared by both the ordinary per-frame LateUpdate path and the
        /// manual "Now" path. Guarded by _haveEverCapturedArmSnapBaseline
        /// for the one true edge case: the literal first call this
        /// component ever makes, with nothing yet to plausibly compare
        /// against.
        ///
        /// _rightUpperArm AND _head ARE SKIPPED SPECIFICALLY, NOT THE WHOLE
        /// METHOD -- EXPLICIT REGRESSION, found by the full suite: a first
        /// version of this gun-aim exclusion skipped this entire method
        /// whenever ApplyGunAimPose's own blend was nonzero, the same
        /// "something else legitimately owns these bones" shape the
        /// swing/melee exclusions in SuppressImplausibleArmSnaps use. That
        /// is too broad here -- ApplyGunAimPose only ever rotates
        /// _rightUpperArm and _head (head added later, see its own doc
        /// comment on the "angled left" head-cant bug), never
        /// leftArm/rightLowerArm, and RealisticChangingMovementNever
        /// SnapsAnArmOrHeadBoneOnItsOwn's own randomised input flips Aiming
        /// on a 20%-per-frame coin flip that keeps the gun-aim blend
        /// hovering above zero for long stretches (unrealistic for a real
        /// player holding right-click, but exactly what that stress test
        /// does) -- disabling the WHOLE check for that long let the
        /// pre-existing, already-documented rare Mecanim blend-tree glitch
        /// this test exists to catch (on the LEFT arm, or even
        /// RightLowerArm, neither of which gun-aiming ever touches) sneak
        /// through uncorrected. Excluding only the bones actually being
        /// driven keeps every other bone's own protection fully intact.
        ///
        /// THE HEAD'S OWN EXCLUSION IS BROADER THAN THE ARM'S, DELIBERATELY
        /// -- ApplyGunWalkHeadPose also drives the head (never the arm)
        /// whenever _gunWalkHeadBlend is positive, the hip-fire case
        /// _gunAimBlend alone does not cover. The right upper arm's own
        /// exclusion stays scoped to _gunAimBlend only, since hip-fire
        /// never touches it.
        /// </summary>
        private void SuppressImplausibleArmSnapsCore()
        {
            if (!_haveEverCapturedArmSnapBaseline)
            {
                RememberCurrentArmRotationsWithoutCorrecting();
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 1.0f / 240.0f);
            if (_gunAimBlend > 0.0f || _gunWalkHeadBlend > 0.0f)
            {
                // Keep the tracked baseline in sync with whatever
                // ApplyGunAimPose/ApplyGunWalkHeadPose just set, rather than
                // comparing against it (or leaving it stale) -- the same
                // "nothing useful to compare against while something else
                // legitimately drives this bone" reasoning
                // RememberCurrentArmRotationsWithoutCorrecting already uses
                // for the bootstrap case, so the FIRST frame after the
                // correction ends has a genuinely fresh, just-rendered
                // reference instead of a stale one.
                if (_head != null)
                {
                    _previousHeadRotation = _head.localRotation;
                }
                _headSnapHeldLastFrame = false;
            }
            else
            {
                SuppressBoneSnap(_head, ref _previousHeadRotation, ref _headSnapHeldLastFrame, deltaTime);
            }
            if (_gunAimBlend > 0.0f)
            {
                if (_rightUpperArm != null)
                {
                    _previousRightUpperArmRotation = _rightUpperArm.localRotation;
                }
                _rightUpperArmSnapHeldLastFrame = false;
            }
            else
            {
                SuppressBoneSnap(_rightUpperArm, ref _previousRightUpperArmRotation, ref _rightUpperArmSnapHeldLastFrame, deltaTime);
            }
            SuppressBoneSnap(_rightLowerArm, ref _previousRightLowerArmRotation, ref _rightLowerArmSnapHeldLastFrame, deltaTime);
            SuppressBoneSnap(_leftUpperArm, ref _previousLeftUpperArmRotation, ref _leftUpperArmSnapHeldLastFrame, deltaTime);
            SuppressBoneSnap(_leftLowerArm, ref _previousLeftLowerArmRotation, ref _leftLowerArmSnapHeldLastFrame, deltaTime);
        }

        /// <summary>
        /// The on-demand capture path (GrappleSwing.Attach/DevConsoleView.Open,
        /// via SuppressImplausibleArmSnapsNow) used to share this exact
        /// correction with the ordinary per-frame path above -- EXPLICIT
        /// BUG, found live: that sharing meant an UNRELATED hold from the
        /// ordinary path's own most recent LateUpdate (some earlier,
        /// completely different frame-to-frame glitch, nothing to do with
        /// this Attach/Open call at all) could leave its own
        /// "accept-unconditionally-next-call" flag set, and an on-demand
        /// call arriving right then would accept literally ANYTHING with
        /// zero validation -- including a genuinely bad mid-transition
        /// snap, at the EXACT moment (a capture or freeze) this whole
        /// mechanism exists to protect. A bounded RotateTowards catch-up
        /// was tried next to remove that cliff-edge, but that traded away
        /// something just as important: a single bounded correction only
        /// closes PART of a large jump, so a genuinely big glitch (the
        /// 4000-12000+ deg/s snaps this file's own history has repeatedly
        /// measured) still shows through substantially once frozen -- CONFIRMED
        /// live, the user reported zero improvement (and, if anything,
        /// more frequent visible distortion) after that version shipped.
        ///
        /// THE ACTUAL FIX: give this path its OWN independent hold state,
        /// touched by nothing else. A full, unconditional hold here can
        /// never be "wrong" the way the shared-state version was, because
        /// this method only ever runs ONCE per Attach/Open call -- there is
        /// no repeating per-frame process here to get permanently stuck,
        /// so the ordinary path's whole reason for ever accepting a
        /// still-implausible value (round 7's fix) does not apply. A
        /// held-at-last-known-good value is exactly the right, final
        /// answer for a one-shot capture: it can never read raw, unfiltered
        /// live data, and it can never contaminate (or be contaminated by)
        /// the ordinary path's own, separately-tracked recovery flags.
        /// </summary>
        private void SuppressImplausibleArmSnapsNowCore()
        {
            if (!_haveEverCapturedArmSnapBaseline)
            {
                RememberCurrentArmRotationsWithoutCorrecting();
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 1.0f / 240.0f);
            // See SuppressImplausibleArmSnapsCore's own comment on why the
            // head's exclusion is broader than the right upper arm's.
            if (_gunAimBlend > 0.0f || _gunWalkHeadBlend > 0.0f)
            {
                if (_head != null)
                {
                    _previousHeadRotation = _head.localRotation;
                }
            }
            else
            {
                SuppressBoneSnapForCapture(_head, ref _previousHeadRotation, deltaTime);
            }
            if (_gunAimBlend > 0.0f)
            {
                if (_rightUpperArm != null)
                {
                    _previousRightUpperArmRotation = _rightUpperArm.localRotation;
                }
            }
            else
            {
                SuppressBoneSnapForCapture(_rightUpperArm, ref _previousRightUpperArmRotation, deltaTime);
            }
            SuppressBoneSnapForCapture(_rightLowerArm, ref _previousRightLowerArmRotation, deltaTime);
            SuppressBoneSnapForCapture(_leftUpperArm, ref _previousLeftUpperArmRotation, deltaTime);
            SuppressBoneSnapForCapture(_leftLowerArm, ref _previousLeftLowerArmRotation, deltaTime);
        }

        /// <summary>
        /// Full, unconditional hold at `previous` -- no bounding, no
        /// advancing, no flag. See SuppressImplausibleArmSnapsNowCore's own
        /// doc comment for why a one-shot capture call needs none of the
        /// ordinary per-frame path's own recovery machinery.
        /// </summary>
        private static void SuppressBoneSnapForCapture(Transform bone, ref Quaternion previous, float deltaTime)
        {
            if (bone == null)
            {
                return;
            }
            Quaternion current = bone.localRotation;
            float degreesPerSecond = Quaternion.Angle(previous, current) / deltaTime;
            if (degreesPerSecond > MaxPlausibleArmDegreesPerSecond)
            {
                bone.localRotation = previous;
            }
            else
            {
                previous = current;
            }
        }

        /// <summary>
        /// HOLD-THEN-ACCEPT-NEXT-FRAME. Holding a bone at `previous`
        /// outright (leaving `previous` itself untouched) self-corrects a
        /// genuine one-frame Mecanim glitch fine, but a LEGITIMATE fast
        /// transition (landing, finishing a swing, a sharp direction
        /// change -- not a glitch, a real new pose that just arrived in
        /// one frame) trips the exact same check -- without this method's
        /// own `heldLastFrame` flag, it would never recover: every later
        /// frame's own ordinary continuation of that new pose would look
        /// "implausible" against the same, increasingly stale reference
        /// forever. `heldLastFrame` bounds the hold to exactly one frame:
        /// the very next call accepts whatever the Animator is doing now
        /// unconditionally instead of re-checking it, so a real glitch
        /// (self-corrects within a frame or two) still gets hidden, but a
        /// legitimate fast transition can never lock up permanently.
        ///
        /// SAFE TO DO THIS HERE, UNLIKE THE ON-DEMAND CAPTURE PATH --
        /// EXPLICIT BUG, found live: an earlier version shared this exact
        /// flag/logic with SuppressImplausibleArmSnapsNow (GrappleSwing.
        /// Attach/DevConsoleView.Open's own one-shot capture call), and an
        /// UNRELATED hold from this method's own most recent, completely
        /// separate LateUpdate call could leave the flag set such that the
        /// next on-demand call accepted literally anything with zero
        /// validation -- at the exact moment (a capture or freeze) that
        /// validation matters most. `heldLastFrame` is now private to THIS
        /// per-frame path alone (see its own field doc comment); the
        /// on-demand path has its own, completely separate, always-strict
        /// SuppressBoneSnapForCapture instead. This method's own
        /// one-frame grace period is safe specifically because nothing
        /// else ever observes or acts on a single frame of this path's own
        /// output the way a capture moment does -- ordinary unfrozen play
        /// has no "instant" for a one-frame recovery lag to be visible in.
        /// </summary>
        private static void SuppressBoneSnap(Transform bone, ref Quaternion previous, ref bool heldLastFrame, float deltaTime)
        {
            if (bone == null)
            {
                return;
            }
            Quaternion current = bone.localRotation;

            if (heldLastFrame)
            {
                previous = current;
                heldLastFrame = false;
                return;
            }

            float degreesPerSecond = Quaternion.Angle(previous, current) / deltaTime;
            if (degreesPerSecond > MaxPlausibleArmDegreesPerSecond)
            {
                bone.localRotation = previous;
                heldLastFrame = true;
            }
            else
            {
                previous = current;
            }
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
        /// Sets/clears IsSitting -- just the flag; ApplySittingPose's own
        /// procedural leg bend (LateUpdate) is what actually shows it. NOT
        /// a swapped Animator state: EXPLICIT VISUAL FINDING (see
        /// ApplySittingPose's own doc comment) that reusing entering_car's
        /// baked hip/leg pose for a bench looked broken no matter how the
        /// root was positioned -- that clip's own shape is built for
        /// reaching a car's wheel, not sitting upright. Bending the real
        /// leg bones directly gives full control over the result instead.
        /// </summary>
        public void SetSitting(bool sitting)
        {
            IsSitting = sitting;
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
        ///
        /// <paramref name="useLeftArm"/>: A LATER, SEPARATE ask -- the
        /// grapple gun occupies the right hand, so casting while it is
        /// equipped raises the left arm instead ("it still works just comes
        /// from your left hand"). PlayerInputReader passes
        /// GrappleSwing.EquippedHook != null for this every frame it calls
        /// SetCastPose, so the raised arm follows whichever hand is
        /// actually free without this needing to know about weapons itself.
        /// </summary>
        public void SetCastPose(float fraction, bool useLeftArm = false)
        {
            _castPoseFraction = Mathf.Clamp01(fraction);
            _castUsesLeftArm = useLeftArm;
        }

        /// <summary>
        /// Layers onto the arm's LIVE animated rotation, refreshed fresh
        /// every frame rather than a one-time captured base the way
        /// ApplyDrivingWheelPose uses -- a cast can happen mid-walk or
        /// mid-run, and the raise is meant to track whatever the current
        /// locomotion pose is doing with that arm, not freeze it to a
        /// snapshot from the moment the cast began.
        ///
        /// "LIVE" ONLY WHILE THE ANIMATOR IS ACTUALLY EVALUATING IT, THOUGH.
        /// EXPLICIT BUG REPORT ("stretched out... until I left console" /
        /// mid-swing contortion): this used to read arm.localRotation back
        /// off the bone itself every frame and multiply the offset onto
        /// THAT -- which is only ever the clean, un-offset clip pose
        /// because the Animator overwrites this same bone with it earlier
        /// in the very same frame, every frame, while it is enabled. The
        /// instant the Animator is disabled for consecutive frames (the
        /// dev console freezing the pose, or a grapple swing owning it) and
        /// a cast is still in progress, nothing refreshes the bone between
        /// frames any more, so that read-back was re-multiplying last
        /// frame's OWN already-offset output into itself -- a genuine
        /// runaway, same shape as GrappleSwing's own leg-sway/ground-clamp
        /// feedback loop (see PlayerLocomotion.ClampFeetAboveGround's own
        /// doc comment) but from an unrelated cause. Fixed by only
        /// refreshing the captured base while the Animator is actually
        /// live, and composing off THAT rather than off the bone's own
        /// current (possibly already-compounded) value -- matches
        /// ApplyDrivingWheelPose's own base-capture shape, just re-captured
        /// every live frame instead of once per drive.
        ///
        /// THE LEFT ARM'S OWN RAISE IS THE MIRROR IMAGE, not the same
        /// signed angle -- confirmed by this rig's own established mirror
        /// convention: PlayerRigBuilder/CaptureGrip's own PoseArmsDown pose
        /// already rotates arm_l by +78 degrees and arm_r by -78 for what
        /// is visually the SAME hanging-down shape on both sides, so a
        /// raise that reads as "up and out" on the right needs the opposite
        /// sign on the left to read the same way rather than folding the
        /// arm across the chest.
        /// </summary>
        private void ApplyCastingPose()
        {
            if (_castPoseFraction <= 0.0f)
            {
                return;
            }
            Transform arm = _castUsesLeftArm ? _leftUpperArm : _rightUpperArm;
            if (arm == null)
            {
                return;
            }
            if ((_animator != null && _animator.enabled) || _castUsesLeftArm != _castBaseIsLeftArm)
            {
                _castBaseRotation = arm.localRotation;
                _castBaseIsLeftArm = _castUsesLeftArm;
            }
            float sign = _castUsesLeftArm ? -1.0f : 1.0f;
            Quaternion offset = Quaternion.AngleAxis(CastRaiseDegrees * _castPoseFraction * sign, Vector3.forward);
            arm.localRotation = _castBaseRotation * offset;
        }

        /// <summary>
        /// How far the gun-arm is allowed to swing off the body's own
        /// forward direction, in any direction -- a real arm aiming a
        /// one-handed pistol has a genuine, comfortable range well short of
        /// the camera's own full +/-90 pitch (or an extreme lateral
        /// parallax angle up close), so this clamps rather than mapping 1:1
        /// (which would hyperextend the shoulder looking straight up/down,
        /// or aiming at something right beside the camera).
        /// </summary>
        public const float GunAimPitchClampDegrees = 55.0f;

        /// <summary>
        /// How far ahead, along the camera's own forward ray, the gun arm
        /// aims AT -- not the pistol's own Range (60m): a real shot fired
        /// from the camera's own position converges toward the body's
        /// centreline by the time it travels that far regardless, so the
        /// visible difference beyond a more modest distance is negligible,
        /// and a shorter convergence distance keeps the arm pose reacting
        /// clearly to something close (a wall, another character) rather
        /// than aiming at a point so far away the pose barely changes.
        /// </summary>
        public const float GunAimConvergenceDistance = 15.0f;

        /// <summary>How fast the arm comes up for a shot when NOT aiming (about 55ms to full, on an S-curve: see EasedGunAimBlend).</summary>
        public const float GunFireBlendPerSecond = 18.0f;

        /// <summary>
        /// The time constant (seconds) of the easing on how far along the camera ray the arm aims -- see
        /// ApplyGunAimPose. Short enough that the gun is on what the crosshair is on by the time a shot is
        /// placed; long enough that a one-frame flick of the hit depth is a ripple, not a snap.
        /// </summary>
        public const float GunAimDepthSmoothSeconds = 0.07f;

        /// <summary>
        /// How slowly the aim depth comes IN (the crosshair landing on something nearer). Against a half ledge or a railing the crosshair ray flickers
        /// across the edge between the ledge a metre or two away and the street beyond it, and following every flip made the gun spasm: coming in is eased
        /// over a quarter of a second, going out stays quick.
        /// </summary>
        public const float GunAimDepthCloserSmoothSeconds = 0.25f;

        /// <summary>Changes of aim depth smaller than this (in log space, about 12%) are ignored: the edge of a ledge under a trembling crosshair.</summary>
        public const float GunAimDepthDeadZone = 0.12f;

        private BloodrageAbility _bloodrage;
        private bool _bloodrageLooked;
        private float _aimLogDepth;
        private bool _aimDepthValid;

        /// <summary>The aim point is never closer than this to the shoulder along the camera ray (aiming at your own feet swings the arm absurdly).</summary>
        public const float GunAimPointMinMetres = 4.0f;

        /// <summary>How fast _gunAimBlend eases toward 1 (or back to 0) -- reaches full strength in ~1/6s, comfortably inside the Animator's own 0.12s crossfade into the Aiming state.</summary>
        public const float GunAimBlendPerSecond = 6.0f;

        /// <summary>
        /// Extra yaw added on top of the camera's own while aiming and
        /// standing still (see Tick's own "AIMING SNAPS INSTANTLY" branch) --
        /// EXPLICIT ASK: "let me adjust the pov and model rotation manually
        /// ... set up something using ingame console," after the plain
        /// snap-to-camera-yaw still didn't read right for every camera
        /// setup. A live, mutable field (not a const, which the /aim
        /// console command couldn't reassign) so it can be tuned in-game
        /// via /aim yaw and baked into this default afterward, the same
        /// "tune live, then bake the winning number" workflow already
        /// established for /jacket and /pistol.
        /// </summary>
        public float AimYawOffsetDegrees = 0.0f;

        /// <summary>
        /// Extra yaw applied to the ARM AND HEAD's shared convergence
        /// target (ApplyGunAimPose's own targetDirection feeds both) --
        /// see that method's own comment on why AimYawOffsetDegrees
        /// (body-only) structurally cannot reach either. Tuned live via
        /// /aim armyaw (arm+head only) or /aim yaw (which now sets this
        /// AND AimYawOffsetDegrees together, so the whole character turns
        /// as one piece instead of the body twisting away from a
        /// still-camera-locked arm/head).
        /// </summary>
        public float ArmAimYawOffsetDegrees = 0.0f;

        /// <summary>
        /// Walking speed while aiming down sights, as a fraction of normal.
        /// Live-tunable with /gun aimspeed. The directional gun-walk clips blend
        /// on the body's real local velocity, so slowing down needs no new
        /// animation -- 0.8 is mild enough not to make the stride visibly slide.
        /// </summary>
        public float AimMoveSpeedMultiplier = 0.8f;

        /// <summary>
        /// EXPLICIT BUG REPORT, twice over. First: "the pistol aimed in
        /// pose is like positioned to the left slightly make it point
        /// forward and turn with the mouse" -- pistol_aim_in is a single
        /// static clip with no pitch tracking at all, so looking up or down
        /// while aiming left the gun always pointing along the same fixed
        /// level line regardless of where the camera (and the actual
        /// hitscan direction PistolWeapon.Fire uses) really pointed. That
        /// first fix pitched the arm around the body's own forward axis,
        /// which fixed up/down but not left/right: the SECOND report,
        /// later, was "the pose he goes in when you hold right click is
        /// aimed to the left slightly he isnt even pointing the gun
        /// forward" -- because the camera sits laterally offset from the
        /// body's own centreline while aiming (ThirdPersonCamera's own
        /// AimShoulderOffset), and a shot fires along the CAMERA's own ray,
        /// not the body's. An arm merely pitched around the centreline
        /// visibly diverges from where the gun is actually about to shoot
        /// -- real parallax, not a sign or axis mistake.
        ///
        /// AIMS AT THE POINT THE CAMERA'S OWN RAY CONVERGES ON
        /// (GunAimConvergenceDistance ahead of it) instead, which corrects
        /// pitch AND the lateral parallax at once, with no separate
        /// handling needed for either -- see the method body's own comment
        /// for the fallback when no real camera data is available at all.
        ///
        /// TARGETS AN ABSOLUTE WORLD DIRECTION (BendLimbToward), NOT an
        /// additive offset on the animated base the way ApplyCastingPose
        /// works -- a continuously-tracked aim needs to re-converge on the
        /// true target every frame regardless of whatever the clip's own
        /// pose is doing, the same reasoning ApplySittingPose's own doc
        /// comment gives for the identical choice.
        ///
        /// BLENDED IN OVER _gunAimBlend, NOT APPLIED AT FULL STRENGTH THE
        /// INSTANT AIMING STARTS -- EXPLICIT REGRESSION, found by the full
        /// suite: BendLimbToward is a hard re-target, and several
        /// randomised PlayMode tests occasionally set Aiming=true as part
        /// of realistic varied input with no gun equipped at all
        /// (DevConsoleAnimatorFreezePlayTests/LocomotionAnimatorStability
        /// PlayTests/etc.) -- PoseDistortionWatchdog correctly caught the
        /// resulting one-frame snap the moment Aiming flipped true, exactly
        /// the class of bug this project has spent a great deal of effort
        /// eliminating elsewhere (see grapple_swing_deprioritized memory).
        /// Easing the CORRECTION'S OWN STRENGTH in via Slerp, rather than
        /// snapping the bone straight to the target, keeps the first frame's
        /// rotation small regardless of how far the target direction is
        /// from wherever the arm already was -- both fixing the test
        /// failures and reading as a more natural "settling into aim"
        /// motion than an instant teleport would have anyway.
        /// </summary>
        private void ApplyGunAimPose()
        {
            float deltaTime = Mathf.Max(_lastTickDeltaTime, 0.0f);
            _gunAimBlend = Mathf.MoveTowards(
                _gunAimBlend, (_isAimingGun || _gunFireHold > 0.0f) ? 1.0f : 0.0f,
                // The arm swings up FAST for a shot: the tracer only lives ~0.12s, and
                // at the ordinary 6/s (0.17s) the gun was still coming up while the
                // streak was already gone.
                (!_isAimingGun && _gunFireHold > 0.0f ? GunFireBlendPerSecond : GunAimBlendPerSecond) * deltaTime);
            // EXCLUDES AIMING (!_isAimingGun), NOT JUST GunEquipped alone --
            // see this method's own "HIP-FIRE HEAD" comment below for why
            // this and _gunAimBlend must never both be applying a head
            // correction on the same frame.
            _gunWalkHeadBlend = Mathf.MoveTowards(
                _gunWalkHeadBlend, (_gunEquipped && !_isAimingGun && _gunFireHold <= 0.0f) ? 1.0f : 0.0f, GunAimBlendPerSecond * deltaTime);

            if (_gunAimBlend <= 0.0f || _rightUpperArm == null || _rightLowerArm == null)
            {
                _aimDepthValid = false;   // the next time the gun comes up it starts from where the crosshair is then
                ApplyGunWalkHeadPose();
                return;
            }

            // EXPLICIT REGRESSION, found by the full suite (the same class
            // this method's own blend-easing doc comment already
            // describes fixing once): several PlayMode tests drive
            // PlayerLocomotion.Tick with a directly-constructed
            // LocomotionInput that never sets AimCameraPosition/Forward at
            // all, which leaves _gunAimCameraForward at its struct
            // default -- an impossible value for a REAL forward vector
            // (always unit length), reliably telling apart "no real camera
            // data supplied" from "camera happens to face world +Z." Aiming
            // at a fixed, player-position-independent world point in that
            // case (rather than degrading to plain body-forward, what the
            // pitch-only formula this replaced always effectively did) is
            // what PoseDistortionWatchdog caught: the target direction
            // itself was wrong for the whole time Aiming stayed true, not
            // just the one already-handled first frame the blend eases.
            Vector3 desiredDirection;
            _hasAimedPoint = false;
            if (_gunAimCameraForward.sqrMagnitude < 0.0001f)
            {
                desiredDirection = transform.forward;
            }
            else
            {
                // AIM AT WHERE THE SHOT REALLY GOES when the reader supplied it (the
                // crosshair ray's actual hit point), so the barrel, the bullet and
                // the tracer all agree; only fall back to a fixed 15m along the ray
                // when there is no such point, or it is too close to aim at sanely.
                //
                // THE DEPTH IS SMOOTHED, THE DIRECTION IS NOT. What the crosshair ray hits is not a continuous
                // thing: across a facade with shot-out windows it flicks between the glass at 3m and the room
                // behind at 30m, a lamp post or a flying shard is a one-frame hit at 2m. Seen from the shoulder
                // (the camera is ~4m behind the gun and to one side) a 3m and a 30m point on the same ray are
                // ~20 degrees apart, and the arm followed every jump instantly -- the reported "pointing in
                // other directions". So the aim point stays ON the camera ray (the mouse still turns the arm
                // with no lag at all) and only how far along it the arm looks is eased, in log space (a jump
                // from 3m to 30m is the same size of step as 30m to 300m).
                Vector3 cameraForward = _gunAimCameraForward.normalized;
                Vector3 shoulder = _rightUpperArm.position;
                float minDepth = Mathf.Max(0.5f, Vector3.Dot(shoulder - _gunAimCameraPosition, cameraForward)) + GunAimPointMinMetres;
                float rawDepth = GunAimConvergenceDistance;
                if (_hasGunAimPoint)
                {
                    rawDepth = Mathf.Max(minDepth, Vector3.Dot(_gunAimPoint - _gunAimCameraPosition, cameraForward));
                }
                float wantedLogDepth = Mathf.Log(Mathf.Max(minDepth, rawDepth));
                if (!_aimDepthValid)
                {
                    _aimLogDepth = wantedLogDepth;   // starting to aim: nothing to be smooth with yet
                    _aimDepthValid = true;
                }
                else
                {
                    float change = wantedLogDepth - _aimLogDepth;
                    if (Mathf.Abs(change) > GunAimDepthDeadZone)
                    {
                        float seconds = change < 0.0f ? GunAimDepthCloserSmoothSeconds : GunAimDepthSmoothSeconds;
                        _aimLogDepth = Mathf.Lerp(_aimLogDepth, wantedLogDepth, 1.0f - Mathf.Exp(-deltaTime / seconds));
                    }
                }
                Vector3 aimPoint = _gunAimCameraPosition + cameraForward * Mathf.Exp(_aimLogDepth);
                _aimedPoint = aimPoint;
                _hasAimedPoint = true;
                desiredDirection = (aimPoint - _rightUpperArm.position).normalized;
            }
            // Clamps the TOTAL swing off dead-ahead (pitch and yaw together),
            // the same reason RotateTowards -- not a separate axis-by-axis
            // clamp -- is the right tool: a hyperextended shoulder is just
            // as wrong from lateral parallax up close as it is from an
            // extreme camera pitch, and this bounds both the same way.
            Vector3 targetDirection = Vector3.RotateTowards(
                transform.forward, desiredDirection, GunAimPitchClampDegrees * Mathf.Deg2Rad, 0.0f);
            // A SEPARATE KNOB FROM AimYawOffsetDegrees -- EXPLICIT BUG REPORT:
            // "the commands your giving arent rotating the part of the body
            // i want." AimYawOffsetDegrees only ever rotates the BODY ROOT;
            // this arm target is computed from the CAMERA's own convergence
            // point, entirely independent of body yaw, specifically so the
            // gun tracks the crosshair regardless of how the body is turned
            // -- which means no amount of AimYawOffsetDegrees can ever visibly
            // move the ARM/GUN itself. If the gun's own angle (not the
            // torso/legs) is what looks wrong, THIS is the value that
            // actually reaches it.
            targetDirection = Quaternion.AngleAxis(ArmAimYawOffsetDegrees, Vector3.up) * targetDirection;
            Vector3 currentDirection = (_rightLowerArm.position - _rightUpperArm.position).normalized;
            Vector3 blendedDirection = Vector3.Slerp(currentDirection, targetDirection, EasedGunAimBlend);
            BendLimbToward(_rightUpperArm, _rightLowerArm.position, blendedDirection);

            // THEN AIM THE BARREL ITSELF. Pointing the UPPER ARM at the target only
            // points the shoulder-to-elbow segment; the forearm, hand and grip carry
            // the clip's own bend, so the gun's barrel ended up ~20 degrees off
            // even with the arm "on target" (the offset the /aim armyaw knob was
            // invented to hand-tune). With the real muzzle known, measure where the
            // barrel actually points and rotate the whole arm by the remaining error:
            // a rigid rotation about the shoulder turns the barrel by exactly that.
            if (GunMuzzle != null && _hasAimedPoint)
            {
                // A FEW PASSES, not one: turning the arm about the shoulder also MOVES
                // the muzzle (about 0.7m from the shoulder), which shifts the direction
                // it should point at a close target -- each pass halves what is left.
                for (int pass = 0; pass < GunBarrelCorrectionPasses; pass++)
                {
                    Vector3 barrel = GunMuzzle.forward;
                    Vector3 wantedBarrel = (_aimedPoint - GunMuzzle.position).normalized;
                    Quaternion correction = Quaternion.FromToRotation(barrel, wantedBarrel);
                    correction.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180.0f)
                    {
                        angle -= 360.0f;   // the short way round
                    }
                    angle = Mathf.Clamp(angle, -GunBarrelCorrectionMaxDegrees, GunBarrelCorrectionMaxDegrees);
                    if (Mathf.Abs(angle) < 0.05f)
                    {
                        break;
                    }
                    correction = Quaternion.AngleAxis(angle * EasedGunAimBlend, axis);
                    _rightUpperArm.rotation = correction * _rightUpperArm.rotation;
                }
            }

            // THE HEAD, SAME TREATMENT -- EXPLICIT BUG REPORT: "the head is
            // still being angled to the left slightly when aiming in just
            // have it face forward and move with the mouse obviously."
            // pistol_aim_in.fbx/pistol_walk.fbx's own retargeted mocap bakes
            // whatever the source actor's head happened to be doing (a
            // realistic sighting cant, slightly off the body's own
            // centreline) into the head bone's LOCAL rotation -- which the
            // body's own instant snap-to-camera (see Tick's own rotation
            // comment) does nothing to correct, since a local bone rotation
            // is relative to its parent and untouched by the root turning.
            // Reuses targetDirection (the SAME clamped camera-converged
            // direction the arm above already aims at) rather than a
            // separate calculation -- down the sights, the eye line and the
            // gun's own barrel point the same way anatomically, and it means
            // the head visually tracks the mouse exactly as far as the arm
            // itself is allowed to swing (GunAimPitchClampDegrees), not
            // independently.
            //
            // .forward, NOT a child-position BendLimbToward call -- the head
            // has no natural "child bone" the way an upper arm has a lower
            // arm to measure a segment direction from. Unity's Humanoid
            // retargeting normalises every bone into a common orientation
            // space where local +Z is the anatomical forward for that bone
            // regardless of the source rig's own raw axes, so `.forward` is
            // exactly "the direction the face is pointing" for any correctly
            // retargeted Humanoid head bone.
            if (_head != null)
            {
                Vector3 currentHeadForward = _head.forward;
                Vector3 blendedHeadForward = Vector3.Slerp(currentHeadForward, targetDirection, EasedGunAimBlend);
                Quaternion headDelta = Quaternion.FromToRotation(currentHeadForward, blendedHeadForward);
                _head.rotation = headDelta * _head.rotation;
            }
        }

        /// <summary>
        /// HIP-FIRE HEAD -- EXPLICIT BUG REPORT, after the aim-only head fix
        /// above shipped: "the walking animation and his head is still
        /// angled to the left slightly why is it doing this... there was no
        /// issue in godot." The fix above only ever runs while
        /// _gunAimBlend is positive, i.e. while right-click is actually
        /// held (or still easing out of it) -- ordinary hip-fire movement
        /// with the gun equipped (ApplyGunAimPose's own early return above
        /// takes THIS branch whenever _isAimingGun is false) never touched
        /// the head at all, so pistol_walk.fbx's own baked head cant --
        /// the SAME retargeting artifact the aim-in clip has, see this
        /// class's own GunEquipped field comment -- showed straight
        /// through uncorrected during ordinary walking, which is most of
        /// the time a drawn pistol is actually on screen.
        ///
        /// TARGETS transform.forward, NOT the camera-converged
        /// targetDirection the aiming correction above uses -- there is no
        /// "aim point" to converge on while just walking with the gun
        /// drawn (hip-fire has no crosshair-lock in this game, matching
        /// Godot's own gun_walk, which never raises the gun to eye level
        /// either), so "face the same way the body itself is already
        /// facing" is the correct target, not a reason to fall back to
        /// reading camera fields that would not mean the same thing here.
        ///
        /// MUTUALLY EXCLUSIVE WITH THE AIMING CORRECTION ABOVE BY
        /// CONSTRUCTION, not by an extra guard -- this is only ever called
        /// from ApplyGunAimPose's own early-return branch, which is only
        /// reached when _gunAimBlend is already at zero.
        /// </summary>
        private void ApplyGunWalkHeadPose()
        {
            if (_gunWalkHeadBlend <= 0.0f || _head == null)
            {
                return;
            }

            Vector3 currentHeadForward = _head.forward;
            Vector3 blendedHeadForward = Vector3.Slerp(currentHeadForward, transform.forward, _gunWalkHeadBlend);
            Quaternion headDelta = Quaternion.FromToRotation(currentHeadForward, blendedHeadForward);
            _head.rotation = headDelta * _head.rotation;
        }

        /// <summary>
        /// How far below dead-horizontal the thigh's TARGET direction
        /// points -- purely horizontal reads as the leg floating with no
        /// weight actually on the seat; a slight downward cant looks like
        /// the thigh genuinely resting on the cushion.
        /// </summary>
        private const float SittingThighDownwardDegrees = 8.0f;

        /// <summary>
        /// Bends the real leg bones into a seated shape every frame while
        /// IsSitting -- EXPLICIT VISUAL FINDING, not a guess: a scratch
        /// capture showed BenchSeat's original design (reusing
        /// entering_car.fbx, this rig's only existing "seated" clip)
        /// looking broken no matter where the root was placed, because
        /// that clip's own hip/leg shape is built for reaching a car's low
        /// wheel with legs stretched flat, not sitting upright with knees
        /// bent -- EXPLICIT FEEDBACK confirmed it: "make him sit on his
        /// but[t]." Rotating LeftUpperLeg/RightUpperLeg/LeftLowerLeg/
        /// RightLowerLeg directly gives a real "seated, knees bent, feet
        /// down" silhouette instead, the same additive-on-top-of-the-live-
        /// Animator-pose idiom ApplyCastingPose/ApplyDrivingWheelPose
        /// already use for the identical reason ("no real clip fits").
        ///
        /// TARGETS AN ABSOLUTE WORLD DIRECTION PER LIMB (BendLimbToward),
        /// NOT a fixed "rotate by N degrees" delta -- a first version used
        /// Transform.Rotate(axis, degrees, Space.World), and a scratch
        /// numeric diagnostic caught it landing at wildly different
        /// resulting angles for the SAME commanded degrees depending on
        /// the leg's own live idle-sway starting rotation (86 degrees
        /// commanded measured as a 22-degree real change; 172 measured as
        /// 147). Aiming each bone at an absolute target direction instead
        /// is self-correcting regardless of whatever the Animator's
        /// current idle pose happens to be doing to that bone, which a
        /// relative delta fundamentally cannot be.
        /// </summary>
        private void ApplySittingPose()
        {
            if (_leftUpperLeg == null || _rightUpperLeg == null ||
                _leftLowerLeg == null || _rightLowerLeg == null ||
                _leftFoot == null || _rightFoot == null)
            {
                return;
            }

            Vector3 thighTarget = Quaternion.AngleAxis(SittingThighDownwardDegrees, transform.right) * transform.forward;
            BendLimbToward(_leftUpperLeg, _leftLowerLeg.position, thighTarget);
            BendLimbToward(_rightUpperLeg, _rightLowerLeg.position, thighTarget);

            // Read AFTER the thigh bend above -- _leftFoot/_rightFoot are
            // grandchildren of the thigh, so their world positions already
            // reflect the thigh's new rotation by the time these run.
            BendLimbToward(_leftLowerLeg, _leftFoot.position, Vector3.down);
            BendLimbToward(_rightLowerLeg, _rightFoot.position, Vector3.down);
        }

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
            //
            // DIVING WINS OVER PLAIN BOOSTING -- checked first, since
            // JetpackDiving only ever fires alongside JetpackBoosting (see
            // its own doc comment) and needs the bigger number to actually
            // win rather than the two conditions racing in an OR.
            float speed = input.JetpackDiving ? SprintSpeed * JetpackDiveSpeedMultiplier
                : (sprinting || input.JetpackBoosting) ? SprintSpeed
                : WalkSpeed;

            // AIMING DOWN SIGHTS SLOWS YOU (Fortnite, Gears, The Division all
            // do): you trade speed for a steady, zoomed aim. Not while the
            // jetpack is flying you -- that is not walking.
            if (input.Aiming && !input.JetpackBoosting && !input.JetpackDiving)
            {
                speed *= AimMoveSpeedMultiplier;
            }
            // BLOODRAGE: +25% movement speed on foot (a jetpack's own speed is its own business).
            if (!input.JetpackBoosting && !input.JetpackDiving)
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
            }
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
            else if (KeepsAirMomentum && !_controller.isGrounded)
            {
                // MOMENTUM CARRIES (the aerial sword spin, until the body lands). The ordinary easing below pulls the velocity toward
                // what the keys ask for at 60 m/s^2, and a spin asks for nothing -- so the body used to brake to a dead stop in mid-air,
                // twice: when the spin took the keys away, and again when it handed them back with none held. Here the velocity is
                // simply kept, and the keys only STEER it: they add acceleration (AirMomentumSteerAcceleration), can slow it by
                // opposing it, and cannot build it past what it already was (or a walk, if it was slower).
                Vector3 carried = _horizontalVelocity;
                Vector3 steered = carried + direction * (AirMomentumSteerAcceleration * deltaTime);
                float cap = Mathf.Max(carried.magnitude, WalkSpeed);
                if (steered.sqrMagnitude > cap * cap)
                {
                    steered = steered.normalized * cap;
                }
                _horizontalVelocity = steered;
                horizontal = steered;
            }
            else
            {
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity, targetHorizontal, HorizontalAcceleration * WetTraction() * deltaTime);
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
                _verticalVelocity -= Gravity * GravityScale * deltaTime;
            }

            if (grounded && input.Jump)
            {
                _verticalVelocity = JumpVelocity;
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
                if (grounded)
                {
                    float heightMultiplier = Mathf.Lerp(
                        1.0f, ChargedJumpHeightMultiplier, Mathf.Clamp01(input.JumpChargeFraction));
                    _verticalVelocity = JumpVelocity * Mathf.Sqrt(heightMultiplier);
                }
                FireTrigger(JumpParameter);
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
            LastMoveCollisionFlags = _controller.Move(motion * deltaTime);

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
            // FIRST, ahead of "moving". EXPLICIT BUG REPORT: "fix the pistol
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
            if (input.Aiming || _gunFireHold > 0.0f)
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
            else if (moving)
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
                _animator.SetFloat(SpeedParameter, CurrentSpeed);
                _animator.SetFloat(MoveXParameter, LocalVelocity.x);
                _animator.SetFloat(MoveZParameter, LocalVelocity.y);
                _animator.SetBool(GroundedParameter, _controller.isGrounded);
                _animator.SetBool(CrouchingParameter, IsCrouching);
                _animator.SetBool(ChargingJumpParameter, input.ChargingJump);
                _animator.SetBool(AimingParameter, input.Aiming);
                _animator.SetBool(SwordEquippedParameter, input.SwordEquipped);
                _animator.SetBool(GunEquippedParameter, input.GunEquipped);
                _animator.SetBool(JetpackThrustingParameter, input.JetpackThrusting);
                _animator.SetBool(DancingParameter, input.Dancing);
                _animator.SetBool(BreakdancingParameter, input.Breakdancing);
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
