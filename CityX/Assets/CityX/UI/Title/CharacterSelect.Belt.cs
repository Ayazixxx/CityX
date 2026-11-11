using CityX.Character;
using CityX.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The character list's belt (docs/CHARACTER_SELECT.md; owner, 2026-11-11: "build the conveyer belt character select"): the characters in
    /// a row on the creator's belt (<see cref="BeltLineup"/>), A / D, the arrows or a controller rolling the next one to the middle
    /// (<see cref="BeltLine"/>), a click or Enter closing the view in on it, and CREATE NEW rolling a blank body in behind them.
    /// </summary>
    public sealed partial class CharacterSelect
    {
        [Header("The belt, wired by CharacterSelectBuilder")]
        [Tooltip("The figures on the creator's belt.")]
        public BeltLineup Lineup;

        [Tooltip("The creator's belt sequence: it empties the belt for the row, and rolls a new body in on CREATE NEW.")]
        public BeltIntro Intro;

        public TitleScreen Title;

        [Tooltip("The chosen character's name and level over its head, while the belt stands still on it.")]
        public CanvasGroup Tag;

        public Text TagName;
        public Text TagDetail;

        [Tooltip("Roll to the character on the left, or the right: shown while there is one.")]
        public GameObject PreviousButton;

        public GameObject NextButton;

        /// <summary>
        /// Where the view closes in to, on the stage (the chosen character stands at its middle, on the belt's top): near enough to see the
        /// character whole and large, a little to the right so it stands left of the middle of the screen with its card beside it.
        /// </summary>
        public static readonly Vector3 InspectView = new Vector3(-0.7f, 1.3f, 2.2f);

        /// <summary>How long the view takes to close in, or to draw back, seconds.</summary>
        public const float InspectSeconds = 0.45f;

        /// <summary>How far over the top of the middle character's head its name is, metres.</summary>
        public const float TagAbove = 0.35f;

        /// <summary>How long the name takes to come up once the belt stops, or to go as it starts, seconds.</summary>
        private const float TagFadeSeconds = 0.2f;

        /// <summary>Held, the key rolls on to the next one once the belt is this near (in characters) the one it is rolling to.</summary>
        private const float HeldRollsOnWithin = 0.5f;

        private BeltLine _line;
        private float _inspected;
        private bool _creating;
        private int _lastPush;

        /// <summary>The row's motion, or null before the server has answered.</summary>
        public BeltLine Line => _line;

        /// <summary>CREATE NEW has been pressed and the belt is about to bring a new body in (once the view is back and the belt has stopped).</summary>
        public bool Creating => _creating;

        /// <summary>How far the view has closed in on the chosen character: 0 the row, 1 its card.</summary>
        public float Inspected => _inspected;

        private Camera View => Lineup != null ? Lineup.View : null;

        /// <summary>The empty belt seen from where the creator sees it, the creator's own body away off the screen.</summary>
        private void OpenTheBelt()
        {
            _line = null;
            _creating = false;
            _inspected = 0.0f;
            _lastPush = 0;
            if (Intro != null)
            {
                Intro.Empty();
            }
            ViewFrom(0.0f);
        }

        /// <summary>Stands the characters in their row with the chosen one in the middle.</summary>
        private void StandTheRow()
        {
            _line = new BeltLine(_characters.Count, _selectedIndex, RowSpacing(View));
            if (Lineup == null)
            {
                return;
            }
            Lineup.Spacing = _line.Spacing;
            Lineup.Origin = (Lineup.Belt != null ? Lineup.Belt.Travel : 0.0f) + _line.Scroll * _line.Spacing;
            Lineup.Show(_characters);
        }

        /// <summary>
        /// How far apart the characters stand on this screen's shape, metres: midway between the edge of the row's view and the edge of the
        /// closed-in one (each at the middle character, on the side the card is), so the one beside the middle is in sight in the row and out of
        /// it beside the card, on a wide screen or a square one.
        /// </summary>
        public static float RowSpacing(Camera view)
        {
            if (view == null)
            {
                return BeltLine.WideSpacing;
            }
            float across = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * view.aspect;
            float closedIn = Mathf.Abs(InspectView.x) + InspectView.z * across;
            return 0.5f * (BeltIntro.EdgeOfScreen(view) + closedIn);
        }

        private void OnDisable()
        {
            ViewFrom(0.0f);
            if (Intro != null)
            {
                BeltIntro.Sound(Intro.Hum, 0.0f);
            }
        }

        /// <summary>Rolls the next character (to the right) to the middle. Past the last there is none.</summary>
        public void Next() => RollOn(1);

        /// <summary>Rolls the one before (to the left) to the middle.</summary>
        public void Previous() => RollOn(-1);

        /// <summary>
        /// A new character (owner, 2026-11-11: "a create new button is on screen always and when you press it ... it just rolls in an empty
        /// character to the center of the screen for you and you start the customizing"): the view draws back if it had closed in, and once
        /// the belt stands still the creator takes it over and rolls a blank body in behind the characters in sight.
        /// </summary>
        public void CreateNew()
        {
            if (_creating)
            {
                return;
            }
            _creating = true;
            if (_mode != Mode.List)
            {
                Show(Mode.List);
            }
        }

        private void RollOn(int way)
        {
            if (_line == null || _mode != Mode.List || _creating)
            {
                return;
            }
            int next = NextOccupied(_line.Target, way);
            if (next >= 0)
            {
                RollTo(next);
            }
        }

        private void RollTo(int slot)
        {
            _line.RollTo(slot);
            _selectedIndex = _line.Target;
        }

        /// <summary>The nearest slot past <paramref name="from"/> going <paramref name="way"/> (1 right, -1 left) with somebody in it, or -1.</summary>
        private int NextOccupied(int from, int way)
        {
            for (int slot = from + way; slot >= 0 && slot < _characters.Count; slot += way)
            {
                if (_characters[slot] != null)
                {
                    return slot;
                }
            }
            return -1;
        }

        /// <summary>A deleted character's figure goes, and the belt rolls the next one (or else the one before) to the middle.</summary>
        private void LeaveTheBelt(int slot)
        {
            if (Lineup != null)
            {
                Lineup.Empty(slot);
            }
            if (_line == null)
            {
                return;
            }
            int next = NextOccupied(slot, 1);
            if (next < 0)
            {
                next = NextOccupied(slot, -1);
            }
            if (next >= 0)
            {
                RollTo(next);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            ReadInput();
            if (_line != null && Lineup != null && Lineup.Belt != null)
            {
                float moved = _line.Step(deltaTime);
                Lineup.Belt.Travel += moved;
                Lineup.Belt.Speed = _line.BeltSpeed;
                if (Intro != null)
                {
                    BeltIntro.Sound(Intro.Hum, Mathf.Clamp01(Mathf.Abs(_line.BeltSpeed) / BeltRide.Speed));
                }
            }

            bool closeIn = _mode == Mode.Inspecting || _mode == Mode.Confirming;
            _inspected = Mathf.MoveTowards(_inspected, closeIn ? 1.0f : 0.0f, deltaTime / InspectSeconds);
            ViewFrom(_inspected);
            ShowTheTag(deltaTime);
            ShowTheArrows();

            if (_creating && _inspected <= 0.0f && (_line == null || _line.Settled))
            {
                HandOverToTheCreator();
            }
        }

        /// <summary>The keys, the controller and the pointer: rolling, choosing, playing, going back.</summary>
        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            int push = Push(keyboard, pad);
            bool pressed = push != 0 && push != _lastPush;
            _lastPush = push;
            bool choose = (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                          || (pad != null && pad.buttonSouth.wasPressedThisFrame);

            if (_mode == Mode.List && !_creating && _line != null)
            {
                if (push != 0 && (pressed || Mathf.Abs(_line.Scroll - _line.Target) < HeldRollsOnWithin))
                {
                    RollOn(push);
                }
                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !OverInterface() && Lineup != null)
                {
                    int slot = Lineup.SlotUnder(mouse.position.ReadValue());
                    if (slot == _line.Target)
                    {
                        Inspect();
                    }
                    else if (slot >= 0)
                    {
                        RollTo(slot);
                    }
                }
                if (choose)
                {
                    Inspect();
                }
            }
            else if (_mode == Mode.Inspecting && choose)
            {
                PlaySelected();
            }
            if (pad != null && pad.buttonEast.wasPressedThisFrame && Title != null)
            {
                Title.Back();
            }
        }

        /// <summary>-1 (left), 0 or 1 (right): A / D, the arrows, a controller's stick or pad.</summary>
        private static int Push(Keyboard keyboard, Gamepad pad)
        {
            int push = 0;
            if (keyboard != null)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) push++;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) push--;
            }
            if (pad != null)
            {
                float across = pad.leftStick.ReadValue().x + pad.dpad.ReadValue().x;
                if (across > 0.5f) push++;
                else if (across < -0.5f) push--;
            }
            return System.Math.Sign(push);
        }

        private static bool OverInterface()
        {
            EventSystem events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }

        /// <summary>The view <paramref name="share"/> of the way (eased) from where the creator sees the belt to closed in on the chosen character.</summary>
        private void ViewFrom(float share)
        {
            if (View == null || Intro == null || Intro.Creator == null)
            {
                return;
            }
            View.transform.localPosition = Vector3.Lerp(Intro.Creator.CameraHome, InspectView, Mathf.SmoothStep(0.0f, 1.0f, share));
        }

        /// <summary>The middle character's name over its head while the belt stands still on it in the row.</summary>
        private void ShowTheTag(float deltaTime)
        {
            if (Tag == null)
            {
                return;
            }
            CharacterSave chosen = Selected;
            bool shown = _mode == Mode.List && !_creating && chosen != null && _line != null && _line.Settled && _inspected <= 0.0f;
            Tag.alpha = Mathf.MoveTowards(Tag.alpha, shown ? 1.0f : 0.0f, deltaTime / TagFadeSeconds);
            if (chosen != null)
            {
                if (TagName != null) TagName.text = chosen.Name;
                if (TagDetail != null) TagDetail.text = $"Level {chosen.Level}";
            }
            if (View != null && Lineup != null)
            {
                // Anchored at the point's share of the screen, so it sits there on any screen size and however the canvas is drawn.
                Vector3 over = Lineup.transform.TransformPoint(new Vector3(0.0f, BodyRig.StandingHeight + TagAbove, 0.0f));
                Vector2 share = View.WorldToViewportPoint(over);
                var place = (RectTransform)Tag.transform;
                place.anchorMin = place.anchorMax = share;
                place.anchoredPosition = Vector2.zero;
            }
        }

        private void ShowTheArrows()
        {
            bool rolling = _mode == Mode.List && !_creating && _line != null;
            if (PreviousButton != null) PreviousButton.SetActive(rolling && NextOccupied(_line.Target, -1) >= 0);
            if (NextButton != null) NextButton.SetActive(rolling && NextOccupied(_line.Target, 1) >= 0);
        }

        /// <summary>
        /// The creator takes the belt over (TitleScreen.BringNewCharacter) and rolls a blank body in from behind the leftmost character in
        /// sight, far enough behind that every character is off the screen's right by the time the body is in the middle. Those out of sight
        /// on the left go first: they would come through the middle ahead of it.
        /// </summary>
        private void HandOverToTheCreator()
        {
            _creating = false;
            if (Title == null || Lineup == null)
            {
                return;
            }
            float leftmost = Lineup.LeftmostInSight();
            float start = leftmost - (BeltIntro.EdgeOfScreen(View) + BeltIntro.BeyondTheEdge);
            Lineup.ClearLeftOf(leftmost - 0.5f * Lineup.Spacing);
            Title.BringNewCharacter(start, Lineup.Belt != null ? Lineup.Belt.Travel : 0.0f);
        }
    }
}
