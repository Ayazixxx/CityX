using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs from something on the back that flies the body (a jetpack, a pair of wings), without depending on the
    /// Weapons assembly: Character depends on nothing, Weapons depends on Character, never the other way round. FlightGear (in CityX.Weapons)
    /// implements it for both. docs/FLIGHT.md.
    /// </summary>
    public interface IFlightGear
    {
        /// <summary>In the air under power: Space took off, and the body has not landed since.</summary>
        bool IsFlying { get; }

        /// <summary>The horizontal speed the gear is flying the body at this frame, metres a second: 0 when not flying.</summary>
        float HorizontalSpeed { get; }

        /// <summary>How quickly the body reaches that speed (and sheds it), metres a second each second: the flight's inertia.</summary>
        float HorizontalAcceleration { get; }

        /// <summary>
        /// Called every frame while equipped, grounded or not (on the ground the gear only eases its lean back to upright), BEFORE
        /// PlayerLocomotion.Tick: a flying gear sets the body's vertical velocity for the frame, which that Tick then integrates.
        /// </summary>
        void Tick(in FlightInput input, float deltaTime);

        /// <summary>Stops the flame, the wings' beat, the audio and the flight feel at once: the body went limp (a crash, a ragdoll).</summary>
        void StopThrustEffects();

        /// <summary>Snaps the body's own flight lean back to upright at once, for the moment a ragdoll takes over.</summary>
        void ResetTilt();
    }

    /// <summary>One frame of what the player asks of their flight gear, read by PlayerInputReader.</summary>
    public struct FlightInput
    {
        /// <summary>Space: in the air it takes off, and flying it climbs.</summary>
        public bool ThrustHeld;

        /// <summary>Ctrl: flying, it sinks (held to the ground, it lands).</summary>
        public bool DescendHeld;

        /// <summary>Shift: faster, and a faster straight climb when nothing else is held.</summary>
        public bool BoostHeld;

        /// <summary>The raw WASD reading (x strafe, y forward), before the camera's yaw is folded in: the body leans toward it.</summary>
        public Vector2 Move;

        /// <summary>ThirdPersonCamera.Pitch, degrees: positive looks down. Flying forward follows it up and down.</summary>
        public float CameraPitchDegrees;

        /// <summary>Off the ground (PlayerLocomotion.IsGrounded is false): only then can a flight begin, and touching down ends one.</summary>
        public bool Airborne;
    }
}
