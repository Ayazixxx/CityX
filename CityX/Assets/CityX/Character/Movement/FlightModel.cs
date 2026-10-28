using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How flight gear moves the body (docs/FLIGHT.md; owner, 2026-10-28: winged flying like the Lego games', and "the jetpack movement could
    /// use some work as well ... lets try to improve both"). The same rules for the jetpack and the wings, each with its own numbers
    /// (<see cref="FlightTuning"/>):
    /// <list type="bullet">
    /// <item>Space in the air takes off; from then on the body is FLYING until it touches the ground (or goes limp).</item>
    /// <item>Flying with nothing held is a HOVER: the body holds its height. Space climbs, Ctrl sinks, Shift is a faster climb when nothing
    /// else is held.</item>
    /// <item>WASD flies horizontally with inertia (<see cref="FlightTuning.Acceleration"/>), Shift faster. Flying FORWARD follows the camera up
    /// and down (look down to dive, up to climb: the jetpack's own rule, kept), on top of whatever Space and Ctrl add.</item>
    /// </list>
    /// Pure: the gear feeds it a frame's input and reads the velocities back.
    /// </summary>
    public sealed class FlightModel
    {
        private readonly FlightTuning _tuning;
        private bool _wasClimbing;

        public FlightModel(FlightTuning tuning)
        {
            _tuning = tuning;
        }

        public FlightTuning Tuning => _tuning;

        /// <summary>Off the ground under power.</summary>
        public bool Flying { get; private set; }

        /// <summary>Space held while flying.</summary>
        public bool Climbing { get; private set; }

        /// <summary>Ctrl held while flying.</summary>
        public bool Descending { get; private set; }

        /// <summary>Shift held while flying.</summary>
        public bool Boosting { get; private set; }

        /// <summary>WASD held while flying, with a forward part.</summary>
        public bool FlyingForward { get; private set; }

        /// <summary>The vertical velocity the body should have this frame, metres a second (up positive). Only meaningful while flying.</summary>
        public float VerticalVelocity { get; private set; }

        /// <summary>The horizontal speed the body should fly at, metres a second: 0 when not flying.</summary>
        public float HorizontalSpeed { get; private set; }

        /// <summary>This frame's tick began a flight.</summary>
        public bool TookOff { get; private set; }

        /// <summary>This frame's tick ended one, on the ground.</summary>
        public bool Landed { get; private set; }

        /// <summary>This frame's tick started a climb (Space went down while flying, or a flight began with it held).</summary>
        public bool ClimbBegan { get; private set; }

        /// <summary>One frame: decides whether the body flies, and how fast in each direction.</summary>
        public void Tick(in FlightInput input, float speedScale = 1.0f)
        {
            TookOff = false;
            Landed = false;
            if (!Flying && input.Airborne && input.ThrustHeld)
            {
                Flying = true;
                TookOff = true;
            }
            else if (Flying && !input.Airborne)
            {
                Flying = false;
                Landed = true;
            }
            if (!Flying)
            {
                Climbing = false;
                Descending = false;
                Boosting = false;
                FlyingForward = false;
                VerticalVelocity = 0.0f;
                HorizontalSpeed = 0.0f;
                ClimbBegan = false;
                _wasClimbing = false;
                return;
            }

            Climbing = input.ThrustHeld;
            Descending = input.DescendHeld;
            Boosting = input.BoostHeld;
            bool moving = input.Move.sqrMagnitude > 0.0001f;
            FlyingForward = input.Move.y > 0.0f;
            ClimbBegan = Climbing && !_wasClimbing;
            _wasClimbing = Climbing;

            float scale = Mathf.Max(0.0f, speedScale);
            HorizontalSpeed = (Boosting ? _tuning.Fast : _tuning.Cruise) * scale;
            VerticalVelocity = VerticalFor(input, HorizontalSpeed, scale);
            if (!moving)
            {
                FlyingForward = false;
            }
        }

        /// <summary>
        /// The vertical velocity for a frame of flight: a hover holds its height; Space climbs (Shift a faster straight climb when nothing
        /// else is held); Ctrl sinks; and flying forward follows the camera's pitch, up or down, at the horizontal speed's rate.
        /// </summary>
        public float VerticalFor(in FlightInput input, float horizontalSpeed, float scale)
        {
            float vertical = 0.0f;
            bool moving = input.Move.sqrMagnitude > 0.0001f;
            if (input.ThrustHeld)
            {
                vertical += (input.BoostHeld && !moving ? _tuning.BoostClimb : _tuning.Climb) * scale;
            }
            if (input.DescendHeld)
            {
                vertical -= _tuning.Descend * scale;
            }
            if (input.Move.y > 0.0f)
            {
                // ThirdPersonCamera's pitch is positive looking DOWN, so looking down must sink the body.
                vertical += -Mathf.Sin(input.CameraPitchDegrees * Mathf.Deg2Rad) * horizontalSpeed * Mathf.Clamp01(input.Move.y);
            }
            return vertical;
        }

        /// <summary>Ends the flight at once (the body went limp, the gear came off).</summary>
        public void Stop()
        {
            Flying = false;
            Climbing = false;
            Descending = false;
            Boosting = false;
            FlyingForward = false;
            VerticalVelocity = 0.0f;
            HorizontalSpeed = 0.0f;
            _wasClimbing = false;
        }
    }

    /// <summary>A flight gear's numbers, metres a second (and a second each second for the inertia).</summary>
    public readonly struct FlightTuning
    {
        /// <summary>Space: how fast the body climbs; Shift with nothing else held, <see cref="BoostClimb"/>.</summary>
        public readonly float Climb;

        public readonly float BoostClimb;

        /// <summary>Ctrl: how fast it sinks.</summary>
        public readonly float Descend;

        /// <summary>WASD: the horizontal speed, and with Shift <see cref="Fast"/>.</summary>
        public readonly float Cruise;

        public readonly float Fast;

        /// <summary>How quickly the horizontal speed is reached and shed: the lower, the more the body carries.</summary>
        public readonly float Acceleration;

        public FlightTuning(float climb, float boostClimb, float descend, float cruise, float fast, float acceleration)
        {
            Climb = climb;
            BoostClimb = boostClimb;
            Descend = descend;
            Cruise = cruise;
            Fast = fast;
            Acceleration = acceleration;
        }
    }
}
