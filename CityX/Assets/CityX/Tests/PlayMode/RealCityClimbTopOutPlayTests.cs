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
    /// THE REPORTED BUG, on the real city: climbing a real building's outside wall mantled at the floor of an inner storey and carried the player through
    /// the outer wall to the inside. Climbs a real streamed facade at several spots; wherever the climb ends in a mantle it must end ON the building's roof (up
    /// and over the top), never at an interior floor part-way up, and never with the body inside geometry.
    /// </summary>
    public sealed class RealCityClimbTopOutPlayTests
    {
        private const int Seed = 87650022;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator ClimbingARealFacadeNeverTopsOutAtAnInteriorFloor()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            BuildingSite site = default;
            bool found = false;
            foreach (BuildingSite candidate in TestSites.NearSpawn(world))
            {
                // A plain box with a roof to reach: not round, not sculpture, not under a sealed block, and no shop awning across the bottom
                // (a climber cannot get past an overhang, which would make this a test of awnings).
                bool plain = !candidate.Shell && !candidate.OnRoof && candidate.Sides < 3 && !candidate.RoofSealed
                    && !WindowPlan.For(world, candidate).Storefront;
                if (plain && candidate.Params.Width > 10.0f && candidate.Params.Height > 12.0f)
                {
                    site = candidate;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "no tall building to test against");

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
            Vector3 inward = -CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
            streamer.StreamAround(TestSites.At(site, site.CentreX, floorY, site.MaxZ + 0.6f), int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            float roofY = floorY + site.Params.Height;
            int climbs = 0;
            int mantles = 0;
            for (int i = 0; i < 6; i++)
            {
                float x = (float)site.MinX + 2.0f + i * (float)(site.MaxX - site.MinX - 4.0) / 5.0f;
                Vector3 at = TestSites.At(site, x, floorY, site.MaxZ + 0.7f);
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
                float mantleStartY = 0.0f;
                const float dt = 1.0f / 60.0f;
                for (float t = 0.0f; t < 40.0f; t += dt)
                {
                    if (climbing.IsClimbing) climbing.Tick(false, true, false, vitals, dt, false);
                    else if (climbing.IsToppingOut)
                    {
                        if (!mantled) mantleStartY = player.transform.position.y;
                        mantled = true;
                        climbing.TickMantle(dt);
                    }
                    else break;
                    yield return null;
                }
                Debug.Log($"CityX climb spot {i}: mantled={mantled} climbing={climbing.IsClimbing} y={player.transform.position.y:F1} roof={roofY:F1} start={floorY:F1} stamina={vitals.Stamina:F1} grounded={player.GetComponent<CharacterController>().isGrounded}");
                if (mantled)
                {
                    mantles++;
                    Assert.Greater(player.transform.position.y, roofY - 1.0f,
                        $"spot {i}: topped out at y={player.transform.position.y:F1} (began mantle at {mantleStartY:F1}), well below the roof at {roofY:F1}: an interior floor");
                }
                if (!LedgeProbe.HasRoom(player.transform.position, BodyRig.StandingHeight, 0.3f, player.transform))
                {
                    var names = new List<string>();
                    Vector3 p = player.transform.position;
                    foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * 0.36f, p + Vector3.up * (BodyRig.StandingHeight - 0.3f), 0.27f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!c.transform.IsChildOf(player.transform)) names.Add($"{c.name} ({c.GetType().Name})");
                    }
                    Assert.Fail($"spot {i}: the body is inside geometry at {p}: {string.Join(", ", names)}");
                }
                Assert.IsTrue(player.GetComponent<CharacterController>().enabled, $"spot {i}: controller left off");
            }
            Debug.Log($"CityX facade climbs: {climbs} grabbed, {mantles} topped out");
            Assert.Greater(climbs, 0, "could not grab the facade anywhere");
            Assert.Greater(mantles, 0, "no facade climb reached the roof");
#else
            yield break;
#endif
        }
    }
}
