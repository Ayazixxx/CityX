using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE CAST POSE: an arm raised for a spell (SetCastPose), the left one when the right is holding something.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
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

        private bool _castBaseIsLeftArm;

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
        /// Bug report ("stretched out... until I left console" /
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
    }
}
