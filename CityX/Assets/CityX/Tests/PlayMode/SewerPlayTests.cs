using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The sewers walked by a real body in the streamed city (docs/SEWERS.md; owner, 2026-11-09: "one world"): down an open manhole to the
    /// room under it, along a tunnel, the street still whole over a shut manhole, and the player's own body up the ladder and out.
    /// </summary>
    public sealed class SewerPlayTests
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

        private CityStreamer Streamed(Vector3 around)
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
            return streamer;
        }

        private CharacterController Body(Vector3 feet)
        {
            var go = new GameObject("Body");
            _made.Add(go);
            go.transform.position = feet;
            var controller = go.AddComponent<CharacterController>();
            controller.height = BodyRig.StandingHeight;
            controller.radius = FusedBodyShape.TorsoRadius;
            controller.skinWidth = 0.03f;
            controller.center = new Vector3(0.0f, BodyRig.StandingHeight * 0.5f, 0.0f);
            controller.stepOffset = 0.3f;
            controller.minMoveDistance = 0.0f;
            controller.slopeLimit = 50.0f;
            Physics.SyncTransforms();
            return controller;
        }

        /// <summary>The room with a shaft nearest the spawn whose lid is off (or on), with a tunnel of some length into it.</summary>
        private static SewerRoom RoomNearSpawn(CityWorld world, bool open, out SewerTube tunnel, out bool atStart)
        {
            PlanWorld plan = world.Plan;
            SewerPlan sewers = SewerPlan.For(plan);
            plan.TryFindSpawn(out double sx, out double sz);
            SewerRoom best = null;
            tunnel = null;
            atStart = true;
            foreach (SewerRoom room in sewers.Rooms)
            {
                if (room.Shaft == null || room.Shaft.Open != open) continue;
                if (best != null && (room.Node.P - new Vec2(sx, sz)).Length >= (best.Node.P - new Vec2(sx, sz)).Length) continue;
                foreach (RoadEdge edge in room.Node.Edges)
                {
                    SewerTube tube = sewers.Tube(edge.Id);
                    if (tube == null || tube.End - tube.Start < 30.0) continue;
                    best = room;
                    tunnel = tube;
                    atStart = edge.A == room.Node;
                    break;
                }
            }
            return best;
        }

        private static Vector3 World3(Vec2 p, double y) => new Vector3((float)p.X, (float)y, (float)p.Y);

        [UnityTest]
        public IEnumerator ABodyFallsDownAnOpenManholeToTheRoomAndWalksOffAlongATunnel()
        {
            var world = new CityWorld(Seed);
            SewerRoom room = RoomNearSpawn(world, true, out SewerTube tube, out bool atStart);
            Assert.IsNotNull(room, "no open manhole with a tunnel near the spawn");
            SewerShaft shaft = room.Shaft;
            Streamed(World3(room.Node.P, shaft.Top));
            yield return null;

            // Into the hole, on the side away from the ladder.
            Vec2 drop = shaft.Centre + shaft.Normal * 0.2;
            CharacterController body = Body(World3(drop, shaft.Top + 0.3));
            for (float t = 0.0f; t < 3.0f; t += Dt)
            {
                body.Move(Vector3.down * (9.0f * Dt));
                yield return null;
            }
            Assert.AreEqual(room.CentreFloor, body.transform.position.y, 0.5f, "the body did not come down the shaft onto the room's floor");

            // To the tunnel's mouth, and 25 m down it on a walkway.
            Vec2 dir = atStart ? tube.Dir : -tube.Dir;
            double from = atStart ? tube.Start : tube.End;
            Vec2 side = tube.Dir.Perp * (tube.Section.ChannelHalfWidth + 0.6);
            Vec2 mouth = tube.At(from) + side;
            body.enabled = false;
            body.transform.position = World3(mouth - dir * 0.8, room.CentreFloor + 0.3);
            body.enabled = true;
            Physics.SyncTransforms();
            var along = new Vector3((float)dir.X, 0.0f, (float)dir.Y);
            float lowest = float.MaxValue, highest = float.MinValue;
            for (float t = 0.0f; t < 14.0f; t += Dt)
            {
                body.Move((along * 2.0f + Vector3.down * 3.0f) * Dt);
                double s = Vec2.Dot(new Vec2(body.transform.position.x, body.transform.position.z) - tube.Origin, tube.Dir);
                if (s > tube.Start + 0.5 && s < tube.End - 0.5)
                {
                    float over = body.transform.position.y - (float)tube.FloorAt(s);
                    lowest = Mathf.Min(lowest, over);
                    highest = Mathf.Max(highest, over);
                }
                yield return null;
            }
            double reached = Vec2.Dot(new Vec2(body.transform.position.x, body.transform.position.z) - tube.At(from), dir);
            Debug.Log($"CityX sewer walk: room {room.Node.Id}, {tube.Section.Kind} {tube.Section.Style}, {reached:F1} m down the tunnel, {lowest:F2} to {highest:F2} m over its walkway");
            Assert.Greater(reached, 24.0, "the body did not get down the tunnel (something in the way, or no floor)");
            Assert.Greater(lowest, -0.15f, "the body sank through the walkway");
            Assert.Less(highest, 0.35f, "the body rode up off the walkway");
        }

        [UnityTest]
        public IEnumerator TheStreetHoldsABodyOverAShutManhole()
        {
            var world = new CityWorld(Seed);
            SewerRoom room = RoomNearSpawn(world, false, out _, out _);
            Assert.IsNotNull(room, "no shut manhole near the spawn");
            Streamed(World3(room.Node.P, room.Shaft.Top));
            yield return null;
            CharacterController body = Body(World3(room.Shaft.Centre, room.Shaft.Top + 0.3));
            for (float t = 0.0f; t < 1.5f; t += Dt)
            {
                body.Move(Vector3.down * (6.0f * Dt));
                yield return null;
            }
            Assert.AreEqual(room.Shaft.Top, body.transform.position.y, 0.3f, "a body fell through a shut manhole");
        }

        [UnityTest]
        public IEnumerator ThePlayersOwnBodyClimbsTheLadderFromTheRoomAndOutOntoTheStreet()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var world = new CityWorld(Seed);
            SewerRoom room = RoomNearSpawn(world, true, out _, out _);
            Assert.IsNotNull(room, "no open manhole near the spawn");
            Ladder ladder = Ladders.Of(room.Shaft);
            Streamed(ladder.Top);
            yield return null;

            Vector3 foot = PlayerLadder.HangPoint(ladder, ladder.Foot.y + 0.1f) + ladder.Normal * 0.25f;
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), foot, Quaternion.LookRotation(-ladder.Normal));
            _made.Add(player);
            PlayerLadder climber = player.GetComponent<PlayerLadder>();
            if (climber == null) climber = player.AddComponent<PlayerLadder>();
            PlayerClimbing climbing = player.GetComponent<PlayerClimbing>();
            yield return null;
            yield return null;

            // The city's own ladder is found from where the body stands, with nothing placed for it.
            Assert.IsTrue(climber.Nearby(out Ladder found, out bool fromTop), "at the foot of the room's ladder, no ladder is found");
            Assert.IsFalse(fromTop);
            Assert.Less(Vector3.Distance(found.Foot, ladder.Foot), 0.01f);
            Assert.IsTrue(climber.TryWalkOn(true, -ladder.Normal), "walking into the ladder did not get on it");
            for (float t = 0.0f; t < 14.0f && climber.IsOn; t += Dt)
            {
                climber.Tick(true, false, false, false, false, Dt);
                yield return null;
            }
            Assert.IsFalse(climber.IsOn, "the climb never reached the top");
            Assert.IsTrue(climbing.IsToppingOut, "the top of the ladder did not start the pull-up out of the manhole");
            for (int i = 0; i < 200 && climbing.IsToppingOut; i++)
            {
                climbing.TickMantle(Dt);
                yield return null;
            }
            yield return null;
            Vector3 stood = player.transform.position;
            Debug.Log($"CityX sewer ladder: room {room.Node.Id}, {ladder.Height:F1} m, out at {stood.y - ladder.Top.y:F2} m over the street, {Vector3.Distance(stood, ladder.StandAtTop):F2} m from where it is stood on");
            Assert.AreEqual(ladder.Top.y, stood.y, 0.35f, "the body is not standing on the street");
            Assert.Less(Vector3.Distance(new Vector3(stood.x, 0.0f, stood.z), new Vector3(ladder.StandAtTop.x, 0.0f, ladder.StandAtTop.z)), 0.4f, "the body did not come out beside the manhole");
            Assert.IsTrue(player.GetComponent<CharacterController>().enabled);

            // And it is dark where it came from, and not where it stands now.
            Assert.AreEqual(1.0f, CurrentSewers.DarknessAt(ladder.Foot + ladder.Normal * 3.0f + Vector3.up * 1.5f), 1e-4f, "the room is not out of the sky's light");
            Assert.AreEqual(0.0f, CurrentSewers.DarknessAt(stood + Vector3.up * 1.5f), 1e-4f, "the street is");
#else
            yield break;
#endif
        }
    }
}
