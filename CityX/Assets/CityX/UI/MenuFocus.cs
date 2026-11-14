using CityX.Character;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>The mouse pointer, freed for a page that takes over the screen and captured again afterwards if it was captured before.</summary>
    public sealed class FreedCursor
    {
        private bool _wasCaptured;

        public void Free()
        {
            _wasCaptured = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Restore()
        {
            if (_wasCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    /// <summary>
    /// What a page that takes over the screen does to the game while it is up: the pointer is freed, the player's input is held and the
    /// orbit camera rests; closing gives each back.
    /// </summary>
    public sealed class MenuFocus
    {
        private readonly FreedCursor _cursor = new FreedCursor();

        public void Take(Object holder, PlayerInputReader input, ThirdPersonCamera orbit)
        {
            _cursor.Free();
            if (input != null) input.Hold(holder);
            if (orbit != null) orbit.enabled = false;
        }

        public void GiveBack(Object holder, PlayerInputReader input, ThirdPersonCamera orbit)
        {
            if (input != null) input.Release(holder);
            if (orbit != null) orbit.enabled = true;
            _cursor.Restore();
        }
    }
}
