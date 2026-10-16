using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The doors onto the balconies (World/BalconyDoors, View/CityMeshBuilder.BalconyDoors.cs, View/BalconyDoorInteractor, Character/BalconyDoorsNetwork):
    /// every flat's balcony has one, of the building's kind (mostly sliding), big enough for a 2.6 m body; the ones onto a floor open, and leave a way
    /// through the wall a body fits, and the ones half way up a room or onto the stairs stay shut; the cells they take have no window; the state is
    /// shared and only real doors are taken from the network.
    /// </summary>
    public sealed class BalconyDoorsTests
    {
        private const int Seed = 45678679;

        /// <summary>The player's capsule (PlayerRigBuilder): its height and its radius, each with the controller's skin round it.</summary>
        private static float BodyHeight => BodyRig.StandingHeight + 2.0f * 0.03f;

        private static float BodyWidth => 2.0f * (FusedBodyShape.TorsoRadius + 0.03f);

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<(BuildingSite site, List<BalconyDoor> doors)> _near;

        /// <summary>The apartment blocks with balcony doors within <paramref name="radius"/> chunks of the spawn, and their doors.</summary>
        private static List<(BuildingSite site, List<BalconyDoor> doors)> WithDoors(int radius)
        {
            var found = new List<(BuildingSite, List<BalconyDoor>)>();
            foreach (BuildingSite site in TestSites.NearSpawn(World, radius))
            {
                var doors = new List<BalconyDoor>();
                BalconyDoors.For(site, WindowPlan.For(World, site), doors);
                if (doors.Count > 0)
                {
                    found.Add((site, doors));
                }
            }
            return found;
        }

        private static List<(BuildingSite site, List<BalconyDoor> doors)> Near => _near ?? (_near = WithDoors(30));

        [SetUp]
        public void SetUp()
        {
            BalconyDoorState.Shared.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BalconyDoorState.Shared.Clear();
            BalconyDoorState.Shared.Relay = null;
        }

        // ---- where they are ----

        [Test]
        public void EveryFlatOnEveryBalconyHasOneDoor()
        {
            int blocks = 0, doors = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 30))
            {
                WindowPlan plan = WindowPlan.For(World, site);
                ApartmentFront front = ApartmentFront.For(site, plan);
                if (!front.Balconies)
                {
                    continue;
                }
                blocks++;
                var list = new List<BalconyDoor>();
                BalconyDoors.For(site, plan, list);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                var edges = new float[ApartmentFront.MaxEdges(columns)];
                int n = front.UnitEdges(-site.Params.Width * 0.5f, columns, edges);
                int floorCount = BuildingStairs.PlanFor(site).FloorCount;
                for (int r = 1; r < rows.Count; r++)
                {
                    // A balcony (and so a door) only on a floor's row: a storey is two window rows.
                    bool floorRow = r % BuildingInterior.StoreyHeightRows == 0 && r / BuildingInterior.StoreyHeightRows < floorCount;
                    for (int k = 0; k + 1 < n; k++)
                    {
                        float mid = (edges[k] + edges[k + 1]) * 0.5f, half = (edges[k + 1] - edges[k]) * 0.5f;
                        bool balcony = floorRow && site.ContactTopCentred(WindowGrid.Wall.South, mid - half, mid + half) <= r * rows.Size - 0.5f;
                        int found = 0;
                        foreach (BalconyDoor d in list)
                        {
                            if (d.Unit == k && d.Row == r)
                            {
                                found++;
                                Assert.That(d.U0, Is.GreaterThanOrEqualTo(edges[k] + BalconyDoors.FromFlatEdge - 1e-3f), $"building {site.Id}: a door past its flat's edge");
                                Assert.That(d.U1, Is.LessThanOrEqualTo(edges[k + 1] - BalconyDoors.FromFlatEdge + 1e-3f), $"building {site.Id}: a door past its flat's edge");
                                float c0 = -site.Params.Width * 0.5f + d.Column * columns.Size;
                                Assert.That(d.U0, Is.GreaterThanOrEqualTo(c0 - 1e-3f), $"building {site.Id}: a doorway outside the cells it takes");
                                Assert.That(d.U1, Is.LessThanOrEqualTo(c0 + d.Columns * columns.Size + 1e-3f), $"building {site.Id}: a doorway outside the cells it takes");
                            }
                        }
                        Assert.AreEqual(balcony ? 1 : 0, found, $"building {site.Id} flat {k} row {r}: {(balcony ? "a balcony with" : "no balcony but")} {found} doors");
                        doors += found;
                    }
                }
            }
            Debug.Log($"[BalconyDoors] {doors} doors on {blocks} apartment blocks");
            Assert.That(blocks, Is.GreaterThan(30), "need a sample of apartment blocks");
        }

        [Test]
        public void TheKindIsTheBuildingsAndMostAreSliding()
        {
            int[] buildings = new int[3];
            int[] doorKinds = new int[3];
            foreach ((BuildingSite site, List<BalconyDoor> doors) in WithDoors(60))
            {
                buildings[(int)BalconyDoors.KindFor(site)]++;
                foreach (BalconyDoor d in doors)
                {
                    doorKinds[(int)d.Kind]++;
                    // A narrower kind only where the flat has no room for the building's: never a wider one.
                    Assert.That((int)d.Kind, Is.GreaterThanOrEqualTo((int)BalconyDoors.KindFor(site)), $"building {site.Id}: a door wider than its building's kind");
                }
            }
            int total = buildings[0] + buildings[1] + buildings[2];
            Debug.Log($"[BalconyDoors] buildings: sliding {buildings[0]}, pairs {buildings[1]}, single {buildings[2]}; doors: sliding {doorKinds[0]}, " +
                $"pairs {doorKinds[1]}, single {doorKinds[2]}");
            Assert.That(total, Is.GreaterThan(150), "need a sample of apartment blocks");
            Assert.That(buildings[0] / (float)total, Is.InRange(0.5f, 0.7f), "most buildings have sliding doors");
            Assert.That(buildings[1] / (float)total, Is.InRange(0.15f, 0.35f), "a quarter have pairs of doors");
            Assert.That(buildings[2] / (float)total, Is.InRange(0.07f, 0.23f), "some have single doors like the street doors");
            Assert.That(doorKinds[0], Is.GreaterThan(doorKinds[1] + doorKinds[2]), "most doors are sliding");
        }

        [Test]
        public void EveryDoorIsBigEnoughForTheBody()
        {
            int checkedDoors = 0;
            float lowest = float.MaxValue, narrowest = float.MaxValue;
            foreach ((BuildingSite site, List<BalconyDoor> doors) in WithDoors(40))
            {
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                foreach (BalconyDoor d in doors)
                {
                    checkedDoors++;
                    float clear = BalconyDoors.ClearHeight(d), passage = BalconyDoors.PassageWidth(d);
                    BalconyDoors.Passage(site, d, out float from, out float to);
                    lowest = Mathf.Min(lowest, clear);
                    narrowest = Mathf.Min(narrowest, passage);
                    string what = $"building {site.Id} flat {d.Unit} row {d.Row} ({d.Kind}, {d.Width:F2} x {d.Height:F2} m, rows of {rows.Size:F2})";
                    Assert.That(clear, Is.GreaterThan(BodyHeight + 0.02f), $"{what}: the way through is {clear:F2} m high, the body {BodyHeight:F2}");
                    Assert.That(passage, Is.GreaterThan(BodyWidth + 0.15f), $"{what}: the way through is {passage:F2} m wide");
                    Assert.AreEqual(passage, to - from, 1e-4f, $"{what}: the passage and its width disagree");
                    Assert.That(d.Y1, Is.LessThanOrEqualTo((d.Row + 1) * rows.Size - ApartmentFront.SlabThickness + 1e-3f), $"{what}: the head is in the slab above");
                    Assert.AreEqual(d.Row * rows.Size, d.Y0, 1e-4f, $"{what}: not on the balcony floor");
                }
            }
            Debug.Log($"[BalconyDoors] {checkedDoors} doors: the lowest way through {lowest:F2} m, the narrowest {narrowest:F2} m");
            Assert.That(checkedDoors, Is.GreaterThan(500));
        }

        // ---- which open ----

        [Test]
        public void OnlyDoorsOntoAFloorOpenAndFromEachYouCanWalkToTheStairs()
        {
            int all = 0, opening = 0, walked = 0, shutForTheStairs = 0, shutInTheWay = 0;
            var stuck = new List<string>();
            foreach ((BuildingSite site, List<BalconyDoor> doors) in WithDoors(30))
            {
                BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                float halfW = site.Params.Width * 0.5f, halfD = site.Params.Depth * 0.5f;
                float hx = halfW - BuildingInterior.WallThickness - 0.05f, hz = halfD - BuildingInterior.WallThickness - 0.05f;
                foreach (BalconyDoor d in doors)
                {
                    all++;
                    int floor = d.Row / BuildingInterior.StoreyHeightRows;
                    bool onAFloor = d.Row % BuildingInterior.StoreyHeightRows == 0 && floor < stairs.FloorCount;
                    if (!d.Opens)
                    {
                        if (onAFloor)
                        {
                            shutForTheStairs++;
                            StairPlan p = stairs.Floors[floor], a = floor == 0 ? stairs.Basement : stairs.Floors[floor - 1];
                            var zone = Rect.MinMaxRect(d.U0, -halfD + BuildingInterior.WallThickness, d.U1, -halfD + BuildingInterior.WallThickness + BalconyDoors.ClearInside);
                            bool inTheWay = BuildingStairs.FloorSolids(p, a, floor, halfW, halfD).Exists(s => s.Overlaps(zone));
                            shutInTheWay += inTheWay ? 1 : 0;
                        }
                        continue;
                    }
                    opening++;
                    string what = $"building {site.Id} flat {d.Unit} row {d.Row}";
                    Assert.IsTrue(onAFloor, $"{what}: a door half way up a room, or above the last floor, opens");
                    if (hx > 60.0f || hz > 60.0f)
                    {
                        continue;   // a walking grid for a 200 m floor is a lot of cells; the small and middling ones carry the same cases
                    }

                    // A body coming in through it walks to the stairs (to step on up, or back down): round them, the opening below them and the
                    // corner room's walls. BuildingStairs.WalkPath, not the doors' own flood, so the two are checked against each other.
                    StairPlan plan = stairs.Floors[floor];
                    StairPlan arriving = floor == 0 ? stairs.Basement : stairs.Floors[floor - 1];
                    List<Rect> solids = BuildingStairs.FloorSolids(plan, arriving, floor, halfW, halfD);
                    var inside = new Vector2((d.U0 + d.U1) * 0.5f, -hz + 0.8f);
                    bool reaches = (plan != null && BuildingStairs.WalkPath(solids, hx, hz, inside, plan.EntryPoint) != null)
                        || (arriving != null && BuildingStairs.WalkPath(solids, hx, hz, inside, arriving.ExitPoint) != null)
                        || (plan == null && arriving == null);
                    if (reaches)
                    {
                        walked++;
                    }
                    else
                    {
                        stuck.Add($"{what} (floor {floor} of {stairs.FloorCount})");
                    }
                }
            }
            Debug.Log($"[BalconyDoors] {opening} of {all} doors open; from {walked} a body walks to the stairs, {stuck.Count} cannot; " +
                $"{shutForTheStairs} on a floor stay shut ({shutInTheWay} with the stairs or a wall just inside, the rest with no way from them to the stairs)");
            // Every door is on a floor now (no balcony half way up a storey): most open, less those with the stairs just inside.
            Assert.That(opening / (float)all, Is.GreaterThan(0.6f), "most doors open (every one is on a floor), less those with the stairs just inside");
            Assert.That(shutForTheStairs / (float)(opening + shutForTheStairs), Is.LessThan(0.3f), "most doors onto a floor open (the rest have the stairs, the opening they come up through or a wall just inside)");
            Assert.That(stuck, Is.Empty, $"doors that open where a body cannot walk to the stairs: {string.Join("; ", stuck.GetRange(0, Mathf.Min(5, stuck.Count)))}");
        }

        [Test]
        public void ADoorTakesTheWindowOfItsCells()
        {
            int cells = 0;
            foreach ((BuildingSite site, List<BalconyDoor> doors) in Near)
            {
                WindowPlan plan = WindowPlan.For(World, site);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                foreach (BalconyDoor d in doors)
                {
                    for (int c = d.Column; c < d.Column + d.Columns; c++)
                    {
                        cells++;
                        Assert.IsTrue(GlassDamage.IsDoorCell(site, WindowGrid.Wall.South, columns, c, d.Row),
                            $"building {site.Id} flat {d.Unit} row {d.Row}: a doorway's cell still has a window that can be broken");
                    }
                    // The doorway's middle is no pane (a shot through it breaks nothing), on the wall's outside face.
                    BalconyDoors.InWorld(site, d, out Vector3 corner, out Vector3 along, out Vector3 outward);
                    Vector3 middle = corner + along * (d.Width * 0.5f) + Vector3.up * (d.Height * 0.5f) - outward * 0.12f;
                    Assert.IsFalse(GlassDamage.TryPaneAt(World, site, middle, out PaneKey _), $"building {site.Id} flat {d.Unit} row {d.Row}: a pane in the doorway");

                    // The cell beside it keeps its window (only the doorway's own cells give theirs up).
                    int next = d.Column + d.Columns;
                    if (next < columns.Count && !BalconyDoors.IsDoorCell(doors, next, d.Row))
                    {
                        Assert.IsFalse(GlassDamage.IsDoorCell(site, WindowGrid.Wall.South, columns, next, d.Row),
                            $"building {site.Id} flat {d.Unit} row {d.Row}: the cell beside the doorway lost its window");
                    }
                }
                // The quick answer a shot gets (worked out on the spot) is the laid-out doors' own, cell by cell, on every row.
                int rowCount = WindowGrid.RowsFor(site.Params.Height).Count;
                for (int row = 0; row < rowCount; row++)
                {
                    for (int c = 0; c < columns.Count; c++)
                    {
                        Assert.AreEqual(BalconyDoors.IsDoorCell(doors, c, row), BalconyDoors.IsDoorCell(site, plan, c, row),
                            $"building {site.Id} column {c} row {row}: the quick answer and the laid-out doors disagree");
                    }
                }
            }
            Assert.That(cells, Is.GreaterThan(200));
        }

        // ---- the mesh ----

        /// <summary>Does the segment from <paramref name="p0"/> to <paramref name="p1"/> cross any triangle of the buffer (either face)?</summary>
        private static bool Crosses(MeshBuffer b, Vector3 p0, Vector3 p1)
        {
            Vector3 dir = p1 - p0;
            for (int i = 0; i + 2 < b.Triangles.Count; i += 3)
            {
                Vector3 a = b.Vertices[b.Triangles[i]];
                Vector3 e1 = b.Vertices[b.Triangles[i + 1]] - a;
                Vector3 e2 = b.Vertices[b.Triangles[i + 2]] - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f)
                {
                    continue;
                }
                float inv = 1.0f / det;
                Vector3 s = p0 - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0.0f || u > 1.0f)
                {
                    continue;
                }
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0.0f || u + v > 1.0f)
                {
                    continue;
                }
                float t = Vector3.Dot(e2, q) * inv;
                if (t >= 0.0f && t <= 1.0f)
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class Built
        {
            public readonly MeshBuffer Structures = new MeshBuffer(), Concrete = new MeshBuffer(), Lit = new MeshBuffer(), Dark = new MeshBuffer();
            public Vector3 Origin;

            public bool Crosses(Vector3 a, Vector3 b) => BalconyDoorsTests.Crosses(Structures, a - Origin, b - Origin)
                || BalconyDoorsTests.Crosses(Concrete, a - Origin, b - Origin) || BalconyDoorsTests.Crosses(Lit, a - Origin, b - Origin)
                || BalconyDoorsTests.Crosses(Dark, a - Origin, b - Origin);
        }

        private static Built Build(BuildingSite site)
        {
            var built = new Built { Origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize) };
            CityMeshBuilder.BuildSite(World, site, built.Origin, built.Structures, built.Concrete, built.Lit, built.Dark, new MeshBuffer(), null, null,
                new MeshBuffer(), false);
            return built;
        }

        [Test]
        public void AnOpenDoorLeavesAWayThroughTheWallAndAShutOneDoesNot()
        {
            var tried = new HashSet<BalconyDoorKind>();
            foreach ((BuildingSite site, List<BalconyDoor> doors) in WithDoors(40))
            {
                foreach (BalconyDoor d in doors)
                {
                    if (!d.Opens || tried.Contains(d.Kind))
                    {
                        continue;
                    }
                    tried.Add(d.Kind);
                    string what = $"building {site.Id} flat {d.Unit} row {d.Row} ({d.Kind})";
                    BalconyDoors.InWorld(site, d, out Vector3 corner, out Vector3 along, out Vector3 outward);
                    BalconyDoors.Passage(site, d, out float from, out float to);

                    // Shut: the middle of its way through is closed (glass, a panel, a leaf).
                    BalconyDoorState.Shared.Clear();
                    Built shut = Build(site);
                    Vector3 mid = corner + along * Mathf.Lerp(from, to, 0.35f) + Vector3.up * 1.3f;
                    Assert.IsTrue(shut.Crosses(mid + outward * 0.4f, mid - outward * (BuildingInterior.WallThickness + 0.6f)),
                        $"{what}: shut, there is a way through it");

                    // Open: a body's width and height go through the wall there, from the balcony into the flat.
                    BalconyDoorState.Shared.Set(site.Id, d.Unit, d.Row, true);
                    Built open = Build(site);
                    float margin = Mathf.Max(0.02f, ((to - from) - BodyWidth) * 0.5f);
                    for (int i = 0; i <= 4; i++)
                    {
                        float x = Mathf.Lerp(from + margin, to - margin, i / 4.0f);
                        for (float y = 0.15f; y <= BodyHeight + 0.01f; y += 0.25f)
                        {
                            Vector3 p = corner + along * x + Vector3.up * Mathf.Min(y, BodyHeight);
                            Assert.IsFalse(open.Crosses(p + outward * 0.4f, p - outward * (BuildingInterior.WallThickness + 0.6f)),
                                $"{what}: open, something is in the way {x:F2} m along and {y:F2} m up");
                        }
                    }
                }
                if (tried.Count == 3)
                {
                    break;
                }
            }
            Assert.That(tried.Count, Is.EqualTo(3), $"found only {string.Join(", ", tried)} doors that open to try");
        }

        // ---- the state, the network ----

        private sealed class Relay : IBalconyDoorRelay
        {
            public readonly List<string> Sent = new List<string>();

            public void Send(int building, int unit, int row, bool open) => Sent.Add($"{building}/{unit}/{row}/{open}");
        }

        [Test]
        public void TheStateOpensAndShutsAndSaysWhoseBuilding()
        {
            var state = new BalconyDoorState();
            var heard = new List<int>();
            state.Changed += heard.Add;
            var relay = new Relay();
            state.Relay = relay;

            state.Use(1234, 3, 2, true);
            Assert.IsTrue(state.IsOpen(1234, 3, 2));
            Assert.IsFalse(state.IsOpen(1234, 3, 4), "another row's door");
            Assert.IsFalse(state.IsOpen(1234, 2, 2), "the next flat's door");
            Assert.AreEqual(new[] { 1234 }, heard.ToArray());
            Assert.AreEqual(new[] { "1234/3/2/True" }, relay.Sent.ToArray(), "a door the player opens goes to the other machines");

            state.Set(1234, 3, 2, true);
            Assert.AreEqual(1, heard.Count, "opening an open door changes nothing");
            state.Set(77, 0, 6, true);
            Assert.AreEqual(2, state.OpenCount);
            Assert.AreEqual(1, relay.Sent.Count, "a door heard from the network is not sent back");

            List<long> open = state.AllOpen();
            Assert.AreEqual(2, open.Count);
            BalconyDoorState.Unkey(BalconyDoorState.Key(98765, 17, 33), out int b, out int u, out int r);
            Assert.AreEqual((98765, 17, 33), (b, u, r));

            state.Use(1234, 3, 2, false);
            Assert.IsFalse(state.IsOpen(1234, 3, 2));
            state.Clear();
            Assert.AreEqual(0, state.OpenCount);
            Assert.AreEqual(new[] { 1234, 77, 1234, 77 }, heard.ToArray(), "every change names its building");
        }

        [Test]
        public void TheNetworkOnlyTakesADoorThatOpens()
        {
            CityWorld was = CurrentWorld.Instance;
            CurrentWorld.Instance = World;
            try
            {
                var scratch = new List<BalconyDoor>();
                BalconyDoor opens = default, locked = default;
                BuildingSite site = default, lockedSite = default;
                bool foundOpen = false, foundLocked = false;
                // A door that opens and one that does not, on any buildings near the spawn (with a balcony per floor a locked door -- the stairs
                // or a wall just inside -- is rare, and seed 91736402 had none on a building that also had an open one).
                foreach ((BuildingSite s, List<BalconyDoor> doors) in Near)
                {
                    foreach (BalconyDoor d in doors)
                    {
                        if (d.Opens && !foundOpen) { opens = d; foundOpen = true; site = s; }
                        if (!d.Opens && !foundLocked) { locked = d; foundLocked = true; lockedSite = s; }
                    }
                    if (foundOpen && foundLocked) break;
                }
                Assert.IsTrue(foundOpen && foundLocked, "need a door that opens and one that does not");
                BalconyDoorMessage Message(int building, int unit, int row) => new BalconyDoorMessage { Building = building, Unit = unit, Row = row, Open = true };
                Assert.IsTrue(BalconyDoorsNetwork.Acceptable(Message(site.Id, opens.Unit, opens.Row), scratch), "a door that opens");
                Assert.IsFalse(BalconyDoorsNetwork.Acceptable(Message(lockedSite.Id, locked.Unit, locked.Row), scratch), "a door that does not open");
                Assert.IsFalse(BalconyDoorsNetwork.Acceptable(Message(site.Id, 4000, opens.Row), scratch), "no such flat");
                Assert.IsFalse(BalconyDoorsNetwork.Acceptable(Message(-1, 0, 2), scratch), "no such building");
                Assert.IsFalse(BalconyDoorsNetwork.Acceptable(Message(World.BuildingCount, 0, 2), scratch), "no such building");
                Assert.IsFalse(BalconyDoorsNetwork.Acceptable(Message(site.Id, opens.Unit, 0), scratch), "the ground row has no balcony");
            }
            finally
            {
                CurrentWorld.Instance = was;
            }
        }

        // ---- the player at a door ----

        private static BalconyDoorInteractor.Near ADoorThatOpens(out BuildingSite site)
        {
            foreach ((BuildingSite s, List<BalconyDoor> doors) in Near)
            {
                foreach (BalconyDoor d in doors)
                {
                    if (d.Opens)
                    {
                        site = s;
                        return new BalconyDoorInteractor.Near(s, d);
                    }
                }
            }
            Assert.Fail("no door that opens near the spawn");
            site = default;
            return default;
        }

        [Test]
        public void WalkingTowardAShutDoorOpensItAndWalkingPastDoesNot()
        {
            BalconyDoorInteractor.Near door = ADoorThatOpens(out _);
            var list = new List<BalconyDoorInteractor.Near> { door };
            Vector3 front = door.Corner + door.Along * (door.Door.Width * 0.5f) + door.Outward * 1.0f + Vector3.up * 0.05f;
            Vector3 inside = door.Corner + door.Along * (door.Door.Width * 0.5f) - door.Outward * 1.2f + Vector3.up * 0.05f;
            Assert.AreEqual(0, BalconyDoorInteractor.Bumped(list, front, -door.Outward * 2.5f), "walking in from the balcony");
            Assert.AreEqual(0, BalconyDoorInteractor.Bumped(list, inside, door.Outward * 2.5f), "walking out from the flat");
            Assert.AreEqual(-1, BalconyDoorInteractor.Bumped(list, front, door.Along * 3.0f), "walking along the balcony past it");
            Assert.AreEqual(-1, BalconyDoorInteractor.Bumped(list, front, -door.Outward * 0.5f), "drifting toward it slowly");
            Assert.AreEqual(-1, BalconyDoorInteractor.Bumped(list, front, door.Outward * 2.5f), "walking away from it");
            Assert.AreEqual(-1, BalconyDoorInteractor.Bumped(list, front + Vector3.up * 3.2f, -door.Outward * 2.5f), "on the balcony above");
            Assert.AreEqual(-1, BalconyDoorInteractor.Bumped(list, front + door.Along * (door.Door.Width * 0.5f + 1.0f), -door.Outward * 2.5f),
                "heading for the wall beside it");
            Assert.AreEqual(-1, BalconyDoorInteractor.Bumped(list, front + door.Outward * 2.0f, -door.Outward * 2.5f), "still far off");
        }

        [Test]
        public void EActsOnTheDoorYouLookAtWithinReach()
        {
            BalconyDoorInteractor.Near door = ADoorThatOpens(out _);
            var list = new List<BalconyDoorInteractor.Near> { door };
            Vector3 feet = door.Corner + door.Along * (door.Door.Width * 0.5f) + door.Outward * 1.2f;
            Vector3 eye = feet + Vector3.up * 2.3f;
            Assert.AreEqual(0, BalconyDoorInteractor.Pick(list, feet, eye, (door.Handle - eye).normalized), "looking at it");
            Assert.AreEqual(-1, BalconyDoorInteractor.Pick(list, feet, eye, door.Outward), "with your back to it");
            Vector3 far = feet + door.Outward * 3.0f;
            Assert.AreEqual(-1, BalconyDoorInteractor.Pick(list, far, far + Vector3.up * 2.3f, (door.Handle - far).normalized), "out of reach");
            Vector3 below = feet - Vector3.up * 3.2f;
            Assert.AreEqual(-1, BalconyDoorInteractor.Pick(list, below, below + Vector3.up * 2.3f, (door.Handle - below).normalized), "from the floor below");

            Assert.IsTrue(BalconyDoorInteractor.InTheDoorway(door, door.Corner + door.Along * (door.Door.Width * 0.5f) - door.Outward * 0.1f));
            Assert.IsFalse(BalconyDoorInteractor.InTheDoorway(door, feet), "on the balcony in front of it");
        }

        [Test]
        public void TheDoorsNearAPlayerAreFoundOnTheirFloor()
        {
            BalconyDoorInteractor.Near door = ADoorThatOpens(out BuildingSite site);
            var go = new GameObject("BalconyDoorInteractor");
            try
            {
                var interactor = go.AddComponent<BalconyDoorInteractor>();
                var found = new List<BalconyDoorInteractor.Near>();
                Vector3 feet = door.Corner + door.Along * (door.Door.Width * 0.5f) + door.Outward * 1.0f;
                interactor.FindNear(World, feet, found);
                Assert.IsTrue(found.Exists(n => n.Site.Id == site.Id && n.Door.Unit == door.Door.Unit && n.Door.Row == door.Door.Row),
                    "the door in front of the player");
                Assert.IsFalse(found.Exists(n => Mathf.Abs(n.Corner.y - feet.y) > BalconyDoorInteractor.ReachUpDown + 1.0f), "a door on another floor");
                interactor.FindNear(World, feet + door.Outward * 40.0f, found);
                Assert.IsFalse(found.Exists(n => n.Site.Id == site.Id && n.Door.Unit == door.Door.Unit && n.Door.Row == door.Door.Row),
                    "a door 40 m away");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
