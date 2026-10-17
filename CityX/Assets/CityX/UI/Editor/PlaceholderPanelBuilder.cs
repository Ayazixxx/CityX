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
    /// Builds the shared "Coming soon" overlay -- see PlaceholderPanelView's
    /// own doc comment for why one instance covers Quests, Social and
    /// Skills rather than three separate panels.
    /// </summary>
    public static class PlaceholderPanelBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color DimColour = new Color(0.0f, 0.0f, 0.0f, 0.55f);
        private static readonly Color PanelColour = new Color(0.07f, 0.075f, 0.10f, 0.97f);
        private static readonly Color BorderColour = new Color(0.55f, 0.63f, 1.0f, 0.5f);
        private static readonly Color TitleColour = new Color(0.90f, 0.90f, 0.94f);
        private static readonly Color MessageColour = new Color(0.68f, 0.71f, 0.80f);
        private static readonly Color CloseColour = new Color(0.20f, 0.21f, 0.27f, 1.0f);

        private const float PanelWidth = 340.0f;
        private const float PanelHeight = 180.0f;

        public static PlaceholderPanelView AddToScene(
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera)
        {
            var root = new GameObject("PlaceholderPanel", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Same tier as the map/inventory panels it stands in for --
            // Quests/Social/Skills are all reached by closing the C-menu
            // first, exactly like Map and Inventory already are.
            canvas.sortingOrder = 56;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            PlaceholderPanelView view = root.AddComponent<PlaceholderPanelView>();
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

            Text title = UiKit.MakeText(panel.transform, "Title", "", 22, TitleColour);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.0f, 1.0f);
            titleRect.anchorMax = new Vector2(1.0f, 1.0f);
            titleRect.pivot = new Vector2(0.5f, 1.0f);
            titleRect.anchoredPosition = new Vector2(0.0f, -24.0f);
            titleRect.sizeDelta = new Vector2(-40.0f, 30.0f);
            title.alignment = TextAnchor.UpperCenter;

            Text message = UiKit.MakeText(panel.transform, "Message", "", 15, MessageColour);
            var messageRect = (RectTransform)message.transform;
            messageRect.anchorMin = new Vector2(0.0f, 0.5f);
            messageRect.anchorMax = new Vector2(1.0f, 0.5f);
            messageRect.pivot = new Vector2(0.5f, 0.5f);
            messageRect.anchoredPosition = Vector2.zero;
            messageRect.sizeDelta = new Vector2(-48.0f, 40.0f);
            message.alignment = TextAnchor.MiddleCenter;

            var closeGo = new GameObject("CloseButton", typeof(RectTransform));
            var closeRect = (RectTransform)closeGo.transform;
            closeRect.SetParent(panel.transform, false);
            closeRect.anchorMin = new Vector2(0.5f, 0.0f);
            closeRect.anchorMax = new Vector2(0.5f, 0.0f);
            closeRect.pivot = new Vector2(0.5f, 0.0f);
            closeRect.anchoredPosition = new Vector2(0.0f, 20.0f);
            closeRect.sizeDelta = new Vector2(110.0f, 34.0f);
            Image closeBackground = closeGo.AddComponent<Image>();
            closeBackground.color = CloseColour;
            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeBackground;
            Text closeLabel = UiKit.MakeText(closeGo.transform, "Label", "Close", 15, TitleColour);
            var closeLabelRect = (RectTransform)closeLabel.transform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.raycastTarget = false;

            view.TitleLabel = title;
            view.MessageLabel = message;
            view.CloseButton = closeButton;

            view.Root = root;
            root.SetActive(false);
            UiKit.EnsureEventSystem();
            return view;
        }
    }
}
