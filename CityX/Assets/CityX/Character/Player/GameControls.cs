using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace CityX.Character
{
    /// <summary>One of the game's controls this frame, from the keyboard and mouse or a controller, whichever is used.</summary>
    public struct ControlState
    {
        /// <summary>Down now.</summary>
        public bool Held;

        /// <summary>Went down this frame.</summary>
        public bool Pressed;

        /// <summary>Came up this frame.</summary>
        public bool Released;

        public static ControlState Of(ButtonControl button) => button == null
            ? default
            : new ControlState { Held = button.isPressed, Pressed = button.wasPressedThisFrame, Released = button.wasReleasedThisFrame };

        /// <summary>A control that is down for as long as <paramref name="now"/>, given whether it was down the frame before.</summary>
        public static ControlState Stepped(bool before, bool now) => new ControlState { Held = now, Pressed = now && !before, Released = before && !now };

        public static ControlState operator |(ControlState a, ControlState b) => new ControlState
        {
            Held = a.Held || b.Held, Pressed = a.Pressed || b.Pressed, Released = a.Released || b.Released,
        };
    }

    /// <summary>
    /// THE GAME'S CONTROLS, ONE PLACE FOR BOTH DEVICES (owner, 2026-11-25: "make it playable on controller"; the layout is docs/CONTROLLER.md).
    /// The game asks for what it wants ("jump went down", "the stick says forward"), and this answers from the keyboard and mouse, exactly
    /// as they were read before, OR from a controller. Nothing on the keyboard and mouse changes; a controller adds its own.
    ///
    /// Read once a frame, the first time anything asks (<see cref="Now"/>), so every reader in a frame sees the same presses and a tap timed
    /// over frames is timed once. What the buttons mean on a controller beyond one-for-one (a shoulder held turning the face buttons into the
    /// hotbar, B tapped for a dash and held for a crouch, L3 for a sprint that stays on) is <see cref="PadLayout"/>'s, pure and tested.
    /// </summary>
    public sealed class GameControls
    {
        private static readonly GameControls Current = new GameControls();
        private int _readFrame = -1;
        private uint _readUpdate = uint.MaxValue;

        private readonly PadLayout _pad = new PadLayout();

        // Last frame's stick-as-keys and triggers, for their Pressed and Released.
        private bool _wasForward, _wasBack, _wasLeft, _wasRight, _wasAttack, _wasAim, _wasNavUp, _wasNavDown, _wasNavLeft, _wasNavRight;

        /// <summary>This frame's controls.</summary>
        public static GameControls Now
        {
            get
            {
                Current.ReadIfNewFrame();
                return Current;
            }
        }

        /// <summary>A controller was the last thing used (for button prompts).</summary>
        public static bool PadInUse { get; private set; }

        public Keyboard Keyboard { get; private set; }
        public Mouse Mouse { get; private set; }
        public Gamepad Pad { get; private set; }

        // --- moving
        /// <summary>WASD or the left stick, each in [-1, 1] (the keys' sum, as before: a diagonal is (1, 1)).</summary>
        public Vector2 Move;
        public ControlState Forward, Back, Left, Right;
        public ControlState Jump, Sprint, Dash, Crouch;

        // --- fighting
        public ControlState Attack, Aim, Interact, Reload, LockOn, ShoulderSwap, Torch;

        /// <summary>The mouse wheel's turn this frame, or a whole notch from the controller's swap button (Y).</summary>
        public float WeaponScroll;

        // --- the camera
        /// <summary>The mouse's move this frame, pixels.</summary>
        public Vector2 LookMouse;

        /// <summary>The right stick, in [-1, 1] after its dead zone and curve (turned into degrees by the camera).</summary>
        public Vector2 LookStick;

        // --- menus and the rest
        public ControlState Inventory, PlayerMenu, Pause, TalkHeld;

        /// <summary>In a menu or a dialogue: choose (Enter, A), go back (Escape, B), and move through a list (the arrows, the D-pad, the left stick).</summary>
        public ControlState Confirm, Cancel, NavUp, NavDown, NavLeft, NavRight;

        /// <summary>The controller's B alone: back out of a window the keyboard closes with its own key (a loot window, an item card).</summary>
        public ControlState PadCancel;

        /// <summary>Driving: a controller's right trigger less its left, in [-1, 1] (the keyboard drives with W and S, in <see cref="Move"/>).</summary>
        public float Throttle;

        private readonly ControlState[] _slots = new ControlState[PadLayout.Slots];

        /// <summary>Hotbar slot <paramref name="slot"/> (0 is key 1, 9 is key 0): its number key, or its shoulder and face button.</summary>
        public ControlState Slot(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot] : default;

        /// <summary>A keyboard key, for what only a keyboard has (chat, the console, emote numbers).</summary>
        public ControlState Key(Key key) => Keyboard != null ? ControlState.Of(Keyboard[key]) : default;

        private void ReadIfNewFrame()
        {
            // Keyed on the input system's own update, not the frame: a FixedUpdate (the car) can ask before this frame's input has been read,
            // and a snapshot taken then would hide the frame's presses from everything after it.
            if (_readUpdate == InputState.updateCount)
            {
                return;
            }
            bool skipped = _readFrame < Time.frameCount - 1;
            _readUpdate = InputState.updateCount;
            _readFrame = Time.frameCount;
            Keyboard = Keyboard.current;
            Mouse = Mouse.current;
            Pad = Gamepad.current;
            ReadKeyboardAndMouse();
            if (Pad != null)
            {
                if (skipped)
                {
                    _pad.Reset();   // unread for a while: a press then is a new one
                }
                AddPad(Pad);
            }
            else
            {
                _pad.Reset();
                _wasForward = _wasBack = _wasLeft = _wasRight = _wasAttack = _wasAim = false;
                _wasNavUp = _wasNavDown = _wasNavLeft = _wasNavRight = false;
            }
        }

        private void ReadKeyboardAndMouse()
        {
            Keyboard k = Keyboard;
            Mouse m = Mouse;
            Forward = k != null ? ControlState.Of(k.wKey) : default;
            Back = k != null ? ControlState.Of(k.sKey) : default;
            Left = k != null ? ControlState.Of(k.aKey) : default;
            Right = k != null ? ControlState.Of(k.dKey) : default;
            Move = new Vector2((Right.Held ? 1.0f : 0.0f) - (Left.Held ? 1.0f : 0.0f), (Forward.Held ? 1.0f : 0.0f) - (Back.Held ? 1.0f : 0.0f));
            Jump = k != null ? ControlState.Of(k.spaceKey) : default;
            Sprint = k != null ? ControlState.Of(k.leftShiftKey) : default;
            Dash = default;   // on the keyboard a dash is Shift tapped twice (PlayerInputReader.ConsumeDoubleTapShift reads Sprint)
            Crouch = k != null ? ControlState.Of(k.leftCtrlKey) : default;
            Interact = k != null ? ControlState.Of(k.eKey) : default;
            Reload = k != null ? ControlState.Of(k.rKey) : default;
            LockOn = k != null ? ControlState.Of(k.tabKey) : default;
            ShoulderSwap = k != null ? ControlState.Of(k.qKey) : default;
            Torch = k != null ? ControlState.Of(k.fKey) : default;
            Inventory = k != null ? ControlState.Of(k.iKey) : default;
            PlayerMenu = k != null ? ControlState.Of(k.cKey) : default;
            Pause = k != null ? ControlState.Of(k.escapeKey) : default;
            TalkHeld = k != null ? ControlState.Of(k.vKey) : default;
            Confirm = k != null ? ControlState.Of(k.enterKey) | ControlState.Of(k.numpadEnterKey) : default;
            Cancel = Pause;
            PadCancel = default;
            Throttle = 0.0f;
            NavUp = k != null ? ControlState.Of(k.upArrowKey) : default;
            NavDown = k != null ? ControlState.Of(k.downArrowKey) : default;
            NavLeft = k != null ? ControlState.Of(k.leftArrowKey) : default;
            NavRight = k != null ? ControlState.Of(k.rightArrowKey) : default;
            for (int slot = 0; slot < _slots.Length; slot++)
            {
                _slots[slot] = k != null ? ControlState.Of(k[PlayerSkills.KeyFor(slot)]) : default;
            }
            Attack = m != null ? ControlState.Of(m.leftButton) : default;
            Aim = m != null ? ControlState.Of(m.rightButton) : default;
            WeaponScroll = m != null ? m.scroll.ReadValue().y : 0.0f;
            LookMouse = m != null ? m.delta.ReadValue() : Vector2.zero;
            LookStick = Vector2.zero;
            if (k != null && k.anyKey.wasPressedThisFrame || m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame))
            {
                PadInUse = false;
            }
        }

        private void AddPad(Gamepad pad)
        {
            Vector2 stick = PadLayout.Stick(pad.leftStick.ReadValue());
            PadLayout.Frame frame = _pad.Step(new PadLayout.Buttons
            {
                South = pad.buttonSouth.isPressed,
                East = pad.buttonEast.isPressed,
                West = pad.buttonWest.isPressed,
                North = pad.buttonNorth.isPressed,
                LeftShoulder = pad.leftShoulder.isPressed,
                RightShoulder = pad.rightShoulder.isPressed,
                LeftStickPress = pad.leftStickButton.isPressed,
                Moving = stick.sqrMagnitude > 0.0f,
            }, Time.unscaledDeltaTime);

            if (stick.sqrMagnitude > Move.sqrMagnitude)
            {
                Move = stick;
            }
            Forward |= Stepped(ref _wasForward, stick.y > PadLayout.StickAsKey);
            Back |= Stepped(ref _wasBack, stick.y < -PadLayout.StickAsKey);
            Right |= Stepped(ref _wasRight, stick.x > PadLayout.StickAsKey);
            Left |= Stepped(ref _wasLeft, stick.x < -PadLayout.StickAsKey);

            Jump |= frame.Jump;
            Sprint |= frame.Sprint;
            Dash |= frame.Dash;
            Crouch |= frame.Crouch;
            Interact |= frame.Interact;
            Reload |= frame.Reload;
            for (int slot = 0; slot < _slots.Length; slot++)
            {
                _slots[slot] |= frame.Slot(slot);
            }
            Attack |= Stepped(ref _wasAttack, pad.rightTrigger.ReadValue() > PadLayout.TriggerDown);
            Aim |= Stepped(ref _wasAim, pad.leftTrigger.ReadValue() > PadLayout.TriggerDown);
            LockOn |= ControlState.Of(pad.rightStickButton);
            ShoulderSwap |= ControlState.Of(pad.dpad.up);
            Torch |= ControlState.Of(pad.dpad.down);
            Inventory |= ControlState.Of(pad.dpad.right);
            TalkHeld |= ControlState.Of(pad.dpad.left);
            PlayerMenu |= ControlState.Of(pad.selectButton);
            Pause |= ControlState.Of(pad.startButton);
            Confirm |= ControlState.Of(pad.buttonSouth);
            PadCancel = ControlState.Of(pad.buttonEast);
            Cancel |= PadCancel | ControlState.Of(pad.startButton);
            Throttle = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            NavUp |= ControlState.Of(pad.dpad.up) | Stepped(ref _wasNavUp, stick.y > PadLayout.StickAsKey);
            NavDown |= ControlState.Of(pad.dpad.down) | Stepped(ref _wasNavDown, stick.y < -PadLayout.StickAsKey);
            NavLeft |= ControlState.Of(pad.dpad.left) | Stepped(ref _wasNavLeft, stick.x < -PadLayout.StickAsKey);
            NavRight |= ControlState.Of(pad.dpad.right) | Stepped(ref _wasNavRight, stick.x > PadLayout.StickAsKey);
            if (frame.SwapWeapon)
            {
                WeaponScroll = PadLayout.SwapNotch;
            }
            LookStick = PadLayout.Look(pad.rightStick.ReadValue());

            if (frame.AnyPressed || stick.sqrMagnitude > 0.0f || LookStick.sqrMagnitude > 0.0f || pad.rightTrigger.ReadValue() > PadLayout.TriggerDown
                || pad.leftTrigger.ReadValue() > PadLayout.TriggerDown || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
                || pad.dpad.ReadValue().sqrMagnitude > 0.0f)
            {
                PadInUse = true;
            }
        }

        private static ControlState Stepped(ref bool was, bool now)
        {
            ControlState state = ControlState.Stepped(was, now);
            was = now;
            return state;
        }

    }
}
