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
        private const string HintText = "[M] close   ·   scroll to zoom   ·   drag to pan   ·   right-click: waypoint (again on it: clear)";
        private const string LegendText = "yellow arrow: you     orange diamonds: landmark towers     purple: waypoint and route";

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

            // WITH ITS CanvasRenderer FROM THE START: adding the Graphic to a bare object and then setting its colour touched a CanvasRenderer
            // that was not there yet ("There is no 'CanvasRenderer' attached to the "Map" game object" on every scene build).
            var canvasObject = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer));
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.SetParent(panel.transform, false);
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            // The waypoint (right-click the map) and its beam in the world live on the root, which is never hidden.
            MapWaypoint waypoint = root.AddComponent<MapWaypoint>();
            waypoint.Streamer = streamer;
            waypoint.Player = player;
            root.AddComponent<WaypointBeacon>().Waypoint = waypoint;

            MapCanvas mapCanvas = canvasObject.AddComponent<MapCanvas>();
            mapCanvas.color = Color.white;
            mapCanvas.Streamer = streamer;
            mapCanvas.Player = player;
            mapCanvas.Waypoint = waypoint;
            view.Canvas = mapCanvas;

            view.MiniMap = BuildMiniMap(root.transform, streamer, player, waypoint);

            Text title = UiKit.MakeText(panel.transform, "Title", TitleText, 22, LabelColour);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.0f, 1.0f);
            titleRect.anchorMax = new Vector2(0.0f, 1.0f);
            titleRect.pivot = new Vector2(0.0f, 1.0f);
            titleRect.anchoredPosition = new Vector2(20.0f, -12.0f);
            titleRect.sizeDelta = new Vector2(300.0f, 30.0f);
            title.alignment = TextAnchor.UpperLeft;

            Text hint = UiKit.MakeText(panel.transform, "Hint", HintText, 13, HintColour);
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(1.0f, 0.0f);
            hintRect.anchorMax = new Vector2(1.0f, 0.0f);
            hintRect.pivot = new Vector2(1.0f, 0.0f);
            hintRect.anchoredPosition = new Vector2(-20.0f, 14.0f);
            hintRect.sizeDelta = new Vector2(620.0f, 20.0f);
            hint.alignment = TextAnchor.LowerRight;

            Text legend = UiKit.MakeText(panel.transform, "Legend", LegendText, 13, HintColour);
            var legendRect = (RectTransform)legend.transform;
            legendRect.anchorMin = new Vector2(1.0f, 1.0f);
            legendRect.anchorMax = new Vector2(1.0f, 1.0f);
            legendRect.pivot = new Vector2(1.0f, 1.0f);
            legendRect.anchoredPosition = new Vector2(-20.0f, -16.0f);
            legendRect.sizeDelta = new Vector2(620.0f, 20.0f);
            legend.alignment = TextAnchor.UpperRight;

            // THE PANEL, NOT THE CANVAS ROOT. MapPanelView's own Update()
            // reads M directly to reopen itself -- if Root pointed at root
            // (the object MapPanelView itself lives on), Close() disabling
            // it would also disable the Update() loop watching for M to
            // ever bring it back. Root has to be a child the view can hide
            // without hiding itself, the same shape DevConsole/PauseMenu/
            // PlayerChat's own builders already use.
            view.Root = panel;
            UiKit.EnsureEventSystem();
            return view;
        }

        private const float MiniMapSize = 170.0f;
        private static readonly Color MiniMapBorder = new Color(0.75f, 0.8f, 0.95f, 0.55f);

        /// <summary>The corner minimap: a framed square in the top right, the N on its rim, the waypoint's distance under it.</summary>
        private static GameObject BuildMiniMap(Transform parent, CityStreamer streamer, Transform player, MapWaypoint waypoint)
        {
            // One group for the frame and the label under it, so they hide together while the full map is open.
            var group = new GameObject("MiniMap", typeof(RectTransform));
            var groupRect = (RectTransform)group.transform;
            groupRect.SetParent(parent, false);
            groupRect.anchorMin = Vector2.zero;
            groupRect.anchorMax = Vector2.one;
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;

            var frame = new GameObject("MiniMapFrame", typeof(RectTransform));
            var frameRect = (RectTransform)frame.transform;
            frameRect.SetParent(groupRect, false);
            frameRect.anchorMin = new Vector2(1.0f, 1.0f);
            frameRect.anchorMax = new Vector2(1.0f, 1.0f);
            frameRect.pivot = new Vector2(1.0f, 1.0f);
            frameRect.anchoredPosition = new Vector2(-16.0f, -16.0f);
            frameRect.sizeDelta = new Vector2(MiniMapSize, MiniMapSize);
            Image back = frame.AddComponent<Image>();
            back.color = BackgroundColour;
            back.raycastTarget = false;
            var outline = frame.AddComponent<Outline>();
            outline.effectColor = MiniMapBorder;
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            frame.AddComponent<RectMask2D>();

            var drawing = new GameObject("MiniMapCanvas", typeof(RectTransform), typeof(CanvasRenderer));
            var drawingRect = (RectTransform)drawing.transform;
            drawingRect.SetParent(frameRect, false);
            drawingRect.anchorMin = Vector2.zero;
            drawingRect.anchorMax = Vector2.one;
            drawingRect.offsetMin = Vector2.zero;
            drawingRect.offsetMax = Vector2.zero;
            MiniMapView mini = drawing.AddComponent<MiniMapView>();
            mini.color = Color.white;
            mini.raycastTarget = false;
            mini.Streamer = streamer;
            mini.Player = player;
            mini.Waypoint = waypoint;

            Text north = UiKit.MakeText(frameRect, "North", "N", 15, new Color(1.0f, 0.4f, 0.35f));
            north.fontStyle = FontStyle.Bold;
            north.alignment = TextAnchor.MiddleCenter;
            var northRect = (RectTransform)north.transform;
            northRect.anchorMin = northRect.anchorMax = new Vector2(0.5f, 0.5f);
            northRect.sizeDelta = new Vector2(20.0f, 20.0f);
            north.gameObject.AddComponent<Outline>().effectColor = new Color(0.0f, 0.0f, 0.0f, 0.8f);
            mini.NorthLabel = northRect;

            // Beside the frame, not in it: the frame's mask would clip a label hung below it.
            Text distance = UiKit.MakeText(groupRect, "WaypointDistance", "", 13, LabelColour);
            distance.alignment = TextAnchor.UpperCenter;
            var distanceRect = (RectTransform)distance.transform;
            distanceRect.anchorMin = distanceRect.anchorMax = new Vector2(1.0f, 1.0f);
            distanceRect.pivot = new Vector2(1.0f, 1.0f);
            distanceRect.anchoredPosition = new Vector2(-16.0f, -16.0f - MiniMapSize - 4.0f);
            distanceRect.sizeDelta = new Vector2(MiniMapSize, 20.0f);
            distance.gameObject.AddComponent<Outline>().effectColor = new Color(0.0f, 0.0f, 0.0f, 0.8f);
            mini.WaypointLabel = distance;
            return group;
        }
    }
}
