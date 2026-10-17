using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The stairways through steep blocks (World/Plan/StairLanes; owner 2026-10-16: "make more staircases throughout the city overall ... san
    /// francisco has a lot of randoms staircases that go up hills"): between the buildings from the low street's pavement to the high street's,
    /// each tread a block of concrete following the ground (StairLanes.Profile), a landing where the slope eases or after every flight, a handrail
    /// on posts down the middle (San Francisco's step streets), a concrete planter either side of each landing (the Lyon Street Steps' paired
    /// flights; bare earth in them, the city's planting waits for the owner), and a retaining face up to the paving either side where the
    /// stairway is cut into the slope. The ground and the block's paving are cut away under it. Each tread is built by the chunk its middle is in.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        private static readonly Color32 StairLaneLook = SurfaceLook.Pack(new Color(0.74f, 0.73f, 0.70f), (int)WallMaterial.Concrete, 0.55f);
        private static readonly Color32 StairLaneRetaining = SurfaceLook.Pack(new Color(0.55f, 0.54f, 0.52f), (int)WallMaterial.Concrete, 0.7f);
        private static readonly Color32 StairLaneRail = SurfaceLook.Pack(new Color(0.24f, 0.25f, 0.26f), (int)WallMaterial.Metal, 0.5f);
        private static readonly Color32 StairLanePlanter = SurfaceLook.Pack(new Color(0.63f, 0.62f, 0.59f), (int)WallMaterial.Concrete, 0.6f);
        private static readonly Color32 StairLaneEarth = SurfaceLook.Pack(new Color(0.42f, 0.36f, 0.29f), SurfaceLook.Earth, 0.7f);

        /// <summary>Under a tread its block reaches this far below the lower of its top and the ground beneath, metres.</summary>
        private const float StairLaneFooting = 0.4f;

        /// <summary>The handrail down the middle: its height over the treads, and its posts' spacing, metres.</summary>
        public const float StairLaneRailHeight = 1.15f;

        private const float StairLaneRailPosts = 2.4f;

        /// <summary>A landing's planters: how far in from the stairway's sides they reach, and how tall they stand over the landing, metres.</summary>
        public const float StairLanePlanterWidth = 0.7f;

        private const float StairLanePlanterHeight = 0.55f;

        /// <summary>The stairways reaching into this chunk (their blocks' lanes), their ground added to <paramref name="cuts"/>.</summary>
        private static List<StairLane> StairLanesHere(Context ctx, int chunkX, int chunkY, List<Vec2[]> cuts)
        {
            var lanes = new List<StairLane>();
            foreach (int b in ctx.Plan.BlocksIn(chunkX, chunkY))
            {
                int id = ctx.Plan.Data.Blocks[b].StairLane;
                if (id < 0) continue;
                StairLane lane = ctx.Plan.Data.StairLanes[id];
                Vec2[] corridor = StairLanes.Corridor(lane);
                Bounds(corridor, out double x0, out double z0, out double x1, out double z1);
                if (!ctx.Rect.Overlaps(x0, z0, x1, z1)) continue;
                lanes.Add(lane);
                cuts.Add(corridor);
            }
            return lanes;
        }

        private static void BuildStairLanes(Context ctx, List<StairLane> lanes, MeshBuffer buffer, bool far)
        {
            foreach (StairLane lane in lanes)
            {
                List<StairTread> treads = StairLanes.Profile(lane, ctx.Plan.Fields);
                Vec2 dir = lane.Dir;
                Vec2 Point(double s, double across) => lane.A + dir * s + dir.Perp * across;
                double Paving(Vec2 p) => ctx.Plan.GroundHeight(p.X, p.Y) + PlanBuildings.PavingRise;
                double half = lane.Width * 0.5;

                for (int i = 0; i < treads.Count; i++)
                {
                    StairTread t = treads[i];
                    Vec2 middle = Point((t.S0 + t.S1) * 0.5, 0.0);
                    if (!ctx.Contains(middle.X, middle.Y)) continue;
                    Vec2 a = Point(t.S0, -half), b = Point(t.S1, -half), c = Point(t.S1, half), d = Point(t.S0, half);
                    double low = System.Math.Min(t.Top, System.Math.Min(System.Math.Min(Paving(a), Paving(b)), System.Math.Min(Paving(c), Paving(d)))) - StairLaneFooting;
                    AddTreadBlock(ctx, buffer, a, b, c, d, t.Top, low, StairLaneLook, StairLaneLook);

                    // Either side, where the paving beside stands over the tread: a retaining face up to it, facing into the stairway.
                    buffer.Look = StairLaneRetaining;
                    foreach (double across in new[] { -half, half })
                    {
                        Vec2 p = Point(t.S0, across), q = Point(t.S1, across);
                        double pp = Paving(p), pq = Paving(q);
                        if (pp <= t.Top + 0.02 && pq <= t.Top + 0.02) continue;
                        Vector3 p0 = ctx.Local(p.X, t.Top, p.Y), q0 = ctx.Local(q.X, t.Top, q.Y);
                        Vector3 p1 = ctx.Local(p.X, System.Math.Max(pp, t.Top), p.Y), q1 = ctx.Local(q.X, System.Math.Max(pq, t.Top), q.Y);
                        Vec2 inward = dir.Perp * -System.Math.Sign(across);
                        float len = (float)(t.S1 - t.S0);
                        buffer.AddQuad(p0, q0, q1, p1, new Vector3((float)inward.X, 0.0f, (float)inward.Y),
                            new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1), new Vector2(0, 1));
                        // Where the paving beside stands well over the stairs (cut into a slope across it): a parapet on the edge, so nobody walks
                        // off the paving into the stairway.
                        if (!far && pp > t.Top + StairLaneParapetFrom && pq > t.Top + StairLaneParapetFrom)
                        {
                            AddParapet(ctx, buffer, p, q, pp, pq, -inward);
                            buffer.Look = StairLaneRetaining;
                        }
                    }

                    // A landing between two flights: a planter either side, clear of the steps either end.
                    bool landing = i > 0 && i < treads.Count - 1 && t.S1 - t.S0 >= StairLanes.Landing - 1e-6;
                    if (landing && !far)
                    {
                        foreach (double way in new[] { -1.0, 1.0 })
                        {
                            double inner = way * (half - StairLanePlanterWidth), outer = way * (half - 0.05);
                            Vec2 pa = Point(t.S0 + 0.2, inner), pb = Point(t.S1 - 0.2, inner), pc = Point(t.S1 - 0.2, outer), pd = Point(t.S0 + 0.2, outer);
                            AddTreadBlock(ctx, buffer, pa, pb, pc, pd, t.Top + StairLanePlanterHeight, t.Top, StairLanePlanter, StairLaneEarth);
                        }
                    }
                }

                if (far) continue;
                // The handrail down the middle, from pad to pad.
                AddHandrail(ctx, buffer, treads, StairLanes.EndPad, lane.Length - StairLanes.EndPad, s => Point(s, 0.0));
            }
        }

        /// <summary>
        /// A handrail on posts along a run of treads, from <paramref name="from"/> to <paramref name="to"/> metres along it at the plan points
        /// <paramref name="at"/>(s): posts every <see cref="StairLaneRailPosts"/> or so standing on the treads, a bar from head to head. Each
        /// post is built by the chunk it stands in, each bar by the chunk its middle is in.
        /// </summary>
        private static void AddHandrail(Context ctx, MeshBuffer buffer, List<StairTread> treads, double from, double to, System.Func<double, Vec2> at)
        {
            if (to - from < 0.5) return;
            buffer.Look = StairLaneRail;
            int posts = Mathf.Max(2, Mathf.RoundToInt((float)((to - from) / StairLaneRailPosts)) + 1);
            Vector3 last = Vector3.zero;
            double lastS = from;
            for (int k = 0; k < posts; k++)
            {
                double s = from + (to - from) * k / (posts - 1);
                Vec2 p = at(s);
                Vector3 foot = ctx.Local(p.X, TreadTopAt(treads, s), p.Y), head = foot + Vector3.up * StairLaneRailHeight;
                if (ctx.Contains(p.X, p.Y))
                {
                    buffer.AddBox(new Vector3(foot.x - 0.04f, foot.y, foot.z - 0.04f), new Vector3(foot.x + 0.04f, head.y, foot.z + 0.04f));
                }
                Vec2 between = at((lastS + s) * 0.5);
                if (k > 0 && ctx.Contains(between.X, between.Y))
                {
                    AddRailBar(buffer, last, head, 0.06f);
                }
                last = head;
                lastS = s;
            }
        }

        /// <summary>A stairway's side gets a parapet where the paving beside stands this far over its treads; the parapet's height over the paving and thickness, metres.</summary>
        public const float StairLaneParapetFrom = 0.6f;

        public const float StairLaneParapetHeight = 0.9f;

        private const float StairLaneParapetThickness = 0.22f;

        /// <summary>
        /// A parapet along a stairway's edge from p to q, standing on the paving beside (at <paramref name="pp"/> and <paramref name="pq"/>) and
        /// rising <see cref="StairLaneParapetHeight"/> over it; its face to the stairway continues the retaining face below it, its back stands on
        /// the paving <see cref="StairLaneParapetThickness"/> out (<paramref name="outward"/>), its ends closed.
        /// </summary>
        private static void AddParapet(Context ctx, MeshBuffer buffer, Vec2 p, Vec2 q, double pp, double pq, Vec2 outward)
        {
            buffer.Look = StairLaneRetaining;
            Vec2 po = p + outward * StairLaneParapetThickness, qo = q + outward * StairLaneParapetThickness;
            float h = StairLaneParapetHeight;
            Vector3 p1 = ctx.Local(p.X, pp, p.Y), q1 = ctx.Local(q.X, pq, q.Y);
            Vector3 p2 = ctx.Local(p.X, pp + h, p.Y), q2 = ctx.Local(q.X, pq + h, q.Y);
            Vector3 po1 = ctx.Local(po.X, pp, po.Y), qo1 = ctx.Local(qo.X, pq, qo.Y);
            Vector3 po2 = ctx.Local(po.X, pp + h, po.Y), qo2 = ctx.Local(qo.X, pq + h, qo.Y);
            var outN = new Vector3((float)outward.X, 0.0f, (float)outward.Y);
            Vector3 along = (q1 - p1).normalized;
            float len = (q1 - p1).magnitude;
            buffer.AddQuad(p1, q1, q2, p2, -outN, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, h), new Vector2(0, h));
            buffer.AddQuad(po1, qo1, qo2, po2, outN, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, h), new Vector2(0, h));
            buffer.AddQuad(p2, q2, qo2, po2, Vector3.up, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 0.22f), new Vector2(0, 0.22f));
            buffer.AddQuad(p1, po1, po2, p2, -along, new Vector2(0, 0), new Vector2(0.22f, 0), new Vector2(0.22f, h), new Vector2(0, h));
            buffer.AddQuad(q1, qo1, qo2, q2, along, new Vector2(0, 0), new Vector2(0.22f, 0), new Vector2(0.22f, h), new Vector2(0, h));
        }

        /// <summary>Whether a point on a block's pavement is in front of either end of the block's stairway (its furniture keeps clear).</summary>
        private static bool BeforeStairway(Context ctx, PlanBlock block, Vec2 at)
        {
            if (block.StairLane < 0) return false;
            StairLane lane = ctx.Plan.Data.StairLanes[block.StairLane];
            double reach = lane.Width * 0.5 + 1.5;
            return (at - lane.A).Length < reach || (at - lane.B).Length < reach;
        }

        /// <summary>The top of the tread over a point along a stairway, <paramref name="s"/> metres from its low end.</summary>
        public static double TreadTopAt(List<StairTread> treads, double s)
        {
            foreach (StairTread t in treads)
            {
                if (s >= t.S0 && s <= t.S1) return t.Top;
            }
            return s <= 0.0 ? treads[0].Top : treads[treads.Count - 1].Top;
        }

        /// <summary>
        /// A block standing on the ground: its top (corners a, b, c, d round its edge) at <paramref name="top"/> in <paramref name="topLook"/>, its
        /// four sides down to <paramref name="bottom"/> in <paramref name="sideLook"/>, each facing out.
        /// </summary>
        private static void AddTreadBlock(Context ctx, MeshBuffer buffer, Vec2 a, Vec2 b, Vec2 c, Vec2 d, double top, double bottom, Color32 sideLook, Color32 topLook)
        {
            Vector3 at = ctx.Local(a.X, top, a.Y), bt = ctx.Local(b.X, top, b.Y), ct = ctx.Local(c.X, top, c.Y), dt = ctx.Local(d.X, top, d.Y);
            Vector3 ab = ctx.Local(a.X, bottom, a.Y), bb = ctx.Local(b.X, bottom, b.Y), cb = ctx.Local(c.X, bottom, c.Y), db = ctx.Local(d.X, bottom, d.Y);
            buffer.Look = topLook;
            buffer.AddQuad(at, bt, ct, dt, Vector3.up, new Vector2((float)a.X, (float)a.Y), new Vector2((float)b.X, (float)b.Y),
                new Vector2((float)c.X, (float)c.Y), new Vector2((float)d.X, (float)d.Y));
            buffer.Look = sideLook;
            Vector3 middle = (at + bt + ct + dt) * 0.25f;
            Vector3[] tops = { at, bt, ct, dt }, bottoms = { ab, bb, cb, db };
            float h = (float)(top - bottom);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 outward = Vector3.Cross(tops[j] - tops[i], Vector3.up).normalized;
                if (Vector3.Dot(outward, (tops[i] + tops[j]) * 0.5f - middle) < 0.0f) outward = -outward;
                float len = (tops[j] - tops[i]).magnitude;
                buffer.AddQuad(tops[i], tops[j], bottoms[j], bottoms[i], outward, new Vector2(0, h), new Vector2(len, h), new Vector2(len, 0), new Vector2(0, 0));
            }
        }

        /// <summary>A rail bar from one post's head to the next: a square section <paramref name="size"/> across, its top along the line.</summary>
        private static void AddRailBar(MeshBuffer buffer, Vector3 from, Vector3 to, float size)
        {
            Vector3 run = to - from;
            Vector3 flat = new Vector3(run.x, 0.0f, run.z);
            if (flat.sqrMagnitude < 1e-6f) return;
            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized) * (size * 0.5f);
            Vector3 down = Vector3.down * size;
            Vector3 a = from - side, b = from + side, c = to + side, d = to - side;
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            if (n.y < 0.0f) n = -n;
            buffer.AddQuad(a, b, c, d, n, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            buffer.AddQuad(a + down, d + down, c + down, b + down, -n, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            buffer.AddQuad(a, d, d + down, a + down, -side.normalized, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            buffer.AddQuad(b, c, c + down, b + down, side.normalized, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }
    }
}
