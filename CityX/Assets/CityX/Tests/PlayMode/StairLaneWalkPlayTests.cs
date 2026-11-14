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
    /// The stairways through steep blocks and the step streets are walked (owner, 2026-10-15: "make everything walkable and functionable as a
    /// pedestrian not just look good"; the stairs, 2026-10-16): the real player controller, in a real streamed city, climbs the stairway nearest
    /// the spawn from the low street's pavement to the high street's, beside the handrail down its middle, and walks back down; and climbs a
    /// step street between its bollards from one junction to the other.
    /// </summary>
    public sealed class StairLaneWalkPlayTests
    {
        private const int Seed = CitySeed.Default;
        private const float Step = 1.0f / 60.0f;
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

        private void City(Vector3 around, double reach)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = Mathf.CeilToInt((float)(reach / PlanWorld.ChunkSize)) + 1;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            streamer.StreamAround(around, int.MaxValue);
            Physics.SyncTransforms();
        }

        private IEnumerator Player(Vector3 feet, PlayerLocomotion[] made)
        {
            var player = new GameObject("Player");
            _made.Add(player);
            player.AddComponent<CharacterController>();
            var locomotion = player.AddComponent<PlayerLocomotion>();
            yield return null;
            player.transform.position = feet;
            Physics.SyncTransforms();
            for (int i = 0; i < 20; i++)
            {
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
            made[0] = locomotion;
        }

        /// <summary>
        /// Walks the player along a straight line (from a point, along a unit direction) from <paramref name="from"/> metres to <paramref name="to"/>,
        /// steering back onto the line as it goes: how far along it got, and the worst it stood off the surface the line's height gives.
        /// </summary>
        private static IEnumerator Walk(PlayerLocomotion locomotion, Vec2 origin, Vec2 dir, double offset, double from, double to,
            System.Func<double, double> surface, float[] result, List<string> trace = null)
        {
            Transform body = locomotion.transform;
            Vec2 side = dir.Perp * offset;
            double along = from;
            float worst = 0.0f;
            int way = to > from ? 1 : -1;
            for (int tick = 0; tick < (int)(System.Math.Abs(to - from) / 1.0 * 60.0) + 600; tick++)
            {
                Vector3 p = body.position;
                along = (p.x - origin.X) * dir.X + (p.z - origin.Y) * dir.Y;
                if (way > 0 ? along >= to : along <= to) break;
                // A point a metre and a half ahead on the line: the player turns to it and walks.
                Vec2 aim = origin + dir * (along + way * 1.5) + side;
                Vector3 toward = new Vector3((float)(aim.X - p.x), 0.0f, (float)(aim.Y - p.z));
                locomotion.Tick(new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg }, Step);
                yield return null;
                if (trace != null && tick < 8)
                {
                    var controller = body.GetComponent<CharacterController>();
                    trace.Add($"tick {tick}: at {body.position} yaw {body.eulerAngles.y:F0} aim yaw {Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg:F0} grounded {locomotion.IsGrounded} " +
                        $"velocity {controller.velocity} flags {controller.collisionFlags} step {controller.stepOffset:F2} slope {controller.slopeLimit:F0} enabled {controller.enabled}");
                }
                worst = Mathf.Max(worst, Mathf.Abs(body.position.y - (float)surface(along)));
            }
            result[0] = (float)along;
            result[1] = worst;
        }

        [UnityTest]
        public IEnumerator TheRealPlayerClimbsAStairwayFromTheLowStreetToTheHighAndWalksBackDown()
        {
            var world = new CityWorld(Seed);
            world.Plan.TryFindSpawn(out double sx, out double sz);
            StairLane lane = null;
            double best = double.MaxValue;
            foreach (StairLane l in world.Plan.Data.StairLanes)
            {
                if (l.Length > 140.0) continue;   // one the test can stream round in one go
                double d = ((l.A + l.B) * 0.5 - new Vec2(sx, sz)).Length;
                if (d < best)
                {
                    best = d;
                    lane = l;
                }
            }
            Assert.IsNotNull(lane, "no stairway");
            List<StairTread> treads = StairLanes.Profile(lane, world.Plan.Fields);
            double length = lane.Length;
            Vec2 mid = (lane.A + lane.B) * 0.5;
            City(new Vector3((float)mid.X, (float)CitySurfaceBuilder.TreadTopAt(treads, length * 0.5), (float)mid.Y), length * 0.5 + 8.0);
            yield return null;

            // Up the right-hand side, 1.2 m off the middle (clear of the handrail and of a landing's planters), from the bottom pad to the top one.
            Vec2 dir = lane.Dir;
            Vec2 start = lane.A + dir * 0.4 + dir.Perp * 1.2;
            var made = new PlayerLocomotion[1];
            yield return Player(new Vector3((float)start.X, (float)treads[0].Top + 0.1f, (float)start.Y), made);
            PlayerLocomotion locomotion = made[0];
            float startY = locomotion.transform.position.y;
            var up = new float[2];
            var trace = new List<string>();
            yield return Walk(locomotion, lane.A, dir, 1.2, 0.4, length - 0.6, s => CitySurfaceBuilder.TreadTopAt(treads, s), up, trace);
            float climbed = locomotion.transform.position.y - startY;
            if (up[0] < length - 2.0)
            {
                // What stopped it, for the failure: what the body's capsule touches, and what is just ahead of it at knee and chest height.
                Vector3 at = locomotion.transform.position;
                var flatDir = new Vector3((float)dir.X, 0.0f, (float)dir.Y);
                var touching = new List<string>();
                foreach (Collider c in Physics.OverlapCapsule(at + Vector3.up * 0.5f, at + Vector3.up * 2.1f, 0.55f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (c.transform.IsChildOf(locomotion.transform)) continue;
                    touching.Add($"{c.name} ({c.GetType().Name}) {c.bounds.center} {c.bounds.size}");
                }
                foreach (Vector3 o in new[] { Vector3.zero, flatDir * 0.4f, -flatDir * 0.4f })
                {
                    if (Physics.Raycast(at + o + Vector3.up * 1.3f, Vector3.up, out RaycastHit over, 6.0f, ~0, QueryTriggerInteraction.Ignore))
                        touching.Add($"over (offset {o}): {over.collider.name} at {over.point.y - at.y:F2} m over the feet, normal {over.normal}");
                }
                foreach (float h in new[] { 0.3f, 1.0f, 1.8f })
                {
                    if (Physics.Raycast(at + Vector3.up * h, flatDir, out RaycastHit hit, 2.0f, ~0, QueryTriggerInteraction.Ignore))
                        touching.Add($"ahead at {h} m: {hit.collider.name} {hit.distance:F2} m, normal {hit.normal}, point {hit.point}");
                }
                Debug.Log($"CityX stairway walk stuck at {at}: {string.Join("; ", touching)}");
                foreach (string line in trace) Debug.Log("CityX stairway walk, " + line);
                foreach (BuildingSite site in world.Plan.Buildings)
                {
                    double dx = site.CentreX - at.x, dz = site.CentreZ - at.z;
                    if (dx * dx + dz * dz > 60.0 * 60.0) continue;
                    foreach (Footbridge b in Footbridges.For(world, site))
                    {
                        if (b.SiteA != site.Id) continue;
                        string stair = b.Stair == null ? "no street stair" : $"street stair {(b.Stair.Switchback ? "tower" : "straight")} from {b.Stair.Points[0]} to {b.Stair.Points[b.Stair.Points.Length - 1]}";
                        Debug.Log($"CityX stairway walk, bridge {b.SiteA}->{b.SiteB} #{b.Index} {b.Shape}: {stair}; piers {string.Join(" ", b.Path.Piers)}");
                    }
                }
            }
            var down = new float[2];
            yield return Walk(locomotion, lane.A, dir, -1.2, length - 0.6, 0.6, s => CitySurfaceBuilder.TreadTopAt(treads, s), down);
            Debug.Log($"CityX stairway walk: stairway {lane.Id} ({length:F0} m, {lane.Drop:F1} m up, {best:F0} m from the spawn): up to {up[0]:F1} m " +
                $"(climbed {climbed:F2} m, at most {up[1]:F2} m off the treads), back down to {down[0]:F1} m (at most {down[1]:F2} m off), now at y " +
                $"{locomotion.transform.position.y:F2} against the bottom pad's {treads[0].Top:F2}");
            Assert.That(up[0], Is.GreaterThan(length - 2.0), $"the climb stopped {length - up[0]:F1} m short of the top");
            Assert.That(climbed, Is.GreaterThan(lane.Drop - 0.6), "the body did not climb the stairway's height");
            Assert.That(down[0], Is.LessThan(2.0), $"the walk down stopped {down[0]:F1} m short of the bottom");
            Assert.That(Mathf.Max(up[1], down[1]), Is.LessThan(0.6f), "the body left the treads");
        }

        [UnityTest]
        public IEnumerator TheRealPlayerClimbsAStepStreetBetweenItsBollards()
        {
            var world = new CityWorld(Seed);
            world.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            EdgeGeometry street = null;
            double best = double.MaxValue;
            foreach (EdgeGeometry g in world.Plan.Geometry.Edges)
            {
                if (g == null || !StepStreets.Is(world.Plan, g.Edge)) continue;
                double run = g.Length - g.TrimA - g.TrimB;
                if (run < 12.0 || run > 120.0) continue;
                double d = ((g.A + g.B) * 0.5 - spawn).Length;
                if (d < best)
                {
                    best = d;
                    street = g;
                }
            }
            Assert.IsNotNull(street, "no step street");
            List<StairTread> treads = StepStreets.Profile(world.Plan, street, out bool fromB);
            double length = street.Length - street.TrimA - street.TrimB;
            Vec2 dir = fromB ? -street.Dir : street.Dir, low = street.A + street.Dir * (fromB ? street.Length - street.TrimB : street.TrimA);
            double half = street.HalfWidth, m = StepStreets.MedianWidth * 0.5;
            // Up the outside of one flight, between its handrail and the kerb (through the gap in the bollards there).
            double across = half >= StepStreets.MedianFrom ? m + (half - m) * 0.5 + 1.0 : half * 0.5;
            Vec2 mid = low + dir * (length * 0.5);
            City(new Vector3((float)mid.X, (float)CitySurfaceBuilder.TreadTopAt(treads, length * 0.5), (float)mid.Y), length * 0.5 + 8.0);
            yield return null;
            Vec2 start = low + dir * 1.2 + dir.Perp * across;
            var made = new PlayerLocomotion[1];
            yield return Player(new Vector3((float)start.X, (float)CitySurfaceBuilder.TreadTopAt(treads, 1.2) + 0.1f, (float)start.Y), made);
            PlayerLocomotion locomotion = made[0];
            float startY = locomotion.transform.position.y;
            var up = new float[2];
            yield return Walk(locomotion, low, dir, across, 1.2, length - 1.2, s => CitySurfaceBuilder.TreadTopAt(treads, s), up);
            float climbed = locomotion.transform.position.y - startY;
            double rise = CitySurfaceBuilder.TreadTopAt(treads, length - 1.2) - CitySurfaceBuilder.TreadTopAt(treads, 1.2);
            Debug.Log($"CityX step street walk: street {street.Edge.Id} ({length:F0} m, {rise:F1} m up, {best:F0} m from the spawn), {across:F1} m off its middle: " +
                $"got to {up[0]:F1} m, climbed {climbed:F2} m, at most {up[1]:F2} m off the treads");
            Assert.That(up[0], Is.GreaterThan(length - 2.5), $"the climb stopped {length - up[0]:F1} m short");
            Assert.That(climbed, Is.GreaterThan(rise - 0.45), "the body did not climb the steps");
            Assert.That(up[1], Is.LessThan(0.6f), "the body left the steps");
        }
    }
}
