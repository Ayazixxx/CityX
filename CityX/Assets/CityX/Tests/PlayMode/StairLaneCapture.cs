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
    /// Not a test: the stairways through the steep blocks, the step streets and the hills (2026-10-16, owner: "make more staircases throughout
    /// the city overall ... san francisco has a lot of randoms staircases that go up hills ... maybe make the terrain more hilly overall"). The
    /// three stairways nearest the spawn from the bottom, the top and above; the view from the spawn up the steepest road leaving it (its
    /// sidewalks sloping paving: the stepped ones were taken out); the nearest step street from its foot and its top; the steepest road
    /// near the spawn from its foot; and the entrance stair near the spawn that folds into the most lanes, from the road and from above, to
    /// Logs/CityShots/stairs-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StairLaneCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheStairsAndHills()
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
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            var world = new CityWorld(streamer.WorldSeed);
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.45f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            RenderSettings.fog = false;
            var cameraObject = new GameObject("StairCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 2500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var viewer = new GameObject("StairViewer");
            streamer.Viewer = viewer.transform;
            world.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);

            // The stairways nearest the spawn.
            var lanes = new List<StairLane>(world.Plan.Data.StairLanes);
            lanes.Sort((p, q) => (((p.A + p.B) * 0.5) - spawn).Length.CompareTo((((q.A + q.B) * 0.5) - spawn).Length));
            for (int k = 0; k < 3 && k < lanes.Count; k++)
            {
                StairLane lane = lanes[k];
                List<StairTread> treads = StairLanes.Profile(lane, world.Plan.Fields);
                Vec2 dir = lane.Dir;
                Vector3 At(double s, double across, double over) =>
                    new Vector3((float)(lane.A.X + dir.X * s + dir.Perp.X * across), (float)(CitySurfaceBuilder.TreadTopAt(treads, s) + over),
                        (float)(lane.A.Y + dir.Y * s + dir.Perp.Y * across));
                Vector3 middle = At(lane.Length * 0.5, 0.0, 0.0);
                viewer.transform.position = middle;
                streamer.Rebuild();
                streamer.StreamAround(middle, int.MaxValue);
                yield return new WaitForSecondsRealtime(2.0f);
                // From the street below, a little back across the pavement, looking up it; from its top looking down; from above one side.
                Vector3 bottom = At(-2.5, 0.8, 2.2), top = At(lane.Length + 1.5, -0.8, 2.2);
                BuildingShapeCapture.Shoot(camera, directory, $"stairs-way{k}-up", bottom, At(lane.Length * 0.6, 0.0, 1.5));
                BuildingShapeCapture.Shoot(camera, directory, $"stairs-way{k}-down", top, At(lane.Length * 0.4, 0.0, 1.0));
                BuildingShapeCapture.Shoot(camera, directory, $"stairs-way{k}-above", At(lane.Length + 7.0, 0.0, 10.0), At(lane.Length * 0.45, 0.0, 0.0));
                Debug.Log($"CityX stair capture: stairway {lane.Id} ({lane.Length:F0} m, {lane.Drop:F1} m up, {treads.Count} treads), " +
                    $"{((lane.A + lane.B) * 0.5 - spawn).Length:F0} m from the spawn");
            }

            // From the spawn up the steepest road leaving its junction (where the owner stood, 2026-10-16, looking at the stepped sidewalk that
            // was taken out): its sidewalks are sloping paving.
            RoadNode spawnNode = null;
            double nodeBest = double.MaxValue;
            foreach (RoadNode node in world.Plan.Data.Nodes)
            {
                double d = (node.P - spawn).Length;
                if (d < nodeBest && node.Edges.Count > 0)
                {
                    nodeBest = d;
                    spawnNode = node;
                }
            }
            if (spawnNode != null)
            {
                RoadEdge uphill = null;
                double rise = double.MinValue;
                foreach (RoadEdge edge in spawnNode.Edges)
                {
                    if (!edge.Alive) continue;
                    RoadNode other = edge.Other(spawnNode);
                    double r = world.Plan.GroundHeight(other.P.X, other.P.Y) - world.Plan.GroundHeight(spawnNode.P.X, spawnNode.P.Y);
                    if (r > rise)
                    {
                        rise = r;
                        uphill = edge;
                    }
                }
                if (uphill != null)
                {
                    Vec2 toward = (uphill.Other(spawnNode).P - spawnNode.P).Normalized;
                    Vec2 eye = spawnNode.P - toward * 4.0 + toward.Perp * 2.0, look = spawnNode.P + toward * 30.0;
                    var target = new Vector3((float)look.X, (float)world.Plan.GroundHeight(look.X, look.Y) + 1.0f, (float)look.Y);
                    viewer.transform.position = new Vector3((float)spawnNode.P.X, (float)world.Plan.GroundHeight(spawnNode.P.X, spawnNode.P.Y), (float)spawnNode.P.Y);
                    streamer.Rebuild();
                    streamer.StreamAround(viewer.transform.position, int.MaxValue);
                    yield return new WaitForSecondsRealtime(2.0f);
                    BuildingShapeCapture.Shoot(camera, directory, "stairs-spawn-uphill", new Vector3((float)eye.X, (float)world.Plan.GroundHeight(eye.X, eye.Y) + 2.4f, (float)eye.Y), target);
                    Debug.Log($"CityX stair capture: from the spawn up road {uphill.Id} ({(StepStreets.Is(world.Plan, uphill) ? "a step street" : "a road")}), {rise:F1} m up over {uphill.Length:F0} m");
                }
            }

            // The step street nearest the spawn: from below its foot looking up it, and from its top looking down.
            EdgeGeometry stepStreet = null;
            double stepBest = double.MaxValue;
            foreach (EdgeGeometry g in world.Plan.Geometry.Edges)
            {
                if (g == null || !StepStreets.Is(world.Plan, g.Edge) || g.Length - g.TrimA - g.TrimB < 15.0) continue;
                double d = ((g.A + g.B) * 0.5 - spawn).Length;
                if (d < stepBest)
                {
                    stepBest = d;
                    stepStreet = g;
                }
            }
            if (stepStreet != null)
            {
                List<StairTread> steps = StepStreets.Profile(world.Plan, stepStreet, out bool fromB);
                double run = stepStreet.Length - stepStreet.TrimA - stepStreet.TrimB;
                Vec2 dir = fromB ? -stepStreet.Dir : stepStreet.Dir, low = stepStreet.A + stepStreet.Dir * (fromB ? stepStreet.Length - stepStreet.TrimB : stepStreet.TrimA);
                Vector3 On(double s, double across, double over) =>
                    new Vector3((float)(low.X + dir.X * s + dir.Perp.X * across), (float)(CitySurfaceBuilder.TreadTopAt(steps, s) + over), (float)(low.Y + dir.Y * s + dir.Perp.Y * across));
                Vector3 middle = On(run * 0.5, 0.0, 0.0);
                viewer.transform.position = middle;
                streamer.Rebuild();
                streamer.StreamAround(middle, int.MaxValue);
                yield return new WaitForSecondsRealtime(2.0f);
                BuildingShapeCapture.Shoot(camera, directory, "stairs-street-up", On(-6.0, 1.5, 2.0), On(run * 0.6, 0.0, 1.0));
                BuildingShapeCapture.Shoot(camera, directory, "stairs-street-down", On(run + 3.0, -2.0, 2.4), On(run * 0.35, 0.0, 0.0));
                Debug.Log($"CityX stair capture: step street {stepStreet.Edge.Id} ({run:F0} m, {steps[steps.Count - 1].Top - steps[0].Top:F1} m up), {stepBest:F0} m from the spawn");
            }

            // The hills: the steepest street still a road within a kilometre of the spawn, from its foot looking up it (the towers hide the hills
            // from above).
            RoadEdge steepest = null;
            double steepestGrade = 0.0;
            foreach (RoadEdge edge in world.Plan.Data.Edges)
            {
                if (!edge.Alive || edge.Length < 40.0 || ((edge.A.P + edge.B.P) * 0.5 - spawn).Length > 1000.0 || StepStreets.Is(world.Plan, edge)) continue;
                double grade = System.Math.Abs(world.Plan.GroundHeight(edge.B.P.X, edge.B.P.Y) - world.Plan.GroundHeight(edge.A.P.X, edge.A.P.Y)) / edge.Length;
                if (grade > steepestGrade)
                {
                    steepestGrade = grade;
                    steepest = edge;
                }
            }
            if (steepest != null)
            {
                bool aLow = world.Plan.GroundHeight(steepest.A.P.X, steepest.A.P.Y) < world.Plan.GroundHeight(steepest.B.P.X, steepest.B.P.Y);
                Vec2 foot = aLow ? steepest.A.P : steepest.B.P, head = aLow ? steepest.B.P : steepest.A.P;
                Vec2 up = (head - foot).Normalized;
                Vec2 eye = foot - up * 6.0;
                var middle = new Vector3((float)((foot.X + head.X) * 0.5), (float)world.Plan.GroundHeight((foot.X + head.X) * 0.5, (foot.Y + head.Y) * 0.5), (float)((foot.Y + head.Y) * 0.5));
                viewer.transform.position = middle;
                streamer.Rebuild();
                streamer.StreamAround(middle, int.MaxValue);
                yield return new WaitForSecondsRealtime(3.0f);
                BuildingShapeCapture.Shoot(camera, directory, "stairs-hill-street", new Vector3((float)eye.X, (float)world.Plan.GroundHeight(eye.X, eye.Y) + 1.9f, (float)eye.Y),
                    new Vector3((float)head.X, (float)world.Plan.GroundHeight(head.X, head.Y) + 3.0f, (float)head.Y));
                Debug.Log($"CityX stair capture: the steepest street near the spawn, {steepest.Length:F0} m at {steepestGrade:P0}, {((foot + head) * 0.5 - spawn).Length:F0} m away");
            }

            // The raised front door near the spawn whose stair folds into the most lanes (World/EntranceStairs.PlanFolded, 2026-10-16): from the road
            // in front, off to the side its first flight runs to, and from above the road at the door's height.
            BuildingSite folded = BuildingSite.None;
            EntranceStairs.Folded foldedStair = null;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 14))
            {
                if (site.OnRoof || site.Shell || site.Sides >= 3 || !BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).NeedsStairs) continue;
                EntranceStairs.Folded f = EntranceStairs.ForDoor(world.Plan, site, 0.0f, site.BaseHeight, site.Params.Depth * 0.5f, site.Params.Width * 0.5f);
                if (f.Reaches && (foldedStair == null || f.Lanes > foldedStair.Lanes || f.Lanes == foldedStair.Lanes && f.Steps > foldedStair.Steps))
                {
                    folded = site;
                    foldedStair = f;
                }
            }
            if (foldedStair != null && foldedStair.Lanes > 1)
            {
                float front = folded.Params.Depth * 0.5f;
                Vector3 Local(double x, double y, double z)
                {
                    folded.LocalToWorld(x, z, out double wx, out double wz);
                    return new Vector3((float)wx, (float)y, (float)wz);
                }
                Vector3 door = Local(0.0, folded.BaseHeight, front);
                viewer.transform.position = door;
                streamer.Rebuild();
                streamer.StreamAround(door, int.MaxValue);
                yield return new WaitForSecondsRealtime(3.0f);
                float ground = (float)world.Plan.GroundHeight(door.x, door.z);
                float side = foldedStair.Runs[0].Dir;
                float kerb = GarageEntrances.CarriagewayOut(world.Plan, folded, 0.0f);
                if (float.IsNaN(kerb)) kerb = 6.0f;
                Vector3 eye = Local(side * 5.0, 0.0, front + kerb + 4.0);
                eye.y = (float)world.Plan.GroundHeight(eye.x, eye.z) + 2.4f;
                BuildingShapeCapture.Shoot(camera, directory, "stairs-entrance-folded", eye, Local(side * 1.0, (folded.BaseHeight + ground) * 0.5f, front + 2.0));
                Vector3 high = Local(-side * 3.0, 0.0, front + kerb + 6.0);
                high.y = folded.BaseHeight + 6.0f;
                BuildingShapeCapture.Shoot(camera, directory, "stairs-entrance-folded-air", high, Local(side * 1.5, ground, front + 2.0));
                Debug.Log($"CityX stair capture: the folded entrance stair of #{folded.Id} ({folded.Params.Width:F0} m front, the road {kerb:F1} m out), its door " +
                    $"{folded.BaseHeight - ground:F1} m over the paving: {foldedStair.Lanes} lanes, {foldedStair.Steps} steps, {foldedStair.Turns.Count} turns");
            }

            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
