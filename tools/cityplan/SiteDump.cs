using System;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// sitedump [seed] [id] [classic]: one building of the plan (classic: on the classic city the tests use): its box, its blank wall stretches
/// (contacts), and every building within 12 m of its footprint, with theirs.
/// </summary>
public static class SiteDump
{
    public static void Run(int seed, int id, bool classic)
    {
        if (classic) CityScale.Active = CityScale.Classic;
        PlanWorld world = PlanWorld.For(seed);
        BuildingSite site = world.Buildings[id];
        Print(site);
        foreach (BuildingSite other in world.Buildings)
        {
            if (other.Id == id || Math.Abs(other.CentreX - site.CentreX) > 80.0 || Math.Abs(other.CentreZ - site.CentreZ) > 80.0) continue;
            double gap = double.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                other.LocalToWorld((k & 1) == 0 ? -other.Params.Width * 0.5 : other.Params.Width * 0.5, (k & 2) == 0 ? -other.Params.Depth * 0.5 : other.Params.Depth * 0.5, out double wx, out double wz);
                site.WorldToLocal(wx, wz, out double lx, out double lz);
                double dx = Math.Max(0.0, Math.Abs(lx) - site.Params.Width * 0.5), dz = Math.Max(0.0, Math.Abs(lz) - site.Params.Depth * 0.5);
                gap = Math.Min(gap, Math.Sqrt(dx * dx + dz * dz));
            }
            if (gap <= 12.0)
            {
                Console.Write($"  near (corner {gap:F2} m off): ");
                Print(other);
            }
        }
    }

    private static void Print(BuildingSite s)
    {
        Console.WriteLine($"#{s.Id} centre ({s.CentreX:F2}, {s.CentreZ:F2}) axis ({s.AxisX:F4}, {s.AxisZ:F4}) {s.Params.Width:F2} x {s.Params.Depth:F2}, {s.Params.Height:F1} m tall, base {s.BaseHeight:F2}, onRoof {s.OnRoof}, sides {s.Sides}, open {s.IsOpenGround}, special {s.Params.SpecialType}, wing {s.Wing}");
        if (s.Contacts != null)
        {
            foreach (WallContact c in s.Contacts) Console.WriteLine($"    contact {c.Wall} {c.U0:F2}..{c.U1:F2} up to {c.Top:F1}");
        }
    }
}
