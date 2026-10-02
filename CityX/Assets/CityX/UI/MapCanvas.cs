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

        /// <summary>
        /// A street tile never draws thinner than this many panel pixels,
        /// however small its own true pixelSize (chunkSize * scale) works
        /// out to -- EXPLICIT ASK: "make the streets thicker than 1 pixel
        /// they are too thin." A street is exactly one chunk wide in world
        /// space, so at any zoom level modest enough to see a real
        /// neighbourhood at once it reads as a hairline next to buildings
        /// several chunks across -- true to scale, but the whole point of a
        /// legible map is contrast a viewer can actually see, not literal
        /// metric accuracy. Grown symmetrically around the tile's own
        /// centre (see the grow/drawTopLeft maths below), so a thickened
        /// tile can only ever OVERLAP its own street-tile neighbours along
        /// the same line (harmless -- two opaque quads of the same colour
        /// merge seamlessly) and never leaves a gap.
        /// </summary>
        public const float StreetMinPixelWidth = 3.5f;

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
            Vector2Int[] explored = Streamer.GetExploredChunks();

            // Pass 1: the ground of every explored chunk, so nothing drawn later is covered by a neighbour's square.
            foreach (Vector2Int coord in explored)
            {
                MapProjection.ChunkRect(panelCentre, coord.x, coord.y, chunkSize,
                    viewX, viewZ, scale, out Vector2 topLeft, out float pixelSize);
                if (MapProjection.IsOffScreen(topLeft, pixelSize, panelSize))
                {
                    continue;
                }
                AddPanelQuad(vh, rect, topLeft, new Vector2(pixelSize, pixelSize), LotColour);
            }

            // Pass 2: the roads, each drawn once as the turned strip it is.
            var drawnRoads = new System.Collections.Generic.HashSet<int>();
            foreach (Vector2Int coord in explored)
            {
                MapProjection.ChunkRect(panelCentre, coord.x, coord.y, chunkSize,
                    viewX, viewZ, scale, out Vector2 topLeft, out float pixelSize);
                if (MapProjection.IsOffScreen(topLeft, pixelSize, panelSize))
                {
                    continue;
                }
                foreach (int edgeId in world.Plan.EdgesIn(coord.x, coord.y))
                {
                    if (!drawnRoads.Add(edgeId))
                    {
                        continue;
                    }
                    CityX.World.Plan.EdgeGeometry g = world.Plan.Geometry.Edges[edgeId];
                    if (g == null)
                    {
                        continue;
                    }
                    // Never thinner than a few pixels, however far the map is zoomed out.
                    float half = Mathf.Max((float)g.HalfWidth, StreetMinPixelWidth * 0.5f / scale);
                    var along = new Vector2((float)g.Dir.X, (float)g.Dir.Y);
                    var across = new Vector2(-along.y, along.x) * half;
                    var a = new Vector2((float)g.A.X, (float)g.A.Y);
                    var b = new Vector2((float)g.B.X, (float)g.B.Y);
                    AddPanelQuad4(vh, rect,
                        MapProjection.ToScreen(panelCentre, a.x + across.x, a.y + across.y, viewX, viewZ, scale),
                        MapProjection.ToScreen(panelCentre, b.x + across.x, b.y + across.y, viewX, viewZ, scale),
                        MapProjection.ToScreen(panelCentre, b.x - across.x, b.y - across.y, viewX, viewZ, scale),
                        MapProjection.ToScreen(panelCentre, a.x - across.x, a.y - across.y, viewX, viewZ, scale),
                        RoadColour);
                }
            }

            // Pass 3: the buildings, each as its own turned rectangle, coloured by height.
            foreach (Vector2Int coord in explored)
            {
                MapProjection.ChunkRect(panelCentre, coord.x, coord.y, chunkSize,
                    viewX, viewZ, scale, out Vector2 topLeft, out float pixelSize);
                if (MapProjection.IsOffScreen(topLeft, pixelSize, panelSize))
                {
                    continue;
                }
                System.Collections.Generic.List<int> owned = world.BuildingsOwnedBy(coord.x, coord.y);
                for (int i = 0; i < owned.Count; i++)
                {
                    BuildingSite site = world.Building(owned[i]);
                    float hw = site.Params.Width * 0.5f;
                    float hd = site.Params.Depth * 0.5f;
                    Vector2 c0 = ScreenOf(site, -hw, -hd, panelCentre, viewX, viewZ, scale);
                    Vector2 c1 = ScreenOf(site, hw, -hd, panelCentre, viewX, viewZ, scale);
                    Vector2 c2 = ScreenOf(site, hw, hd, panelCentre, viewX, viewZ, scale);
                    Vector2 c3 = ScreenOf(site, -hw, hd, panelCentre, viewX, viewZ, scale);
                    float tallness = MapProjection.HeightFraction(
                        site.Params.Height, BuildingMinHeight, BuildingMaxHeight);
                    AddPanelQuad4(vh, rect, c0, c1, c2, c3, Color.Lerp(BuildingLowColour, BuildingTallColour, tallness));
                }
            }

            AddPlayerMarker(vh, rect, panelCentre);
        }

        private static Vector2 ScreenOf(BuildingSite site, float localX, float localZ, Vector2 panelCentre,
            float viewX, float viewZ, float scale)
        {
            site.LocalToWorld(localX, localZ, out double wx, out double wz);
            return MapProjection.ToScreen(panelCentre, (float)wx, (float)wz, viewX, viewZ, scale);
        }

        private static void AddPanelQuad4(VertexHelper vh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
        {
            int start = vh.currentVertCount;
            vh.AddVert(PanelVertex(rect, a, colour));
            vh.AddVert(PanelVertex(rect, b, colour));
            vh.AddVert(PanelVertex(rect, c, colour));
            vh.AddVert(PanelVertex(rect, d, colour));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
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
