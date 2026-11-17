using UnityEngine;

namespace CityX.UI
{
    /// <summary>One of the creator's colour pickers (the paint's, CreatorControls; the hair's, HairColourControls), as its colour code box reads and sets it.</summary>
    public interface IColourPicker
    {
        /// <summary>The colour the picker is on.</summary>
        Color Current { get; }

        /// <summary>Where on its square the colour is: saturation across and value (brightness) up, 0 to 1 (kept as picked, so black keeps its place).</summary>
        float Saturation { get; }

        float Value { get; }

        /// <summary>Picks <paramref name="colour"/>: the square, the hue and the preview move to it, and it is used.</summary>
        void Adopt(Color colour);
    }
}
