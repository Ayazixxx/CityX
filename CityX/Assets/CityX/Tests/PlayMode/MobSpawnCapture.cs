using System.Collections;
using System.Collections.Generic;
using System.Text;
using CityX.Character;
using CityX.UI;
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
    /// Not a test: the world's own enemies in the game's own city (docs/MOB_SPAWNING.md), to Logs/CityShots/mobs-*.png: the player stood
    /// in a street away from the start for 45 seconds, what has appeared at random round them pictured one by one (a ghost or a monster on
    /// open ground, a humanoid on a floor of a building), and how many there are of each, how far off, and how long the budget took to
    /// fill, logged. Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.MobSpawnCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class MobSpawnCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        private Scene _scene;
        private CityScale _scaleBefore;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CityScale.Active = _scaleBefore;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheWorldsEnemies()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            // The game's own city, not the classic one the tests run on: the pictures and the counts are of what is played.
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            PlayerInputReader reader = Object.FindFirstObjectByType<PlayerInputReader>();
            reader.enabled = false;
            GameObject player = reader.gameObject;
            player.GetComponent<PlayerVitals>().InfiniteHealth = true;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (follow != null) follow.enabled = false;
            Camera camera = Camera.main;
            var spawner = Object.FindFirstObjectByType<MobSpawner>();
            Assert.IsNotNull(spawner, "the scene has no MobSpawner -- rebuild the massing scene");
            PlanWorld plan = CurrentWorld.Instance.Plan;
            console.Submit("/time noon");
            console.Cycle.Running = false;

            // A street away from the start (nothing appears round Old Guy's house).
            plan.TryFindSpawn(out double startX, out double startZ);
            Vector3 street = default;
            double nearest = double.MaxValue;
            foreach (EdgeGeometry g in plan.Geometry.Edges)
            {
                if (g == null || g.Edge == null || !g.Edge.Alive || g.Edge.Bridge) continue;
                double x = g.A.X + g.Dir.X * g.Length * 0.5, z = g.A.Y + g.Dir.Y * g.Length * 0.5;
                double away = System.Math.Sqrt((x - startX) * (x - startX) + (z - startZ) * (z - startZ));
                if (away >= 280.0 && away < nearest && MobHabitats.At(plan, x, z, out _) == MobHabitat.Outdoors)
                {
                    nearest = away;
                    street = new Vector3((float)x, (float)plan.GroundHeight(x, z), (float)z);
                }
            }
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = street + Vector3.up;
            controller.enabled = true;
            camera.transform.position = player.transform.position + Vector3.up * 3.0f;

            // 45 seconds of the game's own time for the city to build round the player and its enemies to appear; when each budget first
            // filled is noted.
            float began = Time.time, outdoorsFull = -1.0f, indoorsFull = -1.0f;
            while (Time.time < began + 45.0f)
            {
                if (outdoorsFull < 0.0f && spawner.AliveIn(MobHabitat.Outdoors) >= MobHabitats.MostOutdoors) outdoorsFull = Time.time - began;
                if (indoorsFull < 0.0f && spawner.AliveIn(MobHabitat.Indoors) >= MobHabitats.MostIndoors) indoorsFull = Time.time - began;
                yield return null;
            }

            var alive = new List<(GameObject Body, MobHabitat Habitat)>();
            spawner.CopyAlive(alive);
            var log = new StringBuilder();
            var buildingsNear = new List<int>();
            MobHabitats.BuildingsNear(plan, PlanWorld.ChunkOf(street.x), PlanWorld.ChunkOf(street.z), Mathf.CeilToInt(MobSpawnRules.ToMetres / (float)PlanWorld.ChunkSize), buildingsNear);
            log.AppendLine($"{buildingsNear.Count} buildings near by can hold enemies");
            log.AppendLine($"the player stands in a street at ({street.x:0}, {street.z:0}); after 45 s {spawner.AliveIn(MobHabitat.Outdoors)} are alive out of doors (at most {MobHabitats.MostOutdoors}"
                + $"{(outdoorsFull >= 0.0f ? $", full after {outdoorsFull:0} s" : ", never full")}) and {spawner.AliveIn(MobHabitat.Indoors)} indoors (at most {MobHabitats.MostIndoors}"
                + $"{(indoorsFull >= 0.0f ? $", full after {indoorsFull:0} s" : ", never full")})");
            int shot = 0;
            foreach ((GameObject body, MobHabitat habitat) in alive)
            {
                Vector3 where = body.transform.position;
                string kind = body.GetComponent<Wraith>() != null ? MobSpawnRules.Ghost : body.GetComponent<CrimsonClaw>() != null ? MobSpawnRules.Monster
                    : body.GetComponentInChildren<CityX.Weapons.SwordWeapon>() != null ? MobSpawnRules.Swordsman : MobSpawnRules.Gunner;
                log.AppendLine($"  {kind} ({(habitat == MobHabitat.Indoors ? "indoors" : "out of doors")}) {Vector3.Distance(where, player.transform.position):0} m off");
                // From a few strides in front of it, at head height, looking at it.
                Vector3 from = where + body.transform.forward * 5.0f + Vector3.up * 1.9f;
                yield return null;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"mobs-{shot}-{kind}-{(habitat == MobHabitat.Indoors ? "indoors" : "outdoors")}", from, where + Vector3.up * 1.3f);
                shot++;
            }
            Debug.Log("CityX the world's enemies:\n" + log);
            Assert.Greater(shot, 0, "nothing appeared round the player in 45 seconds");
#else
            yield break;
#endif
        }
    }
}
