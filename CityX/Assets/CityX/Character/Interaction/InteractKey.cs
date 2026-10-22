using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// E as Skyrim reads it (owner, 2026-10-21: "make it so holding E on objects carrys them while tapping E key equips it"): a TAP (let go
    /// within <see cref="HoldSeconds"/>) takes the item, a HOLD lifts it (GrabCarry). An item's pickup therefore waits for the key to come back
    /// up, where it used to act on the press, which would have taken every item the player meant to lift.
    ///
    /// The press is timed once per frame for everything that asks (every pickup and GrabCarry), off the keyboard.
    /// </summary>
    public static class InteractKey
    {
        /// <summary>How long E must be held before it is a hold, not a tap.</summary>
        public const float HoldSeconds = 0.3f;

        private static readonly InteractPress Press = new InteractPress();
        private static int _readFrame = -1;

        /// <summary>E was let go this frame after a press shorter than <see cref="HoldSeconds"/>.</summary>
        public static bool Tapped
        {
            get
            {
                Read();
                return Press.Tapped;
            }
        }

        /// <summary>E has just been held for <see cref="HoldSeconds"/> (true on that one frame).</summary>
        public static bool HoldBegan
        {
            get
            {
                Read();
                return Press.HoldBegan;
            }
        }

        /// <summary>E is down.</summary>
        public static bool IsDown
        {
            get
            {
                Read();
                return Press.IsDown;
            }
        }

        private static void Read()
        {
            if (_readFrame == Time.frameCount)
            {
                return;
            }
            if (_readFrame != Time.frameCount - 1)
            {
                Press.Reset();   // unread for a while (nothing near to use E on): a press then is a new one
            }
            _readFrame = Time.frameCount;
            Keyboard keyboard = Keyboard.current;
            Press.Step(keyboard != null && keyboard.eKey.isPressed, Time.deltaTime);
        }
    }

    /// <summary>The tap/hold split of one key, stepped with whether it is down and the time since the last step. Plain, so a test can drive it.</summary>
    public sealed class InteractPress
    {
        private float _heldFor;

        public bool IsDown { get; private set; }

        /// <summary>The key came up this step, held for less than <see cref="InteractKey.HoldSeconds"/>.</summary>
        public bool Tapped { get; private set; }

        /// <summary>The key reached <see cref="InteractKey.HoldSeconds"/> held this step.</summary>
        public bool HoldBegan { get; private set; }

        /// <summary>Forgets the press under way: the key counts as up.</summary>
        public void Reset()
        {
            IsDown = false;
            Tapped = false;
            HoldBegan = false;
            _heldFor = 0.0f;
        }

        public void Step(bool down, float deltaTime)
        {
            Tapped = false;
            HoldBegan = false;
            if (down)
            {
                float before = IsDown ? _heldFor : 0.0f;
                _heldFor = IsDown ? _heldFor + deltaTime : 0.0f;
                HoldBegan = IsDown && before < InteractKey.HoldSeconds && _heldFor >= InteractKey.HoldSeconds;
            }
            else if (IsDown)
            {
                Tapped = _heldFor < InteractKey.HoldSeconds;
                _heldFor = 0.0f;
            }
            IsDown = down;
        }
    }
}
