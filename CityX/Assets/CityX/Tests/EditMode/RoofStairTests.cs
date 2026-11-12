using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The stairs up the outside to the roofs (World/RoofStairs, docs/LAYERED_CITY_PLAN.md L3; owner, 2026-11-09: "improve upon ... layered
    /// chongqing look"): which blocks have one and on which wall, that it is a stair a body can climb from the paving to the roof, that it
    /// stands clear of the roads, of every other building and of every other stair, that the roof's parapet opens where it comes on and the
    /// facade keeps clear of it, and that every machine puts the same stair in the same place.
    /// </summary>
    public sealed class RoofStairTests
    {
        private const int Seed = 48398181;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<(BuildingSite Site, RoofStair Stair)> _stairs;

        private static List<(BuildingSite Site, RoofStair Stair)> Stairs
        {
            get
            {
                if (_stairs != null) return _stairs;
                _stairs = new List<(BuildingSite, RoofStair)>();
                foreach (BuildingSite site in TestSites.NearSpawn(World, 40))
                {
                    RoofStair stair = RoofStairs.For(World, site);
                    if (stair != null) _stairs.Add((site, stair));
                }
                return _stairs;
            }
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        private static float PavingAt(Vector3 p) => (float)World.Plan.GroundHeight(p.x, p.z) + (float)CityX.World.Plan.PlanBuildings.PavingRise;

        [Test]
        public void ManyBlocksHaveAStairUpToTheRoofOnASideOrBackWallAndOnlyBlocksThatCan()
        {
            int could = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 40))
            {
                if (RoofStairs.Eligible(site)) could++;
            }
            Debug.Log($"CityX roof stairs: {Stairs.Count} of the {could} blocks near the spawn that could have one");
            Assert.Greater(Stairs.Count, 20, "hardly a block near the spawn has a stair up its outside");
            Assert.Less(Stairs.Count, could * 0.7f, "nearly every block has one: a street of fire escapes");

            var walls = new HashSet<WindowGrid.Wall>();
            foreach ((BuildingSite site, RoofStair roof) in Stairs)
            {
                string what = $"building {site.Id}";
                Assert.IsTrue(RoofStairs.Eligible(site), $"{what} cannot have a stair to its roof and has one");
                Assert.IsTrue(RoofShape.For(site).HasParapet, $"{what}: a stair up to a roof with no parapet to open");
                Assert.AreNotEqual(WindowGrid.Wall.North, roof.Stair.Wall, $"{what}: the stair stands across the front");
                WindowPlan plan = WindowPlan.For(World, site);
                Assert.IsFalse(plan.IsBlankWall(roof.Stair.Wall), $"{what}: the stair is on a party wall");
                Assert.IsFalse(ApartmentFront.For(site, plan).Covers(roof.Stair.Wall), $"{what}: the stair is on a wall of balconies");
                Assert.AreEqual(site.Top, roof.Stair.Top, 0.01f, $"{what}: the stair does not top out at the roof");
                Assert.LessOrEqual(roof.Stair.Top - roof.Stair.Bottom, RoofStairs.MaxRise + 0.01f, $"{what}: the tower is higher than a stair to a roof climbs");
                Assert.AreEqual(RoofStairs.GapWidth, roof.GapU1 - roof.GapU0, 0.01f, $"{what}: the opening in the parapet is not a stair wide");
                walls.Add(roof.Stair.Wall);
            }
            Assert.GreaterOrEqual(walls.Count, 2, "every stair is on the same wall of its block");
        }

        [Test]
        public void EveryStairIsOneABodyClimbsFromThePavingOntoTheRoof()
        {
            foreach ((BuildingSite site, RoofStair roof) in Stairs)
            {
                string what = $"building {site.Id}";
                StreetStair s = roof.Stair;
                Assert.AreEqual(s.Points.Length - 1, s.Risers.Length, $"{what}: the walking line's stretches and their steps do not match");
                Assert.GreaterOrEqual(s.Flights.Length, 2, $"{what}: a tower of one flight");
                foreach (StairFlight f in s.Flights)
                {
                    float riser = (f.From.y - f.To.y) / f.Risers, tread = Vector2.Distance(Flat(f.From), Flat(f.To)) / f.Risers;
                    Assert.LessOrEqual(f.Risers, Footbridges.FlightMax, $"{what}: a flight of {f.Risers} steps with no landing");
                    Assert.That(riser, Is.InRange(0.1f, Footbridges.MaxRiser + 1e-3f), $"{what}: a step {riser:F3} m high");
                    Assert.AreEqual(Footbridges.Tread, tread, 0.005f, $"{what}: a tread {tread:F3} m deep");
                }
                // The walking line is unbroken, level between its flights, from the roof to the paving.
                for (int i = 1; i < s.Points.Length; i++)
                {
                    float fall = s.Points[i - 1].y - s.Points[i].y;
                    if (s.Risers[i - 1] == 0)
                    {
                        Assert.AreEqual(0.0f, fall, 0.01f, $"{what}: a landing that slopes");
                    }
                    else
                    {
                        Assert.Greater(fall, 0.15f, $"{what}: a flight that does not go down");
                    }
                }
                Vector3 top = s.Points[0], foot = s.Points[s.Points.Length - 1];
                Assert.AreEqual(site.Top, top.y, 0.01f, $"{what}: the walking line does not start at the roof's height");
                site.WorldToLocal(top.x, top.z, out double lx, out double lz);
                Assert.Less(System.Math.Abs(lx), site.Params.Width * 0.5 - 0.5, $"{what}: the walking line does not start on the roof");
                Assert.Less(System.Math.Abs(lz), site.Params.Depth * 0.5 - 0.5, $"{what}: the walking line does not start on the roof");
                Assert.AreEqual(PavingAt(foot), foot.y, 0.25f, $"{what}: the stair's foot is not on the paving (its foot {foot.y:0.00} at {foot.x:0.0},{foot.z:0.0}, the paving {PavingAt(foot):0.00})");
                Assert.AreEqual(s.Bottom, foot.y, 0.01f);
            }
        }

        [Test]
        public void NoStairStandsOnARoadInAnotherBuildingOrInAnotherStairsWay()
        {
            var towers = new List<(int Site, List<Vector3> Corners)>();
            foreach ((BuildingSite site, RoofStair roof) in Stairs)
            {
                string what = $"building {site.Id}";
                StreetStair s = roof.Stair;
                // Every corner of every landing, and every flight's two ends at both its edges.
                var corners = new List<Vector3>();
                foreach (StairLanding l in s.Landings)
                {
                    foreach (float a in new[] { -1.0f, 1.0f })
                    {
                        foreach (float o in new[] { -1.0f, 1.0f })
                        {
                            Vector2 p = Flat(l.Centre) + l.Along * (a * l.HalfAlong) + l.Out * (o * l.HalfOut);
                            corners.Add(new Vector3(p.x, l.Centre.y, p.y));
                        }
                    }
                }
                foreach (StairFlight f in s.Flights)
                {
                    Vector2 across = new Vector2(-(f.To.z - f.From.z), f.To.x - f.From.x).normalized * (s.Width * 0.5f);
                    foreach (Vector3 end in new[] { f.From, f.To })
                    {
                        corners.Add(end + new Vector3(across.x, 0.0f, across.y));
                        corners.Add(end - new Vector3(across.x, 0.0f, across.y));
                    }
                }
                foreach (Vector3 c in corners)
                {
                    Assert.IsFalse(Footbridges.OnACarriageway(World, c.x, c.z, 0.0), $"{what}: the stair stands on a road");
                    bool inAnother = World.TryBuildingAt(c.x, c.z, out BuildingSite other) && other.Id != site.Id;
                    site.WorldToLocal(c.x, c.z, out double lx, out double lz);
                    Assert.IsFalse(inAnother, $"{what}: the stair stands in building {other.Id} (at {lx:F2}, {lz:F2} of its own {site.Params.Width:F1} by " +
                        $"{site.Params.Depth:F1} m, {c.y - site.BaseHeight:F1} m up, on wall {s.Wall}; the other on a roof: {other.OnRoof}, a wing: {other.Wing})");
                }
                towers.Add((site.Id, corners));
            }
            // Two blocks' towers never stand in each other: every corner of one is clear of every corner of another by more than a rail.
            // (Only towers near enough to touch are gone through corner by corner: a tower is under 15 m from end to end.)
            const float near = 40.0f;
            for (int i = 0; i < towers.Count; i++)
            {
                for (int j = i + 1; j < towers.Count; j++)
                {
                    if (Vector2.Distance(Flat(towers[i].Corners[0]), Flat(towers[j].Corners[0])) > near) continue;
                    foreach (Vector3 a in towers[i].Corners)
                    {
                        foreach (Vector3 b in towers[j].Corners)
                        {
                            if (Vector2.Distance(Flat(a), Flat(b)) <= 0.5f)
                            {
                                Assert.Fail($"the stairs of buildings {towers[i].Site} and {towers[j].Site} stand in each other");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void TheParapetOpensWhereTheStairComesOntoTheRoofAndTheFacadeKeepsClearOfIt()
        {
            int looked = 0;
            foreach ((BuildingSite site, RoofStair roof) in Stairs)
            {
                string what = $"building {site.Id}";
                StreetStair s = roof.Stair;
                var clearings = new List<FacadeClearing>();
                Footbridges.ClearingsOn(site, s.Wall, clearings, 0.0f);
                Footbridges.WallFrame(site, s.Wall, out _, out _, out _, out float length);
                Assert.IsTrue(clearings.Exists(c => c.U0 <= s.U0 - length * 0.5f + 0.01f && c.U1 >= s.U1 - length * 0.5f - 0.01f),
                    $"{what}: the facade is not told to keep clear of the stair on its wall");

                if (looked >= 6) continue;
                looked++;
                var structures = new MeshBuffer();
                var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
                CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), null, null, null, null, false);
                // Out through the opening at knee and chest height: from the roof to the landing's outer rail, nothing of the parapet in the way.
                Vector3 onRoof = s.Points[0] - origin, onLanding = s.Points[1] - origin;
                foreach (float height in new[] { 0.5f, 1.0f })
                {
                    Assert.IsFalse(MeshRays.Crosses(structures, onRoof + Vector3.up * height, onLanding + Vector3.up * height),
                        $"{what}: the parapet is not open where the stair comes onto the roof ({height} m up)");
                }
                // The same line three metres along the wall, away from the stair: the parapet is there.
                Vector3 aside = new Vector3(s.Along.x, 0.0f, s.Along.y) * -(RoofStairs.GapWidth * 0.5f + 1.0f);
                Assert.IsTrue(MeshRays.Crosses(structures, onRoof + aside + Vector3.up * 0.5f, onLanding + aside + Vector3.up * 0.5f),
                    $"{what}: no parapet beside the opening: the roof's edge is open");
                // The landing outside the opening is drawn: something to stand on, level with the roof.
                Assert.IsTrue(MeshRays.Hits(structures, onLanding + Vector3.up * 0.5f, Vector3.down, 0.7f), $"{what}: no landing outside the opening");
                // And the first flight under the walking line a few steps down it.
                Vector3 downFlight = Vector3.Lerp(s.Flights[0].From, s.Flights[0].To, 0.5f) - origin;
                Assert.IsTrue(MeshRays.Hits(structures, downFlight + Vector3.up * 0.6f, Vector3.down, 1.0f), $"{what}: the first flight is not drawn");
            }
            Assert.Greater(looked, 0, "no stair was looked at");
        }

        [Test]
        public void EveryMachinePutsTheSameStairOnTheSameWall()
        {
            var first = new Dictionary<int, (WindowGrid.Wall Wall, float U0, int Flights)>();
            foreach ((BuildingSite site, RoofStair roof) in Stairs)
            {
                first[site.Id] = (roof.Stair.Wall, roof.GapU0, roof.Stair.Flights.Length);
            }
            // Worked out again from nothing, the far blocks first.
            Footbridges.Reset();
            var sites = new List<BuildingSite>(TestSites.NearSpawn(World, 40));
            sites.Reverse();
            int again = 0;
            foreach (BuildingSite site in sites)
            {
                RoofStair roof = RoofStairs.For(World, site);
                Assert.AreEqual(first.ContainsKey(site.Id), roof != null, $"building {site.Id} has a stair one time and not the other");
                if (roof == null) continue;
                again++;
                Assert.AreEqual(first[site.Id], (roof.Stair.Wall, roof.GapU0, roof.Stair.Flights.Length), $"building {site.Id}'s stair moved");
            }
            Assert.AreEqual(first.Count, again);
        }
    }
}
