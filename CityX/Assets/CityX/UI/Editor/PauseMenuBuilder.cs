using CityX.Character;
using CityX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the pause menu into the open scene. Ported from
    /// ui/pause_menu.gd + ui/settings_panel.gd.
    ///
    /// STRAIGHT INTO THE SCENE RATHER THAN A PREFAB, same reasoning as
    /// DevConsoleBuilder: it needs the dev console to guard Escape against,
    /// which only exists in a scene.
    ///
    /// EVERY LISTENER IS WIRED BY PauseMenuView ITSELF, IN ITS OWN AWAKE --
    /// not here. This builder only constructs the hierarchy and hands the
    /// view its references; see PauseMenuView's own doc comment for why an
    /// editor script must never be the one calling onClick.AddListener.
    /// </summary>
    public static class PauseMenuBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);
        private static readonly Color Danger = new Color(0.55f, 0.16f, 0.16f, 1.0f);
        private static readonly Color PanelInk = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        private static readonly Color FieldInk = new Color(0.12f, 0.13f, 0.16f, 0.95f);

        public static PauseMenuView AddToScene(DevConsoleView console, PlayerVoiceCapture voiceCapture = null)
        {
            var root = new GameObject("PauseMenu", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD (40) and the dev console (60) -- a paused game
            // is the topmost state a player can be in.
            canvas.sortingOrder = 80;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            PauseMenuView view = root.AddComponent<PauseMenuView>();
            view.Console = console;
            view.VoiceCapture = voiceCapture;

            var dim = new GameObject("Dim", typeof(RectTransform));
            var dimRect = (RectTransform)dim.transform;
            dimRect.SetParent(root.transform, false);
            UiKit.Stretch(dimRect);
            dim.AddComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.6f);
            view.Dim = dim;

            view.PausePanel = BuildPausePanel(dim.transform, view);
            view.SettingsPanel = BuildSettingsPanel(dim.transform, view);

            UiKit.EnsureEventSystem();
            return view;
        }

        private static GameObject BuildPausePanel(Transform parent, PauseMenuView view)
        {
            GameObject panel = MakeCenteredPanel(parent, "PausePanel", new Vector2(280.0f, 260.0f));

            MakeLabel(panel.transform, "Heading", "PAUSED", 26, Gold, new Vector2(0.0f, 96.0f));
            view.ResumeButton = MakeButton(panel.transform, "ResumeButton", "RESUME", Gold, new Vector2(0.0f, 34.0f));
            view.SettingsButton = MakeButton(panel.transform, "SettingsButton", "SETTINGS", Paper, new Vector2(0.0f, -22.0f));
            view.QuitButton = MakeButton(panel.transform, "QuitButton", "QUIT TO TITLE", Danger, new Vector2(0.0f, -78.0f));

            return panel;
        }

        private static GameObject BuildSettingsPanel(Transform parent, PauseMenuView view)
        {
            // Grown from 360x320 to fit the four new Voice Chat rows below
            // Resolution -- Heading/Master/Fullscreen/Resolution keep their
            // own original positions unchanged. Output/Input Device and
            // Push-to-Talk are single-line rows (label left, control right,
            // matching Fullscreen's own shape) rather than label-above-
            // stepper like Resolution -- this screen's own 1152x648
            // reference resolution gives only +-324 design units of real
            // vertical room (confirmed by rendering the equivalent title
            // screen panel and finding its own first, more spread-out
            // version of these same rows ran past the bottom of the actual
            // frame), which label-above-stepper for three more rows plus a
            // "VOICE CHAT" section heading did not fit inside.
            GameObject panel = MakeCenteredPanel(parent, "SettingsPanel", new Vector2(360.0f, 620.0f));
            panel.SetActive(false);

            MakeLabel(panel.transform, "Heading", "SETTINGS", 24, Gold, new Vector2(0.0f, 170.0f));

            MakeLabel(panel.transform, "MasterLabel", "MASTER VOLUME", 15, Paper, new Vector2(0.0f, 118.0f));
            view.MasterSlider = MakeSlider(panel.transform, "MasterSlider", new Vector2(0.0f, 94.0f));

            MakeLabel(panel.transform, "FullscreenLabel", "FULLSCREEN", 15, Paper, new Vector2(-80.0f, 48.0f));
            view.FullscreenToggle = MakeToggle(panel.transform, "FullscreenToggle", new Vector2(90.0f, 48.0f));

            MakeLabel(panel.transform, "ResolutionLabel", "RESOLUTION", 15, Paper, new Vector2(0.0f, 4.0f));
            view.ResolutionPrevButton = MakeArrowButton(panel.transform, "ResolutionPrev", "<", new Vector2(-110.0f, -22.0f));
            view.ResolutionLabel = MakeLabel(panel.transform, "ResolutionValue", "1920x1080", 15, Paper, new Vector2(0.0f, -22.0f));
            view.ResolutionNextButton = MakeArrowButton(panel.transform, "ResolutionNext", ">", new Vector2(110.0f, -22.0f));

            // The frame-rate cap: a single-line stepper like the device rows below, in the room made by moving
            // Heading..Resolution up (the panel had empty space above its heading).
            MakeLabel(panel.transform, "FpsLabel", "MAX FPS", 12, Paper, new Vector2(-100.0f, -64.0f));
            view.FpsPrevButton = MakeArrowButton(panel.transform, "FpsPrev", "<", new Vector2(40.0f, -64.0f));
            view.FpsLabel = MakeLabel(panel.transform, "FpsValue", "UNLIMITED", 11, Paper, new Vector2(95.0f, -64.0f));
            view.FpsNextButton = MakeArrowButton(panel.transform, "FpsNext", ">", new Vector2(150.0f, -64.0f));

            MakeLabel(panel.transform, "MusicLabel", "MUSIC VOLUME", 13, Paper, new Vector2(0.0f, -100.0f));
            view.MusicSlider = MakeSlider(panel.transform, "MusicSlider", new Vector2(0.0f, -124.0f));

            MakeLabel(panel.transform, "OutputDeviceLabel", "OUTPUT DEVICE", 11, Paper, new Vector2(-100.0f, -158.0f));
            view.OutputDevicePrevButton = MakeArrowButton(panel.transform, "OutputDevicePrev", "<", new Vector2(40.0f, -158.0f));
            view.OutputDeviceLabel = MakeLabel(panel.transform, "OutputDeviceValue", "Default", 11, Paper, new Vector2(95.0f, -158.0f));
            view.OutputDeviceNextButton = MakeArrowButton(panel.transform, "OutputDeviceNext", ">", new Vector2(150.0f, -158.0f));

            // 40 below Output Device, not 32 -- MakeArrowButton is 36 tall,
            // so a 32-unit gap between two of them (18+18 half-heights)
            // touches with zero clearance and rendered as one merged block
            // with no visible gap, confirmed via a real capture.
            MakeLabel(panel.transform, "InputDeviceLabel", "INPUT DEVICE", 11, Paper, new Vector2(-100.0f, -198.0f));
            view.InputDevicePrevButton = MakeArrowButton(panel.transform, "InputDevicePrev", "<", new Vector2(40.0f, -198.0f));
            view.InputDeviceLabel = MakeLabel(panel.transform, "InputDeviceValue", "Default", 11, Paper, new Vector2(95.0f, -198.0f));
            view.InputDeviceNextButton = MakeArrowButton(panel.transform, "InputDeviceNext", ">", new Vector2(150.0f, -198.0f));

            MakeLabel(panel.transform, "PushToTalkLabel", "PUSH TO TALK (HOLD V)", 12, Paper, new Vector2(-75.0f, -234.0f));
            view.PushToTalkToggle = MakeToggle(panel.transform, "PushToTalkToggle", new Vector2(115.0f, -234.0f));

            view.BackButton = MakeButton(panel.transform, "BackButton", "BACK", Paper, new Vector2(0.0f, -274.0f));

            return panel;
        }

        // --- shared pieces, matching CharacterSelectBuilder's own hand-built style ---

        private static GameObject MakeCenteredPanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            go.AddComponent<Image>().color = PanelInk;
            return go;
        }

        private static Text MakeLabel(Transform parent, string name, string content, int size,
            Color colour, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(320.0f, 30.0f);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, string label, Color tint, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(200.0f, 42.0f);

            Image background = go.AddComponent<Image>();
            background.color = FieldInk;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            button.colors = colours;

            MakeLabel(go.transform, "Label", label, 16, tint, Vector2.zero);
            return button;
        }

        private static Button MakeArrowButton(Transform parent, string name, string glyph, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(36.0f, 36.0f);

            Image background = go.AddComponent<Image>();
            background.color = FieldInk;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            MakeLabel(go.transform, "Glyph", glyph, 18, Paper, Vector2.zero);
            return button;
        }

        /// <summary>Hand-built rather than from DefaultControls, matching MakeSlider elsewhere in this project.</summary>
        private static Slider MakeSlider(Transform parent, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(280.0f, 16.0f);

            Image background = go.AddComponent<Image>();
            background.color = FieldInk;

            var fillArea = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillArea.transform;
            fillRect.SetParent(go.transform, false);
            UiKit.Stretch(fillRect);
            Image fill = fillArea.AddComponent<Image>();
            fill.color = Gold;

            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(go.transform, false);
            handleRect.sizeDelta = new Vector2(10.0f, 22.0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Paper;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0.0f;
            slider.maxValue = 1.0f;
            slider.wholeNumbers = false;
            slider.value = 1.0f;
            return slider;
        }

        /// <summary>A checkbox: background, a checkmark shown only while on, and the Toggle tying them together.</summary>
        private static Toggle MakeToggle(Transform parent, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(28.0f, 28.0f);

            Image background = go.AddComponent<Image>();
            background.color = FieldInk;

            var checkmark = new GameObject("Checkmark", typeof(RectTransform));
            var checkRect = (RectTransform)checkmark.transform;
            checkRect.SetParent(go.transform, false);
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = checkRect.offsetMax = Vector2.zero;
            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = Gold;

            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkImage;
            toggle.isOn = true;
            return toggle;
        }
    }
}
