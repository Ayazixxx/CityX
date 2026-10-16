using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The footbridges between buildings (World/Footbridges, the layered city): a concrete deck from a doorway in one building's floor across
    /// the street to a doorway in the other's, with solid parapets, level with the floor at each end and sloping a little between. Drawn by
    /// the lower-numbered building of the pair, in its own frame, into the wall buffer (so it is in the chunk's collider: it is walked); near
    /// and far alike (a bridge reads from down the street). The doorways are the walls' own (BalconyDoorsOf adds a bridge's door to its wall).
    /// </summary>
    public static partial class CityMeshBuilder
    {
        private static readonly Color BridgeConcrete = new Color(0.60f, 0.59f, 0.56f);
        private static readonly Color BridgeUnderside = new Color(0.50f, 0.50f, 0.48f);
        private static readonly Color BridgeRail = new Color(0.30f, 0.30f, 0.32f);

        /// <summary>How far the deck reaches into each wall it meets, so no gap opens at the threshold.</summary>
        private const float BridgeIntoWall = 0.06f;

        /// <summary>The bridges a building decides (its id the lower of the pair), drawn from its own frame.</summary>
        private static void AddFootbridges(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer buffer, bool far)
        {
            Footbridge[] bridges = Footbridges.For(world, site);
            if (bridges.Length == 0)
            {
                return;
            }
            Color32 saved = buffer.Look;
            Color32 deck = SurfaceLook.Pack(BridgeConcrete, (int)WallMaterial.Concrete, 0.5f);
            Color32 under = SurfaceLook.Pack(BridgeUnderside, (int)WallMaterial.Concrete, 0.6f);
            Color32 rail = SurfaceLook.Pack(BridgeRail, (int)WallMaterial.Metal, 0.5f);
            for (int i = 0; i < bridges.Length; i++)
            {
                Footbridge b = bridges[i];
                if (b.SiteA != site.Id)
                {
                    continue;
                }
                // The ends in the building's own frame (the buffer's frame): local x and z about its centre, world heights.
                site.WorldToLocal(b.StartWorld.x, b.StartWorld.z, out double sx, out double sz);
                site.WorldToLocal(b.EndWorld.x, b.EndWorld.z, out double ex, out double ez);
                var s = new Vector3((float)sx, b.StartWorld.y - origin.y, (float)sz);
                var e = new Vector3((float)ex, b.EndWorld.y - origin.y, (float)ez);
                Vector3 run = e - s;
                Vector3 flat = new Vector3(run.x, 0.0f, run.z).normalized;
                // Into each wall a little, so the threshold has no gap.
                s -= flat * BridgeIntoWall;
                e += flat * BridgeIntoWall;
                Vector3 side = new Vector3(flat.z, 0.0f, -flat.x);
                float half = b.Width * 0.5f;

                // The deck.
                AddSlopedSlab(buffer, s - side * half, s + side * half, e + side * half, e - side * half, Footbridges.DeckThickness, deck, under);

                // The parapets, stopping just short of the walls.
                Vector3 ps = s + flat * (BridgeIntoWall + 0.1f), pe = e - flat * (BridgeIntoWall + 0.1f);
                float t = Footbridges.ParapetThickness;
                Vector3 up = Vector3.up * Footbridges.ParapetHeight;
                foreach (float k in new[] { -1.0f, 1.0f })
                {
                    Vector3 edge = side * (k * (half - t * 0.5f));
                    AddSlopedSlab(buffer, ps + edge - side * (t * 0.5f) + up, ps + edge + side * (t * 0.5f) + up, pe + edge + side * (t * 0.5f) + up,
                        pe + edge - side * (t * 0.5f) + up, Footbridges.ParapetHeight, deck, deck);
                    if (!far)
                    {
                        // A handrail along the parapet's top.
                        Vector3 top = up + Vector3.up * 0.06f;
                        AddSlopedSlab(buffer, ps + edge - side * 0.03f + top, ps + edge + side * 0.03f + top, pe + edge + side * 0.03f + top,
                            pe + edge - side * 0.03f + top, 0.06f, rail, rail);
                    }
                }
            }
            buffer.Look = saved;
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
