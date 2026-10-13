using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// One ramp of an interchange, as the plan lays it: from the expressway deck down to a cross street. Laid over the finished road network
    /// as GEOMETRY ONLY (not as roads of the graph): the ramp starts up on the deck, so a street may pass under its first stretch, which a road
    /// of the planar graph could not express; the land it covers is kept clear of lots through <see cref="Exclusion"/> instead.
    ///
    /// The shape is a real diamond interchange's: the ramp leaves the deck's edge at <see cref="Start"/>, runs ALONGSIDE the deck (the
    /// deceleration lane) to <see cref="Mid"/>, then angles down across the land between the expressway and the cross street to meet that
    /// street's kerb at <see cref="Foot"/>, at ground level. A full interchange has one in each of the four quadrants.
    /// </summary>
    public sealed class RampPlan
    {
        /// <summary>The node where the cross street meets the expressway (on the ground, under the deck).</summary>
        public int ExitNode;

        /// <summary>The highway edge the ramp leaves, and how far along it from its A end the ramp's start is, on the centreline.</summary>
        public int HighwayEdge;
        public double SAlong;

        /// <summary>The ramp's start projected onto the expressway's centreline.</summary>
        public Vec2 S;

        /// <summary>Where the ramp leaves the deck's outer edge (at deck height), the end of its stretch alongside the deck, and where it meets the street's kerb (at ground level).</summary>
        public Vec2 Start, Mid, Foot;

        /// <summary>The point on the cross street's centreline the ramp serves.</summary>
        public Vec2 E;

        public int StreetEdge;
        public double HalfWidth;

        /// <summary>Off-ramp (the deck's traffic leaves here) or on-ramp (joins), with right-hand traffic. Decides the gantry sign.</summary>
        public bool Leaving;

        /// <summary>No lot is cut over this polygon: the ramp's strips (widened by a pavement) and the sliver of land between the ramp and the expressway.</summary>
        public Vec2[] Exclusion;

        /// <summary>The sliver of land between the ramp, the expressway and the cross street: paved, and used as a parking lot (CitySurfaceBuilder).</summary>
        public Vec2[] Sliver;

        /// <summary>The convex hull of the exclusion, cached box for quick rejection.</summary>
        public double MinX, MinY, MaxX, MaxY;
    }

    /// <summary>
    /// Where the expressways can be got on and off. Real urban interchanges are about a mile apart (Caltrans/WSDOT design manuals: one mile in
    /// urban areas), the compact DIAMOND is the standard form (WSDOT: "the most generally applicable and serviceable interchange configuration"),
    /// and a ramp is sized to hold a queue and to climb the deck's height at no more than about 6%.
    ///
    /// Here: along every expressway, at a node where a street or arterial crosses it, no two closer than <see cref="ExitSpacing"/> (times the
    /// district's scale), in built-up country only. Each quadrant that has room gets a ramp (RampPlan). Deterministic: candidates are taken in an
    /// order hashed from the seed, not in the order the roads grew.
    /// </summary>
    public static class Interchanges
    {
        /// <summary>
        /// Metres between interchanges, in the classic city (times the square root of the district's scale). Real urban exits are about a mile
        /// apart; this is under half that, because the city is 1.3x and is walked and flown, not driven across, and because it is measured straight
        /// across the map, which keeps the exits of two neighbouring expressways apart too.
        /// </summary>
        public const double ExitSpacing = 700.0;

        /// <summary>A ramp's grade is held to this (real ramps: 5-6%, up to 7%): the stretch alongside the deck is lengthened until the ramp is long enough.</summary>
        public const double MaxGrade = 0.065;

        /// <summary>The least the stretch alongside the deck may leave to the crossing: the stretch across to the street needs that much.</summary>
        public const double MinAcross = 30.0;

        /// <summary>Where the ramp leaves the deck: this far before (or after) the crossing, along the expressway. Classic metres.</summary>
        public const double RampAlong = 150.0;

        /// <summary>The least that is accepted when the highway edge is shorter.</summary>
        public const double MinRampAlong = 110.0;

        /// <summary>The stretch the ramp runs alongside the deck before it angles away: the deceleration lane.</summary>
        public const double RampTaper = 60.0;

        /// <summary>How far along the cross street from the crossing the ramp lands.</summary>
        public const double RampToStreet = 48.0;

        /// <summary>Only built-up country gets interchanges.</summary>
        public const double MinPopulation = 0.18;

        /// <summary>A ramp must not land within this of another junction on the cross street.</summary>
        public const double ClearOfJunction = 26.0;

        /// <summary>A ramp must come down at least this far (metres) from the deck to the street it serves: on a hillside the uphill street can be level with the deck.</summary>
        public const double MinDrop = 3.0;

        /// <summary>Why ramps were not built, for the plan harness: candidates, too close to another exit, no room along the expressway, no cross street, street too short, crossed, foot on a junction, wet or out of the region, near the expressway's end.</summary>
        [ThreadStatic] public static int[] Rejections;

        public static void Place(RoadNetwork net, PlanFields fields, PlanConfig cfg, uint seed, CityPlanData data)
        {
            var candidates = new List<RoadNode>();
            foreach (RoadNode node in net.Nodes)
            {
                int highways = 0, others = 0;
                bool bridge = false;
                foreach (RoadEdge e in node.Edges)
                {
                    if (!e.Alive) continue;
                    if (e.Class == RoadClass.Highway)
                    {
                        highways++;
                        bridge |= e.Bridge;
                    }
                    else
                    {
                        others++;
                    }
                }
                if (highways == 2 && others >= 1 && !bridge && fields.Population(node.P) >= MinPopulation)
                {
                    candidates.Add(node);
                }
            }
            candidates.Sort((a, b) =>
            {
                uint ha = PlanNoise.Mix(seed, unchecked((uint)a.Id)), hb = PlanNoise.Mix(seed, unchecked((uint)b.Id));
                int c = ha.CompareTo(hb);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });

            Rejections = new int[9];
            Rejections[0] = candidates.Count;
            var accepted = new List<Vec2>();
            foreach (RoadNode node in candidates)
            {
                double ls = fields.ScaleAt(node.P.X, node.P.Y);
                // Spacing grows with the district's scale, but slower than its blocks do: a huge district's exits every two of its blocks.
                double spacing = ExitSpacing * Math.Sqrt(ls);
                bool tooClose = false;
                foreach (Vec2 p in accepted)
                {
                    if ((p - node.P).Length < spacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                {
                    Rejections[1]++;
                    continue;
                }
                int before = data.Ramps.Count;
                foreach (RoadEdge highway in node.Edges)
                {
                    if (!highway.Alive || highway.Class != RoadClass.Highway) continue;
                    TryRampsAlong(net, fields, cfg, node, highway, ls, data);
                }
                if (data.Ramps.Count > before)
                {
                    accepted.Add(node.P);
                    data.ExitNodes.Add(node.Id);
                }
            }
        }

        /// <summary>An expressway may bend this little (cosine) between the crossing and a ramp's start, so the stretch alongside the deck is straight.</summary>
        private const double StraightCos = 0.985;

        /// <summary>
        /// The two ramps (one each side) that leave the deck along one of the crossing's highway edges. The ramp's start may lie beyond the
        /// side streets that join the service road along the way (the deck, and the ramp beside it, run over them), but not beyond a junction
        /// of expressways, a bend, or the expressway's end.
        /// </summary>
        private static void TryRampsAlong(RoadNetwork net, PlanFields fields, PlanConfig cfg, RoadNode node, RoadEdge highway, double ls, CityPlanData data)
        {
            Vec2 d = (highway.Other(node).P - node.P).Normalized;   // along the expressway, away from the crossing
            double wanted = RampAlong * ls;
            double margin = cfg.SnapNode * 0.6 * ls;

            // Walk the chain of expressway edges out from the crossing until the ramp's start fits on one.
            RoadEdge edge = highway;
            RoadNode prev = node;
            double walked = 0.0;
            double along = 0.0;
            bool placed = false;
            for (int guard = 0; guard < 16 && !placed; guard++)
            {
                double remaining = wanted - walked;
                if (remaining <= edge.Length - margin && remaining >= margin)
                {
                    along = remaining;
                    placed = true;
                    break;
                }
                if (edge.Length - margin >= MinRampAlong * ls - walked && edge.Length - margin >= margin)
                {
                    along = edge.Length - margin;   // not the whole way, but enough
                    placed = true;
                    break;
                }
                RoadNode next = edge.Other(prev);
                RoadEdge onward = null;
                int highways = 0;
                foreach (RoadEdge e in next.Edges)
                {
                    if (!e.Alive || e.Class != RoadClass.Highway) continue;
                    highways++;
                    if (e != edge) onward = e;
                }
                if (highways != 2 || onward == null || onward.Bridge)
                {
                    break;   // a junction of expressways, or the end of this one
                }
                Vec2 nd = (onward.Other(next).P - next.P).Normalized;
                if (Vec2.Dot(nd, d) < StraightCos)
                {
                    break;   // a bend: the ramp would not lie alongside the deck
                }
                walked += edge.Length;
                prev = next;
                edge = onward;
            }
            if (!placed)
            {
                Rejections[2] += 2;
                return;
            }
            highway = edge;
            Vec2 s = prev.P + d * along;
            double hw = cfg.HighwayHalfWidth, rh = cfg.RampHalfWidth;

            // Not where the expressway is coming down to its end (PlanGeometry lowers the deck over its last DescentLength): a ramp there would
            // leave a deck that is already near the ground, or below the cross street on a hill. Measured along the chain both ways from the start.
            double descent = PlanGeometry.DescentLength * ls;
            double toEndOnward = edge.Length - along + ChainDistanceToEnd(edge.Other(prev), edge, descent);
            double toEndBack = along + ChainDistanceToEnd(prev, edge, descent);
            if (toEndOnward < descent || toEndBack < descent)
            {
                Rejections[8] += 2;
                return;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Vec2 perp = d.Perp * side;
                // The cross street on this side: the road leaving the crossing most squarely to this side of the expressway.
                RoadEdge street = null;
                double bestDot = 0.5;
                foreach (RoadEdge candidate in node.Edges)
                {
                    if (!candidate.Alive || candidate.Class == RoadClass.Highway || candidate.Bridge) continue;
                    double dot = Vec2.Dot((candidate.Other(node).P - node.P).Normalized, perp);
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        street = candidate;
                    }
                }
                if (street == null)
                {
                    Rejections[3]++;
                    continue;
                }
                Vec2 streetDir = (street.Other(node).P - node.P).Normalized;
                double toStreet = RampToStreet * ls;
                if (street.Length < toStreet + ClearOfJunction * ls)
                {
                    Rejections[4]++;
                    continue;
                }
                Vec2 e = node.P + streetDir * toStreet;

                // The ramp: out from the deck's edge, alongside it, then down across to the street's kerb on the expressway's side of the street.
                Vec2 start = s + perp * (hw + rh);
                Vec2 streetNormal = streetDir.Perp;
                if (Vec2.Dot(streetNormal, d) < 0.0) streetNormal = -streetNormal;   // toward the ramp's quadrant
                double streetHalf = CityPlan.HalfWidth(cfg, street.Class);
                Vec2 foot = e + streetNormal * streetHalf;

                // Long enough for its grade: the drop is from the deck's top at the start (as PlanGeometry will lay it: over a dip the deck is
                // well above the clearance) to the ground at the foot. The stretch alongside the deck grows until the whole ramp is long enough,
                // as far as the crossing allows.
                double drop = DeckTopAt(fields, cfg, highway, prev, along) - fields.Height(foot.X, foot.Y);
                if (drop < MinDrop)
                {
                    Rejections[2]++;   // the cross street is up the hill, level with the deck: nothing to come down from
                    continue;
                }
                double taper = RampTaper * ls;
                double across = (foot - (start - d * taper)).Length;
                double needed = drop / MaxGrade;
                if (taper + across < needed)
                {
                    taper = Math.Min(along - MinAcross * ls, needed - across);
                }
                Vec2 mid = start - d * taper;
                across = (foot - mid).Length;
                if (taper < RampTaper * ls * 0.5 || drop / (taper + across) > MaxGrade * 1.15)
                {
                    Rejections[2]++;
                    continue;
                }

                // Nothing may cross the stretch that comes down to the ground (the street a ramp lands on excepted), nothing may lie under its foot,
                // and all of it must be dry land inside the region. (The stretch alongside the deck may pass over a side street.)
                if (net.AnyNodeNear(e, ClearOfJunction * ls, node))
                {
                    Rejections[6]++;
                    continue;
                }
                Vec2 rampDir = (foot - mid).Normalized;
                Vec2 rampPerp = rampDir.Perp * rh;
                if (net.CrossesAnyRoad(mid, foot, street, highway)
                    || net.CrossesAnyRoad(mid + rampPerp, foot + rampPerp, street, highway) || net.CrossesAnyRoad(mid - rampPerp, foot - rampPerp, street, highway))
                {
                    Rejections[5]++;
                    continue;
                }
                bool bad = false;
                foreach (Vec2 p in new[] { start, mid, foot, e })
                {
                    if (Math.Abs(p.X) > cfg.Half || Math.Abs(p.Y) > cfg.Half || fields.IsWater(p))
                    {
                        bad = true;
                    }
                }
                if (bad)
                {
                    Rejections[7]++;
                    continue;
                }

                // Right-hand traffic: the carriageway on this side of the deck travels the way whose right-hand side this is.
                Vec2 travel = d;
                if (Vec2.Dot(-travel.Perp, perp) < 0.0) travel = -d;
                bool leaving = Vec2.Dot(travel, node.P - s) > 0.0;   // that traffic reaches the ramp's start before the crossing: it is where it gets off

                double keepOut = rh + cfg.SidewalkWidth;
                Vec2 outward = rampDir.Perp;
                if (Vec2.Dot(outward, perp) < 0.0) outward = -outward;
                var hull = ConvexHull(new List<Vec2>
                {
                    s, node.P, e, foot, foot + outward * keepOut, mid + outward * keepOut, mid + perp * keepOut, start + perp * keepOut, s + perp * keepOut,
                });
                var ramp = new RampPlan
                {
                    ExitNode = node.Id,
                    HighwayEdge = highway.Id,
                    SAlong = highway.A == prev ? along : highway.Length - along,
                    S = s,
                    Start = start,
                    Mid = mid,
                    Foot = foot,
                    E = e,
                    StreetEdge = street.Id,
                    HalfWidth = rh,
                    Leaving = leaving,
                    Exclusion = hull,
                    Sliver = new[] { s + perp * hw, node.P + perp * hw, foot - rampPerp, mid - rampPerp, start - rampPerp },
                };
                ramp.MinX = ramp.MinY = double.MaxValue;
                ramp.MaxX = ramp.MaxY = double.MinValue;
                foreach (Vec2 p in hull)
                {
                    ramp.MinX = Math.Min(ramp.MinX, p.X);
                    ramp.MaxX = Math.Max(ramp.MaxX, p.X);
                    ramp.MinY = Math.Min(ramp.MinY, p.Y);
                    ramp.MaxY = Math.Max(ramp.MaxY, p.Y);
                }
                data.Ramps.Add(ramp);
            }
        }

        /// <summary>
        /// The deck's top <paramref name="along"/> an expressway edge from <paramref name="from"/>, as PlanGeometry.ComputeDeckTops will set it
        /// away from the ends: each node is the highest ground under any of its spans plus the clearance and the slab, and a span runs straight
        /// between its two nodes. The same sampling, so the ramp's grade is judged on the deck it will really leave.
        /// </summary>
        private static double DeckTopAt(PlanFields fields, PlanConfig cfg, RoadEdge edge, RoadNode from, double along)
        {
            double rise = cfg.DeckClearance + cfg.DeckThickness;
            double topFrom = NodeDeckTop(fields, from, rise);
            double topTo = NodeDeckTop(fields, edge.Other(from), rise);
            double t = edge.Length > 1e-9 ? along / edge.Length : 0.0;
            return topFrom + (topTo - topFrom) * t;
        }

        private static double NodeDeckTop(PlanFields fields, RoadNode node, double rise)
        {
            double top = double.MinValue;
            foreach (RoadEdge e in node.Edges)
            {
                if (!e.Alive || e.Class != RoadClass.Highway) continue;
                top = Math.Max(top, MaxGroundAlong(fields, e));
            }
            return top + rise;
        }

        private static double MaxGroundAlong(PlanFields fields, RoadEdge e)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(e.Length / PlanGeometry.DeckSampleStep));
            double top = double.MinValue;
            Vec2 a = e.A.P, b = e.B.P;
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                top = Math.Max(top, fields.Height(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            }
            return top;
        }

        /// <summary>
        /// How far it is along the chain of expressway edges from <paramref name="from"/> (reached by <paramref name="arrived"/>) to the
        /// expressway's end -- a node with one expressway edge -- or more than <paramref name="limit"/> if a junction of expressways comes first
        /// or the end is further than that.
        /// </summary>
        private static double ChainDistanceToEnd(RoadNode from, RoadEdge arrived, double limit)
        {
            RoadNode node = from;
            RoadEdge edge = arrived;
            double walked = 0.0;
            for (int guard = 0; guard < 64 && walked <= limit; guard++)
            {
                RoadEdge onward = null;
                int highways = 0;
                foreach (RoadEdge e in node.Edges)
                {
                    if (!e.Alive || e.Class != RoadClass.Highway) continue;
                    highways++;
                    if (e != edge) onward = e;
                }
                if (highways == 1)
                {
                    return walked;   // the end
                }
                if (highways != 2 || onward == null)
                {
                    return limit + 1.0;   // a junction of expressways: the deck carries on at height
                }
                walked += onward.Length;
                node = onward.Other(node);
                edge = onward;
            }
            return limit + 1.0;
        }

        /// <summary>Andrew's monotone chain: the convex hull of a few points, counter-clockwise. Arithmetic only.</summary>
        public static Vec2[] ConvexHull(List<Vec2> points)
        {
            points.Sort((a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Y.CompareTo(b.Y);
            });
            int n = points.Count;
            if (n < 3)
            {
                return points.ToArray();
            }
            var hull = new Vec2[2 * n];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && Vec2.Cross(hull[k - 1] - hull[k - 2], points[i] - hull[k - 2]) <= 0.0) k--;
                hull[k++] = points[i];
            }
            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Vec2.Cross(hull[k - 1] - hull[k - 2], points[i] - hull[k - 2]) <= 0.0) k--;
                hull[k++] = points[i];
            }
            var result = new Vec2[k - 1];
            Array.Copy(hull, result, k - 1);
            return result;
        }

        /// <summary>Whether a lot (a polygon) touches a ramp's exclusion: a corner of either inside the other, or an edge crossing.</summary>
        public static bool Touches(RampPlan ramp, IList<Vec2> lot)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Vec2 p in lot)
            {
                x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X);
                y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y);
            }
            if (x1 < ramp.MinX || x0 > ramp.MaxX || y1 < ramp.MinY || y0 > ramp.MaxY)
            {
                return false;
            }
            return PlanMath.PolygonsIntersect(lot, ramp.Exclusion);
        }
    }
}
