using System;
using System.Collections.Generic;
using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Bottom-of-screen conversation box: a speaker name, one line at a time
    /// advanced with Enter, and lines that can carry a set of player replies
    /// to pick between. Ported from ui/dialogue_box.gd.
    ///
    /// OWNS NO CONVERSATION CONTENT. Driven entirely by whichever NPC opens
    /// it (see UniqueNpc); branching is not expressed in here at all -- a
    /// node with options ends the sequence and reports which id was picked
    /// (OptionChosen), and the owner decides what to open next. Free of jump
    /// targets/indices, which are the usual source of dialogue trees that
    /// silently go to the wrong place.
    ///
    /// Cursor visibility is owned HERE, not by whoever opens this box --
    /// the box needs a visible, clickable cursor for its own reply buttons
    /// regardless of who asked it to open, the same self-containment
    /// ui/dialogue_box.gd's own _was_mouse_captured save/restore has.
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour, IDialogueBox
    {
        [Header("Wired by DialogueBoxBuilder")]
        public GameObject Panel;

        public Text SpeakerLabel;
        public Text LineLabel;
        public Transform OptionsParent;
        public GameObject OptionButtonTemplate;
        public Text HintLabel;

        public static readonly Color OptionColour = new Color(0.78f, 0.80f, 0.86f);
        public static readonly Color OptionSelectedColour = new Color(1.0f, 0.87f, 0.35f);

        private const string SelectedPrefix = "> ";
        private const string UnselectedPrefix = "   ";

        public event Action Finished;
        public event Action<string> OptionChosen;

        private DialogueNode[] _nodes = Array.Empty<DialogueNode>();
        private int _index;
        private readonly List<Button> _optionButtons = new List<Button>();
        private readonly List<Text> _optionLabels = new List<Text>();
        private int _selected;
        private bool _wasCursorVisible;
        private CursorLockMode _previousLockState;

        private bool _isOpen;

        /// <summary>
        /// The logical state, tracked separately from Panel.activeSelf
        /// rather than derived from it -- Panel is a visual field wired by
        /// DialogueBoxBuilder and is legitimately null on a bare component
        /// built directly in a test, and IsOpen has to mean the same thing
        /// either way.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Whether ANY DialogueBox anywhere is currently open -- there is one
        /// instance per NPC prefab (DialogueBoxBuilder is baked into each
        /// one, not a single scene singleton like Console/Pause/Map), so
        /// nothing else can hold one instance reference and ask it directly.
        /// PlayerChat reads this to stay closed while a conversation is up;
        /// see its own guard list for why -- without it, Enter to advance a
        /// dialogue line also opened the chat box in the same frame.
        /// </summary>
        public static bool AnyOpen { get; private set; }

        /// <summary>True while the player is being asked to pick a reply.</summary>
        public bool IsChoosing => IsOpen && _optionButtons.Count > 0;

        private void Awake()
        {
            if (Panel != null)
            {
                Panel.SetActive(false);
            }
        }

        /// <summary>
        /// Starts a conversation. An empty node list closes out immediately
        /// (still firing Finished) rather than showing a blank box.
        /// </summary>
        public void Open(string speaker, DialogueNode[] nodes)
        {
            if (nodes == null || nodes.Length == 0)
            {
                Finished?.Invoke();
                return;
            }
            _nodes = nodes;
            _index = 0;
            _isOpen = true;
            AnyOpen = true;
            if (SpeakerLabel != null)
            {
                SpeakerLabel.text = speaker;
            }
            if (Panel != null)
            {
                Panel.SetActive(true);
            }

            // Released for the WHOLE conversation, not just while replies
            // are up -- flipping the cursor on and off between lines reads
            // as a glitch.
            _wasCursorVisible = Cursor.visible;
            _previousLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ShowCurrentNode();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            _isOpen = false;
            AnyOpen = false;
            if (Panel != null)
            {
                Panel.SetActive(false);
            }
            ClearOptions();
            _nodes = Array.Empty<DialogueNode>();
            _index = 0;
            RestoreCursor();
            Finished?.Invoke();
        }

        private void RestoreCursor()
        {
            Cursor.visible = _wasCursorVisible;
            Cursor.lockState = _previousLockState;
        }

        private void ShowCurrentNode()
        {
            ClearOptions();
            DialogueNode node = _nodes[_index];
            if (LineLabel != null)
            {
                LineLabel.text = node.Text;
            }

            if (node.Options.Length == 0)
            {
                if (HintLabel != null)
                {
                    HintLabel.text = _index < _nodes.Length - 1 ? "[Enter] continue" : "[Enter] close";
                }
                return;
            }

            _selected = 0;
            for (int i = 0; i < node.Options.Length; i++)
            {
                int captured = i;
                GameObject buttonObject = OptionButtonTemplate != null
                    ? Instantiate(OptionButtonTemplate, OptionsParent)
                    : new GameObject($"Option{i}");
                buttonObject.SetActive(true);
                Button button = buttonObject.GetComponent<Button>();
                if (button == null)
                {
                    button = buttonObject.AddComponent<Button>();
                }
                Text label = buttonObject.GetComponentInChildren<Text>();

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnOptionPressed(captured));

                var trigger = buttonObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (trigger == null)
                {
                    trigger = buttonObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                }
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter,
                };
                entry.callback.AddListener(_ => OnOptionHovered(captured));
                trigger.triggers.Add(entry);

                _optionButtons.Add(button);
                _optionLabels.Add(label);
            }
            RefreshOptionVisuals();
            if (HintLabel != null)
            {
                HintLabel.text = "[Arrows / A D] choose    [Enter] select";
            }
        }

        private void ClearOptions()
        {
            foreach (Button button in _optionButtons)
            {
                if (button != null)
                {
                    // DestroyImmediate, not Destroy -- Destroy throws
                    // outside Play Mode ("Destroy may not be called from
                    // edit mode"), and this runs from an EditMode test's
                    // own direct DialogueBox.Open/Advance calls, not just
                    // from a live Update loop.
                    if (Application.isPlaying)
                    {
                        Destroy(button.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(button.gameObject);
                    }
                }
            }
            _optionButtons.Clear();
            _optionLabels.Clear();
        }

        private void RefreshOptionVisuals()
        {
            DialogueOption[] options = _nodes[_index].Options;
            for (int i = 0; i < _optionButtons.Count; i++)
            {
                if (_optionLabels[i] == null)
                {
                    continue;
                }
                bool isSelected = i == _selected;
                _optionLabels[i].text = (isSelected ? SelectedPrefix : UnselectedPrefix) + options[i].Text;
                _optionLabels[i].color = isSelected ? OptionSelectedColour : OptionColour;
            }
        }

        private void OnOptionHovered(int index)
        {
            if (index == _selected)
            {
                return;
            }
            _selected = index;
            RefreshOptionVisuals();
        }

        private void OnOptionPressed(int index)
        {
            _selected = index;
            ConfirmSelection();
        }

        /// <summary>Picks a reply by index and confirms it in one call, for a test to drive without a real Keyboard/mouse.</summary>
        public void Choose(int index)
        {
            OnOptionPressed(index);
        }

        /// <summary>
        /// Confirms whichever reply is currently selected. Public so a test
        /// can drive it directly -- Keyboard.current is null in an EditMode
        /// test context (no real input device), and Update is only a thin
        /// wrapper reading it.
        /// </summary>
        public void ConfirmSelection()
        {
            DialogueOption[] options = _nodes[_index].Options;
            if (_selected < 0 || _selected >= options.Length)
            {
                return;
            }
            string id = options[_selected].Id;
            // Torn down BEFORE the event: whoever is listening will almost
            // always open the next stretch of conversation straight away,
            // and that has to land on a clean box rather than on top of the
            // old one's own replies.
            _isOpen = false;
            AnyOpen = false;
            if (Panel != null)
            {
                Panel.SetActive(false);
            }
            ClearOptions();
            _nodes = Array.Empty<DialogueNode>();
            _index = 0;
            RestoreCursor();
            OptionChosen?.Invoke(id);
        }

        /// <summary>Moves to the next line, or closes if that was the last one. Public for the same reason ConfirmSelection is.</summary>
        public void Advance()
        {
            _index++;
            if (_index >= _nodes.Length)
            {
                Close();
                return;
            }
            ShowCurrentNode();
        }

        /// <summary>
        /// Enter advances/confirms; arrows or A/D move the selection while
        /// choosing. Shares Enter with PlayerChat, so this only ever reads
        /// the key while actually open -- it never consumes anything a
        /// closed box has no business intercepting.
        /// </summary>
        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (IsChoosing)
                {
                    ConfirmSelection();
                }
                else
                {
                    Advance();
                }
                return;
            }

            if (!IsChoosing)
            {
                return;
            }

            int step = 0;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame
                || keyboard.aKey.wasPressedThisFrame)
            {
                step = -1;
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                || keyboard.dKey.wasPressedThisFrame)
            {
                step = 1;
            }
            if (step == 0)
            {
                return;
            }
            // Wraps rather than clamping, so pressing past either end of
            // the list rolls round instead of sticking.
            _selected = ((_selected + step) % _optionButtons.Count + _optionButtons.Count) % _optionButtons.Count;
            RefreshOptionVisuals();
        }
    }
}
