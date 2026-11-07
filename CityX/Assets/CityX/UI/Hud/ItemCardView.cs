using System.Text;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE APPRAISAL CARD (docs/ITEM_INSPECT.md; the owner's Sword Art Online screenshot): a pale sheet standing in the world beside the item,
    /// facing the camera, with "Appraisal" across its top, a round mark with the item's initial (the owner draws the icons; a first letter
    /// stands in, as the hotbar's do), the NAME in its rarity's colour, "Knife / One Hand" under it, the numbers in two columns with a green
    /// or red arrow against the item in the hand, and the notes; the keys along the bottom. It unfolds from nothing as the camera glides in
    /// (ItemInspector's eased progress scales it) and folds away as it glides back. Built in code on a world-space canvas; filled from
    /// Weapons.ItemCards.
    /// </summary>
    public sealed class ItemCardView : MonoBehaviour
    {
        public const float UnitsPerMetre = 1000.0f;
        public const string Title = "Appraisal";
        public const string Keys = "[E] Take          [F] Close";
        public const string Better = "<color=#2E9E4F>▲</color>";
        public const string Worse = "<color=#D1382E>▼</color>";

        private static readonly Color Sheet = new Color(0.94f, 0.95f, 0.97f, 0.94f);
        private static readonly Color Band = new Color(0.80f, 0.82f, 0.87f, 1.0f);
        private static readonly Color Ink = new Color(0.20f, 0.21f, 0.24f);
        private static readonly Color Faint = new Color(0.36f, 0.38f, 0.43f);

        /// <summary>A line of the numbers, in canvas units, and the room left under the last before the notes begin.</summary>
        private const float RowHeight = 50.0f;

        private const float RowsTop = 340.0f;

        private const float NotesGap = 40.0f;
        private static readonly Color Mark = new Color(0.16f, 0.17f, 0.20f);

        private GameObject _sheet;
        private RectTransform _rect;
        private Text _name;
        private Text _type;
        private Text _rarity;
        private Text _initial;
        private Text _left;
        private Text _right;
        private Text _notes;
        private ItemInspector _inspector;
        private ItemCard _card;
        private ItemCard _held;
        private string _shownFor;
        private float _nextLook;

        public bool Showing => _sheet != null && _sheet.activeSelf;

        /// <summary>The card being shown (null while closed), and everything written on it, for tests.</summary>
        public ItemCard Card => _card;

        public string AllText
        {
            get
            {
                var text = new StringBuilder();
                foreach (Text t in _sheet.GetComponentsInChildren<Text>(true)) text.Append(t.text).Append('\n');
                return text.ToString();
            }
        }

        private void Awake()
        {
            _sheet = new GameObject("ItemCard", typeof(RectTransform));
            _sheet.transform.SetParent(transform, false);
            var canvas = _sheet.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            _rect = (RectTransform)_sheet.transform;
            _rect.sizeDelta = new Vector2(InspectShot.CardWidth * UnitsPerMetre, InspectShot.CardHeight * UnitsPerMetre);
            _rect.localScale = Vector3.one / UnitsPerMetre;
            _sheet.AddComponent<Image>().color = Sheet;

            float width = _rect.sizeDelta.x;
            Block("Band", 0.0f, 0.0f, width, 110.0f).gameObject.AddComponent<Image>().color = Band;
            Text title = Write("Title", Title, 52, Ink, 0.0f, 24.0f, width, 70.0f, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;

            var mark = Block("Mark", 60.0f, 145.0f, 150.0f, 150.0f).gameObject.AddComponent<Image>();
            mark.sprite = HudArt.Disc;
            mark.color = Mark;
            _initial = Write("Initial", "", 80, Sheet, 60.0f, 145.0f, 150.0f, 150.0f, TextAnchor.MiddleCenter);
            _initial.fontStyle = FontStyle.Bold;

            _name = Write("Name", "", 54, Ink, 240.0f, 140.0f, width - 270.0f, 70.0f, TextAnchor.MiddleLeft);
            _name.fontStyle = FontStyle.Bold;
            _name.resizeTextForBestFit = true;
            _name.resizeTextMinSize = 30;
            _name.resizeTextMaxSize = 54;
            _type = Write("Type", "", 36, Ink, 240.0f, 212.0f, width - 270.0f, 46.0f, TextAnchor.MiddleLeft);
            _rarity = Write("Rarity", "", 30, Faint, 240.0f, 258.0f, width - 270.0f, 40.0f, TextAnchor.MiddleLeft);

            _left = Write("Left", "", 38, Ink, 60.0f, RowsTop, 400.0f, 300.0f, TextAnchor.UpperLeft);
            _left.lineSpacing = RowHeight / 44.0f;
            _right = Write("Right", "", 38, Ink, 470.0f, RowsTop, width - 500.0f, 300.0f, TextAnchor.UpperLeft);
            _right.lineSpacing = _left.lineSpacing;
            _notes = Write("Notes", "", 34, Faint, 60.0f, RowsTop + NotesGap, width - 120.0f, 500.0f, TextAnchor.UpperLeft);
            Write("Hint", Keys, 34, Faint, 0.0f, InspectShot.CardHeight * UnitsPerMetre - 76.0f, width, 56.0f, TextAnchor.MiddleCenter);
            _sheet.SetActive(false);
        }

        /// <summary>A rectangle on the sheet, measured from its top-left corner in canvas units.</summary>
        private RectTransform Block(string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_rect, false);
            Lay(rect, x, y, width, height);
            return rect;
        }

        private static void Lay(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private Text Write(string name, string content, int size, Color colour, float x, float y, float width, float height, TextAnchor align)
        {
            Text text = UiKit.MakeText(_rect, name, content, size, colour);
            Lay((RectTransform)text.transform, x, y, width, height);
            text.alignment = align;
            text.supportRichText = true;
            return text;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextLook && (_inspector == null || !_inspector.IsOpen))
            {
                _nextLook = Time.unscaledTime + 0.5f;
                _inspector = LocalPlayerBody.Find<ItemInspector>();   // this machine's body, which changes when a friend's clone is claimed
            }
            bool show = _inspector != null && _inspector.IsOpen;
            if (_sheet.activeSelf != show) _sheet.SetActive(show);
            if (!show)
            {
                _shownFor = null;
                _card = null;
                return;
            }
            if (_shownFor != _inspector.ItemId)
            {
                Fill(_inspector.ItemId, _inspector.HeldItemId);
            }
            InspectShot shot = _inspector.Shot;
            _rect.SetPositionAndRotation(shot.CardPosition, shot.CardRotation);
            _rect.localScale = Vector3.one * (Mathf.Max(0.001f, _inspector.Eased) / UnitsPerMetre);
        }

        private void Fill(string itemId, string heldItemId)
        {
            _shownFor = itemId;
            var inventory = _inspector.GetComponent<Inventory>();
            _card = ItemCards.Describe(itemId, inventory != null ? inventory.PrefabForItem(itemId) : null);
            _held = string.IsNullOrEmpty(heldItemId) || heldItemId == itemId
                ? null
                : ItemCards.Describe(heldItemId, inventory != null ? inventory.PrefabForItem(heldItemId) : null);

            _name.text = _card.Name.ToUpperInvariant();
            _name.color = RarityLook.OnLight(_card.Rarity);
            _initial.text = _card.Name.Length > 0 ? _card.Name.Substring(0, 1).ToUpperInvariant() : "?";
            _type.text = _card.TypeLine;
            _rarity.text = ItemRarity.Word(_card.Rarity);

            var left = new StringBuilder();
            var right = new StringBuilder();
            for (int i = 0; i < _card.Rows.Count; i++)
            {
                CardRow row = _card.Rows[i];
                int against = _card.Compare(i, _held);
                string line = row.Label + ": " + row.Text + (against > 0 ? " " + Better : against < 0 ? " " + Worse : "");
                ((i & 1) == 0 ? left : right).Append(line).Append('\n');
            }
            _left.text = left.ToString();
            _right.text = right.ToString();
            // The notes follow the last line of numbers, however many there are.
            int lines = (_card.Rows.Count + 1) / 2;
            ((RectTransform)_notes.transform).anchoredPosition = new Vector2(60.0f, -(RowsTop + lines * RowHeight + NotesGap));

            var notes = new StringBuilder();
            if (!string.IsNullOrEmpty(_card.Style)) notes.Append("Style: ").Append(_card.Style).Append('\n');
            if (!string.IsNullOrEmpty(_card.ScalesWith)) notes.Append("Scales with ").Append(_card.ScalesWith).Append('\n');
            foreach (string note in _card.Notes) notes.Append(note).Append('\n');
            _notes.text = notes.ToString();
        }
    }
}
