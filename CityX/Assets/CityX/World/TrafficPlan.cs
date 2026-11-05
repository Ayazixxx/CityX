using System;
using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>What governs a junction (docs/TRAFFIC_CONTROL.md).</summary>
    public enum JunctionControl : byte
    {
        /// <summary>Nothing: a quiet corner, a bend.</summary>
        None = 0,

        /// <summary>STOP signs on the side street; the main street never stops.</summary>
        StopMinor = 1,

        /// <summary>A STOP sign on every approach, an ALL WAY plate under each.</summary>
        AllWayStop = 2,

        /// <summary>Traffic lights.</summary>
        Signal = 3,
    }

    /// <summary>One junction as traffic meets it: its roads in order round it, which of them are the main street, its kerb corners, and what governs it.</summary>
    public sealed class TrafficJunction
    {
        public RoadNode Node;
        public JunctionControl Control;

        /// <summary>Every road at the node, counter-clockwise, as PlanGeometry orders them.</summary>
        public RoadEdge[] Legs;

        /// <summary>Each leg's direction away from the node, its half width, and how far along it its ribbon starts (the plate covers the rest).</summary>
        public Vec2[] Dirs;

        public double[] HalfWidths;
        public double[] Trims;

        /// <summary>False for a leg nothing drives on: a step street, a pedestrian street.</summary>
        public bool[] Drives;

        /// <summary>The main street's legs: the two that run on through the junction. They never stop at a side-street stop, and have the first green at a signal.</summary>
        public bool[] Major;

        /// <summary>The kerb's corner between each leg and the next one round, where the two meet at less than a straight line.</summary>
        public Vec2[] Corners;

        public bool[] HasCorner;

        /// <summary>The lights' timing; null without lights.</summary>
        public SignalTiming Timing;

        public int LegOf(RoadEdge edge) => Array.IndexOf(Legs, edge);

        /// <summary>Whether traffic coming in along a leg faces a STOP sign.</summary>
        public bool Stops(int leg) => Drives[leg] && (Control == JunctionControl.AllWayStop || (Control == JunctionControl.StopMinor && !Major[leg]));

        /// <summary>Whether traffic coming in along a leg faces lights.</summary>
        public bool Signalled(int leg) => Control == JunctionControl.Signal && Drives[leg];

        /// <summary>The widest of the main street's legs, and of the others, kerb to kerb.</summary>
        public double WidthOf(bool major)
        {
            double widest = 0.0;
            for (int i = 0; i < Legs.Length; i++)
            {
                if (Drives[i] && Major[i] == major)
                {
                    widest = Math.Max(widest, HalfWidths[i] * 2.0);
                }
            }
            return widest;
        }

        /// <summary>The busiest class among the main street's legs, or among the others.</summary>
        public RoadClass ClassOf(bool major)
        {
            RoadClass top = RoadClass.Street;
            for (int i = 0; i < Legs.Length; i++)
            {
                if (Drives[i] && Major[i] == major && Legs[i].Class > top)
                {
                    top = Legs[i].Class;
                }
            }
            return top;
        }
    }

    /// <summary>
    /// WHICH JUNCTIONS GET LIGHTS, WHICH GET STOP SIGNS, AND WHICH GET NOTHING (docs/TRAFFIC_CONTROL.md; owner, 2026-11-05: "realistically placed traffic
    /// lights and stop signs throughout the city. research how stop signs vs traffic lights are utilized in real life"). The American rulebook (the MUTCD)
    /// decides by how much traffic each road carries; here a road's class and how built-up the place is stand for its traffic:
    ///
    ///  - two main roads meeting: lights, always;
    ///  - a side street crossing a main road: STOP on the side street, except where lights can stand far enough from the next lights on that road
    ///    (about 1,000 ft on a quiet stretch, a block or two downtown);
    ///  - a street ending at another: STOP on the one that ends;
    ///  - two side streets crossing: lights in the densest core (spaced), an all-way stop where it is busy, a stop on one street elsewhere, the stopping
    ///    street alternating from each such corner to the next (the "basket weave" of American neighbourhoods), and nothing at all on the quietest corners.
    ///
    /// Pure, and a function of the plan alone, so every machine has the same lights without a word sent.
    /// </summary>
    public sealed class TrafficPlan
    {
        /// <summary>How built-up (PlanFields.Population) a place must be: for its side streets' crossings to have lights; to have all-way stops; to have any sign at all.</summary>
        public const double CorePopulation = 0.80;

        public const double BusyPopulation = 0.65;
        public const double QuietPopulation = 0.30;

        /// <summary>How near other lights a side street's crossing of a main road may have its own, metres: on a quiet stretch (1,000 ft at the city's 1.3x) and in the core.</summary>
        public const double QuietSignalSpacing = 400.0;

        public const double CoreSignalSpacing = 170.0;

        /// <summary>The same for a crossing of two side streets in the core.</summary>
        public const double CoreStreetSignalSpacing = 150.0;

        /// <summary>How nearly opposite two legs must be to be one street running through, as the cosine between them.</summary>
        private const double ThroughCosine = -0.5;

        /// <summary>By node id; null where fewer than three roads that can be driven meet.</summary>
        public readonly TrafficJunction[] Junctions;

        private TrafficPlan(TrafficJunction[] junctions)
        {
            Junctions = junctions;
        }

        public TrafficJunction At(RoadNode node) => node != null && node.Id < Junctions.Length ? Junctions[node.Id] : null;

        /// <summary>The junction at one end of a road, and which of its legs the road is; false where nothing governs that end.</summary>
        public bool TryApproach(RoadEdge edge, bool atA, out TrafficJunction junction, out int leg)
        {
            junction = At(atA ? edge.A : edge.B);
            leg = junction != null ? junction.LegOf(edge) : -1;
            return leg >= 0 && junction.Control != JunctionControl.None;
        }

        private struct Candidate
        {
            public TrafficJunction Junction;
            public int Priority;
            public double Population;
            public double Spacing;
            public JunctionControl Otherwise;
        }

        public static TrafficPlan Build(PlanWorld plan)
        {
            var junctions = new TrafficJunction[plan.Data.Nodes.Count];
            var candidates = new List<Candidate>();
            foreach (RoadNode node in plan.Data.Nodes)
            {
                TrafficJunction junction = Describe(plan, node);
                if (junction == null)
                {
                    continue;
                }
                junctions[node.Id] = junction;
                Decide(plan, junction, candidates);
            }

            // Lights go to the junctions that most want them first, and each keeps the next ones off by its spacing.
            candidates.Sort((a, b) =>
            {
                int c = a.Priority.CompareTo(b.Priority);
                if (c == 0) c = b.Population.CompareTo(a.Population);
                return c != 0 ? c : a.Junction.Node.Id.CompareTo(b.Junction.Node.Id);
            });
            var lit = new Dictionary<long, List<Vec2>>();
            foreach (Candidate candidate in candidates)
            {
                Vec2 at = candidate.Junction.Node.P;
                if (candidate.Spacing > 0.0 && AnyWithin(lit, at, candidate.Spacing))
                {
                    candidate.Junction.Control = candidate.Otherwise;
                    continue;
                }
                candidate.Junction.Control = JunctionControl.Signal;
                long cell = Cell(at.X, at.Y);
                if (!lit.TryGetValue(cell, out List<Vec2> here))
                {
                    lit[cell] = here = new List<Vec2>();
                }
                here.Add(at);
            }
            WeaveStops(junctions);
            double[] along = AlongItsRoad(junctions);
            foreach (TrafficJunction junction in junctions)
            {
                if (junction != null && junction.Control == JunctionControl.Signal)
                {
                    junction.Timing = SignalTiming.For(junction, along[junction.Node.Id]);
                }
            }
            return new TrafficPlan(junctions);
        }

        /// <summary>
        /// How far along its main street each set of lights stands, by node id: measured down the road itself, round its bends, from the first lights on it, so that
        /// the green wave (SignalTiming) keeps time along a road that curves. A road is followed from junction to junction through each one's main street, and ends
        /// where it meets a junction as the side street.
        /// </summary>
        private static double[] AlongItsRoad(TrafficJunction[] junctions)
        {
            var along = new double[junctions.Length];
            for (int i = 0; i < along.Length; i++)
            {
                along[i] = double.NaN;
            }
            foreach (TrafficJunction junction in junctions)
            {
                if (junction == null || junction.Control != JunctionControl.Signal || !double.IsNaN(along[junction.Node.Id]))
                {
                    continue;
                }
                along[junction.Node.Id] = 0.0;
                double way = 1.0;
                for (int leg = 0; leg < junction.Legs.Length; leg++)
                {
                    if (junction.Major[leg])
                    {
                        Follow(junctions, along, junction, leg, way);
                        way = -1.0;
                    }
                }
            }
            return along;
        }

        private static void Follow(TrafficJunction[] junctions, double[] along, TrafficJunction from, int leg, double way)
        {
            RoadNode node = from.Node;
            RoadEdge edge = from.Legs[leg];
            double distance = 0.0;
            for (int step = 0; step < junctions.Length; step++)
            {
                RoadNode next = edge.Other(node);
                distance += edge.Length;
                TrafficJunction junction = junctions[next.Id];
                RoadEdge onward = null;
                if (junction != null)
                {
                    int arriving = junction.LegOf(edge);
                    if (arriving < 0 || !junction.Major[arriving])
                    {
                        return;
                    }
                    if (junction.Control == JunctionControl.Signal)
                    {
                        if (!double.IsNaN(along[next.Id]))
                        {
                            return;
                        }
                        along[next.Id] = way * distance;
                    }
                    for (int i = 0; i < junction.Legs.Length; i++)
                    {
                        if (junction.Major[i] && i != arriving)
                        {
                            onward = junction.Legs[i];
                        }
                    }
                }
                else
                {
                    // A bend, or a corner too small to govern: on along the only other road, if there is just the one.
                    foreach (RoadEdge other in next.Edges)
                    {
                        if (!other.Alive || other == edge)
                        {
                            continue;
                        }
                        if (onward != null)
                        {
                            return;
                        }
                        onward = other;
                    }
                }
                if (onward == null)
                {
                    return;
                }
                node = next;
                edge = onward;
            }
        }

        private const double CellSize = 400.0;

        private static long Cell(double x, double y) => ((long)Math.Floor(x / CellSize) << 32) ^ (uint)(int)Math.Floor(y / CellSize);

        private static bool AnyWithin(Dictionary<long, List<Vec2>> lit, Vec2 at, double metres)
        {
            int reach = (int)Math.Ceiling(metres / CellSize);
            int cx = (int)Math.Floor(at.X / CellSize), cy = (int)Math.Floor(at.Y / CellSize);
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    long cell = ((long)(cx + dx) << 32) ^ (uint)(cy + dy);
                    if (!lit.TryGetValue(cell, out List<Vec2> here))
                    {
                        continue;
                    }
                    foreach (Vec2 other in here)
                    {
                        if ((other - at).Length < metres)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static int DriveCount(TrafficJunction junction)
        {
            int count = 0;
            foreach (bool drives in junction.Drives)
            {
                if (drives) count++;
            }
            return count;
        }

        /// <summary>The junction's roads, corners and main street; null where fewer than three roads that can be driven meet.</summary>
        private static TrafficJunction Describe(PlanWorld plan, RoadNode node)
        {
            var legs = new List<RoadEdge>(node.Edges.Count);
            foreach (RoadEdge edge in node.Edges)
            {
                if (edge.Alive && plan.Geometry.Edges[edge.Id] != null)
                {
                    legs.Add(edge);
                }
            }
            legs.Sort((a, b) =>
            {
                Vec2 da = a.Other(node).P - node.P;
                Vec2 db = b.Other(node).P - node.P;
                int c = PlanMath.PseudoAngle(da.X, da.Y).CompareTo(PlanMath.PseudoAngle(db.X, db.Y));
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            int k = legs.Count;
            var junction = new TrafficJunction
            {
                Node = node,
                Legs = legs.ToArray(),
                Dirs = new Vec2[k],
                HalfWidths = new double[k],
                Trims = new double[k],
                Drives = new bool[k],
                Major = new bool[k],
                Corners = new Vec2[k],
                HasCorner = new bool[k],
            };
            int driven = 0;
            for (int i = 0; i < k; i++)
            {
                EdgeGeometry g = plan.Geometry.Edges[legs[i].Id];
                junction.Dirs[i] = (legs[i].Other(node).P - node.P).Normalized;
                junction.HalfWidths[i] = g.HalfWidth;
                junction.Trims[i] = legs[i].A == node ? g.TrimA : g.TrimB;
                junction.Drives[i] = !StepStreets.Is(plan, legs[i]) && !PedestrianStreets.Is(plan, legs[i]);
                if (junction.Drives[i]) driven++;
            }
            if (driven < 3)
            {
                return null;
            }

            // The kerb's corner between each leg and the next, as PlanGeometry finds it: where the one's left kerb line meets the other's right.
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                Vec2 di = junction.Dirs[i], dj = junction.Dirs[j];
                Vec2 rhs = (di.Perp * junction.HalfWidths[i] + dj.Perp * junction.HalfWidths[j]) * -1.0;
                double det = Vec2.Cross(di, dj * -1.0);
                if (Math.Abs(det) < 1e-6)
                {
                    continue;
                }
                double t = Vec2.Cross(rhs, dj * -1.0) / det;
                double s = Vec2.Cross(di, rhs) / det;
                if (t > 0.0 && s > 0.0)
                {
                    junction.Corners[i] = node.P + di * t + di.Perp * junction.HalfWidths[i];
                    junction.HasCorner[i] = true;
                }
            }

            MarkMainStreet(junction);
            return junction;
        }

        /// <summary>The main street: the two legs that best run on through, the busier class first.</summary>
        private static void MarkMainStreet(TrafficJunction junction)
        {
            int bestA = -1, bestB = -1;
            double best = double.MaxValue;
            for (int a = 0; a < junction.Legs.Length; a++)
            {
                if (!junction.Drives[a]) continue;
                for (int b = a + 1; b < junction.Legs.Length; b++)
                {
                    if (!junction.Drives[b]) continue;
                    double score = Vec2.Dot(junction.Dirs[a], junction.Dirs[b]) - 10.0 * ((int)junction.Legs[a].Class + (int)junction.Legs[b].Class);
                    if (score < best)
                    {
                        best = score;
                        bestA = a;
                        bestB = b;
                    }
                }
            }
            junction.Major[bestA] = true;
            junction.Major[bestB] = true;
        }

        private static bool Woven(TrafficJunction junction) =>
            junction != null && junction.Control == JunctionControl.StopMinor && junction.ClassOf(true) == RoadClass.Street && DriveCount(junction) >= 4;

        /// <summary>
        /// The basket weave: at a crossing of two side streets with a stop on one, which one stops alternates from each such crossing to the next down
        /// the street, so nobody drives far down either without stopping. The street nearer east-west runs free at one, the one nearer north-south at
        /// its neighbours, spreading out from the first of each group.
        /// </summary>
        private static void WeaveStops(TrafficJunction[] junctions)
        {
            var eastWestFree = new Dictionary<int, bool>();
            var queue = new Queue<TrafficJunction>();
            foreach (TrafficJunction first in junctions)
            {
                if (!Woven(first) || eastWestFree.ContainsKey(first.Node.Id))
                {
                    continue;
                }
                eastWestFree[first.Node.Id] = true;
                queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    TrafficJunction junction = queue.Dequeue();
                    bool free = eastWestFree[junction.Node.Id];
                    Weave(junction, free);
                    for (int leg = 0; leg < junction.Legs.Length; leg++)
                    {
                        if (!junction.Drives[leg])
                        {
                            continue;
                        }
                        RoadNode next = junction.Legs[leg].Other(junction.Node);
                        TrafficJunction neighbour = junctions[next.Id];
                        if (Woven(neighbour) && !eastWestFree.ContainsKey(next.Id))
                        {
                            eastWestFree[next.Id] = !free;
                            queue.Enqueue(neighbour);
                        }
                    }
                }
            }
        }

        private static void Weave(TrafficJunction junction, bool eastWestFree)
        {
            int first = -1;
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                if (junction.Major[i])
                {
                    first = i;
                    break;
                }
            }
            bool mainIsEastWest = Math.Abs(junction.Dirs[first].X) >= Math.Abs(junction.Dirs[first].Y);
            if (mainIsEastWest == eastWestFree)
            {
                return;
            }
            // The other street runs free, if it runs through at all.
            int otherA = -1, otherB = -1;
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                if (!junction.Drives[i] || junction.Major[i]) continue;
                if (otherA < 0) otherA = i;
                else if (otherB < 0) otherB = i;
            }
            if (otherB < 0 || Vec2.Dot(junction.Dirs[otherA], junction.Dirs[otherB]) > ThroughCosine)
            {
                return;
            }
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                junction.Major[i] = i == otherA || i == otherB;
            }
        }

        private static void Decide(PlanWorld plan, TrafficJunction junction, List<Candidate> candidates)
        {
            junction.Control = JunctionControl.None;
            int driven = 0;
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                if (junction.Drives[i]) driven++;
            }
            Vec2 at = junction.Node.P;
            double population = plan.Fields.Population(at.X, at.Y);
            RoadClass main = junction.ClassOf(true), side = junction.ClassOf(false);

            if (side >= RoadClass.Arterial)
            {
                candidates.Add(new Candidate { Junction = junction, Priority = 0, Population = population, Spacing = 0.0, Otherwise = JunctionControl.Signal });
                return;
            }
            if (main >= RoadClass.Arterial)
            {
                double core = Smooth((population - QuietPopulation) / (CorePopulation - QuietPopulation));
                double spacing = QuietSignalSpacing + (CoreSignalSpacing - QuietSignalSpacing) * core;
                if (driven >= 4)
                {
                    candidates.Add(new Candidate { Junction = junction, Priority = 1, Population = population, Spacing = spacing, Otherwise = JunctionControl.StopMinor });
                }
                else if (population >= CorePopulation)
                {
                    candidates.Add(new Candidate { Junction = junction, Priority = 2, Population = population, Spacing = spacing, Otherwise = JunctionControl.StopMinor });
                }
                else
                {
                    junction.Control = JunctionControl.StopMinor;
                }
                return;
            }

            if (population < QuietPopulation)
            {
                return;
            }
            if (driven == 3)
            {
                junction.Control = JunctionControl.StopMinor;
            }
            else if (population >= CorePopulation)
            {
                candidates.Add(new Candidate
                {
                    Junction = junction, Priority = 3, Population = population, Spacing = CoreStreetSignalSpacing, Otherwise = JunctionControl.AllWayStop,
                });
            }
            else
            {
                junction.Control = population >= BusyPopulation ? JunctionControl.AllWayStop : JunctionControl.StopMinor;
            }
        }

        private static double Smooth(double t)
        {
            t = t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
