using CityX.Character;
using CityX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the Stats panel into the open scene -- Level, an XP bar, how
    /// many points are left to spend, and the list of every stat (built by StatsPanelView
    /// itself when it first opens). Reached from the Player submenu.
    ///
    /// STRAIGHT INTO THE SCENE, not a prefab -- same reasoning as
    /// LoadoutPanelBuilder: wired to the player's own PlayerVitals and
    /// input/camera components, none of which exist outside a scene.
    ///
    /// EVERY LISTENER StatsPanelView OWNS IS WIRED IN ITS OWN Awake, not
    /// here -- see PauseMenuView's doc comment for why an editor-time
    /// AddListener never survives the scene being saved.
    /// </summary>
    public static class StatsPanelBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color BackgroundColour = new Color(0.05f, 0.055f, 0.07f, 0.96f);
        private static readonly Color BorderColour = new Color(0.55f, 0.63f, 1.0f, 0.5f);
        private static readonly Color LabelColour = new Color(0.72f, 0.79f, 1.0f);
        private static readonly Color HintColour = new Color(0.55f, 0.58f, 0.66f);
        private static readonly Color BarWellColour = new Color(0.14f, 0.15f, 0.19f, 1.0f);
        private static readonly Color BarFillColour = new Color(1.0f, 0.78f, 0.28f, 1.0f);
        private static readonly Color CloseColour = new Color(0.20f, 0.21f, 0.27f, 1.0f);

        private const float PanelMargin = 60.0f;
        private const string TitleText = "STATS";
        private const float BarWidth = 420.0f;
        private const float BarHeight = 22.0f;

        public static StatsPanelView AddToScene(PlayerVitals vitals,
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera)
        {
            var root = new GameObject("StatsPanel", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Same tier as Loadout/Items -- Stats is reached from the same
            // submenu and never opens at once with either.
            canvas.sortingOrder = 55;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            StatsPanelView view = root.AddComponent<StatsPanelView>();
            view.Vitals = vitals;
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
            PlaceTopLeft(title.transform, new Vector2(24.0f, -16.0f), new Vector2(300.0f, 30.0f));
            title.alignment = TextAnchor.UpperLeft;

            Text level = UiKit.MakeText(panel.transform, "Level", string.Empty, 18, LabelColour);
            PlaceTopLeft(level.transform, new Vector2(24.0f, -58.0f), new Vector2(300.0f, 24.0f));
            level.alignment = TextAnchor.UpperLeft;
            view.LevelLabel = level;

            view.ExperienceBarFill = BuildBar(panel.transform, new Vector2(24.0f, -92.0f),
                new Vector2(BarWidth, BarHeight));

            Text xp = UiKit.MakeText(panel.transform, "ExperienceLabel", string.Empty, 13, HintColour);
            PlaceTopLeft(xp.transform, new Vector2(24.0f, -120.0f), new Vector2(BarWidth, 18.0f));
            xp.alignment = TextAnchor.UpperLeft;
            view.ExperienceLabel = xp;

            Text points = UiKit.MakeText(panel.transform, "PointsLabel", string.Empty, 15, LabelColour);
            PlaceTopLeft(points.transform, new Vector2(24.0f, -148.0f), new Vector2(300.0f, 22.0f));
            points.alignment = TextAnchor.UpperLeft;
            view.PointsLabel = points;

            // Every stat's row is built by StatsPanelView itself when the page first opens (2026-10-14: twenty-four stats in a list that scrolls).

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
        /// A background well plus a fill child whose anchors StatsPanelView
        /// moves at refresh time -- the same technique PlayerHudBuilder's own
        /// BuildBar/PlayerHud.SetFill pair uses for the HUD's health/stamina
        /// bars, reused here rather than Image.Type.Filled so both bars in
        /// this project scale the same way under the canvas scaler.
        /// </summary>
        private static RectTransform BuildBar(Transform parent, Vector2 position, Vector2 size)
        {
            var well = new GameObject("ExperienceBar", typeof(RectTransform));
            well.transform.SetParent(parent, false);
            PlaceTopLeft(well.transform, position, size);
            well.AddComponent<Image>().color = BarWellColour;

            var fill = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(well.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.0f, 1.0f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = BarFillColour;

            return fillRect;
        }

        private static void PlaceTopLeft(Transform t, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)t;
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
