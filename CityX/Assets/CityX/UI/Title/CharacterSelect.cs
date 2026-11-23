using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Choosing, making and deleting characters, and entering the world (docs/CHARACTER_SELECT.md): the player's characters stand in a row on
    /// the conveyor belt they were made on, and the belt rolls one to the middle (<see cref="CharacterSelect"/>'s belt half,
    /// CharacterSelect.Belt.cs). Chosen, the view closes in on it with its card: PLAY, delete, BACK. CREATE NEW is always there, and rolls a
    /// blank body in behind the row to be made.
    ///
    /// ONE COMPONENT FOR CHOOSING, MAKING AND DELETING, deliberately: they are the same screen in different modes, and splitting them would
    /// mean three scripts passing the same list between each other and agreeing on who owns it.
    ///
    /// It asks the server played on (<see cref="CharacterSources.Current"/>: this PC's own, or the one joined) for the list each time it opens
    /// rather than holding an unsaved one, so backing out of the screen can never lose a character.
    /// </summary>
    public sealed partial class CharacterSelect : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>The row on the belt.</summary>
            List,

            /// <summary>Typing a name for a new one.</summary>
            Creating,

            /// <summary>Confirming a delete, over the chosen character's card.</summary>
            Confirming,

            /// <summary>Closed in on the chosen character, its card up.</summary>
            Inspecting,

            /// <summary>The chosen character's look being redrawn in the redraw creator, over everything (CharacterSelect.Redraw.cs).</summary>
            Redrawing,
        }

        [Header("Wired by CharacterSelectBuilder")]
        [Tooltip("The row's own controls: the name over the middle character, the arrows, BACK.")]
        public GameObject ListPanel;

        public GameObject CreatePanel;
        public GameObject ConfirmPanel;

        [Tooltip("The chosen character's card, beside it once the view has closed in: name, level, play time, stats, PLAY, the bin, BACK.")]
        public GameObject CardPanel;

        public InputField NameField;
        public Text ConfirmText;
        public Text EmptyNotice;

        [Tooltip("On the card.")]
        public Text NameLabel;

        public Text DetailLabel;
        public Text StatsLabel;

        [Tooltip("Swatches the creator offers. Chosen by CharacterSelectBuilder.")]
        public Image[] Swatches = new Image[0];

        [Tooltip("The scene the world lives in.")]
        public string WorldScene = "CityMassing";

        private List<CharacterSave> _characters = new List<CharacterSave>();
        private Mode _mode = Mode.List;
        private int _selectedIndex;
        private string _pendingDeleteId;
        private Color _pendingSkin = CharacterSave.DefaultSkin;

        public Mode Showing => _mode;

        /// <summary>Which slot of the row is chosen (the one in the middle, or being rolled there).</summary>
        public int SelectedIndex => _selectedIndex;

        /// <summary>The row, slot by slot; a slot whose character was deleted while the screen was up holds null.</summary>
        public IReadOnlyList<CharacterSave> Characters => _characters;

        /// <summary>The chosen character, or null with nobody there.</summary>
        public CharacterSave Selected =>
            _selectedIndex >= 0 && _selectedIndex < _characters.Count ? _characters[_selectedIndex] : null;

        /// <summary>
        /// Paint handed over by the creator, waiting for a name.
        ///
        /// Held here rather than in the creator because the creator is torn
        /// down when it closes, and the paint has to outlive it long enough to
        /// be written into the save.
        /// </summary>
        public byte[] PendingPaint;

        /// <summary>The hairstyle chosen in the creator, for the same reason.</summary>
        public string PendingHair = "none";

        /// <summary>The hair colour chosen in the creator, held for the same reason.</summary>
        public Color PendingHairColour = CharacterSave.DefaultHairColour;

        /// <summary>The beard chosen in the creator, held for the same reason.</summary>
        public string PendingBeard = "none";

        /// <summary>
        /// Set by whoever shows a NEW character off before the world loads (the conveyor belt it was made on carries it away, BeltIntro):
        /// handed the loading of the world, to call when it is done. Null: the world loads at once.
        /// </summary>
        public System.Action<System.Action> Departure;

        private void OnEnable()
        {
            OpenTheBelt();
            Show(Mode.List);
            Refresh();
        }

        /// <summary>Asks the server for the player's characters again and stands them on the belt.</summary>
        public void Refresh()
        {
            CharacterSources.Current.List(characters =>
            {
                if (this == null || !isActiveAndEnabled)
                {
                    return;   // a server's late answer, after the screen has gone
                }
                _characters = characters ?? new List<CharacterSave>();
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _characters.Count - 1));
                StandTheRow();
                ShowEmptyNotice();
            });
        }

        public void Show(Mode mode)
        {
            _mode = mode;
            if (ListPanel != null) ListPanel.SetActive(mode == Mode.List);
            if (CreatePanel != null) CreatePanel.SetActive(mode == Mode.Creating);
            if (ConfirmPanel != null) ConfirmPanel.SetActive(mode == Mode.Confirming);
            if (CardPanel != null) CardPanel.SetActive(mode == Mode.Inspecting || mode == Mode.Confirming);
            if (mode == Mode.Inspecting)
            {
                SetCard(Selected);
            }
        }

        /// <summary>
        /// Steps back one mode: from a delete being confirmed to the card, from the card to the row.
        ///
        /// It does NOT hide itself when already at the row. Which panel is on screen is TitleScreen's to decide -- two things setting the same
        /// GameObject active is exactly the bug TitleScreen's enum exists to prevent.
        /// </summary>
        public void Back()
        {
            if (Time.frameCount == _redrawEndedOnFrame)
            {
                return;   // the Escape that just closed the redraw creator, not a second step back
            }
            switch (_mode)
            {
                case Mode.Confirming:
                    CancelDelete();
                    break;
                case Mode.Inspecting:
                case Mode.Creating:
                    Show(Mode.List);
                    break;
            }
        }

        /// <summary>The chosen character's card, closed in on: from the row, with somebody in the middle and the belt stopped.</summary>
        public void Inspect()
        {
            if (_mode == Mode.List && Selected != null && (_line == null || _line.Settled) && !_creating)
            {
                Show(Mode.Inspecting);
            }
        }

        private void SetCard(CharacterSave character)
        {
            if (NameLabel != null)
            {
                NameLabel.text = character != null ? character.Name : string.Empty;
            }
            if (DetailLabel != null)
            {
                DetailLabel.text = character != null ? $"Level {character.Level}   {character.PlaytimeText()}" : string.Empty;
            }
            if (StatsLabel != null)
            {
                StatsLabel.text = character != null ? CharacterStats.Summary(character) : string.Empty;
            }
        }

        /// <summary>What the empty belt says: what it was built saying, before any refusal was written over it.</summary>
        private string _emptyText;

        private void ShowEmptyNotice()
        {
            if (EmptyNotice == null)
            {
                return;
            }
            _emptyText ??= EmptyNotice.text;
            EmptyNotice.text = _emptyText;
            EmptyNotice.gameObject.SetActive(!_characters.Exists(character => character != null));
        }

        // --- making one ---

        /// <summary>The old name-and-skin box (Creating). The creator names its own characters now.</summary>
        public void BeginCreate()
        {
            _pendingSkin = CharacterSave.DefaultSkin;
            if (NameField != null)
            {
                NameField.text = string.Empty;
                NameField.ActivateInputField();
            }
            HighlightSwatches();
            Show(Mode.Creating);
        }

        /// <summary>Picks the nth offered skin colour.</summary>
        public void ChooseSkin(int index)
        {
            if (Swatches == null || index < 0 || index >= Swatches.Length
                || Swatches[index] == null)
            {
                return;
            }
            _pendingSkin = Swatches[index].color;
            HighlightSwatches();
        }

        private void HighlightSwatches()
        {
            if (Swatches == null)
            {
                return;
            }
            foreach (Image swatch in Swatches)
            {
                if (swatch == null)
                {
                    continue;
                }
                var outline = swatch.GetComponent<Outline>();
                if (outline != null)
                {
                    // Approximately, because the colour makes a round trip
                    // through a serialized Image and comes back very slightly
                    // different -- an exact comparison silently never matches.
                    outline.enabled = Approximately(swatch.color, _pendingSkin);
                }
            }
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f
                   && Mathf.Abs(a.g - b.g) < 0.004f
                   && Mathf.Abs(a.b - b.b) < 0.004f;
        }

        /// <summary>
        /// Has the server make the character and goes straight into the world with it --
        /// which is what somebody who just typed a name wants, rather than
        /// being returned to a list to find and click the thing they made.
        /// Returns the server's new character when it answered at once (this PC's own server), else null.
        /// </summary>
        public CharacterSave ConfirmCreate()
        {
            string name = NameField != null ? NameField.text : string.Empty;
            CharacterSave requested = CharacterSave.Create(name, _pendingSkin);
            requested.SetPaint(PendingPaint);
            requested.HairStyle = string.IsNullOrEmpty(PendingHair) ? "none" : PendingHair;
            requested.HairColour = PendingHairColour;
            requested.Beard = string.IsNullOrEmpty(PendingBeard) ? "none" : PendingBeard;
            PendingPaint = null;
            PendingHair = "none";
            PendingBeard = "none";
            PendingHairColour = CharacterSave.DefaultHairColour;
            CharacterSave answered = null;
            CharacterSources.Current.Create(requested, made =>
            {
                answered = made;
                if (this == null)
                {
                    return;
                }
                if (made == null)
                {
                    Refuse($"The server would not make that character (at most {CharacterService.MaxCharactersPerPlayer} each).");
                    return;
                }
                _characters.Insert(0, made);
                _selectedIndex = 0;
                Play(made, isNew: true);
            });
            return answered;
        }

        private void Refuse(string why)
        {
            Debug.LogWarning($"CityX: {why}");
            if (EmptyNotice != null)
            {
                _emptyText ??= EmptyNotice.text;
                EmptyNotice.text = why;
                EmptyNotice.gameObject.SetActive(true);
            }
            Show(Mode.List);
        }

        // --- deleting one ---

        public void BeginDelete(string id)
        {
            _pendingDeleteId = id;
            CharacterSave character = _characters.Find(c => c != null && c.Id == id);
            if (ConfirmText != null)
            {
                ConfirmText.text = character == null
                    ? "Delete this character?"
                    : $"Delete {character.Name}, level {character.Level}?\nThis cannot be undone.";
            }
            Show(Mode.Confirming);
        }

        /// <summary>Deletes the chosen character (after asking).</summary>
        public void BeginDeleteSelected()
        {
            CharacterSave character = Selected;
            if (character != null)
            {
                BeginDelete(character.Id);
            }
        }

        /// <summary>
        /// Deletes it on the server, and it leaves the belt: its spot stays empty while the screen is up (a gap on a belt closes only by
        /// everything on it moving), and the belt rolls the next one to the middle.
        /// </summary>
        public void ConfirmDelete()
        {
            if (!string.IsNullOrEmpty(_pendingDeleteId))
            {
                CharacterSources.Current.Delete(_pendingDeleteId, deleted =>
                {
                    if (!deleted && this != null && isActiveAndEnabled)
                    {
                        Refresh();   // the server kept it: show the row as it really is
                    }
                });
                int slot = _characters.FindIndex(c => c != null && c.Id == _pendingDeleteId);
                if (slot >= 0)
                {
                    _characters[slot] = null;
                    LeaveTheBelt(slot);
                }
                _pendingDeleteId = null;
                ShowEmptyNotice();
            }
            Show(Mode.List);
        }

        public void CancelDelete()
        {
            _pendingDeleteId = null;
            Show(Selected != null ? Mode.Inspecting : Mode.List);
        }

        // --- playing one ---

        public void Play(CharacterSave character, bool isNew)
        {
            if (character == null)
            {
                return;
            }

            // The server hands back the character as IT keeps it (and whether this player is an admin): that copy is the one played.
            CharacterSources.Current.Play(character.Id, (served, isAdmin) =>
            {
                if (this == null)
                {
                    return;
                }
                if (served == null)
                {
                    Refuse($"The server would not let {character.Name} be played.");
                    return;
                }
                PlaySession.Begin(served, isNew, isAdmin);
                if (isNew && Departure != null)
                {
                    Departure(LoadWorld);
                }
                else
                {
                    LoadWorld();
                }
            });
        }

        private void LoadWorld()
        {
            // Guarded because the scene has to be in the build settings for
            // this to work, and a missing entry throws an exception that reads
            // as the button being broken rather than as a project setting.
            if (Application.CanStreamedLevelBeLoaded(WorldScene))
            {
                SceneManager.LoadScene(WorldScene);
            }
            else
            {
                Debug.LogError($"CityX: the scene '{WorldScene}' is not in the build settings, " +
                               "so there is nothing to load. Add it under File > Build Profiles.");
            }
        }

        /// <summary>Plays the chosen character.</summary>
        public void PlaySelected()
        {
            Play(Selected, isNew: false);
        }
    }
}
