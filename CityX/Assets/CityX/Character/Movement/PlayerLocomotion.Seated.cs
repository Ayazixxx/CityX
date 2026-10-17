using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// SEATED: in a car (the hands on the wheel turning with it, getting in and out) and sitting on a seat or a bench.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
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
    }
}
