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
    /// Not a test: one open block of each role (World/OpenSpace, docs/OPEN_SPACE.md), from over its street by day and at dusk and from a person's eye
    /// at its kerb, to Logs/CityShots/open-&lt;role&gt;-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class OpenSpaceCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderOneBlockOfEachRole()
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
            PlanWorld plan = world.Plan;
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
            }
            var skyline = Object.FindFirstObjectByType<SkylineStreamer>();
            if (skyline != null)
            {
                skyline.enabled = false;   // the far boxes are not the subject, and the city is only streamed round each block
            }

            // The nearest block of each role to the spawn.
            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            var chosen = new Dictionary<OpenSpaceRole, int>();
            var nearest = new Dictionary<OpenSpaceRole, double>();
            foreach (int index in plan.Data.PlazaBlocks)
            {
                OpenSpaceLayout layout = OpenSpace.LayoutFor(plan, index);
                if (layout == null) continue;
                double d = (PlanMath.Centroid(plan.Data.Blocks[index].Inner) - spawn).Length;
                if (!nearest.TryGetValue(layout.Role, out double best) || d < best)
                {
                    nearest[layout.Role] = d;
                    chosen[layout.Role] = index;
                }
            }

            var cameraObject = new GameObject("OpenSpaceCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var viewer = new GameObject("OpenSpaceViewer");
            streamer.Viewer = viewer.transform;
            streamer.RadiusChunks = 4;
            streamer.Rebuild();

            foreach (KeyValuePair<OpenSpaceRole, int> pair in chosen)
            {
                PlanBlock block = plan.Data.Blocks[pair.Value];
                OpenSpaceLayout layout = OpenSpace.LayoutFor(plan, pair.Value);
                List<Vec2> ring = block.Inner;
                Vec2 c = PlanMath.Centroid(ring);
                int n = ring.Count;
                Vec2 a = ring[layout.Front], b = ring[(layout.Front + 1) % n];
                Vec2 along = (b - a).Normalized;
                Vec2 inward = along.Perp;
                Vec2 frontMid = (a + b) * 0.5;
                float ground = (float)(plan.GroundHeight(c.X, c.Y) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight);
                var centre = new Vector3((float)c.X, ground, (float)c.Y);
                viewer.transform.position = centre;
                streamer.StreamAround(centre, int.MaxValue);
                yield return null;
                yield return null;

                // From high over the block itself, tilted toward its front (the towers round a downtown block hide it from anywhere lower).
                double extent = System.Math.Sqrt(System.Math.Abs(PlanMath.SignedArea(ring)));
                Vec2 eyeFrom = c - inward * (0.3 * extent);
                var high = new Vector3((float)eyeFrom.X, ground + (float)(0.85 * extent) + 20.0f, (float)eyeFrom.Y);
                Vec2 kerbEye = frontMid + inward * 1.2 + along * 2.0;
                var eye = new Vector3((float)kerbEye.X, ground + 2.3f, (float)kerbEye.Y);
                Vec2 across = c + inward * (0.2 * extent);
                var lookAcross = new Vector3((float)across.X, ground + 1.2f, (float)across.Y);
                string role = pair.Key.ToString().ToLowerInvariant();
                foreach ((string when, float time) in new[] { ("day", SkyCycle.DaySeconds * 0.42f), ("dusk", SkyCycle.DaySeconds * 0.95f) })
                {
                    if (cycle != null)
                    {
                        cycle.TimeOfDay = time;
                        cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                        DynamicGI.UpdateEnvironment();
                    }
                    yield return null;
                    BuildingShapeCapture.Shoot(camera, directory, $"open-{role}-{when}", high, centre);
                    if (when == "day")
                    {
                        BuildingShapeCapture.Shoot(camera, directory, $"open-{role}-kerb", eye, lookAcross);
                    }
                }
                Debug.Log($"CityX open space capture: {pair.Key} block {pair.Value}, {layout.Pieces.Count} pieces, {layout.Lawns.Count} lawns, powered {layout.Powered}, {extent:F0} m across, {nearest[pair.Key]:F0} m from the spawn");
            }
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
