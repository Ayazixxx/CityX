using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// CROUCHING THE WAY SOURCE DOES IT (owner, 2026-10-27: "I kind of want the crouching to work like it does in source games with the crouch
    /// jump as well"; docs/CROUCH.md). Valve's own movement code (Source SDK 2013, gamemovement.cpp, Duck / FinishDuck / FinishUnDuck /
    /// CanUnduck / HandleDuckingSpeedCrop):
    ///
    ///  - On the ground a duck takes <see cref="DuckSeconds"/> (TIME_TO_DUCK, 0.4 s), eased (SimpleSpline); the eyes come down through it and
    ///    the hull shrinks when it is done. Standing back up takes <see cref="UnduckSeconds"/> (TIME_TO_UNDUCK, 0.2 s), and only when the
    ///    standing hull fits (CanUnduck): under a low ceiling the body stays down.
    ///  - IN THE AIR the duck is instant and the FEET come up, not the head down: the origin is lifted by the difference between the two hulls
    ///    (FinishDuck: origin += hullSizeNormal - hullSizeCrouch). That is the crouch jump -- jump, duck at the top, and the feet clear a ledge
    ///    the standing body could not. Letting go in the air drops the feet back down again, instantly, when there is room below them.
    ///  - Ducked on the ground, the body moves at a third of its speed (HandleDuckingSpeedCrop: frac = 0.33333333).
    ///
    /// A plain class (the maths is tested without a scene); PlayerLocomotion.Crouch.cs applies it to the body's controller.
    /// </summary>
    public sealed class CrouchHull
    {
        /// <summary>Source's TIME_TO_DUCK: going down on the ground, seconds.</summary>
        public const float DuckSeconds = 0.4f;

        /// <summary>Source's TIME_TO_UNDUCK: standing back up, seconds.</summary>
        public const float UnduckSeconds = 0.2f;

        /// <summary>Source's duck speed crop: ducked on the ground, a third of the speed.</summary>
        public const float SpeedShare = 1.0f / 3.0f;

        /// <summary>How far down the duck has gone, 0 standing .. 1 down, linear in time (the eased value is <see cref="Shown"/>).</summary>
        public float Amount { get; private set; }

        /// <summary>True while the hull is the crouched one (Source's FL_DUCKING).</summary>
        public bool Ducked { get; private set; }

        /// <summary>The duck eased in and out (Source's SimpleSpline: 3t^2 - 2t^3), what the pose and the eyes follow.</summary>
        public float Shown => Spline(Amount);

        /// <summary>What one step decided the body must do.</summary>
        public enum Shift
        {
            None,

            /// <summary>Ducked in the air: lift the body by the hull difference (the feet come up, the head stays).</summary>
            TuckFeet,

            /// <summary>Let go in the air: lower the body by the hull difference (the feet go back down).</summary>
            DropFeet,
        }

        /// <summary>
        /// One step. <paramref name="held"/>: the crouch is wanted; <paramref name="grounded"/>: on the ground; <paramref name="roomToStand"/>:
        /// the standing hull fits where the body stands (on the ground: up from the feet; in the air: down from the head).
        /// </summary>
        public Shift Step(bool held, bool grounded, bool roomToStand, float deltaTime)
        {
            if (held)
            {
                if (Ducked)
                {
                    Amount = 1.0f;
                    return Shift.None;
                }
                if (!grounded)
                {
                    Amount = 1.0f;
                    Ducked = true;
                    return Shift.TuckFeet;
                }
                Amount = Mathf.Min(1.0f, Amount + deltaTime / DuckSeconds);
                if (Amount >= 1.0f)
                {
                    Ducked = true;
                }
                return Shift.None;
            }

            if (Ducked)
            {
                if (!roomToStand)
                {
                    Amount = 1.0f;   // under something: stays down until there is room
                    return Shift.None;
                }
                Ducked = false;
                if (!grounded)
                {
                    Amount = 0.0f;
                    return Shift.DropFeet;
                }
            }
            Amount = Mathf.Max(0.0f, Amount - deltaTime / UnduckSeconds);
            return Shift.None;
        }

        /// <summary>The speed a body moves at, as a share of its own: a third once ducked on the ground, eased in with the duck.</summary>
        public float SpeedFactor(bool grounded) => grounded ? Mathf.Lerp(1.0f, SpeedShare, Shown) : 1.0f;

        /// <summary>Back to standing (a respawn, a ragdoll, a teleport).</summary>
        public void Reset()
        {
            Amount = 0.0f;
            Ducked = false;
        }

        public static float Spline(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3.0f - 2.0f * t);
        }
    }
}
