using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// sunnyspawn [first] [count]: seeds whose spawn junction is flat and in the sun at the game's starting time (SkyCycle.StartSeconds: the
/// sun 45 degrees up at azimuth 145). Fields-only flatness round the origin first (cheap), then the full plan for the flattest: the
/// spawn's ground spread within 100 m, and how much of the ground within 25 m of it no building shades.
/// </summary>
public static class SunnySpawn
{
    // Toward the sun at the start time, flat: Quaternion.Euler(45, 145, 0)'s forward reversed.
    private static readonly double SunX = -Math.Sin(145.0 * Math.PI / 180.0);
    private static readonly double SunZ = -Math.Cos(145.0 * Math.PI / 180.0);

    // tan(45 degrees): the shadow ray climbs a metre for each metre it runs.
    private const double RisePerMetre = 1.0;

    private const double ShadowReach = 200.0;

    public static void Run(int first, int count)
    {
        var cfg = PlanConfig.For(CityScale.Active);
        var rng = new Random(first);
        var flattest = new List<(double spread, int seed)>();
        for (int k = 0; k < count; k++)
        {
            int seed = rng.Next(10000000, 99999999);
            var fields = new PlanFields(seed, cfg);
            flattest.Add((Spread(fields, 0.0, 0.0, 300.0, 25.0), seed));
        }
        flattest.Sort((a, b) => a.spread.CompareTo(b.spread));

        var ranked = new List<(double lit, double spread, int seed, double x, double z)>();
        for (int k = 0; k < 25 && k < flattest.Count; k++)
        {
            int seed = flattest[k].seed;
            PlanWorld world = PlanWorld.Create(seed, cfg);
            world.TryFindSpawn(out double sx, out double sz);
            double spread = Spread(world.Fields, sx, sz, 100.0, 10.0);
            double lit = LitShare(world, sx, sz);
            ranked.Add((lit, spread, seed, sx, sz));
        }
        ranked.Sort((a, b) => (b.lit - b.spread * 0.05).CompareTo(a.lit - a.spread * 0.05));
        foreach (var r in ranked)
        {
            Console.WriteLine($"  seed {r.seed}: spawn ({r.x:F0}, {r.z:F0}), {r.lit:P0} of the ground within 25 m in sun, ground spread {r.spread:F1} m within 100 m");
        }
    }

    /// <summary>Highest minus lowest ground within <paramref name="radius"/> of a point, metres.</summary>
    private static double Spread(PlanFields fields, double cx, double cz, double radius, double step)
    {
        double low = double.MaxValue, high = double.MinValue;
        for (double x = -radius; x <= radius; x += step)
        {
            for (double z = -radius; z <= radius; z += step)
            {
                if (x * x + z * z > radius * radius) continue;
                double h = fields.Height(cx + x, cz + z);
                low = Math.Min(low, h);
                high = Math.Max(high, h);
            }
        }
        return high - low;
    }

    /// <summary>The share of points within 25 m of the spawn that a ray toward the sun leaves without passing under a building's roof.</summary>
    private static double LitShare(PlanWorld world, double sx, double sz)
    {
        var near = new List<BuildingSite>();
        int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);
        int reach = (int)Math.Ceiling((ShadowReach + 60.0) / PlanWorld.ChunkSize);
        for (int oy = -reach; oy <= reach; oy++)
        {
            for (int ox = -reach; ox <= reach; ox++)
            {
                foreach (int id in world.BuildingsOwnedBy(cx + ox, cy + oy))
                {
                    near.Add(world.Buildings[id]);
                }
            }
        }
        int points = 0, lit = 0;
        for (double x = -25.0; x <= 25.0; x += 5.0)
        {
            for (double z = -25.0; z <= 25.0; z += 5.0)
            {
                if (x * x + z * z > 625.0) continue;
                double px = sx + x, pz = sz + z;
                bool inside = false, shaded = false;
                double ground = world.GroundHeight(px, pz) + 1.0;
                foreach (BuildingSite b in near)
                {
                    double entry = Entry(b, px, pz, out bool within);
                    if (within) { inside = true; break; }
                    if (entry >= 0.0 && ground + entry * RisePerMetre < b.Top)
                    {
                        shaded = true;
                        break;
                    }
                }
                if (inside) continue;   // a point inside a building is not open ground
                points++;
                if (!shaded) lit++;
            }
        }
        return points == 0 ? 0.0 : (double)lit / points;
    }

    /// <summary>Metres along the flat sun ray from a point to where it enters the building's footprint rectangle; -1 if it misses.</summary>
    private static double Entry(BuildingSite b, double px, double pz, out bool within)
    {
        b.WorldToLocal(px, pz, out double ox, out double oz);
        b.WorldToLocal(px + SunX, pz + SunZ, out double tx, out double tz);
        double dx = tx - ox, dz = tz - oz;
        double hx = b.Params.Width * 0.5, hz = b.Params.Depth * 0.5;
        within = Math.Abs(ox) <= hx && Math.Abs(oz) <= hz;
        double t0 = 0.0, t1 = ShadowReach;
        if (!Slab(ox, dx, hx, ref t0, ref t1) || !Slab(oz, dz, hz, ref t0, ref t1)) return -1.0;
        return t0;
    }

    private static bool Slab(double o, double d, double half, ref double t0, ref double t1)
    {
        if (Math.Abs(d) < 1e-9) return Math.Abs(o) <= half;
        double a = (-half - o) / d, c = (half - o) / d;
        if (a > c) (a, c) = (c, a);
        t0 = Math.Max(t0, a);
        t1 = Math.Min(t1, c);
        return t0 <= t1;
    }
}
