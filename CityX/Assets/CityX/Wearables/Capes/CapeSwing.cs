using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// How the cape's hanging part swings (owner, 2026-11-25: "work to get cape physics that are easier on performance. it really doesnt need to
    /// be anything crazy world of warcraft cape physics are like nothing it just fits over the clothing and looks good. maybe the back flies up a
    /// bit when you jump or something. something that will not tank performance in large crowds of people remember there is a hub"): no cloth,
    /// two springs. <see cref="Lift"/> swings it back and up from the shoulders, more the faster the wearer goes forward and the faster they rise
    /// or fall (a jump, a drop); <see cref="Sway"/> swings it to one side as they turn or step sideways, with a light breeze. A few sums a frame
    /// whatever the crowd: the cape's mesh is bent by the graphics card (Cape, CapeSkin).
    /// </summary>
    public sealed class CapeSwing
    {
        /// <summary>Degrees of lift for each metre a second forward (a walk about 16, a sprint about 30).</summary>
        public const float LiftPerSpeed = 4.0f;

        /// <summary>Degrees of lift for each metre a second up or down (a jump about 27; a long drop flies it right up).</summary>
        public const float LiftPerRise = 6.0f;

        public const float MostLift = 70.0f;

        /// <summary>The furthest it swings in towards the body (a little overshoot settling, never into the legs).</summary>
        public const float LeastLift = -3.0f;

        /// <summary>Degrees of sway for each degree a second of turning, and for each metre a second sideways; the most either way.</summary>
        public const float SwayPerTurn = 0.06f;
        public const float SwayPerSideSpeed = 5.0f;
        public const float MostSway = 20.0f;

        /// <summary>The breeze: degrees of sway it gives at most, and how fast it comes and goes (radians a second).</summary>
        public const float BreezeDegrees = 2.5f;
        public const float BreezeRate = 1.3f;

        /// <summary>How the springs swing: their own rate (swings a second) and how much they are held back (1: no overshoot).</summary>
        public const float Frequency = 1.6f;
        public const float DampingRatio = 0.45f;

        /// <summary>The longest frame worked out at once (a long one does not throw the cape).</summary>
        public const float LongestFrame = 0.05f;

        public float Lift { get; private set; }
        public float Sway { get; private set; }

        private float _liftRate;
        private float _swayRate;

        /// <summary>
        /// One frame: the wearer's movement in their own frame (<paramref name="velocity"/>: +x right, +y up, +z forward, metres a second), how
        /// fast they turn (degrees a second, + to the right), and the time (for the breeze).
        /// </summary>
        public void Update(Vector3 velocity, float turnDegreesPerSecond, float time, float dt)
        {
            dt = Mathf.Min(dt, LongestFrame);
            if (dt <= 0.0f)
            {
                return;
            }
            float liftTarget = Mathf.Clamp(Mathf.Max(0.0f, velocity.z) * LiftPerSpeed + Mathf.Abs(velocity.y) * LiftPerRise, 0.0f, MostLift);
            // It trails the way the wearer goes: to the left as they turn or step right.
            float swayTarget = Mathf.Clamp(-(turnDegreesPerSecond * SwayPerTurn + velocity.x * SwayPerSideSpeed), -MostSway, MostSway)
                               + BreezeDegrees * Mathf.Sin(time * BreezeRate);
            float lift = Lift, sway = Sway;
            Spring(ref lift, ref _liftRate, liftTarget, dt);
            Spring(ref sway, ref _swayRate, swayTarget, dt);
            if (lift < LeastLift)
            {
                lift = LeastLift;
                _liftRate = Mathf.Max(0.0f, _liftRate);
            }
            Lift = lift;
            Sway = sway;
        }

        /// <summary>Hanging straight and still (a body put somewhere new).</summary>
        public void Reset()
        {
            Lift = 0.0f;
            Sway = 0.0f;
            _liftRate = 0.0f;
            _swayRate = 0.0f;
        }

        /// <summary>A damped spring, one step (semi-implicit, steady at any frame up to <see cref="LongestFrame"/>).</summary>
        private static void Spring(ref float value, ref float rate, float target, float dt)
        {
            float w = 2.0f * Mathf.PI * Frequency;
            rate += (w * w * (target - value) - 2.0f * DampingRatio * w * rate) * dt;
            value += rate * dt;
        }
    }
}
