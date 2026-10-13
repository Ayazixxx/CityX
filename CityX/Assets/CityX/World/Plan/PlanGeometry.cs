using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>How one road is laid on the ground: its centreline and where its ribbon starts and stops at each end.</summary>
    public sealed class EdgeGeometry
    {
        public RoadEdge Edge;
        public Vec2 A;
        public Vec2 B;
        public Vec2 Dir;
        public double Length;
        public double HalfWidth;

        /// <summary>Distance along the road, from each end, to where the ribbon begins (the junction plate covers the rest).</summary>
        public double TrimA;
        public double TrimB;

        /// <summary>
        /// An expressway: the ground-level ribbon is its service road, and the DECK rides above it on piers, from <see cref="DeckA"/> at A to
        /// <see cref="DeckB"/> at B (the deck's top, metres above sea level; see PlanGeometry.DeckTop). The deck runs straight through every street
        /// crossing; it is only trimmed back (<see cref="DeckTrimA"/>, <see cref="DeckTrimB"/>) where it meets another expressway's deck.
        /// </summary>
        public bool Elevated;
        public double DeckA;
        public double DeckB;
        public double DeckTrimA;
        public double DeckTrimB;

        /// <summary>The highest ground under an expressway's span.</summary>
        public double MaxGround;

        /// <summary>The deck's top this far along the road from A.</summary>
        public double DeckTopAt(double along) => DeckA + (DeckB - DeckA) * (Length > 1e-9 ? along / Length : 0.0);
    }

    /// <summary>The paved area where roads meet: a star-shaped polygon around the node.</summary>
    public sealed class JunctionGeometry
    {
        public RoadNode Node;
        public Vec2[] Plate;

        /// <summary>Where two or more expressway decks meet: the plate of deck between their trimmed ends, at <see cref="DeckTop"/>. Null where fewer than two meet.</summary>
        public Vec2[] DeckPlate;

        /// <summary>The deck's top at this node (NaN where no expressway passes).</summary>
        public double DeckTop = double.NaN;

        public bool Elevated => !double.IsNaN(DeckTop);
    }

    /// <summary>
    /// An interchange ramp as it is built: two straight stretches, from the deck's edge alongside the deck to <see cref="Mid"/>, then down across
    /// to the cross street's kerb at <see cref="Foot"/>, with the ramp's top at each of the three points (the surface follows a straight grade
    /// between them, never below the ground).
    /// </summary>
    public sealed class RampGeometry
    {
        public RampPlan Plan;
        public Vec2 Start, Mid, Foot;
        public double TopStart, TopMid, TopFoot;
        public double HalfWidth;

        /// <summary>Unit directions of the two stretches, and the cross-section direction at each point (the one at Mid is the mitre of the bend, longer than a unit).</summary>
        public Vec2 DirA, DirB;
        public Vec2 PerpStart, PerpMid, PerpFoot;
        public double LengthA, LengthB;

        public double Length => LengthA + LengthB;

        /// <summary>The ramp's top at a distance along it (from Start), on its straight grade.</summary>
        public double TopAt(double along)
        {
            if (along <= LengthA)
            {
                return TopStart + (TopMid - TopStart) * (LengthA > 1e-9 ? along / LengthA : 0.0);
            }
            double t = LengthB > 1e-9 ? (along - LengthA) / LengthB : 1.0;
            return TopMid + (TopFoot - TopMid) * Math.Max(0.0, Math.Min(1.0, t));
        }
    }

    /// <summary>
    /// Where the road surface is: every road's ribbon, trimmed back at each end, and the plate of tarmac at every junction
    /// that joins them. The corners are worked out exactly (the point where one road's kerb line meets the next road's), so the
    /// kerb of the pavement ring (<see cref="PlanBlock.Inner"/>) lands on the same corner -- road and pavement share their edge
    /// instead of overlapping or leaving a gap.
    ///
    /// THE EXPRESSWAY DECKS are laid here too (<see cref="DeckTop"/>): a deck's top at every expressway node is the highest ground under
    /// any of its spans plus the clearance, so the straight run between two nodes clears everything under it; two decks meeting share the
    /// node's height, and an expressway's END comes down over its last <see cref="DescentLength"/> to meet the ground, the way a real freeway
    /// ends at its last surface junction. The ramps of the interchanges (Interchanges) start at the deck's height where they leave it.
    /// </summary>
    public sealed class PlanGeometry
    {
        public EdgeGeometry[] Edges;       // by edge id; null for a dead edge
        public JunctionGeometry[] Junctions;   // by node id; Plate null where fewer than two roads meet

        /// <summary>The expressway deck's top at every node, metres above sea level; NaN at a node no expressway passes.</summary>
        public double[] DeckTop;

        /// <summary>Every interchange ramp, in CityPlanData.Ramps' order.</summary>
        public RampGeometry[] Ramps;

        /// <summary>The road surface floats this far above the ground. CitySurfaceBuilder.RoadLift is held equal to it by a test.</summary>
        public const double RoadLift = 0.07;

        /// <summary>Over this (classic metres, times the district's scale) an expressway's end comes down to the ground.</summary>
        public const double DescentLength = 250.0;

        /// <summary>How closely the ground under a deck's span is sampled for its highest point.</summary>
        public const double DeckSampleStep = 8.0;

        private const double MaxTrim = 42.0;
        [ThreadStatic] private static double _maxTrim;

        public static PlanGeometry Build(CityPlanData data)
        {
            PlanConfig cfg = data.Config;
            _maxTrim = MaxTrim * cfg.Scale;
            var geo = new PlanGeometry
            {
                Edges = new EdgeGeometry[data.Edges.Count],
                Junctions = new JunctionGeometry[data.Nodes.Count],
                DeckTop = new double[data.Nodes.Count],
            };

            foreach (RoadEdge e in data.Edges)
            {
                if (!e.Alive) continue;
                Vec2 d = e.B.P - e.A.P;
                double len = d.Length;
                geo.Edges[e.Id] = new EdgeGeometry
                {
                    Edge = e,
                    A = e.A.P,
                    B = e.B.P,
                    Dir = len > 1e-9 ? d / len : new Vec2(1.0, 0.0),
                    Length = len,
                    HalfWidth = CityPlan.HalfWidth(cfg, e.Class),
                    Elevated = e.Class == RoadClass.Highway,
                };
            }

            foreach (RoadNode node in data.Nodes)
            {
                var junction = new JunctionGeometry { Node = node };
                geo.Junctions[node.Id] = junction;
                BuildJunction(geo, node, junction, RoadClass.Street, false);
            }

            // A ribbon that two long trims would swallow whole is shortened to leave something to draw.
            foreach (EdgeGeometry g in geo.Edges)
            {
                if (g == null) continue;
                double room = g.Length * 0.96;
                if (g.TrimA + g.TrimB > room)
                {
                    double scale = room / (g.TrimA + g.TrimB);
                    g.TrimA *= scale;
                    g.TrimB *= scale;
                }
            }

            ComputeDeckTops(data, geo);
            foreach (RoadNode node in data.Nodes)
            {
                JunctionGeometry junction = geo.Junctions[node.Id];
                junction.DeckTop = geo.DeckTop[node.Id];
                if (junction.Elevated)
                {
                    BuildJunction(geo, node, junction, RoadClass.Highway, true);
                }
            }
            foreach (EdgeGeometry g in geo.Edges)
            {
                if (g == null || !g.Elevated) continue;
                double room = g.Length * 0.96;
                if (g.DeckTrimA + g.DeckTrimB > room)
                {
                    double scale = room / (g.DeckTrimA + g.DeckTrimB);
                    g.DeckTrimA *= scale;
                    g.DeckTrimB *= scale;
                }
            }

            BuildRamps(data, geo);
            return geo;
        }

        /// <summary>
        /// The deck's top at every expressway node. Each node is high enough for every span that meets it (the highest ground under the span
        /// plus the clearance and the slab), so the deck between two nodes clears the ground all the way. An expressway's END (one expressway
        /// edge at the node) is brought down to the ground, and the nodes within DescentLength of it are lowered on a straight grade.
        /// </summary>
        private static void ComputeDeckTops(CityPlanData data, PlanGeometry geo)
        {
            PlanFields fields = data.Fields;
            PlanConfig cfg = data.Config;
            double rise = cfg.DeckClearance + cfg.DeckThickness;

            foreach (EdgeGeometry g in geo.Edges)
            {
                if (g == null || !g.Elevated) continue;
                int steps = Math.Max(1, (int)Math.Ceiling(g.Length / DeckSampleStep));
                double top = double.MinValue;
                for (int i = 0; i <= steps; i++)
                {
                    double t = (double)i / steps;
                    top = Math.Max(top, fields.Height(g.A.X + (g.B.X - g.A.X) * t, g.A.Y + (g.B.Y - g.A.Y) * t));
                }
                g.MaxGround = top;
            }

            var highwayCount = new int[data.Nodes.Count];
            for (int n = 0; n < data.Nodes.Count; n++)
            {
                RoadNode node = data.Nodes[n];
                double top = double.NaN;
                foreach (RoadEdge e in node.Edges)
                {
                    if (!e.Alive || e.Class != RoadClass.Highway) continue;
                    highwayCount[n]++;
                    double candidate = geo.Edges[e.Id].MaxGround + rise;
                    top = double.IsNaN(top) ? candidate : Math.Max(top, candidate);
                }
                geo.DeckTop[n] = top;
            }

            // The ends.
            for (int n = 0; n < data.Nodes.Count; n++)
            {
                if (highwayCount[n] != 1) continue;
                RoadNode terminus = data.Nodes[n];
                double groundTop = fields.Height(terminus.P.X, terminus.P.Y) + RoadLift;
                geo.DeckTop[n] = groundTop;
                double descent = DescentLength * fields.ScaleAt(terminus.P.X, terminus.P.Y);
                RoadEdge e = null;
                foreach (RoadEdge candidate in terminus.Edges)
                {
                    if (candidate.Alive && candidate.Class == RoadClass.Highway) e = candidate;
                }
                RoadNode prev = terminus;
                double dist = 0.0;
                int guard = 0;
                while (e != null && guard++ < 64)
                {
                    RoadNode next = e.Other(prev);
                    dist += e.Length;
                    if (dist >= descent || highwayCount[next.Id] != 2)
                    {
                        break;   // the descent is over, or the deck has reached a junction of expressways (or the other end of a stub)
                    }
                    double full = geo.DeckTop[next.Id];
                    double lowered = groundTop + (full - groundTop) * (dist / descent);
                    lowered = Math.Max(lowered, fields.Height(next.P.X, next.P.Y) + RoadLift + 0.3);
                    geo.DeckTop[next.Id] = Math.Min(geo.DeckTop[next.Id], lowered);
                    RoadEdge onward = null;
                    foreach (RoadEdge candidate in next.Edges)
                    {
                        if (candidate.Alive && candidate.Class == RoadClass.Highway && candidate != e) onward = candidate;
                    }
                    prev = next;
                    e = onward;
                }
            }

            foreach (EdgeGeometry g in geo.Edges)
            {
                if (g == null || !g.Elevated) continue;
                g.DeckA = geo.DeckTop[g.Edge.A.Id];
                g.DeckB = geo.DeckTop[g.Edge.B.Id];
            }
        }

        private static void BuildRamps(CityPlanData data, PlanGeometry geo)
        {
            geo.Ramps = new RampGeometry[data.Ramps.Count];
            for (int i = 0; i < data.Ramps.Count; i++)
            {
                RampPlan plan = data.Ramps[i];
                EdgeGeometry highway = geo.Edges[plan.HighwayEdge];
                var ramp = new RampGeometry
                {
                    Plan = plan,
                    Start = plan.Start,
                    Mid = plan.Mid,
                    Foot = plan.Foot,
                    HalfWidth = plan.HalfWidth,
                    TopStart = highway != null ? highway.DeckTopAt(plan.SAlong) : data.Fields.Height(plan.S.X, plan.S.Y) + RoadLift,
                    TopFoot = data.Fields.Height(plan.Foot.X, plan.Foot.Y) + RoadLift,
                };
                ramp.DirA = (ramp.Mid - ramp.Start).Normalized;
                ramp.DirB = (ramp.Foot - ramp.Mid).Normalized;
                ramp.LengthA = (ramp.Mid - ramp.Start).Length;
                ramp.LengthB = (ramp.Foot - ramp.Mid).Length;
                double total = ramp.LengthA + ramp.LengthB;
                ramp.TopMid = total > 1e-9 ? ramp.TopStart - (ramp.TopStart - ramp.TopFoot) * (ramp.LengthA / total) : ramp.TopFoot;
                ramp.PerpStart = ramp.DirA.Perp;
                ramp.PerpFoot = ramp.DirB.Perp;
                Vec2 bisector = (ramp.DirA + ramp.DirB).Normalized.Perp;
                double cosHalf = Vec2.Dot(bisector, ramp.PerpStart);
                ramp.PerpMid = cosHalf > 0.2 ? bisector / cosHalf : ramp.PerpStart;   // the mitre at the bend, so both stretches share one cross-section
                geo.Ramps[i] = ramp;
            }
        }

        /// <summary>
        /// The plate and trims at one node, from the roads of one kind: every road for the ground (<paramref name="deck"/> false), only the
        /// expressways for the deck above it.
        /// </summary>
        private static void BuildJunction(PlanGeometry geo, RoadNode node, JunctionGeometry junction, RoadClass only, bool deck)
        {
            // Incident roads in counter-clockwise order around the node.
            var order = new List<RoadEdge>(node.Edges.Count);
            foreach (RoadEdge e in node.Edges)
            {
                if (!e.Alive) continue;
                if (deck && e.Class != RoadClass.Highway) continue;
                order.Add(e);
            }
            int k = order.Count;
            if (k == 0) return;
            order.Sort((a, b) =>
            {
                Vec2 da = a.Other(node).P - node.P;
                Vec2 db = b.Other(node).P - node.P;
                int c = PlanMath.PseudoAngle(da.X, da.Y).CompareTo(PlanMath.PseudoAngle(db.X, db.Y));
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });

            var dirs = new Vec2[k];
            var half = new double[k];
            var trim = new double[k];
            for (int i = 0; i < k; i++)
            {
                dirs[i] = (order[i].Other(node).P - node.P).Normalized;
                half[i] = geo.Edges[order[i].Id].HalfWidth;
            }
            if (k == 1)
            {
                return;   // a dead end: the ribbon just stops
            }

            var corner = new Vec2[k];
            var hasCorner = new bool[k];
            var outer = new bool[k];
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                Vec2 di = dirs[i], dj = dirs[j];
                Vec2 rhs = (di.Perp * half[i] + dj.Perp * half[j]) * -1.0;
                // Solve  di*t - dj*s = rhs.
                double det = Vec2.Cross(di, -dj);
                if (Math.Abs(det) < 1e-6)
                {
                    continue;   // parallel: a straight run, nothing to join
                }
                double t = Vec2.Cross(rhs, -dj) / det;
                double s = Vec2.Cross(di, rhs) / det;
                corner[i] = node.P + di * t + di.Perp * half[i];
                hasCorner[i] = true;
                if (t > 0.0 && s > 0.0)
                {
                    trim[i] = Math.Max(trim[i], Math.Min(t, _maxTrim));
                    trim[j] = Math.Max(trim[j], Math.Min(s, _maxTrim));
                }
                else
                {
                    outer[i] = true;   // the outside of a bend: the plate gets the corner point itself
                }
            }

            // Plate: for each road, the right then left point of its trimmed start, plus the outside-bend corner after it.
            var plate = new List<Vec2>(k * 3);
            for (int i = 0; i < k; i++)
            {
                RoadEdge e = order[i];
                EdgeGeometry g = geo.Edges[e.Id];
                if (deck)
                {
                    if (e.A == node) g.DeckTrimA = trim[i]; else g.DeckTrimB = trim[i];
                }
                else
                {
                    if (e.A == node) g.TrimA = trim[i]; else g.TrimB = trim[i];
                }

                Vec2 centre = node.P + dirs[i] * trim[i];
                plate.Add(centre - dirs[i].Perp * half[i]);
                plate.Add(centre + dirs[i].Perp * half[i]);
                if (outer[i] && hasCorner[i] && (corner[i] - node.P).Length < _maxTrim * 1.2)
                {
                    plate.Add(corner[i]);
                }
            }
            if (deck)
            {
                junction.DeckPlate = plate.ToArray();
            }
            else
            {
                junction.Plate = plate.ToArray();
            }
        }
    }
}
