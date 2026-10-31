using System;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// tutoriallot [seed]: where Old Guy's house lands (TutorialLot) beside the spawn: its frame, how far its front is from the spawn, the ground under
/// it, the step up from the pavement, and the generated buildings left nearest it.
/// tutorialseeds [first] [count]: seeds on which the house is the building nearest the spawn, ranked as sunnyspawn ranks them (a flat spawn in the sun).
/// </summary>
public static class TutorialSeeds
{
    public static void Rank(int first, int count)
    {
        var cfg = PlanConfig.For(CityScale.Active);
        var rng = new Random(first);
        var flattest = new System.Collections.Generic.List<(double spread, int seed)>();
        for (int k = 0; k < count; k++)
        {
            int seed = rng.Next(10000000, 99999999);
            flattest.Add((SunnySpawn.Spread(new PlanFields(seed, cfg), 0.0, 0.0, 300.0, 25.0), seed));
        }
        flattest.Sort((a, b) => a.spread.CompareTo(b.spread));
        var ranked = new System.Collections.Generic.List<(double score, string line)>();
        for (int k = 0; k < 40 && k < flattest.Count; k++)
        {
            int seed = flattest[k].seed;
            PlanWorld world = PlanWorld.Create(seed, cfg);
            world.TryFindSpawn(out double sx, out double sz);
            TutorialLot lot = world.TutorialHouse;
            if (!lot.Found)
            {
                Console.WriteLine($"  seed {seed}: no land for the house");
                continue;
            }
            double houseGap = GapToHouse(lot, sx, sz), buildingGap = NearestBuildingGap(world, sx, sz);
            Vec2 front = lot.FrontMiddle;
            double step = lot.FloorHeight - world.GroundHeight(front.X, front.Y) - PlanBuildings.PavingRise;
            double spread = SunnySpawn.Spread(world.Fields, sx, sz, 100.0, 10.0), lit = SunnySpawn.LitShare(world, sx, sz);
            bool nearest = houseGap < buildingGap;
            string line = $"seed {seed}: house {houseGap:F1} m from the spawn, nearest other building {buildingGap:F1} m{(nearest ? "" : " (NOT NEAREST)")}, step {step:F2} m, {lit:P0} in sun, spread {spread:F1} m";
            Console.WriteLine("  " + line);
            if (nearest) ranked.Add((lit - spread * 0.05 - houseGap * 0.005, line));
        }
        ranked.Sort((a, b) => b.score.CompareTo(a.score));
        Console.WriteLine("best:");
        foreach (var r in ranked) Console.WriteLine("  " + r.line);
    }

    private static double GapToHouse(in TutorialLot lot, double x, double z)
    {
        lot.ToLocal(x, z, out double lx, out double lz);
        double dx = Math.Max(0.0, Math.Max(TutorialLot.HouseX0 - lx, lx - TutorialLot.HouseX1));
        double dz = Math.Max(0.0, Math.Max(TutorialLot.HouseZ0 - lz, lz - TutorialLot.HouseZ1));
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private static double NearestBuildingGap(PlanWorld world, double x, double z)
    {
        double best = double.MaxValue;
        foreach (BuildingSite b in world.Buildings)
        {
            if (b.IsOpenGround || Math.Abs(b.CentreX - x) > 200.0 || Math.Abs(b.CentreZ - z) > 200.0) continue;
            b.WorldToLocal(x, z, out double lx, out double lz);
            double dx = Math.Max(0.0, Math.Abs(lx) - b.Params.Width * 0.5), dz = Math.Max(0.0, Math.Abs(lz) - b.Params.Depth * 0.5);
            best = Math.Min(best, Math.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    public static void Report(int seed)
    {
        PlanWorld world = PlanWorld.Create(seed, PlanConfig.For(CityScale.Active));
        world.TryFindSpawn(out double sx, out double sz);
        TutorialLot lot = world.TutorialHouse;
        Console.WriteLine($"seed {seed}: spawn ({sx:F1}, {sz:F1}), {world.Buildings.Length} buildings");
        if (!lot.Found)
        {
            Console.WriteLine("  no land near the spawn holds the house");
            return;
        }
        Vec2 front = lot.FrontMiddle;
        double front0 = world.GroundHeight(front.X, front.Y);
        Console.WriteLine($"  origin ({lot.OriginX:F1}, {lot.OriginZ:F1}) axis ({lot.AxisX:F3}, {lot.AxisZ:F3}) block {lot.Block}");
        Console.WriteLine($"  front middle ({front.X:F1}, {front.Y:F1}), {(front - new Vec2(sx, sz)).Length:F1} m from the spawn");
        Console.WriteLine($"  floor {lot.FloorHeight:F2}, lowest ground {lot.LowestGround:F2}; pavement at the front {front0 + PlanBuildings.PavingRise:F2}: a step of {lot.FloorHeight - front0 - PlanBuildings.PavingRise:F2} m");
        double nearest = double.MaxValue;
        foreach (BuildingSite b in world.Buildings)
        {
            if (lot.Overlaps(b)) Console.WriteLine($"  OVERLAP #{b.Id}");
            double d = (new Vec2(b.CentreX, b.CentreZ) - new Vec2(sx, sz)).Length;
            nearest = Math.Min(nearest, d);
        }
        lot.ToWorld((TutorialLot.HouseX0 + TutorialLot.HouseX1) * 0.5, (TutorialLot.HouseZ0 + TutorialLot.HouseZ1) * 0.5, out double hx, out double hz);
        Console.WriteLine($"  the house's middle {(new Vec2(hx, hz) - new Vec2(sx, sz)).Length:F1} m from the spawn; the nearest generated building's middle {nearest:F1} m");
    }
}
