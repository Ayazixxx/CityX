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
    /// A body walks down into a basement (owner, 2026-10-16: "the basement is only 1 foot high"): the real player controller, in a real streamed
    /// city, steps off the lobby floor onto the stair down and follows it to the basement's own floor a storey below. The street's paving used to
    /// run through the basement a foot under the lobby, and the walk stopped on it after three steps.
    /// </summary>
    public sealed class BasementWalkPlayTests
    {
        private const int Seed = 80469103;
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

        [UnityTest]
        public IEnumerator ABodyWalksDownFromTheLobbyToTheBasementFloor()
        {
            var world = new CityWorld(Seed);
            BuildingSite site = default;
            StairPlan down = null;
            foreach (BuildingSite candidate in TestSites.NearSpawn(world, 16))
            {
                if (!CitySurfaceBuilder.HasBasementUnder(candidate) || candidate.Params.Width < 12.0f || candidate.Params.Depth < 12.0f) continue;
                StairPlan basement = BuildingStairs.PlanFor(candidate).Basement;
                if (basement == null) continue;
                site = candidate;
                down = basement;
                break;
            }
            Assert.IsNotNull(down, "no building with a basement stair near the spawn");
            float storey = BuildingStairs.PlanFor(site).Storey;

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            Vector3 InBuilding(Vector2 local, float y)
            {
                site.LocalToWorld(local.x, local.y, out double wx, out double wz);
                return new Vector3((float)wx, y, (float)wz);
            }
            Vector2[] route = down.Route();
            Vector3 top = InBuilding(route[route.Length - 1], site.BaseHeight + 0.3f);
            streamer.StreamAround(top, int.MaxValue);
            Physics.SyncTransforms();
            yield return null;

            var player = new GameObject("Player");
            _made.Add(player);
            player.AddComponent<CharacterController>();
            var locomotion = player.AddComponent<PlayerLocomotion>();
            yield return null;
            player.transform.position = top;
            Physics.SyncTransforms();
            for (int i = 0; i < 20; i++)
            {
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
            float startY = player.transform.position.y;

            // Down the flight: from the cell you step off at the lobby, back along the steps, to the cell at the basement's floor.
            int next = route.Length - 2;
            for (int tick = 0; tick < 2400 && next >= 0; tick++)
            {
                Vector3 here = player.transform.position;
                Vector3 target = InBuilding(route[next], here.y);
                Vector3 toward = target - here;
                toward.y = 0.0f;
                if (toward.magnitude < 0.6f)
                {
                    next--;
                    continue;
                }
                locomotion.Tick(new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg }, Step);
                yield return null;
            }
            float descended = startY - player.transform.position.y;
            Debug.Log($"CityX basement walk: building {site.Id} ({site.Params.Width:F0}x{site.Params.Depth:F0}), a {down.Kind} stair of {storey:F2} m, " +
                $"the body went down {descended:F2} m (lobby at {site.BaseHeight:F2}, ground {world.Plan.GroundHeight(top.x, top.z):F2})");
            Assert.Greater(descended, storey - 0.5f, $"walked down the basement stair and went only {descended:F2} m of its {storey:F2} m: something is in the way");
        }
    }
}
