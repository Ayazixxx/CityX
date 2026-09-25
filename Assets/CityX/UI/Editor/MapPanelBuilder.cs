using CityX.Character;
using CityX.UI;
using CityX.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the map panel into the open scene. Ported from ui/map_panel.gd.
    ///
    /// STRAIGHT INTO THE SCENE RATHER THAN A PREFAB, same reasoning as
    /// DevConsoleBuilder and PauseMenuBuilder: it is wired to the streamer,
    /// the player, the input reader and the camera, none of which exist
    /// outside a scene.
    ///
    /// EVERY LISTENER MapCanvas OWNS IS WIRED BY ITSELF, not here -- see
    /// PauseMenuView's own doc comment for why an editor-time
    /// onClick.AddListener never survives the scene being saved. MapCanvas
    /// has no listeners of that kind (its scroll/drag handlers come from
    /// implementing IScrollHandler/IDragHandler directly, which the event
    /// system calls through the interface rather than through a saved
    /// delegate), so there is nothing here for that trap to catch -- worth
    /// naming so nobody adds an AddListener call to this file later assuming
    /// it is safe by analogy.
    /// </summary>
    public static class MapPanelBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color BackgroundColour = new Color(0.05f, 0.055f, 0.07f, 0.96f);
        private static readonly Color BorderColour = new Color(0.55f, 0.63f, 1.0f, 0.5f);
        private static readonly Color LabelColour = new Color(0.72f, 0.79f, 1.0f);
        private static readonly Color HintColour = new Color(0.55f, 0.58f, 0.66f);

        private const float PanelMargin = 60.0f;
        private const string TitleText = "CITY MAP";
        private const string HintText = "[M] close   ·   scroll to zoom   ·   drag to pan";

        public static MapPanelView AddToScene(CityStreamer streamer, Transform player,
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera, DevConsoleView console,
            PauseMenuView pause)
        {
            var root = new GameObject("MapPanel", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD (40), below the dev console (60) and the pause
            // menu (80) -- matches map_panel.gd's own relative ordering.
            canvas.sortingOrder = 50;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            MapPanelView view = root.AddComponent<MapPanelView>();
            view.InputReader = inputReader;
            view.OrbitCamera = orbitCamera;
            view.Console = console;
            view.Pause = pause;

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

            // REQUIRED, not cosmetic -- without this MapCanvas keeps drawing
            // chunks straight out past the panel's own border, the same bug
            // Godot's own class comment on clip_contents already names.
            panel.AddComponent<RectMask2D>();

            var canvasObject = new GameObject("Map", typeof(RectTransform));
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.SetParent(panel.transform, false);
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            MapCanvas mapCanvas = canvasObject.AddComponent<MapCanvas>();
            mapCanvas.color = Color.white;
            mapCanvas.Streamer = streamer;
            mapCanvas.Player = player;
            view.Canvas = mapCanvas;

            Text title = MakeText(panel.transform, "Title", TitleText, 22, LabelColour);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.0f, 1.0f);
            titleRect.anchorMax = new Vector2(0.0f, 1.0f);
            titleRect.pivot = new Vector2(0.0f, 1.0f);
            titleRect.anchoredPosition = new Vector2(20.0f, -12.0f);
            titleRect.sizeDelta = new Vector2(300.0f, 30.0f);
            title.alignment = TextAnchor.UpperLeft;

            Text hint = MakeText(panel.transform, "Hint", HintText, 13, HintColour);
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(1.0f, 0.0f);
            hintRect.anchorMax = new Vector2(1.0f, 0.0f);
            hintRect.pivot = new Vector2(1.0f, 0.0f);
            hintRect.anchoredPosition = new Vector2(-20.0f, 14.0f);
            hintRect.sizeDelta = new Vector2(360.0f, 20.0f);
            hint.alignment = TextAnchor.LowerRight;

            // THE PANEL, NOT THE CANVAS ROOT. MapPanelView's own Update()
            // reads M directly to reopen itself -- if Root pointed at root
            // (the object MapPanelView itself lives on), Close() disabling
            // it would also disable the Update() loop watching for M to
            // ever bring it back. Root has to be a child the view can hide
            // without hiding itself, the same shape DevConsole/PauseMenu/
            // PlayerChat's own builders already use.
            view.Root = panel;
            EnsureEventSystem();
            return view;
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

        /// <summary>Without one of these MapCanvas never receives a scroll or drag -- see DevConsoleBuilder's own copy of this note.</summary>
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
