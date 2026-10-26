using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How a Wraith holds itself as it floats (owner, 2026-10-26: "try to animate his movement the best you can"): the pure maths of its pose
    /// from the moment, its speed and turn, where it looks and whether it was just hit. No clips and no Animator: it has no feet to plant and
    /// nothing to do but hang in the air, so every bone is a few slow sines laid over the body's own rest (arms hanging), the way its wisps
    /// are sines in the shader. The body (Wraith) applies the result to its bones after the rest pose, each as a turn about the body's own
    /// axes: pitch about its right, yaw about its up, roll about its forward.
    ///
    ///  * It LEANS into its drift (the spine and the hips), and banks a little into a turn.
    ///  * Its ARMS hang and swing slowly like pendulums, the two out of step, each joint lagging the one above it; drifting, they trail back.
    ///  * Its HEAD drifts idly, and turns to look at a player near it (the start of its sneaking up, which comes later).
    ///  * HIT, it jerks back and its arms fly up, settling over a second.
    /// </summary>
    public static class WraithPose
    {
        /// <summary>How far the spine pitches forward at cruising speed, degrees.</summary>
        public const float LeanDegrees = 12.0f;

        /// <summary>How far it banks into a turn at <see cref="WraithDrift.TurnDegreesPerSecond"/>, degrees.</summary>
        public const float BankDegrees = 6.0f;

        /// <summary>The arms' swing, degrees either side of hanging, and the swing's period.</summary>
        public const float ArmSwingDegrees = 7.0f;
        public const float ArmSwingSeconds = 3.1f;

        /// <summary>How far the arms hang out from the body, degrees, and how far they trail back at cruising speed.</summary>
        public const float ArmSpreadDegrees = 5.0f;
        public const float ArmTrailDegrees = 10.0f;

        /// <summary>Each joint down the arm lags the one above by this share of the swing's cycle, and swings this much more.</summary>
        public const float JointLag = 0.18f;
        public const float ForearmShare = 0.7f;
        public const float HandShare = 0.9f;

        /// <summary>The head's idle drift: this far either side (yaw) and up and down (pitch), degrees, and its period.</summary>
        public const float HeadIdleYawDegrees = 9.0f;
        public const float HeadIdlePitchDegrees = 4.0f;
        public const float HeadIdleSeconds = 7.3f;

        /// <summary>How far the head turns to look at someone, degrees either side and up or down.</summary>
        public const float LookYawLimit = 70.0f;
        public const float LookPitchLimit = 35.0f;

        /// <summary>The hips' slow side-to-side sway, metres, and its period.</summary>
        public const float HipSwayMetres = 0.04f;
        public const float HipSwaySeconds = 4.7f;

        /// <summary>Hit: the spine jerks back this far and the arms fly up this far, times how hurt it still is.</summary>
        public const float FlinchDegrees = 15.0f;
        public const float FlinchArmDegrees = 25.0f;

        /// <summary>What the pose is made from.</summary>
        public readonly struct Input
        {
            public readonly float Time;

            /// <summary>Drift speed, metres per second.</summary>
            public readonly float Speed;

            /// <summary>How fast it is turning, degrees per second, left negative.</summary>
            public readonly float TurnRate;

            /// <summary>Whether something is looked at, and where it is in the body's own frame (from the head's centre).</summary>
            public readonly bool HasLookTarget;
            public readonly Vector3 LookTarget;

            /// <summary>How hurt it still is: 1 the moment it is hit, settling to 0.</summary>
            public readonly float Hurt;

            public Input(float time, float speed, float turnRate, bool hasLookTarget, Vector3 lookTarget, float hurt)
            {
                Time = time;
                Speed = speed;
                TurnRate = turnRate;
                HasLookTarget = hasLookTarget;
                LookTarget = lookTarget;
                Hurt = hurt;
            }
        }

        /// <summary>A bone's turn over its rest: pitch (about the body's right), yaw (about its up), roll (about its forward), degrees.</summary>
        public readonly struct Turn
        {
            public readonly float Pitch;
            public readonly float Yaw;
            public readonly float Roll;

            public Turn(float pitch, float yaw, float roll)
            {
                Pitch = pitch;
                Yaw = yaw;
                Roll = roll;
            }

            public static Turn operator +(Turn a, Turn b) => new Turn(a.Pitch + b.Pitch, a.Yaw + b.Yaw, a.Roll + b.Roll);
        }

        public readonly struct Result
        {
            /// <summary>The hips' sideways shift, metres, in the body's frame.</summary>
            public readonly Vector3 HipShift;

            public readonly Turn Hips;
            public readonly Turn Spine;
            public readonly Turn Head;
            public readonly Turn ArmLeft;
            public readonly Turn ArmRight;
            public readonly Turn ForearmLeft;
            public readonly Turn ForearmRight;
            public readonly Turn HandLeft;
            public readonly Turn HandRight;

            public Result(Vector3 hipShift, Turn hips, Turn spine, Turn head, Turn armLeft, Turn armRight, Turn forearmLeft,
                Turn forearmRight, Turn handLeft, Turn handRight)
            {
                HipShift = hipShift;
                Hips = hips;
                Spine = spine;
                Head = head;
                ArmLeft = armLeft;
                ArmRight = armRight;
                ForearmLeft = forearmLeft;
                ForearmRight = forearmRight;
                HandLeft = handLeft;
                HandRight = handRight;
            }
        }

        public static Result Compute(Input input)
        {
            float drift = Mathf.Clamp01(input.Speed / WraithDrift.CruiseSpeed);
            float bank = Mathf.Clamp(input.TurnRate / WraithDrift.TurnDegreesPerSecond, -1.0f, 1.0f) * BankDegrees;
            float hurt = Mathf.Clamp01(input.Hurt);

            // The trunk: leaning into the drift, banking into the turn, jerked back when hit.
            float lean = drift * LeanDegrees - hurt * FlinchDegrees;
            var hips = new Turn(lean * 0.4f, 0.0f, -bank * 0.5f);
            var spine = new Turn(lean * 0.6f, 0.0f, -bank * 0.5f);
            Vector3 hipShift = Vector3.right * (HipSwayMetres * Mathf.Sin(input.Time * (2.0f * Mathf.PI / HipSwaySeconds)));

            // The arms: pendulums out of step, trailing back as it drifts, flung up by a hit.
            float phase = input.Time * (2.0f * Mathf.PI / ArmSwingSeconds);
            float trail = -drift * ArmTrailDegrees - hurt * FlinchArmDegrees;
            Turn armLeft = Arm(phase, left: true, trail);
            Turn armRight = Arm(phase + Mathf.PI, left: false, trail);
            Turn forearmLeft = Lower(phase, JointLag, ForearmShare);
            Turn forearmRight = Lower(phase + Mathf.PI, JointLag, ForearmShare);
            Turn handLeft = Lower(phase, JointLag * 2.0f, HandShare);
            Turn handRight = Lower(phase + Mathf.PI, JointLag * 2.0f, HandShare);

            // The head: an idle wander, or a look at whoever is near.
            float idle = input.Time * (2.0f * Mathf.PI / HeadIdleSeconds);
            var head = new Turn(HeadIdlePitchDegrees * Mathf.Sin(idle * 1.3f), HeadIdleYawDegrees * Mathf.Sin(idle), 0.0f);
            if (input.HasLookTarget && input.LookTarget.sqrMagnitude > 1e-6f)
            {
                Vector3 to = input.LookTarget.normalized;
                float yaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -LookYawLimit, LookYawLimit);
                float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(to.y, -1.0f, 1.0f)) * Mathf.Rad2Deg, -LookPitchLimit, LookPitchLimit);
                head = new Turn(pitch - lean, yaw, 0.0f);   // the look counters the trunk's lean, so it meets the eye
            }

            return new Result(hipShift, hips, spine, head, armLeft, armRight, forearmLeft, forearmRight, handLeft, handRight);
        }

        /// <summary>An upper arm: swinging forward and back, held a little out from the side, trailing.</summary>
        private static Turn Arm(float phase, bool left, float trail)
        {
            float swing = ArmSwingDegrees * Mathf.Sin(phase);
            float spread = left ? ArmSpreadDegrees : -ArmSpreadDegrees;
            return new Turn(swing + trail, 0.0f, spread);
        }

        /// <summary>A joint further down the arm: the same swing, later and larger, so the arm bends like something hanging in water.</summary>
        private static Turn Lower(float phase, float lag, float share)
        {
            return new Turn(ArmSwingDegrees * share * Mathf.Sin(phase - lag * 2.0f * Mathf.PI), 0.0f, 0.0f);
        }
    }
}
