using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Room lights in a real streamed chunk: a powered building's rooms come with lights that are off until their switch is flipped, and flipping it
    /// (World.RoomLights, which a session shares) turns its real lights and glowing fittings on; a building without power has none to turn on.
    /// And what made a powered block cheap to stream in (2026-10-14): nothing is made for a room until its switch is on -- its real lights are lent
    /// from RoomLightDirector's pool, never more than its budget, and go back when the room goes dark or its chunk goes; its glow and windows are
    /// drawn with its chunk's other lit rooms of the same colour (ChunkRoomLights).
    /// </summary>
    public sealed class RoomLightsPlayTests
    {
        private const int Seed = 80469103;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FillsId = Shader.PropertyToID("_CityXRoomFills");
        private static readonly int FillCountId = Shader.PropertyToID("_CityXRoomFillCount");
        private readonly List<GameObject> _made = new List<GameObject>();
        private CityScale _scaleBefore;
        private bool _scaleChanged;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            RoomLights.Clear();
            DayNightCycle.Daylight = 1.0f;
            if (_scaleChanged)
            {
                CityScale.Active = _scaleBefore;
                _scaleChanged = false;
            }
        }

        private static BuildingSite PoweredNearSpawn(CityWorld world, int minRooms = 1)
        {
            foreach (BuildingSite s in TestSites.NearSpawn(world, 14))
            {
                if (!s.Shell && s.Sides < 3 && PowerGrid.IsOn(s) && RoomLighting.PlansFor(s).Count >= minRooms)
                {
                    return s;
                }
            }
            return default;
        }

        /// <summary>A streamer with the chunks round a building on screen, built here and now (no worker threads).</summary>
        private GameObject StreamRound(BuildingSite site, out CityStreamer streamer)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 1;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            streamer.StreamAround(new Vector3((float)site.CentreX, site.BaseHeight, (float)site.CentreZ), int.MaxValue);
            return go;
        }

        private static ChunkRoomLights ChunkOf(GameObject streamer, int building)
        {
            foreach (ChunkRoomLights chunk in streamer.GetComponentsInChildren<ChunkRoomLights>(true))
            {
                foreach (RoomLightGroup room in chunk.Rooms)
                {
                    if (room.Key.Building == building) return chunk;
                }
            }
            return null;
        }

        private static List<RoomLightGroup> RoomsOf(ChunkRoomLights chunk, int building)
        {
            var rooms = new List<RoomLightGroup>();
            foreach (RoomLightGroup room in chunk.Rooms)
            {
                if (room.Key.Building == building) rooms.Add(room);
            }
            return rooms;
        }

        private static List<Light> LightsOf(RoomLightGroup room)
        {
            var lights = new List<Light>(room.Lamps);
            lights.AddRange(room.Bounces);
            return lights;
        }

        [UnityTest]
        public IEnumerator FlippingAPoweredRoomsSwitchLightsItAndFlippingItAgainPutsItOut()
        {
            var world = new CityWorld(Seed);
            BuildingSite powered = PoweredNearSpawn(world);
            Assert.IsTrue(powered.Exists, "no powered building near the spawn");
            GameObject go = StreamRound(powered, out _);
            yield return null;

            ChunkRoomLights chunk = ChunkOf(go, powered.Id);
            Assert.IsNotNull(chunk, "the powered building's rooms have no lights");
            List<RoomLightGroup> groups = RoomsOf(chunk, powered.Id);
            RoomLightGroup room = groups[0];
            Assert.Greater(room.LightCount, 0, "a lit room with no real light in it");
            Assert.IsFalse(room.IsLit);
            Assert.IsFalse(room.LightsOn);
            Assert.AreEqual(0, room.Lamps.Length + room.Bounces.Length, "lights made for a room nobody has switched on");

            RoomLights.Flip(room.Key);
            yield return null;
            Assert.IsTrue(room.IsLit);
            Assert.IsTrue(room.LightsOn, "the switch is on and the room is still dark");
            List<Light> lights = LightsOf(room);
            Assert.AreEqual(room.LightCount, lights.Count, "the room did not get all its lights");
            foreach (Light l in lights)
            {
                Assert.IsTrue(l.enabled, "the switch is on and the room is still dark");
                Assert.AreEqual(LightType.Spot, l.type, "room lights are spots: a lamp shining down, its bounce shining up (a point light burns the ceiling)");
                Assert.AreNotEqual(0u, CityLightLayers.Of(l) & CityLightLayers.Interior,
                    "a room light not marked as inside shines through the building's walls onto the street");
                Assert.AreEqual(room.Colour.r, l.color.r, 0.2f, "a room light not in the room's colour");
            }
            Assert.Greater(room.Lamps.Length, 0);
            Assert.AreEqual(room.Lamps.Length, room.Bounces.Length);
            foreach (Light lamp in room.Lamps)
            {
                Assert.Less(lamp.transform.forward.y, -0.99f, "a lamp that does not shine down");
                Assert.AreEqual(RoomLighting.LampOuterAngle, lamp.spotAngle, 0.01f);
                Assert.Less(Vector3.Distance(lamp.transform.position, room.Centre), 60.0f, "a lamp put somewhere else than its room");
            }
            foreach (Light bounce in room.Bounces)
            {
                Assert.Greater(bounce.transform.forward.y, 0.99f, "a bounce that does not shine up");
                Assert.AreEqual(RoomLighting.BounceOuterAngle, bounce.spotAngle, 0.01f);
            }

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
            Assert.AreEqual(1, chunk.RoomsDrawn, "a lit room out of reach of its lights stopped glowing");
            foreach (Light l in lights)
            {
                Assert.IsFalse(l.enabled, "a far room's lights left on");
                Assert.AreEqual(LightShadows.None, l.shadows);
            }
            RoomLightDirector.Direct(room.Centre);
            Assert.IsTrue(room.LightsOn, "back in reach and still dark");
            lights = LightsOf(room);

            RoomLights.Flip(room.Key);
            yield return null;
            Assert.IsFalse(room.IsLit);
            Assert.IsFalse(room.LightsOn);
            foreach (Light l in lights) Assert.IsFalse(l.enabled);
        }

        [UnityTest]
        public IEnumerator ALitRoomsBouncedLightGoesToTheShaderAndComesOutWhenItGoesDark()
        {
            // 2026-10-22: the light a lit room's walls throw back (RoomFill) is drawn by the facade shader from an array the director fills with
            // the nearest lit rooms -- lamps or not, so a lit room seen from down the street still glows inside.
            var world = new CityWorld(Seed);
            BuildingSite powered = PoweredNearSpawn(world);
            Assert.IsTrue(powered.Exists, "no powered building near the spawn");
            GameObject go = StreamRound(powered, out _);
            yield return null;
            RoomLightGroup room = RoomsOf(ChunkOf(go, powered.Id), powered.Id)[0];
            Assert.AreEqual(0.0f, Shader.GetGlobalFloat(FillCountId), "bounced light drawn with every room dark");

            RoomLights.Flip(room.Key);
            yield return null;
            RoomLightDirector.Direct(room.Centre);
            Assert.AreEqual(1.0f, Shader.GetGlobalFloat(FillCountId), "the lit room's bounced light is not drawn");
            var expected = new Vector4[RoomFill.Stride];
            room.Fill.Pack(expected, 0);
            Vector4[] given = Shader.GetGlobalVectorArray(FillsId);
            for (int i = 0; i < RoomFill.Stride; i++) Assert.AreEqual(expected[i], given[i], $"entry {i} of the room's bounced light");
            Assert.IsTrue(room.Fill.Covers(room.Lamps[0].transform.position, Vector3.down), "the room's bounced light is not where its lamps are");
            Assert.Greater(room.Fill.Light.maxColorComponent, 0.05f, "next to no bounced light");

            RoomLightDirector.Direct(room.Centre + Vector3.right * (RoomLightDirector.LightReachMetres + 50.0f));
            Assert.IsFalse(room.LightsOn);
            Assert.AreEqual(1.0f, Shader.GetGlobalFloat(FillCountId), "a lit room out of reach of its lamps went dark inside");

            RoomLights.Flip(room.Key);
            yield return null;
            RoomLightDirector.Direct(room.Centre);
            Assert.AreEqual(0.0f, Shader.GetGlobalFloat(FillCountId), "a room switched off still glows inside");
        }

        [UnityTest]
        public IEnumerator NothingIsMadeForAPoweredRoomUntilItsSwitchIsOn()
        {
            // What made a powered block cost 10-20 ms of one frame to stream in (2026-10-14): every room's glow, windows and up to 32 spot lights
            // were made at once, nearly all of them to stay dark. Now a dark room is a few numbers on its chunk.
            var world = new CityWorld(Seed);
            BuildingSite powered = PoweredNearSpawn(world, minRooms: 4);
            Assert.IsTrue(powered.Exists, "no powered building with a few rooms near the spawn");
            GameObject go = StreamRound(powered, out _);
            yield return null;

            ChunkRoomLights chunk = ChunkOf(go, powered.Id);
            Assert.IsNotNull(chunk);
            Assert.GreaterOrEqual(RoomsOf(chunk, powered.Id).Count, 4);
            int doorLamps = chunk.GetComponentsInChildren<EntranceLamp>(true).Length;
            Assert.AreEqual(doorLamps, chunk.GetComponentsInChildren<Light>(true).Length,
                "a chunk whose rooms are all dark holds lights other than its door lamps");
            Assert.AreEqual(0, chunk.RoomsDrawn);
            Assert.AreEqual(0, chunk.DrawnRenderers().Count, "a dark room's glow or windows are drawn");
            foreach (RoomLightGroup room in chunk.Rooms) Assert.IsFalse(room.LightsOn);
        }

        [UnityTest]
        public IEnumerator ALitRoomGlowsInItsOwnColourAndRoomsOfOneColourShareAMesh()
        {
            // Of the building's rooms: two of one colour and one of another, if it has them (its rooms' kinds of lighting are rolled per floor).
            var world = new CityWorld(Seed);
            BuildingSite powered = default;
            RoomLightPlan a = null, b = null, c = null;
            foreach (BuildingSite s in TestSites.NearSpawn(world, 14))
            {
                if (s.Shell || s.Sides >= 3 || !PowerGrid.IsOn(s)) continue;
                List<RoomLightPlan> plans = RoomLighting.PlansFor(s);
                a = b = c = null;
                foreach (RoomLightPlan p in plans)
                {
                    if (p.Fixtures.Count == 0) continue;
                    if (a == null) a = p;
                    else if (b == null && p.Colour == a.Colour) b = p;
                    else if (c == null && p.Colour != a.Colour) c = p;
                }
                if (a != null && b != null && c != null)
                {
                    powered = s;
                    break;
                }
            }
            Assert.IsTrue(powered.Exists, "no powered building near the spawn with rooms of two colours");
            GameObject go = StreamRound(powered, out _);
            yield return null;
            ChunkRoomLights chunk = ChunkOf(go, powered.Id);

            RoomLights.Flip(a.Key);
            yield return null;
            Assert.AreEqual(1, chunk.RoomsDrawn);
            List<Renderer> drawn = chunk.DrawnRenderers();
            Assert.That(drawn.Count, Is.InRange(1, 2), "one lit room: its glow, and its windows if it has any");
            var block = new MaterialPropertyBlock();
            Renderer glow = drawn.Find(r => r.name == "RoomGlow");
            Assert.IsNotNull(glow, "a lit room's fittings do not glow");
            Assert.Greater(glow.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, glow.shadowCastingMode);
            glow.GetPropertyBlock(block);
            Color expected = a.Colour * CityStreamer.RoomGlowBrightness;
            Assert.AreEqual(expected.r, block.GetColor(BaseColorId).r, 0.01f, "the glow is not the room's colour");
            Assert.AreEqual(expected.b, block.GetColor(BaseColorId).b, 0.01f, "the glow is not the room's colour");
            int oneRoom = glow.GetComponent<MeshFilter>().sharedMesh.vertexCount;

            // A second room of the same colour joins the same mesh; one of another colour gets its own.
            RoomLights.Flip(b.Key);
            RoomLights.Flip(c.Key);
            yield return null;
            Assert.AreEqual(3, chunk.RoomsDrawn);
            List<Renderer> glows = chunk.DrawnRenderers().FindAll(r => r.name == "RoomGlow");
            Assert.AreEqual(2, glows.Count, "rooms of two colours should be two glow meshes, whatever the number of rooms");
            Assert.Greater(glow.GetComponent<MeshFilter>().sharedMesh.vertexCount, oneRoom, "the second room of a colour did not join its mesh");

            // Dark again: nothing drawn.
            RoomLights.Flip(a.Key);
            RoomLights.Flip(b.Key);
            RoomLights.Flip(c.Key);
            yield return null;
            Assert.AreEqual(0, chunk.RoomsDrawn);
            Assert.AreEqual(0, chunk.DrawnRenderers().Count, "a room that went dark still glows");
        }

        [UnityTest]
        public IEnumerator RoomLightsAreLentFromAPoolThatNeverOutgrowsTheBudget()
        {
            var world = new CityWorld(Seed);
            BuildingSite powered = PoweredNearSpawn(world, minRooms: 12);
            Assert.IsTrue(powered.Exists, "no powered building with a dozen rooms near the spawn");
            GameObject go = StreamRound(powered, out _);
            yield return null;
            ChunkRoomLights chunk = ChunkOf(go, powered.Id);
            int lentBefore = RoomLightDirector.LightsLent;

            // Every room on: they get their lights up to the budget, nearest first.
            foreach (RoomLightGroup room in chunk.Rooms) RoomLights.Set(room.Key, true);
            RoomLightDirector.Direct(null);
            int lent = 0;
            foreach (RoomLightGroup room in chunk.Rooms) if (room.LightsOn) lent += room.LightCount;
            Assert.Greater(lent, 0);
            Assert.LessOrEqual(lent, RoomLightDirector.MaxRoomLights, "more room lights on than the budget");
            Assert.AreEqual(lentBefore + lent, RoomLightDirector.LightsLent);
            Assert.LessOrEqual(RoomLightDirector.LightsMade, RoomLightDirector.MaxRoomLights, "the pool made more lights than can ever be on");
            int made = RoomLightDirector.LightsMade;

            // All off: every light back in the pool, switched off.
            var all = new List<Light>();
            foreach (RoomLightGroup room in chunk.Rooms) all.AddRange(LightsOf(room));
            foreach (RoomLightGroup room in chunk.Rooms) RoomLights.Set(room.Key, false);
            Assert.AreEqual(lentBefore, RoomLightDirector.LightsLent, "a dark room kept its lights");
            foreach (Light l in all) Assert.IsFalse(l.enabled, "a light given back to the pool is still on");

            // On again: the same lights, none made.
            foreach (RoomLightGroup room in chunk.Rooms) RoomLights.Set(room.Key, true);
            RoomLightDirector.Direct(null);
            Assert.AreEqual(made, RoomLightDirector.LightsMade, "lighting the same rooms again made new lights instead of taking the pool's");
        }

        [UnityTest]
        public IEnumerator AChunkThatGoesGivesItsLightsBackAndALitRoomThatComesBackIsLit()
        {
            var world = new CityWorld(Seed);
            BuildingSite powered = PoweredNearSpawn(world);
            Assert.IsTrue(powered.Exists, "no powered building near the spawn");
            GameObject go = StreamRound(powered, out CityStreamer streamer);
            yield return null;
            int lentBefore = RoomLightDirector.LightsLent;
            RoomLightGroup room = RoomsOf(ChunkOf(go, powered.Id), powered.Id)[0];
            RoomKey key = room.Key;
            RoomLights.Set(key, true);
            RoomLightDirector.Direct(null);
            Assert.IsTrue(room.LightsOn);
            List<Light> lights = LightsOf(room);

            // The chunk goes (the world rebuilt, as a seed change does; streaming out is the same DestroyChunk): its lights go back at once.
            streamer.Rebuild();
            Assert.AreEqual(lentBefore, RoomLightDirector.LightsLent, "a chunk that went kept its rooms' lights");
            foreach (Light l in lights) Assert.IsFalse(l.enabled, "a gone room's light still shining where it was");

            // It streams back in with its switch still on: lit again, the switch remembered (World.RoomLights), not the chunk.
            streamer.StreamAround(new Vector3((float)powered.CentreX, powered.BaseHeight, (float)powered.CentreZ), int.MaxValue);
            yield return null;
            RoomLightGroup again = null;
            foreach (RoomLightGroup r in ChunkOf(go, powered.Id).Rooms) if (r.Key.Equals(key)) again = r;
            Assert.IsNotNull(again);
            Assert.IsTrue(again.IsLit, "a room left switched on came back dark");
            Assert.IsTrue(again.LightsOn, "a room left switched on came back without its lights");
            Assert.AreEqual(1, ChunkOf(go, powered.Id).RoomsDrawn);
        }

        [UnityTest]
        public IEnumerator APoweredBlockCostsLittleToStreamIn()
        {
            // The game's own districts, where the towers are (the tests otherwise pin the classic city), and the chunk round the spawn with the
            // most powered rooms. CityStreamer gives a chunk its rooms with exactly this call (AddRoomLights).
            _scaleBefore = CityScale.Active;
            _scaleChanged = true;
            CityScale.Active = CityScale.Default;
            var world = new CityWorld(Seed);
            Assert.IsTrue(world.Plan.TryFindSpawn(out double sx, out double sz));
            int cx = Mathf.FloorToInt((float)(sx / CityLayout.ChunkSize)), cy = Mathf.FloorToInt((float)(sz / CityLayout.ChunkSize));
            List<CityMeshBuilder.RoomGlow> most = null;
            Vector2Int mostAt = default;
            // Ring by ring out from the spawn, until a chunk with 20 powered rooms has turned up (6 chunks round the spawn held no more than 18 on
            // seed 74102958; seed 83920571's spawn district has a few huge blocks and none of them with power within 10 chunks).
            for (int ring = 0; ring <= 40 && (most == null || most.Count < 20 || ring <= 10); ring++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                    var glows = new List<CityMeshBuilder.RoomGlow>();
                    var origin = new Vector3((cx + dx) * CityLayout.ChunkSize, 0.0f, (cy + dy) * CityLayout.ChunkSize);
                    foreach (int id in world.BuildingsOwnedBy(cx + dx, cy + dy))
                    {
                        BuildingSite site = world.Building(id);
                        if (!PowerGrid.IsOn(site)) continue;
                        // What CityMeshBuilder.BuildChunk does for each building a near chunk owns (only the rooms are wanted here).
                        CityMeshBuilder.BuildSite(world, site, origin, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                            new MeshBuffer(), null, null, new MeshBuffer(), false, glows);
                    }
                    if (glows.Count > 0 && (most == null || glows.Count > most.Count))
                    {
                        most = glows;
                        mostAt = new Vector2Int(cx + dx, cy + dy);
                    }
                }
            }
            Assert.IsNotNull(most, "no powered building round the spawn");
            Assert.GreaterOrEqual(most.Count, 20, "no block round the spawn with 20 powered rooms: this measures nothing");

            Material windowLight = new Material(Shader.Find("CityX/WindowLight"));
            var chunkObject = new GameObject($"Chunk {mostAt.x},{mostAt.y}");
            _made.Add(chunkObject);
            chunkObject.transform.position = new Vector3(mostAt.x * CityLayout.ChunkSize, 0.0f, mostAt.y * CityLayout.ChunkSize);

            // Once on a throwaway object first: the first powered chunk of a session also pays for its code running for the first time (~6 ms).
            var warm = new GameObject("Warm-up chunk");
            ChunkRoomLights.Attach(warm, most, null, windowLight);
            Object.DestroyImmediate(warm);
            yield return null;

            // Streaming in, every room dark (the usual case): one component, no objects, no lights.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            ChunkRoomLights chunk = ChunkRoomLights.Attach(chunkObject, most, null, windowLight);
            double attach = clock.Elapsed.TotalMilliseconds;
            Assert.AreEqual(most.Count, chunk.Rooms.Count);
            Assert.AreEqual(0, chunkObject.transform.childCount, "objects made for dark rooms");
            Assert.AreEqual(0, chunkObject.GetComponentsInChildren<Light>(true).Length, "lights made for dark rooms");

            // Twenty rooms switched on at once (a player joining a session, or walking back to a block somebody lit): their glow drawn and
            // their lights lent, from a warm pool (once warm it stays so).
            int lit = Mathf.Min(20, most.Count);
            Vector3 eye = chunk.Rooms[0].Centre;
            for (int i = 0; i < lit; i++) RoomLights.Set(chunk.Rooms[i].Key, true);
            RoomLightDirector.Direct(eye);
            for (int i = 0; i < lit; i++) RoomLights.Set(chunk.Rooms[i].Key, false);
            RoomLightDirector.Direct(eye);
            yield return null;
            clock.Restart();
            for (int i = 0; i < lit; i++) RoomLights.Set(chunk.Rooms[i].Key, true);
            RoomLightDirector.Direct(eye);
            double lighting = clock.Elapsed.TotalMilliseconds;
            Assert.AreEqual(lit, chunk.RoomsDrawn);
            int lentNow = 0;
            foreach (RoomLightGroup room in chunk.Rooms) if (room.LightsOn) lentNow += room.LightCount;

            string report = $"CityX room lights: chunk {mostAt} with {most.Count} powered rooms: attached in {attach:0.000} ms with every room dark; " +
                            $"{lit} rooms switched on (glow drawn, {lentNow} lights lent) in {lighting:0.00} ms";
            Debug.Log(report);
            TestContext.WriteLine(report);
            Object.Destroy(windowLight);

            // Measured 2026-10-14: ~0.1 ms dark (it was 9-20 ms for a block like this), ~1-2 ms for twenty lit rooms. The limits leave room for a
            // busy machine; the old way is two orders of magnitude over them.
            Assert.Less(attach, 2.0, $"a powered block with every room dark took {attach:0.00} ms to attach");
            Assert.Less(lighting, 8.0, $"lighting {lit} rooms of a block took {lighting:0.00} ms");
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
