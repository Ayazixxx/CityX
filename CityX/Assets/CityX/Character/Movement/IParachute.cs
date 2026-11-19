using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs from a worn parachute (docs/PARACHUTE.md), without depending on the Weapons assembly: the same layering as
    /// IFlightGear (Parachute, in CityX.Weapons, implements it).
    /// </summary>
    public interface IParachute
    {
        /// <summary>
        /// True while the parachute moves the body (falling spread out, opening, flying under the canopy): the keys and Space are its, not the
        /// walk's or the jump's.
        /// </summary>
        bool DrivesBody { get; }

        /// <summary>
        /// Every frame it is worn and the body is the player's to move, before the body is moved: <paramref name="spacePressed"/> is Space going
        /// down this frame, <paramref name="move"/> the keys (x A/D, y W/S). A frame it is not called (the body on a wall, on a rope, knocked down)
        /// ends whatever jump it was in.
        /// </summary>
        void Tick(bool spacePressed, Vector2 move, float deltaTime);
    }
}
