using System.Collections;
using System.Collections.Generic;
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
    /// their cycle, from a driver's eye and a walker's, by night, flashing late at night, under snow; a side street's STOP sign; an all-way stop. And the street
    /// itself (docs/AMERICAN_STREETS.md) to streets-*.png: its sidewalk, a corner, its street lights by day and by night. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed partial class TrafficCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private double _clock;
        private CityScale _scaleBefore;
        private DevConsoleView _console;
        private PlayerVitals _player;
        private Camera _camera;
        private PlanWorld _plan;
        private Vec2 _spawn;

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
            yield return Open();
            DevConsoleView console = _console;
            PlayerVitals player = _player;
            Camera camera = _camera;
            PlanWorld plan = _plan;
            Vec2 spawn = _spawn;

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

        [UnityTest]
        public IEnumerator RenderTheStreets()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Open();
            var lamps = new List<StreetLamp>();
            EdgeGeometry street = NearestStreet(_plan, _spawn, lamps, powered: true);
            Assert.IsNotNull(street, "no street near the spawn with power for its lamps");
            TestContext.WriteLine($"street {street.Edge.Id} ({street.Edge.Class}), {lamps.Count} lamps");
            yield return Go(_console, _player, _camera, street.A + street.Dir * (street.Length * 0.5));

            // The sidewalk from a walker's eye and from above, and the corner at the street's end.
            Vec2 across = street.Dir.Perp;
            double kerb = street.HalfWidth;
            Vec2 walker = street.A + street.Dir * (street.TrimA + 12.0) + across * (kerb + 2.2);
            Vector3 walkerEye = World(_plan, walker, 2.3), ahead = World(_plan, walker + street.Dir * 16.0 - across * 1.2, 0.4);
            yield return Shoot(_camera, "streets-1-sidewalk", walkerEye, ahead);
            Vec2 onTheKerb = street.A + street.Dir * (street.TrimA + 22.0) + across * (kerb + 1.0);
            yield return Shoot(_camera, "streets-1-sidewalk-above", World(_plan, onTheKerb, 13.0) + new Vector3(0.6f, 0.0f, -0.6f), World(_plan, onTheKerb, 0.0));
            Vec2 corner = street.A + street.Dir * street.TrimA + across * kerb;
            yield return Shoot(_camera, "streets-2-corner", World(_plan, corner + street.Dir * 11.0 + across * 3.0, 2.3), World(_plan, corner, 0.3));
            yield return Shoot(_camera, "streets-2-corner-above", World(_plan, corner, 20.0) + new Vector3(0.8f, 0.0f, -0.8f), World(_plan, corner, 0.0));

            // The street lights: down the street, one close up, and from above; then the same by night; then a street with no power.
            Vec2 driver = street.A + street.Dir * (street.TrimA + 3.0) + across * (kerb * 0.5);
            Vector3 driverEye = World(_plan, driver, 2.3), down = World(_plan, street.A + street.Dir * (street.Length * 0.7), 5.0);
            StreetLamp lamp = lamps[lamps.Count / 2];
            Vector3 head = World(_plan, lamp.Head, lamp.Height - 0.8);
            Vector3 nearLamp = World(_plan, lamp.At + lamp.Over * 7.0 + street.Dir * 6.0, 3.2);
            Vector3 above = World(_plan, lamp.Head, 42.0) + new Vector3(1.0f, 0.0f, -1.0f);
            yield return Shoot(_camera, "streets-3-lamps-day", driverEye, down);
            yield return Shoot(_camera, "streets-3-lamp-near", nearLamp, head);
            _console.Submit("/time midnight");
            yield return null;
            yield return null;
            yield return ShootByLamplight(_camera, "streets-4-lamps-night", driverEye, down);
            yield return ShootByLamplight(_camera, "streets-4-lamp-near-night", nearLamp, head);
            yield return ShootByLamplight(_camera, "streets-4-lamps-night-above", above, World(_plan, lamp.At, 0.0));
            yield return ShootByLamplight(_camera, "streets-4-sidewalk-night", walkerEye, ahead);

            // A hydrant and its red kerb, by day.
            _console.Submit("/time noon");
            yield return null;
            var things = new List<StreetFurnishing>();
            StreetFurniture.Along(_plan, street, things);
            foreach (StreetFurnishing thing in things)
            {
                if (thing.Kind != StreetFurnitureKind.Hydrant)
                {
                    continue;
                }
                Vector3 hydrant = World(_plan, thing.At, 0.6);
                Vector3 eye = World(_plan, thing.At + thing.ToRoad * 4.5 + thing.Along * 5.5, 2.4);
                yield return Shoot(_camera, "streets-6-hydrant", eye, hydrant);
                break;
            }
            _console.Submit("/time midnight");
            yield return null;

            EdgeGeometry dark = NearestStreet(_plan, _spawn, lamps, powered: false);
            if (dark != null)
            {
                yield return Go(_console, _player, _camera, dark.A + dark.Dir * (dark.Length * 0.5));
                Vector3 darkEye = World(_plan, dark.A + dark.Dir * (dark.TrimA + 3.0) + dark.Dir.Perp * (dark.HalfWidth * 0.5), 2.3);
                Vector3 darkDown = World(_plan, dark.A + dark.Dir * (dark.Length * 0.7), 5.0);
                yield return ShootByLamplight(_camera, "streets-5-no-power-night", darkEye, darkDown);
                _console.Submit("/time noon");
                yield return null;
                yield return null;
                yield return Shoot(_camera, "streets-5-no-power-day", darkEye, darkDown);
            }
            _console.Submit("/time noon");
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        /// <summary>The game's own city at noon with its clock stopped, the player parked and the camera ours to place.</summary>
        private IEnumerator Open()
        {
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

            _console = Object.FindFirstObjectByType<DevConsoleView>();
            _player = Object.FindFirstObjectByType<PlayerVitals>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (follow != null) follow.enabled = false;
            _camera = Camera.main;
            _plan = CurrentWorld.Instance.Plan;
            _plan.TryFindSpawn(out double spawnX, out double spawnZ);
            _spawn = new Vec2(spawnX, spawnZ);
            SignalClock.Shared = () => _clock;
            _console.Submit("/time noon");
            _console.Cycle.Running = false;
        }
#endif

        /// <summary>The nearest street with at least three street lights, with power for them or without; its lamps are left in <paramref name="lamps"/>.</summary>
        private static EdgeGeometry NearestStreet(PlanWorld plan, Vec2 from, List<StreetLamp> lamps, bool powered)
        {
            EdgeGeometry best = null;
            double nearest = double.MaxValue;
            foreach (EdgeGeometry g in plan.Geometry.Edges)
            {
                if (g == null || !StreetLights.Has(plan, g.Edge) || StreetLights.Powered(plan, g.Edge) != powered) continue;
                lamps.Clear();
                StreetLights.Along(plan, g, lamps);
                double away = (g.A + g.Dir * (g.Length * 0.5) - from).Length;
                if (lamps.Count >= 3 && away < nearest)
                {
                    nearest = away;
                    best = g;
                }
            }
            lamps.Clear();
            if (best != null) StreetLights.Along(plan, best, lamps);
            return best;
        }

        /// <summary>A picture by night: the street lights' real light is stood for this eye first (StreetLampDirector lends it to the nearest lamps).</summary>
        private static IEnumerator ShootByLamplight(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            StreetLampDirector.Direct(position);
            yield return Shoot(camera, name, position, lookAt);
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
