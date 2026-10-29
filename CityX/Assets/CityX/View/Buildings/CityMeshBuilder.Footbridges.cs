using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The footbridges between buildings (World/Footbridges, the layered city): a deck along the bridge's path from a doorway in one building's
    /// floor across the street to a doorway in the other's -- straight, on the slant, round a dog-leg's two landings or on an S-curve, mitred at
    /// each turn and cut along the walls at its ends; its flights of steps where the floors differ; its guard rails (a concrete parapet, a
    /// catwalk's rails, pickets, mesh panels or pipe rails); piers under its landings and long spans; and the stair down along the wall to the
    /// street where it has one. Drawn by the lower-numbered building of the pair, in its own frame, into the wall buffer (so it is in the chunk's
    /// collider: it is walked); near and far alike, the far ones plainer (solid panels for the rails, ramps for the flights).
    ///
    /// A CORRIDOR (BridgeKind.Enclosed, BridgeKind.Breezeway; 2026-10-23, the owner's photo of Chongqing's bridges) has walls along both edges in
    /// place of the rails, in the look the two buildings share, a long window down each from a sill to a lintel -- glazed in bays between thin
    /// mullions, its glass in the dark glass buffer (so it stops a body, like a window), or open to the air between concrete piers -- and a roof
    /// slab over them; inside, an enclosed one wears the pale of the rooms it joins. Where a street stair leaves the deck the wall is open as the
    /// rail would be. Far off, plain walls and the roof.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        private static readonly Color BridgeConcrete = new Color(0.60f, 0.59f, 0.56f);
        private static readonly Color BridgeUnderside = new Color(0.50f, 0.50f, 0.48f);
        private static readonly Color BridgeRailTint = new Color(0.30f, 0.30f, 0.32f);
        private static readonly Color BridgeSteel = new Color(0.36f, 0.37f, 0.39f);
        private static readonly Color BridgeMesh = new Color(0.22f, 0.23f, 0.24f);

        /// <summary>The rails: posts this far apart (pipe rails further), the bars' and posts' thickness, pickets' spacing and size.</summary>
        private const float RailPostSpacing = 1.6f;
        private const float PipePostSpacing = 2.0f;
        private const float RailBar = 0.06f;
        private const float RailPost = 0.08f;
        private const float PicketSpacing = 0.14f;
        private const float PicketSize = 0.035f;

        /// <summary>A steel catwalk's plate and the depth of the two beams under its edges.</summary>
        private const float SteelPlate = 0.06f;
        private const float SteelBeamDepth = 0.32f;
        private const float SteelBeamWidth = 0.12f;

        /// <summary>
        /// The bridges a building decides (its id the lower of the pair), drawn from its own frame; a corridor's glass bay by bay as its damage has
        /// it, through <paramref name="glazing"/> (none far off).
        /// </summary>
        private static void AddFootbridges(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer buffer, in Glazing glazing, in SiteLooks looks, bool far)
        {
            Footbridge[] bridges = Footbridges.For(world, site);
            if (bridges.Length == 0)
            {
                return;
            }
            Color32 saved = buffer.Look;
            for (int i = 0; i < bridges.Length; i++)
            {
                if (bridges[i].SiteA == site.Id)
                {
                    DrawBridge(world, site, origin, buffer, glazing, looks, bridges[i], far);
                }
            }
            buffer.Look = saved;
        }

        /// <summary>The looks of a bridge: its deck's top and underside, its rails, its piers; and a corridor's walls, windows, roof and inside.</summary>
        private readonly struct BridgeLooks
        {
            public readonly Color32 Deck, Under, Rail, Pier, Mesh;

            /// <summary>
            /// A corridor's: the buildings' own wall outside (the two share one look), their window frames, glass and roof; inside, the pale walls,
            /// ceiling and floor of the rooms it joins, lit as the outdoors is (its windows are all round it, not a room with its lamp).
            /// </summary>
            public readonly Color32 Wall, Frame, Glass, Roof, Inside, Ceiling, Floor;

            public BridgeLooks(BridgeDeck deck, BuildingSite site, in SiteLooks building)
            {
                bool steel = deck == BridgeDeck.Steel;
                Deck = steel ? SurfaceLook.Pack(BridgeSteel, (int)WallMaterial.Metal, 0.6f) : SurfaceLook.Pack(BridgeConcrete, (int)WallMaterial.Concrete, 0.5f);
                Under = steel ? SurfaceLook.Pack(BridgeRailTint, (int)WallMaterial.Metal, 0.7f) : SurfaceLook.Pack(BridgeUnderside, (int)WallMaterial.Concrete, 0.6f);
                Rail = SurfaceLook.Pack(BridgeRailTint, (int)WallMaterial.Metal, 0.5f);
                Pier = steel ? Rail : SurfaceLook.Pack(BridgeUnderside, (int)WallMaterial.Concrete, 0.6f);
                Mesh = SurfaceLook.Pack(BridgeMesh, (int)WallMaterial.Metal, 0.6f);
                Wall = building.Wall;
                Frame = building.Frame;
                Glass = SurfaceLook.PackGlass(building.GlassTint, 0, 0.0f, true);
                Roof = building.Roof;
                InteriorLooks rooms = InteriorLooks.For(site);
                Inside = Outdoors(rooms.Wall);
                Ceiling = Outdoors(rooms.Ceiling);
                Floor = Outdoors(rooms.Floor);
            }

            private static Color32 Outdoors(Color32 look)
            {
                SurfaceLook.Unpack(look, out Color32 tint, out int slice, out float wear);
                return SurfaceLook.Pack(tint, slice, wear);
            }
        }

        private static Vector3 BridgeLocal(BuildingSite site, Vector3 origin, Vector3 world)
        {
            site.WorldToLocal(world.x, world.z, out double lx, out double lz);
            return new Vector3((float)lx, world.y - origin.y, (float)lz);
        }

        private static Vector3 BridgeLocalDirection(BuildingSite site, Vector2 world)
        {
            site.WorldToLocal(site.CentreX + world.x, site.CentreZ + world.y, out double lx, out double lz);
            return new Vector3((float)lx, 0.0f, (float)lz).normalized;
        }

        private static Vector3 BridgeLocalDirection(BuildingSite site, Vector3 world) => BridgeLocalDirection(site, new Vector2(world.x, world.z));

        private static Vector3 FlatOf(Vector3 v) => BridgeOutline.FlatOf(v);

        private static Vector3 LeftOf(Vector3 d) => BridgeOutline.LeftOf(d);

        private static void DrawBridge(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer buffer, in Glazing glazing, in SiteLooks building,
            in Footbridge b, bool far)
        {
            BridgePath path = b.Path;
            var looks = new BridgeLooks(b.Deck, site, building);
            int n = path.Points.Length;
            // The walking line (pushed a little into each wall, so the threshold has no gap) and its edges: the world's (BridgeOutline), in this frame.
            BridgeOutline.Of(b, out Vector3[] worldCentre, out Vector3[] worldLeft, out Vector3[] worldRight);
            var left = new Vector3[n];
            var right = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                left[i] = BridgeLocal(site, origin, worldLeft[i]);
                right[i] = BridgeLocal(site, origin, worldRight[i]);
            }

            // The deck, stretch by stretch: a slab (a steel plate on two beams; an enclosed corridor's in its rooms' floor), or a flight's steps.
            Color32 floor = b.Kind == BridgeKind.Enclosed ? looks.Floor : looks.Deck;
            for (int i = 0; i + 1 < n; i++)
            {
                if (path.Risers[i] > 0)
                {
                    AddFlight(buffer, left[i], right[i], right[i + 1], left[i + 1], path.Risers[i], looks, far);
                    continue;
                }
                AddDeckSlab(buffer, left[i], right[i], right[i + 1], left[i + 1], b.Deck, floor, looks);
            }

            // Along both edges, set in from them, the rails (an open deck) or the corridor's walls; where a street stair leaves the deck by A's
            // wall, that side is open there (BridgeOutline.EdgeStretches).
            var stretches = new System.Collections.Generic.List<BridgeEdgeStretch>();
            BridgeOutline.EdgeStretches(b, worldCentre, worldLeft, worldRight, stretches);
            foreach (BridgeEdgeStretch s in stretches)
            {
                Vector3 p = BridgeLocal(site, origin, s.P), q = BridgeLocal(site, origin, s.Q);
                Vector3 inward = BridgeLocalDirection(site, s.Inward);
                if (b.Kind == BridgeKind.Open)
                {
                    AddBridgeRail(buffer, b.Rail, p, q, inward, looks, far, s.Stretch + 2 == n);
                }
                else
                {
                    AddCorridorWall(buffer, b.Kind, p, q, inward, b.Headroom, looks, far);
                }
            }
            if (b.Kind != BridgeKind.Open)
            {
                AddCorridorRoof(buffer, b.Headroom, left, right, looks.Roof, b.Kind == BridgeKind.Enclosed ? looks.Ceiling : looks.Under);
            }
            if (b.Kind == BridgeKind.Enclosed && !far)
            {
                AddBridgeGlass(site, origin, glazing, b, looks.Glass);
            }

            // The piers, from the ground up to the deck they hold.
            foreach (Vector3 pier in path.Piers)
            {
                Vector3 top = BridgeLocal(site, origin, pier);
                float ground = (float)world.Plan.GroundHeight(pier.x, pier.z) - 0.3f - origin.y;
                if (top.y - ground < 1.0f) continue;
                float s = b.Deck == BridgeDeck.Steel ? 0.2f : Footbridges.PierSize * 0.5f;
                buffer.Look = looks.Pier;
                buffer.AddBox(new Vector3(top.x - s, ground, top.z - s), new Vector3(top.x + s, top.y, top.z + s));
            }

            if (b.Stair != null && b.Stair.Site == site.Id)
            {
                DrawStreetStair(world, site, origin, buffer, b.Stair, b.Rail, looks, far);
            }
        }

        /// <summary>A stretch of deck: a concrete slab, or a steel plate on a beam under each edge; its walking surface in <paramref name="top"/>.</summary>
        private static void AddDeckSlab(MeshBuffer buffer, Vector3 a, Vector3 b, Vector3 c, Vector3 d, BridgeDeck deck, Color32 top, BridgeLooks looks)
        {
            if (deck == BridgeDeck.Concrete)
            {
                AddSlopedSlab(buffer, a, b, c, d, Footbridges.DeckThickness, top, looks.Under);
                return;
            }
            AddSlopedSlab(buffer, a, b, c, d, SteelPlate, top, looks.Under);
            // a-d runs along one edge, b-c along the other: a beam under each, set in a little.
            Vector3 drop = Vector3.down * SteelPlate;
            foreach ((Vector3 p, Vector3 q, Vector3 pIn, Vector3 qIn) in new[] { (a, d, b, c), (b, c, a, d) })
            {
                Vector3 inP = (pIn - p).normalized, inQ = (qIn - q).normalized;
                Vector3 p0 = p + inP * 0.05f + drop, q0 = q + inQ * 0.05f + drop;
                Vector3 p1 = p0 + inP * SteelBeamWidth, q1 = q0 + inQ * SteelBeamWidth;
                AddSlopedSlab(buffer, p0, p1, q1, q0, SteelBeamDepth, looks.Under, looks.Under);
            }
        }

        /// <summary>
        /// A flight of <paramref name="risers"/> steps across the quad a (start, one edge), b (start, the other), c (end, other), d (end, one):
        /// each step a block (its top level, a riser and a little under it) from the start's height to the end's; far off, a ramp.
        /// </summary>
        private static void AddFlight(MeshBuffer buffer, Vector3 a, Vector3 b, Vector3 c, Vector3 d, int risers, BridgeLooks looks, bool far)
        {
            if (far)
            {
                AddSlopedSlab(buffer, a, b, c, d, Footbridges.DeckThickness, looks.Deck, looks.Under);
                return;
            }
            float rise = (d.y - a.y) / risers;
            float depth = Mathf.Abs(rise) + 0.22f;
            for (int k = 0; k < risers; k++)
            {
                float s0 = k / (float)risers, s1 = (k + 1) / (float)risers;
                float top = a.y + rise * (k + 1);
                Vector3 p0 = Vector3.Lerp(a, d, s0), p1 = Vector3.Lerp(a, d, s1);
                Vector3 q0 = Vector3.Lerp(b, c, s0), q1 = Vector3.Lerp(b, c, s1);
                p0.y = p1.y = q0.y = q1.y = top;
                AddSlopedSlab(buffer, p0, q0, q1, p1, depth, looks.Deck, looks.Under);
            }
        }

        /// <summary>
        /// A guard rail along one edge from <paramref name="p"/> to <paramref name="q"/> (at the walking surface; <paramref name="inward"/> toward the
        /// deck's middle): a parapet, a catwalk's rails, pickets, a mesh panel or pipe rails. Posts at the start and along it (and at its end when it is
        /// the last stretch); far off, the open kinds are a plain panel.
        /// </summary>
        private static void AddBridgeRail(MeshBuffer buffer, BridgeRail kind, Vector3 p, Vector3 q, Vector3 inward, BridgeLooks looks, bool far, bool last)
        {
            float h = Footbridges.ParapetHeight;
            Vector3 up = Vector3.up;
            if (kind == BridgeRail.Parapet)
            {
                float t = Footbridges.ParapetThickness;
                AddSlopedSlab(buffer, p + up * h, p + inward * t + up * h, q + inward * t + up * h, q + up * h, h, looks.Deck, looks.Deck);
                if (!far)
                {
                    Vector3 cap = up * (h + 0.06f) + inward * (t * 0.5f - 0.03f);
                    AddSlopedSlab(buffer, p + cap, p + cap + inward * 0.06f, q + cap + inward * 0.06f, q + cap, 0.06f, looks.Rail, looks.Rail);
                }
                return;
            }
            Vector3 line0 = p + inward * 0.06f, line1 = q + inward * 0.06f;
            if (far)
            {
                // A thin panel the rail's height: from afar a rail reads as its outline.
                AddSlopedSlab(buffer, line0 + up * h, line0 + inward * 0.03f + up * h, line1 + inward * 0.03f + up * h, line1 + up * h, h - 0.1f,
                    kind == BridgeRail.Mesh ? looks.Mesh : looks.Rail, looks.Rail);
                return;
            }
            float length = FlatOf(q - p).magnitude;
            float spacing = kind == BridgeRail.Pipe ? PipePostSpacing : RailPostSpacing;
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
            buffer.Look = looks.Rail;
            for (int j = 0; j <= posts; j++)
            {
                if (j == posts && !last) break;
                Vector3 at = Vector3.Lerp(line0, line1, j / (float)posts);
                float post = kind == BridgeRail.Pipe ? 0.07f : RailPost;
                buffer.AddBox(new Vector3(at.x - post * 0.5f, at.y, at.z - post * 0.5f), new Vector3(at.x + post * 0.5f, at.y + h + 0.03f, at.z + post * 0.5f));
            }
            // A horizontal bar along the rail at a height, its cross-section w across and t deep.
            void Bar(float y, float w, float t, Color32 look)
            {
                Vector3 o = up * y - inward * (w * 0.5f);
                AddSlopedSlab(buffer, line0 + o, line0 + o + inward * w, line1 + o + inward * w, line1 + o, t, look, look);
            }
            switch (kind)
            {
                case BridgeRail.Rails:
                    Bar(h, RailBar, RailBar, looks.Rail);
                    Bar(h * 0.55f, RailBar * 0.8f, RailBar * 0.8f, looks.Rail);
                    Bar(0.14f, 0.025f, 0.14f, looks.Rail);   // the toe board
                    break;
                case BridgeRail.Pickets:
                {
                    Bar(h, RailBar, RailBar, looks.Rail);
                    Bar(0.12f, RailBar, RailBar, looks.Rail);
                    int pickets = Mathf.Max(1, Mathf.RoundToInt(length / PicketSpacing));
                    for (int j = 1; j < pickets; j++)
                    {
                        Vector3 at = Vector3.Lerp(line0, line1, j / (float)pickets);
                        buffer.AddBox(new Vector3(at.x - PicketSize * 0.5f, at.y + 0.12f, at.z - PicketSize * 0.5f),
                            new Vector3(at.x + PicketSize * 0.5f, at.y + h - RailBar, at.z + PicketSize * 0.5f));
                    }
                    break;
                }
                case BridgeRail.Mesh:
                    Bar(h, RailBar, RailBar, looks.Rail);
                    Bar(0.12f, RailBar, RailBar, looks.Rail);
                    Bar(h - RailBar - 0.02f, 0.025f, h - RailBar - 0.14f, looks.Mesh);
                    break;
                default:
                    Bar(1.2f, RailBar, RailBar, looks.Rail);
                    Bar(0.8f, RailBar, RailBar, looks.Rail);
                    Bar(0.4f, RailBar, RailBar, looks.Rail);
                    break;
            }
        }

        /// <summary>
        /// A corridor's wall along one edge from <paramref name="p"/> to <paramref name="q"/> (at the walking surface; <paramref name="inward"/> toward
        /// the deck's middle): a solid wall under the window sill and a lintel under the roof, and between them the long window in bays -- the
        /// mullions of a glazed one (its glass is AddBridgeGlass's), or concrete piers open to the air between them (the breezeway). Far off, a plain
        /// wall the whole height.
        /// </summary>
        private static void AddCorridorWall(MeshBuffer buffer, BridgeKind kind, Vector3 p, Vector3 q, Vector3 inward, float headroom,
            BridgeLooks looks, bool far)
        {
            float t = Footbridges.CorridorWallThickness;
            bool glazed = kind == BridgeKind.Enclosed;
            Color32 inside = glazed ? looks.Inside : looks.Wall;
            if (far)
            {
                AddWallBox(buffer, p, q, inward, 0.0f, headroom, t, looks.Wall, looks.Wall);
                return;
            }
            float sill = Footbridges.CorridorSill, head = headroom - Footbridges.CorridorLintel;
            AddWallBox(buffer, p, q, inward, 0.0f, sill, t, looks.Wall, inside);
            AddWallBox(buffer, p, q, inward, head, headroom, t, looks.Wall, inside);

            // The posts between the bays: half a post at each end (its other half in the next stretch's wall, or in the building's).
            float length = FlatOf(q - p).magnitude;
            int bays = BridgeOutline.CorridorBays(length);
            float half = (glazed ? Footbridges.CorridorMullion : Footbridges.BreezewayPier) * 0.5f / length;
            Color32 post = glazed ? looks.Frame : looks.Wall;
            for (int j = 0; j <= bays; j++)
            {
                float s = j / (float)bays;
                AddWallBox(buffer, Vector3.Lerp(p, q, Mathf.Clamp01(s - half)), Vector3.Lerp(p, q, Mathf.Clamp01(s + half)), inward, sill, head, t, post, post);
            }
        }

        /// <summary>
        /// An enclosed bridge's glass, bay by bay between its mullions (World/BridgeGlass), each as its damage has it, the way a window is drawn
        /// (AddPane): whole, clear in the wall's middle plane, both faces, the corridor showing through it like a room; cracked, in the cracked
        /// glass with a real hole and a crack decal at every bullet that hit it, and a trigger box a knock finds (a cracked bay is walkable); gone,
        /// nothing between the mullions. A bay on a flight is slanted: cracked, it has no holes cut in it.
        /// </summary>
        private static void AddBridgeGlass(BuildingSite site, Vector3 origin, in Glazing glazing, in Footbridge b, Color32 glassLook)
        {
            if (glazing.Dark == null)
            {
                return;
            }
            var panes = new System.Collections.Generic.List<BridgePane>();
            BridgeGlass.Panes(b, panes);
            foreach (BridgePane pane in panes)
            {
                PaneState state = glazing.Damage == null ? PaneState.Intact : glazing.Damage.StateOf(pane.Key);
                if (state == PaneState.Shattered)
                {
                    continue;
                }
                Vector3 start = BridgeLocal(site, origin, pane.Start), end = BridgeLocal(site, origin, pane.End);
                Vector3 normal = BridgeLocalDirection(site, pane.Normal);
                Vector3 up = Vector3.up * pane.Height;
                float w = pane.Width, h = pane.Height;
                if (state != PaneState.Cracked)
                {
                    Color32 saved = glazing.Dark.Look;
                    glazing.Dark.Look = glassLook;
                    glazing.Dark.AddQuad(start, end, end + up, start + up, normal, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
                    glazing.Dark.AddQuad(start, end, end + up, start + up, -normal, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
                    glazing.Dark.Look = saved;
                    continue;
                }
                MeshBuffer cracked = glazing.Cracked;
                if (cracked == null)
                {
                    continue;
                }
                Vector3 across = FlatOf(end - start).normalized;
                var sheet = new Wall(start, across, w);
                float y0 = start.y, y1 = start.y + h;
                if (pane.Level && glazing.Damage.TryGetImpacts(pane.Key, out Vector2[] impacts))
                {
                    var centres = new Vector2[impacts.Length];
                    for (int k = 0; k < impacts.Length; k++)
                    {
                        centres[k] = ClampedHoleCentre(0.0f, w, y0, y1, Rect.MinMaxRect(0.0f, y0, w, y1), impacts[k]);
                    }
                    AddPaneWithHoles(cracked, sheet, 0.0f, w, y0, y1, normal, centres);
                    if (glazing.Decals != null)
                    {
                        foreach (Vector2 centre in centres)
                        {
                            AddCrackDecal(glazing.Decals, sheet, centre, normal);
                            AddCrackDecal(glazing.Decals, sheet, centre, -normal);
                        }
                    }
                }
                else
                {
                    AddDoubleSidedFace(cracked, normal, start, end, end + up, start + up);
                }
                if (glazing.Openings != null)
                {
                    // The box a knock or a bullet finds, turned from the key's wall onto the bay (CrackedPane measures a knock through the pane
                    // along its turn of the key's wall's normal).
                    Vector3 keyNormal = WindowGrid.OutwardNormal(pane.Key.Wall);
                    Vector3 centre3 = (start + end + up) * 0.5f;
                    var turn = Quaternion.Euler(0.0f, Vector3.SignedAngle(keyNormal, normal, Vector3.up), 0.0f);
                    float tall = h + Mathf.Abs(end.y - start.y);
                    Vector3 size = Mathf.Abs(keyNormal.z) > 0.5f ? new Vector3(w, tall, CrackedOpeningDepth) : new Vector3(CrackedOpeningDepth, tall, w);
                    glazing.Openings.Add(new CrackedOpening(pane.Key, centre3, size, turn));
                }
            }
        }

        /// <summary>
        /// A length of wall standing on the line <paramref name="p"/>..<paramref name="q"/> (its outer face on it, <paramref name="thickness"/> toward
        /// <paramref name="inward"/>), from <paramref name="y0"/> to <paramref name="y1"/> over the line (which may slope): its outer face in one look,
        /// its inner in another, closed at its ends and top; no underside where it stands on the deck.
        /// </summary>
        private static void AddWallBox(MeshBuffer s, Vector3 p, Vector3 q, Vector3 inward, float y0, float y1, float thickness, Color32 outside, Color32 inside)
        {
            if (y1 - y0 <= 0.001f || FlatOf(q - p).sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector3 lift0 = Vector3.up * y0, lift1 = Vector3.up * y1, deep = inward * thickness;
            Vector3 along = FlatOf(q - p).normalized;
            s.Look = outside;
            AddFace(s, -inward, p + lift0, q + lift0, q + lift1, p + lift1);
            AddFace(s, Vector3.up, p + lift1, q + lift1, q + deep + lift1, p + deep + lift1);
            AddFace(s, -along, p + lift0, p + lift1, p + deep + lift1, p + deep + lift0);
            AddFace(s, along, q + lift0, q + lift1, q + deep + lift1, q + deep + lift0);
            if (y0 > 0.001f)
            {
                AddFace(s, Vector3.down, p + lift0, q + lift0, q + deep + lift0, p + deep + lift0);
            }
            s.Look = inside;
            AddFace(s, inward, p + deep + lift0, q + deep + lift0, q + deep + lift1, p + deep + lift1);
        }

        /// <summary>A corridor's roof: a slab over its walls the deck's full width, stretch by stretch, its underside the ceiling.</summary>
        private static void AddCorridorRoof(MeshBuffer buffer, float headroom, Vector3[] left, Vector3[] right, Color32 roof, Color32 ceiling)
        {
            Vector3 up = Vector3.up * (headroom + Footbridges.CorridorRoofThickness);
            for (int i = 0; i + 1 < left.Length; i++)
            {
                AddSlopedSlab(buffer, left[i] + up, right[i] + up, right[i + 1] + up, left[i + 1] + up, Footbridges.CorridorRoofThickness, roof, ceiling);
            }
        }

        /// <summary>
        /// A street stair (World/Footbridges.StreetStair): its flights of steps and landings down along the wall from the bridge to the paving, a
        /// rail along each open side (between a tower's two lanes, along its outer side, round its landings' open ends), and posts down to the ground
        /// under each flight's middle and each landing's outer corners.
        /// </summary>
        private static void DrawStreetStair(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer buffer, StreetStair stair, BridgeRail rail,
            BridgeLooks looks, bool far)
        {
            float half = stair.Width * 0.5f;
            Vector3 outward = BridgeLocalDirection(site, stair.Out);
            var posts = new System.Collections.Generic.List<Vector3>();   // world: x, the underside's height, z

            foreach (StairFlight f in stair.Flights)
            {
                Vector3 p = BridgeLocal(site, origin, f.From), q = BridgeLocal(site, origin, f.To);
                Vector3 dir = FlatOf(q - p).normalized;
                Vector3 left = LeftOf(dir) * half;
                AddFlight(buffer, p + left, p - left, q - left, q + left, f.Risers, looks, far);
                if (f.RailLeft) AddBridgeRail(buffer, rail, p + left, q + left, -LeftOf(dir), looks, far, true);
                if (f.RailRight) AddBridgeRail(buffer, rail, p - left, q - left, LeftOf(dir), looks, far, true);
                // A post under the flight's middle on its open edge (away from the wall).
                Vector3 mid = (f.From + f.To) * 0.5f;
                Vector2 o = stair.Out * (half - 0.15f);
                posts.Add(new Vector3(mid.x + o.x, Mathf.Min(f.From.y, f.To.y) - Footbridges.DeckThickness, mid.z + o.y));
            }

            foreach (StairLanding l in stair.Landings)
            {
                Vector3 c = BridgeLocal(site, origin, l.Centre);
                Vector3 along = BridgeLocalDirection(site, l.Along) * l.HalfAlong, outLocal = BridgeLocalDirection(site, l.Out) * l.HalfOut;
                Vector3 a = c - along - outLocal, b = c + along - outLocal, cc = c + along + outLocal, d = c - along + outLocal;
                AddSlopedSlab(buffer, a, b, cc, d, Footbridges.DeckThickness, looks.Deck, looks.Under);
                Vector3 alongDir = along.normalized, outDir = outLocal.normalized;
                if (l.RailPlus) AddBridgeRail(buffer, rail, b, cc, -alongDir, looks, far, true);
                if (l.RailMinus) AddBridgeRail(buffer, rail, a, d, alongDir, looks, far, true);
                if (l.RailOut) AddBridgeRail(buffer, rail, d, cc, -outDir, looks, far, true);
                // Posts under its two outer corners, unless it lies on the paving (the foot).
                foreach (float s in new[] { -1.0f, 1.0f })
                {
                    Vector2 corner = new Vector2(l.Centre.x, l.Centre.z) + l.Along * (s * (l.HalfAlong - 0.15f)) + l.Out * (l.HalfOut - 0.15f);
                    posts.Add(new Vector3(corner.x, l.Centre.y - Footbridges.DeckThickness, corner.y));
                }
            }

            buffer.Look = looks.Pier;
            float post = stair.Switchback ? 0.12f : 0.1f;
            foreach (Vector3 p in posts)
            {
                float ground = (float)world.Plan.GroundHeight(p.x, p.z) - 0.3f;
                if (p.y - ground < 0.9f) continue;   // on the paving: no post
                Vector3 local = BridgeLocal(site, origin, p);
                float groundLocal = ground - origin.y;
                buffer.AddBox(new Vector3(local.x - post, groundLocal, local.z - post), new Vector3(local.x + post, local.y, local.z + post));
            }
        }


        /// <summary>
        /// A slab whose TOP is the quad <paramref name="a"/> <paramref name="b"/> <paramref name="c"/> <paramref name="d"/> (round its edge, either
        /// way round), <paramref name="thickness"/> deep straight down from it, its edges closed: a sloping deck or parapet.
        /// </summary>
        private static void AddSlopedSlab(MeshBuffer s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float thickness, Color32 top, Color32 under)
        {
            Vector3 drop = Vector3.down * thickness;
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            if (n.y < 0.0f)
            {
                n = -n;
            }
            s.Look = top;
            s.AddQuad(a, b, c, d, n, Uv(a, n), Uv(b, n), Uv(c, n), Uv(d, n));
            s.Look = under;
            s.AddQuad(a + drop, b + drop, c + drop, d + drop, -n, Uv(a, -n), Uv(b, -n), Uv(c, -n), Uv(d, -n));
            s.Look = top;
            Vector3 middle = (a + b + c + d) * 0.25f;
            Vector3[] ring = { a, b, c, d };
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = ring[i], q = ring[(i + 1) % 4];
                Vector3 edge = q - p;
                if (edge.sqrMagnitude < 1e-8f)
                {
                    continue;
                }
                Vector3 outward = Vector3.Cross(edge, Vector3.up).normalized;
                if (Vector3.Dot(outward, (p + q) * 0.5f - middle) < 0.0f)
                {
                    outward = -outward;
                }
                s.AddQuad(p, q, q + drop, p + drop, outward, Uv(p, outward), Uv(q, outward), Uv(q + drop, outward), Uv(p + drop, outward));
            }
        }
    }
}
