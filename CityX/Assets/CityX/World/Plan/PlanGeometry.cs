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
    }

    /// <summary>The paved area where roads meet: a star-shaped polygon around the node.</summary>
    public sealed class JunctionGeometry
    {
        public RoadNode Node;
        public Vec2[] Plate;
    }

    /// <summary>
    /// Where the road surface is: every road's ribbon, trimmed back at each end, and the plate of tarmac at every junction
    /// that joins them. The corners are worked out exactly (the point where one road's kerb line meets the next road's), so the
    /// kerb of the pavement ring (<see cref="PlanBlock.Inner"/>) lands on the same corner -- road and pavement share their edge
    /// instead of overlapping or leaving a gap.
    /// </summary>
    public sealed class PlanGeometry
    {
        public EdgeGeometry[] Edges;       // by edge id; null for a dead edge
        public JunctionGeometry[] Junctions;   // by node id; Plate null where fewer than two roads meet

        private const double MaxTrim = 42.0;

        public static PlanGeometry Build(CityPlanData data)
        {
            PlanConfig cfg = data.Config;
            var geo = new PlanGeometry
            {
                Edges = new EdgeGeometry[data.Edges.Count],
                Junctions = new JunctionGeometry[data.Nodes.Count],
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
                };
            }

            foreach (RoadNode node in data.Nodes)
            {
                var junction = new JunctionGeometry { Node = node };
                geo.Junctions[node.Id] = junction;
                BuildJunction(geo, node, junction);
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
            return geo;
        }

        private static void BuildJunction(PlanGeometry geo, RoadNode node, JunctionGeometry junction)
        {
            int k = node.Edges.Count;
            if (k == 0) return;

            // Incident roads in counter-clockwise order around the node.
            var order = new List<RoadEdge>(node.Edges);
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
                    trim[i] = Math.Max(trim[i], Math.Min(t, MaxTrim));
                    trim[j] = Math.Max(trim[j], Math.Min(s, MaxTrim));
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
                if (e.A == node) g.TrimA = trim[i]; else g.TrimB = trim[i];

                Vec2 centre = node.P + dirs[i] * trim[i];
                plate.Add(centre - dirs[i].Perp * half[i]);
                plate.Add(centre + dirs[i].Perp * half[i]);
                if (outer[i] && hasCorner[i] && (corner[i] - node.P).Length < MaxTrim * 1.2)
                {
                    plate.Add(corner[i]);
                }
            }
            junction.Plate = plate.ToArray();
        }
    }
}
