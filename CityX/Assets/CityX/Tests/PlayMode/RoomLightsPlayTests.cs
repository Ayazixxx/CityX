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
        private const int Seed = 61839025;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            RoomLights.Clear();
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
                Assert.AreEqual(LightShadows.None, l.shadows, "room lights must not cast shadows (a point light's shadow is six renders)");
            }

            RoomLights.Flip(room.Key);
            yield return null;
            Assert.IsFalse(room.IsLit);
            foreach (Light l in lights) Assert.IsFalse(l.enabled);
        }
    }
}
