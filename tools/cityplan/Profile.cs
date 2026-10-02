using System;
using System.Diagnostics;
using CityX.World;
using CityX.World.Plan;

/// <summary>Per-stage timing of the plan generator: dotnet run -c Release -- profile [seed]</summary>
public static class PlanProfile
{
    public static void Run(int seed)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            var cfg = PlanWorld.DefaultConfig();
            var sw = Stopwatch.StartNew();
            var fields = new PlanFields(seed, cfg);
            var centres = fields.FindCentres(cfg.Half, 100.0, 650.0, 0.52, 6);
            long tCentres = sw.ElapsedMilliseconds; sw.Restart();

            var net = new RoadNetwork(fields, PlanNoise.Mix(unchecked((uint)seed), 0x726F6164U));
            net.Grow(centres, cfg.Half);
            long tRoads = sw.ElapsedMilliseconds; sw.Restart();

            var data = new CityPlanData { Config = cfg, Fields = fields, Centres = centres, Nodes = net.Nodes, Edges = net.Edges };
            data.Blocks = BlockExtractor.Extract(net, cfg);
            long tBlocks = sw.ElapsedMilliseconds; sw.Restart();

            LotSubdivider.Subdivide(data, PlanNoise.Mix(unchecked((uint)seed), 0x6C6F7473U));
            long tLots = sw.ElapsedMilliseconds; sw.Restart();

            var geo = PlanGeometry.Build(data);
            long tGeo = sw.ElapsedMilliseconds; sw.Restart();

            var buildings = PlanBuildings.Resolve(data, fields, unchecked((uint)seed));
            long tBuildings = sw.ElapsedMilliseconds;
            Console.WriteLine($"pass {pass}: centres {tCentres} ms, roads {tRoads} ms, blocks {tBlocks} ms, lots {tLots} ms, geometry {tGeo} ms, buildings {tBuildings} ms " +
                $"(total {tCentres + tRoads + tBlocks + tLots + tGeo + tBuildings} ms; {buildings.Length} buildings)");
        }
    }
}
