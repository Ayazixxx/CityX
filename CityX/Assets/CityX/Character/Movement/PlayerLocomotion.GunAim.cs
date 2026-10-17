using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// AIMING A GUN: the arm and the barrel brought onto what the camera looks at, the head with them, and the arm kept up for a
    /// moment after a shot fired from the hip.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
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

            // A RIFLE is held two-handed with its stock in the shoulder, always: its own pose places the gun and the arms follow it
            // (PlayerLocomotion.RifleAim.cs). It shares the aim point (SmoothedGunAimPoint) and the fire hold, not the pistol's arm-bending.
            if (_rifleGun != null)
            {
                ApplyRiflePose(deltaTime);
                return;
            }

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
                Vector3 aimPoint = SmoothedGunAimPoint(_rightUpperArm.position, deltaTime);
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
    }
}
