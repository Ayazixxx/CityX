using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Draws the map itself: explored chunks, buildings shaded by height, and
    /// the player's own facing marker. Ported from ui/map_panel.gd's own
    /// _draw_map/_draw_player_marker.
    ///
    /// ONE MESH REBUILT EVERY FRAME WHILE OPEN, not a ColorRect per chunk --
    /// Godot's own reasoning for a single _draw() pass over hundreds of
    /// rectangles applies exactly as well to a single OnPopulateMesh pass
    /// here, and for the same reason: churning hundreds of real UI elements
    /// every time the player moves is far more expensive than rebuilding one
    /// vertex buffer.
    ///
    /// EVERY HARD PIECE OF MATH LIVES IN MapProjection, not here -- this
    /// class is a thin loop that calls it. What can be silently wrong (the
    /// scale, the zoom-pin, the off-screen cull) is exactly what that class's
    /// own tests already pin.
    ///
    /// NO CLAIMED-BUILDING MARKER. Godot's own map draws a ring on whatever
    /// the player has claimed (see player.gd's own /claim) -- this port has
    /// no claim system yet, so there is nothing for the ring to point at.
    /// </summary>
    public sealed class MapCanvas : MaskableGraphic, IScrollHandler, IBeginDragHandler, IDragHandler
    {
        private static readonly Color RoadColour = new Color(0.17f, 0.17f, 0.19f);
        private static readonly Color LotColour = new Color(0.30f, 0.29f, 0.27f);
        private static readonly Color BuildingLowColour = new Color(0.52f, 0.55f, 0.62f);
        private static readonly Color BuildingTallColour = new Color(0.88f, 0.91f, 0.98f);
        private static readonly Color PlayerColour = new Color(1.0f, 0.85f, 0.2f);

        public const float BuildingMinHeight = 8.0f;
        public const float BuildingMaxHeight = 96.0f;
        public const float PlayerMarkerSize = 7.0f;

        [Tooltip("Whose explored chunks and world seed the map reads.")]
        public CityStreamer Streamer;

        [Tooltip("Whose position and facing the map centres on and marks.")]
        public Transform Player;

        private float _zoom = 1.0f;
        private Vector2 _panWorld;
        private CityWorld _world;
        private int _worldSeed;

        /// <summary>Recentres on the player and resets any drag, keeping the zoom -- matches Godot's own open().</summary>
        public void ResetView()
        {
            _panWorld = Vector2.zero;
        }

        private CityWorld WorldFor(int seed)
        {
            if (_world == null || _worldSeed != seed)
            {
                _world = new CityWorld(seed);
                _worldSeed = seed;
            }
            return _world;
        }

        public void OnScroll(PointerEventData eventData)
        {
            float factor = eventData.scrollDelta.y > 0.0f ? MapProjection.ZoomStep : 1.0f / MapProjection.ZoomStep;
            ZoomAt(eventData, factor);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Nothing to do beyond letting OnDrag receive the following moves --
            // required so this counts as a drag handler at all.
        }

        public void OnDrag(PointerEventData eventData)
        {
            // A local-space delta rather than eventData.delta directly: the
            // canvas can be scaled by CanvasScaler, and comparing two points
            // already converted into this rect's own local space cancels that
            // out the same way ScreenPointToLocalPointInRectangle always does.
            if (!TryLocalPoint(eventData.position, out Vector2 now) ||
                !TryLocalPoint(eventData.position - eventData.delta, out Vector2 previous))
            {
                return;
            }

            // The map moves WITH the cursor, so the view centre moves against
            // it -- drag right and the world slides right, which means
            // looking further left. Divided by the current scale so a drag
            // covers the same on-screen distance however far zoomed in.
            Vector2 localDelta = now - previous; // Unity local space: +Y is up
            float scale = CurrentScale();
            if (scale <= 0.0f)
            {
                return;
            }
            // Flip Y going into panel space (+Y down, matching world +Z).
            _panWorld -= new Vector2(localDelta.x, -localDelta.y) / scale;
            SetVerticesDirty();
        }

        private void ZoomAt(PointerEventData eventData, float factor)
        {
            if (!TryLocalPoint(eventData.position, out Vector2 local))
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

            // Cursor offset from centre, in panel space (+Y down) -- Unity's
            // local space has +Y up and is already pivot-relative, so only
            // the Y sign needs flipping to match.
            var offset = new Vector2(local.x, -local.y);
            Vector2 pan = MapProjection.PanAdjustmentForZoom(offset, before, after);
            _panWorld += pan;
            SetVerticesDirty();
        }

        private bool TryLocalPoint(Vector2 screenPosition, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPosition, null, out local);
        }

        private float CurrentScale()
        {
            if (Streamer == null)
            {
                return 1.0f;
            }
            return MapProjection.ScaleFor(rectTransform.rect.size, CityLayout.ChunkSize, _zoom);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Streamer == null || Player == null)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            Vector2 panelSize = rect.size;
            if (panelSize.x <= 0.0f || panelSize.y <= 0.0f)
            {
                return;
            }

            float chunkSize = CityLayout.ChunkSize;
            float scale = MapProjection.ScaleFor(panelSize, chunkSize, _zoom);
            Vector2 panelCentre = panelSize * 0.5f;

            Vector3 playerPosition = Player.position;
            float viewX = playerPosition.x + _panWorld.x;
            float viewZ = playerPosition.z + _panWorld.y;

            CityWorld world = WorldFor(Streamer.WorldSeed);

            foreach (Vector2Int coord in Streamer.GetExploredChunks())
            {
                MapProjection.ChunkRect(panelCentre, coord.x, coord.y, chunkSize,
                    viewX, viewZ, scale, out Vector2 topLeft, out float pixelSize);
                if (MapProjection.IsOffScreen(topLeft, pixelSize, panelSize))
                {
                    continue;
                }

                bool isStreet = CityLayout.IsStreetChunk(coord.x, coord.y);
                AddPanelQuad(vh, rect, topLeft, new Vector2(pixelSize, pixelSize), isStreet ? RoadColour : LotColour);
                if (isStreet)
                {
                    continue;
                }

                BuildingSite site = world.BuildingAt(coord.x, coord.y);
                if (!site.Exists)
                {
                    continue;
                }

                Vector2 buildingTopLeft = MapProjection.ToScreen(panelCentre,
                    (float)(site.CentreX - site.Params.Width * 0.5), (float)(site.CentreZ - site.Params.Depth * 0.5),
                    viewX, viewZ, scale);
                var buildingSize = new Vector2(site.Params.Width * scale, site.Params.Depth * scale);
                float tallness = MapProjection.HeightFraction(
                    site.Params.Height, BuildingMinHeight, BuildingMaxHeight);
                Color buildingColour = Color.Lerp(BuildingLowColour, BuildingTallColour, tallness);
                AddPanelQuad(vh, rect, buildingTopLeft, buildingSize, buildingColour);
            }

            AddPlayerMarker(vh, rect, panelCentre);
        }

        /// <summary>A filled triangle at the panel centre, pointing whichever way the player is facing.</summary>
        private void AddPlayerMarker(VertexHelper vh, Rect rect, Vector2 panelCentre)
        {
            Vector3 forward3D = Player.forward;
            var facing = new Vector2(forward3D.x, forward3D.z);
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = new Vector2(0.0f, -1.0f);
            }
            facing.Normalize();
            var side = new Vector2(-facing.y, facing.x);

            Vector2 tip = panelCentre + facing * PlayerMarkerSize;
            Vector2 left = panelCentre - facing * (PlayerMarkerSize * 0.6f) + side * (PlayerMarkerSize * 0.6f);
            Vector2 right = panelCentre - facing * (PlayerMarkerSize * 0.6f) - side * (PlayerMarkerSize * 0.6f);

            int start = vh.currentVertCount;
            vh.AddVert(PanelVertex(rect, tip, PlayerColour));
            vh.AddVert(PanelVertex(rect, left, PlayerColour));
            vh.AddVert(PanelVertex(rect, right, PlayerColour));
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddPanelQuad(VertexHelper vh, Rect rect, Vector2 topLeft, Vector2 size, Color colour)
        {
            int start = vh.currentVertCount;
            vh.AddVert(PanelVertex(rect, topLeft, colour));
            vh.AddVert(PanelVertex(rect, topLeft + new Vector2(size.x, 0.0f), colour));
            vh.AddVert(PanelVertex(rect, topLeft + size, colour));
            vh.AddVert(PanelVertex(rect, topLeft + new Vector2(0.0f, size.y), colour));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        /// <summary>
        /// Panel space (origin top-left, +X right, +Y down -- matching Godot's
        /// own Control convention) converted into this RectTransform's real
        /// local space (pivot-relative, +Y up), which is what a Graphic's own
        /// mesh has to be built in.
        /// </summary>
        private static UIVertex PanelVertex(Rect rect, Vector2 panelPosition, Color colour)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = colour;
            vertex.position = new Vector3(
                rect.xMin + panelPosition.x,
                rect.yMax - panelPosition.y,
                0.0f);
            return vertex;
        }
    }
}
