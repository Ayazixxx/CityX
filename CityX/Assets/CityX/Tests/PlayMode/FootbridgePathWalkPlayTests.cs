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
    /// The new footbridges are walked (owner, 2026-10-15: "make everything walkable and functionable as a pedestrian not just look good"; the shapes,
    /// flights and street stairs of 2026-10-16): a body the player's size follows a dog-leg round its landings, an S-curve, a slanted bridge and a
    /// bridge's flight of steps from one doorway to the other, on the walking surface the whole way; and climbs a street stair (a straight run and a
    /// zig-zag tower) from the paving up onto its bridge's deck.
    /// </summary>
    public sealed class FootbridgePathWalkPlayTests
    {
        private const int Seed = 95998845;

        /// <summary>
        /// The street stairs are rare (a straight run only under a low deck with room along its wall), and a world seed may have none near its
        /// spawn: the climb is tried on the game's seed first, then on the earlier worlds' seeds, so a seed without one tests the stair on another.
        /// </summary>
        private static readonly int[] StairSeeds = { Seed, 61256619, 80469103, 23481876, 39271846 };

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

        private static bool Find(CityWorld world, System.Func<Footbridge, bool> wanted, out Footbridge found)
        {
            // Out to 150 chunks, nearest first (the search stops at the first): on seed 19077151 no straight street stair stood within 80 of the spawn.
            foreach (BuildingSite site in TestSites.NearSpawn(world, 150))
            {
                foreach (Footbridge b in Footbridges.For(world, site))
                {
                    if (b.SiteA == site.Id && wanted(b))
                    {
                        found = b;
                        return true;
                    }
                }
            }
            found = default;
            return false;
        }

        private void City(int seed, Vector3 around)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = seed;
            streamer.RadiusChunks = 3;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            streamer.StreamAround(around, int.MaxValue);
            Physics.SyncTransforms();
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

        /// <summary>
        /// Walks a body along a walking line (world points) from <paramref name="from"/> metres along it to <paramref name="to"/>, steering at a point
        /// a metre ahead, with gravity: returns how far along it got, and the worst it stood under or over the line's height (the walking surface).
        /// </summary>
        private static IEnumerator Walk(CharacterController body, Vector3[] line, float from, float to, float[] result)
        {
            float length = 0.0f;
            var at = new float[line.Length];
            for (int i = 1; i < line.Length; i++)
            {
                length += Flat(line[i] - line[i - 1]).magnitude;
                at[i] = length;
            }
            float s = from, worst = 0.0f;
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < Mathf.Abs(to - from) / 1.2f + 8.0f; t += dt)
            {
                if (Mathf.Abs(to - s) < 0.4f) break;
                float aim = Mathf.Clamp(s + Mathf.Sign(to - from) * 1.0f, Mathf.Min(from, to), Mathf.Max(from, to));
                Vector3 target = PointAt(line, at, aim);
                Vector3 step = Flat(target - body.transform.position);
                if (step.sqrMagnitude > 1e-6f) step = step.normalized * 2.0f;
                body.Move((step + Vector3.down * 3.0f) * dt);
                // Where along the line the body is now: the nearest point within a few metres of where it was.
                Vector3 p = body.transform.position;
                float best = s, bestD = float.MaxValue;
                for (float q = s - 1.0f; q <= s + 1.5f; q += 0.05f)
                {
                    float d = Flat(PointAt(line, at, Mathf.Clamp(q, 0.0f, length)) - p).magnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = Mathf.Clamp(q, 0.0f, length);
                    }
                }
                s = best;
                float surface = HeightAt(line, at, s);
                worst = Mathf.Max(worst, Mathf.Abs(p.y - surface));
                yield return null;
            }
            result[0] = s;
            result[1] = worst;
            result[2] = length;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        private static Vector3 PointAt(Vector3[] line, float[] at, float s)
        {
            for (int i = 1; i < line.Length; i++)
            {
                if (s <= at[i] || i == line.Length - 1)
                {
                    float l = at[i] - at[i - 1];
                    return Vector3.Lerp(line[i - 1], line[i], l > 1e-5f ? Mathf.Clamp01((s - at[i - 1]) / l) : 0.0f);
                }
            }
            return line[line.Length - 1];
        }

        /// <summary>The walking line's height (a straight line between its points: on a flight within a riser of the step there).</summary>
        private static float HeightAt(Vector3[] line, float[] at, float s) => PointAt(line, at, s).y;

        private IEnumerator WalkTheBridge(System.Func<Footbridge, bool> wanted, string what)
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(Find(world, wanted, out Footbridge bridge), $"no {what} near the spawn");
            BridgePath path = bridge.Path;
            float length = path.Length;
            Vector2 mid = path.PlanAt(length * 0.5f, out _);
            City(Seed, new Vector3(mid.x, path.HeightAt(length * 0.5f), mid.y));
            yield return null;
            Vector2 start = path.PlanAt(0.8f, out _);
            CharacterController body = Body(new Vector3(start.x, path.HeightAt(0.8f) + 0.05f, start.y));
            var result = new float[3];
            yield return Walk(body, path.Points, 0.8f, length - 0.8f, result);
            Debug.Log($"CityX bridge walk ({what}): {bridge.SiteA}->{bridge.SiteB} #{bridge.Index} {bridge.Shape} {bridge.Rail}, {length:F1} m, " +
                $"flights {(path.HasStairs ? "yes" : "no")}: walked to {result[0]:F1} m, at most {result[1]:F2} m off the walking line");
            Assert.That(result[0], Is.GreaterThan(length - 1.3f), $"the body stopped {length - result[0]:F1} m short on the {what}");
            Assert.That(result[1], Is.LessThan(0.5f), $"the body left the {what}'s walking surface");
        }

        [UnityTest]
        public IEnumerator ADogLegIsWalkedRoundItsLandings() => WalkTheBridge(b => b.Shape == BridgeShape.DogLeg && !b.Path.HasStairs, "dog-leg");

        [UnityTest]
        public IEnumerator ACurveIsWalkedEndToEnd() => WalkTheBridge(b => b.Shape == BridgeShape.Curve, "curve");

        [UnityTest]
        public IEnumerator ASlantedBridgeIsWalkedEndToEnd() => WalkTheBridge(b => b.Shape == BridgeShape.Angled && !b.Path.HasStairs, "slanted bridge");

        [UnityTest]
        public IEnumerator ABridgesFlightIsClimbedFromDoorToDoor() => WalkTheBridge(b => b.Path.HasStairs, "bridge with a flight");

        // The corridors (2026-10-23): between their walls, under their roof, in at one open mouth and out at the other.
        [UnityTest]
        public IEnumerator AnEnclosedCorridorIsWalkedMouthToMouth() => WalkTheBridge(b => b.Kind == BridgeKind.Enclosed, "enclosed corridor");

        [UnityTest]
        public IEnumerator ABreezewayIsWalkedMouthToMouth() => WalkTheBridge(b => b.Kind == BridgeKind.Breezeway, "breezeway");

        private IEnumerator ClimbTheStreetStair(bool tower)
        {
            Footbridge bridge = default;
            int seed = Seed;
            bool found = false;
            foreach (int candidate in StairSeeds)
            {
                seed = candidate;
                found = Find(new CityWorld(seed), b => b.Stair != null && b.Stair.Switchback == tower, out bridge);
                if (found) break;
            }
            if (!found)
            {
                // The bridges favour high floors (Footbridges.TallFactor, HeightWeight), and a straight run is only for a deck 16 m or less over the
                // paving with room along its wall: a whole test region can have none. Not a failure of the stairs.
                Assert.Inconclusive($"no {(tower ? "stair tower" : "straight street stair")} in the region on any of the seeds tried");
            }
            StreetStair stair = bridge.Stair;
            Vector3 foot = stair.Points[stair.Points.Length - 1];
            City(seed, foot);
            yield return null;
            // Up the walking line from the foot to the top, then onto the deck toward its middle by the wall.
            var up = new Vector3[stair.Points.Length + 1];
            for (int i = 0; i < stair.Points.Length; i++) up[i] = stair.Points[stair.Points.Length - 1 - i];
            Vector2 onDeck = bridge.Path.PlanAt(Footbridges.StreetStairOffset, out _);
            up[up.Length - 1] = new Vector3(onDeck.x, bridge.Path.HeightAt(Footbridges.StreetStairOffset), onDeck.y);
            CharacterController body = Body(foot + Vector3.up * 0.05f);
            var result = new float[3];
            float total = 0.0f;
            for (int i = 1; i < up.Length; i++) total += Flat(up[i] - up[i - 1]).magnitude;
            yield return Walk(body, up, 0.3f, total, result);
            Vector3 p = body.transform.position;
            Debug.Log($"CityX street stair climb ({(tower ? "tower" : "straight")}): {bridge.SiteA}->{bridge.SiteB} #{bridge.Index}, {stair.Top - stair.Bottom:F1} m up, " +
                $"{stair.Flights.Length} flights: got {result[0]:F1} of {result[2]:F1} m, ended at y {p.y:F2} (deck {up[up.Length - 1].y:F2}), at most {result[1]:F2} m off the line");
            Assert.That(result[0], Is.GreaterThan(result[2] - 1.0f), $"the body stopped {result[2] - result[0]:F1} m short of the deck");
            Assert.That(p.y, Is.EqualTo(up[up.Length - 1].y).Within(0.4f), "the body is not on the bridge's deck");
            Assert.That(result[1], Is.LessThan(0.6f), "the body left the stair's walking surface");
        }

        [UnityTest]
        public IEnumerator AStraightStreetStairIsClimbedOntoTheBridge() => ClimbTheStreetStair(false);

        [UnityTest]
        public IEnumerator AStairTowerIsClimbedOntoTheBridge() => ClimbTheStreetStair(true);

        // The stairs up the outside to the roofs (World/RoofStairs, 2026-11-09): from the paving up the tower and through the opening in the
        // parapet onto the roof.
        [UnityTest]
        public IEnumerator ARoofStairIsClimbedFromThePavingOntoTheRoof()
        {
            var world = new CityWorld(Seed);
            RoofStair roof = null;
            BuildingSite on = BuildingSite.None;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 60))
            {
                roof = RoofStairs.For(world, site);
                if (roof != null)
                {
                    on = site;
                    break;
                }
            }
            Assert.IsNotNull(roof, "no block near the spawn has a stair up to its roof");
            StreetStair stair = roof.Stair;
            Vector3 foot = stair.Points[stair.Points.Length - 1];
            City(Seed, foot);
            yield return null;
            var up = new Vector3[stair.Points.Length];
            float total = 0.0f;
            for (int i = 0; i < up.Length; i++)
            {
                up[i] = stair.Points[stair.Points.Length - 1 - i];
                if (i > 0) total += Flat(up[i] - up[i - 1]).magnitude;
            }
            CharacterController body = Body(foot + Vector3.up * 0.05f);
            var result = new float[3];
            yield return Walk(body, up, 0.3f, total, result);
            Vector3 p = body.transform.position;
            on.WorldToLocal(p.x, p.z, out double lx, out double lz);
            Debug.Log($"CityX roof stair climb: building {on.Id}, wall {stair.Wall}, {stair.Top - stair.Bottom:F1} m up in {stair.Flights.Length} flights: " +
                $"got {result[0]:F1} of {result[2]:F1} m, ended at y {p.y:F2} (roof {on.Top:F2}), at most {result[1]:F2} m off the line");
            Assert.That(result[0], Is.GreaterThan(result[2] - 1.0f), $"the body stopped {result[2] - result[0]:F1} m short of the roof");
            Assert.That(p.y, Is.EqualTo(on.Top).Within(0.4f), "the body is not on the roof");
            Assert.That(result[1], Is.LessThan(0.6f), "the body left the stair's walking surface");
            Assert.Less(System.Math.Abs(lx), on.Params.Width * 0.5 - 0.3, "the body did not get through the parapet onto the roof");
            Assert.Less(System.Math.Abs(lz), on.Params.Depth * 0.5 - 0.3, "the body did not get through the parapet onto the roof");
        }
    }
}
