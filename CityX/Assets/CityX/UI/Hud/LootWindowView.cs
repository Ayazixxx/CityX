using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE LOOT WINDOW (docs/LOOTING.md): what is in the drawer, cupboard or chest this machine's player has opened (LootSearcher), Skyrim's
    /// container menu in spirit -- the container's name, its items one to a row with the picked one marked, and the keys. Built in code on its own
    /// canvas, hidden until something is opened. A click on a row takes that item.
    /// </summary>
    public sealed class LootWindowView : MonoBehaviour
    {
        public const int MaxRows = 10;

        private static readonly Color Back = new Color(0.06f, 0.06f, 0.08f, 0.92f);
        private static readonly Color Title = new Color(0.95f, 0.88f, 0.70f);
        private static readonly Color Row = new Color(0.86f, 0.88f, 0.92f);
        private static readonly Color Picked = new Color(1.0f, 0.82f, 0.35f);
        private static readonly Color Hint = new Color(0.60f, 0.62f, 0.68f);
        private static readonly Color RowBack = new Color(1.0f, 1.0f, 1.0f, 0.06f);
        private static readonly Color PickedBack = new Color(1.0f, 0.82f, 0.35f, 0.18f);

        private GameObject _panel;
        private Text _title;
        private Text _hint;
        private readonly Text[] _rows = new Text[MaxRows];
        private readonly Image[] _rowBacks = new Image[MaxRows];
        private LootSearcher _searcher;
        private float _nextLook;

        public bool Showing => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            var canvasObject = new GameObject("LootWindow", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1152.0f, 648.0f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel", typeof(RectTransform));
            var panel = (RectTransform)_panel.transform;
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.72f, 0.5f);
            panel.sizeDelta = new Vector2(320.0f, 64.0f + MaxRows * 24.0f);
            _panel.AddComponent<Image>().color = Back;

            _title = UiKit.MakeText(panel, "Title", "", 18, Title);
            Place((RectTransform)_title.transform, 12.0f, 26.0f);
            _title.fontStyle = FontStyle.Bold;
            for (int i = 0; i < MaxRows; i++)
            {
                var rowObject = new GameObject("Row" + i, typeof(RectTransform));
                var rect = (RectTransform)rowObject.transform;
                rect.SetParent(panel, false);
                rect.anchorMin = new Vector2(0.0f, 1.0f);
                rect.anchorMax = new Vector2(1.0f, 1.0f);
                rect.pivot = new Vector2(0.5f, 1.0f);
                rect.offsetMin = new Vector2(8.0f, -(44.0f + (i + 1) * 24.0f));
                rect.offsetMax = new Vector2(-8.0f, -(44.0f + i * 24.0f));
                _rowBacks[i] = rowObject.AddComponent<Image>();
                int index = i;
                rowObject.AddComponent<Button>().onClick.AddListener(() => _searcher?.TakeAt(index));
                _rows[i] = UiKit.MakeText(rect, "Name", "", 15, Row);
                UiKit.Stretch((RectTransform)_rows[i].transform);
                ((RectTransform)_rows[i].transform).offsetMin = new Vector2(8.0f, 0.0f);
                _rows[i].alignment = TextAnchor.MiddleLeft;
                _rows[i].raycastTarget = false;
            }
            _hint = UiKit.MakeText(panel, "Hint", "[E] take   [R] take all   [Tab] close", 12, Hint);
            var hint = (RectTransform)_hint.transform;
            hint.anchorMin = new Vector2(0.0f, 0.0f);
            hint.anchorMax = new Vector2(1.0f, 0.0f);
            hint.pivot = new Vector2(0.5f, 0.0f);
            hint.offsetMin = new Vector2(12.0f, 8.0f);
            hint.offsetMax = new Vector2(-12.0f, 26.0f);
            _hint.alignment = TextAnchor.MiddleLeft;
            _panel.SetActive(false);
            UiKit.EnsureEventSystem();
        }

        private static void Place(RectTransform rect, float left, float height)
        {
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(1.0f, 1.0f);
            rect.pivot = new Vector2(0.5f, 1.0f);
            rect.offsetMin = new Vector2(left, -10.0f - height);
            rect.offsetMax = new Vector2(-left, -10.0f);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextLook && (_searcher == null || _searcher.Current == null))
            {
                _nextLook = Time.unscaledTime + 0.5f;
                _searcher = LocalPlayerBody.Find<LootSearcher>();   // this machine's body, which changes when a friend's clone is claimed
            }
            bool show = _searcher != null && _searcher.Current != null;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            LootList list = _searcher.List;
            _title.text = _searcher.Current.Label;
            // As tall as what is in it (one row at least, for "Empty").
            ((RectTransform)_panel.transform).sizeDelta = new Vector2(320.0f, 76.0f + Mathf.Clamp(list.Items.Count, 1, MaxRows) * 24.0f);
            for (int i = 0; i < MaxRows; i++)
            {
                bool has = i < list.Items.Count;
                _rowBacks[i].gameObject.SetActive(has || (i == 0 && list.IsEmpty));
                if (has)
                {
                    bool picked = i == list.Selected;
                    _rows[i].text = ItemRegistry.TryGet(list.Items[i], out ItemDefinition definition) ? definition.DisplayName : list.Items[i];
                    _rows[i].color = picked ? Picked : Row;
                    _rowBacks[i].color = picked ? PickedBack : RowBack;
                }
                else if (i == 0)
                {
                    _rows[0].text = "Empty";
                    _rows[0].color = Hint;
                    _rowBacks[0].color = RowBack;
                }
            }
        }
    }
}
