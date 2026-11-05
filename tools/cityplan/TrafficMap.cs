using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// traffic [seed] [out.png]: counts what governs the city's junctions (docs/TRAFFIC_CONTROL.md) and draws the 2.6 km round the spawn: lights where two main roads
/// meet in cyan, a side street's lights (which flash late at night) in green, all-way stops as red squares, a side street's STOP as an orange dot with a tick
/// along the street that runs free. The game's own city unless CITYX_CITY_SCALE says otherwise (the tests run on "classic").
/// </summary>
public static class TrafficMap
{
    public static void Run(int seed, string png)
    {
        PlanWorld plan = PlanWorld.Create(seed, PlanConfig.For(CityScale.Active));
        TrafficPlan traffic = plan.Traffic;
        var counts = new int[4];
        var kinds = new SortedDictionary<string, int>();
        var signals = new List<TrafficJunction>();
        int junctions = 0;
        foreach (TrafficJunction j in traffic.Junctions)
        {
            if (j == null) continue;
            junctions++;
            counts[(int)j.Control]++;
            int driven = 0;
            foreach (bool d in j.Drives) if (d) driven++;
            string kind = $"{j.Control} {driven}-leg {j.ClassOf(true)}x{j.ClassOf(false)}";
            kinds[kind] = kinds.TryGetValue(kind, out int n) ? n + 1 : 1;
            if (j.Control == JunctionControl.Signal) signals.Add(j);
        }
        Console.WriteLine($"scale {CityScale.Active.Name}, seed {seed}: {junctions} junctions: lights {counts[3]} ({100.0 * counts[3] / junctions:F0}%), all-way stops {counts[2]}, " +
            $"STOP on the side street {counts[1]}, nothing {counts[0]}");
        foreach (KeyValuePair<string, int> kind in kinds) Console.WriteLine($"  {kind.Value,5}  {kind.Key}");

        var nearest = new List<double>();
        foreach (TrafficJunction a in signals)
        {
            double best = double.MaxValue;
            foreach (TrafficJunction b in signals)
            {
                if (a != b) best = Math.Min(best, (a.Node.P - b.Node.P).Length);
            }
            nearest.Add(best);
        }
        nearest.Sort();
        if (nearest.Count > 0)
        {
            Console.WriteLine($"from each lights to the nearest other: least {nearest[0]:F0} m, median {nearest[nearest.Count / 2]:F0} m");
        }

        plan.TryFindSpawn(out double sx, out double sz);
        Console.WriteLine($"spawn {sx:F0},{sz:F0}; lights within 200 m:");
        foreach (TrafficJunction j in signals)
        {
            if ((j.Node.P - new Vec2(sx, sz)).Length > 200.0) continue;
            SignalTiming t = j.Timing;
            Console.WriteLine($"  node {j.Node.Id} {j.ClassOf(true)}x{j.ClassOf(false)}: cycle {t.Cycle:F0} s; main {t.GreenMajor:F0}+{t.YellowMajor:F1}+{t.AllRedMajor:F1}; " +
                $"side {t.GreenMinor:F0}+{t.YellowMinor:F1}+{t.AllRedMinor:F1}; flashes at night {t.NightFlash}");
        }
        Draw(plan, traffic, sx, sz, png);
    }

    private static void Draw(PlanWorld plan, TrafficPlan traffic, double cx, double cz, string path)
    {
        const int size = 2400;
        const double half = 1300.0;
        double scale = size / (2.0 * half);
        PointF At(Vec2 p) => new PointF((float)((p.X - cx + half) * scale), (float)(size - (p.Y - cz + half) * scale));
        using var image = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using Graphics g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(24, 26, 30));
        foreach (RoadEdge e in plan.Data.Edges)
        {
            if (!e.Alive) continue;
            bool walk = StepStreets.Is(plan, e) || PedestrianStreets.Is(plan, e);
            Color colour = walk ? Color.FromArgb(70, 110, 90) : e.Class == RoadClass.Boulevard ? Color.FromArgb(235, 235, 235)
                : e.Class == RoadClass.Arterial ? Color.FromArgb(170, 170, 175) : Color.FromArgb(95, 97, 104);
            using var pen = new Pen(colour, e.Class == RoadClass.Boulevard ? 7.0f : e.Class == RoadClass.Arterial ? 5.0f : 2.5f);
            g.DrawLine(pen, At(e.A.P), At(e.B.P));
        }
        foreach (TrafficJunction j in traffic.Junctions)
        {
            if (j == null || j.Control == JunctionControl.None) continue;
            PointF p = At(j.Node.P);
            if (j.Control == JunctionControl.Signal)
            {
                using var brush = new SolidBrush(j.Timing.NightFlash ? Color.FromArgb(60, 220, 110) : Color.FromArgb(40, 255, 255));
                g.FillEllipse(brush, p.X - 7, p.Y - 7, 14, 14);
            }
            else if (j.Control == JunctionControl.AllWayStop)
            {
                using var brush = new SolidBrush(Color.FromArgb(235, 60, 50));
                g.FillRectangle(brush, p.X - 5, p.Y - 5, 10, 10);
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(240, 160, 40));
                using var pen = new Pen(Color.FromArgb(240, 160, 40), 2.0f);
                g.FillEllipse(brush, p.X - 3.5f, p.Y - 3.5f, 7, 7);
                for (int i = 0; i < j.Legs.Length; i++)
                {
                    if (j.Major[i]) g.DrawLine(pen, p, At(j.Node.P + j.Dirs[i] * 14.0));
                }
            }
        }
        using (var spawn = new Pen(Color.Magenta, 3.0f))
        {
            PointF s = At(new Vec2(cx, cz));
            g.DrawEllipse(spawn, s.X - 12, s.Y - 12, 24, 24);
        }
        image.Save(path, ImageFormat.Png);
        Console.WriteLine("wrote " + path);
    }
}
