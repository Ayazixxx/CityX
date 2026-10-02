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

            // Pavement rings (between the kerb and the lots).
            using (var brush = new SolidBrush(Color.FromArgb(200, 175, 175, 170)))
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
                }
            }
            // Road ribbons.
            foreach (var eg in world.Geometry.Edges)
            {
                if (eg == null) continue;
                Vec2 a = eg.A + eg.Dir * eg.TrimA, b = eg.B - eg.Dir * eg.TrimB;
                Vec2 n = eg.Dir.Perp * eg.HalfWidth;
                Color col = eg.Edge.Bridge ? Color.FromArgb(255, 235, 150) : (eg.Edge.Class == RoadClass.Highway ? Color.FromArgb(70, 70, 78) : eg.Edge.Class == RoadClass.Arterial ? Color.FromArgb(60, 60, 68) : Color.FromArgb(52, 52, 60));
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
                using (var brush = new SolidBrush(Color.FromArgb(215, shade / 2, shade * 3 / 4, shade)))
                using (var pen = new Pen(Color.FromArgb(240, 240, 240), 1f))
                {
                    var pts = new[] { P(corners[0]), P(corners[1]), P(corners[2]), P(corners[3]) };
                    g.FillPolygon(brush, pts);
                    g.DrawPolygon(pen, pts);
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

    /// <summary>Sanity checks printed with the render: how many fitted buildings overlap another.</summary>
    static void Check(PlanWorld world)
    {
        int overlap = 0;
        for (int i = 0; i < world.Buildings.Length; i++)
        {
            var b = world.Buildings[i];
            double hw = b.Params.Width * 0.5, hd = b.Params.Depth * 0.5;
            int[] sx = { -1, 1, 1, -1 }, sz = { -1, -1, 1, 1 };
            for (int c = 0; c < 4; c++)
            {
                b.LocalToWorld(sx[c] * hw * 0.98, sz[c] * hd * 0.98, out double wx, out double wz);
                int found = world.BuildingAt(wx, wz, 0.0);
                if (found != b.Id) overlap++;
            }
        }
        Console.WriteLine($"  check: {overlap} of {world.Buildings.Length * 4} building corners lie inside a different building");
    }
}
