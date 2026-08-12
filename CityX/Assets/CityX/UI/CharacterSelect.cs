using System.Collections.Generic;
using CityX.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Choosing, making and deleting characters, and entering the world.
    ///
    /// ONE COMPONENT FOR ALL THREE, deliberately. They are the same screen with
    /// different rows on it: making a character is what the empty list offers,
    /// and deleting is a mode the list goes into. Splitting them would mean
    /// three scripts passing the same list between each other and agreeing on
    /// who owns it.
    ///
    /// It reads and writes through <see cref="CharacterStore"/> on every
    /// change rather than holding an unsaved list, so backing out of the screen
    /// can never lose a character and there is no "are you sure you want to
    /// discard" to get wrong.
    /// </summary>
    public sealed class CharacterSelect : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>The list, with a row per character.</summary>
            List,

            /// <summary>Typing a name for a new one.</summary>
            Creating,

            /// <summary>Confirming a delete.</summary>
            Confirming,
        }

        [Header("Wired by CharacterSelectBuilder")]
        public GameObject ListPanel;

        public GameObject CreatePanel;
        public GameObject ConfirmPanel;

        [Tooltip("Rows are instantiated under this.")]
        public RectTransform RowParent;

        [Tooltip("The row template, kept inactive and cloned per character.")]
        public GameObject RowTemplate;

        public InputField NameField;
        public Text ConfirmText;
        public Text EmptyNotice;

        [Tooltip("Swatches the creator offers. Chosen by CharacterSelectBuilder.")]
        public Image[] Swatches = new Image[0];

        [Tooltip("The scene the world lives in.")]
        public string WorldScene = "CityMassing";

        private readonly List<GameObject> _rows = new List<GameObject>();
        private List<CharacterSave> _characters = new List<CharacterSave>();
        private Mode _mode = Mode.List;
        private string _pendingDeleteId;
        private Color _pendingSkin = CharacterSave.DefaultSkin;

        public Mode Showing => _mode;

        /// <summary>How many characters are on screen. Public so a test can read it.</summary>
        public int RowCount => _rows.Count;

        public IReadOnlyList<CharacterSave> Characters => _characters;

        public Color PendingSkin => _pendingSkin;

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

        private void OnEnable()
        {
            Refresh();
            Show(Mode.List);
        }

        /// <summary>Re-reads the store and rebuilds the rows.</summary>
        public void Refresh()
        {
            _characters = CharacterStore.Load();
            BuildRows();
        }

        public void Show(Mode mode)
        {
            _mode = mode;
            if (ListPanel != null) ListPanel.SetActive(mode == Mode.List);
            if (CreatePanel != null) CreatePanel.SetActive(mode == Mode.Creating);
            if (ConfirmPanel != null) ConfirmPanel.SetActive(mode == Mode.Confirming);
        }

        /// <summary>
        /// Steps back one sub-mode, to the list.
        ///
        /// It does NOT hide itself when already at the list. Which panel is on
        /// screen is TitleScreen's to decide -- two things setting the same
        /// GameObject active is exactly the bug TitleScreen's enum exists to
        /// prevent.
        /// </summary>
        public void Back()
        {
            if (_mode != Mode.List)
            {
                Show(Mode.List);
            }
        }

        // --- making one ---

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
        /// Creates the character and goes straight into the world with it --
        /// which is what somebody who just typed a name wants, rather than
        /// being returned to a list to find and click the thing they made.
        /// </summary>
        public CharacterSave ConfirmCreate()
        {
            string name = NameField != null ? NameField.text : string.Empty;
            CharacterSave created = CharacterSave.Create(name, _pendingSkin);
            created.SetPaint(PendingPaint);
            created.HairStyle = string.IsNullOrEmpty(PendingHair) ? "none" : PendingHair;
            PendingPaint = null;
            PendingHair = "none";
            _characters = CharacterStore.Add(created);
            BuildRows();
            Play(created, isNew: true);
            return created;
        }

        // --- deleting one ---

        public void BeginDelete(string id)
        {
            _pendingDeleteId = id;
            CharacterSave character = _characters.Find(c => c.Id == id);
            if (ConfirmText != null)
            {
                ConfirmText.text = character == null
                    ? "Delete this character?"
                    : $"Delete {character.Name}, level {character.Level}?\nThis cannot be undone.";
            }
            Show(Mode.Confirming);
        }

        public void ConfirmDelete()
        {
            if (!string.IsNullOrEmpty(_pendingDeleteId))
            {
                _characters = CharacterStore.Delete(_pendingDeleteId);
                _pendingDeleteId = null;
                BuildRows();
            }
            Show(Mode.List);
        }

        public void CancelDelete()
        {
            _pendingDeleteId = null;
            Show(Mode.List);
        }

        // --- playing one ---

        public void Play(CharacterSave character, bool isNew)
        {
            if (character == null)
            {
                return;
            }

            PlaySession.Begin(character, isNew);

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

        // --- the rows ---

        private void BuildRows()
        {
            foreach (GameObject row in _rows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }
            _rows.Clear();

            if (RowTemplate == null || RowParent == null)
            {
                return;
            }

            foreach (CharacterSave character in _characters)
            {
                GameObject row = Instantiate(RowTemplate, RowParent);
                row.name = $"Row_{character.Id}";
                row.SetActive(true);
                Bind(row, character);
                _rows.Add(row);
            }

            if (EmptyNotice != null)
            {
                EmptyNotice.gameObject.SetActive(_characters.Count == 0);
            }
        }

        /// <summary>
        /// Fills one row and wires its two buttons.
        ///
        /// Found by name rather than by a per-row component, because the row is
        /// built in code a few lines away in CharacterSelectBuilder and a
        /// component for three labels would be more indirection than the thing
        /// it describes.
        /// </summary>
        private void Bind(GameObject row, CharacterSave character)
        {
            SetText(row, "Name", character.Name);
            SetText(row, "Detail", $"Level {character.Level}   {character.PlaytimeText()}");

            Transform portrait = row.transform.Find("Portrait");
            if (portrait != null)
            {
                var image = portrait.GetComponent<Image>();
                if (image != null)
                {
                    image.color = character.Skin;
                }
            }

            string id = character.Id;
            CharacterSave captured = character;

            Transform playButton = row.transform.Find("PlayButton");
            if (playButton != null)
            {
                var button = playButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => Play(captured, isNew: false));
                }
            }

            Transform deleteButton = row.transform.Find("DeleteButton");
            if (deleteButton != null)
            {
                var button = deleteButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => BeginDelete(id));
                }
            }
        }

        private static void SetText(GameObject row, string child, string value)
        {
            Transform found = row.transform.Find(child);
            if (found == null)
            {
                return;
            }
            var label = found.GetComponent<Text>();
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
