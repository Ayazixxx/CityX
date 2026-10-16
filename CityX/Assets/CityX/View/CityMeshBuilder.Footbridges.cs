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
    /// </summary>
    public static partial class CityMeshBuilder
    {
        private static readonly Color BridgeConcrete = new Color(0.60f, 0.59f, 0.56f);
        private static readonly Color BridgeUnderside = new Color(0.50f, 0.50f, 0.48f);
        private static readonly Color BridgeRailTint = new Color(0.30f, 0.30f, 0.32f);
        private static readonly Color BridgeSteel = new Color(0.36f, 0.37f, 0.39f);
        private static readonly Color BridgeMesh = new Color(0.22f, 0.23f, 0.24f);

        /// <summary>How far the deck reaches into each wall it meets, so no gap opens at the threshold.</summary>
        private const float BridgeIntoWall = 0.06f;

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

        /// <summary>The bridges a building decides (its id the lower of the pair), drawn from its own frame.</summary>
        private static void AddFootbridges(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer buffer, bool far)
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
                    DrawBridge(world, site, origin, buffer, bridges[i], far);
                }
            }
            buffer.Look = saved;
        }

        /// <summary>The looks of a bridge: its deck's top and underside, its rails, its piers.</summary>
        private readonly struct BridgeLooks
        {
            public readonly Color32 Deck, Under, Rail, Pier, Mesh;

            public BridgeLooks(BridgeDeck deck)
            {
                bool steel = deck == BridgeDeck.Steel;
                Deck = steel ? SurfaceLook.Pack(BridgeSteel, (int)WallMaterial.Metal, 0.6f) : SurfaceLook.Pack(BridgeConcrete, (int)WallMaterial.Concrete, 0.5f);
                Under = steel ? SurfaceLook.Pack(BridgeRailTint, (int)WallMaterial.Metal, 0.7f) : SurfaceLook.Pack(BridgeUnderside, (int)WallMaterial.Concrete, 0.6f);
                Rail = SurfaceLook.Pack(BridgeRailTint, (int)WallMaterial.Metal, 0.5f);
                Pier = steel ? Rail : SurfaceLook.Pack(BridgeUnderside, (int)WallMaterial.Concrete, 0.6f);
                Mesh = SurfaceLook.Pack(BridgeMesh, (int)WallMaterial.Metal, 0.6f);
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

        private static Vector3 FlatOf(Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        /// <summary>The horizontal unit to the left of a direction in plan.</summary>
        private static Vector3 LeftOf(Vector3 d) => new Vector3(-d.z, 0.0f, d.x);

        /// <summary>
        /// A walking line's edges at <paramref name="half"/> either side: mitred at each turn (never more than twice the half width out), and at each
        /// end cut along the wall it meets (<paramref name="tangentA"/>, <paramref name="tangentB"/>; zero for an end square to the line).
        /// </summary>
        private static void BridgeEdges(Vector3[] c, Vector3 tangentA, Vector3 tangentB, float half, out Vector3[] left, out Vector3[] right)
        {
            int n = c.Length;
            left = new Vector3[n];
            right = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 dIn = i > 0 ? FlatOf(c[i] - c[i - 1]).normalized : Vector3.zero;
                Vector3 dOut = i + 1 < n ? FlatOf(c[i + 1] - c[i]).normalized : Vector3.zero;
                Vector3 offset;
                if (i == 0 || i == n - 1)
                {
                    Vector3 d = i == 0 ? dOut : dIn;
                    Vector3 s = LeftOf(d);
                    Vector3 t = i == 0 ? tangentA : tangentB;
                    if (t.sqrMagnitude < 1e-6f)
                    {
                        offset = s * half;
                    }
                    else
                    {
                        float k = Vector3.Dot(t, s);
                        if (Mathf.Abs(k) < 0.35f) k = k < 0.0f ? -0.35f : 0.35f;
                        offset = t * (half / k);
                    }
                }
                else
                {
                    Vector3 m = LeftOf(dIn) + LeftOf(dOut);
                    if (m.sqrMagnitude < 1e-6f) m = LeftOf(dOut);
                    m.Normalize();
                    offset = m * (half / Mathf.Max(0.5f, Vector3.Dot(m, LeftOf(dOut))));
                }
                left[i] = c[i] + offset;
                right[i] = c[i] - offset;
            }
        }

        private static void DrawBridge(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer buffer, in Footbridge b, bool far)
        {
            BridgePath path = b.Path;
            var looks = new BridgeLooks(b.Deck);
            int n = path.Points.Length;
            var c = new Vector3[n];
            for (int i = 0; i < n; i++) c[i] = BridgeLocal(site, origin, path.Points[i]);
            // Into each wall a little, so the threshold has no gap.
            c[0] -= FlatOf(c[1] - c[0]).normalized * BridgeIntoWall;
            c[n - 1] += FlatOf(c[n - 1] - c[n - 2]).normalized * BridgeIntoWall;
            Vector3 tangentA = BridgeLocalDirection(site, path.TangentA), tangentB = BridgeLocalDirection(site, path.TangentB);
            float half = b.Width * 0.5f;
            BridgeEdges(c, tangentA, tangentB, half, out Vector3[] left, out Vector3[] right);

            // The deck, stretch by stretch: a slab (a steel plate on two beams), or a flight's steps.
            for (int i = 0; i + 1 < n; i++)
            {
                if (path.Risers[i] > 0)
                {
                    AddFlight(buffer, left[i], right[i], right[i + 1], left[i + 1], path.Risers[i], looks, far);
                    continue;
                }
                AddDeckSlab(buffer, left[i], right[i], right[i + 1], left[i + 1], b.Deck, looks);
            }

            // The rails along both edges, set in from them; where a street stair leaves the deck by A's wall, its side is open there.
            Vector3 outA = FlatOf(c[1] - c[0]).normalized;
            Vector3 stairSide = Vector3.zero;
            if (b.Stair != null)
            {
                stairSide = BridgeLocalDirection(site, b.Stair.Along);
            }
            foreach (bool isLeft in new[] { true, false })
            {
                Vector3[] edge = isLeft ? left : right;
                bool open = b.Stair != null && Vector3.Dot(isLeft ? LeftOf(outA) : -LeftOf(outA), stairSide) > 0.5f;
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector3 p = edge[i], q = edge[i + 1];
                    Vector3 inward = (isLeft ? -1.0f : 1.0f) * LeftOf(FlatOf(q - p).normalized);
                    if (open)
                    {
                        // Clip off the stretch within the opening of A's wall.
                        float dp = Vector3.Dot(p - c[0], outA), dq = Vector3.Dot(q - c[0], outA);
                        float opening = Footbridges.StreetStairOpening;
                        if (dp < opening && dq < opening) continue;
                        if (dp < opening) p = Vector3.Lerp(p, q, (opening - dp) / (dq - dp));
                    }
                    AddBridgeRail(buffer, b.Rail, p, q, inward, looks, far, i + 2 == n);
                }
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

        /// <summary>A stretch of deck: a concrete slab, or a steel plate on a beam under each edge.</summary>
        private static void AddDeckSlab(MeshBuffer buffer, Vector3 a, Vector3 b, Vector3 c, Vector3 d, BridgeDeck deck, BridgeLooks looks)
        {
            if (deck == BridgeDeck.Concrete)
            {
                AddSlopedSlab(buffer, a, b, c, d, Footbridges.DeckThickness, looks.Deck, looks.Under);
                return;
            }
            AddSlopedSlab(buffer, a, b, c, d, SteelPlate, looks.Deck, looks.Under);
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
