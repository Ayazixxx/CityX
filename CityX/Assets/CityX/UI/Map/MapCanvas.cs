using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The full map (M): explored chunks, roads, buildings shaded by height with a drop shadow, landmark towers, the waypoint and its route
    /// along the streets, and the player's arrow. North up. Drag to pan, scroll to zoom about the cursor, RIGHT-CLICK to set a waypoint (or to
    /// clear it, on the pin). The drawing is MapPainter's, shared with the minimap; the zoom and pan maths are MapProjection's.
    ///
    /// ONE MESH REBUILT EVERY FRAME WHILE OPEN, not a UI element per chunk: rebuilding one vertex buffer is far cheaper than churning hundreds
    /// of elements every time the player moves.
    /// </summary>
    public sealed class MapCanvas : MaskableGraphic, IScrollHandler, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        public const float PlayerMarkerSize = 9.0f;
        public const float WaypointSize = 9.0f;
        public const float RouteWidth = 5.0f;
        public const float LandmarkSize = 6.0f;

        /// <summary>A right-click this close to the pin, in pixels, clears it instead of moving it.</summary>
        public const float PinClickPixels = 16.0f;

        [Tooltip("Whose explored chunks and world seed the map reads.")]
        public CityStreamer Streamer;

        [Tooltip("Whose position and facing the map centres on and marks.")]
        public Transform Player;

        [Tooltip("The player's waypoint: set by right-clicking here.")]
        public MapWaypoint Waypoint;

        private float _zoom = 1.0f;
        private Vector2 _panWorld;

        /// <summary>Recentres on the player and resets any drag, keeping the zoom.</summary>
        public void ResetView()
        {
            _panWorld = Vector2.zero;
        }

        public float Zoom => _zoom;

        public void OnScroll(PointerEventData eventData)
        {
            float factor = eventData.scrollDelta.y > 0.0f ? MapProjection.ZoomStep : 1.0f / MapProjection.ZoomStep;
            ZoomAt(eventData.position, factor);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Required for OnDrag to be called at all.
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            if (!TryLocalPoint(eventData.position, out Vector2 now) || !TryLocalPoint(eventData.position - eventData.delta, out Vector2 previous))
            {
                return;
            }
            // The map moves with the cursor, so the view moves against it; divided by the scale so a drag covers the same screen distance at any zoom.
            Vector2 localDelta = now - previous;   // Unity local space: +Y up
            float scale = CurrentScale();
            if (scale <= 0.0f)
            {
                return;
            }
            _panWorld -= new Vector2(localDelta.x, -localDelta.y) / scale;
            SetVerticesDirty();
        }

        /// <summary>Right-click: a waypoint where the cursor is, or, on the pin itself, no waypoint.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                RightClickAt(eventData.position);
            }
        }

        /// <summary>Public so a test can click the map without a mouse.</summary>
        public void RightClickAt(Vector2 screenPosition)
        {
            if (Waypoint == null || !TryLocalPoint(screenPosition, out Vector2 local))
            {
                return;
            }
            MapFrame frame = Frame();
            Vector2 panel = LocalToPanel(local);
            if (Waypoint.Has)
            {
                Vector2 pin = frame.ToPanel(Waypoint.Point.x, Waypoint.Point.y);
                if (Vector2.Distance(pin, panel) <= PinClickPixels || Vector2.Distance(pin + new Vector2(0.0f, -WaypointSize * 1.4f), panel) <= PinClickPixels)
                {
                    Waypoint.Clear();
                    SetVerticesDirty();
                    return;
                }
            }
            Waypoint.Set(frame.ToWorld(panel));
            SetVerticesDirty();
        }

        /// <summary>Where a world (x, z) point is on the screen, for tests and tools.</summary>
        public Vector2 ScreenAt(Vector2 world)
        {
            Vector2 panel = Frame().ToPanel(world.x, world.y);
            Rect rect = rectTransform.rect;
            Vector3 local = new Vector3(rect.xMin + panel.x, rect.yMax - panel.y, 0.0f);
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera eventCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(eventCamera, rectTransform.TransformPoint(local));
        }

        private void ZoomAt(Vector2 screenPosition, float factor)
        {
            if (!TryLocalPoint(screenPosition, out Vector2 local))
            {
                return;
            }
            float before = CurrentScale();
            float zoomed = MapProjection.ClampZoom(_zoom * factor);
            if (Mathf.Approximately(zoomed, _zoom))
            {
                return;
            }
            _zoom = zoomed;
            float after = CurrentScale();
            if (before <= 0.0f || after <= 0.0f)
            {
                return;
            }
            // The point under the cursor stays under it.
            var offset = new Vector2(local.x, -local.y);
            _panWorld += MapProjection.PanAdjustmentForZoom(offset, before, after);
            SetVerticesDirty();
        }

        private bool TryLocalPoint(Vector2 screenPosition, out Vector2 local)
        {
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera eventCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, eventCamera, out local);
        }

        /// <summary>Local (pivot-relative, +Y up) to panel space (top-left origin, +Y down).</summary>
        private Vector2 LocalToPanel(Vector2 local)
        {
            Rect rect = rectTransform.rect;
            return new Vector2(local.x - rect.xMin, rect.yMax - local.y);
        }

        private float CurrentScale() => MapProjection.ScaleFor(rectTransform.rect.size, CityLayout.ChunkSize, _zoom);

        private MapFrame Frame()
        {
            Vector3 p = Player != null ? Player.position : Vector3.zero;
            return new MapFrame(rectTransform.rect, p.x + _panWorld.x, p.z + _panWorld.y, CurrentScale());
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Streamer == null || Player == null || rectTransform.rect.width <= 0.0f || rectTransform.rect.height <= 0.0f)
            {
                return;
            }
            CityWorld world = CurrentWorld.Instance ?? new CityWorld(Streamer.WorldSeed);
            MapFrame frame = Frame();
            MapPainter.DrawCity(vh, frame, world, Streamer);
            if (Waypoint != null && Waypoint.Has)
            {
                MapPainter.DrawRoute(vh, frame, Waypoint.Route, RouteWidth);
            }
            MapPainter.DrawLandmarks(vh, frame, world, LandmarkSize);
            if (Waypoint != null && Waypoint.Has)
            {
                MapPainter.DrawWaypoint(vh, frame, Waypoint.Point, WaypointSize);
            }
            MapPainter.DrawPlayer(vh, frame, Player.position, Player.forward, PlayerMarkerSize);
        }
    }
}
