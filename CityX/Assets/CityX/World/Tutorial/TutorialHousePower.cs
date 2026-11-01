using System;

namespace CityX.World
{
    /// <summary>
    /// WHETHER OLD GUY'S HOUSE HAS ITS POWER BACK (docs/OLD_GUYS_HOUSE.md, docs/GAME_DESIGN.md: "the house is cleared, Old Guy has you turn
    /// its power back on, then you claim it"). The house is dark when the player arrives; throwing the fuse box's lever in the garage turns it
    /// on, for good. Kept apart from the house's objects, which the streamer rebuilds when it places the house, so the power survives a
    /// rebuild as the broken pieces do (TutorialHouseDamage). This machine's own: the tutorial is played in a private copy of the streets.
    /// </summary>
    public sealed class TutorialHousePower
    {
        public static TutorialHousePower Shared { get; } = new TutorialHousePower();

        public bool IsOn { get; private set; }

        /// <summary>Raised when the power comes on (true), or is forgotten for a new world (false).</summary>
        public event Action<bool> Changed;

        public void TurnOn()
        {
            if (IsOn)
            {
                return;
            }
            IsOn = true;
            Changed?.Invoke(true);
        }

        /// <summary>A new world, or a test: the house is dark again.</summary>
        public void Reset()
        {
            if (!IsOn)
            {
                return;
            }
            IsOn = false;
            Changed?.Invoke(false);
        }
    }
}
