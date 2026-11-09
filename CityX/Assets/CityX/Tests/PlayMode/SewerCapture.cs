using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Not a test: the sewers (docs/SEWERS.md) in the real city, to Logs/CityShots/sewer-*.png: an open manhole from the street and from
    /// over it, the room under it with its ladder, a tunnel of each kind, and a mouth of each kind of gate from the channel and from inside.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SewerCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        private CityStreamer _streamer;
        private GameObject _viewer;

        private IEnumerator StandAt(Vector3 where)
        {
            _viewer.transform.position = where;
            _streamer.Rebuild();
            _streamer.StreamAround(where, int.MaxValue);
            yield return new WaitForSecondsRealtime(2.0f);
        }

        [UnityTest]
        public IEnumerator RenderTheSewers()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _streamer = Object.FindFirstObjectByType<CityStreamer>();
            var world = new CityWorld(_streamer.WorldSeed);
            PlanWorld plan = world.Plan;
            SewerPlan sewers = SewerPlan.For(plan);
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            var cameraObject = new GameObject("SewerCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            // A lamp carried with the camera, to see the shapes by.
            var lampObject = new GameObject("SewerLamp");
            lampObject.transform.SetParent(cameraObject.transform, false);
            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 40.0f;
            lamp.intensity = 60.0f;
            lamp.color = new Color(1.0f, 0.92f, 0.78f);
            lamp.enabled = false;
            _viewer = new GameObject("SewerViewer");
            _streamer.Viewer = _viewer.transform;

            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);

            // The open manhole nearest the spawn.
            SewerRoom room = null;
            foreach (SewerRoom candidate in sewers.Rooms)
            {
                if (candidate.Shaft == null || !candidate.Shaft.Open) continue;
                if (room == null || (candidate.Node.P - spawn).Length < (room.Node.P - spawn).Length) room = candidate;
            }
            Assert.IsNotNull(room, "no open manhole in this city");
            Ladder ladder = Ladders.Of(room.Shaft);
            Vector3 normal = ladder.Normal, top = ladder.Top;
            Vector3 centre = new Vector3((float)room.Node.P.X, top.y, (float)room.Node.P.Y);
            yield return StandAt(centre);
            BuildingShapeCapture.Shoot(camera, directory, "sewer-1-manhole-street", centre + normal * 7.0f + Vector3.up * 2.4f + Vector3.Cross(Vector3.up, normal) * 2.5f, centre);
            BuildingShapeCapture.Shoot(camera, directory, "sewer-2-manhole-down", centre + Vector3.up * 3.0f + normal * 0.4f, centre + Vector3.down * 4.0f);
            lamp.enabled = true;
            Vector3 floor = new Vector3(centre.x, (float)room.CentreFloor, centre.z);
            BuildingShapeCapture.Shoot(camera, directory, "sewer-3-room-ladder", floor + normal * 5.5f + Vector3.up * 2.6f + Vector3.Cross(Vector3.up, normal) * 1.5f, floor + Vector3.up * 3.2f);
            BuildingShapeCapture.Shoot(camera, directory, "sewer-3-room-up", floor + normal * 2.5f + Vector3.up * 1.2f, floor + Vector3.up * 8.0f);
            Debug.Log($"CityX sewer capture: room {room.Node.Id}, {room.Plate.Length} sides, ladder {ladder.Height:F1} m");

            // A tunnel of each kind and style, nearest the spawn: from its walkway, looking along it.
            var seen = new HashSet<int>();
            var tunnels = new List<(double Distance, SewerTube Tube)>();
            foreach (RoadEdge edge in plan.Data.Edges)
            {
                SewerTube tube = sewers.Tube(edge.Id);
                if (tube == null || tube.End - tube.Start < 30.0) continue;
                tunnels.Add(((tube.At((tube.Start + tube.End) * 0.5) - spawn).Length, tube));
            }
            tunnels.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            foreach ((double _, SewerTube tube) in tunnels)
            {
                int key = (int)tube.Section.Kind * 4 + (int)tube.Section.Style;
                if (!seen.Add(key)) continue;
                double along = tube.Start + 6.0;
                Vec2 at = tube.At(along), ahead = tube.At(along + 20.0);
                Vec2 aside = tube.Dir.Perp * (tube.Section.FloorHalfWidth * 0.55);
                var eye = new Vector3((float)(at.X + aside.X), (float)tube.FloorAt(along) + 2.9f, (float)(at.Y + aside.Y));
                yield return StandAt(eye);
                var look = new Vector3((float)ahead.X, (float)tube.FloorAt(along + 20.0) + 2.0f, (float)ahead.Y);
                string name = $"sewer-4-{tube.Section.Kind}-{tube.Section.Style}".ToLowerInvariant();
                BuildingShapeCapture.Shoot(camera, directory, name, eye, look);
                if (seen.Count == 1)
                {
                    // As it is played: the sky's light gone (SewerAir does this as the eye goes down), by the lamp and then without it.
                    DayNightCycle.Underground = 1.0f;
                    cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                    BuildingShapeCapture.Shoot(camera, directory, "sewer-6-dark-with-a-lamp", eye, look);
                    lamp.enabled = false;
                    BuildingShapeCapture.Shoot(camera, directory, "sewer-6-dark-without", eye, look);
                    BuildingShapeCapture.Shoot(camera, directory, "sewer-6-dark-room-under-the-open-manhole",
                        floor + normal * 6.0f + Vector3.up * 2.6f + Vector3.Cross(Vector3.up, normal) * 2.0f, floor + Vector3.up * 3.0f);
                    lamp.enabled = true;
                    DayNightCycle.Underground = 0.0f;
                    cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                }
                if (seen.Count >= 8) break;
            }

            // A mouth of each kind of gate, nearest the spawn: from the open cut, from the bank beside it, and from inside looking out.
            var mouths = new Dictionary<SewerGate, SewerOutfall>();
            foreach (SewerOutfall outfall in sewers.AllOutfalls)
            {
                if (!mouths.TryGetValue(outfall.Gate, out SewerOutfall best)
                    || (outfall.Tube.At(outfall.Portal) - spawn).Length < (best.Tube.At(best.Portal) - spawn).Length)
                {
                    mouths[outfall.Gate] = outfall;
                }
            }
            foreach (KeyValuePair<SewerGate, SewerOutfall> pair in mouths)
            {
                SewerOutfall outfall = pair.Value;
                SewerTube tube = outfall.Tube;
                Vector3 dir = new Vector3((float)tube.Dir.X, 0.0f, (float)tube.Dir.Y), side = Vector3.Cross(Vector3.up, dir);
                Vec2 p = tube.At(outfall.Portal);
                var portal = new Vector3((float)p.X, (float)outfall.Floor, (float)p.Y);
                yield return StandAt(portal);
                string name = $"sewer-5-mouth-{pair.Key}".ToLowerInvariant();
                float back = Mathf.Min(14.0f, (float)(outfall.End - outfall.Portal) + 4.0f);
                lamp.enabled = false;
                BuildingShapeCapture.Shoot(camera, directory, name + "-front", portal + dir * back + Vector3.up * 2.6f + side * 1.2f, portal + Vector3.up * 2.4f);
                BuildingShapeCapture.Shoot(camera, directory, name + "-bank", portal + dir * (back + 10.0f) + Vector3.up * 9.0f + side * 16.0f, portal + Vector3.up * 2.0f);
                lamp.enabled = true;
                BuildingShapeCapture.Shoot(camera, directory, name + "-inside", portal - dir * 9.0f + Vector3.up * 2.6f - side * 1.0f, portal + dir * 6.0f + Vector3.up * 2.0f);
                Debug.Log($"CityX sewer capture: mouth {pair.Key} under bridge {outfall.Bridge.Id}, {tube.Section.Kind} {tube.Section.Style}, " +
                    $"headwall {outfall.Portal:F0} m from its room, open cut {outfall.End - outfall.Portal:F0} m");
            }
            Object.Destroy(cameraObject);
            Object.Destroy(_viewer);
#else
            yield break;
#endif
        }
    }
}
