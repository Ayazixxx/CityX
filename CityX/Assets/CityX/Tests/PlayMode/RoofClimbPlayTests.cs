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
    /// Climbing a real house with a pitched roof (RoofShape): up the wall under its eaves, round the eaves (half a metre out, inside what a climb
    /// leans round: ClimbJut.MaxLean) and onto the slope. The climb must top out ON the roof, never part-way up and never inside geometry.
    /// </summary>
    public sealed class RoofClimbPlayTests
    {
        private const int Seed = 83920571;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator ClimbingAHousesWallUnderItsEavesTopsOutOnItsRoof()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            BuildingSite site = default;
            RoofShape roof = RoofShape.Flat;
            bool found = false;
            foreach (BuildingSite candidate in TestSites.NearSpawn(world, 30))
            {
                RoofShape shape = RoofShape.For(candidate);
                // A gable or hip house, its eaves along the back wall (the ridge along X), no awning across the bottom.
                if (shape.Pitched && shape.Kind != RoofKind.Skillion && shape.AlongX && candidate.Params.Mass == BuildingMass.LowRise
                    && candidate.Params.Width > 9.0f)
                {
                    site = candidate;
                    roof = shape;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "no gable or hip house near the spawn to climb");

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
            float roofY = site.Top;
            Vector3 inward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);   // from the back wall into the building
            streamer.StreamAround(TestSites.At(site, site.CentreX, floorY, site.MinZ - 0.6f), int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            int climbs = 0, onTheRoof = 0;
            for (int i = 0; i < 3; i++)
            {
                float x = (float)site.MinX + 2.5f + i * (float)(site.MaxX - site.MinX - 5.0) / 2.0f;
                Vector3 at = TestSites.At(site, x, floorY, site.MinZ - 0.7f);
                GameObject player = Object.Instantiate(prefab, at, Quaternion.LookRotation(inward, Vector3.up));
                _made.Add(player);
                var climbing = player.GetComponent<PlayerClimbing>();
                var vitals = player.GetComponent<PlayerVitals>();
                vitals.InfiniteStamina = true;
                yield return null;
                yield return null;
                if (!climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f))
                {
                    continue;
                }
                climbs++;

                bool mantled = false;
                const float dt = 1.0f / 60.0f;
                for (float t = 0.0f; t < 30.0f; t += dt)
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
                for (int f = 0; f < 30; f++) yield return null;
                Vector3 p = player.transform.position;
                Debug.Log($"CityX roof climb {i} ({roof.Kind} {roof.Pitch:F0} deg, eaves {roof.Eave:F2} m): mantled={mantled} " +
                    $"{p.y - roofY:F2} m over the eaves' line, grounded={player.GetComponent<CharacterController>().isGrounded}");
                if (mantled)
                {
                    Assert.Greater(p.y, roofY - 0.8f, $"climb {i}: topped out {roofY - p.y:F2} m under the roof");
                    onTheRoof++;
                }
                if (!LedgeProbe.HasRoom(p, BodyRig.StandingHeight, 0.3f, player.transform))
                {
                    Assert.Fail($"climb {i}: the body is inside geometry {p.y - roofY:F2} m over the eaves' line");
                }
                Assert.IsTrue(player.GetComponent<CharacterController>().enabled, $"climb {i}: controller left off");
                Object.Destroy(player);
                yield return null;
            }
            Debug.Log($"CityX roof climbs: {climbs} grabbed, {onTheRoof} on the roof");
            Assert.Greater(climbs, 0, "could not grab the house's wall anywhere");
            Assert.Greater(onTheRoof, 0, "no climb got round the eaves onto the roof");
#else
            yield break;
#endif
        }
    }
}
