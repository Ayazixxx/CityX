using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Footbridges between buildings (World/Footbridges, View/Buildings/CityMeshBuilder.Footbridges.cs; the layered city, docs/LAYERED_CITY_PLAN.md):
    /// bridges from a floor of one block across the street into a floor of the block facing it, both ends agreeing, level with the floors,
    /// clear of the ground, with a doorway at each end in place of a window, and a deck drawn where the bridge says it is.
    /// </summary>
    public sealed class FootbridgeTests
    {
        private const int Seed = 19077151;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<Footbridge> BridgesNearSpawn(int chunks)
        {
            var seen = new HashSet<(int, int, int)>();
            var bridges = new List<Footbridge>();
            foreach (BuildingSite site in TestSites.NearSpawn(World, chunks))
            {
                foreach (Footbridge b in Footbridges.For(World, site))
                {
                    if (seen.Add((b.SiteA, b.SiteB, b.Index))) bridges.Add(b);
                }
            }
            return bridges;
        }

        [Test]
        public void BridgesJoinFacingBlocksLevelWithTheirFloorsAndClearOfTheStreet()
        {
            List<Footbridge> bridges = BridgesNearSpawn(50);
            var byZone = new Dictionary<ZoneKind, int>();
            foreach (Footbridge b in bridges)
            {
                BuildingSite a = World.Building(b.SiteA), c = World.Building(b.SiteB);
                string what = $"bridge {b.SiteA}->{b.SiteB} ({a.Zone}, floors {b.FloorA}/{b.FloorB})";
                Assert.Less(b.SiteA, b.SiteB, $"{what}: the lower-numbered building decides");
                Assert.IsTrue(Footbridges.Eligible(a) && Footbridges.Eligible(c), $"{what}: an end on a building that cannot carry one");
                Assert.That(b.Span, Is.InRange(Footbridges.MinSpan, Footbridges.MaxSpan), $"{what}: span {b.Span:F1} m");
                // The ends' floors no further apart than a deck takes (it slopes), or the bridge's flights (2026-10-16).
                Assert.That(Mathf.Abs(b.StartWorld.y - b.EndWorld.y), Is.LessThanOrEqualTo((b.Path.HasStairs ? Footbridges.MaxStairRise : Footbridges.MaxRise) + 1e-3f), $"{what}: the ends' floors differ too much");
                // Level with each building's floor.
                float storeyA = BuildingInterior.StoreyHeight(a.Params.Height), storeyB = BuildingInterior.StoreyHeight(c.Params.Height);
                Assert.AreEqual(a.BaseHeight + b.FloorA * storeyA, b.StartWorld.y, 1e-3f, $"{what}: not level with A's floor");
                Assert.AreEqual(c.BaseHeight + b.FloorB * storeyB, b.EndWorld.y, 1e-3f, $"{what}: not level with B's floor");
                // The ends on the walls' faces.
                a.WorldToLocal(b.StartWorld.x, b.StartWorld.z, out double ax, out double az);
                c.WorldToLocal(b.EndWorld.x, b.EndWorld.z, out double bx, out double bz);
                Assert.Less(OffWall(a, b.WallA, (float)ax, (float)az), 0.02f, $"{what}: the start is not on A's wall");
                Assert.Less(OffWall(c, b.WallB, (float)bx, (float)bz), 0.02f, $"{what}: the end is not on B's wall");
                // Clear of the street under the middle.
                Vector2 midPlan = b.Path.PlanAt(b.Path.Length * 0.5f, out _);
                var mid = new Vector3(midPlan.x, b.Path.HeightAt(b.Path.Length * 0.5f), midPlan.y);   // the path's own middle: a curve or a dog-leg is not straight
                float ground = (float)World.Plan.GroundHeight(mid.x, mid.z);
                Assert.That(mid.y - Footbridges.DeckThickness - ground, Is.GreaterThan(Footbridges.GroundClearance - 0.5f), $"{what}: too low over the ground");
                // Both ends agree: B finds the same bridge.
                bool found = false;
                foreach (Footbridge other in Footbridges.For(World, c))
                {
                    found |= other.SiteA == b.SiteA && other.SiteB == b.SiteB && other.Index == b.Index && other.FloorA == b.FloorA;
                }
                Assert.IsTrue(found, $"{what}: the far building does not know the bridge");
                byZone[a.Zone] = (byZone.TryGetValue(a.Zone, out int n) ? n : 0) + 1;
            }
            Debug.Log($"[Footbridges] {bridges.Count} bridges near the spawn: " + string.Join(", ", byZone));
            Assert.That(bridges.Count, Is.GreaterThan(20), "too few bridges to check");
        }

        private static float OffWall(BuildingSite site, WindowGrid.Wall wall, float lx, float lz)
        {
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            switch (wall)
            {
                case WindowGrid.Wall.North: return Mathf.Abs(lz - hd);
                case WindowGrid.Wall.South: return Mathf.Abs(lz + hd);
                case WindowGrid.Wall.East: return Mathf.Abs(lx - hw);
                default: return Mathf.Abs(lx + hw);
            }
        }

        [Test]
        public void EachEndHasADoorwayInPlaceOfItsWindowAndTheDeckIsDrawn()
        {
            int checkedBridges = 0;
            foreach (Footbridge b in BridgesNearSpawn(40))
            {
                BuildingSite a = World.Building(b.SiteA), c = World.Building(b.SiteB);
                string what = $"bridge {b.SiteA}->{b.SiteB} floor {b.FloorA}";
                foreach (BuildingSite site in new[] { a, c })
                {
                    WindowPlan plan = WindowPlan.For(World, site);
                    var doors = new List<BalconyDoor>();
                    Footbridges.DoorsOf(World, site, plan, doors);
                    b.EndOn(site.Id, out WindowGrid.Wall wall, out int floor, out int column, out float u);
                    int row = floor * BuildingInterior.StoreyHeightRows;
                    BalconyDoor? door = null;
                    foreach (BalconyDoor d in doors)
                    {
                        if (d.Wall == wall && d.Row == row && d.Column == column) door = d;
                    }
                    Assert.IsTrue(door.HasValue, $"{what}: building {site.Id} has no doorway for it");
                    Assert.IsTrue(door.Value.Opens, $"{what}: building {site.Id}'s doorway does not open");
                    Assert.That(door.Value.Height, Is.GreaterThanOrEqualTo(2.7f), $"{what}: a doorway a 2.6 m body does not fit");
                    Assert.That(door.Value.Width, Is.GreaterThanOrEqualTo(BalconyDoors.MinSingleWidth - 1e-3f), $"{what}: a doorway too narrow");
                    // The doorway is where the deck meets the wall (a door's U0 runs from the building's centre, the bridge's u from the wall's start).
                    float length = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South ? site.Params.Width : site.Params.Depth;
                    Assert.That(Mathf.Abs((door.Value.U0 + door.Value.U1) * 0.5f - (u - length * 0.5f)), Is.LessThan(0.01f), $"{what}: the doorway is not where the deck meets the wall");
                    // The window cell is a door cell to the glass (no pane to shoot).
                    CellLayout columns = plan.ColumnsFor(length);
                    Footbridges.For(World, site);
                    Assert.IsTrue(GlassDamage.IsDoorCell(site, wall, columns, column, row), $"{what}: building {site.Id}'s doorway cell still counts as a window");
                }
                // The deck is drawn by A: a vertical line through the bridge's middle crosses A's structures.
                var structures = new MeshBuffer();
                var origin = new Vector3(a.OwnerChunkX * CityLayout.ChunkSize, 0, a.OwnerChunkY * CityLayout.ChunkSize);
                CityMeshBuilder.BuildSite(World, a, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), null, null,
                    new MeshBuffer(), false);
                Vector2 midPlan = b.Path.PlanAt(b.Path.Length * 0.5f, out _);
                Vector3 mid = new Vector3(midPlan.x, b.Path.HeightAt(b.Path.Length * 0.5f), midPlan.y) - origin;
                Assert.IsTrue(Crosses(structures, mid + Vector3.up * 0.5f, mid - Vector3.up * 0.6f), $"{what}: no deck drawn at the bridge's middle");
                if (++checkedBridges >= 10) break;
            }
            Assert.That(checkedBridges, Is.GreaterThan(3), "too few bridges to check");
        }

        private static bool Crosses(MeshBuffer b, Vector3 p0, Vector3 p1)
        {
            Vector3 dir = p1 - p0;
            for (int i = 0; i + 2 < b.Triangles.Count; i += 3)
            {
                Vector3 a = b.Vertices[b.Triangles[i]];
                Vector3 e1 = b.Vertices[b.Triangles[i + 1]] - a, e2 = b.Vertices[b.Triangles[i + 2]] - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1.0f / det;
                Vector3 s = p0 - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0.0f || u > 1.0f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0.0f || u + v > 1.0f) continue;
                float t = Vector3.Dot(e2, q) * inv;
                if (t >= 0.0f && t <= 1.0f) return true;
            }
            return false;
        }
    }
}
