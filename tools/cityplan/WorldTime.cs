using System;
using System.Diagnostics;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// How long the game's own city (CityScale.Default) takes to plan, stage by stage, as PlanWorld builds it: dotnet run -c Release -- worldtime [seed].
/// The title screen waits for all of it; CoreCLR here is faster than the game's Mono, but the shares are alike.
/// </summary>
public static class WorldTime
{
    public static void Run(int seed)
    {
        CityScale.Active = CityScale.Default;
        for (int pass = 0; pass < 3; pass++)
        {
            PlanConfig cfg = PlanConfig.For(CityScale.Active);
            var sw = Stopwatch.StartNew();
            var fields = new PlanFields(seed, cfg);
            var centres = fields.FindCentres(cfg.Half, 100.0 * cfg.Scale, 650.0 * cfg.Scale, 0.52, 6);
            if (centres.Count == 0) centres.Add(new Vec2(0.0, 0.0));
            long tCentres = sw.ElapsedMilliseconds; sw.Restart();

            var net = new RoadNetwork(fields, PlanNoise.Mix(unchecked((uint)seed), 0x726F6164U));
            net.Grow(centres, cfg.Half);
            long tRoads = sw.ElapsedMilliseconds; sw.Restart();

            var data = new CityPlanData { Config = cfg, Fields = fields, Centres = centres, Nodes = net.Nodes, Edges = net.Edges };
            data.Expressways = ExpresswayPlanner.Plan(net, fields, cfg, PlanNoise.Mix(unchecked((uint)seed), 0x78776179U), centres);
            long tExpressways = sw.ElapsedMilliseconds; sw.Restart();

            data.Blocks = BlockExtractor.Extract(net, cfg, out bool[] bounding);
            CityPlan.MarkEnclosing(data.Blocks);
            BlockExtractor.RetireRoadsInsideBlocks(net, bounding, data.Blocks, data.Expressways, cfg);
            long tBlocks = sw.ElapsedMilliseconds; sw.Restart();

            LotSubdivider.Subdivide(data, PlanNoise.Mix(unchecked((uint)seed), 0x6C6F7473U));
            long tLots = sw.ElapsedMilliseconds; sw.Restart();

            PlanGeometry.Build(data);
            long tGeometry = sw.ElapsedMilliseconds; sw.Restart();

            BuildingSite[] buildings = PlanBuildings.Resolve(data, fields, unchecked((uint)seed));
            long tBuildings = sw.ElapsedMilliseconds; sw.Restart();

            // The expressways' own steps, through reflection (they are private).
            var net2 = new RoadNetwork(fields, PlanNoise.Mix(unchecked((uint)seed), 0x726F6164U));
            net2.Grow(centres, cfg.Half);
            var type = typeof(ExpresswayPlanner);
            ExpresswayPlanner.Tally = new System.Collections.Generic.Dictionary<string, int>();
            object planner = type.GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
                    new[] { typeof(RoadNetwork), typeof(PlanFields), typeof(PlanConfig), typeof(uint), typeof(System.Collections.Generic.List<Vec2>) }, null)
                .Invoke(new object[] { net2, fields, cfg, PlanNoise.Mix(unchecked((uint)seed), 0x78776179U), centres });
            var steps = new System.Text.StringBuilder("  expressway steps:");
            sw.Restart();
            foreach (string step in new[] { "LayRoutes", "LayProfiles", "SystemInterchanges", "Exits", "Furnish" })
            {
                type.GetMethod(step, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(planner, null);
                steps.Append($" {step} {sw.ElapsedMilliseconds}");
                sw.Restart();
            }
            Console.WriteLine(steps);
            sw.Restart();

            PlanWorld world = PlanWorld.Create(seed, cfg);
            long tWhole = sw.ElapsedMilliseconds;
            long stages = tCentres + tRoads + tExpressways + tBlocks + tLots + tGeometry + tBuildings;
            Console.WriteLine($"pass {pass}: centres {tCentres}, roads {tRoads}, expressways {tExpressways}, blocks {tBlocks}, lots {tLots}, geometry {tGeometry}, " +
                $"buildings {tBuildings} (stages {stages} ms); whole PlanWorld {tWhole} ms, so the rest (spawn, house, hub, indexes, traffic) ~{tWhole - stages} ms; " +
                $"{buildings.Length} / {world.Buildings.Length} buildings");
        }
    }
}
