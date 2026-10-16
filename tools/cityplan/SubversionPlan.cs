using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// Renders the city plan the game uses (World/Plan) to a PNG and prints statistics, with no Unity involved.
/// Usage: dotnet run -c Release --project tools/cityplan -- [seed] [halfMetres] [pixels] [out.png] [centreX centreZ viewHalf]
/// With a centre and view half-size it renders just that window (a close look).
///
/// Colours: expressway main decks pale (brighter the higher), connectors violet, ramps orange, piers white dots, kerb cuts red, the land an
/// interchange keeps clear dark; cyan rings at the exits, magenta at the system interchanges. Plazas light green, malls magenta, garages blue,
/// open car parks grey with a P.
/// </summary>
public static class SubversionPlan
{
    public static int Run(string[] args)
    {
        int seed = args.Length > 0 ? int.Parse(args[0]) : 1234;
        double half = args.Length > 1 ? double.Parse(args[1]) : 2500.0;
        int px = args.Length > 2 ? int.Parse(args[2]) : 1800;
        string outPath = args.Length > 3 ? args[3] : "subplan.png";
        double viewCx = args.Length > 5 ? double.Parse(args[4]) : 0.0;
        double viewCz = args.Length > 5 ? double.Parse(args[5]) : 0.0;
        double view = args.Length > 6 ? double.Parse(args[6]) : half;

        var cfg = PlanWorld.DefaultConfig();
        if (half > 0.0) cfg.Half = half;   // 0: the game's own region, exactly
        else half = cfg.Half;
        if (view <= 0.0) view = half;   // asked for the game's own region: the view is that region
        var sw = Stopwatch.StartNew();
        PlanWorld world = PlanWorld.Create(seed, cfg);
        sw.Stop();
        CityPlanData plan = world.Data;

        double heightSum = 0, tallest = 0; int towers = 0;
        foreach (var b in world.Buildings) { heightSum += b.Params.Height; if (b.Params.Height > tallest) tallest = b.Params.Height; if (b.Params.Height > 80) towers++; }
        var zones = new Dictionary<ZoneKind, int>();
        foreach (var b in world.Buildings) { zones.TryGetValue(b.Zone, out int c); zones[b.Zone] = c + 1; }
        Console.WriteLine($"seed {seed}, region {2 * half:0}m: {plan.Centres.Count} centres, {plan.Nodes.Count} nodes, {plan.Edges.Count} edges, " +
            $"{plan.Blocks.Count} blocks, {plan.Lots.Count} lots -> {world.Buildings.Length} buildings; generated in {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  mean height {(world.Buildings.Length > 0 ? heightSum / world.Buildings.Length : 0):0.#} m, tallest {tallest:0} m, {towers} over 80 m");
        foreach (var kv in zones) Console.WriteLine($"  {kv.Key}: {kv.Value}");
        Stats(world);
        Check(world);

        double scale = px / (2 * view);
        Func<Vec2, PointF> P = v => new PointF((float)((v.X - viewCx + view) * scale), (float)((viewCz + view - v.Y) * scale));

        using (var bmp = new Bitmap(px, px))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var data = bmp.LockBits(new Rectangle(0, 0, px, px), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var pixels = new int[px * px];
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    double wx = viewCx - view + x / scale, wy = viewCz + view - y / scale;
                    double h = plan.Fields.Height(wx, wy);
                    int c;
                    if (plan.Fields.IsWater(wx, wy)) c = Color.FromArgb(70, 70, 80).ToArgb();    // a dry concrete trench
                    else { int gch = (int)Math.Max(40, Math.Min(150, 80 + h * 1.0)); c = Color.FromArgb(40, gch, 60).ToArgb(); }
                    pixels[y * px + x] = c;
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            bmp.UnlockBits(data);

            // Pavement rings (between the kerb and the lots), plazas filled.
            using (var brush = new SolidBrush(Color.FromArgb(200, 175, 175, 170)))
            using (var plaza = new SolidBrush(Color.FromArgb(230, 150, 200, 140)))
            using (var mall = new SolidBrush(Color.FromArgb(120, 190, 120, 190)))
            {
                foreach (var b in plan.Blocks)
                {
                    if (b.Inner == null || b.Outer == null) continue;
                    int n = b.Poly.Count;
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        g.FillPolygon(brush, new[] { P(b.Inner[i]), P(b.Inner[j]), P(b.Outer[j]), P(b.Outer[i]) });
                    }
                    if (b.Plaza || b.Mall)
                    {
                        var pts = new PointF[b.Inner.Count];
                        for (int i = 0; i < pts.Length; i++) pts[i] = P(b.Inner[i]);
                        g.FillPolygon(b.Plaza ? plaza : mall, pts);
                    }
                }
            }
            // Road ribbons.
            foreach (var eg in world.Geometry.Edges)
            {
                if (eg == null) continue;
                Vec2 a = eg.A + eg.Dir * eg.TrimA, b = eg.B - eg.Dir * eg.TrimB;
                Vec2 n = eg.Dir.Perp * eg.HalfWidth;
                Color col = eg.Edge.Bridge ? Color.FromArgb(255, 235, 150) : (eg.Edge.Class == RoadClass.Boulevard ? Color.FromArgb(90, 90, 100) : eg.Edge.Class == RoadClass.Arterial ? Color.FromArgb(60, 60, 68) : Color.FromArgb(52, 52, 60));
                using (var brush = new SolidBrush(col))
                    g.FillPolygon(brush, new[] { P(a + n), P(b + n), P(b - n), P(a - n) });
            }
            // Junction plates.
            using (var brush = new SolidBrush(Color.FromArgb(58, 58, 66)))
            {
                foreach (var j in world.Geometry.Junctions)
                {
                    if (j == null || j.Plate == null) continue;
                    var pts = new PointF[j.Plate.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = P(j.Plate[i]);
                    g.FillPolygon(brush, pts);
                }
            }
            // Lots (thin outline) and the buildings fitted in them.
            using (var pen = new Pen(Color.FromArgb(70, 200, 200, 200), 1f))
                foreach (var lot in plan.Lots)
                {
                    var pts = new PointF[lot.Lot.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = P(lot.Lot[i]);
                    g.DrawPolygon(pen, pts);
                }
            using (var font = new Font("Arial", Math.Max(6f, (float)(14.0 * scale)), FontStyle.Bold))
            foreach (var b in world.Buildings)
            {
                double hw = b.Params.Width * 0.5, hd = b.Params.Depth * 0.5;
                var corners = new Vec2[4];
                int[] sx = { -1, 1, 1, -1 }, sz = { -1, -1, 1, 1 };
                for (int i = 0; i < 4; i++)
                {
                    b.LocalToWorld(sx[i] * hw, sz[i] * hd, out double wx, out double wz);
                    corners[i] = new Vec2(wx, wz);
                }
                int shade = (int)Math.Min(255, 70 + b.Params.Height * 1.1);
                Color fill = Color.FromArgb(215, shade / 2, shade * 3 / 4, shade);
                if (b.IsMall) fill = Color.FromArgb(240, 220, 60, 220);
                else if (b.IsParkingGarage) fill = Color.FromArgb(240, 60, 80, 200);
                else if (b.IsOpenGround) fill = Color.FromArgb(240, 110, 110, 110);
                using (var brush = new SolidBrush(fill))
                using (var pen = new Pen(Color.FromArgb(240, 240, 240), 1f))
                {
                    var pts = new[] { P(corners[0]), P(corners[1]), P(corners[2]), P(corners[3]) };
                    g.FillPolygon(brush, pts);
                    g.DrawPolygon(pen, pts);
                }
                if (b.IsOpenGround || b.IsParkingGarage)
                {
                    var c = P(new Vec2(b.CentreX, b.CentreZ));
                    g.DrawString("P", font, Brushes.White, c.X - font.Size * 0.6f, c.Y - font.Size * 0.7f);
                }
                // The door: a small bar on the front (+Z) wall.
                b.LocalToWorld(-1.0, hd, out double dx0, out double dz0);
                b.LocalToWorld(1.0, hd, out double dx1, out double dz1);
                g.DrawLine(Pens.Gold, P(new Vec2(dx0, dz0)), P(new Vec2(dx1, dz1)));
            }
            DrawExpressways(g, world, P, scale);
            foreach (var c in plan.Centres) { var p = P(c); g.DrawEllipse(Pens.Red, p.X - 6, p.Y - 6, 12, 12); }
            bmp.Save(outPath, ImageFormat.Png);
        }
        Console.WriteLine("wrote " + outPath);
        return 0;
    }

    /// <summary>The set pieces by the numbers: how many, how far apart, what share of the lots.</summary>
    private static void Stats(PlanWorld world)
    {
        CityPlanData plan = world.Data;
        ExpresswayStats(world);

        int garages = 0, lots = 0, malls = 0, groundSites = 0;
        var mallCentres = new List<Vec2>();
        foreach (var b in world.Buildings)
        {
            if (b.OnRoof) continue;
            groundSites++;
            if (b.IsParkingGarage) garages++;
            else if (b.IsOpenGround) lots++;
        }
        // A mall is a complex of corridors and stores on one block: count the blocks.
        foreach (int bi in plan.MallBlocks)
        {
            malls++;
            mallCentres.Add(PlanMath.Centroid(plan.Blocks[bi].Outer));
        }
        double nearestMalls = double.MaxValue;
        for (int i = 0; i < mallCentres.Count; i++)
            for (int j = i + 1; j < mallCentres.Count; j++)
                nearestMalls = Math.Min(nearestMalls, (mallCentres[i] - mallCentres[j]).Length);
        Console.WriteLine($"  parking: {garages} garages, {lots} open lots = {100.0 * (garages + lots) / Math.Max(1, groundSites):F1}% of {groundSites} street-level sites");
        Console.WriteLine($"  malls: {malls}" + (mallCentres.Count > 1 ? $", nearest two {nearestMalls / 1000.0:F1} km apart" : ""));
        // Each mall complex: its pieces (corridors, anchors, pads, a garage) inside its block.
        foreach (int bi in plan.MallBlocks)
        {
            PlanBlock block = plan.Blocks[bi];
            int corridors = 0, anchors = 0, pads = 0, mallGarages = 0;
            Vec2 at = PlanMath.Centroid(block.Outer);
            foreach (var b in world.Buildings)
            {
                if (b.OnRoof || !PlanMath.PointInPolygon(new Vec2(b.CentreX, b.CentreZ), block.Outer)) continue;
                if (b.IsMall) corridors++;
                else if (b.IsMallAnchor) anchors++;
                else if (b.IsParkingGarage) mallGarages++;
                else pads++;
            }
            Console.WriteLine($"    mall at ({at.X:F0}, {at.Y:F0}), {block.Area / 10000.0:F1} ha, {block.MallParking}: {corridors} corridors, {anchors} anchors, {pads} pads, {mallGarages} garage");
            if (corridors == 0)
            {
                // Why a chosen mall block has no mall: its lot, the rectangle fitted in it, the layout, the checks Resolve makes.
                PlanLot lot = null;
                foreach (PlanLot l in plan.Lots) if (l.BlockIndex == bi && l.Use == LotUse.Mall) lot = l;
                if (lot == null) { Console.WriteLine("      no mall lot (block.Mall " + block.Mall + ")"); continue; }
                bool fit = PlanBuildings.FitRectangle(lot.Footprint, lot.FrontEdge, out Vec2 c, out Vec2 ex, out double w, out double d, lot.Scale, PlanBuildings.FitMargin);
                Console.WriteLine($"      lot {lot.Id}: fit {fit} {w:F0} x {d:F0}, scale {lot.Scale:F2}");
                if (!fit) continue;
                var pieces = Amenities.MallLayout.Compose(w, d, lot.Scale, 13.0, 12345U, block.MallParking);
                var vols = new List<MassVolume>();
                foreach (var p in pieces) vols.Add(p.Volume);
                int wet = 0;
                var xs = new double[4];
                var zs = new double[4];
                foreach (var v in vols)
                {
                    v.Corners(xs, zs);
                    for (int k = 0; k < 4; k++)
                    {
                        double wx = c.X + xs[k] * ex.X - zs[k] * ex.Y, wz = c.Y + xs[k] * ex.Y + zs[k] * ex.X;
                        if (world.Fields.IsWater(new Vec2(wx, wz))) wet++;
                    }
                }
                Console.WriteLine($"      {pieces.Count} pieces, inside {PlanBuildings.InsideLot(vols, w, d)}, wet corners {wet}");
            }
        }
        var plazas = new List<Vec2>();
        foreach (int i in plan.PlazaBlocks) plazas.Add(PlanMath.Centroid(plan.Blocks[i].Outer));
        double meanNearest = 0.0;
        foreach (var p in plazas)
        {
            double nearest = double.MaxValue;
            foreach (var q in plazas) if ((p - q).Length > 0.01) nearest = Math.Min(nearest, (p - q).Length);
            if (nearest < double.MaxValue) meanNearest += nearest;
        }
        Console.WriteLine($"  plazas: {plazas.Count}" + (plazas.Count > 1 ? $", mean distance to the nearest other {meanNearest / plazas.Count:F0} m" : ""));
    }

    private static void DrawExpressways(Graphics g, PlanWorld world, Func<Vec2, PointF> P, double scale)
    {
        ExpresswayNetwork xw = world.Data.Expressways;
        using (var infield = new SolidBrush(Color.FromArgb(70, 30, 30, 40)))
            foreach (var poly in xw.Infields)
            {
                var pts = new PointF[poly.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = P(poly[i]);
                g.FillPolygon(infield, pts);
            }
        // Lowest first, so a deck passing over another is drawn on top of it.
        var order = new List<(double top, ViaductPath path, int seg)>();
        foreach (ViaductPath path in xw.Paths)
            for (int i = 0; i < path.Segments; i++) order.Add(((path.Top[i] + path.Top[i + 1]) * 0.5, path, i));
        order.Sort((a, b) => a.top.CompareTo(b.top));
        foreach (var (top, path, i) in order)
        {
            double ground = world.GroundHeight(path.P[i].X, path.P[i].Y);
            double up = Math.Max(0.0, Math.Min(1.0, (top - ground) / 25.0));
            Color c = path.Kind == ViaductKind.Mainline ? Color.FromArgb(255, (int)(150 + 100 * up), (int)(150 + 100 * up), (int)(160 + 90 * up))
                : path.Kind == ViaductKind.Connector ? Color.FromArgb(255, (int)(120 + 80 * up), 80, (int)(170 + 80 * up))
                : Color.FromArgb(255, 230, (int)(110 + 90 * up), 40);
            Vec2[] f = path.Footprint(i, 0.0);
            using (var brush = new SolidBrush(c))
                g.FillPolygon(brush, new[] { P(f[0]), P(f[1]), P(f[2]), P(f[3]) });
        }
        using (var pen = new Pen(Color.FromArgb(200, 60, 60, 70), 1f))
            foreach (ViaductPath path in xw.Paths)
                for (int i = 0; i < path.Segments; i++)
                    if (path.Kind == ViaductKind.Mainline && (i / 4) % 2 == 0) g.DrawLine(pen, P(path.P[i]), P(path.P[i + 1]));
        float dot = Math.Max(1.5f, (float)(1.2 * scale));
        foreach (var pier in xw.Piers)
        {
            var p = P(pier.P);
            g.FillEllipse(Brushes.White, p.X - dot, p.Y - dot, 2 * dot, 2 * dot);
        }
        using (var cut = new SolidBrush(Color.FromArgb(230, 230, 40, 40)))
            foreach (var k in xw.KerbCuts)
            {
                var pts = new PointF[k.Poly.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = P(k.Poly[i]);
                g.FillPolygon(cut, pts);
            }
        using (var exit = new Pen(Color.Cyan, 2f))
        using (var system = new Pen(Color.Magenta, 3f))
            foreach (var ic in xw.Interchanges)
            {
                var p = P(ic.At);
                float rad = Math.Max(6f, (float)((ic.System ? 120.0 : 50.0) * scale));
                g.DrawEllipse(ic.System ? system : exit, p.X - rad, p.Y - rad, 2 * rad, 2 * rad);
            }
        using (var bar = new SolidBrush(Color.Red))
            foreach (var b in xw.Barriers)
            {
                var p = P(b.P);
                g.FillRectangle(bar, p.X - 4, p.Y - 4, 8, 8);
            }
    }

    /// <summary>The expressways by the numbers: routes, interchanges, ramp shapes, grades, clearances; what the planner turned down and why.</summary>
    private static void ExpresswayStats(PlanWorld world)
    {
        ExpresswayNetwork xw = world.Data.Expressways;
        var cfg = world.Data.Config;
        double mainKm = 0.0, alongKm = 0.0, embankKm = 0.0, overRoadKm = 0.0, lowOverRoad = 0.0;
        double steepMain = 0.0;
        foreach (ExpresswayRoute route in xw.Routes)
        {
            ViaductPath m = xw.Paths[route.Path];
            Console.WriteLine($"  route {route.Id}: {(route.Ring ? "ring" : "through")}, {m.Length / 1000.0:F1} km, {m.Count} samples");
            for (int i = 0; i < m.Segments; i++)
            {
                double len = m.S[i + 1] - m.S[i];
                mainKm += len / 1000.0;
                double ground = world.GroundHeight(m.P[i].X, m.P[i].Y);
                double under = m.Top[i] - m.Slab - ground;
                if (under < 2.0) embankKm += len / 1000.0;
                steepMain = Math.Max(steepMain, Math.Abs(m.Top[i + 1] - m.Top[i]) / Math.Max(0.01, len));
                foreach (var eg in world.Geometry.Edges)
                {
                    if (eg == null) continue;
                    if (Math.Abs(eg.A.X - m.P[i].X) > 400 && Math.Abs(eg.B.X - m.P[i].X) > 400) continue;
                    double d = PlanMath.DistanceToSegment(m.P[i], eg.A, eg.B, out _);
                    if (d > eg.HalfWidth + cfg.SidewalkWidth + m.HalfLeft[i]) continue;
                    overRoadKm += len / 1000.0;
                    if (Math.Abs(Vec2.Dot(eg.Dir, m.SegmentDir(i))) > 0.9 && d < eg.HalfWidth + m.HalfLeft[i] * 0.5) alongKm += len / 1000.0;
                    if (under < ExpresswayPlanner.StreetClearance - 0.3) lowOverRoad += len;
                    break;
                }
            }
        }
        Console.WriteLine($"  expressways: {mainKm:F1} km of main line ({100 * embankKm / Math.Max(0.01, mainKm):F0}% on embankment), {100 * overRoadKm / Math.Max(0.01, mainKm):F0}% over a road, {100 * alongKm / Math.Max(0.01, mainKm):F1}% ALONG one; steepest {100 * steepMain:F1}%; low over a road {lowOverRoad:F0} m");
        int systems = 0, exits = 0;
        foreach (var ic in xw.Interchanges) { if (ic.System) systems++; else exits++; }
        var shapes = new Dictionary<string, int>();
        double steepRamp = 0.0;
        int curved = 0, straight = 0, viaductRamps = 0, embankRamps = 0;
        foreach (ViaductPath p in xw.Paths)
        {
            if (p.Kind == ViaductKind.Mainline) continue;
            shapes.TryGetValue(p.Shape, out int c); shapes[p.Shape] = c + 1;
            double turn = 0.0;
            for (int i = 1; i < p.Segments; i++) turn += Math.Abs(DMath.Turn(p.SegmentDir(i - 1), p.SegmentDir(i)));
            if (turn > 0.6) curved++; else straight++;
            bool high = false, low = false;
            for (int i = 0; i < p.Count; i++)
            {
                double under = p.Top[i] - p.Slab - world.GroundHeight(p.P[i].X, p.P[i].Y);
                if (under > 4.0) high = true; else if (under < 1.5 && under > -0.5) low = true;
            }
            if (high) viaductRamps++;
            if (low) embankRamps++;
            for (int i = 0, j = 0; i < p.Count; i++)
            {
                while (j < p.Count - 1 && p.S[j] - p.S[i] < 12.0) j++;
                if (p.S[j] - p.S[i] >= 12.0) steepRamp = Math.Max(steepRamp, Math.Abs(p.Top[j] - p.Top[i]) / (p.S[j] - p.S[i]));
            }
        }
        var sb = new System.Text.StringBuilder();
        foreach (var kv in shapes) sb.Append($" {kv.Key} {kv.Value};");
        Console.WriteLine($"  interchanges: {systems} system, {exits} exits; decks:{sb}  curved {curved}, straight {straight}; with a viaduct {viaductRamps}, with an embankment {embankRamps}; steepest ramp {100 * steepRamp:F1}%; {xw.Piers.Count} piers, {xw.Signs.Count} signs, {xw.KerbCuts.Count} kerb cuts");
        if (ExpresswayPlanner.Tally != null)
        {
            var t = new System.Text.StringBuilder();
            foreach (var kv in ExpresswayPlanner.Tally) t.Append($" {kv.Key}: {kv.Value};");
            Console.WriteLine("  planner:" + t);
        }
    }

    private static void Check(PlanWorld world)
    {
        int inside = 0, corners = 0;
        var buildings = world.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (b.IsOpenGround) continue;
            double hw = b.Params.Width * 0.5, hd = b.Params.Depth * 0.5;
            int[] sx = { -1, 1, 1, -1 }, sz = { -1, -1, 1, 1 };
            for (int k = 0; k < 4; k++)
            {
                b.LocalToWorld(sx[k] * hw * 0.98, sz[k] * hd * 0.98, out double wx, out double wz);
                corners++;
                int other = world.BuildingAt(wx, wz);
                if (other >= 0 && other != b.Id && !buildings[other].OnRoof && !b.OnRoof) inside++;
            }
        }
        Console.WriteLine($"  check: {inside} of {corners} building corners lie inside a different building");
    }
}
