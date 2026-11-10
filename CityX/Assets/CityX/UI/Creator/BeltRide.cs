using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Where the body is on the conveyor belt a character is made on, and how fast the belt runs (docs/BELT_INTRO.md): rolled in from off
    /// the screen's left by the player's own keys, stopped in the middle to be made, then carried off to the right, past the edge of the
    /// screen and off the belt's end. Along the belt in metres, the middle of the screen 0 and its right positive. Pure: the scene only
    /// reads it (BeltIntro).
    /// </summary>
    public sealed class BeltRide
    {
        public enum Stage
        {
            /// <summary>The player runs the belt, either way; the body comes in from the left.</summary>
            RollingIn,

            /// <summary>The body in the middle, the belt stopped: the character is made.</summary>
            Making,

            /// <summary>The belt runs right by itself, the body on it, to the edge of the screen.</summary>
            RollingOut,

            /// <summary>Past the edge of the screen (the wider view from here): on to the belt's end.</summary>
            OnTheEdge,

            /// <summary>At the end of its ride, just short of the belt's end: the body is the ragdoll's, and the belt runs on at its full run.</summary>
            Fallen,
        }

        /// <summary>The belt's speed at full run, m/s, and how fast it gets there and stops, m/s².</summary>
        public const float Speed = 2.4f;

        public const float Acceleration = 6.0f;

        /// <summary>This near the middle the body is in it, metres.</summary>
        private const float Arrived = 0.004f;

        private readonly float _start;
        private readonly float _cut;
        private readonly float _end;

        /// <summary>
        /// The body waits at <paramref name="start"/> (off the screen's left, negative), the view changes when it passes
        /// <paramref name="cut"/> (off its right) and the belt ends at <paramref name="end"/>.
        /// </summary>
        public BeltRide(float start, float cut, float end)
        {
            _start = Mathf.Min(start, -0.5f);
            _cut = Mathf.Max(cut, 0.5f);
            _end = Mathf.Max(end, _cut + 0.5f);
            BodyAt = _start;
        }

        public Stage Now { get; private set; } = Stage.RollingIn;

        /// <summary>Where the body stands along the belt (while the belt still carries it).</summary>
        public float BodyAt { get; private set; }

        /// <summary>The belt's speed now, m/s, positive to the right.</summary>
        public float BeltSpeed { get; private set; }

        /// <summary>How hard the belt is running, either way: 0 stopped to 1 at its full run. What it is heard by.</summary>
        public float Running => Mathf.Abs(BeltSpeed) / Speed;

        /// <summary>How far the belt's surface has run in all, metres, positive to the right: what its slats and rollers are drawn by.</summary>
        public float Travel { get; private set; }

        /// <summary>
        /// One frame. <paramref name="push"/> is the player's keys, -1 (left) to 1 (right), and only counts while rolling in: holding
        /// right brings the body in and the belt slows to set it down exactly in the middle; left runs it back, never past where it
        /// waited.
        /// </summary>
        public void Step(float push, float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }
            float wanted;
            switch (Now)
            {
                case Stage.RollingIn:
                    wanted = Mathf.Clamp(push, -1.0f, 1.0f) * Speed;
                    // No faster than it can still stop from by the middle.
                    wanted = Mathf.Min(wanted, Mathf.Sqrt(2.0f * Acceleration * Mathf.Max(0.0f, -BodyAt)));
                    break;
                case Stage.Making:
                    wanted = 0.0f;
                    break;
                default:
                    wanted = Speed;
                    break;
            }
            BeltSpeed = Mathf.MoveTowards(BeltSpeed, wanted, Acceleration * deltaTime);
            float moved = BeltSpeed * deltaTime;

            if (Now == Stage.RollingIn && BodyAt + moved >= -Arrived)
            {
                // Set down in the middle: the belt stops with it.
                moved = -BodyAt;
                BeltSpeed = 0.0f;
                Now = Stage.Making;
            }
            Travel += moved;
            if (Now == Stage.Fallen)
            {
                return;
            }
            BodyAt = Mathf.Max(_start, BodyAt + moved);
            if (Now == Stage.RollingOut && BodyAt >= _cut)
            {
                Now = Stage.OnTheEdge;
            }
            if (Now == Stage.OnTheEdge && BodyAt >= _end)
            {
                Now = Stage.Fallen;
            }
        }

        /// <summary>The character is made: the belt carries it off. Only from the middle.</summary>
        public void Finish()
        {
            if (Now == Stage.Making)
            {
                Now = Stage.RollingOut;
            }
        }

        /// <summary>The body is let go here, short of the end, its belt running on: only on the last ride, past the edge of the screen.</summary>
        public void LetGo()
        {
            if (Now == Stage.OnTheEdge)
            {
                Now = Stage.Fallen;
            }
        }
    }
}
