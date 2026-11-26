/// <summary>tools/plan_time_mono.ps1's entry: the plan's stages timed in Unity's own Mono (tools/cityplan/WorldTime.cs).</summary>
public static class PlanTimeMono
{
    public static int Main(string[] args)
    {
        WorldTime.Run(args.Length > 0 ? int.Parse(args[0]) : 48398181);
        return 0;
    }
}
