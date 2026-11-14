using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The world's own enemies in the real scene, playing alone (owner, 2026-11-14: "lets try implementing mobs spawning in the world ...
    /// monster and ghost type enemies spawn outside and humanoid enemies spawn indoors for starters", and "it should be more akin to
    /// minecraft where they spawn at random"; docs/MOB_SPAWNING.md): they appear at random round the player, ghosts and monsters on open
    /// ground and humanoids on the floors of buildings, never more than the budget, never in the player's face; a killed one is gone, the
    /// ones left behind are taken away, and /mobs off takes them all away.
    /// </summary>
    public sealed class MobSpawnerPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        private Scene _scene;
        private GameObject _player;
        private MobSpawner _spawner;
        private PlanWorld _plan;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

#if UNITY_EDITOR
        private IEnumerator LoadTheCity()
        {
            // The real scene's camera cannot render under the default -nographics run; unrelated to what is under test.
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            // (The humanoid enemies are built on the player's body: the player is the one with the keys.)
            PlayerInputReader reader = Object.FindFirstObjectByType<PlayerInputReader>();
            Assert.IsNotNull(reader, "no player in the real scene");
            _player = reader.gameObject;
            _player.GetComponent<PlayerVitals>().InfiniteHealth = true;
            _spawner = Object.FindFirstObjectByType<MobSpawner>();
            Assert.IsNotNull(_spawner, "the scene has no MobSpawner -- rebuild the massing scene");
            Assert.IsNotNull(_spawner.Streamer, "the spawner was not given the city");
            yield return Until(() => _spawner.Streamer.World != null, 20.0f, "the city to start");
            _plan = _spawner.Streamer.World.Plan;
        }

        private static IEnumerator Until(System.Func<bool> done, float seconds, string what) => Until(done, seconds, () => what);

        private static IEnumerator Until(System.Func<bool> done, float seconds, System.Func<string> what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup, until, $"timed out after {seconds:0} s waiting for {what()}");
                yield return null;
            }
        }

        /// <summary>
        /// The middle of a street at least <paramref name="fromStart"/> metres from the city's start: the nearest such to it. Well clear
        /// of Old Guy's house, round which nothing appears. With <paramref name="buildingsNear"/>, one with at least that many buildings
        /// that can hold enemies near by (much of the city's building stock cannot: wings, sealed blocks, narrow ones).
        /// </summary>
        private Vector3 AStreet(double fromStart, int buildingsNear = 0)
        {
            var near = new List<int>();
            int reach = Mathf.CeilToInt(MobSpawnRules.ToMetres / (float)PlanWorld.ChunkSize) - 1;
            Assert.IsTrue(_plan.TryFindSpawn(out double startX, out double startZ));
            Vector3 best = default;
            double nearest = double.MaxValue;
            foreach (EdgeGeometry g in _plan.Geometry.Edges)
            {
                if (g == null || g.Edge == null || !g.Edge.Alive || g.Edge.Bridge)
                {
                    continue;
                }
                double x = g.A.X + g.Dir.X * g.Length * 0.5, z = g.A.Y + g.Dir.Y * g.Length * 0.5;
                double away = System.Math.Sqrt((x - startX) * (x - startX) + (z - startZ) * (z - startZ));
                if (away >= fromStart && away < nearest && MobHabitats.At(_plan, x, z, out _) == MobHabitat.Outdoors)
                {
                    if (buildingsNear > 0)
                    {
                        near.Clear();
                        MobHabitats.BuildingsNear(_plan, PlanWorld.ChunkOf(x), PlanWorld.ChunkOf(z), reach, near);
                        if (near.Count < buildingsNear)
                        {
                            continue;
                        }
                    }
                    nearest = away;
                    best = new Vector3((float)x, (float)_plan.GroundHeight(x, z), (float)z);
                }
            }
            Assert.Less(nearest, double.MaxValue, "no street that far from the start");
            return best;
        }

        private void StandAt(Vector3 ground)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.position = ground + Vector3.up;
            controller.enabled = true;
        }
#endif

        [UnityTest]
        public IEnumerator TheyAppearAtRandomRoundThePlayerGhostsAndMonstersOutOfDoorsAndHumanoidsOnTheFloorsOfBuildings()
        {
#if UNITY_EDITOR
            yield return LoadTheCity();
            Vector3 street = AStreet(280.0, buildingsNear: 6);
            StandAt(street);

            var seen = new HashSet<GameObject>();
            var alive = new List<(GameObject Body, MobHabitat Habitat)>();
            int outdoors = 0, indoors = 0;
            // Each one, the frame it is first seen: where it appeared and what it is.
            yield return Until(() =>
            {
                alive.Clear();
                _spawner.CopyAlive(alive);
                Assert.LessOrEqual(_spawner.AliveIn(MobHabitat.Outdoors), MobHabitats.MostOutdoors, "more alive out of doors than the budget");
                Assert.LessOrEqual(_spawner.AliveIn(MobHabitat.Indoors), MobHabitats.MostIndoors, "more alive indoors than the budget");
                foreach ((GameObject body, MobHabitat habitat) in alive)
                {
                    if (!seen.Add(body))
                    {
                        continue;
                    }
                    Vector3 where = body.transform.position, player = _player.transform.position;
                    float away = new Vector2(where.x - player.x, where.z - player.z).magnitude;
                    Assert.LessOrEqual(away, MobSpawnRules.ToMetres + 3.0f, "an enemy appeared out of range");
                    if (habitat == MobHabitat.Outdoors)
                    {
                        outdoors++;
                        Assert.GreaterOrEqual(away, MobSpawnRules.OutdoorsFromMetres - 3.0f, "an enemy appeared in the player's face");
                        Assert.IsTrue(body.GetComponent<Wraith>() != null || body.GetComponent<CrimsonClaw>() != null, "something other than a ghost or a monster appeared out of doors");
                        Assert.Less(_plan.BuildingAt(where.x, where.z), 0, "a ghost or a monster appeared inside a building");
                        // (The built ground can lie a little off the height the plan gives a spot: a kerb, a slope across a block. The spawner allows that much.)
                        Assert.That(where.y - (float)_plan.GroundHeight(where.x, where.z), Is.InRange(-MobSpawner.GroundHeightTolerance - 0.1f, 5.0f), "an outdoor enemy is not over the ground");
                    }
                    else
                    {
                        indoors++;
                        Assert.GreaterOrEqual(away, MobSpawnRules.IndoorsFromMetres - 3.0f, "an enemy appeared on top of the player");
                        Assert.IsNotNull(body.GetComponent<PlayerVitals>(), "a building's enemy is not a humanoid");
                        Assert.IsNull(body.GetComponent<PlayerInputReader>(), "a building's enemy is a player");
                        int building = _plan.BuildingAtHeight(where.x, where.y + 1.0, where.z);
                        Assert.GreaterOrEqual(building, 0, "a humanoid appeared outside every building");
                        BuildingSite site = _plan.Buildings[building];
                        Assert.IsTrue(MobHabitats.Holds(site), "a humanoid appeared in a building that should hold none");
                        float offItsFloor = float.MaxValue;
                        for (int floor = 0; floor < MobHabitats.Floors(site); floor++)
                        {
                            offItsFloor = Mathf.Min(offItsFloor, Mathf.Abs(where.y - MobHabitats.FloorY(site, floor)));
                        }
                        Assert.Less(offItsFloor, 1.0f, "a humanoid appeared between two floors");
                    }
                }
                return outdoors >= 2 && indoors >= 2;
            }, 150.0f, () => $"two enemies to appear out of doors and two indoors ({outdoors} had out of doors, {indoors} indoors)");

            // A humanoid stays on its floor: it has not fallen through it.
            GameObject humanoid = null;
            foreach ((GameObject body, MobHabitat habitat) in alive)
            {
                if (habitat == MobHabitat.Indoors) humanoid = body;
            }
            Assert.IsNotNull(humanoid);
            float floorAt = humanoid.transform.position.y;
            float settleUntil = Time.realtimeSinceStartup + 2.0f;
            while (Time.realtimeSinceStartup < settleUntil)
            {
                yield return null;
            }
            Assert.IsNotNull(humanoid, "the humanoid was taken away while the player stood by");
            Assert.Greater(humanoid.transform.position.y, floorAt - 0.5f, "the humanoid fell through its floor");

            // Killed, it is this machine's no longer.
            GameObject victim = null;
            foreach ((GameObject body, MobHabitat habitat) in alive)
            {
                if (habitat == MobHabitat.Outdoors && body != null) victim = body;
            }
            Assert.IsNotNull(victim);
            victim.GetComponent<IDamageable>().ApplyHit(100000.0f, _player.transform.position, 0.0f);
            yield return Until(() =>
            {
                alive.Clear();
                _spawner.CopyAlive(alive);
                return !alive.Exists(held => held.Body == victim);
            }, 5.0f, "the killed enemy to be counted no longer");

            // Left behind: whatever was alive round the player is taken out of the world.
            alive.Clear();
            _spawner.CopyAlive(alive);
            var before = new List<GameObject>();
            foreach ((GameObject body, MobHabitat _) in alive) before.Add(body);
            Assert.IsNotEmpty(before);
            Vector3 far = AStreet(280.0 + MobHabitats.CrowdMetres + MobSpawnRules.ToMetres + 150.0);
            Assert.Greater(Vector3.Distance(far, street), MobHabitats.CrowdMetres + MobSpawnRules.ToMetres + 20.0f, "the far street is not far from the first");
            StandAt(far);
            yield return Until(() => before.TrueForAll(left => left == null), 15.0f, "the enemies left behind to be taken away");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-14: "Cleared buildings should stop spawning enemies". The last humanoid alive in a building killed, the
        /// building is cleared: nothing more appears in it, while the others near by go on being occupied.
        /// </summary>
        [UnityTest]
        public IEnumerator ABuildingClearedOfItsEnemiesStaysEmpty()
        {
#if UNITY_EDITOR
            yield return LoadTheCity();
            StandAt(AStreet(280.0, buildingsNear: 6));
            var alive = new List<(GameObject Body, MobHabitat Habitat)>();
            int BuildingOf(GameObject body) => _plan.BuildingAtHeight(body.transform.position.x, body.transform.position.y + 1.0, body.transform.position.z);

            // The first humanoid to appear, and the building it is in.
            int cleared = -1;
            yield return Until(() =>
            {
                alive.Clear();
                _spawner.CopyAlive(alive);
                foreach ((GameObject body, MobHabitat habitat) in alive)
                {
                    if (habitat == MobHabitat.Indoors && BuildingOf(body) >= 0)
                    {
                        cleared = BuildingOf(body);
                        return true;
                    }
                }
                return false;
            }, 150.0f, "a humanoid to appear in a building");
            Assert.IsFalse(_spawner.IsCleared(cleared), "a building with an enemy alive in it is called cleared");

            // Every enemy in that building is killed (another may have appeared in it meanwhile: those too).
            yield return Until(() =>
            {
                alive.Clear();
                _spawner.CopyAlive(alive);
                foreach ((GameObject body, MobHabitat habitat) in alive)
                {
                    if (habitat == MobHabitat.Indoors && BuildingOf(body) == cleared)
                    {
                        body.GetComponent<IDamageable>().ApplyHit(100000.0f, _player.transform.position, 0.0f);
                    }
                }
                return _spawner.IsCleared(cleared);
            }, 20.0f, "the building to be cleared once its enemies are dead");

            // Nothing appears in it again, though the budget has room and the other buildings fill.
            int elsewhere = 0;
            var seen = new HashSet<GameObject>();
            float watchUntil = Time.realtimeSinceStartup + 45.0f;
            while (Time.realtimeSinceStartup < watchUntil)
            {
                alive.Clear();
                _spawner.CopyAlive(alive);
                foreach ((GameObject body, MobHabitat habitat) in alive)
                {
                    if (habitat != MobHabitat.Indoors || !seen.Add(body))
                    {
                        continue;
                    }
                    Assert.AreNotEqual(cleared, BuildingOf(body), "an enemy appeared in a building that had been cleared");
                    elsewhere++;
                }
                yield return null;
            }
            Assert.IsTrue(_spawner.IsCleared(cleared), "the building is no longer cleared");
            Assert.Greater(elsewhere, 0, "nothing appeared in any other building either: the spawner has stopped, not the building");
#else
            yield break;
#endif
        }

        /// <summary>/mobs off: the ones alive are taken away at once and none appear; on again, they are back.</summary>
        [UnityTest]
        public IEnumerator TurnedOffTheWorldsEnemiesAreTakenAwayAndNoneAppearUntilTheyAreTurnedOnAgain()
        {
#if UNITY_EDITOR
            yield return LoadTheCity();
            StandAt(AStreet(280.0));
            yield return Until(() => _spawner.Alive > 0, 120.0f, "an enemy to appear");
            var alive = new List<(GameObject Body, MobHabitat Habitat)>();
            _spawner.CopyAlive(alive);
            Assert.IsNotEmpty(alive);

            _spawner.SetSpawning(false);
            yield return Until(() => alive.TrueForAll(held => held.Body == null), 5.0f, "the enemies to be taken away");
            float quietUntil = Time.realtimeSinceStartup + 6.0f;
            while (Time.realtimeSinceStartup < quietUntil)
            {
                Assert.AreEqual(0, _spawner.Alive, "an enemy appeared while they were turned off");
                yield return null;
            }

            _spawner.SetSpawning(true);
            yield return Until(() => _spawner.Alive > 0, 60.0f, "the enemies to come back once turned on");
#else
            yield break;
#endif
        }
    }
}
