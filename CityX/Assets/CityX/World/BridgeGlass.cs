using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// One bay of an enclosed bridge's long window, in the world: its sill line from <see cref="Start"/> to <see cref="End"/> in the wall's middle
    /// plane (a flight's climbs), the glass <see cref="Height"/> up from it, and its outward normal (level, away from the deck).
    /// </summary>
    public readonly struct BridgePane
    {
        public readonly PaneKey Key;
        public readonly Vector3 Start, End;
        public readonly float Height;
        public readonly Vector3 Normal;

        public BridgePane(PaneKey key, Vector3 start, Vector3 end, float height, Vector3 normal)
        {
            Key = key;
            Start = start;
            End = end;
            Height = height;
            Normal = normal;
        }

        /// <summary>Its width in plan, metres.</summary>
        public float Width => BridgeOutline.FlatOf(End - Start).magnitude;

        /// <summary>Level along its sill (on a deck, not a flight): its glass is a rectangle.</summary>
        public bool Level => Mathf.Abs(End.y - Start.y) < 0.01f;

        /// <summary>A world point as metres along the sill, up from the sill line there, and off the glass's plane (outward positive).</summary>
        public Vector3 Local(Vector3 point)
        {
            Vector3 across = BridgeOutline.FlatOf(End - Start);
            float width = across.magnitude;
            across = width > 1e-4f ? across / width : Vector3.right;
            Vector3 d = point - Start;
            float u = Vector3.Dot(BridgeOutline.FlatOf(d), across);
            float sill = Mathf.Lerp(Start.y, End.y, width > 1e-4f ? Mathf.Clamp01(u / width) : 0.0f);
            return new Vector3(u, point.y - sill, Vector3.Dot(d, Normal));
        }
    }

    /// <summary>
    /// THE GLASS OF THE ENCLOSED SKY BRIDGES (owner, 2026-10-28: "make it so the sky bridges that have windows have shatterable windows"). Each bay
    /// of a corridor's long window, between two mullions, is a pane of its own in GlassDamage, keyed to the bridge's lower-numbered building (which
    /// draws it): shot, it takes a bullet hole and cracks, the fifth bullet or a blow brings it down, something walking into a cracked one breaks
    /// it -- like a window. The same layout CityMeshBuilder.AddFootbridges draws the corridor from (BridgeOutline), so what a bullet hits, what is
    /// drawn and what falls in pieces agree. Pure, from the world.
    /// </summary>
    public static class BridgeGlass
    {
        /// <summary>A bridge pane's column: past every door's (DoorGlass.PaneColumnBase), then the bridge, its side, its stretch and its bay.</summary>
        public const int PaneColumnBase = 1 << 24;

        private const int Bays = 256;
        private const int Stretches = 64;

        /// <summary>How far off a pane's plane a blow may land and still be on its glass, metres.</summary>
        public const float HitDepth = 0.2f;

        public static bool IsBridgePane(in PaneKey key) => key.Column >= PaneColumnBase;

        public static PaneKey KeyOf(in Footbridge b, int side, int stretch, int bay) =>
            new PaneKey(b.SiteA, b.WallA, PaneColumnBase + ((b.Index * 2 + side) * Stretches + stretch) * Bays + bay, 0);

        private static int BridgeOf(in PaneKey key) => (key.Column - PaneColumnBase) / (Bays * Stretches * 2);

        /// <summary>A bridge's panes (only an enclosed one has any), into <paramref name="into"/>; returns how many it added.</summary>
        public static int Panes(in Footbridge b, List<BridgePane> into)
        {
            if (b.Kind != BridgeKind.Enclosed)
            {
                return 0;
            }
            BridgeOutline.Of(b, out Vector3[] centre, out Vector3[] left, out Vector3[] right);
            var stretches = new List<BridgeEdgeStretch>();
            BridgeOutline.EdgeStretches(b, centre, left, right, stretches);
            int before = into.Count;
            float sill = Footbridges.CorridorSill, height = b.Headroom - Footbridges.CorridorLintel - sill;
            foreach (BridgeEdgeStretch s in stretches)
            {
                float length = BridgeOutline.FlatOf(s.Q - s.P).magnitude;
                if (length < 1e-3f || height <= 0.0f)
                {
                    continue;
                }
                Vector3 mid = s.Inward * (Footbridges.CorridorWallThickness * 0.5f) + Vector3.up * sill;
                int bays = BridgeOutline.CorridorBays(length);
                float half = Footbridges.CorridorMullion * 0.5f / length;
                for (int j = 0; j < bays; j++)
                {
                    float s0 = j / (float)bays + half, s1 = (j + 1) / (float)bays - half;
                    into.Add(new BridgePane(KeyOf(b, s.Side, s.Stretch, j), Vector3.Lerp(s.P, s.Q, s0) + mid, Vector3.Lerp(s.P, s.Q, s1) + mid, height,
                        -s.Inward));
                }
            }
            return into.Count - before;
        }

        /// <summary>The pane a key names. False for a key that names no pane of a standing bridge.</summary>
        public static bool TryPane(CityWorld world, in PaneKey key, out BridgePane pane)
        {
            pane = default;
            if (!IsBridgePane(key) || key.Building < 0 || key.Building >= world.BuildingCount)
            {
                return false;
            }
            int index = BridgeOf(key);
            foreach (Footbridge b in Footbridges.For(world, world.Building(key.Building)))
            {
                if (b.SiteA != key.Building || b.Index != index)
                {
                    continue;
                }
                var panes = new List<BridgePane>();
                Panes(b, panes);
                foreach (BridgePane p in panes)
                {
                    if (p.Key.Equals(key))
                    {
                        pane = p;
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// The bridge pane a blow at <paramref name="point"/> (world) lands on, if any: within <see cref="HitDepth"/> of its plane and over its
        /// glass. Looks through the enclosed bridges of every building that could reach the point.
        /// </summary>
        public static bool TryPaneAt(CityWorld world, Vector3 point, out BridgePane pane)
        {
            pane = default;
            int cx = Plan.PlanWorld.ChunkOf(point.x), cy = Plan.PlanWorld.ChunkOf(point.z);
            int reach = Mathf.CeilToInt((Footbridges.MaxSpan + SearchHalfSize) / CityLayout.ChunkSize);
            float best = float.MaxValue;
            bool found = false;
            List<BridgePane> panes = null;
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    List<int> owned = world.BuildingsOwnedBy(cx + dx, cy + dy);
                    for (int i = 0; i < owned.Count; i++)
                    {
                        foreach (Footbridge b in Footbridges.For(world, world.Building(owned[i])))
                        {
                            if (b.SiteA != owned[i] || b.Kind != BridgeKind.Enclosed || !Near(b, point))
                            {
                                continue;
                            }
                            panes ??= new List<BridgePane>();
                            panes.Clear();
                            Panes(b, panes);
                            foreach (BridgePane p in panes)
                            {
                                Vector3 local = p.Local(point);
                                float off = Mathf.Abs(local.z);
                                if (off > HitDepth || off >= best || local.x < -0.02f || local.x > p.Width + 0.02f || local.y < -0.02f
                                    || local.y > p.Height + 0.02f)
                                {
                                    continue;
                                }
                                best = off;
                                pane = p;
                                found = true;
                            }
                        }
                    }
                }
            }
            return found;
        }

        /// <summary>Where a point lands on a pane's glass, 0..1 along it and up it.</summary>
        public static Vector2 Fraction(in BridgePane pane, Vector3 point)
        {
            Vector3 p = pane.Local(point);
            return new Vector2(Mathf.Clamp01(p.x / Mathf.Max(1e-4f, pane.Width)), Mathf.Clamp01(p.y / Mathf.Max(1e-4f, pane.Height)));
        }

        /// <summary>How far from a building's centre its bridges' doorways can be, at most, metres (a big block's half), for the search.</summary>
        private const float SearchHalfSize = 40.0f;

        /// <summary>Whether a point is anywhere near a bridge: within its width of the walking line in plan, and between its deck and its roof.</summary>
        private static bool Near(in Footbridge b, Vector3 point)
        {
            Vector3[] points = b.Path.Points;
            float reach = b.Width * 0.5f + HitDepth + 0.3f;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector3 a = points[i], c = points[i + 1];
                Vector3 ac = BridgeOutline.FlatOf(c - a);
                float t = ac.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(BridgeOutline.FlatOf(point - a), ac) / ac.sqrMagnitude) : 0.0f;
                Vector3 on = Vector3.Lerp(a, c, t);
                if (BridgeOutline.FlatOf(point - on).magnitude <= reach && point.y >= on.y - 0.5f && point.y <= on.y + b.Headroom + 0.5f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
