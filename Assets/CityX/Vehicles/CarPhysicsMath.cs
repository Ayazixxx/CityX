using UnityEngine;

namespace CityX.Vehicles
{
    /// <summary>
    /// The pure numbers behind Car's own GTA-style arcade handling --
    /// ported from vehicles/car.gd's private helper methods (_update_steering,
    /// _apply_engine_force, _apply_drag, _apply_grip, _apply_angular,
    /// _angular_response_curve), factored out exactly the way this project's
    /// own JetpackTests/ClaimCutsceneMathTests split "the math" from "the
    /// MonoBehaviour" -- so the numbers that decide how driving feels can be
    /// tested without a live Rigidbody or a physics step at all.
    ///
    /// UNITS MATCH Car's OWN FIELDS EXACTLY (metres, metres/second, degrees
    /// for angles a person tunes, radians for angles the physics carries) --
    /// see each method's own parameter names for which is which.
    /// </summary>
    public static class CarPhysicsMath
    {
        // --- Implementation-level constants (not exposed on Car -- these
        // shape HOW the tunables apply, not what "feels right," matching
        // car.gd's own identical split between @export tunables and these). ---
        public const float RearGripWeight = 0.95f;
        public const float GripBlendSpeed = 6.0f;
        public const float LateralCancelRate = 8.0f;
        public const float DriftGripRecoveryFraction = 0.85f;
        public const float DriftMinSpeed = 3.0f;
        public const float NormalAngularLerpSpeed = 10.0f;
        public const float DriftAngularLerpSpeed = 2.0f;
        public const float DriftTurnRateBoost = 1.15f;
        public const float HighSpeedSteerReduction = 0.6f;
        public const float MinSteerFraction = 0.35f;
        public const float MinPowerFraction = 0.05f;
        public const float AngularRisePoint = 0.35f;
        public const float AngularTopSpeedReduction = 0.3f;
        public const float AngularGain = 3.0f;

        /// <summary>
        /// Which way a positive Rigidbody.angularVelocity.y actually turns
        /// the car's own nose is a UNITY-SPECIFIC CONVENTION QUESTION, not
        /// something to assume from Godot's own ANGULAR_Y_SIGN -- the two
        /// engines' rotation-direction conventions are not guaranteed to
        /// match, and car.gd's own comment on this exact constant already
        /// warns "verified empirically... not just derived." CarPhysicsPlayTests'
        /// own real-Rigidbody steering-direction check is what actually
        /// verifies this value for Unity; it is not derived on paper here.
        /// </summary>
        public const float AngularYSign = 1.0f;

        /// <summary>
        /// The handbrake (or an unrecovered slide) drastically weakening
        /// lateral grip -- ported from car.gd's own _integrate_forces
        /// "one-frame-stale _effective_grip is fine" comment: computed once
        /// up front from the PREVIOUS frame's effective grip, not the one
        /// _apply_grip is about to blend toward this frame.
        /// </summary>
        public static bool IsDrifting(bool handbrakeInput, float effectiveGrip, float gripRear, float forwardSpeed)
        {
            bool recovered = effectiveGrip >= gripRear * DriftGripRecoveryFraction;
            return handbrakeInput || (!recovered && Mathf.Abs(forwardSpeed) > DriftMinSpeed);
        }

        /// <summary>The wheel-steer angle this frame's input is asking for, in radians.</summary>
        public static float SteerTargetAngleRadians(float steerInput, float speedFraction, bool isDrifting, float turnRateDegrees)
        {
            float steerReduction = Mathf.Max(MinSteerFraction, 1.0f - speedFraction * HighSpeedSteerReduction);
            float effectiveTurnRateDegrees = turnRateDegrees * (isDrifting ? DriftTurnRateBoost : 1.0f);
            return steerInput * effectiveTurnRateDegrees * Mathf.Deg2Rad * steerReduction;
        }

        /// <summary>Eases the wheel toward its target at a fixed degrees/second rate -- car.gd's own move_toward.</summary>
        public static float MoveWheelAngleRadians(float currentRadians, float targetRadians, float turnSpeedDegreesPerSecond, float deltaTime)
        {
            return Mathf.MoveTowards(currentRadians, targetRadians, turnSpeedDegreesPerSecond * Mathf.Deg2Rad * deltaTime);
        }

        /// <summary>
        /// The force magnitude to apply along the car's own flattened
        /// forward vector -- positive drives forward, negative reverses or
        /// brakes. Zero (not applied at all) below a small input deadzone,
        /// matching car.gd's own absf(throttle_input) &lt; 0.01 guard.
        /// </summary>
        public static float EngineForceAlongForward(float throttleInput, float forwardSpeed,
            float maxSpeed, float reverseSpeed, float acceleration, float brakingForce)
        {
            if (Mathf.Abs(throttleInput) < 0.01f)
            {
                return 0.0f;
            }
            if (throttleInput > 0.0f)
            {
                if (forwardSpeed < -0.5f)
                {
                    return brakingForce;
                }
                float speedFraction = Mathf.Clamp01(forwardSpeed / maxSpeed);
                float powerCurve = Mathf.Max(MinPowerFraction, 1.0f - speedFraction);
                return throttleInput * acceleration * powerCurve;
            }

            if (forwardSpeed > 0.5f)
            {
                return -brakingForce;
            }
            float reverseFraction = Mathf.Clamp01(-forwardSpeed / reverseSpeed);
            float reversePowerCurve = Mathf.Max(MinPowerFraction, 1.0f - reverseFraction);
            return throttleInput * acceleration * reversePowerCurve;
        }

        /// <summary>Quadratic drag plus a flat rolling resistance, opposing whatever flat velocity the car already has.</summary>
        public static Vector3 DragForce(Vector3 velocityFlat, float dragCoefficient, float rollingResistance)
        {
            float speed = velocityFlat.magnitude;
            if (speed < 0.01f)
            {
                return Vector3.zero;
            }
            return -velocityFlat.normalized * (dragCoefficient * speed * speed + rollingResistance);
        }

        /// <summary>
        /// _effective_grip's own per-frame blend toward whatever grip the
        /// current input (handbrake or not) is asking for -- GRIP_BLEND_SPEED
        /// is what makes "recovery isn't instant" a real, feelable thing
        /// rather than an instant snap.
        /// </summary>
        public static float NextEffectiveGrip(float currentGrip, bool handbrakeInput,
            float gripFront, float gripRear, float handbrakeGripRear, float deltaTime)
        {
            float rearGrip = handbrakeInput ? handbrakeGripRear : gripRear;
            float targetGrip = Mathf.Lerp(gripFront, rearGrip, RearGripWeight);
            return Mathf.Lerp(currentGrip, targetGrip, Mathf.Clamp01(GripBlendSpeed * deltaTime));
        }

        /// <summary>
        /// Converts a 0..1 grip AMOUNT into a per-second lateral-velocity
        /// cancellation FRACTION -- see car.gd's own _apply_grip comment on
        /// why this has to scale with real time rather than wipe a fixed
        /// fraction off every physics tick regardless of how fast those
        /// tick.
        /// </summary>
        public static float LateralCancelFraction(float effectiveGrip, float deltaTime)
        {
            return Mathf.Clamp01(effectiveGrip * LateralCancelRate * deltaTime);
        }

        /// <summary>
        /// No rotation at zero speed, peaks around AngularRisePoint,
        /// slightly reduced again by top speed -- matches the spec's own
        /// "no rotation at 0 speed, max around mid-speed, slightly reduced
        /// at top speed."
        /// </summary>
        public static float AngularResponseCurve(float speedFraction)
        {
            if (speedFraction <= 0.001f)
            {
                return 0.0f;
            }
            float rise = Mathf.Clamp01(speedFraction / AngularRisePoint);
            float fall = 1.0f - Mathf.Clamp01((speedFraction - AngularRisePoint) / (1.0f - AngularRisePoint)) * AngularTopSpeedReduction;
            return rise * fall;
        }

        /// <summary>The angular_velocity.y this frame's wheel angle and speed are asking for.</summary>
        public static float TargetAngularY(float wheelAngleRadians, float responseCurve)
        {
            return wheelAngleRadians * AngularYSign * AngularGain * responseCurve;
        }
    }
}
