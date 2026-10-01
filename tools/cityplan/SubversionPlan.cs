using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using CityX.World.Plan;

/// <summary>
/// Renders the Subversion-style city plan (World/Plan) to a PNG and prints statistics, with no Unity involved.
/// Usage: dotnet run -c Release --project tools/cityplan -- sub [seed] [halfMetres] [pixels] [out.png]
/// </summary>
public static class SubversionPlan
{
    public static int Run(string[] args)
    {
        int seed = args.Length > 1 ? int.Parse(args[1]) : 1234;
        double half = args.Length > 2 ? double.Parse(args[2]) : 1500.0;
        int px = args.Length > 3 ? int.Parse(args[3]) : 1800;
        string outPath = args.Length > 4 ? args[4] : "subplan.png";

        var cfg = new PlanConfig { Half = half };
        var sw = Stopwatch.StartNew();
        CityPlanData plan = CityPlan.Generate(seed, cfg);
        sw.Stop();

        double heightSum = 0, tallest = 0; int towers = 0;
        foreach (var lot in plan.Lots) { heightSum += lot.Height; if (lot.Height > tallest) tallest = lot.Height; if (lot.Height > 80) towers++; }
        Console.WriteLine($"seed {seed}, region {2 * half:0}m: {plan.Centres.Count} centres, {plan.Nodes.Count} nodes, {plan.Edges.Count} edges, " +
            $"{plan.Blocks.Count} blocks, {plan.Lots.Count} lots; generated in {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  mean height {(plan.Lots.Count > 0 ? heightSum / plan.Lots.Count : 0):0.#} m, tallest {tallest:0} m, {towers} towers over 80 m");

        double scale = px / (2 * half);
        Func<Vec2, PointF> P = v => new PointF((float)((v.X + half) * scale), (float)((half - v.Y) * scale));

        using (var bmp = new Bitmap(px, px))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Ground and water.
            var data = bmp.LockBits(new Rectangle(0, 0, px, px), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var pixels = new int[px * px];
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    double wx = x / scale - half, wy = half - y / scale;
                    int c;
                    if (plan.Fields.IsWater(wx, wy)) c = Color.FromArgb(20, 40, 215).ToArgb();
                    else
                    {
                        double h = plan.Fields.Height(wx, wy);
                        int gch = (int)Math.Max(40, Math.Min(150, 70 + h * 0.9));
                        c = Color.FromArgb(25, gch, 55).ToArgb();
                    }
                    pixels[y * px + x] = c;
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            bmp.UnlockBits(data);

            // Blocks (the land between roads).
            using (var brush = new SolidBrush(Color.FromArgb(70, 20, 40, 60)))
            {
                foreach (var b in plan.Blocks)
                {
                    var pts = new PointF[b.Poly.Count];
                    for (int i = 0; i < pts.Length; i++) pts[i] = P(b.Poly[i]);
                    g.FillPolygon(brush, pts);
                }
            }

            // Roads.
            foreach (var e in plan.Edges)
            {
                if (!e.Alive) continue;
                float w = (float)(CityPlan.HalfWidth(cfg, e.Class) * 2.0 * scale);
                Color col = e.Bridge ? Color.FromArgb(255, 240, 160) : (e.Class == RoadClass.Highway ? Color.FromArgb(215, 215, 235) : (e.Class == RoadClass.Arterial ? Color.FromArgb(170, 170, 200) : Color.FromArgb(120, 120, 150)));
                using (var pen = new Pen(col, Math.Max(1.0f, w)))
                {
                    g.DrawLine(pen, P(e.A.P), P(e.B.P));
                }
            }

            // Lots and their buildings.
            foreach (var lot in plan.Lots)
            {
                var pts = new PointF[lot.Footprint.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = P(lot.Footprint[i]);
                int shade = (int)Math.Min(255, 60 + lot.Height * 1.2);
                using (var brush = new SolidBrush(Color.FromArgb(150, shade / 2, shade * 3 / 4, shade)))
                using (var pen = new Pen(Color.FromArgb(235, 235, 245), 1.0f))
                {
                    g.FillPolygon(brush, pts);
                    g.DrawPolygon(pen, pts);
                }
            }

            foreach (var c in plan.Centres)
            {
                var p = P(c);
                g.DrawEllipse(Pens.Red, p.X - 6, p.Y - 6, 12, 12);
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
        Console.WriteLine("wrote " + outPath);
        return 0;
    }
}
