public static class Program
{
    public static int Main(string[] args)
    {
        // dotnet run -c Release --project tools/cityplan -- [seed] [halfMetres] [pixels] [out.png] [centreX centreZ viewHalf]
        if (args.Length > 0 && args[0] == "nearspawn") { PlanDiagnose.NearSpawn(args.Length > 1 ? int.Parse(args[1]) : 87650022, args.Length > 2 ? int.Parse(args[2]) : 12); return 0; }
        if (args.Length > 0 && args[0] == "overlaps") { PlanDiagnose.Overlaps(args.Length > 1 ? int.Parse(args[1]) : 87650022); return 0; }
        if (args.Length > 0 && args[0] == "profile") { PlanProfile.Run(args.Length > 1 ? int.Parse(args[1]) : 87650022); return 0; }
        return SubversionPlan.Run(args);
    }
}
