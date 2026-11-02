using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Nothing on a facade stands in front of a footbridge's doorway (owner, 2026-10-16: "make it so the sliding glass door on a connecting bridge isnt
    /// being blocked by the buildings decals/architecture make those go away around the glass door"): the piers (FacadeOrnament) and the cast fins
    /// (BuildingDecor ribs) stop under a bridge's deck and start again over its door. Checked by looking at each door from the deck: rays from 1.5 m out
    /// over the deck, across the doorway's width and up its height, reach the wall's face without hitting anything on the way.
    /// </summary>
    public sealed class FootbridgeDoorwayTests
    {
        private const int Seed = 95998845;

        [Test]
        public void NothingOnTheFacadeStandsInFrontOfABridgeDoor()
        {
            var world = new CityWorld(Seed);
            var doors = new List<BalconyDoor>();
            int checkedDoors = 0, withRelief = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 40))
            {
                if (Footbridges.For(world, site).Length == 0) continue;
                doors.Clear();
                Footbridges.DoorsOf(world, site, WindowPlan.For(world, site), doors);
                if (doors.Count == 0) continue;
                FacadeOrnament o = FacadeOrnament.For(site, WindowPlan.For(world, site).Style, BuildingLook.For(world, site, WindowPlan.For(world, site).Style).Wall);
                bool relief = o.PierEveryColumns > 0 || BuildingDecor.Ribs(site.Seed, site.Params.Width, site.Params.Depth, site.Params.Height,
                    -BuildingInterior.EntranceWidth(site) * 0.5f, BuildingInterior.EntranceWidth(site) * 0.5f).Fins.Count > 0;
                var structures = new MeshBuffer();
                var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
                CityMeshBuilder.BuildSite(world, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), null, null, null, null, false);
                Footbridge[] bridges = Footbridges.For(world, site);
                foreach (BalconyDoor door in doors)
                {
                    BalconyDoors.InWorld(site, door, out Vector3 corner, out Vector3 along, out Vector3 outward);
                    checkedDoors++;
                    withRelief += relief ? 1 : 0;
                    // Along the deck back to the door (a bridge can leave its wall on the slant, 2026-10-16): from 1.5 m along it, it is 1.5 m to the wall.
                    Footbridge bridge = bridges[door.Unit - Footbridges.DoorUnitBase];
                    Vector3[] line = bridge.Path.Points;
                    Vector3 deck = site.Id == bridge.SiteA ? line[1] - line[0] : line[line.Length - 2] - line[line.Length - 1];
                    deck.y = 0.0f;
                    deck.Normalize();
                    const float start = 1.5f;
                    for (int i = 1; i <= 5; i++)
                    {
                        float u = door.Width * i / 6.0f;
                        for (int j = 1; j <= 5; j++)
                        {
                            float y = 0.2f + (door.Height - 0.35f) * j / 5.0f;
                            Vector3 from = corner + along * u + Vector3.up * y + deck * start;
                            if (MeshRays.Nearest(structures, from - origin, -deck, start - 0.04f, out float distance, out Vector3 a, out Vector3 b, out Vector3 c))
                            {
                                string Frame(Vector3 p)
                                {
                                    Vector3 d = p + origin - corner;
                                    return $"({Vector3.Dot(d, along):F2} along, {d.y:F2} up, {Vector3.Dot(d, outward):F2} out)";
                                }
                                ApartmentFront front = ApartmentFront.For(site, WindowPlan.For(world, site));
                                Assert.Fail($"building {site.Id} ({site.Params.Mass} {site.Zone}, pier every {o.PierEveryColumns}, relief {relief}, apartment front " +
                                    $"{front.Any} balconies {front.Balconies}): something stands {start - distance:F2} m out from the {door.Wall} wall in front of the " +
                                    $"bridge door (row {door.Row}, {door.Kind}), {u:F2} m along it and {y:F2} m up: a triangle {Frame(a)} {Frame(b)} {Frame(c)}");
                            }
                        }
                    }
                }
            }
            Debug.Log($"[FootbridgeDoorway] {checkedDoors} bridge doors looked at from their decks, {withRelief} on facades with piers or fins");
            Assert.That(checkedDoors, Is.GreaterThan(10), "too few bridge doors near the spawn to check");
        }
    }
}
