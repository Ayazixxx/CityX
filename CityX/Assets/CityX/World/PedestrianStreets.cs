using System;
using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// PEDESTRIAN STREETS (docs/OPEN_SPACE.md 4.4 and 8; the Buchanan Mall and the NACTO parklet references): a short street in dense country is
    /// closed to cars at both ends -- bollards and planters across it just inside the junction plates -- raised to the pavement's height and laid
    /// in setts with no lane paint, and furnished down its middle (dead trees in planters, umbrella tables, benches, lights) with a walk either
    /// side. Which streets: a pure rule of the edge and the world seed, so the plan stores nothing and every chunk of the street agrees.
    /// </summary>
    public static class PedestrianStreets
    {
        /// <summary>The street's length at the classic scale (times the plan's scale where it is): long enough to be a place, short enough to close.</summary>
        public const double MinLength = 30.0, MaxLength = 120.0;

        /// <summary>Only in dense country (the population map at the street's middle).</summary>
        public const double MinPopulation = 0.5;

        /// <summary>The share of the streets that qualify.</summary>
        public const double Share = 0.05;

        /// <summary>The walk kept clear either side of the furniture, metres (a body is 0.9 m; two abreast and room).</summary>
        public const double WalkWidth = 3.0;

        /// <summary>Furniture every this many metres down the middle.</summary>
        public const double FurnitureEvery = 9.0;

        /// <summary>How far inside each end's trim the bollard line stands.</summary>
        public const double EndInset = 1.2;

        private static double Unit(int seed, int edgeId, int salt)
        {
            uint a = PlanNoise.Mix(unchecked((uint)seed), 0x70656473U);   // "peds"
            uint b = PlanNoise.Mix(unchecked((uint)edgeId), unchecked((uint)salt));
            return PlanNoise.Mix(a, b) / 4294967296.0;
        }

        private static int LiveEdges(RoadNode node)
        {
            int n = 0;
            foreach (RoadEdge e in node.Edges)
            {
                if (e.Alive) n++;
            }
            return n;
        }

        /// <summary>Whether a street is one of the pedestrian ones.</summary>
        public static bool Is(PlanWorld plan, RoadEdge edge)
        {
            if (edge == null || edge.Class != RoadClass.Street || edge.Bridge || !edge.Alive) return false;
            if (StepStreets.Is(plan, edge)) return false;   // too steep for tables and trees: it is steps (StepStreets)
            Vec2 mid = (edge.A.P + edge.B.P) * 0.5;
            double s = plan.Fields.ScaleAt(mid.X, mid.Y);
            double length = edge.Length;
            if (length < MinLength * s || length > MaxLength * s) return false;
            if (plan.Fields.Population(mid) < MinPopulation) return false;
            if (LiveEdges(edge.A) < 3 || LiveEdges(edge.B) < 3) return false;   // closing it strands nothing
            if (Unit(plan.Seed, edge.Id, 1) >= Share) return false;
            // Between buildings: a street with nothing built on either side of it (seed 91736402's first pick ran between two empty paved blocks)
            // is no place for tables and trees.
            foreach (int block in plan.BlocksBeside(edge))
            {
                if (plan.BlockHasLots(block)) return true;
            }
            return false;
        }

        /// <summary>Whether the street's lights come on at night (half of them: the ones with power).</summary>
        public static bool Powered(PlanWorld plan, RoadEdge edge) => Unit(plan.Seed, edge.Id, 2) < 0.5;

        /// <summary>
        /// The street's furniture as open-space pieces (world positions): bollards and planters across both ends just inside the trims, and down
        /// the middle, every <see cref="FurnitureEvery"/> metres, a dead tree in its planter, an umbrella table or a bench, with a bollard light
        /// between each pair; everything keeps <see cref="WalkWidth"/> of walk either side.
        /// </summary>
        public static List<OpenPiece> Furniture(PlanWorld plan, EdgeGeometry g)
        {
            var pieces = new List<OpenPiece>();
            if (g == null || !Is(plan, g.Edge)) return pieces;
            double t0 = g.TrimA + EndInset, t1 = g.Length - g.TrimB - EndInset;
            if (t1 - t0 < 8.0) return pieces;
            Vec2 dir = g.Dir, across = g.Dir.Perp;
            int seed = g.Edge.Id * 977;
            double half = g.HalfWidth;

            // The ends: bollards every 1.6 m across the whole width, a planter either side just inside the line.
            foreach (double t in new[] { t0, t1 })
            {
                Vec2 line = g.A + dir * t;
                int k = 0;
                for (double w = -half + 0.8; w <= half - 0.8; w += 1.6, k++)
                {
                    pieces.Add(new OpenPiece(OpenProp.Bollard, line + across * w, dir, 0.14, 0.14, seed + k));
                }
                // A planter either side of the middle just inside the line, the walks along the sides left clear (a planter at the side
                // stopped the walked body two metres short of the end).
                double inward = t == t0 ? 1.8 : -1.8;
                pieces.Add(new OpenPiece(OpenProp.Planter, line + dir * inward + across * 1.8, dir, 0.9, 0.5, seed + 100 + (t == t0 ? 0 : 1)));
                pieces.Add(new OpenPiece(OpenProp.Planter, line + dir * inward - across * 1.8, dir, 0.9, 0.5, seed + 102 + (t == t0 ? 0 : 1)));
            }

            // The middle: a street too narrow for a walk either side of a planter gets only its ends.
            if (half - 0.6 < WalkWidth) return pieces;
            int m = 0;
            for (double t = t0 + 4.5; t < t1 - 4.0; t += FurnitureEvery, m++)
            {
                Vec2 at = g.A + dir * t;
                switch (m % 3)
                {
                    case 0:
                        pieces.Add(new OpenPiece(OpenProp.TreePlanter, at, dir, 0.6, 0.6, seed + 200 + m));
                        break;
                    case 1:
                        if (half - 1.1 >= WalkWidth)
                        {
                            pieces.Add(new OpenPiece(OpenProp.UmbrellaTable, at, dir, 1.1, 1.1, seed + 200 + m, fallen: Unit(plan.Seed, g.Edge.Id, 40 + m) < 0.3));
                        }
                        else
                        {
                            pieces.Add(new OpenPiece(OpenProp.Bench, at, dir, 1.1, 0.3, seed + 200 + m));
                        }
                        break;
                    default:
                        // A bench facing one walk or the other.
                        pieces.Add(new OpenPiece(OpenProp.Bench, at, Unit(plan.Seed, g.Edge.Id, 60 + m) < 0.5 ? dir : -dir, 1.1, 0.3, seed + 200 + m));
                        break;
                }
                if (t + FurnitureEvery * 0.5 < t1 - 2.0)
                {
                    pieces.Add(new OpenPiece(OpenProp.BollardLight, g.A + dir * (t + FurnitureEvery * 0.5), dir, 0.15, 0.15, seed + 300 + m));
                }
            }
            return pieces;
        }

        /// <summary>Whether the block edge from <paramref name="a"/> to <paramref name="b"/> (two road nodes) runs along a pedestrian street.</summary>
        public static bool Along(PlanWorld plan, Vec2 a, Vec2 b) => Is(plan, plan.EdgeBetween(a, b));
    }
}
