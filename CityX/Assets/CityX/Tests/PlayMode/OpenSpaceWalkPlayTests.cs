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
    /// A furnished square is walked (owner, 2026-10-15: "make everything walkable and functionable as a pedestrian not just look good"): a body
    /// the player's size steps in at the front kerb, walks the front walk to the middle, turns and walks the side walk out to the side kerb, on the
    /// real streamed paving among the real pieces, never stopped and never through the ground.
    /// </summary>
    public sealed class OpenSpaceWalkPlayTests
    {
        private const int Seed = 19077151;
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

        /// <summary>Walks the body toward a point at 2 m/s until it is within half a metre or the time a straight walk would take, plus three seconds, is up.</summary>
        private static IEnumerator WalkTo(CharacterController body, Vector3 target, System.Func<Vector3, float> paving, System.Action<float> lowest)
        {
            const float dt = 1.0f / 60.0f;
            float budget = Vector3.Distance(body.transform.position, target) / 2.0f + 3.0f;
            for (float t = 0.0f; t < budget; t += dt)
            {
                Vector3 to = target - body.transform.position;
                to.y = 0.0f;
                if (to.magnitude < 0.5f) yield break;
                body.Move((to.normalized * 2.0f + Vector3.down * 3.0f) * dt);
                lowest(body.transform.position.y - paving(body.transform.position));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ABodyWalksTheFrontWalkToTheMiddleAndTheSideWalkOut()
        {
            var world = new CityWorld(Seed);
            PlanWorld plan = world.Plan;
            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            int chosen = -1;
            double best = double.MaxValue;
            OpenSpaceLayout layout = null;
            foreach (int index in plan.Data.PlazaBlocks)
            {
                OpenSpaceLayout l = OpenSpace.LayoutFor(plan, index);
                if (l == null || l.Role != OpenSpaceRole.Plaza) continue;
                double d = (PlanMath.Centroid(plan.Data.Blocks[index].Inner) - spawn).Length;
                if (d < best) { best = d; chosen = index; layout = l; }
            }
            Assert.GreaterOrEqual(chosen, 0, "no furnished square in the city");
            List<Vec2> ring = plan.Data.Blocks[chosen].Inner;
            int n = ring.Count;
            Vec2 c = PlanMath.Centroid(ring);
            Vec2 Door(int e)
            {
                Vec2 a = ring[e], b = ring[(e + 1) % n];
                return (a + b) * 0.5 + (b - a).Normalized.Perp * 1.6;
            }
            // The side: the next kerb round from the front that is long enough to have a walk of its own.
            int side = -1;
            for (int k = 1; k < n && side < 0; k++)
            {
                int e = (layout.Front + k) % n;
                if ((ring[(e + 1) % n] - ring[e]).Length >= 6.0) side = e;
            }
            Assert.GreaterOrEqual(side, 0, "the square has only one kerb long enough to walk in from");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 3;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            float Paving(Vector3 p) => (float)(plan.GroundHeight(p.x, p.z) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight);
            Vector3 World(Vec2 p) => new Vector3((float)p.X, Paving(new Vector3((float)p.X, 0.0f, (float)p.Y)), (float)p.Y);
            Vector3 centre = World(c);
            streamer.StreamAround(centre, int.MaxValue);
            Physics.SyncTransforms();
            yield return null;

            Vector3 frontDoor = World(Door(layout.Front));
            Vector3 sideDoor = World(Door(side));
            // The walks stop short of the piece in the middle: 5 m from the centre covers the widest focal piece.
            Vector3 toCentre = centre - frontDoor;
            toCentre.y = 0.0f;
            Vector3 nearMiddle = centre - toCentre.normalized * 5.0f;
            Vector3 fromSide = centre - sideDoor;
            fromSide.y = 0.0f;
            Vector3 nearMiddleSide = centre - fromSide.normalized * 5.0f;

            CharacterController body = Body(frontDoor + Vector3.up * 0.1f);
            float lowest = float.MaxValue;
            yield return WalkTo(body, nearMiddle, Paving, h => lowest = Mathf.Min(lowest, h));
            float reachedMiddle = Vector3.Distance(new Vector3(body.transform.position.x, 0.0f, body.transform.position.z), new Vector3(nearMiddle.x, 0.0f, nearMiddle.z));
            yield return WalkTo(body, nearMiddleSide, Paving, h => lowest = Mathf.Min(lowest, h));
            yield return WalkTo(body, sideDoor, Paving, h => lowest = Mathf.Min(lowest, h));
            float reachedSide = Vector3.Distance(new Vector3(body.transform.position.x, 0.0f, body.transform.position.z), new Vector3(sideDoor.x, 0.0f, sideDoor.z));
            Debug.Log($"CityX open space walk: block {chosen} ({layout.Role}, {layout.Pieces.Count} pieces, {best:F0} m from the spawn): the body ended {reachedMiddle:F2} m from the middle's walk end and {reachedSide:F2} m from the side kerb, lowest {lowest:F2} m to the paving");
            Assert.That(lowest, Is.GreaterThan(-0.3f), "the body fell through the paving");
            Assert.That(reachedMiddle, Is.LessThan(1.5f), $"the front walk is blocked: the body stopped {reachedMiddle:F1} m from its end");
            Assert.That(reachedSide, Is.LessThan(1.5f), $"the side walk is blocked: the body stopped {reachedSide:F1} m from the side kerb");
        }
    }
}
