using System;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// tutoriallot [seed]: where Old Guy's house lands (TutorialLot) beside the spawn: its frame, how far its front is from the spawn, the ground under
/// it, the step up from the pavement, and the generated buildings left nearest it.
/// tutorialseeds [first] [count]: seeds ranked by the sun and the flat ground where the player lands (the house's driveway), with a small step onto it.
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
            SkyHubPlan hub = world.SkyHub;
            if (!hub.Found)
            {
                Console.WriteLine($"  seed {seed}: no land for the sky hub's landing");
                continue;
            }
            // The player lands on the driveway (TutorialLot.LandingX/Z): the sun and the ground round that spot are what is seen first.
            lot.ToWorld(TutorialLot.LandingX, TutorialLot.LandingZ, out double lx, out double lz);
            double houseGap = GapToHouse(lot, sx, sz), buildingGap = NearestBuildingGap(world, lx, lz);
            Vec2 front = lot.FrontMiddle;
            double step = lot.FloorHeight - world.GroundHeight(front.X, front.Y) - PlanBuildings.PavingRise;
            double spread = SunnySpawn.Spread(world.Fields, lx, lz, 100.0, 10.0), lit = SunnySpawn.LitShare(world, lx, lz);
            string line = $"seed {seed}: house walls {houseGap:F1} m from the spawn junction, nearest other building {buildingGap:F1} m from the landing, step {step:F2} m, {lit:P0} in sun at the landing, spread {spread:F1} m";
            double hillShare = HillShare(seed);
            line += $", sky hub landing {(hub.PlaceAt(0.0) - new Vec2(sx, sz)).Length:F0} m from the spawn, {hillShare:P1} of the city on a hill";
            Console.WriteLine("  " + line);
            if (step <= 0.3 && hillShare > 0.125) ranked.Add((lit - spread * 0.05 - houseGap * 0.003, line));
        }
        ranked.Sort((a, b) => b.score.CompareTo(a.score));
        Console.WriteLine("best:");
        foreach (var r in ranked) Console.WriteLine("  " + r.line);
    }

    /// <summary>The share of the land within 2.5 km of the origin on a hill, as HillTests measures it (the game's city wants hills; a flat spawn can come with a flat city).</summary>
    private static double HillShare(int seed)
    {
        var fields = new PlanFields(seed, PlanConfig.For(CityScale.Varied));
        int land = 0, hilly = 0;
        const double step = 20.0;
        for (double x = -2500.0; x <= 2500.0; x += step)
        {
            for (double z = -2500.0; z <= 2500.0; z += step)
            {
                if (fields.IsWater(x, z)) continue;
                land++;
                if (fields.Hillock(x, z) > 2.0) hilly++;
            }
        }
        return (double)hilly / land;
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

    /// <summary>Where the sky hub lands (SkyHubPlan): its middle, its landing, how far they are from the spawn.</summary>
    private static void ReportHub(PlanWorld world, double sx, double sz)
    {
        SkyHubPlan hub = world.SkyHub;
        if (!hub.Found)
        {
            Console.WriteLine("  no land near the spawn holds the sky hub's landing");
            return;
        }
        Vec2 foot = hub.PlaceAt(0.0), spawn = new Vec2(sx, sz);
        Console.WriteLine($"  sky hub middle ({hub.CentreX:F1}, {hub.CentreZ:F1}), {(new Vec2(hub.CentreX, hub.CentreZ) - spawn).Length:F0} m from the spawn; landing ({foot.X:F1}, {foot.Y:F1}), {(foot - spawn).Length:F0} m from the spawn, floor {hub.FootHeight:F2}, ground down to {hub.LowestGround:F2}, block {hub.Block}");
    }

    public static void Report(int seed)
    {
        PlanWorld world = PlanWorld.Create(seed, PlanConfig.For(CityScale.Active));
        world.TryFindSpawn(out double sx, out double sz);
        TutorialLot lot = world.TutorialHouse;
        Console.WriteLine($"seed {seed}: spawn ({sx:F1}, {sz:F1}), {world.Buildings.Length} buildings");
        ReportHub(world, sx, sz);
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
