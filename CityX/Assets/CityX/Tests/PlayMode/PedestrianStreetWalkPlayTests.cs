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
    /// A pedestrian street is walked (owner, 2026-10-15: "make everything walkable and functionable as a pedestrian not just look good"): a body
    /// the player's size steps onto the setts past the bollards at one end and walks one side's walk to the far end, among the trees and tables,
    /// never stopped and never through the ground; and the bollards really do stand across the end.
    /// </summary>
    public sealed class PedestrianStreetWalkPlayTests
    {
        private const int Seed = 63428175;
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

        [UnityTest]
        public IEnumerator ABodyWalksTheStreetEndToEndAndTheBollardsStopItAtTheMouth()
        {
            var world = new CityWorld(Seed);
            PlanWorld plan = world.Plan;
            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            RoadEdge edge = null;
            EdgeGeometry g = null;
            double best = double.MaxValue;
            foreach (RoadEdge e in plan.Data.Edges)
            {
                if (!PedestrianStreets.Is(plan, e)) continue;
                EdgeGeometry eg = plan.Geometry.Edges[e.Id];
                if (eg == null || eg.Length - eg.TrimA - eg.TrimB < 24.0 || eg.HalfWidth < PedestrianStreets.WalkWidth + 0.7) continue;
                double d = ((e.A.P + e.B.P) * 0.5 - spawn).Length;
                if (d < best) { best = d; edge = e; g = eg; }
            }
            Assert.IsNotNull(edge, "no pedestrian street wide enough to walk");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 4;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            Vec2 mid = g.A + g.Dir * (g.Length * 0.5);
            float Paving(Vec2 p) => (float)(plan.GroundHeight(p.X, p.Y) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight);
            Vector3 World(Vec2 p, float lift = 0.0f) => new Vector3((float)p.X, Paving(p) + lift, (float)p.Y);
            streamer.StreamAround(World(mid), int.MaxValue);
            Physics.SyncTransforms();
            yield return null;

            // Down one walk: 1.5 m in from the ribbon's side, from just past the near bollard line to just before the far one.
            double side = g.HalfWidth - 1.5;
            double t0 = g.TrimA + PedestrianStreets.EndInset + 1.0, t1 = g.Length - g.TrimB - PedestrianStreets.EndInset - 1.0;
            Vec2 start = g.A + g.Dir * t0 + g.Dir.Perp * side;
            Vec2 end = g.A + g.Dir * t1 + g.Dir.Perp * side;
            CharacterController body = Body(World(start, 0.1f));
            var flat = new Vector3((float)g.Dir.X, 0.0f, (float)g.Dir.Y);
            float lowest = float.MaxValue;
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < (t1 - t0) / 2.0 + 3.0; t += dt)
            {
                var here = new Vec2(body.transform.position.x, body.transform.position.z);
                if (Vec2.Dot(end - here, g.Dir) < 0.3) break;   // there (the setts end a kerb's step above the plate just beyond the bollards)
                body.Move((flat * 2.0f + Vector3.down * 3.0f) * dt);
                here = new Vec2(body.transform.position.x, body.transform.position.z);
                lowest = Mathf.Min(lowest, body.transform.position.y - Paving(here));
                yield return null;
            }
            double walked = Vec2.Dot(new Vec2(body.transform.position.x, body.transform.position.z) - start, g.Dir);
            Debug.Log($"CityX pedestrian street walk: edge {edge.Id} ({g.Length:F0} m long, {g.HalfWidth * 2.0:F1} m wide, {best:F0} m from the spawn): walked {walked:F1} of {t1 - t0:F1} m, lowest {lowest:F2} m to the setts");
            if (walked < t1 - t0 - 1.5)
            {
                // What stopped it: whatever stands ahead at knee, hip, chest and head height, and where that is on the street.
                var stopped = new Vec2(body.transform.position.x, body.transform.position.z);
                float ground = Paving(stopped);
                // Rays along the street from 2.5 m behind the body (clear of its own capsule), every half metre across the whole ribbon and at four
                // heights: what stands ahead, and how much of the street's width it takes (seed 46283197: a cordon of barricades across the middle).
                var perp = new Vector3((float)g.Dir.Perp.X, 0.0f, (float)g.Dir.Perp.Y);
                Vector3 behind = new Vector3(body.transform.position.x, ground, body.transform.position.z) - flat * 2.5f - perp * (float)side;
                for (float w = -(float)g.HalfWidth + 0.3f; w <= (float)g.HalfWidth - 0.3f; w += 0.5f)
                {
                    foreach (float h in new[] { 0.1f, 0.4f, 1.0f, 2.2f })
                    {
                        if (Physics.Raycast(behind + perp * w + Vector3.up * h, flat, out RaycastHit hit, 6.0f) && hit.collider.gameObject != body.gameObject)
                        {
                            var p = new Vec2(hit.point.x, hit.point.z);
                            string parent = hit.collider.transform.parent != null ? hit.collider.transform.parent.name : "-";
                            Debug.Log($"CityX pedestrian street walk: at w={Vec2.Dot(p - g.A, g.Dir.Perp):F1} {h:F1} m up: '{hit.collider.name}' (under '{parent}') at t={Vec2.Dot(p - g.A, g.Dir):F1}, " +
                                $"{hit.point.y - ground:F2} m over the setts, normal ({hit.normal.x:F2},{hit.normal.y:F2},{hit.normal.z:F2})");
                        }
                    }
                }
            }
            Assert.That(lowest, Is.GreaterThan(-0.3f), "the body fell through the street");
            Assert.That(walked, Is.GreaterThan(t1 - t0 - 1.5), $"the walk is blocked: the body stopped {t1 - t0 - walked:F1} m short of the far end");

            // On out past the far bollard line: the bollards stand 1.6 m apart, so a 0.9 m body walks out between two of them onto the junction
            // (the street is closed to cars, not to people). Aim at the middle of the gap nearest this walk's line.
            double lineAlong = g.Length - g.TrimB - PedestrianStreets.EndInset;
            Vec2 lineFar = g.A + g.Dir * lineAlong;
            var ws = new List<double>();
            foreach (OpenPiece p in PedestrianStreets.Furniture(plan, g))
            {
                if (p.Kind == OpenProp.Bollard && System.Math.Abs(Vec2.Dot(p.At - g.A, g.Dir) - lineAlong) < 0.2) ws.Add(Vec2.Dot(p.At - g.A, g.Dir.Perp));
            }
            ws.Sort();
            Assert.GreaterOrEqual(ws.Count, 2, "no bollards across the far end");
            double gap = double.NaN;
            for (int i = 0; i + 1 < ws.Count; i++)
            {
                double middle = (ws[i] + ws[i + 1]) * 0.5;
                if (double.IsNaN(gap) || System.Math.Abs(middle - side) < System.Math.Abs(gap - side)) gap = middle;
            }
            Vec2 before = lineFar - g.Dir * 2.5 + g.Dir.Perp * gap;
            body.transform.position = World(before, 0.1f);
            Physics.SyncTransforms();
            for (float t = 0.0f; t < 4.0f; t += dt)
            {
                body.Move((flat * 1.5f + Vector3.down * 3.0f) * dt);
                yield return null;
            }
            double past = Vec2.Dot(new Vec2(body.transform.position.x, body.transform.position.z) - lineFar, g.Dir);
            Assert.That(past, Is.GreaterThan(1.0), $"the body could not walk out between the bollards (it got {past:F2} m past the line)");
        }
    }
}
