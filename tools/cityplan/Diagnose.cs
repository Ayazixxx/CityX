using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;

/// <summary>
/// Building diagnostics with no Unity: dotnet run -c Release -- overlaps [seed]. Lists buildings whose footprints overlap over the same heights (the
/// check BuildingOverlapTests makes) and the lots they came from.
/// </summary>
public static class PlanDiagnose
{
    public static void Overlaps(int seed)
    {
        PlanWorld world = PlanWorld.For(seed);
        BuildingSite[] b = world.Buildings;
        Console.WriteLine($"seed {seed}: {b.Length} volumes, {world.Data.Lots.Count} lots");
        int bad = 0;
        for (int i = 0; i < b.Length; i++)
        {
            for (int j = i + 1; j < b.Length; j++)
            {
                BuildingSite sa = b[i], sb = b[j];
                double dx = sa.CentreX - sb.CentreX, dz = sa.CentreZ - sb.CentreZ;
                if (dx * dx + dz * dz > 300.0 * 300.0) continue;
                double vertical = Math.Min(sa.Top, sb.Top) - Math.Max(sa.PodiumBase, sb.PodiumBase);
                if (vertical < 1.0) continue;
                double depth = Sat(Corners(sa), Corners(sb));
                if (depth > 0.3)
                {
                    bad++;
                    if (bad <= 10)
                    {
                        Console.WriteLine($"#{sa.Id} {sa.Params.Width:F0}x{sa.Params.Depth:F0} wing={sa.Wing} roof={sa.OnRoof} shell={sa.Shell} lot={LotOf(world, sa)}  vs  #{sb.Id} {sb.Params.Width:F0}x{sb.Params.Depth:F0} wing={sb.Wing} roof={sb.OnRoof} lot={LotOf(world, sb)}  by {depth:F1} m");
                        foreach (BuildingSite s in new[] { sa, sb })
                        {
                            Console.WriteLine($"  #{s.Id} corners " + string.Join(" ", Array.ConvertAll(Corners(s), p => $"({p[0]:F1},{p[1]:F1})")));
                            foreach (PlanLot lot in world.Data.Lots)
                            {
                                if (Inside(lot.Footprint, new Vec2(s.CentreX, s.CentreZ)))
                                {
                                    Console.WriteLine($"    in lot {lot.Id} block {lot.BlockIndex} wall={lot.StreetWall} front={lot.FrontEdge} lot " + string.Join(" ", Array.ConvertAll(lot.Lot, p => $"({p.X:F1},{p.Y:F1})")));
                                    Console.WriteLine($"      footprint " + string.Join(" ", Array.ConvertAll(lot.Footprint, p => $"({p.X:F1},{p.Y:F1})")));
                                }
                            }
                        }
                    }
                }
            }
        }
        Console.WriteLine($"{bad} overlapping pairs");

        // Lots whose footprint contains the centre of another lot's footprint: a block that swallowed another.
        var lots = world.Data.Lots;
        int nested = 0;
        for (int i = 0; i < lots.Count; i++)
        {
            for (int j = 0; j < lots.Count; j++)
            {
                if (i == j || lots[j].Area >= lots[i].Area) continue;
                Vec2 c = Centroid(lots[j].Footprint);
                if (Inside(lots[i].Footprint, c))
                {
                    nested++;
                    if (nested <= 8)
                    {
                        Console.WriteLine($"lot {lots[i].Id} (block {lots[i].BlockIndex}, area {lots[i].Area:F0}) contains lot {lots[j].Id} (block {lots[j].BlockIndex}, area {lots[j].Area:F0})");
                    }
                    break;
                }
            }
        }
        Console.WriteLine($"{nested} lots contain another");
        foreach (PlanLot lot in lots)
        {
            if (lot.Id == 5732)
            {
                Console.WriteLine($"lot 5732 lot pts {lot.Lot.Length}: " + string.Join(" ", Array.ConvertAll(lot.Lot, p => $"({p.X:F0},{p.Y:F0})")));
                Console.WriteLine($"lot 5732 footprint: " + string.Join(" ", Array.ConvertAll(lot.Footprint, p => $"({p.X:F0},{p.Y:F0})")));
                var blk = world.Data.Blocks[lot.BlockIndex];
                Console.WriteLine($"block {lot.BlockIndex}: {blk.GetType().Name}");
            }
        }
    }

    private static Vec2 Centroid(Vec2[] p)
    {
        double x = 0, y = 0;
        foreach (Vec2 v in p) { x += v.X; y += v.Y; }
        return new Vec2(x / p.Length, y / p.Length);
    }

    private static bool Inside(Vec2[] poly, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>The buildings TestSites.NearSpawn would visit first (rings of chunks round the spawn chunk), with what they are.</summary>
    public static void NearSpawn(int seed, int count)
    {
        PlanWorld world = PlanWorld.For(seed);
        world.TryFindSpawn(out double sx, out double sz);
        int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);
        int n = 0;
        for (int r = 0; r <= 12 && n < count; r++)
        {
            for (int dy = -r; dy <= r && n < count; dy++)
            {
                for (int dx = -r; dx <= r && n < count; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    foreach (int id in world.BuildingsOwnedBy(cx + dx, cy + dy))
                    {
                        BuildingSite s = world.Buildings[id];
                        Console.WriteLine($"#{s.Id} {s.Params.Width:F1}x{s.Params.Depth:F1}x{s.Params.Height:F1} base {s.BaseHeight:F1} door {s.DoorGroundHeight:F1} sides={s.Sides} shell={s.Shell} wing={s.Wing} roof={s.OnRoof} sealed={s.RoofSealed} contacts={(s.Contacts?.Length ?? 0)} obstacle={s.HasRoofObstacle}");
                        if (++n >= count) break;
                    }
                }
            }
        }
    }

    /// <summary>The lot whose fitted rectangle contains the building's centre (the first found), with its area and rectangle size.</summary>
    private static string LotOf(PlanWorld world, BuildingSite s)
    {
        foreach (PlanLot lot in world.Data.Lots)
        {
            if (!PlanBuildings.FitRectangle(lot.Footprint, lot.FrontEdge, out Vec2 c, out Vec2 ex, out double w, out double d, lot.Scale)) continue;
            double lx = (s.CentreX - c.X) * ex.X + (s.CentreZ - c.Y) * ex.Y;
            double lz = -(s.CentreX - c.X) * ex.Y + (s.CentreZ - c.Y) * ex.X;
            if (Math.Abs(lx) <= w * 0.5 + 0.01 && Math.Abs(lz) <= d * 0.5 + 0.01)
            {
                return $"{lot.Id}(rect {w:F0}x{d:F0}, area {lot.Area:F0}, footprint pts {lot.Footprint.Length}, block {lot.BlockIndex})";
            }
        }
        return "?";
    }

    private static double[][] Corners(BuildingSite s)
    {
        var c = new double[4][];
        double hx = s.Params.Width * 0.5, hz = s.Params.Depth * 0.5;
        double[,] l = { { -hx, -hz }, { hx, -hz }, { hx, hz }, { -hx, hz } };
        for (int i = 0; i < 4; i++)
        {
            s.LocalToWorld(l[i, 0], l[i, 1], out double wx, out double wz);
            c[i] = new[] { wx, wz };
        }
        return c;
    }

    private static double Sat(double[][] a, double[][] b)
    {
        double least = double.MaxValue;
        foreach (double[][] shape in new[] { a, b })
        {
            for (int i = 0; i < 4; i++)
            {
                double ex = shape[(i + 1) % 4][0] - shape[i][0], ez = shape[(i + 1) % 4][1] - shape[i][1];
                double len = Math.Sqrt(ex * ex + ez * ez);
                double ax = -ez / len, az = ex / len;
                Project(a, ax, az, out double a0, out double a1);
                Project(b, ax, az, out double b0, out double b1);
                double o = Math.Min(a1, b1) - Math.Max(a0, b0);
                if (o <= 0.0) return 0.0;
                least = Math.Min(least, o);
            }
        }
        return least;
    }

    private static void Project(double[][] p, double ax, double az, out double min, out double max)
    {
        min = double.MaxValue;
        max = double.MinValue;
        foreach (double[] v in p)
        {
            double d = v[0] * ax + v[1] * az;
            min = Math.Min(min, d);
            max = Math.Max(max, d);
        }
    }
}
