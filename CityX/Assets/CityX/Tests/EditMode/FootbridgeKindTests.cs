using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The footbridges' kinds (owner, 2026-10-23, with a photo of Chongqing's enclosed bridges: "make multiple types of sky bridges ... more that
    /// are fully indoor like the buildings they stem from and have windows too ... No entry door needed ... roofs and long window like spaces ...
    /// no glass in the windows ... the buildings connected should be the same color"): all three built near the spawn; a corridor's mouth an open
    /// doorway its own width and height with nothing to open, and its two buildings wearing one look; drawn with a wall either side and a roof
    /// over the walking line, the enclosed one's long windows glazed and the breezeway's open to the air; the facade kept clear up to its roof.
    /// </summary>
    public sealed class FootbridgeKindTests
    {
        private const int Seed = 23481876;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<Footbridge> _bridges;

        private static List<Footbridge> Bridges
        {
            get
            {
                if (_bridges != null) return _bridges;
                var seen = new HashSet<(int, int, int)>();
                _bridges = new List<Footbridge>();
                foreach (BuildingSite site in TestSites.NearSpawn(World, 50))
                {
                    foreach (Footbridge b in Footbridges.For(World, site))
                    {
                        if (seen.Add((b.SiteA, b.SiteB, b.Index))) _bridges.Add(b);
                    }
                }
                return _bridges;
            }
        }

        private static string Name(in Footbridge b) => $"bridge {b.SiteA}->{b.SiteB} #{b.Index} ({b.Kind}, {b.Shape}, floors {b.FloorA}/{b.FloorB})";

        [Test]
        public void EveryKindIsBuiltNearTheSpawnAndACorridorHasHeadroomForABody()
        {
            var kinds = new Dictionary<BridgeKind, int>();
            foreach (Footbridge b in Bridges)
            {
                kinds[b.Kind] = (kinds.TryGetValue(b.Kind, out int n) ? n : 0) + 1;
                if (b.Kind == BridgeKind.Open)
                {
                    Assert.AreEqual(0.0f, b.Headroom, $"{Name(b)}: an open deck has no headroom");
                    continue;
                }
                Assert.That(b.Headroom, Is.InRange(Footbridges.MinHeadroom, Footbridges.MaxHeadroom), $"{Name(b)}: headroom {b.Headroom:F2} m");
                Assert.AreEqual(Footbridges.HeadroomFor(World.Building(b.SiteA), World.Building(b.SiteB)), b.Headroom, 1e-4f, $"{Name(b)}: not the pair's headroom");
                Assert.AreEqual(BridgeDeck.Concrete, b.Deck, $"{Name(b)}: a corridor's floor is a slab");
            }
            Debug.Log($"CityX footbridge kinds near the spawn: {Bridges.Count}; {string.Join(", ", kinds)}");
            foreach (BridgeKind kind in new[] { BridgeKind.Open, BridgeKind.Enclosed, BridgeKind.Breezeway })
            {
                Assert.IsTrue(kinds.ContainsKey(kind), $"no {kind} bridge near the spawn");
            }
        }

        [Test]
        public void ACorridorsMouthIsAnOpenDoorwayItsOwnSizeAndItsTwoBuildingsWearOneLook()
        {
            int corridors = 0;
            foreach (Footbridge b in Bridges)
            {
                if (b.Kind == BridgeKind.Open) continue;
                BuildingSite a = World.Building(b.SiteA), c = World.Building(b.SiteB);
                foreach (BuildingSite site in new[] { a, c })
                {
                    WindowPlan plan = WindowPlan.For(World, site);
                    var doors = new List<BalconyDoor>();
                    Footbridges.DoorsOf(World, site, plan, doors);
                    b.EndOn(site.Id, out WindowGrid.Wall wall, out int floor, out int column, out float u);
                    int row = floor * BuildingInterior.StoreyHeightRows;
                    BalconyDoor? mouth = null;
                    foreach (BalconyDoor d in doors)
                    {
                        if (d.Wall == wall && d.Row == row && d.Column <= column && column < d.Column + d.Columns) mouth = d;
                    }
                    string what = $"{Name(b)}, building {site.Id}";
                    Assert.IsTrue(mouth.HasValue, $"{what}: no mouth in its wall");
                    BalconyDoor door = mouth.Value;
                    Assert.AreEqual(BalconyDoorKind.Opening, door.Kind, $"{what}: the mouth is a door");
                    Assert.IsFalse(door.Opens, $"{what}: an open doorway has nothing to open");
                    Assert.AreEqual(Footbridges.CorridorWidth, door.Width, 1e-3f, $"{what}: the mouth is not the corridor's clear width");
                    Assert.AreEqual(b.Headroom, door.Height, 1e-3f, $"{what}: the mouth is not the corridor's headroom");
                    float length = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South ? site.Params.Width : site.Params.Depth;
                    Assert.That(Mathf.Abs((door.U0 + door.U1) * 0.5f - (u - length * 0.5f)), Is.LessThan(0.01f), $"{what}: the mouth is not where the corridor meets the wall");
                    // Every cell the mouth passes through has no window (no pane to shoot).
                    CellLayout columns = plan.ColumnsFor(length);
                    Footbridges.For(World, site);
                    for (int k = door.Column; k < door.Column + door.Columns; k++)
                    {
                        Assert.IsTrue(GlassDamage.IsDoorCell(site, wall, columns, k, row), $"{what}: cell {k} of the mouth still counts as a window");
                    }
                }
                BuildingLook la = BuildingLook.For(World, a, WindowPlan.For(World, a).Style), lc = BuildingLook.For(World, c, WindowPlan.For(World, c).Style);
                Assert.AreEqual(la.Wall, lc.Wall, $"{Name(b)}: the two buildings' walls are of different stuff");
                Assert.AreEqual(la.WallTint, lc.WallTint, $"{Name(b)}: the two buildings are different colours");
                Assert.AreEqual(la.BaseTint, lc.BaseTint, $"{Name(b)}: the two buildings' ground floors differ");
                Assert.AreEqual(la.BandTint, lc.BandTint, $"{Name(b)}: the two buildings' bands differ");
                Assert.AreEqual(la.RoofTint, lc.RoofTint, $"{Name(b)}: the two buildings' roofs differ");
                corridors++;
            }
            Debug.Log($"CityX corridors near the spawn: {corridors}, each with an open mouth at both ends and both buildings in one look");
            Assert.Greater(corridors, 3, "too few corridors to check");
        }

        [Test]
        public void ABuildingWithNoCorridorKeepsItsOwnLookAndAGroupWearsItsLowestNumbereds()
        {
            int own = 0, shared = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 50))
            {
                if (!Footbridges.Eligible(site)) continue;
                FacadeStyle style = WindowPlan.For(World, site).Style;
                BuildingSite from = Footbridges.SharesLookOf(World, site);
                BuildingLook look = BuildingLook.For(World, site, style);
                if (!from.Exists)
                {
                    Assert.AreEqual(BuildingLook.For(site, style).WallTint, look.WallTint, $"building {site.Id} with no corridor does not wear its own look");
                    own++;
                    continue;
                }
                Assert.Less(from.Id, site.Id, $"building {site.Id} wears a higher-numbered building's look");
                Assert.IsFalse(Footbridges.SharesLookOf(World, from).Exists, $"building {site.Id}'s look does not come from the lowest-numbered of its group");
                Assert.AreEqual(BuildingLook.For(from, WindowPlan.For(World, from).Style).WallTint, look.WallTint, $"building {site.Id} does not wear building {from.Id}'s look");
                shared++;
            }
            Debug.Log($"CityX looks near the spawn: {own} buildings in their own, {shared} wearing a corridor's far building's");
            Assert.Greater(own, 10);
            Assert.Greater(shared, 3);
        }

        [Test]
        public void ACorridorIsDrawnWithWallsAndARoofTheEnclosedOneGlazedTheBreezewayOpen()
        {
            bool enclosedSeen = false, breezewaySeen = false;
            foreach (Footbridge b in Bridges)
            {
                if (b.Kind == BridgeKind.Open || b.Path.HasStairs) continue;
                if (b.Kind == BridgeKind.Enclosed ? enclosedSeen : breezewaySeen) continue;
                BuildingSite a = World.Building(b.SiteA);
                var structures = new MeshBuffer();
                var glass = new MeshBuffer();
                var origin = new Vector3(a.OwnerChunkX * CityLayout.ChunkSize, 0, a.OwnerChunkY * CityLayout.ChunkSize);
                CityMeshBuilder.BuildSite(World, a, origin, structures, new MeshBuffer(), new MeshBuffer(), glass, null, null, null, null, false);
                string what = Name(b);
                BridgePath path = b.Path;
                float length = path.Length;
                Vector2 midPlan = path.PlanAt(length * 0.5f, out Vector2 dir);
                Vector3 mid = new Vector3(midPlan.x, path.HeightAt(length * 0.5f), midPlan.y) - origin;
                var side = new Vector3(-dir.y, 0.0f, dir.x);
                // The roof over the walking line at the headroom, and a wall either side at the corridor's clear half width.
                Assert.IsTrue(MeshRays.Nearest(structures, mid + Vector3.up, Vector3.up, b.Headroom + 0.5f, out float toRoof, out _, out _, out _), $"{what}: no roof");
                Assert.AreEqual(b.Headroom - 1.0f, toRoof, 0.05f, $"{what}: the roof is not at the headroom");
                foreach (float s in new[] { 1.0f, -1.0f })
                {
                    Assert.IsTrue(MeshRays.Nearest(structures, mid + Vector3.up * 0.5f, side * s, Footbridges.DeckWidth, out float toWall, out _, out _, out _), $"{what}: no wall on one side");
                    Assert.AreEqual(Footbridges.CorridorWidth * 0.5f, toWall, 0.1f, $"{what}: the wall is {toWall:F2} m from the middle");
                }
                // Through the window band: glass the whole way (and only the mullions solid) in the enclosed one; open air between the breezeway's piers.
                int samples = 0, glazed = 0, solid = 0;
                for (float along = length * 0.3f; along <= length * 0.7f; along += 0.25f)
                {
                    Vector2 x = path.PlanAt(along, out Vector2 d);
                    Vector3 at = new Vector3(x.x, path.HeightAt(along) + Footbridges.CorridorSill + 0.6f, x.y) - origin;
                    var across = new Vector3(-d.y, 0.0f, d.x);
                    samples++;
                    if (MeshRays.Hits(glass, at, across, Footbridges.DeckWidth)) glazed++;
                    if (MeshRays.Hits(structures, at, across, Footbridges.DeckWidth)) solid++;
                }
                if (b.Kind == BridgeKind.Enclosed)
                {
                    Assert.AreEqual(samples, glazed, $"{what}: the long window is not glazed all along");
                    Assert.Less(solid, samples * 0.3f, $"{what}: the window band is mostly wall");
                    enclosedSeen = true;
                }
                else
                {
                    Assert.AreEqual(0, glazed, $"{what}: a breezeway's windows have glass in them");
                    Assert.Less(solid, samples * 0.5f, $"{what}: a breezeway's windows are mostly pier");
                    breezewaySeen = true;
                }
                Debug.Log($"CityX {what}: roof {toRoof + 1.0f:F2} m up, {glazed}/{samples} samples through glass, {solid} through wall");
            }
            Assert.IsTrue(enclosedSeen, "no enclosed bridge without a flight near the spawn to look at");
            Assert.IsTrue(breezewaySeen, "no breezeway without a flight near the spawn to look at");
        }

        [Test]
        public void TheFacadeKeepsClearUpToACorridorsRoof()
        {
            int checkedMouths = 0;
            var clearings = new List<FacadeClearing>();
            foreach (Footbridge b in Bridges)
            {
                if (b.Kind == BridgeKind.Open) continue;
                foreach (int id in new[] { b.SiteA, b.SiteB })
                {
                    BuildingSite site = World.Building(id);
                    Footbridges.For(World, site);
                    b.EndOn(id, out WindowGrid.Wall wall, out int floor, out _, out float u);
                    float length = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South ? site.Params.Width : site.Params.Depth;
                    float centre = u - length * 0.5f, y = floor * BuildingInterior.StoreyHeight(site.Params.Height);
                    clearings.Clear();
                    Footbridges.ClearingsOn(site, wall, clearings, 0.0f);
                    bool covered = false;
                    foreach (FacadeClearing cl in clearings)
                    {
                        covered |= cl.U0 <= centre - Footbridges.CorridorWidth * 0.5f && cl.U1 >= centre + Footbridges.CorridorWidth * 0.5f
                            && cl.Y0 <= y - Footbridges.DeckThickness + 0.01f && cl.Y1 >= y + b.Headroom + Footbridges.CorridorRoofThickness - 0.01f;
                    }
                    Assert.IsTrue(covered, $"{Name(b)}: building {id}'s facade is not kept clear up to the corridor's roof");
                    checkedMouths++;
                }
            }
            Assert.Greater(checkedMouths, 6);
        }
    }
}
