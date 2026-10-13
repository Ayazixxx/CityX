using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Room lights in a real streamed chunk: a powered building's rooms come with lights that are off until their switch is flipped, and flipping it
    /// (World.RoomLights, which a session shares) turns its real lights and glowing fittings on; a building without power has none to turn on.
    /// </summary>
    public sealed class RoomLightsPlayTests
    {
        private const int Seed = 61470385;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            RoomLights.Clear();
            DayNightCycle.Daylight = 1.0f;
        }

        [UnityTest]
        public IEnumerator FlippingAPoweredRoomsSwitchLightsItAndFlippingItAgainPutsItOut()
        {
            var world = new CityWorld(Seed);
            BuildingSite powered = default;
            foreach (BuildingSite s in TestSites.NearSpawn(world, 14))
            {
                if (!s.Shell && s.Sides < 3 && PowerGrid.IsOn(s))
                {
                    powered = s;
                    break;
                }
            }
            Assert.IsTrue(powered.Exists, "no powered building near the spawn");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 1;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            var centre = new Vector3((float)powered.CentreX, powered.BaseHeight, (float)powered.CentreZ);
            streamer.StreamAround(centre, int.MaxValue);
            yield return null;

            var groups = new List<RoomLightGroup>();
            foreach (RoomLightGroup g in go.GetComponentsInChildren<RoomLightGroup>(true))
            {
                if (g.Key.Building == powered.Id) groups.Add(g);
            }
            Assert.Greater(groups.Count, 0, "the powered building's rooms have no lights");
            RoomLightGroup room = groups[0];
            Light[] lights = room.GetComponentsInChildren<Light>(true);
            Assert.Greater(lights.Length, 0, "a lit room with no real light in it");
            Assert.IsFalse(room.IsLit);
            foreach (Light l in lights) Assert.IsFalse(l.enabled, "lights on before anyone flipped the switch");

            RoomLights.Flip(room.Key);
            yield return null;
            Assert.IsTrue(room.IsLit);
            foreach (Light l in lights)
            {
                Assert.IsTrue(l.enabled, "the switch is on and the room is still dark");
                Assert.AreEqual(LightType.Spot, l.type, "room lights are spots: a lamp shining down, its bounce shining up (a point light burns the ceiling)");
                Assert.AreNotEqual(0u, CityLightLayers.Of(l) & CityLightLayers.Interior,
                    "a room light not marked as inside shines through the building's walls onto the street");
            }
            Assert.Greater(room.Lamps.Length, 0);
            Assert.AreEqual(room.Lamps.Length, room.Bounces.Length);
            foreach (Light lamp in room.Lamps) Assert.Less(lamp.transform.forward.y, -0.99f, "a lamp that does not shine down");
            foreach (Light bounce in room.Bounces) Assert.Greater(bounce.transform.forward.y, 0.99f, "a bounce that does not shine up");

            // Standing in the room: its nearest lamps cast shadows (a few, never a bounce).
            RoomLightDirector.Direct(room.Centre + Vector3.down * 2.0f);
            int shadowed = 0;
            foreach (Light lamp in room.Lamps) if (lamp.shadows != LightShadows.None) shadowed++;
            foreach (Light bounce in room.Bounces) Assert.AreEqual(LightShadows.None, bounce.shadows, "a bounce casting a shadow");
            Assert.That(shadowed, Is.InRange(1, RoomLightDirector.MaxShadowedLamps), "the lamps over the player cast no shadows, or too many do");

            // Far away: the room still glows (its fittings, its windows) but its real lights are spent elsewhere.
            RoomLightDirector.Direct(room.Centre + Vector3.right * (RoomLightDirector.LightReachMetres + 50.0f));
            Assert.IsTrue(room.IsLit);
            Assert.IsFalse(room.LightsOn);
            foreach (Light l in lights)
            {
                Assert.IsFalse(l.enabled, "a far room's lights left on");
                Assert.AreEqual(LightShadows.None, l.shadows);
            }
            RoomLightDirector.Direct(room.Centre);
            Assert.IsTrue(room.LightsOn, "back in reach and still dark");

            RoomLights.Flip(room.Key);
            yield return null;
            Assert.IsFalse(room.IsLit);
            foreach (Light l in lights) Assert.IsFalse(l.enabled);
        }

        [UnityTest]
        public IEnumerator APoweredBuildingsDoorHasALampThatComesOnAtNightAndADeadOnesHasNone()
        {
            var world = new CityWorld(Seed);
            BuildingSite powered = default;
            foreach (BuildingSite s in TestSites.NearSpawn(world, 14))
            {
                if (EntranceLighting.Has(s))
                {
                    powered = s;
                    break;
                }
            }
            Assert.IsTrue(powered.Exists, "no powered building near the spawn");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 1;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            DayNightCycle.Daylight = 1.0f;
            streamer.StreamAround(new Vector3((float)powered.CentreX, powered.BaseHeight, (float)powered.CentreZ), int.MaxValue);
            yield return null;

            var lamps = new List<EntranceLamp>(go.GetComponentsInChildren<EntranceLamp>(true));
            int poweredDoors = 0;
            foreach (Transform chunk in go.transform)
            {
                string[] parts = chunk.name.Replace("Chunk ", "").Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int cx) || !int.TryParse(parts[1], out int cy)) continue;
                foreach (int id in world.BuildingsOwnedBy(cx, cy))
                {
                    if (EntranceLighting.Has(world.Building(id))) poweredDoors++;
                }
            }
            Assert.Greater(lamps.Count, 0, "a powered building with no lamp over its door");
            Assert.AreEqual(poweredDoors, lamps.Count, "a lamp over a door with no power behind it, or a powered door without one");

            EntranceLamp lamp = lamps[0];
            Assert.IsFalse(lamp.IsLit, "lit by day");
            Assert.IsFalse(lamp.Light.enabled);
            Assert.AreNotEqual(0u, CityLightLayers.Of(lamp.Light) & CityLightLayers.Exterior, "a door lamp not marked as outside lights the lobby through the wall");

            DayNightCycle.Daylight = 0.0f;
            yield return null;
            Assert.IsTrue(lamp.IsLit, "dark at night");
            Assert.IsTrue(lamp.Light.enabled);
            Assert.Less(lamp.Light.transform.forward.y, -0.8f, "it should shine down on the steps");
            DayNightCycle.Daylight = 1.0f;
        }
    }
}
