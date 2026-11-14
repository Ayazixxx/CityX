using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The player's own body in the sewers' water in the streamed city (docs/SEWERS.md; owner, 2026-11-13: "add water to the sewers right now
    /// there isnt any"): ankle deep in a room of an ordinary neighbourhood, waist deep and slowed in a flooded one, dry on the street above,
    /// and the water drawn round the camera.
    /// </summary>
    public sealed class SewerWaterPlayTests
    {
        private const int Seed = CitySeed.Default;
        private const float Dt = 1.0f / 60.0f;

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private void Streamed(Vector3 around)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            streamer.StreamAround(around, int.MaxValue);
            Physics.SyncTransforms();
        }

        private SewerWaterSurface Water(Camera eye)
        {
            var go = new GameObject("Water");
            _made.Add(go);
            go.SetActive(false);
            var water = go.AddComponent<SewerWaterSurface>();
#if UNITY_EDITOR
            water.Material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/SewerWater.mat");
            Assert.IsNotNull(water.Material, "SewerWater.mat is missing: rebuild the city scene");
#endif
            water.Eye = eye;
            go.SetActive(true);
            return water;
        }

        private PlayerLocomotion Player(Vector3 feet)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, feet, Quaternion.identity);
            _made.Add(player);
            var reader = player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            Physics.SyncTransforms();
            return player.GetComponent<PlayerLocomotion>();
#else
            return null;
#endif
        }

        /// <summary>The room nearest the spawn holding this much water.</summary>
        private static SewerRoom RoomNearSpawn(PlanWorld plan, double water)
        {
            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            SewerRoom best = null;
            foreach (SewerRoom room in SewerPlan.For(plan).Rooms)
            {
                if (room.Water != water) continue;
                if (best == null || (room.Node.P - spawn).Length < (best.Node.P - spawn).Length) best = room;
            }
            return best;
        }

        /// <summary>Where a body can stand in a room: off its middle, away from the ladder.</summary>
        private static Vector3 StandIn(SewerRoom room)
        {
            Vec2 at = room.Node.P + (room.Shaft != null ? room.Shaft.Normal * 1.8 : new Vec2(0.0, 0.0));
            Assert.IsTrue(SewerPlan.TryRoomFloor(room, at, out double floor), "the place to stand is not in the room");
            return new Vector3((float)at.X, (float)floor + 0.05f, (float)at.Y);
        }

        private static IEnumerator Settle(PlayerLocomotion body, float seconds, LocomotionInput input)
        {
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                body.Tick(input, Dt);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheBodyStandsAnkleDeepInARoomAndWaistDeepInAFloodedOne()
        {
            Time.captureFramerate = 60;
            var world = new CityWorld(Seed);
            foreach (double depth in new[] { SewerPlan.AnkleDeep, SewerPlan.WaistDeep })
            {
                SewerRoom room = RoomNearSpawn(world.Plan, depth);
                Assert.IsNotNull(room, $"no room {depth} m deep in this world");
                Vector3 feet = StandIn(room);
                Streamed(feet);
                Water(null);
                yield return null;
                PlayerLocomotion body = Player(feet);
                yield return null;
                yield return Settle(body, 1.0f, default);
                Debug.Log($"CityX sewer water: room {room.Node.Id}, {depth} m poured, the body stands {body.WaterDepth:F2} m deep ({body.WaterDepthShare:P0} of its height)");
                Assert.AreEqual((float)depth, body.WaterDepth, 0.1f, $"the body does not stand {depth} m deep in room {room.Node.Id}");
                Assert.IsFalse(body.IsFloating, "the body floats in the sewers");
                foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
                _made.Clear();
                yield return null;
            }
        }

        /// <summary>
        /// Owner, 2026-11-13: "if theres a current make it so you can feel it but not to a point where its just annoying". Waist deep on a
        /// flooded tunnel's walkway: standing still, the body drifts downstream; walking against it, it keeps most of its wading speed.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCurrentCarriesAStandingBodyButBarelyHoldsBackOneWalkingAgainstIt()
        {
            Time.captureFramerate = 60;
            var world = new CityWorld(Seed);
            world.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            SewerPlan sewers = SewerPlan.For(world.Plan);
            SewerTube tube = null;
            foreach (RoadEdge edge in world.Plan.Data.Edges)
            {
                SewerTube t = sewers.Tube(edge.Id);
                if (t == null || t.End - t.Start < 40.0 || t.WaterStart != SewerPlan.WaistDeep || t.WaterEnd != SewerPlan.WaistDeep) continue;
                if (tube == null || (t.At(t.Start) - spawn).Length < (tube.At(tube.Start) - spawn).Length) tube = t;
            }
            Assert.IsNotNull(tube, "no flooded tunnel 40 m long in this world");
            double along = (tube.Start + tube.End) * 0.5;
            Vec2 at = tube.At(along) + tube.Dir.Perp * ((tube.Section.ChannelHalfWidth + tube.Section.FloorHalfWidth) * 0.5);
            var feet = new Vector3((float)at.X, (float)tube.FloorAt(along) + 0.05f, (float)at.Y);
            Streamed(feet);
            Water(null);
            yield return null;
            PlayerLocomotion body = Player(feet);
            yield return null;
            yield return Settle(body, 0.5f, default);

            Vec2 down = SewerPlan.Downstream(tube);
            var downstream = new Vector3((float)down.X, 0.0f, (float)down.Y);
            Vector3 from = body.transform.position;
            yield return Settle(body, 2.0f, default);
            float drift = Vector3.Dot(body.transform.position - from, downstream) / 2.0f;

            float yaw = Mathf.Atan2(-downstream.x, -downstream.z) * Mathf.Rad2Deg;
            var upstream = new LocomotionInput { Move = Vector2.up, YawDegrees = yaw };
            yield return Settle(body, 0.5f, upstream);
            from = body.transform.position;
            yield return Settle(body, 2.0f, upstream);
            float against = -Vector3.Dot(body.transform.position - from, downstream) / 2.0f;
            float wade = body.WalkSpeed * Wading.SpeedShare(body.WaterDepthShare);
            Debug.Log($"CityX sewer current: {body.WaterDepth:F2} m deep, drifts {drift:F2} m/s standing, walks upstream at {against:F2} m/s of a {wade:F2} m/s wade");
            Assert.Greater(drift, 0.3f, "standing in the current, the body does not feel it");
            Assert.Less(drift, 0.8f, "the current sweeps a standing body away");
            Assert.Greater(against, wade * 0.7f, "walking against the current is a slog");
        }

        [UnityTest]
        public IEnumerator TheStreetOverTheSewersIsDryAndTheWaterIsDrawnRoundTheCamera()
        {
            Time.captureFramerate = 60;
            var world = new CityWorld(Seed);
            SewerRoom room = RoomNearSpawn(world.Plan, SewerPlan.AnkleDeep);
            Vector3 down = StandIn(room);
            Streamed(down);
            yield return null;

            var eyeObject = new GameObject("Eye");
            _made.Add(eyeObject);
            Camera eye = eyeObject.AddComponent<Camera>();
            eye.transform.position = down + Vector3.up * 2.4f;
            SewerWaterSurface water = Water(eye);
            yield return null;
            Assert.Greater(water.TriangleCount, 50, "hardly any water is drawn round the camera in the sewers");

            float street = (float)world.Plan.GroundHeight(room.Node.P.X, room.Node.P.Y);
            PlayerLocomotion body = Player(new Vector3(down.x, street + 1.0f, down.z));
            yield return null;
            yield return Settle(body, 1.0f, default);
            Assert.AreEqual(0.0f, body.WaterDepth, 1e-4f, "the street over the sewers is under water");
            Assert.AreEqual(street, body.transform.position.y, 0.6f, "the body did not stand on the street");
        }
    }
}
