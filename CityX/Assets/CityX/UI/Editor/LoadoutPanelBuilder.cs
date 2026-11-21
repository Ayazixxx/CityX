using CityX.Character;
using CityX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the Loadout panel into the open scene -- an 8-slot equip
    /// grid (Helmet, Torso, Legs, Shoes, Gloves, Back, Primary Weapon,
    /// Secondary Weapon), reached from the Player submenu. See
    /// LoadoutPanelView's own doc comment for why there is no storage grid
    /// here the way InventoryPanelBuilder's own equip row has one beside it.
    ///
    /// STRAIGHT INTO THE SCENE, not a prefab -- same reasoning as
    /// InventoryPanelBuilder: wired to the player's own Inventory and
    /// input/camera components, none of which exist outside a scene.
    ///
    /// EVERY LISTENER LoadoutPanelView OWNS IS WIRED IN ITS OWN Awake, not
    /// here -- see PauseMenuView's doc comment for why an editor-time
    /// AddListener never survives the scene being saved.
    /// </summary>
    public static class LoadoutPanelBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color BackgroundColour = new Color(0.05f, 0.055f, 0.07f, 0.96f);
        private static readonly Color BorderColour = new Color(0.55f, 0.63f, 1.0f, 0.5f);
        private static readonly Color LabelColour = new Color(0.72f, 0.79f, 1.0f);
        private static readonly Color HintColour = new Color(0.55f, 0.58f, 0.66f);
        private static readonly Color SlotColour = new Color(0.18f, 0.17f, 0.13f, 1.0f);
        private static readonly Color SlotTextColour = new Color(0.90f, 0.90f, 0.92f);
        private static readonly Color CloseColour = new Color(0.20f, 0.21f, 0.27f, 1.0f);

        private const float PanelMargin = 60.0f;
        private const string TitleText = "LOADOUT";
        private const string HintText = "click a filled slot to unequip it   ·   equip new items from Items";

        private const int Columns = 4;
        private const float SlotWidth = 220.0f;
        private const float SlotHeight = 130.0f;
        private const float ColumnPitch = 240.0f;
        private const float RowPitch = 160.0f;

        public static LoadoutPanelView AddToScene(Inventory inventory, GameObject player,
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera)
        {
            var root = new GameObject("LoadoutPanel", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Same tier as the inventory panel it stands beside -- Loadout
            // and Items are reached from the same submenu and never open
            // at once (both close the C-menu first), so their relative
            // order does not matter beyond both sitting under the console.
            canvas.sortingOrder = 55;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            LoadoutPanelView view = root.AddComponent<LoadoutPanelView>();
            view.Inventory = inventory;
            view.Player = player;
            view.InputReader = inputReader;
            view.OrbitCamera = orbitCamera;

            var panel = new GameObject("Panel", typeof(RectTransform));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(root.transform, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(PanelMargin, PanelMargin);
            panelRect.offsetMax = new Vector2(-PanelMargin, -PanelMargin);
            panel.AddComponent<Image>().color = BackgroundColour;
            var border = panel.AddComponent<Outline>();
            border.effectColor = BorderColour;
            border.effectDistance = new Vector2(2.0f, -2.0f);

            Text title = UiKit.MakeText(panel.transform, "Title", TitleText, 22, LabelColour);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.0f, 1.0f);
            titleRect.anchorMax = new Vector2(0.0f, 1.0f);
            titleRect.pivot = new Vector2(0.0f, 1.0f);
            titleRect.anchoredPosition = new Vector2(24.0f, -16.0f);
            titleRect.sizeDelta = new Vector2(300.0f, 30.0f);
            title.alignment = TextAnchor.UpperLeft;

            Text hint = UiKit.MakeText(panel.transform, "Hint", HintText, 13, HintColour);
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(1.0f, 1.0f);
            hintRect.anchorMax = new Vector2(1.0f, 1.0f);
            hintRect.pivot = new Vector2(1.0f, 1.0f);
            hintRect.anchoredPosition = new Vector2(-24.0f, -22.0f);
            hintRect.sizeDelta = new Vector2(460.0f, 20.0f);
            hint.alignment = TextAnchor.UpperRight;

            BuildSlotGrid(panel.transform, view);

            var closeGo = new GameObject("CloseButton", typeof(RectTransform));
            var closeRect = (RectTransform)closeGo.transform;
            closeRect.SetParent(panel.transform, false);
            closeRect.anchorMin = new Vector2(0.5f, 0.0f);
            closeRect.anchorMax = new Vector2(0.5f, 0.0f);
            closeRect.pivot = new Vector2(0.5f, 0.0f);
            closeRect.anchoredPosition = new Vector2(0.0f, 20.0f);
            closeRect.sizeDelta = new Vector2(140.0f, 36.0f);
            Image closeBackground = closeGo.AddComponent<Image>();
            closeBackground.color = CloseColour;
            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeBackground;
            Text closeLabel = UiKit.MakeText(closeGo.transform, "Label", "Close", 15, LabelColour);
            var closeLabelRect = (RectTransform)closeLabel.transform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.raycastTarget = false;
            view.CloseButton = closeButton;

            view.Root = root;
            root.SetActive(false);
            UiKit.EnsureEventSystem();
            return view;
        }

        /// <summary>
        /// LoadoutPanelView.SlotKeys.Length slots in a fixed-column grid --
        /// helmet/mask/torso/legs on the first row, shoes/gloves/back/
        /// primary weapon on the second, secondary weapon and shield on the third, each labelled with its own
        /// friendly name (LoadoutPanelView.SlotLabelText) rather than the
        /// raw Inventory slot key.
        /// </summary>
        private static void BuildSlotGrid(Transform parent, LoadoutPanelView view)
        {
            string[] keys = LoadoutPanelView.SlotKeys;
            string[] labelText = LoadoutPanelView.SlotLabelText;
            var buttons = new Button[keys.Length];
            var labels = new Text[keys.Length];

            float startX = 24.0f + SlotWidth * 0.5f;
            float startY = -80.0f - SlotHeight * 0.5f;

            for (int i = 0; i < keys.Length; i++)
            {
                int column = i % Columns;
                int row = i / Columns;

                var go = new GameObject("Slot_" + keys[i], typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.0f, 1.0f);
                rect.anchorMax = new Vector2(0.0f, 1.0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(
                    startX + column * ColumnPitch, startY - row * RowPitch);
                rect.sizeDelta = new Vector2(SlotWidth, SlotHeight);

                Image background = go.AddComponent<Image>();
                background.color = SlotColour;

                Button button = go.AddComponent<Button>();
                button.targetGraphic = background;

                Text keyCaption = UiKit.MakeText(go.transform, "Key", labelText[i], 13, HintColour);
                var keyRect = (RectTransform)keyCaption.transform;
                keyRect.anchorMin = new Vector2(0.0f, 1.0f);
                keyRect.anchorMax = new Vector2(1.0f, 1.0f);
                keyRect.pivot = new Vector2(0.5f, 1.0f);
                keyRect.anchoredPosition = new Vector2(0.0f, -10.0f);
                keyRect.sizeDelta = new Vector2(-12.0f, 18.0f);
                keyCaption.alignment = TextAnchor.UpperCenter;
                keyCaption.raycastTarget = false;

                Text itemCaption = UiKit.MakeText(go.transform, "Item", "--", 15, SlotTextColour);
                var itemRect = (RectTransform)itemCaption.transform;
                itemRect.anchorMin = Vector2.zero;
                itemRect.anchorMax = Vector2.one;
                itemRect.offsetMin = new Vector2(8.0f, 10.0f);
                itemRect.offsetMax = new Vector2(-8.0f, -30.0f);
                itemCaption.alignment = TextAnchor.MiddleCenter;
                itemCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
                itemCaption.raycastTarget = false;

                buttons[i] = button;
                labels[i] = itemCaption;
            }

            view.SlotButtons = buttons;
            view.SlotLabels = labels;
        }
    }
}
