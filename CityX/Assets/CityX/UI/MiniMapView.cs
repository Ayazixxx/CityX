using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The minimap in the corner of the screen: the streets and buildings around the player, TURNED SO THE WAY THE CAMERA LOOKS IS UP (GTA's
    /// radar does this; a north-up minimap makes the player turn the map in their head every time they turn), the player's arrow in the
    /// middle, the waypoint's route, and the waypoint itself -- held at the edge, pointing the way, when it is further than the minimap shows.
    /// An N on the rim shows where north is, and a line under it how far the waypoint is.
    ///
    /// Kept small and out of the way: research on minimaps says to show only what helps the moment (routes, objectives), and the newest
    /// open-world games keep the screen clean. It hides while the full map is open.
    /// </summary>
    public sealed class MiniMapView : MaskableGraphic
    {
        /// <summary>How far the minimap reaches from the player to its edge, metres.</summary>
        public const float RadiusMetres = 85.0f;

        public const float PlayerMarkerSize = 8.0f;
        public const float WaypointSize = 7.0f;
        public const float RouteWidth = 4.0f;
        public const float LandmarkSize = 5.0f;

        public CityStreamer Streamer;

        public Transform Player;

        public MapWaypoint Waypoint;

        [Tooltip("Whose look turns the minimap. Falls back to the main camera.")]
        public Transform View;

        [Tooltip("The N on the rim, moved round to wherever north is.")]
        public RectTransform NorthLabel;

        [Tooltip("How far the waypoint is, under the minimap; hidden with no waypoint.")]
        public Text WaypointLabel;

        /// <summary>The turn the minimap is drawn at (for tests): the camera's heading put straight up.</summary>
        public float Rotation { get; private set; }

        private void Update()
        {
            SetVerticesDirty();
            if (WaypointLabel != null)
            {
                bool has = Waypoint != null && Waypoint.Has;
                WaypointLabel.enabled = has;
                if (has)
                {
                    WaypointLabel.text = $"WAYPOINT  {Mathf.RoundToInt(Waypoint.RouteMetres)} m";
                }
            }
        }

        private Transform ViewTransform
        {
            get
            {
                if (View != null)
                {
                    return View;
                }
                Camera main = Camera.main;
                return main != null ? main.transform : Player;
            }
        }

        public MapFrame Frame()
        {
            Rect rect = rectTransform.rect;
            Vector3 p = Player != null ? Player.position : Vector3.zero;
            Transform view = ViewTransform;
            Vector3 forward = view != null ? view.forward : Vector3.forward;
            Rotation = MapFrame.RotationPuttingUp(new Vector2(forward.x, forward.z));
            float scale = Mathf.Min(rect.width, rect.height) * 0.5f / RadiusMetres;
            return new MapFrame(rect, p.x, p.z, scale, Rotation);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Streamer == null || Player == null || rectTransform.rect.width <= 0.0f)
            {
                return;
            }
            CityWorld world = CurrentWorld.Instance ?? new CityWorld(Streamer.WorldSeed);
            MapFrame frame = Frame();
            MapPainter.DrawCity(vh, frame, world, Streamer);
            float edge = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f - WaypointSize * 1.6f;
            if (Waypoint != null && Waypoint.Has)
            {
                MapPainter.DrawRoute(vh, frame, Waypoint.Route, RouteWidth);
            }
            MapPainter.DrawLandmarks(vh, frame, world, LandmarkSize);
            if (Waypoint != null && Waypoint.Has)
            {
                MapPainter.DrawWaypoint(vh, frame, Waypoint.Point, WaypointSize, clampRadius: edge);
            }
            MapPainter.DrawPlayer(vh, frame, Player.position, Player.forward, PlayerMarkerSize);
            PlaceNorth(frame);
        }

        private void PlaceNorth(MapFrame frame)
        {
            if (NorthLabel == null)
            {
                return;
            }
            // World north is -Z (the map's top at no turn).
            Vector2 north = frame.TurnDirection(new Vector2(0.0f, -1.0f));
            float rim = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f - 11.0f;
            NorthLabel.anchoredPosition = new Vector2(north.x, -north.y) * rim;
        }
    }
}
