public static class Program
{
    public static int Main(string[] args)
    {
        // dotnet run -c Release --project tools/cityplan -- [seed] [halfMetres] [pixels] [out.png] [centreX centreZ viewHalf]
        if (args.Length > 0 && args[0] == "site")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 61470385);
            var s = w.Buildings[int.Parse(args[1])];
            System.Console.WriteLine($"#{s.Id} {s.Params.Width:F1}x{s.Params.Depth:F1}x{s.Params.Height:F1} mass={s.Params.Mass} sides={s.Sides} shell={s.Shell} wing={s.Wing} roof={s.OnRoof} sealed={s.RoofSealed} contacts={(s.Contacts?.Length ?? 0)} obstacle={s.HasRoofObstacle} basement={s.Params.HasBasement}");
            System.Console.WriteLine($"  centre ({s.CentreX:F1},{s.CentreZ:F1}) axis ({s.AxisX:F3},{s.AxisZ:F3}) base {s.BaseHeight:F2}");
            if (s.Contacts != null) foreach (var c in s.Contacts) System.Console.WriteLine($"  contact {c.Wall} {c.U0:F2}..{c.U1:F2} top {c.Top:F1}");
            foreach (var o in w.Buildings)
            {
                double dx = o.CentreX - s.CentreX, dz = o.CentreZ - s.CentreZ;
                if (o.Id == s.Id || dx * dx + dz * dz > 80.0 * 80.0) continue;
                System.Console.WriteLine($"  near #{o.Id} {o.Params.Width:F1}x{o.Params.Depth:F1} centre ({o.CentreX:F1},{o.CentreZ:F1}) axis ({o.AxisX:F3},{o.AxisZ:F3}) roof={o.OnRoof} wing={o.Wing} shell={o.Shell} sides={o.Sides}");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "nearspawn") { PlanDiagnose.NearSpawn(args.Length > 1 ? int.Parse(args[1]) : 61470385, args.Length > 2 ? int.Parse(args[2]) : 12); return 0; }
        if (args.Length > 0 && args[0] == "overlaps") { PlanDiagnose.Overlaps(args.Length > 1 ? int.Parse(args[1]) : 61470385); return 0; }
        if (args.Length > 0 && args[0] == "profile-line")
        {
            // The ground and what stands on it along a line: profile-line x0 z0 x1 z1 [seed]
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 5 ? int.Parse(args[5]) : 61470385);
            double x0 = double.Parse(args[1]), z0 = double.Parse(args[2]), x1 = double.Parse(args[3]), z1 = double.Parse(args[4]);
            for (int i = 0; i <= 20; i++)
            {
                double t = i / 20.0, x = x0 + (x1 - x0) * t, z = z0 + (z1 - z0) * t;
                int b = w.BuildingAt(x, z, 0.0);
                System.Console.WriteLine($"({x:F1},{z:F1}) ground {w.GroundHeight(x, z):F2}" + (b >= 0 ? $" building #{b} floor {w.Buildings[b].BaseHeight:F2}" : ""));
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "partywalls")
        {
            // How much of the city stands shoulder to shoulder: ground-level boxes with a blank stretch of wall, and how much wall that is.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 61470385);
            int ground = 0, touching = 0, contacts = 0;
            double metres = 0.0;
            foreach (var s in w.Buildings)
            {
                if (s.OnRoof || s.Sides >= 3) continue;
                ground++;
                if (s.Contacts == null || s.Contacts.Length == 0) continue;
                touching++;
                contacts += s.Contacts.Length;
                foreach (var c in s.Contacts) metres += c.U1 - c.U0;
            }
            System.Console.WriteLine($"{ground} street-level boxes, {touching} touch another block ({100.0 * touching / ground:F0}%), {contacts} contacts, {metres / 1000.0:F1} km of blank wall");
            return 0;
        }
        if (args.Length > 0 && args[0] == "fitcheck")
        {
            // Every lot's fitted rectangle must lie inside the lot's footprint.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 61470385);
            int bad = 0, total = 0;
            foreach (var lot in w.Data.Lots)
            {
                if (!CityX.World.Plan.PlanBuildings.FitRectangle(lot.Footprint, lot.FrontEdge, out var c, out var ex, out double width, out double depth, lot.Scale,
                    lot.StreetWall ? CityX.World.Plan.PlanBuildings.StreetWallFitMargin : CityX.World.Plan.PlanBuildings.FitMargin)) continue;
                total++;
                var ez = new CityX.World.Plan.Vec2(-ex.Y, ex.X);
                double worst = 0.0;
                for (int k = 0; k < 4; k++)
                {
                    var p = c + ex * (((k & 1) == 0 ? -0.5 : 0.5) * width) + ez * (((k & 2) == 0 ? -0.5 : 0.5) * depth);
                    worst = System.Math.Max(worst, Outside(lot.Footprint, p));
                }
                if (worst > 0.05)
                {
                    bad++;
                    if (worst > 0.2) System.Console.WriteLine($"lot {lot.Id} pts {lot.Footprint.Length} front {lot.FrontEdge}: rect {width:F1}x{depth:F1} pokes out {worst:F2} m");
                }
            }
            System.Console.WriteLine($"{bad} of {total} rectangles poke out of their lot");
            return 0;
        }
        if (args.Length > 0 && args[0] == "sizes")
        {
            // How big the city's buildings are: footprint side (square root of the area), height, and the share above fixed thresholds
            // (pass the thresholds from an earlier run as args 2-3 to compare a change against the same lines).
            var w = CityX.World.Plan.PlanWorld.Create(args.Length > 1 ? int.Parse(args[1]) : 61470385, CityX.World.Plan.PlanWorld.DefaultConfig());
            var sides = new System.Collections.Generic.List<double>();
            var heights = new System.Collections.Generic.List<double>();
            foreach (var b in w.Buildings)
            {
                if (b.OnRoof || b.Wing || b.Params.IsSpecial) continue;   // whole buildings at street level, ordinary ones
                double top = b.Top - b.BaseHeight;
                for (int k = b.Id + 1; k < w.Buildings.Length; k++)
                {
                    var v = w.Buildings[k];
                    if (!v.OnRoof && !v.Wing) break;
                    top = System.Math.Max(top, v.Top - b.BaseHeight);
                }
                sides.Add(System.Math.Sqrt(b.Params.Width * b.Params.Depth));
                heights.Add(top);
            }
            double sideLine = args.Length > 2 ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
            double heightLine = args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
            void Report(string name, System.Collections.Generic.List<double> xs, double line)
            {
                xs.Sort();
                double mean = 0.0;
                foreach (double x in xs) mean += x;
                mean /= xs.Count;
                double P(double q) => xs[(int)System.Math.Min(xs.Count - 1, q * xs.Count)];
                if (double.IsNaN(line)) line = P(0.75);
                int above = 0;
                foreach (double x in xs) if (x >= line) above++;
                System.Console.WriteLine($"{name}: {xs.Count} buildings, mean {mean:F1}, median {P(0.5):F1}, p75 {P(0.75):F1}, p90 {P(0.9):F1}, p99 {P(0.99):F1}, max {xs[xs.Count - 1]:F1}; at or above {line:F1}: {100.0 * above / xs.Count:F1}%");
            }
            Report("footprint side (m)", sides, sideLine);
            Report("height (m)", heights, heightLine);
            return 0;
        }
        if (args.Length > 0 && args[0] == "nearbridge")
        {
            // The bridge nearest the spawn (what RainRealCityPlayTests stands beside), and what is round it.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 61470385);
            w.TryFindSpawn(out double sx, out double sz);
            CityX.World.Plan.EdgeGeometry best = null;
            double bestD = double.MaxValue;
            foreach (var g in w.Geometry.Edges)
            {
                if (g == null || !g.Edge.Bridge || g.Elevated) continue;
                double dx = (g.A.X + g.B.X) * 0.5 - sx, dz = (g.A.Y + g.B.Y) * 0.5 - sz;
                if (dx * dx + dz * dz < bestD) { bestD = dx * dx + dz * dz; best = g; }
            }
            System.Console.WriteLine($"spawn ({sx:F0}, {sz:F0}); bridge edge {best.Edge.Id} {best.Edge.Class} from ({best.A.X:F0}, {best.A.Y:F0}) to ({best.B.X:F0}, {best.B.Y:F0}), length {best.Length:F0}");
            var mid = (best.A + best.B) * 0.5;
            var along = best.Dir.Perp;
            foreach (double d in new[] { 0.0, 10.0, 22.0, 34.0, -22.0 })
            {
                var p = mid + along * d;
                int building = w.BuildingAtHeight(p.X, w.GroundHeight(p.X, p.Y) + 2.0, p.Y);
                System.Console.WriteLine($"  {d,5:F0} m along the trench: ground {w.GroundHeight(p.X, p.Y):F1}, water {w.Fields.IsWater(p)}, building {building}");
            }
            foreach (var g in w.Geometry.Edges)
            {
                if (g == null || g == best) continue;
                double d = CityX.World.Plan.PlanMath.DistanceToSegment(mid, g.A, g.B, out _);
                if (d < 60.0) System.Console.WriteLine($"  road {g.Edge.Id} {g.Edge.Class}{(g.Edge.Bridge ? " bridge" : "")}{(g.Elevated ? " elevated" : "")} {d:F0} m away");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "mallshapes")
        {
            // How the mall layout comes out over many seeds, for a few lot sizes: pieces of each kind, branches, garages.
            foreach ((double w, double d, double sc) in new[] { (180.0, 130.0, 1.0), (260.0, 200.0, 1.0), (360.0, 260.0, 4.0), (480.0, 340.0, 4.0) })
            {
                var counts = new System.Collections.Generic.Dictionary<string, int>();
                int garages = 0;
                for (uint h = 1; h <= 400; h++)
                {
                    var pieces = CityX.World.Plan.Amenities.MallLayout.Compose(w, d, sc, 13.0, CityX.World.Plan.PlanNoise.Mix(h, 77U),
                        h % 5U == 0U ? CityX.World.Plan.MallParking.GarageAndLot : CityX.World.Plan.MallParking.OpenLot);
                    int corridors = 0, anchors = 0, pads = 0;
                    foreach (var p in pieces)
                    {
                        if (p.Part == CityX.World.Plan.Amenities.MallPart.Concourse) corridors++;
                        else if (p.Part == CityX.World.Plan.Amenities.MallPart.Anchor) anchors++;
                        else if (p.Part == CityX.World.Plan.Amenities.MallPart.Pad) pads++;
                        else garages++;
                    }
                    string key = $"{corridors}c{anchors}a";
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                var keys = new System.Collections.Generic.List<string>(counts.Keys);
                keys.Sort();
                System.Console.WriteLine($"{w:F0} x {d:F0} (scale {sc}): " + string.Join(", ", keys.ConvertAll(k => $"{k} {counts[k]}")) + $"; garages {garages} of 80 asked");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "profile") { PlanProfile.Run(args.Length > 1 ? int.Parse(args[1]) : 61470385); return 0; }
        return SubversionPlan.Run(args);
    }

    /// <summary>How far a point is outside a counter-clockwise convex-or-not polygon (0 inside): the largest distance past any edge it is outside of, when outside.</summary>
    private static double Outside(CityX.World.Plan.Vec2[] poly, CityX.World.Plan.Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        }
        if (inside) return 0.0;
        double best = double.MaxValue;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Length];
            var d = b - a;
            double t = System.Math.Clamp(CityX.World.Plan.Vec2.Dot(p - a, d) / CityX.World.Plan.Vec2.Dot(d, d), 0.0, 1.0);
            best = System.Math.Min(best, (a + d * t - p).Length);
        }
        return best;
    }
}
