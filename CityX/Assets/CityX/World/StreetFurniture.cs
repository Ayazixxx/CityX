using System;
using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    public enum StreetFurnitureKind
    {
        /// <summary>A dead street tree in a grated pit.</summary>
        Tree,
        Meter,
        Hydrant,
        Mailbox,

        /// <summary>A row of newspaper boxes.</summary>
        NewsBoxes,
        Bin,

        /// <summary>A utility cabinet.</summary>
        Cabinet,
    }

    /// <summary>One thing standing in a sidewalk's furnishing zone: what, where, which way its street runs and which way the road is.</summary>
    public readonly struct StreetFurnishing
    {
        public readonly StreetFurnitureKind Kind;
        public readonly Vec2 At;

        /// <summary>Along the kerb, a unit vector.</summary>
        public readonly Vec2 Along;

        /// <summary>From the thing toward the road, a unit vector.</summary>
        public readonly Vec2 ToRoad;

        public readonly int Seed;

        public StreetFurnishing(StreetFurnitureKind kind, Vec2 at, Vec2 along, Vec2 toRoad, int seed)
        {
            Kind = kind;
            At = at;
            Along = along;
            ToRoad = toRoad;
            Seed = seed;
        }
    }

    /// <summary>
    /// WHAT STANDS ALONG AN AMERICAN SIDEWALK (docs/AMERICAN_STREETS.md): everything is in the furnishing zone, the strip beside the kerb,
    /// and the walk behind it is left clear. At a corner a bin, a mailbox, a row of newspaper boxes; a hydrant near one end of the block;
    /// along some streets dead trees in grated pits (nothing has grown since the rapture; what stood, stands), along some parking meters,
    /// one to a stall. A pure rule of the plan, like the street lights (StreetLights), and it keeps off them.
    /// </summary>
    public static class StreetFurniture
    {
        /// <summary>Nothing stands nearer a junction's plate than this, metres: the corner's arc, its lights and its signs have that stretch.</summary>
        public const double CornerClear = 8.0;

        /// <summary>The middle of the furnishing zone, in from the kerb, metres; a meter and a hydrant stand nearer the kerb.</summary>
        public const double ZoneFromKerb = 0.85;

        public const double MeterFromKerb = 0.45;
        public const double HydrantFromKerb = 0.6;

        /// <summary>Tree to tree, metres (25 to 30 ft x 1.3).</summary>
        public const double TreeSpacing = 11.0;

        /// <summary>A parking stall along the kerb, metres (22 ft x 1.3), and how far out into the road its marks reach.</summary>
        public const double StallLength = 8.6;

        public const double StallDepth = 2.4;

        /// <summary>The kerb is painted red this far either way of a hydrant, and nobody parks there, metres (15 ft x 1.3).</summary>
        public const double RedKerbEach = 5.9;

        /// <summary>A side of a street shorter than this between its corners' clear stretches has nothing standing along it, metres.</summary>
        public const double ShortestFurnished = 6.0;

        /// <summary>The shares of streets lined with trees, of sides with meters, and of corners with each thing.</summary>
        /// <summary>The trees are rare: a street in ten (owner, 2026-11-05: "make the trees pretty uncommon").</summary>
        public const double TreeLinedShare = 0.1;

        public const double MeteredShare = 0.4, BinShare = 0.55, MailboxShare = 0.3, NewsShare = 0.22, CabinetShare = 0.25;

        /// <summary>How near two things' middles may be along the kerb, metres: a tree's pit, and anything else.</summary>
        public const double TreeClear = 2.4, Clear = 1.3;

        private const ulong Salt = 0x4655524E49UL;   // "FURNI"

        private static double Unit(PlanWorld plan, RoadEdge edge, int what) =>
            WorldHash.Unit01(edge.Id, what, Salt, unchecked((ulong)(long)plan.Seed));

        /// <summary>Whether a street is lined with trees.</summary>
        public static bool TreeLined(PlanWorld plan, RoadEdge edge) => Unit(plan, edge, 1) < TreeLinedShare;

        /// <summary>Whether one side of a street (+1 the left of its direction, -1 the right) has parking meters and marked stalls. Never a boulevard.</summary>
        public static bool Metered(PlanWorld plan, RoadEdge edge, int side) =>
            edge.Class != RoadClass.Boulevard && Unit(plan, edge, side > 0 ? 2 : 3) < MeteredShare;

        /// <summary>Where one side's stretch for standing things begins and ends, along the road from its A end; false where there is none.</summary>
        public static bool TryStretch(PlanWorld plan, EdgeGeometry g, out double from, out double to)
        {
            from = to = 0.0;
            if (g == null || !StreetLights.Has(plan, g.Edge))
            {
                return false;
            }
            from = g.TrimA + CornerClear;
            to = g.Length - g.TrimB - CornerClear;
            return to - from >= ShortestFurnished;
        }

        /// <summary>The stall lines of a metered side: how far along the road each lies (a meter stands at each, a mark is painted at each).</summary>
        public static void StallLines(PlanWorld plan, EdgeGeometry g, int side, List<double> into)
        {
            if (!TryStretch(plan, g, out double from, out double to) || !Metered(plan, g.Edge, side))
            {
                return;
            }
            // No stall reaches into the red kerb by a hydrant: it stands near one end of its side, and the stalls keep to the rest.
            if (TryHydrant(plan, g, side, out double hydrantAt))
            {
                if (hydrantAt - from < to - hydrantAt)
                {
                    from = Math.Max(from, hydrantAt + RedKerbEach);
                }
                else
                {
                    to = Math.Min(to, hydrantAt - RedKerbEach);
                }
            }
            int stalls = (int)Math.Floor((to - from - 2.0) / StallLength);
            if (stalls < 1)
            {
                return;
            }
            double first = (from + to) * 0.5 - stalls * StallLength * 0.5;
            for (int k = 0; k <= stalls; k++)
            {
                into.Add(first + k * StallLength);
            }
        }

        /// <summary>Where a side of a street has its hydrant, along the road from its A end: one to a street, near one of its ends.</summary>
        public static bool TryHydrant(PlanWorld plan, EdgeGeometry g, int side, out double along)
        {
            along = 0.0;
            if (!TryStretch(plan, g, out double from, out double to) || (Unit(plan, g.Edge, 4) < 0.5) != (side > 0) || to - from < 24.0)
            {
                return false;
            }
            along = Unit(plan, g.Edge, 5) < 0.5 ? from + 7.5 : to - 7.5;
            return true;
        }

        /// <summary>Everything standing along one road's two sidewalks, added to <paramref name="into"/>.</summary>
        public static void Along(PlanWorld plan, EdgeGeometry g, List<StreetFurnishing> into)
        {
            if (!TryStretch(plan, g, out double from, out double to))
            {
                return;
            }
            var lamps = new List<StreetLamp>();
            StreetLights.Along(plan, g, lamps);
            var taken = new List<double>();
            var stalls = new List<double>();
            for (int side = 1; side >= -1; side -= 2)
            {
                Vec2 aside = g.Dir.Perp * side;
                taken.Clear();
                foreach (StreetLamp lamp in lamps)
                {
                    if (Vec2.Dot(lamp.At - g.A, aside) > 0.0)
                    {
                        taken.Add(Vec2.Dot(lamp.At - g.A, g.Dir));
                    }
                }
                int salt = side > 0 ? 100 : 200;

                // At each corner, walking in from it: a bin, a mailbox, a row of newspaper boxes. Not every corner has each.
                for (int end = 0; end < 2; end++)
                {
                    double step = 0.5;
                    Corner(plan, g, into, taken, aside, StreetFurnitureKind.Bin, BinShare, salt + end * 10 + 1, from, to, end, ref step, 1.3);
                    Corner(plan, g, into, taken, aside, StreetFurnitureKind.Mailbox, MailboxShare, salt + end * 10 + 2, from, to, end, ref step, 1.5);
                    Corner(plan, g, into, taken, aside, StreetFurnitureKind.NewsBoxes, NewsShare, salt + end * 10 + 3, from, to, end, ref step, 2.6);
                }

                // One hydrant to a street, near one end, on one side.
                if (TryHydrant(plan, g, side, out double hydrantAt))
                {
                    Stand(g, into, taken, aside, StreetFurnitureKind.Hydrant, hydrantAt, HydrantFromKerb, salt + 30, Clear);
                }

                if (Unit(plan, g.Edge, salt + 40) < CabinetShare)
                {
                    double at = (from + to) * 0.5 + (Unit(plan, g.Edge, salt + 41) - 0.5) * (to - from) * 0.4;
                    Stand(g, into, taken, aside, StreetFurnitureKind.Cabinet, at, ZoneFromKerb, salt + 42, Clear + 0.6);
                }

                if (TreeLined(plan, g.Edge))
                {
                    int trees = (int)Math.Floor((to - from - 14.0) / TreeSpacing);
                    double first = (from + to) * 0.5 - trees * TreeSpacing * 0.5 + (side > 0 ? 0.0 : TreeSpacing * 0.5);
                    for (int k = 0; k <= trees; k++)
                    {
                        double at = first + k * TreeSpacing;
                        if (at <= to - 7.0)
                        {
                            Stand(g, into, taken, aside, StreetFurnitureKind.Tree, at, ZoneFromKerb, salt + 50 + k, TreeClear);
                        }
                    }
                }

                stalls.Clear();
                StallLines(plan, g, side, stalls);
                for (int k = 0; k < stalls.Count; k++)
                {
                    Stand(g, into, taken, aside, StreetFurnitureKind.Meter, stalls[k], MeterFromKerb, salt + 60 + k, Clear * 0.7);
                }
            }
        }

        private static void Corner(PlanWorld plan, EdgeGeometry g, List<StreetFurnishing> into, List<double> taken, Vec2 aside, StreetFurnitureKind kind,
            double share, int salt, double from, double to, int end, ref double step, double room)
        {
            if (Unit(plan, g.Edge, salt) >= share || to - from < 2.0 * (step + room) + 2.0)
            {
                return;
            }
            double at = end == 0 ? from + step + room * 0.5 : to - step - room * 0.5;
            if (Stand(g, into, taken, aside, kind, at, ZoneFromKerb, salt, Clear))
            {
                step += room;
            }
        }

        /// <summary>Stands a thing at a distance along the road, unless something already stands within <paramref name="clear"/> of it on this side.</summary>
        private static bool Stand(EdgeGeometry g, List<StreetFurnishing> into, List<double> taken, Vec2 aside, StreetFurnitureKind kind, double along,
            double fromKerb, int salt, double clear)
        {
            foreach (double other in taken)
            {
                if (Math.Abs(other - along) < clear)
                {
                    return false;
                }
            }
            taken.Add(along);
            Vec2 at = g.A + g.Dir * along + aside * (g.HalfWidth + fromKerb);
            into.Add(new StreetFurnishing(kind, at, g.Dir, -aside, unchecked(g.Edge.Id * 7919 + salt)));
            return true;
        }
    }
}
