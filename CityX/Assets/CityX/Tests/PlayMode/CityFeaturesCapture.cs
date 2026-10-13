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
    /// Not a test: renders the city's life to Logs/CityShots/city-*.png -- an expressway deck from the air and from a car's height, an exit with
    /// its ramp and sign, the ring road, a parking garage and a surface car park, a mall outside and in its concourse, and a plaza. Explicit;
    /// needs -Graphics. Look at the pictures: green tests do not mean it looks right.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CityFeaturesCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheCitysLife()
        {
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
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }

            var cameraObject = new GameObject("CityCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.farClipPlane = 2500.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            // The streamer follows its viewer every frame and drops what is far from it: far from the spawn the chunks StreamAround builds
            // would be gone by the next frame unless the viewer is this camera.
            streamer.Viewer = cameraObject.transform;

            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);

            // ---- the expressway: the highest span nearest the spawn ----
            EdgeGeometry span = null;
            double spanScore = double.MaxValue;
            foreach (EdgeGeometry g in plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Class != RoadClass.Highway || !g.Elevated || g.Length < 80.0) continue;
                Vec2 mid = (g.A + g.B) * 0.5;
                double rise = System.Math.Min(g.DeckA, g.DeckB) - g.MaxGround;
                if (rise < 6.0) continue;
                double score = (mid - spawn).Length;
                if (score < spanScore) { spanScore = score; span = g; }
            }
            if (span != null)
            {
                Vec2 mid = (span.A + span.B) * 0.5;
                float top = (float)span.DeckTopAt(span.Length * 0.5);
                var centre = new Vector3((float)mid.X, top, (float)mid.Y);
                var along = new Vector3((float)span.Dir.X, 0.0f, (float)span.Dir.Y);
                var across = new Vector3(-along.z, 0.0f, along.x);
                yield return Stream(streamer, centre);
                Shoot(camera, directory, "city-deck-air", centre + across * 70.0f + Vector3.up * 45.0f - along * 30.0f, centre);
                Shoot(camera, directory, "city-deck-drive", centre - along * 30.0f + Vector3.up * 2.4f + across * 3.0f, centre + along * 60.0f + Vector3.up * 1.0f);
                Shoot(camera, directory, "city-deck-under", centre - across * 22.0f + Vector3.up * (-top + (float)plan.GroundHeight(mid.X, mid.Y) + 1.9f) + along * 10.0f, centre - Vector3.up * 3.0f + along * 25.0f);
                Debug.Log($"CityX city capture: deck edge {span.Edge.Id} at {mid.X:F0},{mid.Y:F0}, {top - plan.GroundHeight(mid.X, mid.Y):F1} m up");
            }

            // ---- an exit: the off-ramp nearest the spawn ----
            RampGeometry ramp = null;
            double rampScore = double.MaxValue;
            foreach (RampGeometry r in plan.Geometry.Ramps)
            {
                if (!r.Plan.Leaving) continue;
                double score = (r.Start - spawn).Length;
                if (score < rampScore) { rampScore = score; ramp = r; }
            }
            if (ramp != null)
            {
                var start = new Vector3((float)ramp.Start.X, (float)ramp.TopStart, (float)ramp.Start.Y);
                var foot = new Vector3((float)ramp.Foot.X, (float)ramp.TopFoot, (float)ramp.Foot.Y);
                var midPoint = new Vector3((float)ramp.Mid.X, (float)ramp.TopMid, (float)ramp.Mid.Y);
                var dirA = new Vector3((float)ramp.DirA.X, 0.0f, (float)ramp.DirA.Y);
                Vector3 look = (foot - start).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, look);
                // From the deck, approaching the exit: the gantry sign stands over the deck where the ramp leaves it.
                yield return Stream(streamer, start);
                Shoot(camera, directory, "city-exit-drive", start - dirA * 40.0f + Vector3.up * 2.4f, midPoint + Vector3.up * 1.0f);
                Shoot(camera, directory, "city-exit-sign", start - dirA * 18.0f + Vector3.up * 3.0f, start + dirA * 20.0f + Vector3.up * 5.0f);
                yield return Stream(streamer, midPoint);
                Shoot(camera, directory, "city-exit-air", midPoint + side * 90.0f + Vector3.up * 60.0f - look * 20.0f, midPoint);
                Shoot(camera, directory, "city-exit-street", foot + look * 25.0f + Vector3.up * 1.9f + side * 8.0f, midPoint + Vector3.up * 2.0f);
                Debug.Log($"CityX city capture: exit {ramp.Plan.ExitNode} ramp from {ramp.Start.X:F0},{ramp.Start.Y:F0} ({ramp.TopStart:F1}) to {ramp.Foot.X:F0},{ramp.Foot.Y:F0} ({ramp.TopFoot:F1}), {ramp.Length:F0} m");
            }

            // ---- parking: the garage and the car park nearest the spawn ----
            if (TryNearest(plan, spawn, b => b.IsParkingGarage, out BuildingSite garage))
            {
                yield return ShootBuilding(streamer, camera, directory, "city-garage", garage, 1.0f);
            }
            if (TryNearest(plan, spawn, b => b.IsOpenGround, out BuildingSite open))
            {
                yield return ShootBuilding(streamer, camera, directory, "city-carpark", open, 0.6f);
            }
            // ---- the biggest mall complex: whole from the air, its car park at eye height, its main corridor and an anchor inside ----
            int mallBlock = -1;
            foreach (int bi in plan.Data.MallBlocks)
            {
                if (mallBlock < 0 || plan.MallSites(bi).Length > plan.MallSites(mallBlock).Length) mallBlock = bi;
            }
            BuildingSite mall = default;
            bool hasMall = false;
            if (mallBlock >= 0)
            {
                foreach (BuildingSite b in plan.MallSites(mallBlock))
                {
                    if (b.IsMall && !b.Wing) { mall = b; hasMall = true; break; }
                }
                PlanBlock block = plan.Data.Blocks[mallBlock];
                Vec2 c = PlanMath.Centroid(block.Inner);
                float size = Mathf.Sqrt((float)block.Area);
                var centre = new Vector3((float)c.X, (float)plan.GroundHeight(c.X, c.Y), (float)c.Y);
                yield return Stream(streamer, centre);
                Shoot(camera, directory, "city-mall-complex", centre + new Vector3(size * 0.55f, size * 0.6f + 40.0f, -size * 0.55f), centre);
                Shoot(camera, directory, "city-mall-complex2", centre + new Vector3(-size * 0.5f, size * 0.45f + 30.0f, size * 0.6f), centre);
                Debug.Log($"CityX city capture: mall complex on block {mallBlock}, {plan.MallSites(mallBlock).Length} buildings, {block.MallParking}");
            }
            if (hasMall)
            {
                // The car park at a walker's eye height, looking at the mall: the first open spot out from the main corridor (front, then the
                // sides, then the back) that is on the block and clear of every building of the complex.
                float floor = mall.BaseHeight;
                BuildingSite[] complex = plan.MallSites(mallBlock);
                List<Vec2> lotRing = plan.Data.Blocks[mallBlock].Inner;
                Vector3 lotEye = Vector3.zero;
                bool foundEye = false;
                foreach ((double fx, double fz) in new[] { (0.0, 1.0), (0.6, 0.8), (-0.6, 0.8), (1.0, 0.0), (-1.0, 0.0), (0.0, -1.0) })
                {
                    for (double d = 30.0; d <= 160.0 && !foundEye; d += 10.0)
                    {
                        Vector3 p = TestSites.At(mall, mall.CentreX + fx * (mall.Params.Width * 0.5 + d), floor + 1.9, mall.CentreZ + fz * (mall.Params.Depth * 0.5 + d));
                        var flat = new Vec2(p.x, p.z);
                        if (!PlanMath.PointInPolygon(flat, lotRing)) break;
                        bool clear = true;
                        foreach (BuildingSite b in complex)
                        {
                            b.WorldToLocal(p.x, p.z, out double lx, out double lz);
                            if (System.Math.Abs(lx) < b.Params.Width * 0.5 + 22.0 && System.Math.Abs(lz) < b.Params.Depth * 0.5 + 22.0) clear = false;
                        }
                        if (clear)
                        {
                            lotEye = new Vector3(p.x, (float)plan.GroundHeight(p.x, p.z) + 6.0f, p.z);   // as if from a lamp post, to see along the rows
                            foundEye = true;
                        }
                    }
                    if (foundEye) break;
                }
                Vector3 frontDoor = TestSites.At(mall, mall.CentreX, floor + 4.0, mall.CentreZ);
                if (foundEye) yield return Stream(streamer, lotEye);
                if (foundEye) Shoot(camera, directory, "city-mall-carpark", lotEye, frontDoor);
                Vector3 nearMall = TestSites.At(mall, mall.CentreX, floor, mall.CentreZ);
                yield return Stream(streamer, nearMall);
                // Inside: down the concourse from one end, at a walker's eye height, and from an upper gallery.
                Vector3 eye = TestSites.At(mall, mall.CentreX - mall.Params.Width * 0.28, floor + 2.3, mall.CentreZ);
                Vector3 farEnd = TestSites.At(mall, mall.CentreX + mall.Params.Width * 0.3, floor + 2.0, mall.CentreZ);
                Shoot(camera, directory, "city-mall-concourse", eye, farEnd);
                // On the upper gallery (it runs beside the void, 3.5 m wide from 7 m off the centreline), looking down into the food court.
                Vector3 gallery = TestSites.At(mall, mall.CentreX + mall.Params.Width * 0.1, floor + (float)Amenities.MallLevelHeight + 2.0, mall.CentreZ - 5.2);
                Vector3 courtLook = TestSites.At(mall, mall.CentreX - mall.Params.Width * 0.1, floor + 1.0, mall.CentreZ + 2.0);
                Shoot(camera, directory, "city-mall-gallery", gallery, courtLook);
                Vector3 food = TestSites.At(mall, mall.CentreX + 6.0, floor + 2.2, mall.CentreZ + 5.0);
                Vector3 foodLook = TestSites.At(mall, mall.CentreX - 14.0, floor + 1.2, mall.CentreZ - 4.0);
                Shoot(camera, directory, "city-mall-foodcourt", food, foodLook);
                Debug.Log($"CityX city capture: mall #{mall.Id} {mall.Params.Width:F0}x{mall.Params.Depth:F0}x{mall.Params.Height:F0} at {mall.CentreX:F0},{mall.CentreZ:F0}");

                // An anchor's hall, from a corner across its shelving.
                foreach (BuildingSite anchor in plan.MallSites(mallBlock))
                {
                    if (!anchor.IsMallAnchor) continue;
                    Vector3 hallEye = TestSites.At(anchor, anchor.CentreX - anchor.Params.Width * 0.35, anchor.BaseHeight + 2.4, anchor.CentreZ - anchor.Params.Depth * 0.3);
                    Vector3 hallLook = TestSites.At(anchor, anchor.CentreX + anchor.Params.Width * 0.3, anchor.BaseHeight + 1.2, anchor.CentreZ + anchor.Params.Depth * 0.3);
                    yield return Stream(streamer, hallEye);
                    Shoot(camera, directory, "city-mall-anchor", hallEye, hallLook);
                    Vector3 outside = TestSites.At(anchor, anchor.CentreX, anchor.BaseHeight, anchor.CentreZ);
                    Vector3 front = CityMeshBuilder.WallNormal(anchor, WindowGrid.Wall.North);
                    Shoot(camera, directory, "city-mall-anchor-air", outside + front * 70.0f + Vector3.up * 45.0f, outside + Vector3.up * 6.0f);
                    break;
                }
            }

            // ---- a plaza: the nearest to the spawn ----
            int plazaIndex = -1;
            double plazaScore = double.MaxValue;
            foreach (int index in plan.Data.PlazaBlocks)
            {
                PlanBlock block = plan.Data.Blocks[index];
                if (block.Inner == null) continue;
                double score = (PlanMath.Centroid(block.Inner) - spawn).Length;
                if (score < plazaScore) { plazaScore = score; plazaIndex = index; }
            }
            if (plazaIndex >= 0)
            {
                PlanBlock block = plan.Data.Blocks[plazaIndex];
                Vec2 c = PlanMath.Centroid(block.Inner);
                float ground = (float)plan.GroundHeight(c.X, c.Y);
                var centre = new Vector3((float)c.X, ground, (float)c.Y);
                float size = Mathf.Sqrt((float)block.Area);
                yield return Stream(streamer, centre);
                Shoot(camera, directory, "city-plaza-air", centre + new Vector3(size * 0.7f, size * 0.9f + 20.0f, -size * 0.7f), centre);
                Vec2 edge = block.Inner[0];
                Vec2 inward = (c - edge).Normalized;
                Shoot(camera, directory, "city-plaza-eye", new Vector3((float)(edge.X + inward.X * 3.0), ground + 1.9f, (float)(edge.Y + inward.Y * 3.0)), centre + Vector3.up * 1.0f);
                Debug.Log($"CityX city capture: plaza block {plazaIndex} at {c.X:F0},{c.Y:F0}, {block.Area:F0} m2");
            }

            // ---- the ring road: from high above the centre ----
            double radius = plan.Data.Config.OrbitalRadius;
            if (radius > 0.0)
            {
                var over = new Vector3((float)radius, 0.0f, 0.0f);
                float ground = (float)plan.GroundHeight(radius, 0.0);
                yield return Stream(streamer, new Vector3(over.x, ground, over.z));
                Shoot(camera, directory, "city-ring-air", new Vector3(over.x + 120.0f, ground + 160.0f, over.z - 200.0f), new Vector3(over.x, ground, over.z + 150.0f));
            }

            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Stream(CityStreamer streamer, Vector3 at)
        {
            // The viewer (the capture camera) goes there first, so the streamer's own per-frame pass keeps what is built here.
            if (streamer.Viewer != null)
            {
                streamer.Viewer.position = at;
            }
            streamer.StreamAround(at, int.MaxValue);
            yield return null;
            yield return null;
        }

        private static bool TryNearest(PlanWorld plan, Vec2 from, System.Func<BuildingSite, bool> pick, out BuildingSite best)
        {
            best = default;
            bool found = false;
            double bestScore = double.MaxValue;
            foreach (BuildingSite b in plan.Buildings)
            {
                if (!pick(b)) continue;
                double score = (new Vec2(b.CentreX, b.CentreZ) - from).Length;
                if (score < bestScore) { bestScore = score; best = b; found = true; }
            }
            return found;
        }

        private static IEnumerator ShootBuilding(CityStreamer streamer, Camera camera, string directory, string name, BuildingSite site, float heightShare)
        {
            Vector3 centre = TestSites.At(site, site.CentreX, site.BaseHeight, site.CentreZ);
            yield return Stream(streamer, centre);
            float size = Mathf.Max(site.Params.Width, site.Params.Depth);
            Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Vector3 mid = centre + Vector3.up * site.Params.Height * 0.5f;
            Shoot(camera, directory, name + "-air", mid + (front * 1.0f + side * 0.8f).normalized * size * 1.1f + Vector3.up * (size * 0.55f + site.Params.Height * heightShare), centre);
            Vector3 street = centre + front * (site.Params.Depth * 0.5f + 16.0f) + side * (site.Params.Width * 0.2f) + Vector3.up * 1.9f;
            Shoot(camera, directory, name + "-street", street, centre + Vector3.up * Mathf.Min(site.Params.Height * 0.4f, 8.0f));
            Debug.Log($"CityX city capture: {name} #{site.Id} {site.Params.Width:F0}x{site.Params.Depth:F0}x{site.Params.Height:F0} at {site.CentreX:F0},{site.CentreZ:F0}");
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            BuildingShapeCapture.Shoot(camera, directory, name, position, lookAt);
        }
    }
}
