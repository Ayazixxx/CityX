using CityX.Character;
using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the C-menu: a vertical rail of 4 circular icon buttons
    /// (Quests, Player, Social, Map) and the Player button's own 5-item
    /// fly-out beside it. Matches the two reference mockups the feature was
    /// specified from -- the rail is left-of-centre, and the fly-out opens
    /// immediately to its right rather than replacing it.
    ///
    /// STRAIGHT INTO THE SCENE, not a prefab -- same reasoning as every
    /// other panel builder in this project: it is wired to the player's own
    /// components, none of which exist outside a scene.
    ///
    /// ALL LISTENERS ARE WIRED IN PlayerMenuView/PlayerSubmenuView'S OWN
    /// Awake, not here -- see InventoryPanelBuilder's own doc comment.
    /// </summary>
    public static class PlayerMenuBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);

        private static readonly Color RailButtonColour = new Color(1.0f, 1.0f, 1.0f, 0.92f);
        private static readonly Color IconColour = new Color(0.16f, 0.19f, 0.26f, 1.0f);
        private static readonly Color FlyoutBackground = new Color(0.98f, 0.98f, 0.99f, 0.97f);
        private static readonly Color FlyoutRowColour = new Color(0.90f, 0.91f, 0.94f, 1.0f);
        private static readonly Color FlyoutRowSelected = new Color(1.0f, 0.78f, 0.28f, 1.0f);
        private static readonly Color FlyoutTextColour = new Color(0.14f, 0.16f, 0.22f, 1.0f);

        private const float RailButtonSize = 60.0f;
        private const float RailPitch = 76.0f;

        private const float FlyoutWidth = 220.0f;
        private const float FlyoutRowHeight = 52.0f;
        private const float FlyoutRowPitch = 58.0f;

        public static PlayerMenuView AddToScene(GameObject player, Transform cameraTransform,
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera,
            DevConsoleView console, PauseMenuView pause, MapPanelView map,
            InventoryPanelView inventory, PlayerChat chat,
            LoadoutPanelView loadout, StatsPanelView stats, PlaceholderPanelView placeholder,
            PlayerRedrawStarter redraw)
        {
            var root = new GameObject("PlayerMenu", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD/dialogue (40/45), below the map/inventory/console/
            // pause stack it yields to (50/55/60/80) -- see PlayerMenuView's
            // own gate list.
            canvas.sortingOrder = 42;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            PlayerMenuView view = root.AddComponent<PlayerMenuView>();
            view.Player = player.transform;
            view.CameraTransform = cameraTransform;
            view.InputReader = inputReader;
            view.OrbitCamera = orbitCamera;
            view.Console = console;
            view.Pause = pause;
            view.Map = map;
            view.Inventory = inventory;
            view.Chat = chat;
            view.Placeholder = placeholder;
            view.MenuCamera = player.AddComponent<PlayerMenuCamera>();

            // A SEPARATE CHILD FROM root, not root itself. PlayerMenuView's
            // own Update() reads C directly to reopen itself -- if Root were
            // root (the object the view itself lives on), Close() disabling
            // it would also disable the very Update() loop watching for C to
            // ever bring it back. Same shape DevConsole/PauseMenu/PlayerChat
            // already use; see their own builders for the same reasoning.
            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(root.transform, false);
            Stretch(contentRect);

            var rail = new GameObject("Rail", typeof(RectTransform));
            var railRect = (RectTransform)rail.transform;
            railRect.SetParent(content.transform, false);
            railRect.anchorMin = new Vector2(0.0f, 0.5f);
            railRect.anchorMax = new Vector2(0.0f, 0.5f);
            railRect.pivot = new Vector2(0.5f, 0.5f);
            railRect.anchoredPosition = new Vector2(90.0f, 0.0f);
            railRect.sizeDelta = new Vector2(RailButtonSize, RailPitch * 4.0f);

            string[] kinds = { "exclamation", "person", "people", "pin" };
            string[] names = { "Quests", "Player", "Social", "Map" };
            var buttons = new Button[4];
            float startY = RailPitch * 1.5f;
            for (int i = 0; i < 4; i++)
            {
                buttons[i] = MakeRailButton(rail.transform, names[i], kinds[i],
                    new Vector2(0.0f, startY - i * RailPitch));
            }
            view.QuestsButton = buttons[0];
            view.PlayerButton = buttons[1];
            view.SocialButton = buttons[2];
            view.MapButton = buttons[3];

            view.Submenu = BuildSubmenu(content.transform, view, loadout, stats, inventory, placeholder, redraw);

            view.Root = content;
            EnsureEventSystem();
            return view;
        }

        private static Button MakeRailButton(Transform parent, string name, string iconKind, Vector2 position)
        {
            var go = new GameObject(name + "Button", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(RailButtonSize, RailButtonSize);

            Image background = go.AddComponent<Image>();
            background.sprite = CircleSprite.Ensure();
            background.color = RailButtonColour;
            background.type = Image.Type.Simple;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            MakeIcon(go.transform, iconKind, RailButtonSize * 0.5f);
            return button;
        }

        /// <summary>
        /// The Player button's own 5-row fly-out, opened immediately to the
        /// rail's right and hidden the rest of the time -- see
        /// PlayerSubmenuView's own doc comment for why every row here
        /// closes the whole menu rather than nesting further.
        /// </summary>
        private static PlayerSubmenuView BuildSubmenu(Transform parent, PlayerMenuView menu,
            LoadoutPanelView loadout, StatsPanelView stats, InventoryPanelView inventory,
            PlaceholderPanelView placeholder, PlayerRedrawStarter redraw)
        {
            var flyoutRoot = new GameObject("Submenu", typeof(RectTransform));
            var flyoutRect = (RectTransform)flyoutRoot.transform;
            flyoutRect.SetParent(parent, false);
            flyoutRect.anchorMin = new Vector2(0.0f, 0.5f);
            flyoutRect.anchorMax = new Vector2(0.0f, 0.5f);
            flyoutRect.pivot = new Vector2(0.0f, 0.5f);
            flyoutRect.anchoredPosition = new Vector2(140.0f, 0.0f);
            flyoutRect.sizeDelta = new Vector2(FlyoutWidth, FlyoutRowPitch * 5.0f + 24.0f);

            Image panelImage = flyoutRoot.AddComponent<Image>();
            panelImage.color = FlyoutBackground;

            PlayerSubmenuView submenu = flyoutRoot.AddComponent<PlayerSubmenuView>();
            submenu.Menu = menu;
            submenu.Loadout = loadout;
            submenu.Stats = stats;
            submenu.Items = inventory;
            submenu.Placeholder = placeholder;
            submenu.Redraw = redraw;

            string[] kinds = { "vest", "chart", "star", "bag", "brush" };
            string[] labels = { "Loadout", "Stats", "Skills", "Items", "ReDraw" };
            var rowButtons = new Button[5];
            float startY = FlyoutRowPitch * 2.0f;
            for (int i = 0; i < 5; i++)
            {
                rowButtons[i] = MakeFlyoutRow(flyoutRoot.transform, labels[i], kinds[i],
                    new Vector2(0.0f, startY - i * FlyoutRowPitch));
            }
            submenu.LoadoutButton = rowButtons[0];
            submenu.StatsButton = rowButtons[1];
            submenu.SkillsButton = rowButtons[2];
            submenu.ItemsButton = rowButtons[3];
            submenu.ReDrawButton = rowButtons[4];

            submenu.Root = flyoutRoot;
            flyoutRoot.SetActive(false);
            return submenu;
        }

        private static Button MakeFlyoutRow(Transform parent, string label, string iconKind, Vector2 position)
        {
            var go = new GameObject(label + "Row", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(FlyoutWidth - 16.0f, FlyoutRowHeight);

            Image background = go.AddComponent<Image>();
            background.color = FlyoutRowColour;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            var colours = button.colors;
            colours.highlightedColor = FlyoutRowSelected;
            colours.pressedColor = FlyoutRowSelected;
            button.colors = colours;

            var iconHolder = MakeIcon(go.transform, iconKind, 30.0f);
            var iconRect = (RectTransform)iconHolder.transform;
            iconRect.anchorMin = new Vector2(0.0f, 0.5f);
            iconRect.anchorMax = new Vector2(0.0f, 0.5f);
            iconRect.pivot = new Vector2(0.0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(10.0f, 0.0f);

            Text text = MakeText(go.transform, "Label", label, 16, FlyoutTextColour);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = new Vector2(0.0f, 0.0f);
            textRect.anchorMax = new Vector2(1.0f, 1.0f);
            textRect.offsetMin = new Vector2(50.0f, 0.0f);
            textRect.offsetMax = new Vector2(-8.0f, 0.0f);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            return button;
        }

        /// <summary>
        /// One icon image sized square inside its parent. "brush" reuses
        /// ToolIcons directly rather than MenuIcons -- see MenuIcons' own
        /// doc comment on why ReDraw's icon is the exception.
        /// </summary>
        private static Image MakeIcon(Transform parent, string kind, float size)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);

            Texture2D texture = kind == "brush" ? ToolIcons.Build("brush") : MenuIcons.Build(kind);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width);

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = IconColour;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static Text MakeText(Transform parent, string name, string content, int size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Without one of these no button on this panel ever receives a click -- see MapPanelBuilder's own copy of this note.</summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }
    }
}
