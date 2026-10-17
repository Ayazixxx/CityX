using CityX.Character;
using CityX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the inventory panel into the open scene. Ported from
    /// ui/inventory_panel.gd's own Inventory tab -- see InventoryPanelView's
    /// own doc comment for why the Stats/Skills tabs it also carries are
    /// not part of this pass.
    ///
    /// STRAIGHT INTO THE SCENE, not a prefab -- same reasoning as
    /// DevConsoleBuilder, PauseMenuBuilder and MapPanelBuilder: it is wired
    /// to the player's own Inventory, the input reader and the camera,
    /// none of which exist outside a scene.
    ///
    /// EVERY LISTENER InventoryPanelView OWNS IS WIRED IN ITS OWN Awake, not
    /// here -- see PauseMenuView's doc comment for why an editor-time
    /// AddListener never survives the scene being saved.
    /// </summary>
    public static class InventoryPanelBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color BackgroundColour = new Color(0.05f, 0.055f, 0.07f, 0.96f);
        private static readonly Color BorderColour = new Color(0.55f, 0.63f, 1.0f, 0.5f);
        private static readonly Color LabelColour = new Color(0.72f, 0.79f, 1.0f);
        private static readonly Color HintColour = new Color(0.55f, 0.58f, 0.66f);
        private static readonly Color SlotColour = new Color(0.14f, 0.15f, 0.19f, 1.0f);
        private static readonly Color EquipSlotColour = new Color(0.18f, 0.17f, 0.13f, 1.0f);
        private static readonly Color SlotTextColour = new Color(0.90f, 0.90f, 0.92f);

        private const float PanelMargin = 60.0f;
        private const string TitleText = "INVENTORY";
        private const string HintText = "[I] close   ·   click a filled slot to equip or unequip it";

        private const int StorageColumns = 5;
        private const float StoragePitch = 84.0f;
        private const float StorageSlotSize = 76.0f;
        private const float EquipSlotSize = 68.0f;
        private const float EquipPitch = 84.0f;

        public static InventoryPanelView AddToScene(Inventory inventory, GameObject player,
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera, DevConsoleView console,
            PauseMenuView pause, MapPanelView map)
        {
            var root = new GameObject("InventoryPanel", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the map (50), below the dev console (60) -- the map and
            // the inventory never show at once, guarded on both sides, so
            // their relative order does not matter beyond both sitting
            // under the console.
            canvas.sortingOrder = 55;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            InventoryPanelView view = root.AddComponent<InventoryPanelView>();
            view.Inventory = inventory;
            view.Player = player;
            view.InputReader = inputReader;
            view.OrbitCamera = orbitCamera;
            view.Console = console;
            view.Pause = pause;
            view.Map = map;

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
            hintRect.anchorMin = new Vector2(1.0f, 0.0f);
            hintRect.anchorMax = new Vector2(1.0f, 0.0f);
            hintRect.pivot = new Vector2(1.0f, 0.0f);
            hintRect.anchoredPosition = new Vector2(-24.0f, 16.0f);
            hintRect.sizeDelta = new Vector2(460.0f, 20.0f);
            hint.alignment = TextAnchor.LowerRight;

            Text equipLabel = UiKit.MakeText(panel.transform, "EquipLabel", "EQUIPPED", 14, LabelColour);
            var equipLabelRect = (RectTransform)equipLabel.transform;
            equipLabelRect.anchorMin = new Vector2(0.0f, 1.0f);
            equipLabelRect.anchorMax = new Vector2(0.0f, 1.0f);
            equipLabelRect.pivot = new Vector2(0.0f, 1.0f);
            equipLabelRect.anchoredPosition = new Vector2(24.0f, -56.0f);
            equipLabelRect.sizeDelta = new Vector2(300.0f, 20.0f);
            equipLabel.alignment = TextAnchor.UpperLeft;

            BuildEquipRow(panel.transform, view);

            Text storageLabel = UiKit.MakeText(panel.transform, "StorageLabel", "STORAGE", 14, LabelColour);
            var storageLabelRect = (RectTransform)storageLabel.transform;
            storageLabelRect.anchorMin = new Vector2(0.0f, 1.0f);
            storageLabelRect.anchorMax = new Vector2(0.0f, 1.0f);
            storageLabelRect.pivot = new Vector2(0.0f, 1.0f);
            storageLabelRect.anchoredPosition = new Vector2(24.0f, -150.0f);   // between the equipped row (to -144) and the grid (from -176)
            storageLabelRect.sizeDelta = new Vector2(300.0f, 20.0f);
            storageLabel.alignment = TextAnchor.UpperLeft;

            BuildStorageGrid(panel.transform, view);

            // THE PANEL, NOT THE CANVAS ROOT -- InventoryPanelView's own
            // Update() reads I directly to reopen itself; if Root pointed at
            // root (the object the view itself lives on), Close() disabling
            // it would also disable the very Update() loop watching for I to
            // ever bring it back. Same shape DevConsole/PauseMenu/PlayerChat
            // already use.
            view.Root = panel;
            UiKit.EnsureEventSystem();
            return view;
        }

        /// <summary>
        /// One row of InventoryPanelView.EquipSlotKeys.Length slots, each captioned with what it holds -- HELMET, TORSO, SHOES, BACK and so on
        /// (InventoryPanelView.SlotLabelText) -- since some of them have no item yet and a slot with no caption would read as decoration rather
        /// than as "nothing equipped here yet."
        /// </summary>
        private static void BuildEquipRow(Transform parent, InventoryPanelView view)
        {
            string[] keys = InventoryPanelView.EquipSlotKeys;
            var buttons = new Button[keys.Length];
            var labels = new Text[keys.Length];

            float totalWidth = (keys.Length - 1) * EquipPitch;
            float startX = 24.0f + EquipSlotSize * 0.5f;

            for (int i = 0; i < keys.Length; i++)
            {
                var go = new GameObject("Equip_" + keys[i], typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.0f, 1.0f);
                rect.anchorMax = new Vector2(0.0f, 1.0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + i * EquipPitch, -110.0f);
                rect.sizeDelta = new Vector2(EquipSlotSize, EquipSlotSize);

                Image background = go.AddComponent<Image>();
                background.color = EquipSlotColour;

                Button button = go.AddComponent<Button>();
                button.targetGraphic = background;

                Text keyCaption = UiKit.MakeText(go.transform, "Key", InventoryPanelView.SlotLabelText[i], 9, HintColour);
                var keyRect = (RectTransform)keyCaption.transform;
                keyRect.anchorMin = new Vector2(0.0f, 1.0f);
                keyRect.anchorMax = new Vector2(1.0f, 1.0f);
                keyRect.pivot = new Vector2(0.5f, 1.0f);
                keyRect.anchoredPosition = new Vector2(0.0f, -4.0f);
                keyRect.sizeDelta = new Vector2(0.0f, 12.0f);
                keyCaption.alignment = TextAnchor.UpperCenter;
                keyCaption.raycastTarget = false;

                Text itemCaption = UiKit.MakeText(go.transform, "Item", "--", 11, SlotTextColour);
                var itemRect = (RectTransform)itemCaption.transform;
                itemRect.anchorMin = Vector2.zero;
                itemRect.anchorMax = Vector2.one;
                itemRect.offsetMin = new Vector2(4.0f, 6.0f);
                itemRect.offsetMax = new Vector2(-4.0f, -14.0f);
                itemCaption.alignment = TextAnchor.MiddleCenter;
                itemCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
                itemCaption.raycastTarget = false;

                buttons[i] = button;
                labels[i] = itemCaption;
            }

            view.EquipButtons = buttons;
            view.EquipLabels = labels;
        }

        /// <summary>
        /// Inventory.StorageSlotCount slots in a fixed-column grid, each a
        /// plain button whose own label InventoryPanelView.Refresh fills
        /// in -- empty slots stay blank rather than showing a placeholder
        /// dash, so a glance at the grid separates "used" from "free" at
        /// once, the same reading item_slot.gd's own empty-icon path gives.
        /// </summary>
        private static void BuildStorageGrid(Transform parent, InventoryPanelView view)
        {
            int count = Inventory.StorageSlotCount;
            var buttons = new Button[count];
            var labels = new Text[count];

            float startX = 24.0f + StorageSlotSize * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int column = i % StorageColumns;
                int row = i / StorageColumns;

                var go = new GameObject("Storage_" + i, typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.0f, 1.0f);
                rect.anchorMax = new Vector2(0.0f, 1.0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(
                    startX + column * StoragePitch, -214.0f - row * StoragePitch);
                rect.sizeDelta = new Vector2(StorageSlotSize, StorageSlotSize);

                Image background = go.AddComponent<Image>();
                background.color = SlotColour;

                Button button = go.AddComponent<Button>();
                button.targetGraphic = background;

                Text caption = UiKit.MakeText(go.transform, "Label", string.Empty, 11, SlotTextColour);
                var captionRect = (RectTransform)caption.transform;
                captionRect.anchorMin = Vector2.zero;
                captionRect.anchorMax = Vector2.one;
                captionRect.offsetMin = new Vector2(4.0f, 4.0f);
                captionRect.offsetMax = new Vector2(-4.0f, -4.0f);
                caption.alignment = TextAnchor.MiddleCenter;
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
                caption.raycastTarget = false;

                buttons[i] = button;
                labels[i] = caption;
            }

            view.StorageButtons = buttons;
            view.StorageLabels = labels;
        }
    }
}
