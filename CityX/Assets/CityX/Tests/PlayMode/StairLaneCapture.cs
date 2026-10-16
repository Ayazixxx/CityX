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
    /// Not a test: the stairways through the steep blocks, the stepped sidewalks and the hills (2026-10-16, owner: "make more staircases
    /// throughout the city overall ... san francisco has a lot of randoms staircases that go up hills ... maybe make the terrain more hilly
    /// overall"). The three stairways nearest the spawn from the bottom looking up and from the top looking down, the nearest stepped sidewalk
    /// from across its street, and the hills from above the spawn, to Logs/CityShots/stairs-*.png. Explicit; needs -Graphics.
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

            // The stepped sidewalk nearest the spawn, from across its street.
            (int block, int edge) pick = (-1, -1);
            double best = double.MaxValue;
            List<PlanBlock> blocks = world.Plan.Data.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                if (blocks[b].Inner == null || blocks[b].Outer == null) continue;
                for (int e = 0; e < blocks[b].Inner.Count; e++)
                {
                    if (!SteppedSidewalks.Is(world.Plan, b, e)) continue;
                    Vec2 a = blocks[b].Inner[e], c = blocks[b].Inner[(e + 1) % blocks[b].Inner.Count];
                    if ((c - a).Length < 20.0) continue;
                    double d = ((a + c) * 0.5 - spawn).Length;
                    if (d < best)
                    {
                        best = d;
                        pick = (b, e);
                    }
                }
            }
            if (pick.block >= 0)
            {
                PlanBlock block = blocks[pick.block];
                Vec2 i0 = block.Inner[pick.edge], i1 = block.Inner[(pick.edge + 1) % block.Inner.Count];
                Vec2 o0 = block.Outer[pick.edge];
                Vec2 mid = (i0 + i1) * 0.5, toRoad = (i0 - o0).Normalized;
                double y = world.Plan.GroundHeight(mid.X, mid.Y);
                var target = new Vector3((float)mid.X, (float)y + 0.6f, (float)mid.Y);
                viewer.transform.position = target;
                streamer.Rebuild();
                streamer.StreamAround(target, int.MaxValue);
                yield return new WaitForSecondsRealtime(2.0f);
                Vec2 eye = mid + toRoad * 9.0 + (i1 - i0).Normalized * -6.0;
                BuildingShapeCapture.Shoot(camera, directory, "stairs-sidewalk", new Vector3((float)eye.X, (float)world.Plan.GroundHeight(eye.X, eye.Y) + 1.9f, (float)eye.Y), target);
                Vec2 along = mid + (i1 - i0).Normalized * ((i1 - i0).Length * 0.5 + 3.0) + toRoad * 1.5;
                BuildingShapeCapture.Shoot(camera, directory, "stairs-sidewalk-along", new Vector3((float)along.X, (float)world.Plan.GroundHeight(along.X, along.Y) + 2.0f, (float)along.Y), target);
                Debug.Log($"CityX stair capture: stepped sidewalk block {pick.block} edge {pick.edge}, {(i1 - i0).Length:F0} m, {best:F0} m from the spawn");
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

            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
