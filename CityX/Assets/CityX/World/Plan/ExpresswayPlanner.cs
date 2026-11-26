using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// Lays the city's expressways over the finished street network: a RING round the downtown and a couple of routes RIGHT ACROSS the region,
    /// each in its own corridor, up on a viaduct over the streets it crosses, down on an embankment in open country; where two meet, one passes
    /// OVER the other and a cloverleaf of connectors joins them; every so often an EXIT: ramps that peel off the deck, curve and slope down,
    /// and meet a street.
    ///
    /// WHY IT IS ITS OWN LAYER (owner, 2026-10-13, after the first version: "all the highways are above streets parallel to them which doesnt
    /// happen that often in real life", a four-way crossing of two decks on the flat "very glitched and doesnt work in real life", an end that
    /// "goes downward and sinks into a street blocking the street below"). The first expressways were the deck laid over the boulevards each
    /// centre grew along, so every one hung right over a street of its own, met the next one on a flat plate and came down onto the road under it.
    /// What the research found instead (docs/HIGHWAYS.md):
    ///
    ///  * PLACEMENT. Urban freeways are laid in their own right-of-way: along rivers, rail and waterfronts, through cheaper fringe districts and
    ///    round the edge of downtown, crossing the street grid at whatever angle that gives (Horwood, "The Nature of Urban Freeway Systems",
    ///    HRB 1959; FHWA/Caltrans). A city's network is radials plus an inner loop and an outer ring, a few kilometres apart -- the TRB studies
    ///    found every point within about 2 miles of one. Games compress the same plan: GTA V's Los Santos has four freeways through the whole
    ///    city meeting only at interchanges; Saints Row: The Third's Steelport one loop and a cross link along its shores. Here: one ring and
    ///    <see cref="PlanConfig.ThroughRoutes"/> routes across, smooth (no curve tighter than a few hundred metres), nudged off water and steep
    ///    ground and away from the very centre, running out of the region at both ends -- an expressway does not just stop in the middle of a city.
    ///  * HEIGHT. Over a street the deck stands well clear (real elevated roads 7-10 m; <see cref="StreetClearance"/> at 1.3x), higher still
    ///    where it runs along a street rather than across it; in the city it stays up; in open country with nothing to cross it comes down to an
    ///    embankment (TxDOT: elevated where the ground or the land below demands it, at grade beside rail and water) -- rising again, at no more than
    ///    <see cref="MainlineGrade"/>, to pass over the next road.
    ///  * INTERCHANGES. "All connections to freeways are by traffic interchanges" (Caltrans HDM 501.2): two expressways never meet on the flat.
    ///    The later route passes <see cref="LevelGap"/> over the earlier; connectors (outer curves for the right turns, 270-degree loops for the
    ///    left: a cloverleaf) join them. Exits come every <see cref="ExitSpacing"/> or so, as on a downtown freeway (owner: "you should mostly be
    ///    entering the highway from a street and exiting onto one as well"), clear of the cloverleafs, each with an off- and an on-ramp for each
    ///    direction onto a cross street, meeting it at right angles (HDM 502.2: "ramp termini should be perpendicular to the local road") or
    ///    merging along it (a loop); ramp grades at most <see cref="RampGrade"/> (HDM: 8%).
    ///
    /// Pure and deterministic like the rest of the plan: trig only from DMath, choices from hashes of the seed.
    /// </summary>
    public sealed partial class ExpresswayPlanner
    {
        // ---- design numbers, game metres (the city is 1.3x real) ----

        /// <summary>Samples along every deck this far apart.</summary>
        public const double SampleStep = 6.0;

        /// <summary>The steepest a main line climbs (AASHTO: 3-5% on urban freeways).</summary>
        public const double MainlineGrade = 0.04;

        /// <summary>The steepest a ramp climbs (Caltrans HDM 504.2(5): not over 8%; TxDOT 5-7% by ramp speed).</summary>
        public const double RampGrade = 0.08;

        /// <summary>The main deck's slab, and a ramp's.</summary>
        public const double MainlineSlab = 1.4;
        public const double RampSlab = 1.0;

        /// <summary>A main deck's underside over any road it crosses (real elevated urban roads stand 7-10 m up; a statutory 5 m is the least).</summary>
        public const double StreetClearance = 9.5;

        /// <summary>...and over a road it runs ALONG rather than across: higher (owner: "if it does then it is higher not typically right above").</summary>
        public const double ParallelClearance = 12.5;

        /// <summary>One deck's underside over another deck's top where two expressways cross (16.5 ft = 5 m real, plus room for the connectors' piers).</summary>
        public const double LevelGap = 9.0;

        /// <summary>A ramp or connector's underside over a road it passes over (16.5 ft = 5.0 m is the US standard; the player is 2.6 m tall).</summary>
        public const double RampClearance = 5.3;

        /// <summary>In open country the carriageway sits this far above the ground: an embankment.</summary>
        public const double EmbankmentRise = 0.9;

        /// <summary>From this population up, the main line stays up on its viaduct the whole way: a city viaduct, not a roller coaster.</summary>
        public const double UrbanPopulation = 0.22;

        /// <summary>
        /// Exits this far apart along a route, classic metres times the road scale: a downtown freeway's spacing (ramps every few blocks, as on
        /// GTA V's freeways through Los Santos), well inside Caltrans's suburban mile -- so most ramps a player meets come from and lead to a street.
        /// </summary>
        public const double ExitSpacing = 450.0;

        /// <summary>An exit keeps this far from a freeway-to-freeway interchange, and its ramps' tapers keep clear of the cloverleaf's own.</summary>
        public const double SystemSpacing = 400.0;

        /// <summary>Only built-up country gets exits.</summary>
        public const double ExitPopulation = 0.14;

        /// <summary>A ramp runs alongside its main line this far before it peels away (the deceleration lane) or after it joins (acceleration).</summary>
        public const double Taper = 45.0;

        /// <summary>The land between a ramp peeling away and its main line is paved over (a gore) until the gap is this wide: then the nose, and parapets.</summary>
        public const double GoreWidth = 3.0;

        /// <summary>The last stretch of a ramp before it meets a street runs straight at it, this long, across the pavement.</summary>
        public const double Landing = 14.0;

        /// <summary>A ramp's foot keeps this far from a junction on its street (classic metres times the road scale).</summary>
        public const double FootClearOfJunction = 24.0;

        /// <summary>The road's surface floats this far above the ground (PlanGeometry.RoadLift), and the pavement this far above the road.</summary>
        private const double RoadLift = PlanGeometry.RoadLift;

        private readonly RoadNetwork _net;
        private readonly PlanFields _fields;
        private readonly PlanConfig _cfg;
        private readonly uint _seed;
        private readonly double _r;
        private readonly double _w;
        private readonly double _wr;
        private readonly ExpresswayNetwork _out = new ExpresswayNetwork();
        private readonly List<RoadEdge> _edgeScratch = new List<RoadEdge>();

        /// <summary>The OUTLYING urban centres (CityPlan's, beyond the downtown): the routes across are aimed at them, and exits land beside them.</summary>
        private readonly List<Vec2> _satellites = new List<Vec2>();

        /// <summary>
        /// A centre this near a route's line (metres, times the road scale) is served by it; an exit this near a centre is that centre's; a centre
        /// this far from the origin (a share of the ring's radius) is outlying, not downtown. Owner, 2026-10-15: "the highways as they are now feel
        /// pointless they just go nowhere ... make them function the same to connect our cities": a real network's radials run from the ring out
        /// to the satellite towns and the next cities (FHWA's 1944 interregional plan; Horwood 1959), and that is where its exits are.
        /// </summary>
        public const double SatelliteReach = 600.0;

        public const double SatelliteExitReach = 900.0;

        public const double SatelliteFromRing = 0.6;

        /// <summary>How hard a route's line is pulled toward an outlying centre it passes (a cost taken off, see PlaceCost), and the most routes across.</summary>
        public const double SatellitePull = 0.6;

        public const int MaxThroughRoutes = 4;

        /// <summary>
        /// Two outlying centres on opposite sides of the downtown (the angle between them within about 70 degrees of a straight line) share one
        /// route through both; a centre with no partner well outside the ring gets a BYPASS, a route past it along its tangent; one nearer in gets
        /// none of its own (the exits of whatever passes it come first, NearCentre). Every route through the downtown crosses every other one
        /// there and the ring twice, and each crossing is a cloverleaf: a real city has two or three freeway-to-freeway interchanges, not nine
        /// (owner, 2026-10-15, of the first try: "highway clusters where they just spin around eachother and go nowhere").
        /// </summary>
        public const double SatellitePairCos = 0.3;

        /// <summary>What happened to the candidates, for the plan harness: counts by reason.</summary>
        [ThreadStatic] public static Dictionary<string, int> Tally;

        private ExpresswayPlanner(RoadNetwork net, PlanFields fields, PlanConfig cfg, uint seed, List<Vec2> centres)
        {
            _net = net;
            _fields = fields;
            _cfg = cfg;
            _seed = seed;
            _w = cfg.ExpresswayHalfWidth;
            _wr = cfg.RampHalfWidth;
            _r = cfg.ExpresswayHalfWidth / 12.0;   // the road scale: 1 in the classic city
            // The plan's own centres (the same call CityPlan.Generate makes, when it does not hand them in), the downtown's left out.
            foreach (Vec2 c in centres ?? fields.FindCentres(cfg.Half, 100.0 * cfg.Scale, 650.0 * cfg.Scale, 0.52, 6))
            {
                if (c.Length > SatelliteFromRing * cfg.RingRadius)
                {
                    _satellites.Add(c);
                }
            }
        }

        /// <summary>The expressways of a region, over its finished road network; <paramref name="centres"/> are the plan's urban centres (CityPlan).</summary>
        public static ExpresswayNetwork Plan(RoadNetwork net, PlanFields fields, PlanConfig cfg, uint seed, List<Vec2> centres = null)
        {
            Tally = new Dictionary<string, int>();
            var planner = new ExpresswayPlanner(net, fields, cfg, seed, centres);
            planner.LayRoutes();
            planner.LayProfiles();
            planner.SystemInterchanges();
            planner.Exits();
            planner.Furnish();
            planner._out.Index();
            return planner._out;
        }

        /// <summary>The last reason something was turned down, for the harness's debug output.</summary>
        [ThreadStatic] private static string _lastReason;

        /// <summary>While one shape of ramp is being tried, its name: the harness counts each shape's failures apart.</summary>
        [ThreadStatic] private static string _tallyPrefix;

        private static void Count(string what)
        {
            _lastReason = what;
            if (Tally == null) return;
            if (_tallyPrefix != null) what = _tallyPrefix + what;
            Tally.TryGetValue(what, out int c);
            Tally[what] = c + 1;
        }

        private double Rand(uint salt) => PlanNoise.Mix(_seed, salt) / 4294967296.0;

        private double Rand(uint salt, int index) => PlanNoise.Mix(PlanNoise.Mix(_seed, salt), unchecked((uint)index)) / 4294967296.0;

        private bool InRegion(Vec2 p, double margin = 0.0) => Math.Abs(p.X) <= _cfg.Half - margin && Math.Abs(p.Y) <= _cfg.Half - margin;

        private double Ground(Vec2 p) => _fields.Height(p.X, p.Y);

        // ================================================================== routes

        private void LayRoutes()
        {
            if (_cfg.RingRadius > 0.0)
            {
                List<Vec2> ring = RingAlignment();
                if (ring != null) AddMainline(ring, true);
            }
            // The routes across are aimed at the outlying centres (the satellite towns), strongest first: a centre gets a route passing beside it,
            // two on opposite sides of the downtown share one, and a centre that another route already passes needs none; then, up to the
            // configured count, routes at hashed angles as before.
            var served = new bool[_satellites.Count];
            int laid = 0;
            for (int i = 0; i < _satellites.Count && laid < MaxThroughRoutes; i++)
            {
                if (served[i]) continue;
                Vec2 a = _satellites[i];
                int partner = -1;
                double bestCos = SatellitePairCos;
                for (int j = i + 1; j < _satellites.Count; j++)
                {
                    if (served[j]) continue;
                    double cos = Vec2.Dot(a.Normalized, (_satellites[j] * -1.0).Normalized);
                    if (cos > bestCos)
                    {
                        bestCos = cos;
                        partner = j;
                    }
                }
                served[i] = true;
                if (partner >= 0) served[partner] = true;
                List<Vec2> route = RadialAlignment(laid, a, partner >= 0 ? _satellites[partner] : (Vec2?)null);
                if (route == null) continue;
                AddMainline(route, false);
                laid++;
                for (int j = 0; j < _satellites.Count; j++)
                {
                    if (!served[j] && DistanceToPolyline(_satellites[j], route) <= SatelliteReach * _r) served[j] = true;
                }
            }
            for (int k = 0; laid < _cfg.ThroughRoutes && k < MaxThroughRoutes; k++)
            {
                List<Vec2> route = ThroughAlignment(k);
                if (route == null) continue;
                AddMainline(route, false);
                laid++;
            }
        }

        /// <summary>The nearest a point comes to a route's line.</summary>
        private static double DistanceToPolyline(Vec2 p, List<Vec2> pts)
        {
            double best = double.MaxValue;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                best = Math.Min(best, PlanMath.DistanceToSegment(p, pts[i], pts[i + 1], out _));
            }
            return best;
        }

        /// <summary>Whether a point is beside one of the outlying centres (an exit there is that centre's).</summary>
        private bool NearSatellite(Vec2 p)
        {
            foreach (Vec2 s in _satellites)
            {
                if ((p - s).Length <= SatelliteExitReach * _r) return true;
            }
            return false;
        }

        /// <summary>
        /// The ring: a closed curve round the origin at about <see cref="PlanConfig.RingRadius"/>, swelling and narrowing a little (two low
        /// harmonics with hashed phases), each control point free to move a tenth of the radius in or out to keep off water and steep ground and
        /// not to run along a street (<see cref="Choose"/>).
        /// </summary>
        private List<Vec2> RingAlignment()
        {
            double r0 = _cfg.RingRadius;
            const int n = 36;
            const int choices = 9;
            double ph2 = Rand(0x72696E32U) * DMath.TwoPi, ph3 = Rand(0x72696E33U) * DMath.TwoPi;
            var position = new Vec2[n, choices];
            var cost = new double[n, choices];
            for (int k = 0; k < n; k++)
            {
                double th = DMath.TwoPi * k / n;
                double radius = r0 * (1.0 + 0.09 * DMath.Sin(2.0 * th + ph2) + 0.05 * DMath.Sin(3.0 * th + ph3));
                Vec2 dir = DMath.Dir(th);
                for (int c = 0; c < choices; c++)
                {
                    double delta = r0 * 0.1 * (2.0 * c / (choices - 1) - 1.0);
                    Vec2 q = Clamp(dir * (radius + delta), _cfg.Half - 300.0 * _r);
                    position[k, c] = q;
                    cost[k, c] = PlaceCost(q, 0.0) + 0.1 * (delta / (r0 * 0.1)) * (delta / (r0 * 0.1));
                }
            }
            int[] pick = Choose(position, cost, true);
            var ctrl = new List<Vec2>(n);
            for (int k = 0; k < n; k++) ctrl.Add(position[k, pick[k]]);
            SmoothClosed(ctrl, 1);
            List<Vec2> pts = Spline(ctrl, true, SampleStep);
            return pts.Count >= 8 ? pts : null;
        }

        /// <summary>
        /// A route across the region: a line at a hashed angle (the routes spread evenly round the compass), offset from the origin so it skirts
        /// the downtown rather than running through the spawn, bent by up to a fifth of the ring's radius either way to keep off water, steep
        /// ground, the very centre and the streets it would otherwise run along (<see cref="Choose"/>), then smoothed and cut to the region.
        /// </summary>
        private List<Vec2> ThroughAlignment(int k)
        {
            int count = Math.Max(1, _cfg.ThroughRoutes);
            double r0 = Math.Max(_cfg.RingRadius, 600.0 * _r);
            double phi = Rand(0x74687230U) * DMath.Pi + k * DMath.Pi / count + (Rand(0x74687231U, k) - 0.5) * 0.4;
            Vec2 u = DMath.Dir(phi);
            Vec2 nrm = u.Perp;
            double side = (k & 1) == 0 ? 1.0 : -1.0;
            double offset = side * r0 * (0.13 + 0.09 * Rand(0x74687232U, k));
            return AlignmentAlong(u, nrm * offset, r0);
        }

        /// <summary>
        /// A route across the region aimed at an outlying centre <paramref name="a"/>: with <paramref name="b"/> (one on the far side of the
        /// downtown) the line through both, moved sideways if need be to skirt the downtown as the hashed routes do; alone, a bypass along the
        /// tangent at <paramref name="a"/> (past the town, not into the downtown); then bent and cut like the hashed routes.
        /// </summary>
        private List<Vec2> RadialAlignment(int k, Vec2 a, Vec2? b)
        {
            double r0 = Math.Max(_cfg.RingRadius, 600.0 * _r);
            // A bypass along the tangent only well outside the ring: nearer, a tangent meets the ring at a grazing angle no cloverleaf can be laid
            // on (the test city's lone centre sat on the ring: "0 loops and 1 curves of 8"). Nearer in, the line toward the downtown, pushed
            // sideways a third of the ring's radius so it crosses the ring squarely and skirts the centre.
            bool tangent = !b.HasValue && a.Length >= BypassFromRing * r0;
            Vec2 u = b.HasValue ? (b.Value - a).Normalized : tangent ? a.Normalized.Perp : a.Normalized;
            Vec2 nrm = u.Perp;
            Vec2 lineOrigin = a - u * Vec2.Dot(a, u);   // the line's point nearest the origin
            double off = Vec2.Dot(lineOrigin, nrm);
            double least = r0 * (b.HasValue ? 0.13 + 0.09 * Rand(0x72616431U, k) : 0.35);   // "rad1"
            if (Math.Abs(off) < least)
            {
                double side = off > 0.0 ? 1.0 : off < 0.0 ? -1.0 : ((k & 1) == 0 ? 1.0 : -1.0);
                lineOrigin = nrm * (side * least);
            }
            // A lone centre nearer in than a bypass allows gets no route of its own: a line through it toward the downtown crossed the ring
            // where there was no room for a cloverleaf (the test city) and added two interchanges for one town.
            if (!b.HasValue && !tangent)
            {
                return null;
            }
            List<Vec2> route = AlignmentAlong(u, lineOrigin, r0);
            // A stub (a tangent clipped short by the region's corner) is no route.
            return route != null && Length(route) >= MinRouteShare * 2.0 * _cfg.Half ? route : null;
        }

        /// <summary>A lone centre at least this many ring radii out gets a tangent bypass; a route across must be at least this share of the region wide.</summary>
        public const double BypassFromRing = 1.5;

        public const double MinRouteShare = 0.6;

        private static double Length(List<Vec2> pts)
        {
            double length = 0.0;
            for (int i = 0; i + 1 < pts.Count; i++) length += (pts[i + 1] - pts[i]).Length;
            return length;
        }

        /// <summary>The route along the line through <paramref name="lineOrigin"/> in direction <paramref name="u"/>, bent off what it should keep off, smoothed and cut.</summary>
        private List<Vec2> AlignmentAlong(Vec2 u, Vec2 lineOrigin, double r0)
        {
            Vec2 nrm = u.Perp;
            double step = 320.0 * _r;
            double extent = _cfg.Half * 1.6;
            int m = (int)(2.0 * extent / step) + 1;
            const int choices = 9;
            double reach = r0 * 0.2;
            var cost = new double[m, choices];
            var position = new Vec2[m, choices];
            for (int j = 0; j < m; j++)
            {
                Vec2 baseP = lineOrigin + u * (-extent + j * step);
                for (int c = 0; c < choices; c++)
                {
                    double delta = -reach + 2.0 * reach * c / (choices - 1);
                    Vec2 q = baseP + nrm * delta;
                    position[j, c] = q;
                    cost[j, c] = PlaceCost(q, r0) + 0.12 * (delta / reach) * (delta / reach);
                }
            }
            int[] pick = Choose(position, cost, false);
            var ctrl = new List<Vec2>(m);
            for (int j = 0; j < m; j++) ctrl.Add(position[j, pick[j]]);
            List<Vec2> pts = Spline(ctrl, false, SampleStep);
            return ClipToRegion(pts);
        }

        /// <summary>
        /// The cheapest smooth choice of one candidate position per control point (Viterbi): each position's own cost, plus between neighbours a
        /// cost for jumping sideways and the share of the stretch that would run ALONG a street (owner: an expressway right over a street it
        /// follows "doesnt happen that often in real life"). A closed route (the ring) is chosen as a loop, its last point joined to its first.
        /// </summary>
        private int[] Choose(Vec2[,] position, double[,] cost, bool closed)
        {
            int m = position.GetLength(0), choices = position.GetLength(1);
            var step = new double[m, choices, choices];   // into point j at c from point j-1 (j = 0: from the last, for a loop) at p
            for (int j = closed ? 0 : 1; j < m; j++)
            {
                int prev = (j - 1 + m) % m;
                for (int p = 0; p < choices; p++)
                {
                    for (int c = 0; c < choices; c++)
                    {
                        double jump = (c - p) / (double)(choices - 1);
                        step[j, p, c] = 2.5 * jump * jump + 3.0 * AlongStreetShare(position[prev, p], position[j, c]);
                    }
                }
            }
            int[] bestPick = null;
            double bestTotal = double.MaxValue;
            int starts = closed ? choices : 1;
            for (int start = 0; start < starts; start++)
            {
                var best = new double[m, choices];
                var from = new int[m, choices];
                for (int c = 0; c < choices; c++) best[0, c] = closed && c != start ? double.MaxValue : cost[0, c];
                for (int j = 1; j < m; j++)
                {
                    for (int c = 0; c < choices; c++)
                    {
                        double bestPrev = double.MaxValue;
                        int arg = 0;
                        for (int p = 0; p < choices; p++)
                        {
                            if (best[j - 1, p] == double.MaxValue) continue;
                            double v = best[j - 1, p] + step[j, p, c];
                            if (v < bestPrev - 1e-12)
                            {
                                bestPrev = v;
                                arg = p;
                            }
                        }
                        best[j, c] = bestPrev == double.MaxValue ? double.MaxValue : bestPrev + cost[j, c];
                        from[j, c] = arg;
                    }
                }
                int end = -1;
                double total = double.MaxValue;
                for (int c = 0; c < choices; c++)
                {
                    if (best[m - 1, c] == double.MaxValue) continue;
                    double t = best[m - 1, c] + (closed ? step[0, c, start] : 0.0);
                    if (t < total - 1e-12)
                    {
                        total = t;
                        end = c;
                    }
                }
                if (end < 0 || total >= bestTotal - 1e-12) continue;
                bestTotal = total;
                bestPick = new int[m];
                for (int j = m - 1; j >= 0; j--)
                {
                    bestPick[j] = end;
                    end = from[j, end];
                }
            }
            return bestPick ?? new int[m];
        }

        /// <summary>The share of a straight stretch (sampled every 30 m) that lies along a street running the same way, near enough that a deck there would be over it.</summary>
        private double AlongStreetShare(Vec2 a, Vec2 b)
        {
            Vec2 d = b - a;
            double length = d.Length;
            if (length < 1.0) return 0.0;
            Vec2 u = d / length;
            int k = Math.Max(1, (int)(length / 30.0));
            int hits = 0, counted = 0;
            for (int i = 0; i <= k; i++)
            {
                Vec2 p = a + d * (i / (double)k);
                if (!InRegion(p)) continue;
                counted++;
                _net.EdgesNear(p, p, _w + _cfg.BoulevardHalfWidth + 4.0, _edgeScratch);
                foreach (RoadEdge e in _edgeScratch)
                {
                    if (Math.Abs(Vec2.Dot((e.B.P - e.A.P).Normalized, u)) < 0.9) continue;
                    if (PlanMath.DistanceToSegment(p, e.A.P, e.B.P, out _) < _w * 0.7 + CityPlan.HalfWidth(_cfg, e.Class))
                    {
                        hits++;
                        break;
                    }
                }
            }
            return counted > 0 ? hits / (double)counted : 0.0;
        }

        /// <summary>How unwelcome a control point is: water (a bridge is dear), steep ground, the very centre, a dense district (dear land).</summary>
        private double PlaceCost(Vec2 q, double r0)
        {
            if (!InRegion(q)) return 0.0;   // beyond the region nothing is built: any line will do
            double wet = 0.0;
            for (int k = 0; k < 5; k++)
            {
                Vec2 s = q + (k == 0 ? new Vec2(0.0, 0.0) : DMath.Dir(k * DMath.HalfPi) * (60.0 * _r));
                if (_fields.IsWater(s)) wet += 0.2;
            }
            double slope = _fields.Slope(q.X, q.Y);
            double centre = r0 > 0.0 && q.Length < 0.3 * r0 ? (0.3 * r0 - q.Length) / (0.3 * r0) : 0.0;
            // Pulled toward an outlying centre it passes (SatellitePull), so the route serves the town rather than skirting it wide.
            double pull = 0.0;
            foreach (Vec2 s in _satellites)
            {
                double d = (q - s).Length / (SatelliteReach * _r);
                pull = Math.Max(pull, Math.Exp(-d * d));
            }
            return wet * 2.0 + slope * 3.0 + centre * 2.0 + _fields.Population(q) * 0.25 - pull * SatellitePull;
        }

        private static Vec2 Clamp(Vec2 p, double half) => new Vec2(Math.Max(-half, Math.Min(half, p.X)), Math.Max(-half, Math.Min(half, p.Y)));

        private static void SmoothClosed(List<Vec2> pts, int passes)
        {
            int n = pts.Count;
            for (int pass = 0; pass < passes; pass++)
            {
                var copy = new List<Vec2>(pts);
                for (int i = 0; i < n; i++)
                {
                    pts[i] = copy[(i + n - 1) % n] * 0.25 + copy[i] * 0.5 + copy[(i + 1) % n] * 0.25;
                }
            }
        }

        /// <summary>
        /// A centripetal Catmull-Rom spline through control points (closed, or open with its ends extended), resampled at an even spacing. Smooth,
        /// passes through every control point, and never overshoots into loops (the centripetal parameterisation).
        /// </summary>
        public static List<Vec2> Spline(List<Vec2> ctrl, bool closed, double step)
        {
            int n = ctrl.Count;
            if (n < 2) return new List<Vec2>(ctrl);
            var dense = new List<Vec2>();
            int spans = closed ? n : n - 1;
            for (int i = 0; i < spans; i++)
            {
                Vec2 p1 = ctrl[i];
                Vec2 p2 = ctrl[(i + 1) % n];
                Vec2 p0 = closed ? ctrl[(i + n - 1) % n] : (i > 0 ? ctrl[i - 1] : p1 * 2.0 - p2);
                Vec2 p3 = closed ? ctrl[(i + 2) % n] : (i + 2 < n ? ctrl[i + 2] : p2 * 2.0 - p1);
                double chord = (p2 - p1).Length;
                int pieces = Math.Max(4, (int)Math.Ceiling(chord / (step * 0.5)));
                for (int k = 0; k < pieces; k++)
                {
                    dense.Add(CatmullRom(p0, p1, p2, p3, k / (double)pieces));
                }
            }
            dense.Add(closed ? ctrl[0] : ctrl[n - 1]);
            return Resample(dense, step, closed);
        }

        private static double Knot(Vec2 a, Vec2 b) => Math.Max(1e-6, Math.Sqrt((b - a).Length));

        private static Vec2 CatmullRom(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, double f)
        {
            double t0 = 0.0;
            double t1 = t0 + Knot(p0, p1);
            double t2 = t1 + Knot(p1, p2);
            double t3 = t2 + Knot(p2, p3);
            double t = t1 + (t2 - t1) * f;
            Vec2 a1 = p0 * ((t1 - t) / (t1 - t0)) + p1 * ((t - t0) / (t1 - t0));
            Vec2 a2 = p1 * ((t2 - t) / (t2 - t1)) + p2 * ((t - t1) / (t2 - t1));
            Vec2 a3 = p2 * ((t3 - t) / (t3 - t2)) + p3 * ((t - t2) / (t3 - t2));
            Vec2 b1 = a1 * ((t2 - t) / (t2 - t0)) + a2 * ((t - t0) / (t2 - t0));
            Vec2 b2 = a2 * ((t3 - t) / (t3 - t1)) + a3 * ((t - t1) / (t3 - t1));
            return b1 * ((t2 - t) / (t2 - t1)) + b2 * ((t - t1) / (t2 - t1));
        }

        /// <summary>A polyline resampled at an even spacing by arc length (a closed one at a spacing that divides it exactly, ending on its start).</summary>
        public static List<Vec2> Resample(List<Vec2> line, double step, bool closed)
        {
            var cumulative = new double[line.Count];
            for (int i = 1; i < line.Count; i++) cumulative[i] = cumulative[i - 1] + (line[i] - line[i - 1]).Length;
            double total = cumulative[line.Count - 1];
            int pieces = Math.Max(1, (int)Math.Round(total / step));
            double spacing = total / pieces;
            var result = new List<Vec2>(pieces + 1);
            int j = 0;
            for (int k = 0; k <= pieces; k++)
            {
                double s = Math.Min(total, k * spacing);
                while (j < line.Count - 2 && cumulative[j + 1] < s) j++;
                double seg = cumulative[j + 1] - cumulative[j];
                double f = seg > 1e-12 ? (s - cumulative[j]) / seg : 0.0;
                result.Add(line[j] + (line[j + 1] - line[j]) * Math.Max(0.0, Math.Min(1.0, f)));
            }
            if (closed) result[result.Count - 1] = result[0];
            return result;
        }

        /// <summary>The longest run of a polyline inside the region, ending exactly on the region's edge.</summary>
        private List<Vec2> ClipToRegion(List<Vec2> pts)
        {
            int bestStart = -1, bestEnd = -1;
            for (int i = 0; i < pts.Count;)
            {
                if (!InRegion(pts[i]))
                {
                    i++;
                    continue;
                }
                int j = i;
                while (j + 1 < pts.Count && InRegion(pts[j + 1])) j++;
                if (bestStart < 0 || j - i > bestEnd - bestStart)
                {
                    bestStart = i;
                    bestEnd = j;
                }
                i = j + 1;
            }
            if (bestStart < 0 || bestEnd - bestStart < 20) return null;
            var run = new List<Vec2>();
            if (bestStart > 0) run.Add(EdgePoint(pts[bestStart - 1], pts[bestStart]));
            for (int i = bestStart; i <= bestEnd; i++) run.Add(pts[i]);
            if (bestEnd + 1 < pts.Count) run.Add(EdgePoint(pts[bestEnd + 1], pts[bestEnd]));
            return run;
        }

        /// <summary>Where the segment from a point outside the region to one inside it crosses the region's edge.</summary>
        private Vec2 EdgePoint(Vec2 outside, Vec2 inside)
        {
            double lo = 0.0, hi = 1.0;   // fraction from inside toward outside
            for (int k = 0; k < 40; k++)
            {
                double mid = (lo + hi) * 0.5;
                if (InRegion(inside + (outside - inside) * mid)) lo = mid; else hi = mid;
            }
            return inside + (outside - inside) * lo;
        }

        // ================================================================== paths

        /// <summary>A deck from its centreline: tangents by central differences, normals, distances; flat parapets on both edges.</summary>
        private ViaductPath MakePath(ViaductKind kind, List<Vec2> pts, List<Vec2> dirs, double halfLeft, double halfRight, double slab, bool closed)
        {
            int n = pts.Count;
            var path = new ViaductPath
            {
                Kind = kind,
                P = pts.ToArray(),
                N = new Vec2[n],
                S = new double[n],
                Top = new double[n],
                HalfLeft = new double[n],
                HalfRight = new double[n],
                Slab = slab,
                ParapetLeft = new bool[Math.Max(0, n - 1)],
                ParapetRight = new bool[Math.Max(0, n - 1)],
                Closed = closed,
            };
            for (int i = 0; i < n; i++)
            {
                Vec2 t;
                if (dirs != null)
                {
                    t = dirs[i];
                }
                else if (closed)
                {
                    // The last sample repeats the first: their neighbours are n-2 and 1.
                    int prev = i == 0 ? n - 2 : i - 1;
                    int next = i == n - 1 ? 1 : i + 1;
                    t = pts[next] - pts[prev];
                }
                else
                {
                    t = pts[Math.Min(n - 1, i + 1)] - pts[Math.Max(0, i - 1)];
                }
                path.N[i] = t.Normalized.Perp;
                if (i > 0) path.S[i] = path.S[i - 1] + (pts[i] - pts[i - 1]).Length;
                path.HalfLeft[i] = halfLeft;
                path.HalfRight[i] = halfRight;
            }
            for (int i = 0; i < n - 1; i++)
            {
                path.ParapetLeft[i] = true;
                path.ParapetRight[i] = true;
            }
            return path;
        }

        private ViaductPath Add(ViaductPath path)
        {
            path.Id = _out.Paths.Count;
            _out.Paths.Add(path);
            AddToGrid(path);
            return path;
        }

        private void AddMainline(List<Vec2> pts, bool ring)
        {
            ViaductPath path = MakePath(ViaductKind.Mainline, pts, null, _w, _w, MainlineSlab, ring);
            var route = new ExpresswayRoute { Id = _out.Routes.Count, Ring = ring };
            path.Route = route.Id;
            path.Shape = ring ? "ring" : "through";
            Add(path);
            route.Path = path.Id;
            _out.Routes.Add(route);
        }

        // ---- a working grid of every deck segment laid so far, for the clash checks while planning ----

        private const double GridCell = 64.0;
        private readonly Dictionary<long, List<long>> _grid = new Dictionary<long, List<long>>();

        private static long GridKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        private static int GridOf(double v) => (int)Math.Floor(v / GridCell);

        private void AddToGrid(ViaductPath path)
        {
            for (int i = 0; i < path.Segments; i++)
            {
                Vec2[] f = path.Footprint(i, 1.0);
                double x0 = Math.Min(Math.Min(f[0].X, f[1].X), Math.Min(f[2].X, f[3].X)), x1 = Math.Max(Math.Max(f[0].X, f[1].X), Math.Max(f[2].X, f[3].X));
                double y0 = Math.Min(Math.Min(f[0].Y, f[1].Y), Math.Min(f[2].Y, f[3].Y)), y1 = Math.Max(Math.Max(f[0].Y, f[1].Y), Math.Max(f[2].Y, f[3].Y));
                for (int cy = GridOf(y0); cy <= GridOf(y1); cy++)
                {
                    for (int cx = GridOf(x0); cx <= GridOf(x1); cx++)
                    {
                        long key = GridKey(cx, cy);
                        if (!_grid.TryGetValue(key, out List<long> list)) _grid[key] = list = new List<long>(4);
                        list.Add(((long)path.Id << 32) | (uint)i);
                    }
                }
            }
        }

        /// <summary>Every laid deck segment whose footprint (grown by <paramref name="margin"/>) covers a point, with its top there.</summary>
        private void DecksAt(Vec2 p, double margin, List<(int path, int seg, double top)> result)
        {
            result.Clear();
            int span = (int)Math.Ceiling(margin / GridCell);
            int cx0 = GridOf(p.X), cy0 = GridOf(p.Y);
            HashSet<long> seen = null;
            for (int cy = cy0 - span; cy <= cy0 + span; cy++)
            {
                for (int cx = cx0 - span; cx <= cx0 + span; cx++)
                {
                    if (!_grid.TryGetValue(GridKey(cx, cy), out List<long> list)) continue;
                    foreach (long r in list)
                    {
                        if (span > 0 && !(seen ?? (seen = new HashSet<long>())).Add(r)) continue;
                        int pathId = (int)(r >> 32), seg = (int)(uint)r;
                        ViaductPath v = _out.Paths[pathId];
                        if (!v.FootprintContains(seg, margin, p)) continue;
                        result.Add((pathId, seg, v.TopAt(seg, p)));
                    }
                }
            }
        }

        // ================================================================== profiles

        /// <summary>
        /// The height of every route's main deck: what each sample needs (over a road, over a river, in the city, under nothing at all), then
        /// the least smooth profile that meets every need climbing at no more than <see cref="MainlineGrade"/>. Routes are profiled in order, and a
        /// later route passing over an earlier one needs <see cref="LevelGap"/> over its top there: so no two expressways ever meet on the flat.
        /// </summary>
        private void LayProfiles()
        {
            foreach (ExpresswayRoute route in _out.Routes)
            {
                ViaductPath m = _out.Paths[route.Path];
                int n = m.Count;
                var need = new double[n];
                var ground = new double[n];
                for (int i = 0; i < n; i++)
                {
                    ground[i] = DeckGround(m, i);
                }
                for (int i = 0; i < n; i++)
                {
                    need[i] = Need(m, i, ground);
                }
                // Over the expressways already laid: a level higher.
                foreach (ExpresswayRoute earlier in _out.Routes)
                {
                    if (earlier.Id >= route.Id) break;
                    ViaductPath lower = _out.Paths[earlier.Path];
                    for (int i = 0; i < n; i++)
                    {
                        if (NearestOn(lower, m.P[i], out double s, out double distance) && distance < lower.HalfLeft[0] + m.HalfLeft[i] * 0.5 + 10.0 * _r)
                        {
                            lower.At(s, out _, out _, out double lowerTop);
                            need[i] = Math.Max(need[i], lowerTop + LevelGap + MainlineSlab);
                        }
                    }
                }
                m.Top = SmoothProfile(need, m.S, MainlineGrade, m.Closed, 4);
            }
        }

        /// <summary>The highest ground across a deck's width at a sample.</summary>
        private double DeckGround(ViaductPath m, int i)
        {
            double top = double.MinValue;
            for (int k = -2; k <= 2; k++)
            {
                Vec2 q = m.P[i] + m.N[i] * (k * 0.5 * (k < 0 ? m.HalfRight[i] : m.HalfLeft[i]));
                top = Math.Max(top, Ground(q));
            }
            return top;
        }

        /// <summary>What one sample of a main line needs: clear of a road under it (more if it runs along it), up in the city, over a river.</summary>
        private double Need(ViaductPath m, int i, double[] ground)
        {
            double g = ground[i];
            double need = g + EmbankmentRise;
            Vec2 p = m.P[i];
            if (_fields.Population(p) >= UrbanPopulation)
            {
                need = g + StreetClearance + MainlineSlab;
            }
            RoadsUnder(m, i, out bool over, out bool along);
            if (over)
            {
                need = Math.Max(need, g + (along ? ParallelClearance : StreetClearance) + MainlineSlab);
            }
            bool wet = false;
            for (int k = -1; k <= 1 && !wet; k++)
            {
                wet = _fields.IsWater(p + m.N[i] * (k * m.HalfLeft[i]));
            }
            if (wet)
            {
                // A bridge: no lower than the banks either side, whatever the trench under it.
                double bank = g;
                int reach = (int)Math.Ceiling(90.0 * _r / SampleStep);
                for (int j = Math.Max(0, i - reach); j <= Math.Min(m.Count - 1, i + reach); j++) bank = Math.Max(bank, ground[j]);
                need = Math.Max(need, bank + EmbankmentRise + 1.5);
            }
            return need;
        }

        /// <summary>Whether a road (with its pavements) lies under a main line's sample, and whether it runs along the deck rather than across it.</summary>
        private void RoadsUnder(ViaductPath m, int i, out bool over, out bool along)
        {
            over = false;
            along = false;
            Vec2 p = m.P[i];
            Vec2 t = m.N[i].Perp * -1.0;   // the tangent (N is its left normal)
            double reach = Math.Max(m.HalfLeft[i], m.HalfRight[i]) + _cfg.BoulevardHalfWidth + _cfg.SidewalkWidth + 2.0;
            _net.EdgesNear(p, p, reach, _edgeScratch);
            foreach (RoadEdge e in _edgeScratch)
            {
                double half = CityPlan.HalfWidth(_cfg, e.Class) + _cfg.SidewalkWidth;
                for (int k = -2; k <= 2; k++)
                {
                    Vec2 q = p + m.N[i] * (k * 0.5 * (k < 0 ? m.HalfRight[i] : m.HalfLeft[i]));
                    if (PlanMath.DistanceToSegment(q, e.A.P, e.B.P, out _) <= half)
                    {
                        over = true;
                        Vec2 d = (e.B.P - e.A.P).Normalized;
                        if (Math.Abs(Vec2.Dot(d, t)) > 0.9) along = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The least profile at or above every need that climbs and falls at no more than <paramref name="grade"/>: the upper envelope of
        /// cones (a forward and a backward pass), then rounded off -- a few passes of a moving average for the vertical curves, raised back
        /// to the needs, the cones again.
        /// </summary>
        public static double[] SmoothProfile(double[] need, double[] s, double grade, bool closed, int window)
        {
            int n = need.Length;
            var top = (double[])need.Clone();
            Envelope(top, s, grade, closed);
            for (int round = 0; round < 3; round++)
            {
                var blurred = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double sum = 0.0;
                    int count = 0;
                    for (int k = -window; k <= window; k++)
                    {
                        int j = i + k;
                        if (closed)
                        {
                            j = ((j % (n - 1)) + (n - 1)) % (n - 1);
                        }
                        else if (j < 0 || j >= n)
                        {
                            continue;
                        }
                        sum += top[j];
                        count++;
                    }
                    blurred[i] = sum / count;
                }
                if (closed) blurred[n - 1] = blurred[0];
                for (int i = 0; i < n; i++) top[i] = Math.Max(blurred[i], need[i]);
                Envelope(top, s, grade, closed);
            }
            return top;
        }

        private static void Envelope(double[] top, double[] s, double grade, bool closed)
        {
            int n = top.Length;
            for (int lap = 0; lap < (closed ? 2 : 1); lap++)
            {
                for (int i = 1; i < n; i++) top[i] = Math.Max(top[i], top[i - 1] - grade * (s[i] - s[i - 1]));
                if (closed) top[0] = Math.Max(top[0], top[n - 1]);
                for (int i = n - 2; i >= 0; i--) top[i] = Math.Max(top[i], top[i + 1] - grade * (s[i + 1] - s[i]));
                if (closed) top[n - 1] = Math.Max(top[n - 1], top[0]);
            }
            if (closed)
            {
                double m = Math.Max(top[0], top[n - 1]);
                top[0] = top[n - 1] = m;
            }
        }

        /// <summary>The nearest point of a path's centreline to a point: its distance along, and how far away. False if the path is empty.</summary>
        private static bool NearestOn(ViaductPath path, Vec2 p, out double along, out double distance)
        {
            along = 0.0;
            distance = double.MaxValue;
            for (int i = 0; i < path.Segments; i++)
            {
                double d = PlanMath.DistanceToSegment(p, path.P[i], path.P[i + 1], out double t);
                if (d < distance)
                {
                    distance = d;
                    along = path.S[i] + (path.S[i + 1] - path.S[i]) * t;
                }
            }
            return path.Segments > 0;
        }
    }
}
