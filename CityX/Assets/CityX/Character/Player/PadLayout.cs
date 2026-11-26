using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT A CONTROLLER'S BUTTONS MEAN (docs/CONTROLLER.md), where one button does more than one thing. Pure: stepped once a frame with which
    /// buttons are down and the time since the last step, so a test drives it without a device.
    /// <list type="bullet">
    /// <item>A shoulder held (LB / RB) turns the face buttons into the hotbar: LB with A, B, X, Y is slots 1 to 4, RB slots 5 to 8, both
    /// shoulders with A and B slots 9 and 0. A face button pressed then is that slot's until it comes up, whatever the shoulders do.</item>
    /// <item>Otherwise A jumps; B tapped dashes (on the let-go) and held crouches; X is interact (InteractKey times its tap and hold, as E's)
    /// and held, reloads; Y swaps weapons.</item>
    /// <item>L3 (the left stick pressed) sprints, and the sprint stays on until the stick comes back to the middle.</item>
    /// </list>
    /// </summary>
    public sealed class PadLayout
    {
        /// <summary>Hotbar slots (Hotbar.SlotCount: 1 to 9 and 0).</summary>
        public const int Slots = 10;

        /// <summary>A stick nearer the middle than this is at rest (worn sticks drift), and what is past it is spread over the rest.</summary>
        public const float DeadZone = 0.2f;

        /// <summary>A stick this far over counts as the key that way held (W, A, S, D for climbing, ladders, a menu).</summary>
        public const float StickAsKey = 0.5f;

        /// <summary>A trigger pulled this far is down.</summary>
        public const float TriggerDown = 0.4f;

        /// <summary>B held this long is a crouch, not a dash, seconds.</summary>
        public const float CrouchAfterSeconds = 0.25f;

        /// <summary>How much of a mouse wheel's notch Y's weapon swap is (the wheel turns 120 a notch on Windows).</summary>
        public const float SwapNotch = 120.0f;

        public const int South = 0, East = 1, West = 2, North = 3;

        public struct Buttons
        {
            public bool South, East, West, North;
            public bool LeftShoulder, RightShoulder;
            public bool LeftStickPress;

            /// <summary>The left stick is off its rest.</summary>
            public bool Moving;

            public bool Face(int face) => face == PadLayout.South ? South : face == PadLayout.East ? East : face == PadLayout.West ? West : North;
        }

        /// <summary>What the buttons meant this step.</summary>
        public struct Frame
        {
            public ControlState Jump, Sprint, Dash, Crouch, Interact, Reload;
            public bool SwapWeapon;

            /// <summary>The hotbar slot whose button went down, is down, came up this step: -1 for none.</summary>
            public int SlotPressed, SlotHeld, SlotReleased;

            /// <summary>Some button went down this step.</summary>
            public bool AnyPressed;

            public ControlState Slot(int slot) => new ControlState
            {
                Pressed = slot == SlotPressed, Held = slot == SlotHeld, Released = slot == SlotReleased,
            };
        }

        private readonly bool[] _down = new bool[4];
        private readonly int[] _slotOf = { -1, -1, -1, -1 };
        private readonly float[] _heldFor = new float[4];
        private bool _crouching;
        private bool _reloadStarted;
        private bool _stickPressDown;
        private bool _sprintOn;
        private bool _shouldersDown;

        /// <summary>Forgets every press under way: every button counts as up.</summary>
        public void Reset()
        {
            for (int face = 0; face < 4; face++)
            {
                _down[face] = false;
                _slotOf[face] = -1;
                _heldFor[face] = 0.0f;
            }
            _crouching = false;
            _reloadStarted = false;
            _stickPressDown = false;
            _sprintOn = false;
            _shouldersDown = false;
        }

        /// <summary>The hotbar slot a face button is with these shoulders held: -1 for none (no shoulder, or no slot there).</summary>
        public static int SlotOf(int face, bool leftShoulder, bool rightShoulder)
        {
            if (leftShoulder && rightShoulder)
            {
                return face == South ? 8 : face == East ? 9 : -1;
            }
            if (leftShoulder)
            {
                return face;
            }
            return rightShoulder ? 4 + face : -1;
        }

        public Frame Step(Buttons buttons, float deltaTime)
        {
            var frame = new Frame { SlotPressed = -1, SlotHeld = -1, SlotReleased = -1 };
            bool shoulders = buttons.LeftShoulder || buttons.RightShoulder;
            frame.AnyPressed = shoulders && !_shouldersDown;
            _shouldersDown = shoulders;
            for (int face = 0; face < 4; face++)
            {
                bool down = buttons.Face(face), was = _down[face];
                if (down && !was)
                {
                    frame.AnyPressed = true;
                    _heldFor[face] = 0.0f;
                    _slotOf[face] = SlotOf(face, buttons.LeftShoulder, buttons.RightShoulder);
                    if (shoulders && _slotOf[face] < 0)
                    {
                        _slotOf[face] = int.MaxValue;   // a shoulder held with no slot on this button: nothing, until it comes up
                    }
                }
                else if (down)
                {
                    _heldFor[face] += deltaTime;
                }
                _down[face] = down;
                int slot = _slotOf[face];
                if (slot >= 0)
                {
                    if (slot != int.MaxValue)
                    {
                        if (down && !was) frame.SlotPressed = slot;
                        if (down) frame.SlotHeld = slot;
                        if (!down) frame.SlotReleased = slot;
                    }
                    if (!down)
                    {
                        _slotOf[face] = -1;
                    }
                    continue;
                }
                switch (face)
                {
                    case South:
                        frame.Jump = ControlState.Stepped(was, down);
                        break;
                    case East:
                        if (down && !_crouching && _heldFor[face] >= CrouchAfterSeconds)
                        {
                            _crouching = true;
                            frame.Crouch.Pressed = true;
                        }
                        if (!down && was)
                        {
                            frame.Dash.Pressed = !_crouching;
                            frame.Dash.Released = !_crouching;
                            frame.Crouch.Released = _crouching;
                            _crouching = false;
                        }
                        frame.Crouch.Held = _crouching;
                        break;
                    case West:
                        frame.Interact = ControlState.Stepped(was, down);
                        if (down && !_reloadStarted && _heldFor[face] >= InteractKey.HoldSeconds)
                        {
                            _reloadStarted = true;
                            frame.Reload.Pressed = true;
                        }
                        frame.Reload.Held = _reloadStarted;
                        if (!down)
                        {
                            frame.Reload.Released = _reloadStarted && was;
                            _reloadStarted = false;
                        }
                        break;
                    default:
                        frame.SwapWeapon = down && !was;
                        break;
                }
            }

            bool stickPress = buttons.LeftStickPress;
            if (stickPress && !_stickPressDown)
            {
                frame.AnyPressed = true;
                frame.Sprint.Pressed = !_sprintOn;
                _sprintOn = true;
            }
            _stickPressDown = stickPress;
            if (!buttons.Moving && _sprintOn)
            {
                _sprintOn = false;
                frame.Sprint.Released = true;
            }
            frame.Sprint.Held = _sprintOn;
            return frame;
        }

        /// <summary>A stick's reading with its rest taken out: zero inside <see cref="DeadZone"/>, the rest spread over 0 to 1, no longer than 1.</summary>
        public static Vector2 Stick(Vector2 raw)
        {
            float length = raw.magnitude;
            if (length <= DeadZone)
            {
                return Vector2.zero;
            }
            float spread = Mathf.Clamp01((length - DeadZone) / (1.0f - DeadZone));
            return raw / length * spread;
        }

        /// <summary>The camera stick: past its rest, squared, so a small push turns the camera finely and a full one fast.</summary>
        public static Vector2 Look(Vector2 raw)
        {
            Vector2 stick = Stick(raw);
            return stick * stick.magnitude;
        }
    }
}
