using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE PROMPT UNDER THE CROSSHAIR (docs/ITEM_INSPECT.md): the name of the item this machine's player is looking at in reach, in its rarity's
    /// colour, and the keys -- "[E] Take  [F] Inspect" -- as Skyrim names what you look at; for a drawer or cupboard, its name and "[E] Search".
    /// Reads ItemInspector; built in code on its own canvas, hidden while nothing is looked at or the card is open.
    /// </summary>
    public sealed class ItemPromptView : MonoBehaviour
    {
        public const string ItemKeys = "[E] Take     [F] Inspect";
        public const string ContainerKeys = "[E] Search";

        private static readonly Color Keys = new Color(0.90f, 0.91f, 0.94f);
        private static readonly Color Plain = new Color(0.95f, 0.95f, 0.95f);

        private GameObject _panel;
        private Text _name;
        private Text _keys;
        private ItemInspector _inspector;
        private float _nextLook;

        public bool Showing => _panel != null && _panel.activeSelf;

        public string NameText => _name != null ? _name.text : string.Empty;

        public string KeysText => _keys != null ? _keys.text : string.Empty;

        private void Awake()
        {
            var canvasObject = new GameObject("ItemPrompt", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 44;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1152.0f, 648.0f);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = new GameObject("Panel", typeof(RectTransform));
            var panel = (RectTransform)_panel.transform;
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 1.0f);
            panel.anchoredPosition = new Vector2(0.0f, -44.0f);
            panel.sizeDelta = new Vector2(420.0f, 54.0f);

            _name = UiKit.MakeText(panel, "Name", "", 20, Plain);
            _name.fontStyle = FontStyle.Bold;
            Place((RectTransform)_name.transform, 0.0f, 28.0f);
            _keys = UiKit.MakeText(panel, "Keys", "", 16, Keys);
            Place((RectTransform)_keys.transform, 28.0f, 22.0f);
            _panel.SetActive(false);
        }

        private static void Place(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(1.0f, 1.0f);
            rect.pivot = new Vector2(0.5f, 1.0f);
            rect.offsetMin = new Vector2(0.0f, -top - height);
            rect.offsetMax = new Vector2(0.0f, -top);
            Text text = rect.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.85f);
            shadow.effectDistance = new Vector2(1.0f, -1.0f);
        }

        private void Update()
        {
            if (_inspector == null && Time.unscaledTime >= _nextLook)
            {
                _nextLook = Time.unscaledTime + 0.5f;
                foreach (ItemInspector inspector in FindObjectsByType<ItemInspector>(FindObjectsSortMode.None))
                {
                    if (inspector.GetComponent<PlayerInputReader>() != null) _inspector = inspector;
                }
            }
            bool show = _inspector != null && !_inspector.IsOpen && (_inspector.Hovered != null || _inspector.HoveredContainer != null);
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            if (_inspector.Hovered != null)
            {
                string itemId = _inspector.Hovered.ItemId;
                _name.text = ItemRegistry.TryGet(itemId, out ItemDefinition definition) ? definition.DisplayName : itemId;
                _name.color = RarityLook.OnDark(ItemRarity.Of(itemId));
                _keys.text = ItemKeys;
            }
            else
            {
                _name.text = _inspector.HoveredContainer.Label;
                _name.color = Plain;
                _keys.text = ContainerKeys;
            }
        }
    }
}
