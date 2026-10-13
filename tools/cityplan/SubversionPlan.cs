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
/// Colours: expressways pale grey with a yellow centre (the deck), their interchange ramps orange and the land they cut off dark grey;
/// cyan rings at the exits, red squares where an expressway ends. Plazas light green, malls magenta, garages blue, open car parks grey with a P.
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
        cfg.Half = half;
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
                Color col = eg.Edge.Bridge ? Color.FromArgb(255, 235, 150) : (eg.Edge.Class == RoadClass.Highway ? Color.FromArgb(120, 120, 128) : eg.Edge.Class == RoadClass.Arterial ? Color.FromArgb(60, 60, 68) : Color.FromArgb(52, 52, 60));
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
            // The expressway decks: a yellow line down the middle of each (dashed where it descends to the ground), and the plates where decks meet.
            using (var deckPen = new Pen(Color.FromArgb(230, 200, 60), Math.Max(1f, (float)(1.5 * scale))))
            using (var endPen = new Pen(Color.FromArgb(230, 90, 60), Math.Max(1f, (float)(1.5 * scale))) { DashStyle = DashStyle.Dash })
            using (var deckBrush = new SolidBrush(Color.FromArgb(135, 135, 145)))
            {
                foreach (var j in world.Geometry.Junctions)
                {
                    if (j == null || j.DeckPlate == null) continue;
                    var pts = new PointF[j.DeckPlate.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = P(j.DeckPlate[i]);
                    g.FillPolygon(deckBrush, pts);
                }
                foreach (var eg in world.Geometry.Edges)
                {
                    if (eg == null || !eg.Elevated) continue;
                    double groundA = plan.Fields.Height(eg.A.X, eg.A.Y), groundB = plan.Fields.Height(eg.B.X, eg.B.Y);
                    bool low = eg.DeckA - groundA < cfg.DeckClearance || eg.DeckB - groundB < cfg.DeckClearance;
                    g.DrawLine(low ? endPen : deckPen, P(eg.A), P(eg.B));
                }
            }
            // Interchanges: the land each ramp cuts off, then the ramp itself.
            using (var sliver = new SolidBrush(Color.FromArgb(90, 88, 92)))
            using (var ramp = new SolidBrush(Color.FromArgb(235, 150, 60)))
            using (var exit = new Pen(Color.Cyan, 2f))
            {
                foreach (var r in world.Geometry.Ramps)
                {
                    var pts = new PointF[r.Plan.Sliver.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = P(r.Plan.Sliver[i]);
                    g.FillPolygon(sliver, pts);
                }
                foreach (var r in world.Geometry.Ramps)
                {
                    Vec2 pa = r.PerpStart * r.HalfWidth, pm = r.PerpMid * r.HalfWidth, pf = r.PerpFoot * r.HalfWidth;
                    g.FillPolygon(ramp, new[] { P(r.Start + pa), P(r.Mid + pm), P(r.Mid - pm), P(r.Start - pa) });
                    g.FillPolygon(ramp, new[] { P(r.Mid + pm), P(r.Foot + pf), P(r.Foot - pf), P(r.Mid - pm) });
                }
                foreach (int id in plan.ExitNodes)
                {
                    var p = P(plan.Nodes[id].P);
                    float rad = Math.Max(5f, (float)(40.0 * scale));
                    g.DrawEllipse(exit, p.X - rad, p.Y - rad, 2 * rad, 2 * rad);
                }
            }
            // Where expressways end.
            using (var endBrush = new SolidBrush(Color.FromArgb(230, 60, 60)))
            {
                foreach (var node in plan.Nodes)
                {
                    int highways = 0;
                    foreach (var e in node.Edges) if (e.Alive && e.Class == RoadClass.Highway) highways++;
                    if (highways != 1) continue;
                    var p = P(node.P);
                    g.FillRectangle(endBrush, p.X - 5, p.Y - 5, 10, 10);
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
        double highwayKm = 0.0;
        int ends = 0, deckJunctions = 0;
        foreach (var eg in world.Geometry.Edges) if (eg != null && eg.Elevated) highwayKm += eg.Length / 1000.0;
        foreach (var node in plan.Nodes)
        {
            int highways = 0;
            foreach (var e in node.Edges) if (e.Alive && e.Class == RoadClass.Highway) highways++;
            if (highways == 1) ends++;
            if (highways >= 3) deckJunctions++;
        }
        Console.WriteLine($"  expressways: {highwayKm:F1} km, {plan.ExitNodes.Count} interchanges ({plan.Ramps.Count} ramps, one per {(plan.ExitNodes.Count > 0 ? highwayKm / plan.ExitNodes.Count : 0):F2} km), {ends} ends, {deckJunctions} junctions of expressways");
        {
            Vec2 c0 = new Vec2(0.0, 0.0);
            double radius = plan.Config.OrbitalRadius;
            int ringEdges = 0;
            foreach (var eg in world.Geometry.Edges)
            {
                if (eg == null || !eg.Elevated) continue;
                double da = (eg.A - c0).Length, db = (eg.B - c0).Length;
                if (Math.Abs(da - radius) < 0.25 * radius && Math.Abs(db - radius) < 0.25 * radius) ringEdges++;
            }
            Console.WriteLine($"  orbital: radius {radius:F0} m round ({c0.X:F0}, {c0.Y:F0}): {ringEdges} expressway edges near that circle");
        }
        double worstGrade = 0.0, leastDrop = double.MaxValue;
        foreach (var r in world.Geometry.Ramps)
        {
            double grade = (r.TopStart - r.TopFoot) / Math.Max(1.0, r.Length);
            worstGrade = Math.Max(worstGrade, grade);
            leastDrop = Math.Min(leastDrop, r.TopStart - r.TopFoot);
            if (r.TopStart - r.TopFoot < Interchanges.MinDrop)
            {
                var eg = world.Geometry.Edges[r.Plan.HighwayEdge];
                double rise = world.Data.Config.DeckClearance + world.Data.Config.DeckThickness;
                Console.WriteLine($"  LOW RAMP at exit {r.Plan.ExitNode} S=({r.Plan.S.X:F0},{r.Plan.S.Y:F0}): top {r.TopStart:F2} foot {r.TopFoot:F2}; edge {eg.Edge.Id} deck A {eg.DeckA:F2} B {eg.DeckB:F2}, maxGround+rise {eg.MaxGround + rise:F2}, ground at S {world.GroundHeight(r.Plan.S.X, r.Plan.S.Y):F2}, SAlong {r.Plan.SAlong:F0} of {eg.Length:F0}");
            }
        }
        Console.WriteLine($"  ramps: steepest grade {100.0 * worstGrade:F1}%, least drop {leastDrop:F1} m");
        int[] rej = Interchanges.Rejections;
        if (rej != null)
        {
            Console.WriteLine($"  exit candidates {rej[0]}: too close {rej[1]}; ramps lost: no room along {rej[2]}, no cross street {rej[3]}, street short {rej[4]}, crossed {rej[5]}, foot on junction {rej[6]}, wet/outside {rej[7]}, near an end {rej[8]}");
        }
        int shown = 0;
        foreach (int id in plan.ExitNodes)
        {
            if (shown++ >= 4) break;
            Console.WriteLine($"  exit at ({plan.Nodes[id].P.X:F0}, {plan.Nodes[id].P.Y:F0})");
        }

        int garages = 0, lots = 0, malls = 0, groundSites = 0;
        var mallCentres = new List<Vec2>();
        foreach (var b in world.Buildings)
        {
            if (b.OnRoof) continue;
            groundSites++;
            if (b.IsParkingGarage) garages++;
            else if (b.IsOpenGround) lots++;
            else if (b.IsMall) { malls++; mallCentres.Add(new Vec2(b.CentreX, b.CentreZ)); }
        }
        double nearestMalls = double.MaxValue;
        for (int i = 0; i < mallCentres.Count; i++)
            for (int j = i + 1; j < mallCentres.Count; j++)
                nearestMalls = Math.Min(nearestMalls, (mallCentres[i] - mallCentres[j]).Length);
        Console.WriteLine($"  parking: {garages} garages, {lots} open lots = {100.0 * (garages + lots) / Math.Max(1, groundSites):F1}% of {groundSites} street-level sites");
        Console.WriteLine($"  malls: {malls}" + (mallCentres.Count > 1 ? $", nearest two {nearestMalls / 1000.0:F1} km apart" : ""));
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
