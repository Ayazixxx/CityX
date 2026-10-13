using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    public sealed partial class ExpresswayPlanner
    {
        /// <summary>A main line's piers this far apart (real viaduct spans 20-55 m); a ramp's or connector's closer.</summary>
        public const double MainlinePierSpacing = 36.0;
        public const double RampPierSpacing = 24.0;

        /// <summary>Where the underside is lower than this over the ground the deck is an embankment: nothing to hold up.</summary>
        public const double PierMinimumHeight = 2.2;

        /// <summary>A pier's column keeps this far from any road's edge (pavement included), so it never stands in a street.</summary>
        public const double PierClearOfRoad = 1.2;

        /// <summary>Half the width of a main line's column (and of a twin's), and of a ramp's.</summary>
        public const double MainColumnHalf = 1.0;
        public const double RampColumnHalf = 0.6;

        /// <summary>Above this half width a main line stands on two columns, one under each carriageway, rather than one hammerhead.</summary>
        public const double TwinColumnsAbove = 9.0;

        private void Furnish()
        {
            foreach (ViaductPath path in _out.Paths)
            {
                Piers(path);
            }
            // Where a route leaves the region it is closed: a row of barriers across it (the evacuation's cordon).
            foreach (ExpresswayRoute route in _out.Routes)
            {
                if (route.Ring) continue;
                ViaductPath m = _out.Paths[route.Path];
                foreach (double s in new[] { 4.0, m.Length - 4.0 })
                {
                    m.At(s, out Vec2 p, out Vec2 t, out double top);
                    _out.Barriers.Add(new ExpresswayBarrier { P = p, Across = t.Perp, Top = top, HalfWidth = m.HalfLeft[0] });
                }
            }
        }

        /// <summary>
        /// The piers under a deck: every so often, shifted a few metres either way until each column stands clear of every road and of any deck
        /// passing under this one -- a span over a street is simply longer -- and none where the deck is down on its embankment.
        /// </summary>
        private void Piers(ViaductPath path)
        {
            bool main = path.Kind == ViaductKind.Mainline;
            double spacing = main ? MainlinePierSpacing : RampPierSpacing;
            double colHalf = main ? MainColumnHalf : RampColumnHalf;
            double phase = Rand(0x70696572U, path.Id) * spacing;
            double[] shifts = { 0.0, 4.0, -4.0, 8.0, -8.0, 12.0, -12.0 };
            var decks = new List<(int path, int seg, double top)>();
            for (double s = phase; s < path.Length; s += spacing)
            {
                foreach (double shift in shifts)
                {
                    double at = s + shift;
                    if (at < (main ? 1.0 : 10.0) || at > path.Length - (main ? 1.0 : 10.0)) continue;
                    path.At(at, out Vec2 p, out Vec2 t, out double top);
                    double underside = top - path.Slab;
                    int i = path.IndexAt(at);
                    if (!main && path.HalfRight[i] < 1.5) continue;   // a taper's narrow tip, held by its main line's own piers beside it
                    bool twin = main && path.HalfLeft[i] > TwinColumnsAbove;
                    Vec2 n = t.Perp;
                    Vec2[] columns = twin
                        ? new[] { p + n * (path.HalfLeft[i] * 0.55), p - n * (path.HalfRight[i] * 0.55) }
                        : new[] { p };
                    double lowest = double.MaxValue;
                    foreach (Vec2 c in columns) lowest = Math.Min(lowest, underside - Ground(c));
                    if (lowest < PierMinimumHeight) break;   // an embankment here: nothing to hold up
                    bool ok = true;
                    foreach (Vec2 c in columns)
                    {
                        ok &= !OnRoad(c, colHalf + PierClearOfRoad) && !OverLowerDeck(c, underside, path, colHalf, decks);
                    }
                    if (!ok) continue;
                    double ground = double.MaxValue;
                    foreach (Vec2 c in columns) ground = Math.Min(ground, Ground(c));
                    _out.Piers.Add(new ViaductPier
                    {
                        Path = path.Id,
                        P = p,
                        Along = t,
                        Underside = underside,
                        Ground = ground,
                        HalfSpan = Math.Max(path.HalfLeft[i], path.HalfRight[i]) - (main ? 0.6 : 0.3),
                        Twin = twin,
                        ColumnLeft = twin ? path.HalfLeft[i] * 0.55 : 0.0,
                        ColumnRight = twin ? path.HalfRight[i] * 0.55 : 0.0,
                    });
                    break;
                }
            }
        }

        /// <summary>Whether a column there would stand on a road or its pavement.</summary>
        private bool OnRoad(Vec2 c, double margin)
        {
            _net.EdgesNear(c, c, _cfg.BoulevardHalfWidth + _cfg.SidewalkWidth + margin, _edgeScratch);
            foreach (RoadEdge e in _edgeScratch)
            {
                if (PlanMath.DistanceToSegment(c, e.A.P, e.B.P, out _) < CityPlan.HalfWidth(_cfg, e.Class) + _cfg.SidewalkWidth + margin) return true;
            }
            return false;
        }

        /// <summary>Whether a column there would come down through another deck passing under this one.</summary>
        private bool OverLowerDeck(Vec2 c, double underside, ViaductPath self, double colHalf, List<(int path, int seg, double top)> decks)
        {
            DecksAt(c, colHalf + 0.5, decks);
            foreach ((int path, int seg, double top) in decks)
            {
                if (path == self.Id) continue;
                if (top < underside - 0.3) return true;
            }
            return false;
        }
    }
}
