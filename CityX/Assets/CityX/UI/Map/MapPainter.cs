using System.Collections.Generic;
using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Where the map is looking and how it is turned: world (x, z) -> panel pixels (origin top-left, +Y down, world +Z south), centred on a
    /// view point, scaled, and optionally TURNED so a given heading points up (the minimap turns with the camera, as GTA's does; the full map
    /// stays north-up).
    /// </summary>
    public readonly struct MapFrame
    {
        public readonly Rect Rect;
        public readonly Vector2 Centre;
        public readonly float ViewX;
        public readonly float ViewZ;
        public readonly float Scale;
        private readonly float _cos;
        private readonly float _sin;

        public MapFrame(Rect rect, float viewX, float viewZ, float scale, float rotationRadians = 0.0f)
        {
            Rect = rect;
            Centre = rect.size * 0.5f;
            ViewX = viewX;
            ViewZ = viewZ;
            Scale = scale;
            _cos = Mathf.Cos(rotationRadians);
            _sin = Mathf.Sin(rotationRadians);
        }

        /// <summary>The turn that puts a world heading (x, z) straight up on the panel.</summary>
        public static float RotationPuttingUp(Vector2 heading)
        {
            if (heading.sqrMagnitude < 1e-6f)
            {
                return 0.0f;
            }
            return -Mathf.PI * 0.5f - Mathf.Atan2(heading.y, heading.x);
        }

        public Vector2 ToPanel(float worldX, float worldZ)
        {
            float dx = worldX - ViewX;
            float dz = worldZ - ViewZ;
            return Centre + new Vector2(dx * _cos - dz * _sin, dx * _sin + dz * _cos) * Scale;
        }

        /// <summary>A world direction turned onto the panel (no translation, no scale).</summary>
        public Vector2 TurnDirection(Vector2 worldDirection)
        {
            return new Vector2(worldDirection.x * _cos - worldDirection.y * _sin, worldDirection.x * _sin + worldDirection.y * _cos);
        }

        /// <summary>Panel pixels back to the world (x, z): what a click on the map points at.</summary>
        public Vector2 ToWorld(Vector2 panel)
        {
            Vector2 d = (panel - Centre) / Scale;
            return new Vector2(ViewX + d.x * _cos + d.y * _sin, ViewZ - d.x * _sin + d.y * _cos);
        }

        /// <summary>How far from the view point, in metres, the panel reaches at its corners.</summary>
        public float ReachMetres => Centre.magnitude / Mathf.Max(Scale, 1e-4f);

        public UIVertex Vertex(Vector2 panel, Color colour)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = colour;
            vertex.position = new Vector3(Rect.xMin + panel.x, Rect.yMax - panel.y, 0.0f);
            return vertex;
        }
    }

    /// <summary>
    /// Draws the city map into a UI mesh, for both the full map (M) and the corner minimap. What the research on game maps says matters and is
    /// done here (see docs/MAP.md):
    ///  * fog of war: only chunks the player has explored are drawn (the streamer keeps them);
    ///  * roads read clearly at every zoom (never thinner than a few pixels), buildings stand out from the ground with a soft drop shadow and are
    ///    shaded by height (GTA VI's raised buildings rather than flat linework);
    ///  * LANDMARKS are always marked, explored or not: the towers over 150 m that can be seen from across the city (a cognitive map is built
    ///    from landmarks -- Oueijan, "Stop Getting Lost", GDC);
    ///  * the waypoint's route is a bold line along the streets (GTA's GPS), the waypoint a pin.
    /// </summary>
    public static class MapPainter
    {
        public static readonly Color RoadColour = new Color(0.17f, 0.17f, 0.19f);
        public static readonly Color LotColour = new Color(0.30f, 0.29f, 0.27f);
        public static readonly Color BuildingLowColour = new Color(0.52f, 0.55f, 0.62f);
        public static readonly Color BuildingTallColour = new Color(0.88f, 0.91f, 0.98f);
        public static readonly Color ShadowColour = new Color(0.0f, 0.0f, 0.0f, 0.35f);
        public static readonly Color PlayerColour = new Color(1.0f, 0.85f, 0.2f);
        public static readonly Color RouteColour = new Color(0.72f, 0.35f, 1.0f);
        public static readonly Color WaypointColour = new Color(0.85f, 0.5f, 1.0f);
        public static readonly Color LandmarkColour = new Color(1.0f, 0.62f, 0.25f);

        public const float BuildingMinHeight = 8.0f;
        public const float BuildingMaxHeight = 96.0f;

        /// <summary>A street never draws thinner than this many pixels, however far out the map is zoomed ("make the streets thicker than 1 pixel").</summary>
        public const float StreetMinPixelWidth = 3.5f;

        /// <summary>How far a building's shadow falls, in pixels, down and to the right, as if lit from the top left.</summary>
        public const float ShadowPixels = 2.5f;

        /// <summary>The ground, roads and buildings of every explored chunk the frame can see.</summary>
        public static void DrawCity(VertexHelper vh, MapFrame frame, CityWorld world, CityStreamer streamer)
        {
            float chunk = CityLayout.ChunkSize;
            float reach = frame.ReachMetres + chunk * 1.5f;
            int minX = Mathf.FloorToInt((frame.ViewX - reach) / chunk);
            int maxX = Mathf.FloorToInt((frame.ViewX + reach) / chunk);
            int minZ = Mathf.FloorToInt((frame.ViewZ - reach) / chunk);
            int maxZ = Mathf.FloorToInt((frame.ViewZ + reach) / chunk);
            var visible = new List<Vector2Int>();
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var coord = new Vector2Int(x, z);
                    if (streamer.IsChunkExplored(coord))
                    {
                        visible.Add(coord);
                    }
                }
            }

            foreach (Vector2Int c in visible)
            {
                float x0 = c.x * chunk, z0 = c.y * chunk;
                // A hair over a chunk so turned neighbours never show a seam.
                const float overlap = 0.15f;
                Quad(vh, frame, frame.ToPanel(x0 - overlap, z0 - overlap), frame.ToPanel(x0 + chunk + overlap, z0 - overlap),
                    frame.ToPanel(x0 + chunk + overlap, z0 + chunk + overlap), frame.ToPanel(x0 - overlap, z0 + chunk + overlap), LotColour);
            }

            var drawnRoads = new HashSet<int>();
            foreach (Vector2Int c in visible)
            {
                foreach (int edgeId in world.Plan.EdgesIn(c.x, c.y))
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
                    float half = Mathf.Max((float)g.HalfWidth, StreetMinPixelWidth * 0.5f / frame.Scale);
                    Segment(vh, frame, new Vector2((float)g.A.X, (float)g.A.Y), new Vector2((float)g.B.X, (float)g.B.Y), half, RoadColour, worldWidth: true);
                }
            }

            var drawnBuildings = new HashSet<int>();
            var shadows = new List<Vector2[]>();
            var roofs = new List<(Vector2[] Corners, Color Colour)>();
            foreach (Vector2Int c in visible)
            {
                List<int> owned = world.BuildingsOwnedBy(c.x, c.y);
                for (int i = 0; i < owned.Count; i++)
                {
                    if (!drawnBuildings.Add(owned[i]))
                    {
                        continue;
                    }
                    BuildingSite site = world.Building(owned[i]);
                    float hw = site.Params.Width * 0.5f;
                    float hd = site.Params.Depth * 0.5f;
                    var corners = new[]
                    {
                        Corner(site, -hw, -hd, frame), Corner(site, hw, -hd, frame), Corner(site, hw, hd, frame), Corner(site, -hw, hd, frame),
                    };
                    float tallness = Mathf.Clamp01((site.Params.Height - BuildingMinHeight) / (BuildingMaxHeight - BuildingMinHeight));
                    shadows.Add(corners);
                    roofs.Add((corners, Color.Lerp(BuildingLowColour, BuildingTallColour, tallness)));
                }
            }
            // Every shadow under every roof, so a tall building's shadow never lands on top of its neighbour.
            var drop = new Vector2(ShadowPixels, ShadowPixels);
            foreach (Vector2[] s in shadows)
            {
                Quad(vh, frame, s[0] + drop, s[1] + drop, s[2] + drop, s[3] + drop, ShadowColour);
            }
            foreach ((Vector2[] corners, Color colour) in roofs)
            {
                Quad(vh, frame, corners[0], corners[1], corners[2], corners[3], colour);
            }
        }

        /// <summary>Every landmark tower in the city as a diamond, explored or not: they can be seen from anywhere.</summary>
        public static void DrawLandmarks(VertexHelper vh, MapFrame frame, CityWorld world, float sizePixels)
        {
            foreach ((int X, int Y) chunk in world.Plan.LandmarkChunks)
            {
                foreach (int id in world.BuildingsOwnedBy(chunk.X, chunk.Y))
                {
                    BuildingSite site = world.Building(id);
                    if (site.Params.Height < CityX.World.Plan.PlanWorld.LandmarkHeight)
                    {
                        continue;
                    }
                    site.LocalToWorld(0.0, 0.0, out double wx, out double wz);
                    Vector2 at = frame.ToPanel((float)wx, (float)wz);
                    Diamond(vh, frame, at, sizePixels + 1.5f, new Color(0.0f, 0.0f, 0.0f, 0.6f));
                    Diamond(vh, frame, at, sizePixels, LandmarkColour);
                }
            }
        }

        /// <summary>The waypoint's route along the streets: a bold line with a dark edge, so it reads over roads and roofs alike.</summary>
        public static void DrawRoute(VertexHelper vh, MapFrame frame, IReadOnlyList<Vector2> route, float widthPixels)
        {
            if (route == null || route.Count < 2)
            {
                return;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                float half = (pass == 0 ? widthPixels + 2.0f : widthPixels) * 0.5f;
                Color colour = pass == 0 ? new Color(0.1f, 0.0f, 0.18f, 0.85f) : RouteColour;
                for (int i = 1; i < route.Count; i++)
                {
                    Segment(vh, frame, route[i - 1], route[i], half, colour, worldWidth: false);
                    Disc(vh, frame, frame.ToPanel(route[i].x, route[i].y), half, colour);
                }
            }
        }

        /// <summary>The waypoint: a pin (a disc on a point) at its place, or, when <paramref name="clampRadius"/> is set and it is off the panel, at the edge pointing to it.</summary>
        public static void DrawWaypoint(VertexHelper vh, MapFrame frame, Vector2 world, float sizePixels, float clampRadius = 0.0f)
        {
            Vector2 at = frame.ToPanel(world.x, world.y);
            if (clampRadius > 0.0f)
            {
                Vector2 offset = at - frame.Centre;
                if (offset.magnitude > clampRadius)
                {
                    at = frame.Centre + offset.normalized * clampRadius;
                }
            }
            Vector2 tip = at;
            Vector2 head = at + new Vector2(0.0f, -sizePixels * 1.4f);
            Triangle(vh, frame, tip, head + new Vector2(-sizePixels * 0.55f, 0.0f), head + new Vector2(sizePixels * 0.55f, 0.0f), new Color(0.1f, 0.0f, 0.15f, 0.9f));
            Disc(vh, frame, head, sizePixels * 0.75f + 1.5f, new Color(0.1f, 0.0f, 0.15f, 0.9f));
            Triangle(vh, frame, tip + new Vector2(0.0f, -1.5f), head + new Vector2(-sizePixels * 0.4f, 0.0f), head + new Vector2(sizePixels * 0.4f, 0.0f), WaypointColour);
            Disc(vh, frame, head, sizePixels * 0.75f, WaypointColour);
            Disc(vh, frame, head, sizePixels * 0.3f, Color.white);
        }

        /// <summary>The player: an arrow at their place pointing the way they face, with a dark rim so it reads on anything.</summary>
        public static void DrawPlayer(VertexHelper vh, MapFrame frame, Vector3 position, Vector3 forward, float sizePixels)
        {
            Vector2 at = frame.ToPanel(position.x, position.z);
            Vector2 facing = frame.TurnDirection(new Vector2(forward.x, forward.z));
            if (facing.sqrMagnitude < 1e-6f)
            {
                facing = new Vector2(0.0f, -1.0f);
            }
            facing.Normalize();
            var side = new Vector2(-facing.y, facing.x);
            for (int pass = 0; pass < 2; pass++)
            {
                float s = pass == 0 ? sizePixels + 2.0f : sizePixels;
                Color colour = pass == 0 ? new Color(0.0f, 0.0f, 0.0f, 0.8f) : PlayerColour;
                Vector2 tip = at + facing * s;
                Vector2 notch = at - facing * (s * 0.3f);
                Vector2 left = at - facing * (s * 0.7f) + side * (s * 0.7f);
                Vector2 right = at - facing * (s * 0.7f) - side * (s * 0.7f);
                Triangle(vh, frame, tip, left, notch, colour);
                Triangle(vh, frame, tip, notch, right, colour);
            }
        }

        public static readonly Color CompassNorthColour = new Color(1.0f, 0.4f, 0.35f);
        public static readonly Color CompassColour = new Color(0.86f, 0.89f, 0.96f);

        /// <summary>Where north (world -Z) points on the panel, as a unit direction: straight up unless the frame is turned.</summary>
        public static Vector2 NorthOnPanel(MapFrame frame) => frame.TurnDirection(new Vector2(0.0f, -1.0f)).normalized;

        /// <summary>
        /// A COMPASS ROSE (owner, 2026-10-30: "is there a compass on the map? if not there probably should be"): a four-pointed star on a dark disc,
        /// its north point red, each point shaded in two halves so it reads as raised; <paramref name="north"/> is the panel direction north points.
        /// </summary>
        public static void DrawCompass(VertexHelper vh, MapFrame frame, Vector2 centre, float radius, Vector2 north)
        {
            north = north.sqrMagnitude > 1e-6f ? north.normalized : new Vector2(0.0f, -1.0f);
            Vector2 east = new Vector2(-north.y, north.x);
            Disc(vh, frame, centre, radius, new Color(0.75f, 0.8f, 0.95f, 0.5f), 32);
            Disc(vh, frame, centre, radius - 1.5f, new Color(0.03f, 0.035f, 0.05f, 0.85f), 32);
            Vector2[] points = { north, east, -north, -east };
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < 4; i++)
                {
                    float length = radius * (i % 2 == 0 ? 0.82f : 0.58f) + (pass == 0 ? 1.5f : 0.0f);
                    float half = radius * 0.17f + (pass == 0 ? 1.5f : 0.0f);
                    Vector2 tip = centre + points[i] * length;
                    Vector2 side = new Vector2(-points[i].y, points[i].x) * half;
                    Color light = pass == 0 ? new Color(0.0f, 0.0f, 0.0f, 0.8f) : i == 0 ? CompassNorthColour : CompassColour;
                    Color dark = pass == 0 ? light : Color.Lerp(light, Color.black, 0.35f);
                    Triangle(vh, frame, centre, tip, centre + side, light);
                    Triangle(vh, frame, centre, centre - side, tip, dark);
                }
            }
            Disc(vh, frame, centre, radius * 0.08f, new Color(0.03f, 0.035f, 0.05f));
        }

        // ---- shapes ----

        private static Vector2 Corner(BuildingSite site, float localX, float localZ, MapFrame frame)
        {
            site.LocalToWorld(localX, localZ, out double wx, out double wz);
            return frame.ToPanel((float)wx, (float)wz);
        }

        /// <summary>A strip from world point a to b, <paramref name="half"/> wide either side (metres if <paramref name="worldWidth"/>, else pixels).</summary>
        private static void Segment(VertexHelper vh, MapFrame frame, Vector2 a, Vector2 b, float half, Color colour, bool worldWidth)
        {
            Vector2 pa = frame.ToPanel(a.x, a.y);
            Vector2 pb = frame.ToPanel(b.x, b.y);
            Vector2 along = pb - pa;
            if (along.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector2 across = new Vector2(-along.y, along.x).normalized * (worldWidth ? half * frame.Scale : half);
            Quad(vh, frame, pa + across, pb + across, pb - across, pa - across, colour);
        }

        public static void Quad(VertexHelper vh, MapFrame frame, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
        {
            int start = vh.currentVertCount;
            vh.AddVert(frame.Vertex(a, colour));
            vh.AddVert(frame.Vertex(b, colour));
            vh.AddVert(frame.Vertex(c, colour));
            vh.AddVert(frame.Vertex(d, colour));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        public static void Triangle(VertexHelper vh, MapFrame frame, Vector2 a, Vector2 b, Vector2 c, Color colour)
        {
            int start = vh.currentVertCount;
            vh.AddVert(frame.Vertex(a, colour));
            vh.AddVert(frame.Vertex(b, colour));
            vh.AddVert(frame.Vertex(c, colour));
            vh.AddTriangle(start, start + 1, start + 2);
        }

        public static void Disc(VertexHelper vh, MapFrame frame, Vector2 centre, float radius, Color colour, int segments = 12)
        {
            int start = vh.currentVertCount;
            vh.AddVert(frame.Vertex(centre, colour));
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2.0f / segments;
                vh.AddVert(frame.Vertex(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, colour));
                if (i > 0)
                {
                    vh.AddTriangle(start, start + i, start + i + 1);
                }
            }
        }

        private static void Diamond(VertexHelper vh, MapFrame frame, Vector2 centre, float radius, Color colour)
        {
            Quad(vh, frame, centre + new Vector2(0.0f, -radius), centre + new Vector2(radius, 0.0f), centre + new Vector2(0.0f, radius),
                centre + new Vector2(-radius, 0.0f), colour);
        }
    }
}
