public static class Program
{
    public static int Main(string[] args)
    {
        // dotnet run -c Release --project tools/cityplan -- [seed] [halfMetres] [pixels] [out.png] [centreX centreZ viewHalf]
        if (args.Length > 0 && args[0] == "site")
        {
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
        if (args.Length > 0 && args[0] == "nearspawn") { PlanDiagnose.NearSpawn(args.Length > 1 ? int.Parse(args[1]) : 46283197, args.Length > 2 ? int.Parse(args[2]) : 12); return 0; }
        if (args.Length > 0 && args[0] == "overlaps") { PlanDiagnose.Overlaps(args.Length > 1 ? int.Parse(args[1]) : 46283197); return 0; }
        if (args.Length > 0 && args[0] == "profile-line")
        {
            // The ground and what stands on it along a line: profile-line x0 z0 x1 z1 [seed]
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 5 ? int.Parse(args[5]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 2 ? int.Parse(args[2]) : 46283197);
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
            int seed = args.Length > 1 ? int.Parse(args[1]) : 46283197;
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
            var w = CityX.World.Plan.PlanWorld.Create(args.Length > 1 ? int.Parse(args[1]) : 46283197, CityX.World.Plan.PlanWorld.DefaultConfig());
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
            var w = CityX.World.Plan.PlanWorld.For(args.Length > 1 ? int.Parse(args[1]) : 46283197);
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
        if (args.Length > 0 && args[0] == "profile") { PlanProfile.Run(args.Length > 1 ? int.Parse(args[1]) : 46283197); return 0; }
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
