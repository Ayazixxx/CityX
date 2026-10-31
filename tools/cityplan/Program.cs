public static class Program
{
    public static int Main(string[] args)
    {
        // dotnet run -c Release --project tools/cityplan -- [seed] [halfMetres] [pixels] [out.png] [centreX centreZ viewHalf]
        if (args.Length > 0 && args[0] == "site")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 81565117);
            var s = w.Buildings[int.Parse(args[1])];
            System.Console.WriteLine($"#{s.Id} {s.Params.Width:F1}x{s.Params.Depth:F1}x{s.Params.Height:F1} mass={s.Params.Mass} sides={s.Sides} shell={s.Shell} wing={s.Wing} roof={s.OnRoof} sealed={s.RoofSealed} contacts={(s.Contacts?.Length ?? 0)} obstacle={s.HasRoofObstacle} basement={s.Params.HasBasement}");
            System.Console.WriteLine($"  centre ({s.CentreX:F1},{s.CentreZ:F1}) axis ({s.AxisX:F3},{s.AxisZ:F3}) base {s.BaseHeight:F2}");
            if (s.Contacts != null) foreach (var c in s.Contacts) System.Console.WriteLine($"  contact {c.Wall} {c.U0:F2}..{c.U1:F2} top {c.Top:F1}");
            foreach (var o in w.Buildings)
            {
                double dx = o.CentreX - s.CentreX, dz = o.CentreZ - s.CentreZ;
                if (o.Id == s.Id || dx * dx + dz * dz > 80.0 * 80.0) continue;
                System.Console.WriteLine($"  near #{o.Id} {o.Params.Width:F1}x{o.Params.Depth:F1} centre ({o.CentreX:F1},{o.CentreZ:F1}) axis ({o.AxisX:F3},{o.AxisZ:F3}) roof={o.OnRoof} wing={o.Wing} shell={o.Shell} sides={o.Sides}");
            }
            return 0;
        }
        // garagemouths [seed]: every garage drive's mouth against the road's edge across the opening: how far the drawn (twisted) mouth stands off it.
        if (args.Length > 0 && args[0] == "garagemouths")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int drives = 0, off5 = 0, off10 = 0, off20 = 0, fall1 = 0, fall2 = 0;
            var worst = new System.Collections.Generic.List<(float off, string what)>();
            foreach (var s in w.Buildings)
            {
                if (!s.Exists || !s.IsParkingGarage) continue;
                foreach (CityX.World.GarageEntrance e in CityX.World.GarageEntrances.For(w, s))
                {
                    if (!e.HasDriveway) continue;
                    drives++;
                    float off = 0.0f;
                    for (float x = e.X0 + 0.3f; x <= e.X1 - 0.29f; x += 0.5f)
                    {
                        s.LocalToWorld(x, s.Params.Depth * 0.5 + e.KerbOut, out double gx, out double gz);
                        float road = (float)(w.GroundHeight(gx, gz) + CityX.World.GarageEntrances.RoadLift);
                        float drawn = CityX.World.GarageEntrances.DrawnHeight(e, -e.KerbOut) + CityX.World.GarageEntrances.UnderRoad;   // a mouth level across
                        off = System.Math.Max(off, System.Math.Abs(drawn - road));
                    }
                    s.LocalToWorld(e.X0, s.Params.Depth * 0.5 + e.KerbOut, out double lx0, out double lz0);
                    s.LocalToWorld(e.X1, s.Params.Depth * 0.5 + e.KerbOut, out double lx1, out double lz1);
                    float fall = (float)System.Math.Abs(w.GroundHeight(lx1, lz1) - w.GroundHeight(lx0, lz0));
                    if (off > 0.05f) off5++;
                    if (off > 0.10f) off10++;
                    if (off > 0.20f) off20++;
                    if (fall > 1.0f) fall1++;
                    if (fall > 2.0f) fall2++;
                    worst.Add((off, $"garage {s.Id} opening {e.X0:F0}: mouth off by {off:F2}, road falls {fall:F2} m across, kerb {e.KerbOut:F1}, road {e.RoadY:F2} deck {e.DeckY:F2}"));
                }
            }
            worst.Sort((a, b) => b.off.CompareTo(a.off));
            System.Console.WriteLine($"{drives} drives: mouth off the road's edge by > 5 cm {off5}, > 10 cm {off10}, > 20 cm {off20}; road falling > 1 m across the opening {fall1}, > 2 m {fall2}");
            for (int i = 0; i < System.Math.Min(12, worst.Count); i++) System.Console.WriteLine("  " + worst[i].what);
            // Against the road as DRAWN (CitySurfaceBuilder.BuildRoad: 5 m pieces, each a quad on the ground at its corners, so its kerb edge is
            // straight between them; the trimmed ends are junction plates): a level mouth, and the ground function, against it (the game draws the mouth on the road as drawn: CitySurfaceBuilder.DriveHeight).
            int onPlate = 0, drawnOff5 = 0, drawnOff10 = 0, groundOff5 = 0, groundOff10 = 0;
            float worstDrawn = 0.0f, worstGround = 0.0f;
            foreach (var s in w.Buildings)
            {
                if (!s.Exists || !s.IsParkingGarage) continue;
                float front = s.Params.Depth * 0.5f;
                foreach (CityX.World.GarageEntrance e in CityX.World.GarageEntrances.For(w, s))
                {
                    if (!e.HasDriveway) continue;
                    CityX.World.GarageEntrances.CarriagewayOut(w, s, (e.X0 + e.X1) * 0.5f, out int road);
                    if (road < 0) continue;
                    CityX.World.Plan.EdgeGeometry g = w.Geometry.Edges[road];
                    double t0 = g.TrimA, t1 = g.Length - g.TrimB;
                    int pieces = System.Math.Max(1, (int)System.Math.Ceiling((t1 - t0) / 5.0));
                    float offDrawn = 0.0f, offGround = 0.0f;
                    bool plate = false;
                    for (float x = e.X0 + 0.3f; x <= e.X1 - 0.29f; x += 0.5f)
                    {
                        s.LocalToWorld(x, front + e.KerbOut, out double px, out double pz);
                        var p = new CityX.World.Plan.Vec2(px, pz);
                        double t = CityX.World.Plan.Vec2.Dot(p - g.A, g.Dir);
                        if (t < t0 || t > t1) { plate = true; continue; }
                        double side = CityX.World.Plan.Vec2.Dot(p - g.A, g.Dir.Perp) >= 0.0 ? 1.0 : -1.0;
                        int i = System.Math.Min(pieces - 1, (int)((t - t0) / (t1 - t0) * pieces));
                        double ta = t0 + (t1 - t0) * i / pieces, tb = t0 + (t1 - t0) * (i + 1) / pieces;
                        var ca = g.A + g.Dir * ta + g.Dir.Perp * (g.HalfWidth * side);
                        var cb = g.A + g.Dir * tb + g.Dir.Perp * (g.HalfWidth * side);
                        double ha = w.GroundHeight(ca.X, ca.Y), hb = w.GroundHeight(cb.X, cb.Y);
                        float drawn = (float)(ha + (hb - ha) * (t - ta) / (tb - ta) + CityX.World.GarageEntrances.RoadLift);
                        float mouth = CityX.World.GarageEntrances.DrawnHeight(e, -e.KerbOut) + CityX.World.GarageEntrances.UnderRoad;   // a mouth level across
                        float ground = (float)(w.GroundHeight(px, pz) + CityX.World.GarageEntrances.RoadLift);
                        offDrawn = System.Math.Max(offDrawn, System.Math.Abs(mouth - drawn));
                        offGround = System.Math.Max(offGround, System.Math.Abs(ground - drawn));
                    }
                    if (plate) onPlate++;
                    if (offDrawn > 0.05f) drawnOff5++;
                    if (offDrawn > 0.10f) drawnOff10++;
                    if (offGround > 0.05f) groundOff5++;
                    if (offGround > 0.10f) groundOff10++;
                    worstDrawn = System.Math.Max(worstDrawn, offDrawn);
                    worstGround = System.Math.Max(worstGround, offGround);
                }
            }
            System.Console.WriteLine($"against the road as drawn: {onPlate} mouths reach a junction plate; a level mouth off it by > 5 cm {drawnOff5}, > 10 cm {drawnOff10} (worst {worstDrawn:F2}); " +
                $"the ground function off it by > 5 cm {groundOff5}, > 10 cm {groundOff10} (worst {worstGround:F2})");
            return 0;
        }
        // garage <id> [seed]: one garage's two entrances (kerb distance, road and deck heights, the road's slope across the mouth).
        if (args.Length > 1 && args[0] == "garage")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 81565117);
            var s = w.Buildings[int.Parse(args[1])];
            foreach (CityX.World.GarageEntrance e in CityX.World.GarageEntrances.For(w, s))
            {
                System.Console.WriteLine($"  opening {e.X0:F1}..{e.X1:F1}: kerb {e.KerbOut:F2} m out, road {e.RoadY:F2}, deck {e.DeckY:F2}, ramp end {e.RampEnd:F2}");
                if (!e.HasDriveway) continue;
                var line = new System.Text.StringBuilder("    the ground along the kerb line (x: height, carriageway out):");
                for (float x = e.X0 - 1.0f; x <= e.X1 + 1.01f; x += 0.5f)
                {
                    s.LocalToWorld(x, s.Params.Depth * 0.5 + e.KerbOut, out double gx, out double gz);
                    line.Append($" {x:F1}: {w.GroundHeight(gx, gz):F2}/{CityX.World.GarageEntrances.CarriagewayOut(w, s, x):F1}");
                }
                System.Console.WriteLine(line);
                // The road as drawn (5 m pieces, straight along each kerb between their corners) along the kerb line, and along the opening's middle
                // out over the road (the way the car comes in).
                CityX.World.GarageEntrances.CarriagewayOut(w, s, (e.X0 + e.X1) * 0.5f, out int road);
                if (road < 0) continue;
                CityX.World.Plan.EdgeGeometry g = w.Geometry.Edges[road];
                double t0 = g.TrimA, t1 = g.Length - g.TrimB;
                int pieces = System.Math.Max(1, (int)System.Math.Ceiling((t1 - t0) / 5.0));
                double Drawn(double px, double pz)
                {
                    var p = new CityX.World.Plan.Vec2(px, pz);
                    double t = System.Math.Max(t0, System.Math.Min(t1, CityX.World.Plan.Vec2.Dot(p - g.A, g.Dir)));
                    double side = CityX.World.Plan.Vec2.Dot(p - g.A, g.Dir.Perp) >= 0.0 ? 1.0 : -1.0;
                    int i = System.Math.Min(pieces - 1, (int)((t - t0) / (t1 - t0) * pieces));
                    double ta = t0 + (t1 - t0) * i / pieces, tb = t0 + (t1 - t0) * (i + 1) / pieces;
                    var ca = g.A + g.Dir * ta + g.Dir.Perp * (g.HalfWidth * side);
                    var cb = g.A + g.Dir * tb + g.Dir.Perp * (g.HalfWidth * side);
                    double ha = w.GroundHeight(ca.X, ca.Y), hb = w.GroundHeight(cb.X, cb.Y);
                    return ha + (hb - ha) * (t - ta) / (tb - ta) + CityX.World.GarageEntrances.RoadLift;
                }
                var drawnLine = new System.Text.StringBuilder($"    road {road} ({g.HalfWidth * 2.0:F1} m wide), as drawn along the kerb line:");
                for (float x = e.X0; x <= e.X1 + 0.01f; x += 1.0f)
                {
                    s.LocalToWorld(x, s.Params.Depth * 0.5 + e.KerbOut, out double gx, out double gz);
                    drawnLine.Append($" {x:F1}: {Drawn(gx, gz):F2}");
                }
                System.Console.WriteLine(drawnLine);
                var outLine = new System.Text.StringBuilder("    along the middle, out from the wall (out: ground / kerb-edge road as drawn):");
                for (float o = 0.0f; o <= e.KerbOut + 6.01f; o += 1.0f)
                {
                    s.LocalToWorld((e.X0 + e.X1) * 0.5, s.Params.Depth * 0.5 + o, out double gx, out double gz);
                    outLine.Append($" {o:F0}: {w.GroundHeight(gx, gz) + CityX.World.GarageEntrances.RoadLift:F2}/{Drawn(gx, gz):F2}");
                }
                System.Console.WriteLine(outLine);
            }
            return 0;
        }
        // garages [seed]: every parking garage: its car deck over the street in front of its entrances (the rise a car must climb), and the road
        // nearest that point (how far out of its carriageway).
        if (args.Length > 0 && args[0] == "garages")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int count = 0, raised = 0, noRoad = 0;
            foreach (var s in w.Buildings)
            {
                if (!s.IsParkingGarage) continue;
                count++;
                double worst = double.MinValue, nearest = double.MaxValue;
                foreach (double lx in new[] { -6.5, 6.5 })
                {
                    s.LocalToWorld(lx, s.Params.Depth * 0.5 + 1.5, out double fx, out double fz);
                    worst = System.Math.Max(worst, s.BaseHeight - w.GroundHeight(fx, fz));
                    s.LocalToWorld(lx, s.Params.Depth * 0.5, out double ex, out double ez);
                    var p = new CityX.World.Plan.Vec2(ex, ez);
                    for (int cy = CityX.World.Plan.PlanWorld.ChunkOf(ez) - 1; cy <= CityX.World.Plan.PlanWorld.ChunkOf(ez) + 1; cy++)
                    {
                        for (int cx = CityX.World.Plan.PlanWorld.ChunkOf(ex) - 1; cx <= CityX.World.Plan.PlanWorld.ChunkOf(ex) + 1; cx++)
                        {
                            foreach (int e in w.EdgesIn(cx, cy))
                            {
                                CityX.World.Plan.EdgeGeometry g = w.Geometry.Edges[e];
                                if (g == null) continue;
                                nearest = System.Math.Min(nearest, CityX.World.Plan.PlanMath.DistanceToSegment(p, g.A, g.B, out _) - g.HalfWidth);
                            }
                        }
                    }
                }
                if (worst > 0.3) raised++;
                if (nearest > 8.0) noRoad++;
                if (worst > 0.3 || nearest > 8.0) System.Console.WriteLine($"  garage {s.Id} {s.Params.Width:F0}x{s.Params.Depth:F0}x{s.Params.Height:F0}: deck {worst:F2} m over the ground in front, nearest carriageway {nearest:F1} m from the front wall, base {s.BaseHeight:F2} door ground {s.DoorGroundHeight:F2} podium {s.PodiumBase:F2}");
            }
            int below = 0, flush = 0, upTo1 = 0, upTo2 = 0, over2 = 0;
            foreach (var s in w.Buildings) { if (!s.IsParkingGarage) continue; double r = s.BaseHeight - s.DoorGroundHeight; if (r < -0.05) below++; else if (r < 0.2) flush++; else if (r < 1.0) upTo1++; else if (r < 2.0) upTo2++; else over2++; }
            System.Console.WriteLine($"rise of the deck over the paving at the door: below {below}, flush {flush}, 0.2-1 m {upTo1}, 1-2 m {upTo2}, over 2 m {over2}");
            // The entrance ramps (GarageEntrances): how long and how steep.
            int ramps = 0, steep = 0, steepest = 0;
            double maxGrade = 0.0, maxLength = 0.0;
            foreach (var s in w.Buildings)
            {
                foreach (CityX.World.GarageEntrance e in CityX.World.GarageEntrances.For(w, s))
                {
                    if (!e.HasRamp) continue;
                    ramps++;
                    double grade = e.Rise / e.Length;
                    if (grade > CityX.World.GarageEntrances.Grade + 1e-3) steep++;
                    if (grade > 0.25) steepest++;
                    maxGrade = System.Math.Max(maxGrade, grade);
                    maxLength = System.Math.Max(maxLength, e.Length);
                    if (grade > 0.2 || e.Length > 30.0) System.Console.WriteLine($"  garage {s.Id} {s.Params.Width:F0}x{s.Params.Depth:F0}: opening {e.X0:F0}..{e.X1:F0} rise {e.Rise:F2} m over {e.Length:F1} m ({grade:P0}); deck {e.DeckY:F2} street {e.StreetY:F2} door ground {s.DoorGroundHeight:F2} podium {s.PodiumBase:F2}");
                }
            }
            System.Console.WriteLine($"entrance ramps: {ramps}; steeper than {CityX.World.GarageEntrances.Grade:P0} (garage too shallow): {steep}, over 25 %: {steepest}; steepest {maxGrade:P0}, longest {maxLength:F1} m");
            System.Console.WriteLine($"{count} garages; {raised} with the deck over 0.3 m above the ground in front; {noRoad} with no road within 8 m of the front");
            return 0;
        }
        // openwalk [seed] [block]: an open block's layout and why OpenSpace.Walkable fails it: each kerb edge's door point, whether it is free,
        // and the pieces that block it or the edges it is too near.
        // near [seed] [x] [z]: what of the stairs, steps and step streets lies within 6 m of a point, and which blocks are listed for its chunk.
        if (args.Length > 0 && args[0] == "near")
        {
            var w = CityX.World.Plan.PlanWorld.For(int.Parse(args[1]));
            var p = new CityX.World.Plan.Vec2(double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture), double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            int pcx = CityX.World.Plan.PlanWorld.ChunkOf(p.X), pcy = CityX.World.Plan.PlanWorld.ChunkOf(p.Y);
            System.Console.WriteLine($"point {p} in chunk ({pcx},{pcy}); ground {w.GroundHeight(p.X, p.Y):F2}");
            for (int cy = pcy - 1; cy <= pcy + 1; cy++)
                for (int cx = pcx - 1; cx <= pcx + 1; cx++)
                    System.Console.WriteLine($"  chunk ({cx},{cy}) blocks: {string.Join(",", w.BlocksIn(cx, cy))}");
            for (int b = 0; b < w.Data.Blocks.Count; b++)
            {
                var block = w.Data.Blocks[b];
                if (block.Inner == null || block.Outer == null) continue;
                if (block.StairLane >= 0)
                {
                    var corridor = CityX.World.Plan.StairLanes.Corridor(w.Data.StairLanes[block.StairLane]);
                    double d = double.MaxValue;
                    for (int i = 0; i < corridor.Length; i++) d = System.Math.Min(d, CityX.World.Plan.PlanMath.DistanceToSegment(p, corridor[i], corridor[(i + 1) % corridor.Length], out _));
                    if (d < 6.0) System.Console.WriteLine($"  stairway {block.StairLane} (block {b}) at {d:F2} m: {string.Join(" ", corridor)}");
                }
            }
            foreach (var g in w.Geometry.Edges)
            {
                if (g == null) continue;
                double d = CityX.World.Plan.PlanMath.DistanceToSegment(p, g.A, g.B, out _) - g.HalfWidth;
                if (d < 6.0) System.Console.WriteLine($"  road {g.Edge.Id} {g.Edge.Class} at {d:F2} m from its edge; step street {CityX.World.StepStreets.Is(w, g.Edge)}; pedestrian {CityX.World.PedestrianStreets.Is(w, g.Edge)}");
            }
            return 0;
        }
        // stairdepth [seed]: how far the stairways' and step streets' treads sink under (or stand over) the surface they follow, the worst of each.
        if (args.Length > 0 && args[0] == "stairdepth")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            double laneSink = 0.0, laneSide = 0.0;
            int laneDeep = 0, laneSideDeep = 0;
            foreach (CityX.World.Plan.StairLane lane in w.Data.StairLanes)
            {
                var treads = CityX.World.Plan.StairLanes.Profile(lane, w.Fields);
                double worst = 0.0, side = 0.0;
                foreach (var t in treads)
                {
                    double s = (t.S0 + t.S1) * 0.5;
                    var p = lane.A + lane.Dir * s;
                    worst = System.Math.Max(worst, w.GroundHeight(p.X, p.Y) + 0.23 - t.Top);
                    foreach (double across in new[] { -lane.Width * 0.5, lane.Width * 0.5 })
                    {
                        var q = p + lane.Dir.Perp * across;
                        side = System.Math.Max(side, w.GroundHeight(q.X, q.Y) + 0.23 - t.Top);
                    }
                }
                laneSink = System.Math.Max(laneSink, worst);
                laneSide = System.Math.Max(laneSide, side);
                if (worst > 0.6) laneDeep++;
                if (side > 0.6) laneSideDeep++;
            }
            System.Console.WriteLine($"stairways {w.Data.StairLanes.Count}: the middle sinks up to {laneSink:F2} m ({laneDeep} over 0.6 m); a side's paving stands up to {laneSide:F2} m over the treads ({laneSideDeep} over 0.6 m)");
            double streetSink = 0.0, streetOver = 0.0;
            int streetDeep = 0, streets = 0;
            foreach (var g in w.Geometry.Edges)
            {
                if (g == null || !CityX.World.StepStreets.Is(w, g.Edge)) continue;
                streets++;
                var treads = CityX.World.StepStreets.Profile(w, g, out bool fromB);
                double t0 = g.TrimA, t1 = g.Length - g.TrimB;
                var dir = fromB ? -g.Dir : g.Dir;
                var low = g.A + g.Dir * (fromB ? t1 : t0);
                double worst = 0.0;
                foreach (var t in treads)
                {
                    var p = low + dir * ((t.S0 + t.S1) * 0.5);
                    double road = w.GroundHeight(p.X, p.Y) + 0.07;
                    worst = System.Math.Max(worst, road - t.Top);
                    streetOver = System.Math.Max(streetOver, t.Top - road);
                }
                streetSink = System.Math.Max(streetSink, worst);
                if (worst > 0.6) streetDeep++;
            }
            System.Console.WriteLine($"step streets {streets}: the treads sink up to {streetSink:F2} m under the road ({streetDeep} over 0.6 m), stand up to {streetOver:F2} m over it");
            return 0;
        }
        // stepstreet [seed] [edge]: a step street's carriageway between its trims, the road's height along it, and its treads.
        if (args.Length > 0 && args[0] == "stepstreet")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            var g = w.Geometry.Edges[int.Parse(args[2])];
            double t0 = g.TrimA, t1 = g.Length - g.TrimB;
            System.Console.WriteLine($"edge {g.Edge.Id} {g.Edge.Class}: {g.Length:F1} m, trims {g.TrimA:F1}/{g.TrimB:F1}, carriageway {t1 - t0:F1} m; step street {CityX.World.StepStreets.Is(w, g.Edge)}");
            for (double t = t0; t <= t1 + 1e-6; t += (t1 - t0) / 10.0)
            {
                var p = g.A + g.Dir * t;
                System.Console.WriteLine($"  {t - t0,5:F1} m: road {w.GroundHeight(p.X, p.Y) + 0.07:F2}");
            }
            var treads = CityX.World.StepStreets.Profile(w, g, out bool fromB);
            System.Console.WriteLine($"  from {(fromB ? "B" : "A")}: {treads.Count} treads");
            foreach (var t in treads) System.Console.Write($" [{t.S0:F2}-{t.S1:F2} {t.Top:F2}]");
            System.Console.WriteLine();
            return 0;
        }
        if (args.Length > 0 && args[0] == "openwalkall")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int all = 0, shut = 0;
            foreach (int index in w.Data.PlazaBlocks)
            {
                var block = w.Data.Blocks[index];
                if (block.Inner == null) continue;
                all++;
                if (!CityX.World.OpenSpace.Walkable(CityX.World.OpenSpace.LayoutFor(w, index), block.Inner, 0.45, out double share))
                {
                    shut++;
                    System.Console.WriteLine($"  block {index} shut: {share:P1} reached");
                }
            }
            System.Console.WriteLine($"{shut} of {all} open blocks cannot be crossed");
            return 0;
        }
        if (args.Length > 0 && args[0] == "openwalk")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int index = int.Parse(args[2]);
            var block = w.Data.Blocks[index];
            var layout = CityX.World.OpenSpace.LayoutFor(w, index);
            bool ok = CityX.World.OpenSpace.Walkable(layout, block.Inner, 0.45, out double share);
            System.Console.WriteLine($"block {index}: {layout.Role}, {layout.Pieces.Count} pieces, front edge {layout.Front}; walkable {ok}, {share:P1} reached");
            var ring = block.Inner;
            int n = ring.Count;
            for (int e = 0; e < n; e++)
            {
                var a = ring[e];
                var c = ring[(e + 1) % n];
                double len = (c - a).Length;
                var door = (a + c) * 0.5 + (c - a).Normalized.Perp * (0.45 + 0.6);
                bool inside = CityX.World.OpenSpace.Inside(ring, door, 0.45);
                var blockers = new System.Collections.Generic.List<string>();
                foreach (var p in layout.Pieces) if (p.Blocks && p.Distance(door) < 0.45) blockers.Add($"{p.Kind} at {p.At}");
                System.Console.WriteLine($"  edge {e}: {len:F1} m from {a} to {c}; door {door} inside-with-margin {inside}; blocked by {string.Join(", ", blockers)}");
            }
            foreach (var p in layout.Pieces) System.Console.WriteLine($"  piece {p.Kind} at {p.At} half {p.HalfX:F2}x{p.HalfZ:F2} blocks {p.Blocks}");
            return 0;
        }
        // hillseeds [first] [count]: seeds whose ground round the origin (where the spawn's downtown is) is most on the hills, quickly from the
        // fields alone, the best ten listed: for choosing a new world seed where the hills and their stairs are seen from the spawn.
        if (args.Length > 0 && args[0] == "hillseeds")
        {
            int first = args.Length > 1 ? int.Parse(args[1]) : 10000000;
            int count = args.Length > 2 ? int.Parse(args[2]) : 2000;
            var cfg = CityX.World.Plan.PlanConfig.For(CityX.World.Plan.CityScale.Active);
            var ranked = new System.Collections.Generic.List<(double share, double near, int seed)>();
            var rng = new System.Random(first);
            for (int k = 0; k < count; k++)
            {
                int seed = rng.Next(10000000, 99999999);
                var fields = new CityX.World.Plan.PlanFields(seed, cfg);
                int land = 0, hilly = 0, nearLand = 0, nearHilly = 0;
                for (double gx = -1200.0; gx <= 1200.0; gx += 100.0)
                {
                    for (double gz = -1200.0; gz <= 1200.0; gz += 100.0)
                    {
                        if (fields.IsWater(gx, gz)) continue;
                        bool on = fields.Hillock(gx, gz) > 2.0;
                        land++;
                        if (on) hilly++;
                        if (gx * gx + gz * gz < 500.0 * 500.0)
                        {
                            nearLand++;
                            if (on) nearHilly++;
                        }
                    }
                }
                ranked.Add(((double)hilly / System.Math.Max(1, land), (double)nearHilly / System.Math.Max(1, nearLand), seed));
            }
            ranked.Sort((p, q) => (q.share + q.near).CompareTo(p.share + p.near));
            for (int k = 0; k < 10 && k < ranked.Count; k++)
            {
                System.Console.WriteLine($"  seed {ranked[k].seed}: {ranked[k].share:P0} of the land within 1.2 km of the origin on a hill, {ranked[k].near:P0} within 500 m");
            }
            return 0;
        }
        // hills [seed] [amplitude] [wavelength]: how steep the city is with these hills (the scale's own when left out): the streets' grades along
        // their length, the sidewalks' along the kerb, and how far each building's floor stands over the lowest ground round it (the bare wall it
        // shows downhill).
        if (args.Length > 0 && args[0] == "hills")
        {
            int seed = args.Length > 1 ? int.Parse(args[1]) : 81565117;
            var cfg = CityX.World.Plan.PlanConfig.For(CityX.World.Plan.CityScale.Active);
            if (args.Length > 2) cfg.HillAmplitude = double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
            if (args.Length > 3) cfg.HillWavelength = double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
            if (args.Length > 4) cfg.HillockAmplitude = double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture);
            if (args.Length > 5) cfg.HillockWavelength = double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);
            if (args.Length > 6) cfg.HillDistrictWavelength = double.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var w = CityX.World.Plan.PlanWorld.Create(seed, cfg);
            System.Console.WriteLine($"{CityX.World.Plan.CityScale.Active.Name}: amplitude {cfg.HillAmplitude:F1} m, wavelength {cfg.HillWavelength:F0} m, hills {cfg.HillockAmplitude:F0} m every {cfg.HillockWavelength:F0} m in districts {cfg.HillDistrictWavelength:F0} m; planned in {clock.ElapsedMilliseconds} ms, {w.Data.Lots.Count} lots, {w.Buildings.Length} buildings");
            double Q(System.Collections.Generic.List<double> l, double q) => l.Count == 0 ? 0.0 : l[System.Math.Min(l.Count - 1, (int)(q * l.Count))];
            var grades = new System.Collections.Generic.List<double>();
            double over16 = 0.0, over21 = 0.0, allLength = 0.0;
            foreach (var e in w.Data.Edges)
            {
                if (!e.Alive || e.Length < 10.0) continue;
                double g = System.Math.Abs(w.GroundHeight(e.B.P.X, e.B.P.Y) - w.GroundHeight(e.A.P.X, e.A.P.Y)) / e.Length;
                grades.Add(g);
                allLength += e.Length;
                if (g > 0.16) over16 += e.Length;
                if (g > 0.21) over21 += e.Length;
            }
            grades.Sort();
            System.Console.WriteLine($"  streets {grades.Count}: grade median {Q(grades, 0.5):P1}, 90% {Q(grades, 0.9):P1}, 99% {Q(grades, 0.99):P1}, steepest {Q(grades, 1.0):P1}; " +
                $"of the length over 16% {over16 / System.Math.Max(1.0, allLength):P1}, over 21% (a car stalls) {over21 / System.Math.Max(1.0, allLength):P1}");
            int all = 0;
            var bands = new int[6];
            foreach (CityX.World.Plan.PlanBlock block in w.Data.Blocks)
            {
                if (block.Inner == null || block.Outer == null) continue;
                for (int e = 0; e < block.Inner.Count; e++)
                {
                    CityX.World.Plan.Vec2 i0 = block.Inner[e], i1 = block.Inner[(e + 1) % block.Inner.Count];
                    double length = (i1 - i0).Length;
                    if (length < 6.0) continue;
                    all++;
                    double grade = System.Math.Abs(w.GroundHeight(i1.X, i1.Y) - w.GroundHeight(i0.X, i0.Y)) / length;
                    bands[System.Math.Min(5, (int)(grade / 0.07))]++;
                }
            }
            System.Console.WriteLine($"  sidewalks {all}: 0-7% {bands[0]}, 7-14% {bands[1]}, 14-21% {bands[2]}, 21-28% {bands[3]}, 28-35% {bands[4]}, 35%+ {bands[5]}");
            double lowest = double.MaxValue, highest = double.MinValue;
            int land = 0, hilly = 0;
            for (double gx = -cfg.Half; gx <= cfg.Half; gx += 50.0)
            {
                for (double gz = -cfg.Half; gz <= cfg.Half; gz += 50.0)
                {
                    if (w.Fields.IsWater(gx, gz)) continue;
                    land++;
                    double gh = w.GroundHeight(gx, gz);
                    lowest = System.Math.Min(lowest, gh);
                    highest = System.Math.Max(highest, gh);
                    if (w.Fields.Hillock(gx, gz) > 2.0) hilly++;
                }
            }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            double sum = 0.0;
            for (int i = 0; i < 1000000; i++) sum += w.Fields.Height((i * 1.37) % cfg.Half, (i * 0.71) % cfg.Half);
            System.Console.WriteLine($"  ground {lowest:F0} to {highest:F0} m; {(double)hilly / System.Math.Max(1, land):P0} of the land on a hill; " +
                $"a million heights in {timer.ElapsedMilliseconds} ms ({sum % 1.0:F2})");
            var exposed = new System.Collections.Generic.List<double>();
            foreach (var s in w.Buildings)
            {
                if (s.OnRoof || s.Wing) continue;
                double low = double.MaxValue;
                foreach (double fx in new[] { -0.5, 0.0, 0.5 })
                {
                    foreach (double fz in new[] { -0.5, 0.0, 0.5 })
                    {
                        if (fx == 0.0 && fz == 0.0) continue;
                        s.LocalToWorld(fx * s.Params.Width, fz * s.Params.Depth, out double x, out double z);
                        low = System.Math.Min(low, w.GroundHeight(x, z));
                    }
                }
                exposed.Add(s.BaseHeight - low);
            }
            exposed.Sort();
            int tall = 0;
            foreach (double x in exposed) if (x > 6.0) tall++;
            System.Console.WriteLine($"  buildings' floor over the lowest ground round them: median {Q(exposed, 0.5):F1} m, 90% {Q(exposed, 0.9):F1} m, 99% {Q(exposed, 0.99):F1} m, " +
                $"most {Q(exposed, 1.0):F1} m; over 6 m: {tall} ({(double)tall / System.Math.Max(1, exposed.Count):P1})");
            return 0;
        }
        // steepsidewalks [seed] [grade]: the blocks' sidewalks (kerb to lot line, along each road) whose ground climbs at least the grade along the
        // kerb: how many and how long, by steepness band.
        if (args.Length > 0 && args[0] == "steepsidewalks")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            double least = args.Length > 2 ? double.Parse(args[2]) : 0.14;
            int all = 0, steep = 0;
            double steepLength = 0.0, steepest = 0.0;
            var bands = new int[6];
            foreach (CityX.World.Plan.PlanBlock block in w.Data.Blocks)
            {
                if (block.Inner == null || block.Outer == null) continue;
                for (int e = 0; e < block.Inner.Count; e++)
                {
                    CityX.World.Plan.Vec2 i0 = block.Inner[e], i1 = block.Inner[(e + 1) % block.Inner.Count];
                    double length = (i1 - i0).Length;
                    if (length < 6.0) continue;
                    all++;
                    double grade = System.Math.Abs(w.GroundHeight(i1.X, i1.Y) - w.GroundHeight(i0.X, i0.Y)) / length;
                    bands[System.Math.Min(5, (int)(grade / 0.05))]++;
                    steepest = System.Math.Max(steepest, grade);
                    if (grade < least) continue;
                    steep++;
                    steepLength += length;
                }
            }
            System.Console.WriteLine($"{all} sidewalks of 6 m or more; {steep} climb {least:P0} or more ({steepLength:F0} m of them); the steepest {steepest:P0}");
            System.Console.WriteLine($"  by grade: 0-5% {bands[0]}, 5-10% {bands[1]}, 10-15% {bands[2]}, 15-20% {bands[3]}, 20-25% {bands[4]}, 25%+ {bands[5]}");
            return 0;
        }
        // stairlanes [seed]: the stairways through steep blocks (World/Plan/StairLanes): how many, how long and how high, how many steps, and how many
        // steep blocks there were to choose from (a drop of StairLanes.MinDrop or more across them).
        // doorrise [seed]: how far each building's floor stands over the paving at its front door (the entrance stairs' rise), by band, and how
        // many would need a flight longer than half their front wall.
        if (args.Length > 0 && args[0] == "doorrise")
        {
            int doorSeed = args.Length > 1 ? int.Parse(args[1]) : 81565117;
            var doorCfg = CityX.World.Plan.PlanConfig.For(CityX.World.Plan.CityScale.Active);
            if (args.Length > 2) doorCfg.HillockAmplitude = double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
            var w = CityX.World.Plan.PlanWorld.Create(doorSeed, doorCfg);
            int all = 0, over1 = 0, over3 = 0, over6 = 0, over10 = 0, tooLong = 0;
            double worst = 0.0;
            foreach (var s in w.Buildings)
            {
                if (s.OnRoof || s.Wing || s.IsOpenGround || !s.Exists) continue;
                all++;
                double rise = s.BaseHeight - s.DoorGroundHeight;
                if (rise > 1.0) over1++;
                if (rise > 3.0) over3++;
                if (rise > 6.0) over6++;
                if (rise > 10.0) over10++;
                worst = System.Math.Max(worst, rise);
                double run = System.Math.Ceiling(rise / 0.22) * 0.32 + 2.0;
                if (run > s.Params.Width * 0.5) tooLong++;
            }
            System.Console.WriteLine($"{all} buildings: door over the paving by more than 1 m {over1}, 3 m {over3}, 6 m {over6}, 10 m {over10}; the most {worst:F1} m; " +
                $"{tooLong} whose flight would run past the end of their front wall");
            return 0;
        }
        // stairlane [seed] [id]: one stairway: its ends, its block, its first and last treads, and the buildings within 6 m of its low end.
        if (args.Length > 0 && args[0] == "stairlane")
        {
            var w = CityX.World.Plan.PlanWorld.For(int.Parse(args[1]));
            var lane = w.Data.StairLanes[int.Parse(args[2])];
            var treads = CityX.World.Plan.StairLanes.Profile(lane, w.Fields);
            System.Console.WriteLine($"stairway {lane.Id} block {lane.Block}: A {lane.A} B {lane.B}, {lane.Length:F1} m, {lane.Drop:F1} m up, dir {lane.Dir}");
            for (int i = 0; i < treads.Count && i < 12; i++) System.Console.Write($" [{treads[i].S0:F2}-{treads[i].S1:F2} {treads[i].Top:F2}]");
            System.Console.WriteLine();
            for (double s = 0.0; s <= 3.0; s += 0.5)
            {
                foreach (double across in new[] { -2.2, -1.2, 0.0, 1.2, 2.2 })
                {
                    var p = lane.A + lane.Dir * s + lane.Dir.Perp * across;
                    System.Console.Write($" s{s:F1}/{across:F1}: g{w.GroundHeight(p.X, p.Y) + 0.23:F2} b{w.BuildingAt(p.X, p.Y)}");
                }
                System.Console.WriteLine();
            }
            foreach (var site in w.Buildings)
            {
                double d = System.Math.Sqrt((site.CentreX - lane.A.X) * (site.CentreX - lane.A.X) + (site.CentreZ - lane.A.Y) * (site.CentreZ - lane.A.Y));
                if (d < System.Math.Max(site.Params.Width, site.Params.Depth) * 0.6 + 8.0)
                    System.Console.WriteLine($"  near #{site.Id} {site.Params.Width:F1}x{site.Params.Depth:F1} centre ({site.CentreX:F1},{site.CentreZ:F1}) base {site.BaseHeight:F2} garage {site.IsParkingGarage} open {site.IsOpenGround}");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "stairlanes")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int steep = 0;
            foreach (CityX.World.Plan.PlanBlock block in w.Data.Blocks)
            {
                if (block.Outer == null || block.Outer.Count < 3 || !block.Paved || block.Plaza || block.Mall || block.Encloses) continue;
                double lo = double.MaxValue, hi = double.MinValue;
                foreach (CityX.World.Plan.Vec2 p in block.Outer)
                {
                    double h = w.GroundHeight(p.X, p.Y);
                    lo = System.Math.Min(lo, h);
                    hi = System.Math.Max(hi, h);
                }
                if (hi - lo >= CityX.World.Plan.StairLanes.MinDrop) steep++;
            }
            double length = 0.0, drop = 0.0;
            int steps = 0;
            foreach (CityX.World.Plan.StairLane lane in w.Data.StairLanes)
            {
                length += lane.Length;
                drop += lane.Drop;
                var treads = CityX.World.Plan.StairLanes.Profile(lane, w.Fields);
                for (int i = 1; i < treads.Count; i++) if (treads[i].Top > treads[i - 1].Top + 1e-9) steps++;
            }
            int n = System.Math.Max(1, w.Data.StairLanes.Count);
            System.Console.WriteLine($"{w.Data.StairLanes.Count} stairways ({steep} blocks drop {CityX.World.Plan.StairLanes.MinDrop} m or more across), " +
                $"mean {length / n:F0} m long, {drop / n:F1} m up, {steps} steps up in all");
            w.TryFindSpawn(out double sx, out double sz);
            var spawn = new CityX.World.Plan.Vec2(sx, sz);
            int near1 = 0, near2 = 0;
            foreach (CityX.World.Plan.StairLane lane in w.Data.StairLanes)
            {
                double d = ((lane.A + lane.B) * 0.5 - spawn).Length;
                if (d < 1000.0) near1++;
                if (d < 2000.0) near2++;
            }
            int land = 0, hilly = 0;
            for (double gx = sx - 1500.0; gx <= sx + 1500.0; gx += 50.0)
            {
                for (double gz = sz - 1500.0; gz <= sz + 1500.0; gz += 50.0)
                {
                    if (w.Fields.IsWater(gx, gz)) continue;
                    land++;
                    if (w.Fields.Hillock(gx, gz) > 2.0) hilly++;
                }
            }
            System.Console.WriteLine($"  near the spawn ({sx:F0}, {sz:F0}): {near1} within 1 km, {near2} within 2 km; {(double)hilly / System.Math.Max(1, land):P0} of the land within 1.5 km on a hill");
            int stepStreets = 0, stepNear = 0;
            int stepShort = 0;
            double stepWorst = 0.0;
            double stepLength = 0.0;
            foreach (CityX.World.Plan.RoadEdge edge in w.Data.Edges)
            {
                if (!CityX.World.StepStreets.Is(w, edge)) continue;
                stepStreets++;
                var sg = w.Geometry.Edges[edge.Id];
                var st = CityX.World.StepStreets.Profile(w, sg, out bool sFromB);
                var top = sg.A + sg.Dir * (sFromB ? sg.TrimA : sg.Length - sg.TrimB);
                double miss = System.Math.Abs(w.GroundHeight(top.X, top.Y) + 0.07 - st[st.Count - 1].Top);
                if (miss > 0.15) { stepShort++; stepWorst = System.Math.Max(stepWorst, miss); }
                stepLength += edge.Length;
                if (((edge.A.P + edge.B.P) * 0.5 - spawn).Length < 1000.0) stepNear++;
            }
            System.Console.WriteLine($"  {stepStreets} step streets ({stepLength:F0} m), {stepNear} within 1 km of the spawn; {stepShort} miss their top by more than 0.15 m (worst {stepWorst:F2} m)");
            return 0;
        }
        // underroads [seed]: every live road's carriageway sampled each metre at its middle and 80 % out to either side: the points a building stands on.
        if (args.Length > 0 && args[0] == "underroads")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int roads = 0, points = 0, shown = 0;
            foreach (CityX.World.Plan.RoadEdge e in w.Data.Edges)
            {
                if (!e.Alive) continue;
                CityX.World.Plan.EdgeGeometry g = w.Geometry.Edges[e.Id];
                if (g == null) continue;
                roads++;
                for (double t = g.TrimA; t <= g.Length - g.TrimB; t += 1.0)
                {
                    foreach (double side in new[] { -0.8, 0.0, 0.8 })
                    {
                        CityX.World.Plan.Vec2 q = g.A + g.Dir * t + g.Dir.Perp * (g.HalfWidth * side);
                        int b = w.BuildingAt(q.X, q.Y);
                        if (b < 0) continue;
                        points++;
                        if (shown++ < 10) System.Console.WriteLine($"  road {e.Id} ({e.Class}, {g.Length:F0} m, blocks beside {w.BlocksBeside(e).Count}) under building {b} ({w.Buildings[b].Params.Mass} '{w.Buildings[b].Params.SpecialType}', onRoof {w.Buildings[b].OnRoof}) at t {t:F0} side {side}");
                    }
                }
            }
            System.Console.WriteLine($"{roads} live roads; {points} carriageway points under a building");
            return 0;
        }
        // spurs [seed]: the alive roads with no block beside them (dead ends pruned before blocks are traced, roads inside one block): how many,
        // how many run under a building, and what those buildings are.
        if (args.Length > 0 && args[0] == "spurs")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 91736402);
            int alive = 0, spurs = 0, underBuildings = 0, deadEnds = 0;
            var kinds = new System.Collections.Generic.Dictionary<string, int>();
            int shown = 0;
            foreach (CityX.World.Plan.RoadEdge e in w.Data.Edges)
            {
                if (!e.Alive) continue;
                alive++;
                if (w.BlocksBeside(e).Count > 0) continue;
                spurs++;
                bool dead = e.A.Edges.Count == 1 || e.B.Edges.Count == 1;
                if (dead) deadEnds++;
                CityX.World.Plan.EdgeGeometry g = w.Geometry.Edges[e.Id];
                if (g == null) continue;
                var hit = new System.Collections.Generic.HashSet<int>();
                for (double t = g.TrimA; t <= g.Length - g.TrimB; t += 1.0)
                {
                    CityX.World.Plan.Vec2 p = g.A + g.Dir * t;
                    foreach (double side in new[] { -g.HalfWidth * 0.8, 0.0, g.HalfWidth * 0.8 })
                    {
                        CityX.World.Plan.Vec2 q = p + g.Dir.Perp * side;
                        int b = w.BuildingAt(q.X, q.Y);
                        if (b >= 0) hit.Add(b);
                    }
                }
                if (hit.Count == 0) continue;
                underBuildings++;
                foreach (int b in hit)
                {
                    var s = w.Buildings[b];
                    string kind = string.IsNullOrEmpty(s.Params.SpecialType) ? s.Params.Mass.ToString() : s.Params.SpecialType;
                    kinds[kind] = kinds.TryGetValue(kind, out int n) ? n + 1 : 1;
                }
                if (shown++ < 12)
                {
                    System.Console.WriteLine($"  edge {e.Id} ({e.Class}, {g.Length:F0} m{(dead ? ", dead end" : "")}) under buildings {string.Join(",", hit)}");
                }
            }
            System.Console.WriteLine($"{alive} alive roads; {spurs} with no block beside them ({deadEnds} dead ends); {underBuildings} of those run under a building");
            foreach (var kv in kinds) System.Console.WriteLine($"  {kv.Value,5} x {kv.Key}");
            return 0;
        }
        // edge <id> [seed] [t]: one road edge's geometry (ends, direction, length, trims, half width), the chunks it runs through and where it crosses
        // their boundaries, the road nodes within 20 m of the point t metres along it (a junction mid-way), and the blocks beside it.
        if (args.Length > 1 && args[0] == "edge")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 91736402);
            int id = int.Parse(args[1]);
            double at = args.Length > 3 ? double.Parse(args[3]) : double.NaN;
            CityX.World.Plan.RoadEdge edge = w.Data.Edges[id];
            CityX.World.Plan.EdgeGeometry g = w.Geometry.Edges[id];
            System.Console.WriteLine($"edge {id}: class {edge.Class} bridge {edge.Bridge} alive {edge.Alive} A ({g.A.X:F1},{g.A.Y:F1}) B ({g.B.X:F1},{g.B.Y:F1}) dir ({g.Dir.X:F3},{g.Dir.Y:F3}) length {g.Length:F1} trims {g.TrimA:F1}/{g.TrimB:F1} halfWidth {g.HalfWidth:F2}");
            System.Console.WriteLine($"  node A {edge.A.Id}: {edge.A.Edges.Count} edges; node B {edge.B.Id}: {edge.B.Edges.Count} edges; ramps landing on it {w.Data.Expressways.Paths.FindAll(p => p.StreetEdge == id).Count}, kerb cuts {w.Data.Expressways.KerbCuts.FindAll(c => c.StreetEdge == id).Count}, exits {w.Data.Expressways.Interchanges.FindAll(i => i.StreetEdge == id).Count}");
            double chunk = CityX.World.Plan.PlanWorld.ChunkSize;
            int lastCx = int.MinValue, lastCy = int.MinValue;
            for (double t = 0.0; t <= g.Length; t += 0.25)
            {
                CityX.World.Plan.Vec2 p = g.A + g.Dir * t;
                int cx = CityX.World.Plan.PlanWorld.ChunkOf(p.X), cy = CityX.World.Plan.PlanWorld.ChunkOf(p.Y);
                if (cx != lastCx || cy != lastCy)
                {
                    System.Console.WriteLine($"  t {t,6:F2}: chunk ({cx},{cy}) ground {w.GroundHeight(p.X, p.Y):F2}");
                    lastCx = cx;
                    lastCy = cy;
                }
            }
            if (!double.IsNaN(at))
            {
                CityX.World.Plan.Vec2 p = g.A + g.Dir * at;
                System.Console.WriteLine($"  point t={at:F1}: ({p.X:F1},{p.Y:F1}) chunk ({CityX.World.Plan.PlanWorld.ChunkOf(p.X)},{CityX.World.Plan.PlanWorld.ChunkOf(p.Y)}) ground {w.GroundHeight(p.X, p.Y):F2}");
                for (int n = 0; n < w.Data.Nodes.Count; n++)
                {
                    CityX.World.Plan.Vec2 q = w.Data.Nodes[n].P;
                    double d = (q - p).Length;
                    if (d <= 20.0) System.Console.WriteLine($"    node {n} at ({q.X:F1},{q.Y:F1}) {d:F1} m away, {w.Data.Nodes[n].Edges.Count} edges");
                }
                // The buildings about the point: how far their middle is along and across the edge, and the rise their door climbs (its steps).
                foreach (var s in w.Buildings)
                {
                    var c = new CityX.World.Plan.Vec2(s.CentreX, s.CentreZ);
                    double along = CityX.World.Plan.Vec2.Dot(c - g.A, g.Dir), across = CityX.World.Plan.Vec2.Dot(c - g.A, g.Dir.Perp);
                    if (System.Math.Abs(along - at) > 30.0 || System.Math.Abs(across) > 40.0) continue;
                    System.Console.WriteLine($"    building {s.Id} {s.Params.Width:F1}x{s.Params.Depth:F1}x{s.Params.Height:F1} {s.Params.Mass} at t {along:F1} w {across:F1}, axis ({s.AxisX:F2},{s.AxisZ:F2}), base {s.BaseHeight:F2} door ground {s.DoorGroundHeight:F2} (rise {s.BaseHeight - s.DoorGroundHeight:F2}), onRoof {s.OnRoof} wing {s.Wing} special '{s.Params.SpecialType}'");
                }
                for (int e = 0; e < w.Data.Edges.Count; e++)
                {
                    if (e == id || !w.Data.Edges[e].Alive) continue;
                    CityX.World.Plan.EdgeGeometry h = w.Geometry.Edges[e]; if (h == null) continue;
                    double d = CityX.World.Plan.PlanMath.DistanceToSegment(p, h.A, h.B, out double u);
                    if (d <= 20.0) System.Console.WriteLine($"    edge {e} ({w.Data.Edges[e].Class}, {h.Length:F0} m, half {h.HalfWidth:F1}) {d:F1} m away at u {u:F2}");
                }
            }
            foreach (int bi in w.BlocksBeside(edge))
            {
                CityX.World.Plan.PlanBlock b = w.Data.Blocks[bi]; System.Console.WriteLine($"  block {bi}: paved {b.Paved} plaza {b.Plaza} lots {w.BlockHasLots(bi)} corners {b.Poly.Count}");
                // Its kerb ring (Inner) and lot line (Outer) where they run along this edge: how far across from the road's centreline (the road's
                // own half width is where the surface ends), and the polygon edge they inset from.
                foreach ((string name, System.Collections.Generic.List<CityX.World.Plan.Vec2> ring) in new[] { ("poly", b.Poly), ("kerb", b.Inner), ("lot", b.Outer) })
                {
                    if (ring == null) continue;
                    for (int i = 0; i < ring.Count; i++)
                    {
                        CityX.World.Plan.Vec2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
                        CityX.World.Plan.Vec2 d = p1 - p0;
                        if (d.Length < 1e-6) continue;
                        d = d.Normalized;
                        if (System.Math.Abs(CityX.World.Plan.Vec2.Dot(d, g.Dir)) < 0.9) continue;
                        CityX.World.Plan.Vec2 mid = (p0 + p1) * 0.5;
                        double across = CityX.World.Plan.Vec2.Dot(mid - g.A, g.Dir.Perp);
                        double along = CityX.World.Plan.Vec2.Dot(mid - g.A, g.Dir);
                        if (System.Math.Abs(across) > 30.0 || along < -10.0 || along > g.Length + 10.0) continue;
                        double w0 = CityX.World.Plan.Vec2.Dot(p0 - g.A, g.Dir.Perp), w1 = CityX.World.Plan.Vec2.Dot(p1 - g.A, g.Dir.Perp);
                        System.Console.WriteLine($"    {name} segment {i}: across {w0:F2} .. {w1:F2} (mid {across:F2}), {(p1 - p0).Length:F1} m long, along {CityX.World.Plan.Vec2.Dot(p0 - g.A, g.Dir):F1}..{CityX.World.Plan.Vec2.Dot(p1 - g.A, g.Dir):F1}");
                    }
                }
            }
            return 0;
        }
        // towers [seed] [chunkRadius]: the tall buildings round the spawn by mass and by the flags that keep a tower top (World/TowerTop) off them.
        if (args.Length > 0 && args[0] == "towers")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int radius = args.Length > 2 ? int.Parse(args[2]) : 80;
            w.TryFindSpawn(out double sx, out double sz);
            int cx = CityX.World.Plan.PlanWorld.ChunkOf(sx), cy = CityX.World.Plan.PlanWorld.ChunkOf(sz);
            var byMass = new System.Collections.Generic.Dictionary<string, int>();
            var byFlags = new System.Collections.Generic.Dictionary<string, int>();
            int tall = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    foreach (int id in w.BuildingsOwnedBy(cx + dx, cy + dy))
                    {
                        var s = w.Buildings[id];
                        if (!s.Params.HasBuilding || s.Params.Height < 100.0f) continue;
                        tall++;
                        string mass = s.Params.Mass.ToString();
                        byMass[mass] = byMass.TryGetValue(mass, out int m) ? m + 1 : 1;
                        bool tower = s.Params.Mass == CityX.World.BuildingMass.Tower || s.Params.Mass == CityX.World.BuildingMass.Supertall;
                        if (!tower) continue;
                        string flags = (s.Params.IsSpecial ? "special " : "") + (s.Shell ? "shell " : "") + (s.OnRoof ? "onroof " : "") + (s.Wing ? "wing " : "")
                            + (s.Sides >= 3 ? "round " : "") + (s.HasRoofObstacle ? "obstacle " : "") + (s.RoofSealed ? "sealed " : "");
                        if (flags == "") flags = "clean";
                        byFlags[flags] = byFlags.TryGetValue(flags, out int f) ? f + 1 : 1;
                    }
                }
            }
            System.Console.WriteLine($"{tall} buildings of 100 m or more within {radius} chunks of the spawn");
            foreach (var kv in byMass) System.Console.WriteLine($"  mass {kv.Key}: {kv.Value}");
            System.Console.WriteLine("towers and supertalls by flags:");
            foreach (var kv in byFlags) System.Console.WriteLine($"  {kv.Value,5}  {kv.Key}");
            return 0;
        }
        // floorcheck [seed]: every ground floor whose footprint corner has the paving above it (BlockPavingTests), with what kind of volume it is.
        if (args.Length > 0 && args[0] == "floorcheck")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int bad = 0;
            foreach (var s in w.Buildings)
            {
                if (s.OnRoof) continue;
                double hw = s.Params.Width * 0.5, hd = s.Params.Depth * 0.5, worst = double.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    s.LocalToWorld((k & 1) == 0 ? -hw : hw, (k & 2) == 0 ? -hd : hd, out double x, out double z);
                    worst = System.Math.Max(worst, w.GroundHeight(x, z) + CityX.World.Plan.PlanBuildings.PavingRise - s.BaseHeight);
                }
                if (worst <= 0.0) continue;
                bad++;
                System.Console.WriteLine($"#{s.Id} paving {worst:F3} over the floor; {s.Params.Width:F1}x{s.Params.Depth:F1} mass={s.Params.Mass} special={s.Params.SpecialType} wing={s.Wing} sides={s.Sides} base {s.BaseHeight:F2} centre ({s.CentreX:F1},{s.CentreZ:F1})");
            }
            System.Console.WriteLine($"{bad} ground floors with paving over a corner");
            return 0;
        }
        if (args.Length > 0 && args[0] == "dubins")
        {
            // dubins x0 y0 heading0Deg x1 y1 heading1Deg radius: every word's pieces, length and total turn.
            var from = new CityX.World.Plan.Pose(new CityX.World.Plan.Vec2(double.Parse(args[1]), double.Parse(args[2])), CityX.World.Plan.DMath.Dir(double.Parse(args[3]) * System.Math.PI / 180.0));
            var to = new CityX.World.Plan.Pose(new CityX.World.Plan.Vec2(double.Parse(args[4]), double.Parse(args[5])), CityX.World.Plan.DMath.Dir(double.Parse(args[6]) * System.Math.PI / 180.0));
            double radius = double.Parse(args[7]);
            foreach (CityX.World.Plan.DubinsPath.Word word in System.Enum.GetValues(typeof(CityX.World.Plan.DubinsPath.Word)))
            {
                var p = CityX.World.Plan.DubinsPath.Solve(from, to, radius, word);
                if (p == null) { System.Console.WriteLine($"{word}: none"); continue; }
                var end = p.At(p.Length);
                System.Console.WriteLine($"{word}: t {p.T:F3} p {p.P:F3} q {p.Q:F3} length {p.Length:F1} turn {p.TotalTurn / System.Math.PI:F2} pi, ends at ({end.P.X:F1},{end.P.Y:F1}) heading {System.Math.Atan2(end.Dir.Y, end.Dir.X) * 180 / System.Math.PI:F1}");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "interchanges")
        {
            // interchanges [seed]: the game's own plan (PlanWorld.For), its spawn, every interchange and the ramps' ends, to compare with a capture's log.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            var xw = w.Data.Expressways;
            w.TryFindSpawn(out double sx, out double sz);
            System.Console.WriteLine($"spawn ({sx:F0},{sz:F0}); {w.Data.Nodes.Count} nodes, {w.Data.Lots.Count} lots");
            foreach (var ic in xw.Interchanges)
            {
                System.Console.WriteLine($"{(ic.System ? "system" : "exit")} {ic.Id} at ({ic.At.X:F0},{ic.At.Y:F0}), {ic.Paths.Count} paths");
                foreach (int id in ic.Paths)
                {
                    var p = xw.Paths[id];
                    System.Console.WriteLine($"   {p.Kind} {p.Id} {p.Shape} {(p.Leaving ? "off" : "on")}: ({p.P[0].X:F0},{p.P[0].Y:F0}) {p.Top[0]:F1} -> ({p.P[p.Count - 1].X:F0},{p.P[p.Count - 1].Y:F0}) {p.Top[p.Count - 1]:F1}, {p.Length:F0} m");
                }
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "tutoriallot") { TutorialSeeds.Report(args.Length > 1 ? int.Parse(args[1]) : 81565117); return 0; }
        if (args.Length > 0 && args[0] == "tutorialseeds") { TutorialSeeds.Rank(args.Length > 1 ? int.Parse(args[1]) : 20261030, args.Length > 2 ? int.Parse(args[2]) : 1500); return 0; }
        if (args.Length > 0 && args[0] == "sunnyspawn") { SunnySpawn.Run(args.Length > 1 ? int.Parse(args[1]) : 20261025, args.Length > 2 ? int.Parse(args[2]) : 1500); return 0; }
        if (args.Length > 0 && args[0] == "nearspawn") { PlanDiagnose.NearSpawn(args.Length > 1 ? int.Parse(args[1]) : 81565117, args.Length > 2 ? int.Parse(args[2]) : 12); return 0; }
        if (args.Length > 0 && args[0] == "overlaps") { PlanDiagnose.Overlaps(args.Length > 1 ? int.Parse(args[1]) : 81565117); return 0; }
        if (args.Length > 0 && args[0] == "profile-line")
        {
            // The ground and what stands on it along a line: profile-line x0 z0 x1 z1 [seed]
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 5 ? int.Parse(args[5]) : 81565117);
            double x0 = double.Parse(args[1]), z0 = double.Parse(args[2]), x1 = double.Parse(args[3]), z1 = double.Parse(args[4]);
            for (int i = 0; i <= 20; i++)
            {
                double t = i / 20.0, x = x0 + (x1 - x0) * t, z = z0 + (z1 - z0) * t;
                int b = w.BuildingAt(x, z, 0.0);
                System.Console.WriteLine($"({x:F1},{z:F1}) ground {w.GroundHeight(x, z):F2}" + (b >= 0 ? $" building #{b} floor {w.Buildings[b].BaseHeight:F2}" : ""));
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "partywalls")
        {
            // How much of the city stands shoulder to shoulder: ground-level boxes with a blank stretch of wall, and how much wall that is.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int ground = 0, touching = 0, contacts = 0;
            double metres = 0.0;
            foreach (var s in w.Buildings)
            {
                if (s.OnRoof || s.Sides >= 3) continue;
                ground++;
                if (s.Contacts == null || s.Contacts.Length == 0) continue;
                touching++;
                contacts += s.Contacts.Length;
                foreach (var c in s.Contacts) metres += c.U1 - c.U0;
            }
            System.Console.WriteLine($"{ground} street-level boxes, {touching} touch another block ({100.0 * touching / ground:F0}%), {contacts} contacts, {metres / 1000.0:F1} km of blank wall");
            return 0;
        }
        if (args.Length > 0 && args[0] == "pavingcheck")
        {
            // pavingcheck [seed]: every paved block whose kerb outline, triangulated, does not cover itself exactly (BlockPavingTests), with its corners.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int bad = 0, paved = 0, index = -1;
            foreach (var block in w.Data.Blocks)
            {
                index++;
                if (!block.Paved) continue;
                paved++;
                double area = 0.0;
                int[] t = block.InnerTriangles;
                for (int k = 0; k < t.Length; k += 3)
                {
                    area += System.Math.Abs(CityX.World.Plan.PlanMath.SignedArea(new[] { block.Inner[t[k]], block.Inner[t[k + 1]], block.Inner[t[k + 2]] }));
                }
                double outline = System.Math.Abs(CityX.World.Plan.PlanMath.SignedArea(block.Inner));
                if (System.Math.Abs(outline - area) <= outline * 1e-6 + 1e-6) continue;
                bad++;
                System.Console.WriteLine($"block {index}: outline {outline:F3} triangles {area:F3} ({t.Length / 3} triangles, {block.Inner.Count} corners, {block.Poly.Count} in the road outline)");
                for (int i = 0; i < block.Poly.Count; i++) System.Console.WriteLine($"  road corner {i}: ({block.Poly[i].X:F4},{block.Poly[i].Y:F4}) class {block.EdgeClass[i]} half width {CityX.World.Plan.CityPlan.HalfWidth(w.Data.Config, block.EdgeClass[i]):F3} sidewalk {w.Data.Config.SidewalkWidth:F3}");
                for (int i = 0; i < block.Inner.Count; i++)
                {
                    var a = block.Inner[i];
                    var b = block.Inner[(i + 1) % block.Inner.Count];
                    var c = block.Inner[(i + 2) % block.Inner.Count];
                    double cross = CityX.World.Plan.Vec2.Cross(b - a, c - b);
                    System.Console.WriteLine($"  corner {i}: ({a.X:F4},{a.Y:F4}) edge {(b - a).Length:F4} m, turn {cross:E2}");
                }
                for (int k = 0; k < t.Length; k += 3)
                {
                    System.Console.WriteLine($"  triangle {t[k]} {t[k + 1]} {t[k + 2]} area {CityX.World.Plan.PlanMath.SignedArea(new[] { block.Inner[t[k]], block.Inner[t[k + 1]], block.Inner[t[k + 2]] }):F3}");
                }
            }
            System.Console.WriteLine($"{bad} of {paved} paved blocks not covered exactly");
            return 0;
        }
        if (args.Length > 0 && args[0] == "insetfail")
        {
            // insetfail [seed]: every block whose kerb line could not be inset (no paving, no lots), with why -- the inset's own steps, replayed here.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int lost = 0;
            foreach (var block in w.Data.Blocks)
            {
                if (block.Inner != null || block.Poly == null || block.Poly.Count < 3) continue;
                lost++;
                var poly = block.Poly;
                int n = poly.Count;
                var distances = new double[n];
                for (int i = 0; i < n; i++) distances[i] = CityX.World.Plan.CityPlan.HalfWidth(w.Data.Config, block.EdgeClass[i]);
                var lotDistances = new double[n];
                for (int i = 0; i < n; i++) lotDistances[i] = distances[i] + w.Data.Config.SidewalkWidth;
                string why = "kerb: " + InsetWhy(poly, distances) + "; lot line: " + InsetWhy(poly, lotDistances);
                double minEdge = double.MaxValue;
                for (int i = 0; i < n; i++) minEdge = System.Math.Min(minEdge, (poly[(i + 1) % n] - poly[i]).Length);
                System.Console.WriteLine($"block {w.Data.Blocks.IndexOf(block)}: {n} corners, shortest edge {minEdge:F2} m, area {System.Math.Abs(CityX.World.Plan.PlanMath.SignedArea(poly)):F0}: {why}");
            }
            System.Console.WriteLine($"{lost} blocks without a kerb line");
            return 0;
        }
        if (args.Length > 0 && args[0] == "edgeblocks")
        {
            // edgeblocks <edge id> [seed]: the blocks either side of a road edge, with what is on them (lots), to see what a pedestrian street runs between.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 81565117);
            int edgeId = int.Parse(args[1]);
            var e = w.Data.Edges[edgeId];
            var lotsByBlock = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var lot in w.Data.Lots) lotsByBlock[lot.BlockIndex] = (lotsByBlock.TryGetValue(lot.BlockIndex, out int c) ? c : 0) + 1;
            System.Console.WriteLine($"edge {edgeId} {e.Class} {e.Length:F0} m from ({e.A.P.X:F0},{e.A.P.Y:F0}) to ({e.B.P.X:F0},{e.B.P.Y:F0}), population {w.Fields.Population((e.A.P + e.B.P) * 0.5):F2}");
            for (int b = 0; b < w.Data.Blocks.Count; b++)
            {
                var block = w.Data.Blocks[b];
                int n = block.Poly.Count;
                for (int i = 0; i < n; i++)
                {
                    var p = block.Poly[i]; var q = block.Poly[(i + 1) % n];
                    bool match = (p - e.A.P).Length < 0.01 && (q - e.B.P).Length < 0.01 || (p - e.B.P).Length < 0.01 && (q - e.A.P).Length < 0.01;
                    if (!match) continue;
                    lotsByBlock.TryGetValue(b, out int lots);
                    System.Console.WriteLine($"  block {b}: {n} corners, area {block.Area:F0}, paved {block.Paved}, plaza {block.Plaza}, mall {block.Mall}, encloses {block.Encloses}, inner {(block.Inner != null)}, lots {lots}");
                }
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "emptyblocks")
        {
            // emptyblocks [seed]: paved blocks that are no plaza and no mall and got no lots at all -- how many, how big, where (population).
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            var lotsByBlock = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var lot in w.Data.Lots) lotsByBlock[lot.BlockIndex] = (lotsByBlock.TryGetValue(lot.BlockIndex, out int c) ? c : 0) + 1;
            int paved = 0, empty = 0, emptyDense = 0;
            double emptyArea = 0.0;
            var samples = new System.Collections.Generic.List<string>();
            for (int b = 0; b < w.Data.Blocks.Count; b++)
            {
                var block = w.Data.Blocks[b];
                if (!block.Paved || block.Plaza || block.Mall || block.Encloses) continue;
                paved++;
                if (lotsByBlock.ContainsKey(b)) continue;
                empty++;
                emptyArea += block.Area;
                var c = CityX.World.Plan.PlanMath.Centroid(block.Inner);
                double pop = w.Fields.Population(c);
                if (pop > 0.5) emptyDense++;
                if (samples.Count < 12) samples.Add($"block {b}: {block.Poly.Count} corners, {block.Area:F0} m2, population {pop:F2}, at ({c.X:F0},{c.Y:F0})");
            }
            System.Console.WriteLine($"{empty} of {paved} paved ordinary blocks have no lots ({emptyArea / 10000.0:F1} ha), {emptyDense} of them in dense country");
            foreach (string s in samples) System.Console.WriteLine("  " + s);
            return 0;
        }
        if (args.Length > 0 && args[0] == "emptynear")
        {
            // emptynear [seed]: of the paved ordinary blocks with no lots, how many lie under or beside an expressway (a path within 40 m of the centroid or crossing the block).
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            var lotsByBlock = new System.Collections.Generic.HashSet<int>();
            foreach (var lot in w.Data.Lots) lotsByBlock.Add(lot.BlockIndex);
            int empty = 0, nearXway = 0, water = 0, steep = 0, small = 0;
            var paths = w.Data.Expressways.Paths;
            for (int b = 0; b < w.Data.Blocks.Count; b++)
            {
                var block = w.Data.Blocks[b];
                if (!block.Paved || block.Plaza || block.Mall || block.Encloses || lotsByBlock.Contains(b)) continue;
                empty++;
                var c = CityX.World.Plan.PlanMath.Centroid(block.Inner);
                double nearest = double.MaxValue;
                foreach (var path in paths)
                {
                    for (int i = 0; i + 1 < path.Count; i++)
                    {
                        nearest = System.Math.Min(nearest, CityX.World.Plan.PlanMath.DistanceToSegment(c, path.P[i], path.P[i + 1], out _));
                    }
                }
                double radius = System.Math.Sqrt(block.Area) * 0.5;
                if (nearest < radius + 40.0) nearXway++;
                if (w.Fields.IsWater(c)) water++;
                double h0 = w.GroundHeight(c.X - radius, c.Y), h1 = w.GroundHeight(c.X + radius, c.Y), h2 = w.GroundHeight(c.X, c.Y - radius), h3 = w.GroundHeight(c.X, c.Y + radius);
                if (System.Math.Max(System.Math.Abs(h1 - h0), System.Math.Abs(h3 - h2)) / (2.0 * radius + 1e-6) > 0.15) steep++;
                if (block.Area < 1200.0) small++;
            }
            System.Console.WriteLine($"{empty} empty blocks: {nearXway} by an expressway, {water} on water, {steep} steep (over 15 %), {small} under 1200 m2");
            return 0;
        }
        if (args.Length > 0 && args[0] == "fitcheck")
        {
            // Every lot's fitted rectangle must lie inside the lot's footprint.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            int bad = 0, total = 0;
            foreach (var lot in w.Data.Lots)
            {
                if (!CityX.World.Plan.PlanBuildings.FitRectangle(lot.Footprint, lot.FrontEdge, out var c, out var ex, out double width, out double depth, lot.Scale,
                    lot.StreetWall ? CityX.World.Plan.PlanBuildings.StreetWallFitMargin : CityX.World.Plan.PlanBuildings.FitMargin)) continue;
                total++;
                var ez = new CityX.World.Plan.Vec2(-ex.Y, ex.X);
                double worst = 0.0;
                for (int k = 0; k < 4; k++)
                {
                    var p = c + ex * (((k & 1) == 0 ? -0.5 : 0.5) * width) + ez * (((k & 2) == 0 ? -0.5 : 0.5) * depth);
                    worst = System.Math.Max(worst, Outside(lot.Footprint, p));
                }
                if (worst > 0.05)
                {
                    bad++;
                    if (worst > 0.2) System.Console.WriteLine($"lot {lot.Id} pts {lot.Footprint.Length} front {lot.FrontEdge}: rect {width:F1}x{depth:F1} pokes out {worst:F2} m");
                }
            }
            System.Console.WriteLine($"{bad} of {total} rectangles poke out of their lot");
            return 0;
        }
        if (args.Length > 0 && args[0] == "fitlot")
        {
            // fitlot <lot id> [seed]: one lot's footprint in its frontage's frame (x along it, z out of the front) and the rectangle fitted in it.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 81565117);
            int id = int.Parse(args[1]);
            foreach (var lot in w.Data.Lots)
            {
                if (lot.Id != id) continue;
                var poly = lot.Footprint;
                int n = poly.Length, f = lot.FrontEdge;
                var d = (poly[(f + 1) % n] - poly[f]).Normalized;
                var fz = new CityX.World.Plan.Vec2(d.Y, -d.X);
                var fx = new CityX.World.Plan.Vec2(-d.X, -d.Y);
                var o = (poly[f] + poly[(f + 1) % n]) * 0.5;
                System.Console.WriteLine($"lot {id}: {n} corners, front edge {f}, street wall {lot.StreetWall}, scale {lot.Scale:F2}");
                for (int i = 0; i < n; i++)
                {
                    var q = poly[i] - o;
                    System.Console.WriteLine($"  corner {i}: x {CityX.World.Plan.Vec2.Dot(q, fx):F3} z {CityX.World.Plan.Vec2.Dot(q, fz):F3}");
                }
                double margin = lot.StreetWall ? CityX.World.Plan.PlanBuildings.StreetWallFitMargin : CityX.World.Plan.PlanBuildings.FitMargin;
                if (!CityX.World.Plan.PlanBuildings.FitRectangle(poly, f, out var c, out var ex, out double width, out double depth, lot.Scale, margin))
                {
                    System.Console.WriteLine("  no rectangle");
                    break;
                }
                var cq = c - o;
                System.Console.WriteLine($"  rectangle {width:F3} x {depth:F3} at x {CityX.World.Plan.Vec2.Dot(cq, fx):F3} z {CityX.World.Plan.Vec2.Dot(cq, fz):F3}");
                var ez = new CityX.World.Plan.Vec2(-ex.Y, ex.X);
                for (int k = 0; k < 4; k++)
                {
                    var p = c + ex * (((k & 1) == 0 ? -0.5 : 0.5) * width) + ez * (((k & 2) == 0 ? -0.5 : 0.5) * depth);
                    var pq = p - o;
                    System.Console.WriteLine($"  rect corner {k}: x {CityX.World.Plan.Vec2.Dot(pq, fx):F3} z {CityX.World.Plan.Vec2.Dot(pq, fz):F3} outside by {Outside(poly, p):F3}");
                }
                break;
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "scalecheck")
        {
            // scalecheck <seed>: CityScaleTests.AVariedCityRunsFromClassicSizedBuildingsToHugeOnes's numbers -- the long side's percentiles,
            // classic against varied.
            int seed = args.Length > 1 ? int.Parse(args[1]) : 81565117;
            System.Collections.Generic.List<double> Long(CityX.World.Plan.CityScale scale)
            {
                var w = CityX.World.Plan.PlanWorld.Create(seed, CityX.World.Plan.PlanConfig.For(scale));
                var l = new System.Collections.Generic.List<double>();
                foreach (var s in w.Buildings) l.Add(System.Math.Max(s.Params.Width, s.Params.Depth));
                l.Sort();
                return l;
            }
            double Pc(System.Collections.Generic.List<double> s, double p) => s[System.Math.Min(s.Count - 1, (int)(p * s.Count))];
            var cl = Long(CityX.World.Plan.CityScale.Classic);
            var vl = Long(CityX.World.Plan.CityScale.Varied);
            System.Console.WriteLine($"seed {seed}: classic p50 {Pc(cl, 0.5):F1} p95 {Pc(cl, 0.95):F1}; varied p10 {Pc(vl, 0.1):F1} p95 {Pc(vl, 0.95):F1}; " +
                $"p95 ratio {Pc(vl, 0.95) / Pc(cl, 0.95):F2} (needs > 2.2); p10 vs 1.3 x classic p50 {Pc(vl, 0.1):F1} < {Pc(cl, 0.5) * 1.3:F1}; spread {Pc(vl, 0.95) / Pc(vl, 0.1):F2} (> 3.5)");
            return 0;
        }
        if (args.Length > 0 && args[0] == "sizes")
        {
            // How big the city's buildings are: footprint side (square root of the area), height, and the share above fixed thresholds
            // (pass the thresholds from an earlier run as args 2-3 to compare a change against the same lines).
            var w = CityX.World.Plan.PlanWorld.Create(args.Length > 1 ? int.Parse(args[1]) : 81565117, CityX.World.Plan.PlanWorld.DefaultConfig());
            var sides = new System.Collections.Generic.List<double>();
            var heights = new System.Collections.Generic.List<double>();
            foreach (var b in w.Buildings)
            {
                if (b.OnRoof || b.Wing || b.Params.IsSpecial) continue;   // whole buildings at street level, ordinary ones
                double top = b.Top - b.BaseHeight;
                for (int k = b.Id + 1; k < w.Buildings.Length; k++)
                {
                    var v = w.Buildings[k];
                    if (!v.OnRoof && !v.Wing) break;
                    top = System.Math.Max(top, v.Top - b.BaseHeight);
                }
                sides.Add(System.Math.Sqrt(b.Params.Width * b.Params.Depth));
                heights.Add(top);
            }
            double sideLine = args.Length > 2 ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
            double heightLine = args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
            void Report(string name, System.Collections.Generic.List<double> xs, double line)
            {
                xs.Sort();
                double mean = 0.0;
                foreach (double x in xs) mean += x;
                mean /= xs.Count;
                double P(double q) => xs[(int)System.Math.Min(xs.Count - 1, q * xs.Count)];
                if (double.IsNaN(line)) line = P(0.75);
                int above = 0;
                foreach (double x in xs) if (x >= line) above++;
                System.Console.WriteLine($"{name}: {xs.Count} buildings, mean {mean:F1}, median {P(0.5):F1}, p75 {P(0.75):F1}, p90 {P(0.9):F1}, p99 {P(0.99):F1}, max {xs[xs.Count - 1]:F1}; at or above {line:F1}: {100.0 * above / xs.Count:F1}%");
            }
            Report("footprint side (m)", sides, sideLine);
            Report("height (m)", heights, heightLine);
            return 0;
        }
        if (args.Length > 0 && args[0] == "nearbridge")
        {
            // The bridge nearest the spawn (what RainRealCityPlayTests stands beside), and what is round it.
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 81565117);
            w.TryFindSpawn(out double sx, out double sz);
            CityX.World.Plan.EdgeGeometry best = null;
            double bestD = double.MaxValue;
            foreach (var g in w.Geometry.Edges)
            {
                if (g == null || !g.Edge.Bridge) continue;
                double dx = (g.A.X + g.B.X) * 0.5 - sx, dz = (g.A.Y + g.B.Y) * 0.5 - sz;
                if (dx * dx + dz * dz < bestD) { bestD = dx * dx + dz * dz; best = g; }
            }
            System.Console.WriteLine($"spawn ({sx:F0}, {sz:F0}); bridge edge {best.Edge.Id} {best.Edge.Class} from ({best.A.X:F0}, {best.A.Y:F0}) to ({best.B.X:F0}, {best.B.Y:F0}), length {best.Length:F0}");
            var mid = (best.A + best.B) * 0.5;
            var along = best.Dir.Perp;
            foreach (double d in new[] { 0.0, 10.0, 22.0, 34.0, -22.0 })
            {
                var p = mid + along * d;
                int building = w.BuildingAtHeight(p.X, w.GroundHeight(p.X, p.Y) + 2.0, p.Y);
                System.Console.WriteLine($"  {d,5:F0} m along the trench: ground {w.GroundHeight(p.X, p.Y):F1}, water {w.Fields.IsWater(p)}, building {building}");
            }
            foreach (var g in w.Geometry.Edges)
            {
                if (g == null || g == best) continue;
                double d = CityX.World.Plan.PlanMath.DistanceToSegment(mid, g.A, g.B, out _);
                if (d < 60.0) System.Console.WriteLine($"  road {g.Edge.Id} {g.Edge.Class}{(g.Edge.Bridge ? " bridge" : "")} {d:F0} m away");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "mallshapes")
        {
            // How the mall layout comes out over many seeds, for a few lot sizes: pieces of each kind, branches, garages.
            foreach ((double w, double d, double sc) in new[] { (180.0, 130.0, 1.0), (260.0, 200.0, 1.0), (360.0, 260.0, 4.0), (480.0, 340.0, 4.0) })
            {
                var counts = new System.Collections.Generic.Dictionary<string, int>();
                int garages = 0;
                for (uint h = 1; h <= 400; h++)
                {
                    var pieces = CityX.World.Plan.Amenities.MallLayout.Compose(w, d, sc, 13.0, CityX.World.Plan.PlanNoise.Mix(h, 77U),
                        h % 5U == 0U ? CityX.World.Plan.MallParking.GarageAndLot : CityX.World.Plan.MallParking.OpenLot);
                    int corridors = 0, anchors = 0, pads = 0;
                    foreach (var p in pieces)
                    {
                        if (p.Part == CityX.World.Plan.Amenities.MallPart.Concourse) corridors++;
                        else if (p.Part == CityX.World.Plan.Amenities.MallPart.Anchor) anchors++;
                        else if (p.Part == CityX.World.Plan.Amenities.MallPart.Pad) pads++;
                        else garages++;
                    }
                    string key = $"{corridors}c{anchors}a";
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                var keys = new System.Collections.Generic.List<string>(counts.Keys);
                keys.Sort();
                System.Console.WriteLine($"{w:F0} x {d:F0} (scale {sc}): " + string.Join(", ", keys.ConvertAll(k => $"{k} {counts[k]}")) + $"; garages {garages} of 80 asked");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "profile") { PlanProfile.Run(args.Length > 1 ? int.Parse(args[1]) : 81565117); return 0; }
        return SubversionPlan.Run(args);
    }

    /// <summary>How far a point is outside a counter-clockwise convex-or-not polygon (0 inside): the largest distance past any edge it is outside of, when outside.</summary>
    /// <summary>PlanMath.InsetPolygon step by step, saying which check refused the polygon.</summary>
    private static string InsetWhy(System.Collections.Generic.IList<CityX.World.Plan.Vec2> poly, double[] distances)
    {
        int n = poly.Count;
        var lineP = new CityX.World.Plan.Vec2[n];
        var lineD = new CityX.World.Plan.Vec2[n];
        for (int i = 0; i < n; i++)
        {
            var dir = (poly[(i + 1) % n] - poly[i]).Normalized;
            lineP[i] = poly[i] + dir.Perp * distances[i];
            lineD[i] = dir;
        }
        var result = new System.Collections.Generic.List<CityX.World.Plan.Vec2>();
        for (int i = 0; i < n; i++)
        {
            int prev = (i + n - 1) % n;
            double denom = CityX.World.Plan.Vec2.Cross(lineD[prev], lineD[i]);
            CityX.World.Plan.Vec2 v;
            if (System.Math.Abs(denom) < 1e-9) v = lineP[i];
            else
            {
                double t = CityX.World.Plan.Vec2.Cross(lineP[i] - lineP[prev], lineD[i]) / denom;
                v = lineP[prev] + lineD[prev] * t;
                double limit = 3.0 * System.Math.Max(distances[prev], distances[i]) + 1.0;
                if ((v - poly[i]).Length > limit * 2.5) return $"mitre at corner {i} thrown {(v - poly[i]).Length:F1} m (limit {limit * 2.5:F1})";
            }
            result.Add(v);
        }
        for (int pass = 0; pass < n; pass++)
        {
            bool folded = false;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var edge = result[j] - result[i];
                if (CityX.World.Plan.Vec2.Dot(edge, lineD[i]) >= 0.0) continue;
                int prev = (i + n - 1) % n;
                double denom = CityX.World.Plan.Vec2.Cross(lineD[prev], lineD[j]);
                if (System.Math.Abs(denom) < 1e-9) return $"edge {i} backwards by {edge.Length:F2} m (road edge {(poly[j] - poly[i]).Length:F2} m) and its neighbours are parallel";
                double t = CityX.World.Plan.Vec2.Cross(lineP[j] - lineP[prev], lineD[j]) / denom;
                var meet = lineP[prev] + lineD[prev] * t;
                double limit = 3.0 * System.Math.Max(distances[prev], System.Math.Max(distances[i], distances[j])) + 1.0;
                if ((meet - poly[i]).Length > limit * 2.5) return $"edge {i} backwards by {edge.Length:F2} m (road edge {(poly[j] - poly[i]).Length:F2} m), collapse point {(meet - poly[i]).Length:F1} m off (limit {limit * 2.5:F1})";
                System.Console.WriteLine($"    (collapsed edge {i}: it ran backwards by {edge.Length:F2} m, road edge {(poly[j] - poly[i]).Length:F2} m)");
                result[i] = meet;
                result[j] = meet;
                folded = true;
            }
            if (!folded) break;
        }
        double before = CityX.World.Plan.PlanMath.SignedArea(poly);
        double after = CityX.World.Plan.PlanMath.SignedArea(result);
        if (after <= 1.0) return $"inset area {after:F1} (vanished)";
        if (after > before) return $"inset area {after:F1} > {before:F1}";
        if (CityX.World.Plan.PlanMath.SelfIntersects(result)) return "the inset crosses itself";
        return "fine now?";
    }
    private static double Outside(CityX.World.Plan.Vec2[] poly, CityX.World.Plan.Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        }
        if (inside) return 0.0;
        double best = double.MaxValue;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Length];
            var d = b - a;
            double t = System.Math.Clamp(CityX.World.Plan.Vec2.Dot(p - a, d) / CityX.World.Plan.Vec2.Dot(d, d), 0.0, 1.0);
            best = System.Math.Min(best, (a + d * t - p).Length);
        }
        return best;
    }
}
