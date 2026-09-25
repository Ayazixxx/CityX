using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using CityX.World;

public static class Program
{
    static Color ZoneColour(ZoneKind z)
    {
        switch (z)
        {
            case ZoneKind.Core: return Color.FromArgb(230, 60, 60);
            case ZoneKind.Urban: return Color.FromArgb(230, 150, 60);
            case ZoneKind.Residential: return Color.FromArgb(90, 150, 210);
            case ZoneKind.OldTown: return Color.FromArgb(170, 110, 200);
            case ZoneKind.Industrial: return Color.FromArgb(150, 150, 60);
            default: return Color.FromArgb(70, 140, 80);
        }
    }

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "bench") { Bench(); return 0; }
        int seed = args.Length > 4 ? int.Parse(args[4]) : 58316047;
        int min = args.Length > 0 ? int.Parse(args[0]) : -96;
        int max = args.Length > 1 ? int.Parse(args[1]) : 95;
        int px = args.Length > 2 ? int.Parse(args[2]) : 6;
        string outPath = args.Length > 3 ? args[3] : "plan.png";
        var world = new CityWorld(seed);
        int span = max - min + 1;

        // ---------------- plan ----------------
        using (var bmp = new Bitmap(span * px, span * px))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(13, 15, 18));
            for (int cx = min; cx <= max; cx++)
            for (int cy = min; cy <= max; cy++)
            {
                int sx = (cx - min) * px, sy = (max - cy) * px;
                if (CityLayout.IsStreetChunk(cx, cy))
                {
                    Color c = CityLayout.IsHighwayChunk(cx, cy) ? Color.FromArgb(255, 150, 40)
                            : CityLayout.IsArterialChunk(cx, cy) ? Color.FromArgb(150, 155, 165)
                            : Color.FromArgb(70, 74, 82);
                    using var b = new SolidBrush(c);
                    g.FillRectangle(b, sx, sy, px, px);
                    continue;
                }
                LotRole role = world.RoleAt(cx, cy);
                if (role.Kind == LotKind.Open)
                {
                    using var b = new SolidBrush(Color.FromArgb(40, 90, 50));
                    g.FillRectangle(b, sx, sy, px, px);
                    continue;
                }
                BuildingSite site = world.BuildingAt(cx, cy);
                if (!site.Exists || site.AnchorX != cx || site.AnchorY != cy) continue;
                double halfW = site.Params.Width / (2.0 * CityLayout.ChunkSize);
                double halfD = site.Params.Depth / (2.0 * CityLayout.ChunkSize);
                double ccx = cx + site.Role.SpanX * 0.5 + site.Params.OffsetX / CityLayout.ChunkSize;
                double ccy = cy + site.Role.SpanY * 0.5 + site.Params.OffsetZ / CityLayout.ChunkSize;
                float left = (float)((ccx - halfW - min) * px);
                float top = (float)((max + 1 - (ccy + halfD)) * px);
                float w = (float)(halfW * 2 * px), h = (float)(halfD * 2 * px);
                Color mc;
                switch (site.Params.Mass)
                {
                    case BuildingMass.LowRise: mc = Color.FromArgb(111, 117, 124); break;
                    case BuildingMass.MidRise: mc = Color.FromArgb(154, 162, 171); break;
                    case BuildingMass.Slab: mc = Color.FromArgb(195, 202, 210); break;
                    case BuildingMass.Tower: mc = Color.FromArgb(240, 245, 250); break;
                    case BuildingMass.Warehouse: mc = Color.FromArgb(150, 130, 90); break;
                    default: mc = Color.FromArgb(255, 180, 84); break;
                }
                using var bb = new SolidBrush(mc);
                g.FillRectangle(bb, left, top, w, h);
            }
            bmp.Save(outPath, ImageFormat.Png);
        }

        // ---------------- height map ----------------
        using (var bmp = new Bitmap(span * px, span * px))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(10, 12, 16));
            for (int cx = min; cx <= max; cx++)
            for (int cy = min; cy <= max; cy++)
            {
                int sx = (cx - min) * px, sy = (max - cy) * px;
                if (CityLayout.IsStreetChunk(cx, cy))
                {
                    using var sb = new SolidBrush(Color.FromArgb(26, 28, 34));
                    g.FillRectangle(sb, sx, sy, px, px);
                    continue;
                }
                LotRole role = world.RoleAt(cx, cy);
                if (role.Kind == LotKind.Open)
                {
                    using var ob = new SolidBrush(Color.FromArgb(24, 52, 30));
                    g.FillRectangle(ob, sx, sy, px, px);
                    continue;
                }
                BuildingSite site = world.BuildingAt(cx, cy);
                if (!site.Exists || site.AnchorX != cx || site.AnchorY != cy) continue;
                double halfW = site.Params.Width / (2.0 * CityLayout.ChunkSize);
                double halfD = site.Params.Depth / (2.0 * CityLayout.ChunkSize);
                double ccx = cx + site.Role.SpanX * 0.5 + site.Params.OffsetX / CityLayout.ChunkSize;
                double ccy = cy + site.Role.SpanY * 0.5 + site.Params.OffsetZ / CityLayout.ChunkSize;
                float left = (float)((ccx - halfW - min) * px);
                float top = (float)((max + 1 - (ccy + halfD)) * px);
                float w = (float)(halfW * 2 * px), h = (float)(halfD * 2 * px);
                double t = Math.Min(1.0, site.Params.Height / 300.0);
                // low = deep blue, mid = green/yellow, tall = white-hot
                int r = (int)(40 + 215 * Math.Pow(t, 0.8));
                int gg = (int)(60 + 195 * Math.Pow(t, 1.1));
                int bl = (int)(140 - 60 * t + (t > 0.7 ? 175 * (t - 0.7) / 0.3 : 0));
                using var hb = new SolidBrush(Color.FromArgb(Math.Min(255, r), Math.Min(255, gg), Math.Max(0, Math.Min(255, bl))));
                g.FillRectangle(hb, left, top, w, h);
            }
            bmp.Save(outPath.Replace(".png", "_height.png"), ImageFormat.Png);
        }

        // ---------------- zone map (per chunk) ----------------
        using (var bmp = new Bitmap(span * px, span * px))
        using (var g = Graphics.FromImage(bmp))
        {
            for (int cx = min; cx <= max; cx++)
            for (int cy = min; cy <= max; cy++)
            {
                double d = CityZones.DensityAt(cx + 0.5, cy + 0.5);
                ZoneKind z = CityZones.ZoneFor(d, CityZones.CharacterAt(cx + 0.5, cy + 0.5));
                Color zc = ZoneColour(z);
                int shade = (int)(70 + 185 * d);
                Color c = Color.FromArgb(zc.R * shade / 255, zc.G * shade / 255, zc.B * shade / 255);
                using var b = new SolidBrush(c);
                g.FillRectangle(b, (cx - min) * px, (max - cy) * px, px, px);
            }
            bmp.Save(outPath.Replace(".png", "_zones.png"), ImageFormat.Png);
        }

        // ---------------- statistics ----------------
        var zoneChunks = new Dictionary<ZoneKind, int>();
        var massCount = new Dictionary<BuildingMass, int>();
        var heightSum = new Dictionary<ZoneKind, double>();
        var heightN = new Dictionary<ZoneKind, int>();
        int total = 0, streets = 0, open = 0, buildings = 0, lots = 0, emptyLots = 0, tall28 = 0, tall60 = 0, arterial = 0;
        var blockSizes = new Dictionary<string, int>();
        var seen = new HashSet<(int, int)>();
        var spans = new Dictionary<string, int>();
        for (int cx = min; cx <= max; cx++)
        for (int cy = min; cy <= max; cy++)
        {
            total++;
            ZoneKind z = CityZones.ZoneAt(cx + 0.5, cy + 0.5);
            zoneChunks[z] = zoneChunks.GetValueOrDefault(z) + 1;
            if (CityLayout.IsStreetChunk(cx, cy)) { streets++; if (CityLayout.IsArterialChunk(cx, cy)) arterial++; continue; }
            LotRole role = world.RoleAt(cx, cy);
            if (role.Kind == LotKind.Open) { open++; }
            if (CityLayout.TryGetBlockRect(cx, cy, out int mx, out int my, out int bw, out int bh) && seen.Add((mx, my)))
            {
                string k = $"{bw}x{bh}";
                blockSizes[k] = blockSizes.GetValueOrDefault(k) + 1;
            }
            if (role.Kind == LotKind.Building)
            {
                lots++;
                string sk = $"{role.SpanX}x{role.SpanY}";
                spans[sk] = spans.GetValueOrDefault(sk) + 1;
                BuildingSite site = world.BuildingAt(cx, cy);
                if (!site.Exists) { emptyLots++; continue; }
                buildings++;
                massCount[site.Params.Mass] = massCount.GetValueOrDefault(site.Params.Mass) + 1;
                ZoneKind bz = CityChunkGenerator.ZoneOfRole(role);
                heightSum[bz] = heightSum.GetValueOrDefault(bz) + site.Params.Height;
                heightN[bz] = heightN.GetValueOrDefault(bz) + 1;
                if (site.Params.Height >= 28) tall28++;
                if (site.Params.Height >= 60) tall60++;
            }
        }
        Console.WriteLine($"chunks={total} streets={100.0 * streets / total:0.0}% (arterial {100.0 * arterial / total:0.0}%) openChunks={100.0 * open / total:0.0}% lots={lots} buildings={buildings} emptyLots={100.0 * emptyLots / lots:0.0}%");
        Console.WriteLine("zones by chunk: " + string.Join(", ", ZoneList(zoneChunks, total)));
        Console.Write("mass:");
        foreach (var kv in massCount) Console.Write($" {kv.Key}={kv.Value} ({100.0 * kv.Value / buildings:0.0}%)");
        Console.WriteLine();
        Console.WriteLine($"28m+ = {100.0 * tall28 / buildings:0.0}%   60m+ = {100.0 * tall60 / buildings:0.0}%");
        Console.Write("avg height by zone:");
        foreach (var kv in heightSum) Console.Write($" {kv.Key}={kv.Value / heightN[kv.Key]:0}m");
        Console.WriteLine();
        Console.Write("lot spans:");
        foreach (var kv in spans) Console.Write($" {kv.Key}={kv.Value}");
        Console.WriteLine();
        var bs = new List<KeyValuePair<string, int>>(blockSizes);
        bs.Sort((a, b) => b.Value.CompareTo(a.Value));
        Console.Write("block sizes (top 12):");
        for (int i = 0; i < Math.Min(12, bs.Count); i++) Console.Write($" {bs[i].Key}:{bs[i].Value}");
        Console.WriteLine();
        return 0;
    }

    static void Bench()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int hits = 0;
        for (int rep = 0; rep < 20; rep++)
        for (int x = -200; x < 200; x++)
        for (int y = -200; y < 200; y++)
            if (CityLayout.IsStreetChunk(x, y)) hits++;
        sw.Stop();
        double calls = 20.0 * 400 * 400;
        Console.WriteLine($"IsStreetChunk: {sw.Elapsed.TotalMilliseconds * 1e6 / calls:0.0} ns/call ({hits} hits)");
        var world = new CityWorld(58316047);
        sw.Restart();
        int b = 0;
        for (int rep = 0; rep < 3; rep++)
        for (int x = -200; x < 200; x++)
        for (int y = -200; y < 200; y++)
            if (world.BuildingAt(x, y).Exists) b++;
        sw.Stop();
        Console.WriteLine($"BuildingAt: {sw.Elapsed.TotalMilliseconds * 1e6 / (3.0 * 160000):0.0} ns/call ({b} buildings)");
        sw.Restart();
        double acc = 0;
        for (int rep = 0; rep < 3; rep++)
        for (int x = -200; x < 200; x++)
        for (int y = -200; y < 200; y++)
            acc += CityZones.DensityAt(x + 0.5, y + 0.5);
        sw.Stop();
        Console.WriteLine($"DensityAt: {sw.Elapsed.TotalMilliseconds * 1e6 / (3.0 * 160000):0.0} ns/call ({acc:0.0})");
    }

    static IEnumerable<string> ZoneList(Dictionary<ZoneKind, int> d, int total)
    {
        foreach (var kv in d) yield return $"{kv.Key} {100.0 * kv.Value / total:0.0}%";
    }
}
