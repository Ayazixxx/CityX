using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A MENU HAS THE KEYS (chat, the inventory, the map, the console, an item's card...): it holds the reader instead of switching it off.
    /// Switched off, the reader stopped ticking the body too (it is the only thing that does), so a menu opened in mid-air hung the body
    /// there until it closed; two menus open at once did not know of each other, and closing one gave the keys back under the other; and
    /// a body whose reader was off counted as nobody's, so enemies stopped coming for a player reading their inventory.
    /// Held, the body goes on falling, landing and getting its breath back with no keys read, and every holder must let go before the
    /// keys are the player's again.
    /// </summary>
    public sealed partial class PlayerInputReader
    {
        private readonly HashSet<Object> _holders = new HashSet<Object>();

        /// <summary>How many bodies some menu holds the keys of.</summary>
        private static int _held;

        /// <summary>A menu has the keys of some body here: an Escape now belongs to it, not to the pause menu.</summary>
        public static bool AnyHeld => _held > 0;

        /// <summary>
        /// The frame the last menu gave some body its keys back. The Escape that closed it is not also a pause: the page and the pause menu
        /// read the same press in the same frame, in either order (PauseMenuView).
        /// </summary>
        public static int KeysGivenBackOnFrame { get; private set; } = -1;

        /// <summary>Some menu has the keys of this body (one destroyed while open no longer counts).</summary>
        public bool IsHeld
        {
            get
            {
                if (_holders.Count > 0 && _holders.RemoveWhere(holder => holder == null) > 0 && _holders.Count == 0)
                {
                    _held--;
                    KeysGivenBackOnFrame = Time.frameCount;
                }
                return _holders.Count > 0;
            }
        }

        /// <summary>The keys move this body: it is driven from this machine and no menu has them.</summary>
        public bool TakesKeys => enabled && !IsHeld;

        /// <summary><paramref name="holder"/> (a menu, opening) has the keys until it lets go; the body goes on with none.</summary>
        public void Hold(Object holder)
        {
            if (holder != null && _holders.Add(holder) && _holders.Count == 1)
            {
                _held++;
            }
        }

        /// <summary><paramref name="holder"/> (closing) gives the keys back; they are the player's once nothing else holds them.</summary>
        public void Release(Object holder)
        {
            if (holder != null && _holders.Remove(holder) && _holders.Count == 0)
            {
                _held--;
                KeysGivenBackOnFrame = Time.frameCount;
            }
        }

        /// <summary>A body gone with a menu still holding it holds nothing any more.</summary>
        private void LetGoOfEveryHolder()
        {
            if (_holders.Count > 0)
            {
                _holders.Clear();
                _held--;
            }
        }

        /// <summary>
        /// The frame of a held body: no key is read, what a held click was doing is let go of, and the body goes on with no movement asked
        /// of it (it falls and lands). On a wall or on a rope it stays as it is until the menu closes: ticked with no keys there, it would
        /// let go.
        /// </summary>
        private void TickHeld(float deltaTime)
        {
            _audio?.SilenceFootsteps();
            LetGoOfTheTrigger(deltaTime);
            if ((_climbing != null && _climbing.IsClimbing) || (_grappleSwing != null && _grappleSwing.IsActive))
            {
                return;
            }
            _locomotion.Tick(new LocomotionInput { YawDegrees = transform.eulerAngles.y }, deltaTime);
        }
    }
}
