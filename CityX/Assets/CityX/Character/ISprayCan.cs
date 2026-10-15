using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A spray can in the player's hand (Weapons' SprayCanItem): right-click at a wall with one opens the graffiti canvas (the UI answers
    /// PlayerInputReader.SprayRequested). The can remembers the colour and the brush size last painted with.
    /// </summary>
    public interface ISprayCan
    {
        Color Colour { get; set; }

        int Brush { get; set; }
    }
}
