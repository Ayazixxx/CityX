using System.Collections;
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
    /// Not a test: renders the traffic lights and STOP signs in the real city (docs/TRAFFIC_CONTROL.md) to Logs/CityShots/traffic-*.png: the lights at the spawn through
    /// their cycle, from a driver's eye and a walker's, by night, flashing late at night, under snow; a side street's STOP sign; an all-way stop. Explicit; run by name
    /// with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class TrafficCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private double _clock;
        private CityScale _scaleBefore;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SignalClock.Shared = null;
            CityScale.Active = _scaleBefore;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheTrafficControl()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            // The game's own city, its roads wider than the classic city's the tests run on: the pictures are of what is played.
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            var player = Object.FindFirstObjectByType<PlayerVitals>();
            player.GetComponent<PlayerInputReader>().enabled = false;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (follow != null) follow.enabled = false;
            Camera camera = Camera.main;
            PlanWorld plan = CurrentWorld.Instance.Plan;
            plan.TryFindSpawn(out double spawnX, out double spawnZ);
            var spawn = new Vec2(spawnX, spawnZ);
            SignalClock.Shared = () => _clock;
            console.Submit("/time noon");
            console.Cycle.Running = false;

            TrafficJunction lights = Nearest(plan, spawn, j => j.Control == JunctionControl.Signal && j.ClassOf(true) >= RoadClass.Arterial);
            Assert.IsNotNull(lights, "no lights on a main road in this city");
            SignalTiming timing = lights.Timing;
            TestContext.WriteLine($"lights at node {lights.Node.Id}: {lights.ClassOf(true)} x {lights.ClassOf(false)}, cycle {timing.Cycle}, main {timing.GreenMajor}+{timing.YellowMajor}+{timing.AllRedMajor}, side {timing.GreenMinor}+{timing.YellowMinor}+{timing.AllRedMinor}");
            yield return Go(console, player, camera, lights.Node.P);

            int main = FirstLeg(lights, true), side = FirstLeg(lights, false);
            Vector3 middle = World(plan, lights.Node.P, 0.0);
            // A driver's eye on the main road, 30 m back from the junction on the right of the road, and one on the side street.
            Vector3 driver = World(plan, lights.Node.P + lights.Dirs[main] * 32.0 + lights.Dirs[main].Perp * (lights.HalfWidths[main] * 0.5), 2.2);
            Vector3 sideDriver = World(plan, lights.Node.P + lights.Dirs[side] * 26.0 + lights.Dirs[side].Perp * (lights.HalfWidths[side] * 0.5), 2.2);
            Vector3 aim = middle + Vector3.up * 5.0f;

            // Through the cycle, as the main road's driver sees it: green, yellow, the all-red, then red while the side street goes.
            yield return At(timing, 2.0);
            yield return Shoot(camera, "traffic-1-main-green", driver, aim);
            yield return Shoot(camera, "traffic-1-side-red", sideDriver, aim);
            yield return At(timing, timing.GreenMajor + 1.0);
            yield return Shoot(camera, "traffic-2-main-yellow", driver, aim);
            yield return At(timing, timing.MinorStart + 2.0);
            yield return Shoot(camera, "traffic-3-main-red", driver, aim);
            yield return Shoot(camera, "traffic-3-side-green", sideDriver, aim);

            // The whole junction from above and from a corner, and a walk signal close up while it says walk, and while its hand shows.
            yield return At(timing, 2.0);
            yield return Shoot(camera, "traffic-4-above", middle + new Vector3(2.0f, 46.0f, -2.0f), middle);
            Vec2 corner = lights.Corners[main];
            Vector3 walker = World(plan, corner + (corner - lights.Node.P).Normalized * 5.0 + lights.Dirs[main] * 4.0, 2.3);
            yield return Shoot(camera, "traffic-5-walker", walker, middle + Vector3.up * 3.6f);
            int before = (main + lights.Legs.Length - 1) % lights.Legs.Length;
            if (lights.HasCorner[main] && lights.HasCorner[before])
            {
                Vector3 far = World(plan, lights.Corners[before], 3.3);
                Vector3 near = World(plan, lights.Corners[main] + (lights.Corners[main] - lights.Corners[before]).Normalized * 1.0 + lights.Dirs[main] * 2.5, 2.4);
                yield return At(timing, timing.MinorStart + 2.0);
                yield return Shoot(camera, "traffic-6-walk", near, far);
                yield return At(timing, 2.0);
                yield return Shoot(camera, "traffic-6-hand", near, far);
            }

            // By night; flashing in the dead of night; and under snow.
            console.Submit("/time midnight");
            yield return At(timing, 2.0);
            yield return null;
            TestContext.WriteLine($"midnight: flashing {SignalLampDriver.Flashing}, these lights flash at night {timing.NightFlash}");
            yield return Shoot(camera, "traffic-7-night", driver, aim);
            _clock += 0.5;
            yield return null;
            yield return null;
            yield return Shoot(camera, "traffic-7-night-half-a-second-on", driver, aim);
            console.Submit("/time noon");
            yield return null;

            // A side street's STOP at a main road, and an all-way stop.
            TrafficJunction stop = Nearest(plan, spawn, j => j.Control == JunctionControl.StopMinor && j.ClassOf(true) >= RoadClass.Arterial);
            if (stop != null)
            {
                yield return Go(console, player, camera, stop.Node.P);
                int stopping = StoppingLeg(stop);
                Vector3 stopDriver = World(plan, stop.Node.P + stop.Dirs[stopping] * (stop.Trims[stopping] + 16.0) + stop.Dirs[stopping].Perp * (stop.HalfWidths[stopping] * 0.5), 2.2);
                Vector3 junction = World(plan, stop.Node.P, 2.5);
                yield return Shoot(camera, "traffic-9-stop-sign", stopDriver, junction);
                Vector3 sign = World(plan, stop.Node.P + stop.Dirs[stopping] * (stop.Trims[stopping] + 6.0) + stop.Dirs[stopping].Perp * (stop.HalfWidths[stopping] + 0.8), 3.3);
                yield return Shoot(camera, "traffic-9-stop-sign-near", sign + World(plan, stop.Dirs[stopping] * 4.0 - stop.Dirs[stopping].Perp * 1.5, 0.0) - World(plan, new Vec2(0.0, 0.0), 0.0) + Vector3.down * 0.6f, sign);
                yield return Shoot(camera, "traffic-9-stop-above", junction + new Vector3(1.0f, 42.0f, -1.0f), junction);
            }
            TrafficJunction allWay = Nearest(plan, spawn, j => j.Control == JunctionControl.AllWayStop);
            if (allWay != null)
            {
                yield return Go(console, player, camera, allWay.Node.P);
                int leg = StoppingLeg(allWay);
                Vector3 junction = World(plan, allWay.Node.P, 2.5);
                Vector3 allWayDriver = World(plan, allWay.Node.P + allWay.Dirs[leg] * (allWay.Trims[leg] + 16.0) + allWay.Dirs[leg].Perp * (allWay.HalfWidths[leg] * 0.5), 2.2);
                yield return Shoot(camera, "traffic-10-all-way", allWayDriver, junction);
                yield return Shoot(camera, "traffic-10-all-way-above", junction + new Vector3(1.0f, 42.0f, -1.0f), junction);
            }

            // The lights again under snow.
            yield return Go(console, player, camera, lights.Node.P);
            yield return At(timing, 2.0);
            console.Submit("/weather deepsnow");
            yield return new WaitForSeconds(3.0f);
            yield return Shoot(camera, "traffic-8-snow", driver, aim);
            console.Submit("/weather dry");
#else
            yield break;
#endif
        }

        private static TrafficJunction Nearest(PlanWorld plan, Vec2 from, System.Func<TrafficJunction, bool> wanted)
        {
            TrafficJunction best = null;
            double nearest = double.MaxValue;
            foreach (TrafficJunction junction in plan.Traffic.Junctions)
            {
                if (junction == null || !wanted(junction)) continue;
                double away = (junction.Node.P - from).Length;
                if (away < nearest)
                {
                    nearest = away;
                    best = junction;
                }
            }
            return best;
        }

        private static int FirstLeg(TrafficJunction junction, bool major)
        {
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                if (junction.Drives[i] && junction.Major[i] == major) return i;
            }
            return 0;
        }

        private static int StoppingLeg(TrafficJunction junction)
        {
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                if (junction.Stops(i)) return i;
            }
            return 0;
        }

        private static Vector3 World(PlanWorld plan, Vec2 at, double above) => new Vector3((float)at.X, (float)(plan.GroundHeight(at.X, at.Y) + above), (float)at.Y);

        private static IEnumerator Go(DevConsoleView console, PlayerVitals player, Camera camera, Vec2 to)
        {
            PlanWorld plan = CurrentWorld.Instance.Plan;
            Vector3 at = World(plan, to, 0.3);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            // Off to one side, out of the pictures.
            player.transform.position = at + new Vector3(60.0f, 0.0f, 60.0f);
            controller.enabled = true;
            camera.transform.position = at + Vector3.up * 3.0f;
            console.Streamer.Rebuild();
            console.Streamer.StreamAround(at, int.MaxValue);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
        }

        /// <summary>The shared clock set so the junction is <paramref name="into"/> seconds into its cycle.</summary>
        private IEnumerator At(SignalTiming timing, double into)
        {
            _clock = SignalTiming.Mod(into - timing.Offset, timing.Cycle) + timing.Cycle * 4.0;
            yield return null;
            yield return null;
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
