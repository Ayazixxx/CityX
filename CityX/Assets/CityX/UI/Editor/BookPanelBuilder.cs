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
    /// Builds the shared book-reading overlay -- see BookPanelView's own
    /// doc comment; this is PlaceholderPanelBuilder's own structure, just a
    /// bigger panel with a word-wrapped, top-aligned body for a real page
    /// of text instead of one short placeholder line.
    /// </summary>
    public static class BookPanelBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color DimColour = new Color(0.0f, 0.0f, 0.0f, 0.55f);
        private static readonly Color PanelColour = new Color(0.09f, 0.08f, 0.07f, 0.97f);
        private static readonly Color BorderColour = new Color(0.75f, 0.65f, 0.45f, 0.5f);
        private static readonly Color TitleColour = new Color(0.94f, 0.90f, 0.82f);
        private static readonly Color BodyColour = new Color(0.85f, 0.82f, 0.74f);
        private static readonly Color CloseColour = new Color(0.22f, 0.19f, 0.15f, 1.0f);

        private const float PanelWidth = 640.0f;
        private const float PanelHeight = 480.0f;

        public static BookPanelView AddToScene(
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera)
        {
            var root = new GameObject("BookPanel", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Same tier as the Placeholder/Map/Inventory panels -- a book
            // is read after closing whatever else was open, never over it.
            canvas.sortingOrder = 56;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            BookPanelView view = root.AddComponent<BookPanelView>();
            view.InputReader = inputReader;
            view.OrbitCamera = orbitCamera;

            var dim = new GameObject("Dim", typeof(RectTransform));
            var dimRect = (RectTransform)dim.transform;
            dimRect.SetParent(root.transform, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = DimColour;

            var panel = new GameObject("Panel", typeof(RectTransform));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(dim.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.AddComponent<Image>().color = PanelColour;
            var border = panel.AddComponent<Outline>();
            border.effectColor = BorderColour;
            border.effectDistance = new Vector2(2.0f, -2.0f);

            Text title = UiKit.MakeText(panel.transform, "Title", "", 26, TitleColour);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.0f, 1.0f);
            titleRect.anchorMax = new Vector2(1.0f, 1.0f);
            titleRect.pivot = new Vector2(0.5f, 1.0f);
            titleRect.anchoredPosition = new Vector2(0.0f, -28.0f);
            titleRect.sizeDelta = new Vector2(-64.0f, 36.0f);
            title.alignment = TextAnchor.UpperCenter;
            title.fontStyle = FontStyle.Bold;

            // A REAL PAGE OF TEXT, NOT ONE LINE -- top-anchored and word-
            // wrapped, filling the whole middle of the panel between the
            // title and the close button rather than PlaceholderPanelView's
            // own fixed 40px-tall single-line message rect.
            Text body = UiKit.MakeText(panel.transform, "Body", "", 17, BodyColour);
            var bodyRect = (RectTransform)body.transform;
            bodyRect.anchorMin = new Vector2(0.0f, 0.0f);
            bodyRect.anchorMax = new Vector2(1.0f, 1.0f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(32.0f, 64.0f);
            bodyRect.offsetMax = new Vector2(-32.0f, -76.0f);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;

            var closeGo = new GameObject("CloseButton", typeof(RectTransform));
            var closeRect = (RectTransform)closeGo.transform;
            closeRect.SetParent(panel.transform, false);
            closeRect.anchorMin = new Vector2(0.5f, 0.0f);
            closeRect.anchorMax = new Vector2(0.5f, 0.0f);
            closeRect.pivot = new Vector2(0.5f, 0.0f);
            closeRect.anchoredPosition = new Vector2(0.0f, 20.0f);
            closeRect.sizeDelta = new Vector2(130.0f, 36.0f);
            Image closeBackground = closeGo.AddComponent<Image>();
            closeBackground.color = CloseColour;
            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeBackground;
            Text closeLabel = UiKit.MakeText(closeGo.transform, "Label", "Close (Esc)", 15, TitleColour);
            var closeLabelRect = (RectTransform)closeLabel.transform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.raycastTarget = false;

            view.TitleLabel = title;
            view.BodyLabel = body;
            view.CloseButton = closeButton;

            view.Root = root;
            root.SetActive(false);
            UiKit.EnsureEventSystem();
            return view;
        }
    }
}
