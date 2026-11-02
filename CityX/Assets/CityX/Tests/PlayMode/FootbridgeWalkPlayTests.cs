using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A footbridge is walked (owner, 2026-10-15: "make everything walkable and functionable as a pedestrian not just look good"): a body the
    /// player's size steps out of one building's doorway onto the deck, walks it to the far doorway and is still on the deck, at its height,
    /// the whole way.
    /// </summary>
    public sealed class FootbridgeWalkPlayTests
    {
        private const int Seed = 95998845;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        /// <summary>A bridge near the spawn between two free-standing blocks.</summary>
        private static bool FindBridge(CityWorld world, out Footbridge bridge)
        {
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                foreach (Footbridge b in Footbridges.For(world, site))
                {
                    if (b.SiteA != site.Id || b.Shape != BridgeShape.Straight || b.Path.HasStairs) continue;   // a straight deck: the walk goes door to door in a line (the other shapes: FootbridgePathWalkPlayTests)
                    BuildingSite other = world.Building(b.SiteB);
                    if (site.Contacts != null && site.Contacts.Length > 0 || other.Contacts != null && other.Contacts.Length > 0) continue;
                    bridge = b;
                    return true;
                }
            }
            bridge = default;
            return false;
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

        [UnityTest]
        public IEnumerator ABodyWalksABridgeFromOneDoorwayToTheOther()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindBridge(world, out Footbridge bridge), "no footbridge near the spawn");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            Vector3 middle = (bridge.StartWorld + bridge.EndWorld) * 0.5f;
            streamer.StreamAround(middle, int.MaxValue);
            Physics.SyncTransforms();
            yield return null;

            Vector3 flat = new Vector3(bridge.EndWorld.x - bridge.StartWorld.x, 0.0f, bridge.EndWorld.z - bridge.StartWorld.z).normalized;
            Vector3 start = bridge.StartWorld + flat * 0.6f + Vector3.up * 0.05f;
            CharacterController body = Body(start);
            float lowest = float.MaxValue;
            const float dt = 1.0f / 60.0f;
            float span = bridge.Span;
            for (float t = 0.0f; t < span / 2.0f + 1.0f; t += dt)
            {
                body.Move((flat * 2.0f + Vector3.down * 3.0f) * dt);
                float deckHere = Mathf.Lerp(bridge.StartWorld.y, bridge.EndWorld.y, Vector3.Dot(body.transform.position - bridge.StartWorld, flat) / span);
                lowest = Mathf.Min(lowest, body.transform.position.y - deckHere);
                yield return null;
            }
            Vector3 end = body.transform.position;
            float along = Vector3.Dot(end - bridge.StartWorld, flat);
            Debug.Log($"CityX footbridge walk: {bridge.SiteA}->{bridge.SiteB}, span {span:F1} m, the body got {along:F1} m along, lowest {lowest:F2} m to the deck");
            Assert.That(lowest, Is.GreaterThan(-0.3f), "the body fell through or off the deck");
            Assert.That(along, Is.GreaterThan(span - 1.2f), $"the body stopped {span - along:F1} m short of the far doorway (something in the way)");
            Assert.That(Mathf.Abs(end.y - bridge.EndWorld.y), Is.LessThan(0.4f), "the body is not at the far floor's height");
        }
    }
}
