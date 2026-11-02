using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Vehicles;
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
    /// A car park is driven into from its street (owner, 2026-10-16: "there was physically no way for the car to drive into it"). The garage near
    /// the spawn dug deepest into its hill is streamed; a body walks in through each opening and ends on the lowest deck, and the real car, driven
    /// from the carriageway in front, ends inside on that deck.
    /// </summary>
    public sealed class GarageDriveInPlayTests
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

        /// <summary>The garage near the spawn whose lowest deck is furthest under the plan's floor (dug into its hill).</summary>
        private static BuildingSite FindGarage(CityWorld world)
        {
            BuildingSite best = BuildingSite.None;
            float bestDug = float.MinValue;
            foreach (BuildingSite s in TestSites.NearSpawn(world, 30))
            {
                if (!s.IsParkingGarage) continue;
                float dug = s.BaseHeight - GarageEntrances.For(world.Plan, s)[0].DeckY;
                if (dug > bestDug)
                {
                    bestDug = dug;
                    best = s;
                }
            }
            return best;
        }

        private void City(Vector3 around)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
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

        private static Vector3 World(BuildingSite site, double localX, double localZ, float y)
        {
            site.LocalToWorld(localX, localZ, out double x, out double z);
            return new Vector3((float)x, y, (float)z);
        }

        [UnityTest]
        public IEnumerator ABodyWalksInThroughEitherOpeningOntoTheLowestDeck()
        {
            var world = new CityWorld(Seed);
            BuildingSite site = FindGarage(world);
            Assert.IsTrue(site.Exists, "no garage near the spawn");
            GarageEntrance[] two = GarageEntrances.For(world.Plan, site);
            float front = site.Params.Depth * 0.5f;
            Debug.Log($"CityX garage {site.Id}: {site.Params.Width:F0} x {site.Params.Depth:F0} x {site.Params.Height:F0} m, lowest deck {two[0].DeckY:F2}, plan floor {site.BaseHeight:F2} " +
                $"({site.BaseHeight - two[0].DeckY:F2} m dug in); entrances: {two[0].StreetY:F2} over {two[0].Length:F1} m, {two[1].StreetY:F2} over {two[1].Length:F1} m");
            City(World(site, 0.0, front, two[0].DeckY));
            yield return null;

            const float dt = 1.0f / 60.0f;
            bool walkedIn = false;
            foreach (GarageEntrance e in two)
            {
                float mid = (e.X0 + e.X1) * 0.5f;
                // KNOWN CORNER (docs/SKY_HUB.md, seed 95998845's garage 3317, on a steep hillside): an opening with no road found in front and the paving
                // outside a metre over the deck. A body on that paving is too tall for it. Not a thing to walk in through; the other opening must be.
                Vector3 outside = World(site, mid, front + 1.5, 0.0f);
                if (!e.HasDriveway && world.Plan.GroundHeight(outside.x, outside.z) > e.DeckY + 0.3)
                {
                    Assert.IsTrue(walkedIn || e.X0 > two[0].X0, "neither of the garage's openings can be walked in through");
                    continue;
                }
                walkedIn = true;
                CharacterController body = Body(World(site, mid, front + 1.5, e.SurfaceAt(-1.5f) + 0.1f));
                Vector3 inward = World(site, mid, front - 1.0, 0.0f) - World(site, mid, front, 0.0f);
                inward.y = 0.0f;
                inward.Normalize();
                float goal = front - e.Length - 3.0f;
                for (float t = 0.0f, walk = (e.Length + 8.0f) / 2.0f; t < walk; t += dt)   // at 2 m/s, the whole ramp and on (one is 33 m long on some seeds)
                {
                    body.Move((inward * 2.0f + Vector3.down * 3.0f) * dt);
                    site.WorldToLocal(body.transform.position.x, body.transform.position.z, out _, out double lz);
                    if (lz < goal) break;
                    yield return null;
                }
                site.WorldToLocal(body.transform.position.x, body.transform.position.z, out double ex, out double ez);
                Debug.Log($"CityX garage walk through the opening at {mid:F0}: ended {front - ez:F1} m in, at y {body.transform.position.y:F2} (deck {e.DeckY:F2})");
                Assert.Less(ez, goal + 0.2, $"the body could not walk in through the opening at {mid:F0}: it stopped {front - ez:F1} m in");
                Assert.AreEqual(e.DeckY, body.transform.position.y, 0.35f, "the body is not on the lowest deck");
                Object.DestroyImmediate(body.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TheCarDrivesInFromTheStreet()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            // The entrance near the spawn with the biggest climb from the road to the deck, of those whose road the car starts on is not tilted
            // more than 6 degrees along the way in (a road on a steep hillside tilts across its width: no car drives up that, garage or none).
            BuildingSite site = BuildingSite.None;
            GarageEntrance e = default;
            float bestClimb = -1.0f;
            foreach (BuildingSite s in TestSites.NearSpawn(world, 30))
            {
                if (!s.IsParkingGarage) continue;
                foreach (GarageEntrance candidate in GarageEntrances.For(world.Plan, s))
                {
                    if (!candidate.HasDriveway) continue;
                    float m = (candidate.X0 + candidate.X1) * 0.5f, f = s.Params.Depth * 0.5f;
                    // Not a road with a row of barricades across it (the evacuation's cordons stand in the way of any car).
                    GarageEntrances.CarriagewayOut(world.Plan, s, m, out int roadEdge);
                    if (roadEdge < 0 || CitySurfaceBuilder.HasCordon(world.Plan.Geometry.Edges[roadEdge])) continue;
                    s.LocalToWorld(m, f + candidate.KerbOut + 2.0, out double nx, out double nz);
                    s.LocalToWorld(m, f + candidate.KerbOut + 7.0, out double fx, out double fz);
                    float tilt = Mathf.Atan2((float)System.Math.Abs(world.Plan.GroundHeight(nx, nz) - world.Plan.GroundHeight(fx, fz)), 5.0f) * Mathf.Rad2Deg;
                    float climb = Mathf.Abs(candidate.DeckY - candidate.RoadY);
                    if (tilt <= 6.0f && climb > bestClimb)
                    {
                        bestClimb = climb;
                        site = s;
                        e = candidate;
                    }
                }
            }
            Assert.IsTrue(site.Exists, "no garage near the spawn with a drive from a level road");
            float front = site.Params.Depth * 0.5f;
            float mid = (e.X0 + e.X1) * 0.5f;
            float kerb = e.KerbOut;
            City(World(site, mid, front, e.DeckY));
            yield return null;

            // On the carriageway, 4 m out past the kerb, facing the opening.
            double startOut = kerb + 4.0;
            site.LocalToWorld(mid, front + startOut, out double sx, out double sz);
            float road = (float)(world.Plan.GroundHeight(sx, sz) + CitySurfaceBuilder.RoadLift);
            Vector3 inward = World(site, mid, front - 1.0, 0.0f) - World(site, mid, front, 0.0f);
            inward.y = 0.0f;
            inward.Normalize();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Vehicles/Mustang.prefab");
            Assert.IsNotNull(prefab, "no car prefab");
            GameObject carObject = Object.Instantiate(prefab, new Vector3((float)sx, road + 1.3f, (float)sz), Quaternion.LookRotation(inward, Vector3.up));
            _made.Add(carObject);
            var car = carObject.GetComponent<Car>();
            car.enabled = false;   // driven here, step by step, not by its own (empty) driver's seat
            var body = carObject.GetComponent<Rigidbody>();

            double goal = front - e.Length - 6.0;
            var trace = new System.Text.StringBuilder();
            float lowestSpeed = float.MaxValue;
            double ez = double.MaxValue;
            for (float t = 0.0f; t < 14.0f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                float speed = Vector3.Dot(body.linearVelocity, inward);
                car.TickDriving(0.0f, speed < 4.0f ? 1.0f : 0.0f, false, Time.fixedDeltaTime);
                site.WorldToLocal(carObject.transform.position.x, carObject.transform.position.z, out _, out ez);
                if (t > 2.0f) lowestSpeed = Mathf.Min(lowestSpeed, speed);
                if (ez < goal) break;
                if (Mathf.Abs(t - Mathf.Round(t)) < Time.fixedDeltaTime * 0.5f)
                {
                    // Once a second: where the car is, how it sits, and what is just ahead of its nose and under its wheels.
                    Vector3 c = carObject.transform.position;
                    string ahead = "";
                    foreach (float h in new[] { -0.9f, -0.5f, 0.0f, 0.6f })
                    {
                        Vector3 from = c + Vector3.up * h;
                        foreach (RaycastHit hit in Physics.RaycastAll(from, inward, 5.0f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (hit.collider.transform.IsChildOf(carObject.transform)) continue;
                            ahead += $" [{h:F1}: '{hit.collider.name}' {hit.distance:F2} m, normal ({hit.normal.x:F2},{hit.normal.y:F2},{hit.normal.z:F2})]";
                        }
                    }
                    string under = Physics.Raycast(c, Vector3.down, out RaycastHit floor, 4.0f, ~0, QueryTriggerInteraction.Ignore) ? $"'{floor.collider.name}' {floor.distance:F2} m down" : "nothing";
                    // Low along the ground from just behind the front bumper: a ridge or a step the box's nose meets.
                    var box = carObject.GetComponent<BoxCollider>();
                    Vector3 nose = carObject.transform.TransformPoint(box.center + new Vector3(0.0f, -box.size.y * 0.5f, box.size.z * 0.5f - 0.05f));
                    foreach (float h in new[] { 0.03f, 0.08f, 0.14f, 0.22f, 0.32f })
                    {
                        if (Physics.Raycast(nose + Vector3.up * h, inward, out RaycastHit low, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            site.WorldToLocal(low.point.x, low.point.z, out double hx, out double hz);
                            ahead += $" [bumper+{h:F2}: {low.distance:F2} m, {hz - front:F2} m out from the wall at x {hx:F1}, y {low.point.y:F2}, normal ({low.normal.x:F2},{low.normal.y:F2},{low.normal.z:F2})]";
                        }
                    }
                    trace.AppendLine($"  t={t:F0}: {front - ez:F1} m in, y {c.y:F2}, speed {speed:F2}, pitch {carObject.transform.eulerAngles.x:F0} roll {carObject.transform.eulerAngles.z:F0}; under: {under}; ahead:{ahead}");
                }
            }
            Vector3 p = carObject.transform.position;
            Debug.Log($"CityX garage drive: garage {site.Id}, opening at {mid:F0} ({e.StreetY:F2} to the deck {e.DeckY:F2} over {e.Length:F1} m), kerb {kerb:F1} m out; " +
                $"the car ended {front - ez:F1} m in at y {p.y:F2}, slowest after the start {lowestSpeed:F2} m/s");
            if (ez >= goal + 0.5) Debug.Log("CityX garage drive, second by second:\n" + trace);
            Assert.Less(ez, goal + 0.5, $"the car did not get in: it ended {front - ez:F1} m in from the front wall (at {p})");
            Assert.That(p.y - e.DeckY, Is.InRange(0.3f, 2.5f), "the car is not on the lowest deck");
#else
            yield break;
#endif
        }
    }
}
