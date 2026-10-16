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
    /// The stairways through steep blocks and the stepped sidewalks are walked (owner, 2026-10-15: "make everything walkable and functionable as a
    /// pedestrian not just look good"; the stairs, 2026-10-16): the real player controller, in a real streamed city, climbs the stairway nearest
    /// the spawn from the low street's pavement to the high street's, beside the handrail down its middle, and walks back down; and climbs a
    /// stepped sidewalk from its low end to its high end.
    /// </summary>
    public sealed class StairLaneWalkPlayTests
    {
        private const int Seed = 45678679;
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
            System.Func<double, double> surface, float[] result)
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
            yield return Walk(locomotion, lane.A, dir, 1.2, 0.4, length - 0.6, s => CitySurfaceBuilder.TreadTopAt(treads, s), up);
            float climbed = locomotion.transform.position.y - startY;
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

        [UnityTest]
        public IEnumerator TheRealPlayerClimbsASteppedSidewalk()
        {
            var world = new CityWorld(Seed);
            world.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            (int block, int edge) pick = (-1, -1);
            double best = double.MaxValue;
            List<PlanBlock> blocks = world.Plan.Data.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                if (blocks[b].Inner == null || blocks[b].Outer == null) continue;
                for (int e = 0; e < blocks[b].Inner.Count; e++)
                {
                    if (!SteppedSidewalks.Is(world.Plan, b, e)) continue;
                    Vec2 a = blocks[b].Inner[e], c = blocks[b].Inner[(e + 1) % blocks[b].Inner.Count];
                    double length = (c - a).Length;
                    if (length < 15.0 || length > 120.0) continue;
                    double d = ((a + c) * 0.5 - spawn).Length;
                    if (d < best)
                    {
                        best = d;
                        pick = (b, e);
                    }
                }
            }
            Assert.GreaterOrEqual(pick.block, 0, "no stepped sidewalk");
            PlanBlock block = blocks[pick.block];
            int n = block.Inner.Count, f = (pick.edge + 1) % n;
            Vec2 i0 = block.Inner[pick.edge], i1 = block.Inner[f], o0 = block.Outer[pick.edge], o1 = block.Outer[f];
            Vec2 m0 = (i0 + o0) * 0.5, m1 = (i1 + o1) * 0.5;
            List<(double t0, double t1, double top)> treads = SteppedSidewalks.Treads(world.Plan, block, pick.edge);
            // Climb it from its low end: the line down the strip's middle.
            bool upward = treads[treads.Count - 1].top > treads[0].top;
            Vec2 low = upward ? m0 : m1, high = upward ? m1 : m0;
            double run = (high - low).Length;
            Vec2 dir = (high - low).Normalized;
            double Top(double s)
            {
                double t = upward ? s / run : 1.0 - s / run;
                foreach ((double t0, double t1, double top) in treads)
                {
                    if (t >= t0 && t <= t1) return top;
                }
                return t <= 0.0 ? treads[0].top : treads[treads.Count - 1].top;
            }
            City(new Vector3((float)((low.X + high.X) * 0.5), (float)Top(run * 0.5), (float)((low.Y + high.Y) * 0.5)), run * 0.5 + 8.0);
            yield return null;
            Vec2 start = low + dir * 0.8;
            var made = new PlayerLocomotion[1];
            yield return Player(new Vector3((float)start.X, (float)Top(0.8) + 0.1f, (float)start.Y), made);
            PlayerLocomotion locomotion = made[0];
            float startY = locomotion.transform.position.y;
            var up = new float[2];
            yield return Walk(locomotion, low, dir, 0.0, 0.8, run - 1.0, Top, up);
            float climbed = locomotion.transform.position.y - startY;
            double rise = Top(run - 1.0) - Top(0.8);
            Debug.Log($"CityX stepped sidewalk walk: block {pick.block} edge {pick.edge} ({run:F0} m, {treads.Count} treads, {rise:F1} m up, {best:F0} m from the spawn): " +
                $"got to {up[0]:F1} m, climbed {climbed:F2} m, at most {up[1]:F2} m off the treads");
            Assert.That(up[0], Is.GreaterThan(run - 2.0), $"the climb stopped {run - up[0]:F1} m short");
            Assert.That(climbed, Is.GreaterThan(rise - 0.45), "the body did not climb the steps");
            Assert.That(up[1], Is.LessThan(0.6f), "the body left the steps");
        }
    }
}
