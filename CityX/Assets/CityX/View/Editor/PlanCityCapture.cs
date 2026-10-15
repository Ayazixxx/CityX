using System.IO;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Renders the plan city (World/Plan) to PNGs without opening the editor UI -- aerial, street level, a building's front, a bridge.
    /// Needs a real graphics device, so it must run WITHOUT -nographics.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod CityX.ViewEditor.PlanCityCapture.Capture
    ///
    /// Shots go to Logs/CityShots/plan-*.png. The rig builds its own sun, sky and camera from the same assets the real scene loads, so
    /// the pictures are of the real game's look.
    /// </summary>
    public static class PlanCityCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        // Must match CityMassingSceneBuilder.WorldSeed.
        private const int WorldSeed = 70485213;

        // The time of day the rig is lit for, and what the shots are called: the same cameras at night give the lit windows, the signs
        // and the streets under lamplight their own picture.
        private static float _timeOfDay = SkyCycle.StartSeconds;
        private static string _prefix = "";
        private static bool _stairs;
        private static bool _paint;

        public static void CapturePaint()
        {
            _timeOfDay = SkyCycle.StartSeconds;
            _prefix = "";
            _paint = true;
            CaptureShots();
        }

        /// <summary>Street-level shots at the mouth of a few streets, looking at where the zebra crossings are.</summary>
        private static void ShootPaint(CityStreamer streamer, Camera camera, string directory, CityWorld world, Vector3 spawn)
        {
            int shots = 0;
            foreach (RoadEdge edge in world.Plan.Data.Edges)
            {
                EdgeGeometry g = world.Plan.Geometry.Edges[edge.Id];
                if (g == null || edge.Class != RoadClass.Street || g.TrimA < 1.0 || g.Length < 40.0)
                {
                    continue;
                }
                double ex = g.A.X + g.Dir.X * (g.TrimA + 9.0);
                double ez = g.A.Y + g.Dir.Y * (g.TrimA + 9.0);
                if (new Vector2((float)(ex - spawn.x), (float)(ez - spawn.z)).sqrMagnitude > 500.0f * 500.0f)
                {
                    continue;
                }
                float y = (float)world.Terrain.ContinuousHeightAt(ex, ez) + 1.9f;
                var look = new Vector3((float)-g.Dir.X, -0.12f, (float)-g.Dir.Y);
                Shoot(streamer, camera, directory, "paint-" + shots, new Vector3((float)ex, y, (float)ez), Quaternion.LookRotation(look, Vector3.up).eulerAngles, 3);
                if (++shots >= 6)
                {
                    break;
                }
            }
        }

        public static void CaptureStairs()
        {
            _timeOfDay = SkyCycle.StartSeconds;
            _prefix = "";
            _stairs = true;
            CaptureShots();
        }

        /// <summary>One interior shot per kind of stair, standing in the cell you step on from and looking at the stair.</summary>
        private static void ShootStairs(CityStreamer streamer, Camera camera, string directory, CityWorld world, Vector3 spawn)
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (BuildingSite site in TestSitesNear(world, spawn, 700.0f))
            {
                if (site.Params.Width < 14.0f || site.Params.Depth < 14.0f || BuildingInterior.FloorCountFor(site.Params.Height) < 3)
                {
                    continue;
                }
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                for (int f = 0; f < 2; f++)
                {
                    StairPlan plan = layout.Floors[f];
                    if (plan == null)
                    {
                        continue;
                    }
                    string key = (layout.Core ? "core" : plan.Kind.ToString().ToLower() + plan.Flights);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    Vector2 entry = plan.Entry.center;
                    Vector2 middle = plan.Bounds.center;
                    Vector2 away = (entry - middle).normalized;
                    Vector2 eyeLocal = entry + away * 1.0f;
                    site.LocalToWorld(eyeLocal.x, eyeLocal.y, out double ex, out double ez);
                    site.LocalToWorld(middle.x, middle.y, out double mx, out double mz);
                    float floorY = site.BaseHeight + f * BuildingInterior.StoreyHeight(site.Params.Height);
                    var eye = new Vector3((float)ex, floorY + 2.3f, (float)ez);
                    var target = new Vector3((float)mx, floorY + plan.Rise * 0.4f, (float)mz);
                    Debug.Log($"CityX: stair shot {key}: building {site.Id} floor {f}, {plan.Kind} x{plan.Flights}, {plan.AngleDegrees:0.0} deg, rise {plan.Rise:0.0}");
                    Shoot(streamer, camera, directory, "stair-" + key + "-f" + f, eye, Quaternion.LookRotation(target - eye, Vector3.up).eulerAngles, 3);
                    break;
                }
                if (seen.Count >= 7)
                {
                    break;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<BuildingSite> TestSitesNear(CityWorld world, Vector3 around, float radius)
        {
            for (int id = 0; id < world.BuildingCount; id++)
            {
                BuildingSite s = world.Building(id);
                if (s.Params.HasBuilding && (new Vector3((float)s.CentreX, 0, (float)s.CentreZ) - new Vector3(around.x, 0, around.z)).sqrMagnitude < radius * radius)
                {
                    yield return s;
                }
            }
        }

        [MenuItem("CityX/Capture Plan City")]
        public static void Capture()
        {
            _timeOfDay = SkyCycle.StartSeconds;
            _prefix = CityScale.Active.Name + "-";
            CaptureShots();
        }

        [MenuItem("CityX/Capture Plan City At Night")]
        public static void CaptureNight()
        {
            _timeOfDay = SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.45f;
            _prefix = "night-";
            CaptureShots();
        }

        private static void CaptureShots()
        {
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);

            var root = new GameObject("PlanCaptureRig");
            try
            {
                Material ground = Load("CityGround");
                Material pavement = Load("CityPavement");
                Material structure = Load("CityConcrete");
                if (ground == null || pavement == null || structure == null)
                {
                    Debug.LogError("CityX: materials missing -- run CityX/Rebuild Massing Scene first.");
                    return;
                }

                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                AttachSunFlare(sun);

                var moonObject = new GameObject("Moon");
                moonObject.transform.SetParent(root.transform);
                Light moon = moonObject.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.shadows = LightShadows.Soft;

                var cycleObject = new GameObject("Cycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.Sun = sun;
                cycle.Moon = moon;
                cycle.Running = false;
                cycle.LitWindows = Load("CityGlassLit");
                cycle.TimeOfDay = _timeOfDay;

                var streamerObject = new GameObject("Streamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                Dress(streamer);

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 4500.0f * Mathf.Min((float)CityScale.Active.Footprint, 4.0f);
                camera.nearClipPlane = 0.3f;
                if (!AttachEnvironment(root, camera, cycle))
                {
                    return;
                }
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));

                var world = new CityWorld(WorldSeed);
                world.Plan.TryFindSpawn(out double sx, out double sz);
                float sy = (float)world.Terrain.ContinuousHeightAt(sx, sz);
                var spawn = new Vector3((float)sx, sy, (float)sz);
                Debug.Log($"CityX: plan city seed {WorldSeed}: {world.BuildingCount} buildings, spawn at {spawn}");

                if (_stairs)
                {
                    ShootStairs(streamer, camera, directory, world, spawn);
                    return;
                }
                if (_paint)
                {
                    ShootPaint(streamer, camera, directory, world, spawn);
                    return;
                }

                // Aerial, from a little way off, looking over the downtown.
                float sc = (float)CityScale.Active.Footprint;
                Vector3 aerialEye = spawn + new Vector3(-260.0f, 300.0f, -380.0f) * (sc * 0.6f);
                Shoot(streamer, camera, directory, "plan-aerial",
                    aerialEye, Quaternion.LookRotation(spawn - aerialEye, Vector3.up).eulerAngles, 26);

                // From a long way back at roof height, looking at the downtown: the skyline.
                Vector3 skylineEye = spawn + new Vector3(-420.0f, 0.0f, -700.0f) * sc + Vector3.up * (60.0f * sc);
                Shoot(streamer, camera, directory, "plan-skyline", skylineEye, Quaternion.LookRotation(spawn + Vector3.up * 140.0f * sc - skylineEye, Vector3.up).eulerAngles, 30);

                // Straight down on the spawn junction, to compare with the 2D plan.
                Vector3 topEye = spawn + Vector3.up * 230.0f * sc;
                Shoot(streamer, camera, directory, "plan-topdown", topEye, new Vector3(90.0f, 0.0f, 0.0f), 14);

                // Street level, standing at the spawn junction looking along the first road that leaves it.
                Vector3 along = Vector3.forward;
                RoadNode node = NearestNode(world.Plan, sx, sz);
                if (node != null && node.Edges.Count > 0)
                {
                    Vec2 d = (node.Edges[0].Other(node).P - node.P).Normalized;
                    along = new Vector3((float)d.X, 0.0f, (float)d.Y);
                }
                Vector3 across = new Vector3(-along.z, 0.0f, along.x);
                Vector3 eye = spawn + Vector3.up * 1.7f - along * 10.0f + across * 7.5f;
                Shoot(streamer, camera, directory, "plan-street",
                    eye, Quaternion.LookRotation(along, Vector3.up).eulerAngles, 14);

                // Up the road from a little higher.
                Shoot(streamer, camera, directory, "plan-street-high",
                    spawn + Vector3.up * 14.0f - along * 30.0f, Quaternion.LookRotation(along + Vector3.down * 0.25f, Vector3.up).eulerAngles, 14);

                // The front of a building near the spawn: the tallest one within 150 m.
                BuildingSite pick = BuildingSite.None;
                float bestTop = 0.0f;
                for (int dx = -6; dx <= 6; dx++)
                {
                    for (int dz = -6; dz <= 6; dz++)
                    {
                        var owned = world.BuildingsOwnedBy(PlanWorld.ChunkOf(sx) + dx, PlanWorld.ChunkOf(sz) + dz);
                        for (int i = 0; i < owned.Count; i++)
                        {
                            BuildingSite s = world.Building(owned[i]);
                            if (s.Params.Height > bestTop && s.Params.Height < 50.0f && s.Params.Height > 18.0f && s.Params.Width > 12.0f)
                            {
                                bestTop = s.Params.Height;
                                pick = s;
                            }
                        }
                    }
                }
                if (pick.Exists)
                {
                    pick.LocalToWorld(0.0, pick.Params.Depth * 0.5 + 12.0, out double fx, out double fz);
                    pick.LocalToWorld(0.0, pick.Params.Depth * 0.5, out double tx, out double tz);
                    var camPos = new Vector3((float)fx, pick.BaseHeight + 1.7f, (float)fz);
                    var target = new Vector3((float)tx, pick.BaseHeight + 4.0f, (float)tz);
                    // StreamAround only builds chunks that are not loaded yet, and the earlier shots left these loaded as the far
                    // version (no doors, no interior). The real game re-details them as you walk up; the capture starts afresh.
                    streamer.Rebuild();
                    Shoot(streamer, camera, directory, "plan-building-front",
                        camPos, Quaternion.LookRotation(target - camPos, Vector3.up).eulerAngles, 12);

                    // The door itself, from a few metres out at eye height.
                    pick.LocalToWorld(0.0, pick.Params.Depth * 0.5 + 4.5, out double ex, out double ez);
                    var doorEye = new Vector3((float)ex, pick.BaseHeight + 1.6f, (float)ez);
                    var doorAt = new Vector3((float)tx, pick.BaseHeight + 1.3f, (float)tz);
                    Shoot(streamer, camera, directory, "plan-door",
                        doorEye, Quaternion.LookRotation(doorAt - doorEye, Vector3.up).eulerAngles, 8);

                    // The door is a real object hung in the opening the wall builder cut: confirm it is where the plan says.
                    Vector3 hinge = BuildingDoor.PanelFor(pick).Hinge;
                    float nearest = float.MaxValue;
                    foreach (BuildingDoor door in Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None))
                    {
                        nearest = Mathf.Min(nearest, Vector3.Distance(door.transform.position, hinge));
                    }
                    Debug.Log($"CityX: building {pick.Id}'s door hinge is {nearest:F2} m from the nearest hung door");

                    // The same building from the side and above, to see its shape against its lot.
                    pick.LocalToWorld(40.0, -10.0, out double ox, out double oz);
                    var camPos2 = new Vector3((float)ox, pick.BaseHeight + pick.Params.Height * 0.8f + 12.0f, (float)oz);
                    var target2 = new Vector3((float)pick.CentreX, pick.BaseHeight + pick.Params.Height * 0.4f, (float)pick.CentreZ);
                    Shoot(streamer, camera, directory, "plan-building-side",
                        camPos2, Quaternion.LookRotation(target2 - camPos2, Vector3.up).eulerAngles, 12);
                }

                // A shopfront: the nearest building whose ground floor is a shop, from across the street.
                BuildingSite shop = BuildingSite.None;
                double bestShop = double.MinValue;
                Vector3 towardSun = -sun.transform.forward;
                for (int dx = -10; dx <= 10; dx++)
                {
                    for (int dz = -10; dz <= 10; dz++)
                    {
                        var owned = world.BuildingsOwnedBy(PlanWorld.ChunkOf(sx) + dx, PlanWorld.ChunkOf(sz) + dz);
                        for (int i = 0; i < owned.Count; i++)
                        {
                            BuildingSite s = world.Building(owned[i]);
                            if (s.Params.Width <= 14.0f || s.Params.Height <= 12.0f || s.Params.Height > 70.0f)
                            {
                                continue;
                            }
                            WindowPlan shopPlan = WindowPlan.For(world, s);
                            if (!shopPlan.Storefront || shopPlan.Style == FacadeStyle.Curtain)
                            {
                                continue;
                            }
                            // The shop front that is best lit by the sun: a shopfront in shadow tells nothing about itself.
                            double score = Vector3.Dot(CityMeshBuilder.WallNormal(s, WindowGrid.Wall.North), towardSun) * 100.0
                                - System.Math.Sqrt((s.CentreX - sx) * (s.CentreX - sx) + (s.CentreZ - sz) * (s.CentreZ - sz)) * 0.05;
                            if (score > bestShop)
                            {
                                bestShop = score;
                                shop = s;
                            }
                        }
                    }
                }
                if (shop.Exists)
                {
                    shop.LocalToWorld(0.0, shop.Params.Depth * 0.5 + 9.0, out double sfx, out double sfz);
                    shop.LocalToWorld(0.0, shop.Params.Depth * 0.5, out double stx, out double stz);
                    var shopEye = new Vector3((float)sfx, shop.BaseHeight + 1.7f, (float)sfz);
                    var shopAt = new Vector3((float)stx, shop.BaseHeight + 3.0f, (float)stz);
                    streamer.Rebuild();
                    Shoot(streamer, camera, directory, "plan-shop",
                        shopEye, Quaternion.LookRotation(shopAt - shopEye, Vector3.up).eulerAngles, 8);

                    // The same street from the pavement, looking along the fronts: a stretch of street, not one facade.
                    shop.LocalToWorld(-22.0, shop.Params.Depth * 0.5 + 7.0, out double bx, out double bz);
                    shop.LocalToWorld(26.0, shop.Params.Depth * 0.5 + 0.0, out double btx, out double btz);
                    var blockEye = new Vector3((float)bx, shop.BaseHeight + 1.7f, (float)bz);
                    var blockAt = new Vector3((float)btx, shop.BaseHeight + 7.0f, (float)btz);
                    Shoot(streamer, camera, directory, "plan-block",
                        blockEye, Quaternion.LookRotation(blockAt - blockEye, Vector3.up).eulerAngles, 8);
                }

                // Down the longest boulevard within reach of the spawn, standing in the road: the long, bending main street.
                EdgeGeometry highway = null;
                double bestLength = 0.0;
                foreach (EdgeGeometry g in world.Plan.Geometry.Edges)
                {
                    if (g == null || g.Edge.Bridge || g.Edge.Class != RoadClass.Boulevard) continue;
                    double mx2 = (g.A.X + g.B.X) * 0.5 - sx, mz2 = (g.A.Y + g.B.Y) * 0.5 - sz;
                    if (mx2 * mx2 + mz2 * mz2 > 600.0 * 600.0) continue;
                    if (g.Length > bestLength) { bestLength = g.Length; highway = g; }
                }
                if (highway != null)
                {
                    double hx = highway.A.X + highway.Dir.X * highway.TrimA;
                    double hz = highway.A.Y + highway.Dir.Y * highway.TrimA;
                    float hy = (float)world.Terrain.ContinuousHeightAt(hx, hz);
                    var hDir = new Vector3((float)highway.Dir.X, 0.0f, (float)highway.Dir.Y);
                    var hEye = new Vector3((float)hx, hy + 2.2f, (float)hz);
                    Shoot(streamer, camera, directory, "plan-boulevard",
                        hEye, Quaternion.LookRotation(hDir + Vector3.down * 0.02f, Vector3.up).eulerAngles, 20);
                }

                // A bridge over a trench.
                EdgeGeometry bridge = null;
                double bestDistance = double.MaxValue;
                foreach (EdgeGeometry g in world.Plan.Geometry.Edges)
                {
                    if (g == null || !g.Edge.Bridge) continue;
                    double mx = (g.A.X + g.B.X) * 0.5 - sx, mz = (g.A.Y + g.B.Y) * 0.5 - sz;
                    double d2 = mx * mx + mz * mz;
                    if (d2 < bestDistance) { bestDistance = d2; bridge = g; }
                }
                if (bridge != null)
                {
                    double mx = (bridge.A.X + bridge.B.X) * 0.5, mz = (bridge.A.Y + bridge.B.Y) * 0.5;
                    float by = (float)world.Terrain.ContinuousHeightAt(mx, mz);
                    var centre = new Vector3((float)mx, by + 4.0f, (float)mz);
                    var side = new Vector3((float)bridge.Dir.Perp.X, 0.0f, (float)bridge.Dir.Perp.Y);
                    var camPos = centre + side * 55.0f + Vector3.up * 22.0f;
                    Shoot(streamer, camera, directory, "plan-bridge",
                        camPos, Quaternion.LookRotation(centre - camPos, Vector3.up).eulerAngles, 10);
                }

                Debug.Log($"CityX: plan city shots written to {directory}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// How much the plan city costs to make: the plan itself, then the chunks around the spawn built in full and as the far
        /// version, with their triangle counts. Prints to the log.
        ///
        ///   Unity.exe -batchmode -nographics -projectPath &lt;proj&gt; -executeMethod CityX.ViewEditor.PlanCityCapture.Bench -quit
        /// </summary>
        public static void Bench()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var world = new CityWorld(WorldSeed);
            long planMs = clock.ElapsedMilliseconds;

            world.Plan.TryFindSpawn(out double sx, out double sz);
            int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);

            foreach (bool far in new[] { false, true })
            {
                double total = 0.0, worst = 0.0;
                long triangles = 0;
                int chunks = 0, withBuilding = 0;
                double worstBuilding = 0.0;
                for (int dy = -10; dy <= 10; dy++)
                {
                    for (int dx = -10; dx <= 10; dx++)
                    {
                        int cx = cx0 + dx, cy = cy0 + dy;
                        var buffers = new MeshBuffer[5];
                        for (int i = 0; i < 5; i++) buffers[i] = new MeshBuffer();
                        var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        CityMeshBuilder.BuildChunk(world, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[4], buffers[3],
                            new MeshBuffer(), null, new System.Collections.Generic.List<CityMeshBuilder.CrackedOpening>(),
                            new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), far: far);
                        timer.Stop();
                        double ms = timer.Elapsed.TotalMilliseconds;
                        total += ms;
                        if (ms > worst) worst = ms;
                        chunks++;
                        bool has = world.BuildingsOwnedBy(cx, cy).Count > 0;
                        if (has) { withBuilding++; if (ms > worstBuilding) worstBuilding = ms; }
                        foreach (MeshBuffer b in buffers) triangles += b.Triangles.Count / 3;
                    }
                }
                Debug.Log($"CityX BENCH {(far ? "far " : "full")}: {chunks} chunks ({withBuilding} with buildings): mean {total / chunks:0.00} ms, worst {worst:0.0} ms " +
                          $"(worst with a building {worstBuilding:0.0} ms); {triangles / chunks} triangles per chunk on average");
            }
            Debug.Log($"CityX BENCH plan: {planMs} ms for {world.BuildingCount} buildings, {world.Plan.Data.Edges.Count} roads");
        }

        private static RoadNode NearestNode(PlanWorld plan, double x, double z)
        {
            RoadNode best = null;
            double bestD = double.MaxValue;
            foreach (RoadNode n in plan.Data.Nodes)
            {
                double d = (n.P.X - x) * (n.P.X - x) + (n.P.Y - z) * (n.P.Y - z);
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        // ---- rig helpers: the same assets the real scene loads ----

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"Assets/CityX/View/Materials/{name}.mat");
        }

        private static void Dress(CityStreamer streamer)
        {
            streamer.DarkGlassMaterial = Load("CityGlassDark");
            streamer.LitGlassMaterial = Load("CityGlassLit");
            streamer.CrackedGlassMaterial = Load("CityGlassCracked");
            streamer.RoadLineMaterial = Load("RoadLine");
            streamer.CrosswalkMaterial = Load("Crosswalk");
            streamer.DoorMaterial = Load("BuildingDoor");
            streamer.DoorHandleMaterial = Load("BuildingDoorHandle");
            streamer.GuardRailMaterial = Load("CityGuardRail");
        }

        private static void AttachSunFlare(Light sun)
        {
            LensFlareComponentSRP flare = sun.gameObject.AddComponent<LensFlareComponentSRP>();
            flare.lensFlareData = CityMassingSceneBuilder.EnsureSunLensFlareData();
            flare.intensity = 1.4f;
            flare.useOcclusion = true;
            flare.occlusionRadius = 0.2f;
            flare.sampleCount = 16;
        }

        private static bool AttachEnvironment(GameObject root, Camera camera, DayNightCycle cycle)
        {
            Material skyDome = Load("CityXSky");
            var grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/CityX/View/Materials/CityGrade.asset");
            if (skyDome == null || grade == null)
            {
                Debug.LogError("CityX: sky or grade missing -- run CityX/Rebuild Massing Scene first.");
                return false;
            }
            RenderSettings.skybox = skyDome;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var gradeObject = new GameObject("Grade");
            gradeObject.transform.SetParent(root.transform);
            var volume = gradeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = grade;

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            cycle.Sky = camera;
            cycle.SkyDome = skyDome;
            return true;
        }

        private static void Shoot(CityStreamer streamer, Camera camera, string directory, string name,
            Vector3 position, Vector3 euler, int radiusChunks)
        {
            streamer.RadiusChunks = radiusChunks;
            // int.MaxValue: build the whole visible city in this one call.
            streamer.StreamAround(position, int.MaxValue);

            camera.transform.position = position;
            camera.transform.rotation = Quaternion.Euler(euler);

            UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
            {
                data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderPostProcessing = true;
            camera.allowMSAA = true;

            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, _prefix + name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
            Debug.Log($"CityX: {name} -- {streamer.LoadedChunkCount} chunks loaded");
        }
    }
}
