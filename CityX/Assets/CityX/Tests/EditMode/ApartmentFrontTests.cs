using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// An apartment block's faces (World/ApartmentFront, View/Buildings/CityMeshBuilder.Apartment.cs): a balcony on every floor across the back wall and a
    /// walkway across the street wall, only on ordinary homes, the partitions on column lines (never across a window), the same stripes near
    /// and far, and nothing of it where a 2.6 m body walks or hanging through a slab.
    /// </summary>
    public sealed class ApartmentFrontTests
    {
        private const int Seed = CitySeed.Default;

        /// <summary>The player's height (BodyRig.StandingHeight): what must walk out of the door and along the back under the first slab.</summary>
        private const float BodyHeight = 2.6f;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<BuildingSite> _sites;
        private static List<BuildingSite> Sites => _sites ?? (_sites = new List<BuildingSite>(TestSites.NearSpawn(World, 18)));   // 14 chunks held 14 apartment fronts on seed 61256619

        private static ApartmentFront FrontOf(BuildingSite site) => ApartmentFront.For(site, WindowPlan.For(World, site));

        private static void Build(BuildingSite site, bool far, MeshBuffer structures, out Vector3 origin)
        {
            origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                null, null, new MeshBuffer(), far);
        }

        private static bool FreeStanding(BuildingSite site) => site.Contacts == null || site.Contacts.Length == 0;

        [Test]
        public void OnlyOrdinaryHomesHaveThem()
        {
            int fronts = 0, homes = 0;
            foreach (BuildingSite site in Sites)
            {
                WindowPlan plan = WindowPlan.For(World, site);
                ApartmentFront front = ApartmentFront.For(site, plan);
                if (!front.Any)
                {
                    continue;
                }
                fronts++;
                string what = $"building {site.Id} ({site.Zone} {site.Params.Mass} {plan.Style}, {site.Params.Height:F1} m)";
                Assert.IsFalse(site.Params.IsSpecial || site.Shell || site.OnRoof || site.Wing || site.Sides >= 3,
                    $"{what}: a special, sealed, roof-top, wing or round block got an apartment front");
                Assert.That(plan.Style, Is.Not.EqualTo(FacadeStyle.Curtain).And.Not.EqualTo(FacadeStyle.HighBand).And.Not.EqualTo(FacadeStyle.Blank),
                    $"{what}: balconies on a glass skin, a shed or a blank box");
                Assert.That(site.Params.Mass, Is.Not.EqualTo(BuildingMass.Warehouse).And.Not.EqualTo(BuildingMass.Supertall), what);
                Assert.That(site.Zone, Is.Not.EqualTo(ZoneKind.Industrial), $"{what}: an apartment block among the sheds");
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                Assert.That(rows.Count, Is.GreaterThanOrEqualTo(ApartmentFront.MinRowsAbove + 1), $"{what}: too low for an apartment block");
                Assert.That(rows.Size - ApartmentFront.SlabThickness, Is.GreaterThan(BodyHeight + 0.1f),
                    $"{what}: the first floor's slab hangs where a 2.6 m body walks");
                if (front.Walkway)
                {
                    Assert.IsFalse(plan.Storefront, $"{what}: a walkway over a shopfront (its sign and awnings are on that wall)");
                    homes++;
                }
                if (front.Balconies)
                {
                    Assert.IsFalse(plan.IsBlankWall(WindowGrid.Wall.South), $"{what}: balconies on a party wall");
                }
                Assert.That(front.UnitColumns, Is.InRange(2, 3), what);
                Assert.That(front.Depth, Is.InRange(ApartmentFront.MinDepth, ApartmentFront.MaxDepth), what);
            }
            Debug.Log($"[ApartmentFront] {fronts} apartment blocks of {Sites.Count} buildings near spawn ({homes} with a walkway)");
            Assert.That(fronts, Is.GreaterThan(15), "too few apartment blocks near the spawn (downtown) to be seen");
            Assert.That(homes, Is.GreaterThan(0), "no apartment block has a walkway");
        }

        [Test]
        public void TheyAreCommonInTheHomesZonesAndFewerDowntown()
        {
            // Of the buildings that could be one (the right shape and facade), the share that are, zone by zone, out to about a kilometre.
            var could = new int[6];
            var are = new int[6];
            foreach (BuildingSite site in TestSites.NearSpawn(World, 60))
            {
                if (!site.Exists || site.Params.IsSpecial || site.OnRoof || site.Shell || site.Wing || site.Sides >= 3 || RoofShape.For(site).Pitched)
                {
                    continue;
                }
                WindowPlan plan = WindowPlan.For(World, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                if (plan.Style == FacadeStyle.Curtain || plan.Style == FacadeStyle.HighBand || plan.Style == FacadeStyle.Blank
                    || site.Params.Mass == BuildingMass.Warehouse || site.Params.Mass == BuildingMass.Supertall
                    || rows.Count - 1 < ApartmentFront.MinRowsAbove || rows.Size < ApartmentFront.MinRowHeight)
                {
                    continue;
                }
                could[(int)site.Zone]++;
                are[(int)site.Zone] += ApartmentFront.For(site, plan).Any ? 1 : 0;
            }
            float Share(ZoneKind zone) => could[(int)zone] > 0 ? are[(int)zone] / (float)could[(int)zone] : -1.0f;
            Debug.Log($"[ApartmentFront] share: core {Share(ZoneKind.Core):F2} of {could[0]}, urban {Share(ZoneKind.Urban):F2} of {could[1]}, " +
                $"residential {Share(ZoneKind.Residential):F2} of {could[2]}, old town {Share(ZoneKind.OldTown):F2} of {could[3]}");
            Assert.That(could[(int)ZoneKind.Residential], Is.GreaterThan(30), "need a residential sample");
            Assert.That(Share(ZoneKind.Residential), Is.InRange(0.5f, 0.85f), "most homes in a residential zone are apartment blocks");
            Assert.That(Share(ZoneKind.Core), Is.InRange(0.12f, 0.4f), "downtown has some among its offices, not all");
            Assert.That(Share(ZoneKind.Residential), Is.GreaterThan(Share(ZoneKind.Urban)));
            Assert.That(Share(ZoneKind.Urban), Is.GreaterThan(Share(ZoneKind.Core)));
        }

        [Test, Explicit("a survey: how many apartment blocks each zone gets, near the spawn and further out")]
        public void SurveyTheZones()
        {
            foreach (int radius in new[] { 14, 40 })
            {
                var all = new int[6];
                var shaped = new int[6];
                var fronts = new int[6];
                var styles = new Dictionary<string, int>();
                foreach (BuildingSite site in TestSites.NearSpawn(World, radius))
                {
                    if (!site.Exists || site.Params.IsSpecial || site.OnRoof || site.Shell || site.Wing)
                    {
                        continue;
                    }
                    int z = (int)site.Zone;
                    all[z]++;
                    WindowPlan plan = WindowPlan.For(World, site);
                    string key = $"{site.Zone}/{site.Params.Mass}/{plan.Style}";
                    styles[key] = styles.TryGetValue(key, out int c) ? c + 1 : 1;
                    CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                    bool shape = site.Sides < 3 && plan.Style != FacadeStyle.Curtain && plan.Style != FacadeStyle.HighBand && plan.Style != FacadeStyle.Blank
                        && site.Params.Mass != BuildingMass.Warehouse && site.Params.Mass != BuildingMass.Supertall
                        && rows.Count - 1 >= ApartmentFront.MinRowsAbove && rows.Size >= ApartmentFront.MinRowHeight;
                    shaped[z] += shape ? 1 : 0;
                    fronts[z] += ApartmentFront.For(site, plan).Any ? 1 : 0;
                }
                var text = new System.Text.StringBuilder($"[ApartmentFront survey] radius {radius} chunks:");
                for (int z = 0; z < 6; z++)
                {
                    text.Append($"\n  {(ZoneKind)z}: {all[z]} buildings, {shaped[z]} could be, {fronts[z]} are");
                }
                var list = new List<KeyValuePair<string, int>>(styles);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < Mathf.Min(25, list.Count); i++)
                {
                    text.Append($"\n    {list[i].Key}: {list[i].Value}");
                }
                Debug.Log(text.ToString());
            }
        }

        [Test, Explicit("a measurement: what the fronts cost the chunks around the spawn and a residential district, near and far")]
        public void MeasureTheCost()
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            int scx = (int)System.Math.Floor(sx / CityLayout.ChunkSize), scz = (int)System.Math.Floor(sz / CityLayout.ChunkSize);
            // A residential district: the first residential apartment block out from the spawn.
            int rcx = scx, rcz = scz;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 40))
            {
                if (site.Zone == ZoneKind.Residential && FrontOf(site).Balconies)
                {
                    rcx = site.OwnerChunkX;
                    rcz = site.OwnerChunkY;
                    break;
                }
            }
            var text = new System.Text.StringBuilder("[ApartmentFront cost]");
            foreach (var (name, cx, cz) in new[] { ("downtown spawn", scx, scz), ("residential", rcx, rcz) })
            {
                foreach (bool far in new[] { false, true })
                {
                    long[] triangles = new long[2];
                    double[] ms = new double[2];
                    for (int pass = 0; pass < 3; pass++)   // the first pass warms the plan's caches; the last two are timed
                    {
                        foreach (bool on in new[] { false, true })
                        {
                            CityMeshBuilder.ApartmentFrontsOn = on;
                            var watch = System.Diagnostics.Stopwatch.StartNew();
                            long tris = 0;
                            for (int dz = -4; dz <= 4; dz++)
                            {
                                for (int dx = -4; dx <= 4; dx++)
                                {
                                    var ground = new MeshBuffer();
                                    var flat = new MeshBuffer();
                                    var structures = new MeshBuffer();
                                    var lit = new MeshBuffer();
                                    var dark = new MeshBuffer();
                                    var origin = new Vector3((cx + dx) * CityLayout.ChunkSize, 0.0f, (cz + dz) * CityLayout.ChunkSize);
                                    CityMeshBuilder.BuildChunk(World, cx + dx, cz + dz, origin, ground, flat, structures, lit, dark, far: far);
                                    tris += structures.Triangles.Count / 3;
                                }
                            }
                            watch.Stop();
                            if (pass > 0)
                            {
                                triangles[on ? 1 : 0] = tris;
                                ms[on ? 1 : 0] += watch.Elapsed.TotalMilliseconds / 81.0 / 2.0;
                            }
                        }
                    }
                    CityMeshBuilder.ApartmentFrontsOn = true;
                    text.Append($"\n  {name} ({(far ? "far" : "near")}), 81 chunks: wall triangles {triangles[0]} -> {triangles[1]} " +
                        $"(+{100.0 * (triangles[1] - triangles[0]) / System.Math.Max(1, triangles[0]):F1}%), " +
                        $"build {ms[0]:F2} -> {ms[1]:F2} ms a chunk");
                }
            }
            Debug.Log(text.ToString());
        }

        [Test]
        public void TheSameBuildingAlwaysGetsTheSameFront()
        {
            // Every machine builds the city on its own: the front is a pure function of the building.
            int compared = 0;
            foreach (BuildingSite site in Sites)
            {
                ApartmentFront a = FrontOf(site);
                ApartmentFront b = FrontOf(World.Building(site.Id));
                Assert.AreEqual(a.Balconies, b.Balconies);
                Assert.AreEqual(a.Walkway, b.Walkway);
                Assert.AreEqual(a.UnitColumns, b.UnitColumns);
                Assert.AreEqual(a.Depth, b.Depth);
                Assert.AreEqual(a.Railing, b.Railing);
                Assert.AreEqual(a.Finish, b.Finish);
                if (++compared >= 200)
                {
                    break;
                }
            }
            var fresh = new CityWorld(Seed);
            foreach (BuildingSite site in TestSites.NearSpawn(fresh, 2))
            {
                ApartmentFront a = ApartmentFront.For(site, WindowPlan.For(fresh, site));
                ApartmentFront b = FrontOf(World.Building(site.Id));
                Assert.AreEqual(a.Any, b.Any, $"building {site.Id}: another world of the same seed decided differently");
                Assert.AreEqual(a.Depth, b.Depth);
            }
        }

        [Test]
        public void PartitionsStandOnColumnLinesAndTheBandStopsShortOfTheCorners()
        {
            foreach (int unit in new[] { 2, 3 })
            {
                foreach (int count in new[] { 3, 4, 5, 7, 12, 15 })
                {
                    var front = new ApartmentFront(true, false, unit, 1.8f, false, ApartmentFront.ParapetFinish.Wall);
                    var columns = new CellLayout(count, 1.7f);
                    float u0 = -count * columns.Size * 0.5f;
                    var edges = new float[ApartmentFront.MaxEdges(columns)];
                    int n = front.UnitEdges(u0, columns, edges);
                    string what = $"{count} columns, a flat every {unit}";

                    Assert.AreEqual(u0 + ApartmentFront.EndInset, edges[0], 1e-4f, $"{what}: the band's start (clear of the corner pier)");
                    Assert.AreEqual(u0 + count * columns.Size - ApartmentFront.EndInset, edges[n - 1], 1e-4f, $"{what}: the band's end");
                    Assert.That(ApartmentFront.EndInset, Is.GreaterThan(FacadeOrnament.CornerPierHalf), "the band runs into the corner piers");
                    for (int i = 1; i < n - 1; i++)
                    {
                        float c = (edges[i] - u0) / columns.Size;
                        Assert.AreEqual(Mathf.Round(c), c, 1e-3f, $"{what}: a partition off a column line stands across a window");
                        Assert.AreEqual(0, Mathf.RoundToInt(c) % unit, $"{what}: a partition between two windows of one flat");
                    }
                    for (int i = 1; i < n; i++)
                    {
                        Assert.Greater(edges[i] - edges[i - 1], columns.Size * 0.5f, $"{what}: a flat narrower than half a window");
                    }
                }
            }
        }

        [Test]
        public void AnApartmentBlockHasABalconyOnEveryFloorAboveTheStreetNearAndFar()
        {
            // The far version keeps the slabs, so a block's stripes do not appear as it comes into full detail.
            int built = 0;
            foreach (BuildingSite site in Sites)
            {
                ApartmentFront front = FrontOf(site);
                if (!front.Balconies || !FreeStanding(site))
                {
                    continue;
                }
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                float halfDepth = site.Params.Depth * 0.5f;
                foreach (bool far in new[] { false, true })
                {
                    var structures = new MeshBuffer();
                    Build(site, far, structures, out Vector3 origin);
                    var found = new bool[rows.Count + 1];
                    for (int i = 0; i < structures.Vertices.Count; i++)
                    {
                        if (structures.Normals[i].y < 0.99f)
                        {
                            continue;
                        }
                        Vector3 v = structures.Vertices[i] + origin;
                        site.WorldToLocal(v.x, v.z, out _, out double lz);
                        // The slab's outer edge, the balcony's depth out from the back wall.
                        if (System.Math.Abs(lz + halfDepth + front.Depth) > 0.02)
                        {
                            continue;
                        }
                        float rel = v.y - site.BaseHeight;
                        int r = Mathf.RoundToInt(rel / rows.Size);
                        if (r >= 0 && r <= rows.Count && Mathf.Abs(rel - r * rows.Size) < 0.05f)
                        {
                            found[r] = true;
                        }
                    }
                    // A balcony on every FLOOR (a storey is two window rows), and the eave over the top one; none on the row half way up a storey
                    // (owner, 2026-10-15: its door "stacked on top" of the floor's in one room).
                    int floors = BuildingInterior.FloorCountFor(site.Params.Height);
                    for (int r = 1; r <= rows.Count; r++)
                    {
                        bool floorRow = r % BuildingInterior.StoreyHeightRows == 0 && r / BuildingInterior.StoreyHeightRows < floors;
                        bool eave = r == rows.Count;
                        Assert.AreEqual(floorRow || eave, found[r], $"building {site.Id} ({(far ? "far" : "near")}): {(floorRow || eave ? "no balcony slab" : "a balcony slab")} on row line {r} of {rows.Count}");
                    }
                    Assert.IsFalse(found[0], $"building {site.Id}: a balcony slab in front of the street floor");
                }
                if (++built >= 6)
                {
                    break;
                }
            }
            Assert.That(built, Is.GreaterThan(0), "no free-standing apartment block with balconies near spawn");
        }

        [Test]
        public void UnderAWalkwayTheDoorHasNoCanopyOfItsOwn()
        {
            // The walkway is the shelter; a canopy under it stood on the walkway's floor. Its underside was at the door's top + 0.12 m.
            int built = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 40))   // walkway blocks are a minority of a minority: look widely
            {
                if (!FrontOf(site).Walkway || !FreeStanding(site))
                {
                    continue;
                }
                var structures = new MeshBuffer();
                Build(site, false, structures, out Vector3 origin);
                float canopy = site.BaseHeight + BuildingInterior.DoorHeight(site.Params.Height) + 0.12f;
                float halfDepth = site.Params.Depth * 0.5f;
                for (int i = 0; i < structures.Vertices.Count; i++)
                {
                    Vector3 v = structures.Vertices[i] + origin;
                    if (structures.Normals[i].y > -0.99f || Mathf.Abs(v.y - canopy) > 0.02f)
                    {
                        continue;
                    }
                    site.WorldToLocal(v.x, v.z, out double lx, out double lz);
                    Assert.IsFalse(lz > halfDepth + 0.05 && lz < halfDepth + 1.3 && System.Math.Abs(lx) < BuildingInterior.DoorWidth,
                        $"building {site.Id}: an entrance canopy under its walkway at ({lx:F2}, {lz:F2})");
                }
                if (++built >= 6)
                {
                    break;
                }
            }
            Assert.That(built, Is.GreaterThan(0), "no free-standing apartment block with a walkway near spawn");
        }

        [Test]
        public void NothingHangsThroughABalconyFloor()
        {
            // An air conditioner hangs a metre under its window; on a balcony it stands on the floor. Under a low window it used to hang down
            // through the slab and out of its underside.
            Color32 appliance = SurfaceLook.Pack(new Color(0.86f, 0.87f, 0.88f), (int)WallMaterial.Metal, 0.55f);
            int units = 0, built = 0;
            // Homes away from downtown: the zones where air conditioners hang (BuildingLook.AcDensity).
            foreach (BuildingSite site in TestSites.NearSpawn(World, 40))
            {
                if (site.Zone == ZoneKind.Core || !FrontOf(site).Balconies || !FreeStanding(site)
                    || BuildingLook.For(site, WindowPlan.For(World, site).Style).AcDensity <= 0.0f)
                {
                    continue;
                }
                var structures = new MeshBuffer();
                Build(site, false, structures, out Vector3 origin);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                float halfDepth = site.Params.Depth * 0.5f;
                built++;
                for (int i = 0; i < structures.Vertices.Count; i++)
                {
                    Color32 c = structures.Colors[i];
                    if (c.r != appliance.r || c.g != appliance.g || c.b != appliance.b || c.a != appliance.a)
                    {
                        continue;
                    }
                    Vector3 v = structures.Vertices[i] + origin;
                    site.WorldToLocal(v.x, v.z, out _, out double lz);
                    if (lz > -halfDepth + 0.01)
                    {
                        continue;   // not on the balcony side
                    }
                    units++;
                    float rel = v.y - site.BaseHeight;
                    float above = rel - Mathf.Floor(rel / rows.Size + 0.001f) * rows.Size;
                    Assert.That(above, Is.LessThan(1.0f), $"building {site.Id}: an air conditioner {rows.Size - above:F2} m under a balcony floor");
                }
                if (units > 200 || built > 80)
                {
                    break;
                }
            }
            Debug.Log($"[ApartmentFront] {units / 8} air conditioners checked on {built} balconied blocks");
            Assert.That(units, Is.GreaterThan(0), "no air conditioner on any balcony: nothing was checked");
        }
    }
}
