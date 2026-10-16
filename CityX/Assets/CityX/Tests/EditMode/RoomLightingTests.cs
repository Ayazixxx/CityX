using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Room lights (World.RoomLighting): every room has a switch by its door and lights on its ceiling, in several patterns; one building in ten has
    /// power; switches are shared state (World.RoomLights) that a session keeps the same on every machine (Character.RoomLightsNetwork).
    /// </summary>
    public sealed class RoomLightingTests
    {
        private const int Seed = 46283197;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        [TearDown]
        public void TearDown()
        {
            RoomLights.Clear();
            RoomLights.Relay = null;
            PowerGrid.Reset();
        }

        private static IEnumerable<BuildingSite> Interiors(int count)
        {
            int n = 0;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 12))
            {
                if (s.Shell || s.Sides >= 3 || s.Params.IsSpecial)   // a garage, car park or mall has no rooms
                {
                    continue;
                }
                yield return s;
                if (++n >= count)
                {
                    yield break;
                }
            }
        }

        [Test]
        public void OneBuildingInTenHasPower()
        {
            int all = 0, powered = 0;
            foreach (BuildingSite s in World.Plan.Buildings)
            {
                all++;
                if (PowerGrid.HasPowerFromTheStart(s)) powered++;
            }
            double share = powered / (double)all;
            Assert.That(share, Is.InRange(0.08, 0.12), $"{powered} of {all} buildings have power");
        }

        [Test]
        public void RestoringPowerTurnsABuildingOn()
        {
            foreach (BuildingSite s in World.Plan.Buildings)
            {
                if (PowerGrid.IsOn(s)) continue;
                PowerGrid.Restore(s.Id);
                Assert.IsTrue(PowerGrid.IsOn(s));
                return;
            }
            Assert.Fail("every building already had power");
        }

        [Test]
        public void EveryRoomHasASwitchAndLightsAboveHeadHeight()
        {
            int rooms = 0;
            foreach (BuildingSite site in Interiors(60))
            {
                List<RoomLightPlan> plans = RoomLighting.PlansFor(site);
                Assert.GreaterOrEqual(plans.Count, BuildingInterior.FloorCountFor(site.Params.Height), $"building {site.Id}: a floor with no lit room");
                float storey = BuildingInterior.StoreyHeight(site.Params.Height);
                foreach (RoomLightPlan plan in plans)
                {
                    rooms++;
                    float floorY = site.BaseHeight + plan.Key.Floor * storey;
                    Assert.Greater(plan.Fixtures.Count, 0, $"{plan.Key}: a room with no light in it");
                    Assert.LessOrEqual(plan.Fixtures.Count, RoomLighting.MaxFixtures);
                    Assert.Greater(plan.LightPoints.Count, 0, $"{plan.Key}: its fittings would light nothing");
                    foreach (Fixture f in plan.Fixtures)
                    {
                        Assert.Greater(f.HousingMin.y, floorY + BodyRig.StandingHeight + 0.1f, $"{plan.Key}: a light hangs where a 2.6 m player walks into it");
                        Assert.LessOrEqual(Mathf.Abs(f.HousingMin.x), site.Params.Width * 0.5f, $"{plan.Key}: a light outside the building");
                        Assert.LessOrEqual(Mathf.Abs(f.HousingMin.z), site.Params.Depth * 0.5f, $"{plan.Key}: a light outside the building");
                    }
                    Assert.AreEqual(floorY + RoomLighting.SwitchHeight, plan.Switch.y, 0.01f, $"{plan.Key}: the switch is not at hand height");
                    Assert.LessOrEqual(Mathf.Abs(plan.Switch.x), site.Params.Width * 0.5f);
                    Assert.LessOrEqual(Mathf.Abs(plan.Switch.z), site.Params.Depth * 0.5f);
                }
            }
            Assert.Greater(rooms, 200, "need a real sample");
        }

        [Test]
        public void TheCityHasManyKindsOfLighting()
        {
            // Across the whole city, not round the spawn: a district has its own few kinds (a downtown spawn sees only an office's three).
            var seen = new HashSet<LightPattern>();
            int stride = Mathf.Max(1, World.BuildingCount / 300), sampled = 0;
            for (int id = 0; id < World.BuildingCount && sampled < 300; id += stride)
            {
                BuildingSite site = World.Building(id);
                if (site.Shell || site.Sides >= 3 || site.Params.IsSpecial) continue;
                sampled++;
                foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
                {
                    seen.Add(plan.Pattern);
                }
            }
            Assert.GreaterOrEqual(seen.Count, 4, $"only {string.Join(", ", seen)}");
        }

        [Test]
        public void NoLightHangsOverTheStairHole()
        {
            foreach (BuildingSite site in Interiors(40))
            {
                BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
                {
                    StairPlan flight = stairs.Floors[plan.Key.Floor];
                    if (flight == null) continue;
                    foreach (Fixture f in plan.Fixtures)
                    {
                        var foot = Rect.MinMaxRect(f.HousingMin.x, f.HousingMin.z, f.HousingMax.x, f.HousingMax.z);
                        Assert.IsFalse(foot.Overlaps(flight.Hole), $"{plan.Key}: a light over the hole the stairs come up through");
                    }
                }
            }
        }

        [Test]
        public void AFlipIsSharedThroughTheSession()
        {
            var relay = new RecordingRelay();
            RoomLights.Relay = relay;
            var key = new RoomKey(3, 1, 0);
            var heard = new List<bool>();
            System.Action<RoomKey, bool> listen = (k, on) => { if (k.Equals(key)) heard.Add(on); };
            RoomLights.Changed += listen;
            try
            {
                RoomLights.Flip(key);
                Assert.IsTrue(RoomLights.IsOn(key));
                RoomLights.Set(key, true);   // the server's echo changes nothing
                RoomLights.Flip(key);
                Assert.IsFalse(RoomLights.IsOn(key));
                CollectionAssert.AreEqual(new[] { true, false }, heard);
                CollectionAssert.AreEqual(new[] { true, false }, relay.Sent);
            }
            finally
            {
                RoomLights.Changed -= listen;
            }
        }

        [Test]
        public void AnEnclosedStairwellHasItsOwnLightAndASwitchInside()
        {
            // Owner, 2026-10-10: "the rooms with the stairwells should have lights and switches in there".
            int stairwells = 0, lit = 0;
            foreach (BuildingSite site in Interiors(200))
            {
                BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                List<RoomLightPlan> plans = RoomLighting.PlansFor(site);
                for (int floor = 0; floor < stairs.Floors.Length; floor++)
                {
                    StairPlan flight = stairs.Floors[floor];
                    if (flight == null || !flight.Enclosed) continue;
                    stairwells++;
                    RoomLightPlan plan = plans.Find(p => p.Key.Floor == floor && p.Key.Room == RoomLighting.StairwellRoom);
                    if (plan == null) continue;
                    lit++;
                    Rect b = flight.Bounds;
                    Assert.IsTrue(b.Contains(new Vector2(plan.Switch.x, plan.Switch.z)), $"{plan.Key}: its switch is outside the stairwell");
                    foreach (Fixture f in plan.Fixtures)
                    {
                        var foot = Rect.MinMaxRect(f.HousingMin.x, f.HousingMin.z, f.HousingMax.x, f.HousingMax.z);
                        Assert.IsTrue(b.Overlaps(foot), $"{plan.Key}: its light is not in the stairwell");
                    }
                }
            }
            Assert.Greater(stairwells, 10, "need some enclosed stairwells");
            Assert.Greater(lit, stairwells * 0.7f, $"only {lit} of {stairwells} stairwells have a light");
        }

        [Test]
        public void TheServerRefusesARoomThatCannotExist()
        {
            Assert.IsTrue(RoomLightsNetwork.Acceptable(new LightSwitchMessage { Building = 0, Floor = 2, Room = 1, On = true }));
            Assert.IsFalse(RoomLightsNetwork.Acceptable(new LightSwitchMessage { Building = -1, Floor = 0, Room = 0 }));
            Assert.IsTrue(RoomLightsNetwork.Acceptable(new LightSwitchMessage { Building = 0, Floor = 1, Room = RoomLighting.StairwellRoom, On = true }), "a stairwell's switch");
            Assert.IsFalse(RoomLightsNetwork.Acceptable(new LightSwitchMessage { Building = 0, Floor = 0, Room = 7 }));
            Assert.IsFalse(RoomLightsNetwork.Acceptable(new LightSwitchMessage { Building = 0, Floor = 100000, Room = 0 }));
        }

        [Test]
        public void OnlyAPoweredBuildingGetsLightsThatCanComeOn()
        {
            BuildingSite powered = default, dead = default;
            foreach (BuildingSite site in Interiors(400))
            {
                if (PowerGrid.IsOn(site) && !powered.Exists) powered = site;
                if (!PowerGrid.IsOn(site) && !dead.Exists) dead = site;
            }
            Assert.IsTrue(powered.Exists && dead.Exists, "need one of each");
            Assert.Greater(Glows(powered).Count, 0, "a powered building has no lights to switch on");
            Assert.AreEqual(0, Glows(dead).Count, "a building without power has lights that could come on");
        }

        [Test]
        public void ALitRoomsWindowsGlowAndOnlyItsOwn()
        {
            // From the street, a switched-on room shows as its windows lit: each room carries the panes in front of it, on its own floor.
            BuildingSite powered = default;
            foreach (BuildingSite site in Interiors(400))
            {
                if (PowerGrid.IsOn(site) && site.Params.Height > 12.0f) { powered = site; break; }
            }
            Assert.IsTrue(powered.Exists, "need a powered building");
            List<CityMeshBuilder.RoomGlow> glows = Glows(powered);
            int withWindows = 0;
            float storey = BuildingInterior.StoreyHeight(powered.Params.Height);
            int floors = BuildingInterior.FloorCountFor(powered.Params.Height);
            var origin = new Vector3(powered.OwnerChunkX * CityLayout.ChunkSize, 0, powered.OwnerChunkY * CityLayout.ChunkSize);
            foreach (CityMeshBuilder.RoomGlow glow in glows)
            {
                if (glow.Windows.IsEmpty) continue;
                withWindows++;
                float floorY = powered.BaseHeight + glow.Key.Floor * storey;
                // Up to its ceiling: the top floor takes what is left over above the last whole storey (seed 52817364's building 80).
                float ceilingY = powered.BaseHeight + BuildingInterior.FloorTop(Mathf.Max(0, glow.Key.Floor), floors, powered.Params.Height, storey);
                foreach (Vector3 v in glow.Windows.Vertices)
                {
                    float y = v.y + origin.y;
                    Assert.That(y, Is.InRange(floorY - 0.1f, ceilingY + 0.6f), $"{glow.Key}: a window of another floor lights with this room");
                }
            }
            Assert.Greater(withWindows, 0, "no room of a powered building lights its windows");
        }

        [Test]
        public void TheSwitchByTheDoorIsTheOneYouFlip()
        {
            BuildingSite site = default;
            foreach (BuildingSite s in Interiors(10)) { site = s; break; }
            RoomLightPlan hall = RoomLighting.PlansFor(site)[0];
            site.LocalToWorld(hall.Switch.x, hall.Switch.z + 1.2f * hall.SwitchFacing.z, out double fx, out double fz);
            var feet = new Vector3((float)fx, site.BaseHeight, (float)fz);
            site.LocalToWorld(hall.Switch.x, hall.Switch.z, out double sx, out double sz);
            var at = new Vector3((float)sx, hall.Switch.y, (float)sz);
            Vector3 eye = feet + Vector3.up * 2.2f;
            Assert.IsTrue(LightSwitchInteractor.TryFind(World, feet, eye, (at - eye).normalized, out BuildingSite found, out RoomLightPlan plan),
                "standing at the switch, looking at it, nothing to flip");
            Assert.AreEqual(site.Id, found.Id);
            Assert.AreEqual(hall.Key, plan.Key);
            Assert.IsFalse(LightSwitchInteractor.TryFind(World, feet, eye, -(at - eye).normalized, out _, out _), "flipped a switch behind the player");
        }

        private static List<CityMeshBuilder.RoomGlow> Glows(BuildingSite site)
        {
            var glows = new List<CityMeshBuilder.RoomGlow>();
            var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, site, origin, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                null, null, new MeshBuffer(), false, glows);
            return glows;
        }

        private sealed class RecordingRelay : IRoomLightRelay
        {
            public readonly List<bool> Sent = new List<bool>();
            public void Send(RoomKey key, bool on) => Sent.Add(on);
        }
    }
}
