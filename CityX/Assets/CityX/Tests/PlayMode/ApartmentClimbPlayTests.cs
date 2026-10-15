using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Climbing the back of a real apartment block (ApartmentFront): a balcony on every floor stands out two metres from the wall. Whatever a climb up
    /// that wall does, it must end cleanly: never with the body inside a slab, a parapet or a partition, never with the controller left off.
    /// </summary>
    public sealed class ApartmentClimbPlayTests
    {
        private const int Seed = 47360519;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator ClimbingTheBalconySideEndsCleanly()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            BuildingSite site = default;
            bool found = false;
            foreach (BuildingSite candidate in TestSites.NearSpawn(world))
            {
                ApartmentFront front = ApartmentFront.For(candidate, WindowPlan.For(world, candidate));
                if (front.Balconies && (candidate.Contacts == null || candidate.Contacts.Length == 0) && candidate.Params.Width > 12.0f)
                {
                    site = candidate;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "no free-standing apartment block near the spawn");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);

            float floorY = site.BaseHeight;
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            Vector3 inward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);   // from the back wall into the building
            streamer.StreamAround(TestSites.At(site, site.CentreX, floorY, site.MinZ - 0.6f), int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            int climbs = 0, onABalcony = 0;
            float highest = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                float x = (float)site.MinX + 2.5f + i * (float)(site.MaxX - site.MinX - 5.0) / 3.0f;
                Vector3 at = TestSites.At(site, x, floorY, site.MinZ - 0.7f);
                GameObject player = Object.Instantiate(prefab, at, Quaternion.LookRotation(inward, Vector3.up));
                _made.Add(player);
                var climbing = player.GetComponent<PlayerClimbing>();
                var vitals = player.GetComponent<PlayerVitals>();
                vitals.InfiniteStamina = true;
                yield return null;
                yield return null;
                if (!climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f)) continue;
                climbs++;

                bool mantled = false;
                const float dt = 1.0f / 60.0f;
                for (float t = 0.0f; t < 20.0f; t += dt)
                {
                    if (climbing.IsClimbing) climbing.Tick(false, true, false, vitals, dt, false);
                    else if (climbing.IsToppingOut)
                    {
                        mantled = true;
                        climbing.TickMantle(dt);
                    }
                    else break;
                    yield return null;
                }
                // Let whatever it ended in settle (a fall, a landing on a balcony).
                for (int f = 0; f < 90; f++) yield return null;
                Vector3 p = player.transform.position;
                float rel = p.y - floorY;
                bool balcony = rel > rows.Size * 0.5f && Mathf.Abs(rel - Mathf.Round(rel / rows.Size) * rows.Size) < 0.3f;
                onABalcony += balcony ? 1 : 0;
                highest = Mathf.Max(highest, rel);
                Debug.Log($"CityX apartment climb {i}: mantled={mantled} climbing={climbing.IsClimbing} {rel:F2} m up (rows of {rows.Size:F2}), " +
                    $"on a balcony {balcony}, grounded={player.GetComponent<CharacterController>().isGrounded}");
                if (!LedgeProbe.HasRoom(p, BodyRig.StandingHeight, 0.3f, player.transform))
                {
                    var names = new List<string>();
                    foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * 0.36f, p + Vector3.up * (BodyRig.StandingHeight - 0.3f), 0.27f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!c.transform.IsChildOf(player.transform)) names.Add($"{c.name} ({c.GetType().Name})");
                    }
                    Assert.Fail($"climb {i}: the body is inside geometry {rel:F2} m up: {string.Join(", ", names)}");
                }
                Assert.IsTrue(player.GetComponent<CharacterController>().enabled, $"climb {i}: controller left off");
                Object.Destroy(player);
                yield return null;
            }
            Debug.Log($"CityX apartment climbs: {climbs} grabbed, {onABalcony} ended on a balcony, highest {highest:F1} m");
            Assert.Greater(climbs, 0, "could not grab the balcony side anywhere");
#else
            yield break;
#endif
        }
    }
}
