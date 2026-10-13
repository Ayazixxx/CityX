using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// How a mall's cars were parked. Owner, 2026-10-12: "generate a parking lot fitted to the available space around the mall, and then figure
    /// out what are the chances the mall has a parking garage attached, i think those will be far less common". Every mall has the open-air lot;
    /// a garage beside it is the exception (Amenities.MallGarageChanceAt).
    /// </summary>
    public enum MallParking
    {
        /// <summary>An open-air lot all round the mall: painted rows, planted islands, lamp posts.</summary>
        OpenLot = 0,

        /// <summary>The open-air lot, and a multi-storey garage standing beside the mall as well.</summary>
        GarageAndLot = 1,
    }

    /// <summary>What a lot is for. Everything but a building is rare, and spaced out on purpose (see Amenities).</summary>
    public enum LotUse
    {
        Building = 0,

        /// <summary>An open car park: paved, painted bays, a low wall.</summary>
        ParkingLot = 1,

        /// <summary>A multi-storey car park: open-sided decks on a frame, ramps between them.</summary>
        ParkingGarage = 2,

        /// <summary>A shopping mall taking a whole block: a concourse of stores on two or three levels, anchors at the ends, a food court.</summary>
        Mall = 3,
    }

    /// <summary>
    /// The city's public and commercial set pieces, chosen in the plan so every machine agrees on them: pedestrian plazas, malls and parking.
    /// All deterministic from the seed, taken in a hashed order (not the order the blocks were found in), and kept apart by distance.
    ///
    /// HOW OFTEN, from the research (2026-10-12):
    ///  * PLAZAS. Whyte (The Social Life of Small Urban Spaces): the best plazas are SMALL and busy, right beside the street, with sittable edges,
    ///    and their catchment is about three blocks. So: small blocks only, in dense country, one every few blocks' walk.
    ///  * MALLS. A regional mall is 37,000-74,000 m2 of shops on a site of 24-48 ha with a trade area of 8-24 km, two or more anchor stores, a food
    ///    court where the concourses meet. They are rare and far apart; here no two within <see cref="MallSeparation"/> (real metres, not scaled) and
    ///    at most a few in a region, each on one whole big block outside the downtown core.
    ///  * PARKING. The median US downtown gives 26% of its land to parking (Parking Reform Network: 1% in New York to 49% in San Bernardino);
    ///    a dense Asian megacity is nearer New York. The owner asked for parking "not too often": about 3% of lots become a garage (downtown) or
    ///    a surface lot (elsewhere), never two in one block, and the land inside interchanges is parking too (as it is in real cities).
    /// </summary>
    public static partial class Amenities
    {
        // ---- plazas ----
        public const double PlazaMinPopulation = 0.40;
        public const double PlazaMinArea = 700.0;
        public const double PlazaMaxArea = 9000.0;
        public const double PlazaShare = 0.40;
        public const double PlazaSpacing = 260.0;

        // ---- malls ----

        /// <summary>The block a mall takes, classic square metres times the district's scale (not its square: a mall does not need sixteen times the land where the blocks are four times as wide).</summary>
        public const double MallMinArea = 24000.0;
        public const double MallMaxArea = 110000.0;
        public const double MallMinPopulation = 0.30;
        public const double MallMaxPopulation = 0.78;
        public const double MallSeparation = 6000.0;
        public const int MaxMalls = 4;

        /// <summary>
        /// The rectangle a mall's block must hold, classic metres times the square root of the scale: room for the concourse and its anchors at
        /// the back and a car park in front and round them (MallLayout).
        /// </summary>
        public const double MallMinWidth = 150.0;
        public const double MallMinDepth = 115.0;
        public const double MallLevelHeight = 6.5;

        /// <summary>
        /// The chance a mall has a parking garage beside its open-air lot, at the edge of town and at the densest a mall stands in (population
        /// MallMinPopulation .. MallMaxPopulation), about one in five on average. No national count of malls with garages was found (research,
        /// 2026-10-12); planning documents describe the conventional regional mall as anchors, a food court and in-line shops surrounded by surface
        /// parking, with surface lots supplying 80-90% of US parking, and structured parking the mark of urban and mixed-use centres -- so rarer at the
        /// edge, commoner toward the core.
        /// </summary>
        public const double MallGarageChanceEdge = 0.12;
        public const double MallGarageChanceDense = 0.30;

        /// <summary>The chance of a garage for a mall at a population (see MallGarageChanceEdge).</summary>
        public static double MallGarageChanceAt(double population)
        {
            double t = (population - MallMinPopulation) / (MallMaxPopulation - MallMinPopulation);
            t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
            return MallGarageChanceEdge + (MallGarageChanceDense - MallGarageChanceEdge) * t;
        }

        // ---- parking ----
        public const double GarageChance = 0.35;
        public const double LotChance = 0.30;

        /// <summary>The least side a car park's lot can have, classic metres times the SQUARE ROOT of the district's scale (a garage does not need to be four times as wide where the blocks are).</summary>
        public const double GarageMinSide = 26.0;
        public const double LotMinSide = 18.0;
        public const double GarageSpacing = 180.0;
        public const double LotSpacing = 140.0;
        public const int MinGarageLevels = 4;
        public const int MaxGarageLevels = 7;
        public const double GarageFloorHeight = 4.0;
        public const double ParkingLotHeight = 0.4;

        private static double Hash01(uint seed, int salt, int index) => PlanNoise.Mix(PlanNoise.Mix(seed, unchecked((uint)salt)), unchecked((uint)index)) / 4294967296.0;

        /// <summary>Marks the blocks that become pedestrian plazas (PlanBlock.Plaza): no lots are cut from them.</summary>
        public static void ChoosePlazas(CityPlanData data, uint seed)
        {
            PlanFields fields = data.Fields;
            var candidates = new List<int>();
            for (int i = 0; i < data.Blocks.Count; i++)
            {
                PlanBlock block = data.Blocks[i];
                if (block.Outer == null || block.Encloses || block.Mall) continue;
                Vec2 c = PlanMath.Centroid(block.Outer);
                double pop = fields.Population(c);
                double s = fields.ScaleAt(c.X, c.Y);
                if (pop < PlazaMinPopulation || block.Area < PlazaMinArea * s * s || block.Area > PlazaMaxArea * s * s) continue;
                if (TouchesAnExpressway(data, block.Outer)) continue;
                if (Hash01(seed, 0x706C617A, i) < PlazaShare) candidates.Add(i);
            }
            candidates.Sort((a, b) =>
            {
                int c = Hash01(seed, 0x6F726465, a).CompareTo(Hash01(seed, 0x6F726465, b));
                return c != 0 ? c : a.CompareTo(b);
            });
            var placed = new List<Vec2>();
            foreach (int i in candidates)
            {
                PlanBlock block = data.Blocks[i];
                Vec2 c = PlanMath.Centroid(block.Outer);
                double spacing = PlazaSpacing * fields.ScaleAt(c.X, c.Y);
                if (!Far(placed, c, spacing)) continue;
                block.Plaza = true;
                placed.Add(c);
                data.PlazaBlocks.Add(i);
            }
        }

        /// <summary>Whether any point of a polygon, sampled on a grid <paramref name="step"/> apart, is water (a river or lake channel).</summary>
        private static bool HasWaterInside(PlanFields fields, IList<Vec2> poly, double step)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Vec2 p in poly)
            {
                x0 = Math.Min(x0, p.X);
                x1 = Math.Max(x1, p.X);
                y0 = Math.Min(y0, p.Y);
                y1 = Math.Max(y1, p.Y);
            }
            for (double y = y0 + step * 0.5; y < y1; y += step)
            {
                for (double x = x0 + step * 0.5; x < x1; x += step)
                {
                    var p = new Vec2(x, y);
                    if (PlanMath.PointInPolygon(p, poly) && fields.IsWater(p)) return true;
                }
            }
            return false;
        }

        /// <summary>Marks the blocks that become malls (PlanBlock.Mall): each is cut as ONE lot (LotSubdivider).</summary>
        public static void ChooseMalls(CityPlanData data, uint seed)
        {
            PlanFields fields = data.Fields;
            var candidates = new List<int>();
            for (int i = 0; i < data.Blocks.Count; i++)
            {
                PlanBlock block = data.Blocks[i];
                if (block.Outer == null || block.Encloses) continue;
                Vec2 c = PlanMath.Centroid(block.Outer);
                double pop = fields.Population(c);
                double s = fields.ScaleAt(c.X, c.Y);
                if (pop < MallMinPopulation || pop > MallMaxPopulation) continue;
                if (block.Area < MallMinArea * s || block.Area > MallMaxArea * s) continue;
                if (TouchesAnExpressway(data, block.Outer)) continue;
                if (HasWaterInside(fields, block.Outer, 10.0 * Math.Sqrt(s))) continue;   // a river channel through it: the complex would not stand
                int front = MallFront(block);
                if (!PlanBuildings.FitRectangle(block.Outer.ToArray(), front, out _, out _, out double width, out double depth, s, PlanBuildings.FitMargin)) continue;
                double side = Math.Sqrt(s);
                if (width < MallMinWidth * side || depth < MallMinDepth * side) continue;
                candidates.Add(i);
            }
            candidates.Sort((a, b) =>
            {
                int c = Hash01(seed, 0x6D616C6C, a).CompareTo(Hash01(seed, 0x6D616C6C, b));
                return c != 0 ? c : a.CompareTo(b);
            });
            var placed = new List<Vec2>();
            foreach (int i in candidates)
            {
                if (placed.Count >= MaxMalls) break;
                Vec2 c = PlanMath.Centroid(data.Blocks[i].Outer);
                if (!Far(placed, c, MallSeparation)) continue;
                data.Blocks[i].Mall = true;
                double roll = Hash01(seed, 0x6D706B67, i);   // "mpkg"
                data.Blocks[i].MallParking = roll < MallGarageChanceAt(fields.Population(c)) ? MallParking.GarageAndLot : MallParking.OpenLot;
                placed.Add(c);
                data.MallBlocks.Add(i);
            }
        }

        /// <summary>The edge of a mall's block its front faces: the biggest road it is on, the longest of those. An arterial before a street, and an expressway's service road last.</summary>
        public static int MallFront(PlanBlock block)
        {
            int best = 0;
            double bestScore = double.MinValue;
            int n = block.Poly.Count;
            for (int i = 0; i < n; i++)
            {
                double rank = block.EdgeClass[i] == RoadClass.Arterial ? 2.0 : (block.EdgeClass[i] == RoadClass.Street ? 1.0 : 0.0);
                double length = (block.Poly[(i + 1) % n] - block.Poly[i]).Length;
                double score = rank * 100000.0 + length;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Whether a block or lot reaches under an expressway's deck or ramps, or onto the land an interchange cuts off.</summary>
        public static bool TouchesAnExpressway(CityPlanData data, IList<Vec2> poly) => data.Expressways.Touches(poly);

        private static bool Far(List<Vec2> from, Vec2 p, double distance)
        {
            foreach (Vec2 q in from)
            {
                if ((q - p).Length < distance) return false;
            }
            return true;
        }

        /// <summary>
        /// Hands out the parking as the lots are resolved into buildings, in lot order: a garage downtown, an open lot elsewhere, each rare, never
        /// two in one block, and never within a spacing of another.
        /// </summary>
        public sealed class ParkingPlanner
        {
            private readonly List<Vec2> _garages = new List<Vec2>();
            private readonly List<Vec2> _lots = new List<Vec2>();
            private int _garageBlock = -1;
            private int _lotBlock = -1;

            public LotUse Decide(ZoneKind zone, PlanLot lot, Vec2 centre, double width, double depth, double hash)
            {
                if (lot.Use != LotUse.Building)
                {
                    return lot.Use;
                }
                double s = lot.Scale;
                double side = Math.Sqrt(s);
                if (zone == ZoneKind.Core || zone == ZoneKind.Urban)
                {
                    if (width >= GarageMinSide * side && depth >= GarageMinSide * side && hash < GarageChance
                        && lot.BlockIndex != _garageBlock && Far(_garages, centre, GarageSpacing * s))
                    {
                        _garages.Add(centre);
                        _garageBlock = lot.BlockIndex;
                        lot.Use = LotUse.ParkingGarage;
                        return LotUse.ParkingGarage;
                    }
                }
                else if (zone != ZoneKind.OldTown)
                {
                    if (width >= LotMinSide * side && depth >= LotMinSide * side && hash < LotChance
                        && lot.BlockIndex != _lotBlock && Far(_lots, centre, LotSpacing * s))
                    {
                        _lots.Add(centre);
                        _lotBlock = lot.BlockIndex;
                        lot.Use = LotUse.ParkingLot;
                        return LotUse.ParkingLot;
                    }
                }
                return LotUse.Building;
            }
        }
    }
}
