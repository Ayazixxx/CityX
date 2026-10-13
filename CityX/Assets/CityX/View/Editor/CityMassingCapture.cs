#if CITYX_LEGACY_GRID_DIAGNOSTICS
using System.Collections.Generic;
using System.IO;
using CityX.View;
using CityX.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Renders the massing to PNGs without opening the editor UI.
    ///
    /// WHY BOTHER. Every other check in this project answers "are the numbers
    /// consistent". None of them can answer "does a 200m tower on a 24m street
    /// grid feel right", and that is the only question this phase exists to
    /// settle. Rendering it offline means the answer arrives in seconds rather
    /// than requiring somebody to open Unity, walk around and report back.
    ///
    /// Needs a real graphics device, so it must run WITHOUT -nographics.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.ViewEditor.CityMassingCapture.Capture
    /// </summary>
    public static class CityMassingCapture
    {
        private const int Width = 1280;
        private const int Height = 720;
        // Must match CityMassingSceneBuilder.WorldSeed exactly -- otherwise
        // these screenshots stop being screenshots OF the real game.
        // EXPLICIT ASK: "generate a new world every time you make a change
        // to the city generation" -- change this alongside that constant
        // (and TitleScreenBuilder's own copy), never alone.
        private const int WorldSeed = 38461927;

        /// <summary>Matches PC_RPAsset's own m_MSAA -- see Shoot's own comment.</summary>
        private const int PC_MsaaSamples = 4;

        [MenuItem("CityX/Capture Massing Screenshots")]
        public static void Capture()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("CaptureRig");
            try
            {
                Material ground = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/CityX/View/Materials/CityGround.mat");
                Material pavement = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/CityX/View/Materials/CityPavement.mat");
                Material structure = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/CityX/View/Materials/CityConcrete.mat");
                if (ground == null || pavement == null || structure == null)
                {
                    Debug.LogError("CityX: materials missing -- run CityX/Rebuild Massing Scene first.");
                    return;
                }

                // THE REAL CYCLE, not a hand-built copy of it.
                //
                // This used to set a sun intensity and three ambient colours
                // itself, and the moment the scene's own lighting changed the
                // photographs stopped being photographs OF the scene -- which
                // defeats the whole point of a camera whose job is catching
                // what a test cannot. Same failure the materials had: the rig
                // has to be told about anything new or it silently shows
                // something else. Driving it from DayNightCycle means there is
                // nothing left to keep in step.
                var sunObject = new GameObject("CaptureSun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                AttachSunFlare(sun);

                var moonObject = new GameObject("CaptureMoon");
                moonObject.transform.SetParent(root.transform);
                Light moon = moonObject.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.shadows = LightShadows.Soft;

                var cycleObject = new GameObject("CaptureCycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.Sun = sun;
                cycle.Moon = moon;
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.StartSeconds;

                var streamerObject = new GameObject("CaptureStreamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                Dress(streamer);
                streamer.Rebuild();

                var cameraObject = new GameObject("CaptureCamera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 3000.0f;
                camera.nearClipPlane = 0.2f;
                if (!AttachEnvironment(root, camera, cycle))
                {
                    return;
                }
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));

                TerrainField terrain = new TerrainField(WorldSeed);
                float streetLevel = terrain.PadHeightFor(0, 0);
                float chunk = CityLayout.ChunkSize;

                // Chunk column x=0 is a street on every row, so standing on its
                // centreline and facing +Z looks straight down a road. The
                // first attempt pointed at a corner block instead and produced
                // a photograph of one wall.
                Shoot(streamer, camera, outputDirectory, "street-level",
                    new Vector3(chunk * 0.5f, streetLevel + 1.7f, chunk * 0.5f),
                    new Vector3(0.0f, 0.0f, 0.0f), 16);

                // Same road, head tipped back. This is the reference framing --
                // sky reduced to a slot, walkways crossing overhead.
                Shoot(streamer, camera, outputDirectory, "looking-up",
                    new Vector3(chunk * 0.5f, streetLevel + 1.7f, chunk * 4.5f),
                    new Vector3(-58.0f, 0.0f, 0.0f), 16);

                // Rooftop height, to judge the skyline and the walkway lattice.
                Shoot(streamer, camera, outputDirectory, "rooftops",
                    new Vector3(chunk * 6.0f, streetLevel + 95.0f, chunk * 2.0f),
                    new Vector3(6.0f, 42.0f, 0.0f), 20);

                // Far and high, for overall silhouette.
                Shoot(streamer, camera, outputDirectory, "skyline",
                    new Vector3(-chunk * 22.0f, streetLevel + 210.0f, -chunk * 22.0f),
                    new Vector3(11.0f, 45.0f, 0.0f), 26);

                // Terracing is the Chongqing signature and buildings hide most
                // of it, so this hunts for the biggest step in the area and
                // frames that rather than hoping one lands in shot.
                ShootTerraceEdge(streamer, camera, outputDirectory, terrain);
                ShootSlopedStreet(streamer, camera, outputDirectory, terrain);
                ShootInterior(streamer, camera, outputDirectory);
                ShootFacade(streamer, camera, outputDirectory);
                ShootEntrance(streamer, camera, outputDirectory);
                ShootClosedJunction(streamer, camera, outputDirectory, terrain);

                Debug.Log($"CityX: massing shots written to {outputDirectory}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The street with bodies standing in it.
        ///
        /// THE ONLY WAY TO ANSWER THE SCALE QUESTION. Every number in this
        /// world -- a 24m street, a 3.2m floor, a 2.2m person -- is defensible
        /// on its own and says nothing about whether the city feels right to
        /// stand in. A person against a facade settles it in one look.
        /// </summary>
        [MenuItem("CityX/Capture Scale Screenshots")]
        public static void CaptureScale()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            var player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            var civilian = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Civilian.prefab");
            Material ground = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityGround.mat");
            Material pavement = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityPavement.mat");
            Material structure = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityConcrete.mat");

            if (player == null || civilian == null || ground == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Massing Scene first.");
                return;
            }

            var root = new GameObject("ScaleRig");
            bool previousFog = RenderSettings.fog;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                AttachSunFlare(sun);

                var streamerObject = new GameObject("Streamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                Dress(streamer);
                streamer.Rebuild();

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 3000.0f;
                camera.nearClipPlane = 0.2f;

                var cycleObject = new GameObject("Cycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                // The windows are glass now, so this is what has to be told the
                // hour -- without it every pane in the shot renders unlit.
                cycle.LitWindows = GlassMaterial(true);
                cycle.Sun = sun;
                cycle.Running = false;
                if (!AttachEnvironment(root, camera, cycle))
                {
                    return;
                }
                cycle.Apply(SkyCycle.At(SkyCycle.DaySeconds * 0.32f));

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 140.0f;
                RenderSettings.fogEndDistance = 330.0f;

                TerrainField terrain = new TerrainField(WorldSeed);
                float streetLevel = terrain.PadHeightFor(0, 0);
                float chunk = CityLayout.ChunkSize;

                // THE CAMERA IS PLACED FIRST AND THE BODIES ARE PUT RELATIVE TO
                // IT. The first version of this shot stood everyone 20 to 35
                // metres down the street, and a person at 30m is small because
                // perspective says so -- which tells you nothing about scale.
                // The question is what a person looks like standing NEXT to the
                // building, so somebody has to be within a few metres.
                var eye = new Vector3(chunk * 0.5f - StreetNetwork.PavementOffset,
                    streetLevel + 1.7f, chunk * 0.5f);

                // Just ahead and slightly aside, so the whole body is in frame
                // and not blocking the street behind it.
                PlaceBody(root.transform, player,
                    new Vector3(eye.x + 1.2f, streetLevel, eye.z + 4.5f), 200.0f);

                // The rest walk away down the same pavement, giving a ladder of
                // distances to read the street's depth against.
                for (int i = 0; i < 5; i++)
                {
                    Vector3 spot = StreetNetwork.SnapToPavement(
                        new Vector3(eye.x, streetLevel, eye.z + 12.0f + i * 9.0f));
                    // Alternating sides, so both pavements are populated rather
                    // than one queue.
                    if (i % 2 != 0)
                    {
                        spot.x = chunk - spot.x;
                    }
                    PlaceBody(root.transform, civilian, spot, i % 2 == 0 ? 0.0f : 180.0f);
                }

                Shoot(streamer, camera, outputDirectory, "scale-street",
                    eye, new Vector3(-2.0f, 6.0f, 0.0f), 30);

                // Across the street from a body standing at a facade, so the
                // person can be read against the ground floor and its windows.
                var acrossEye = new Vector3(chunk * 0.5f + StreetNetwork.PavementOffset,
                    streetLevel + 1.7f, chunk * 0.5f + 2.0f);
                Shoot(streamer, camera, outputDirectory, "scale-facade",
                    acrossEye, new Vector3(-4.0f, -38.0f, 0.0f), 34);

                Debug.Log($"CityX: scale shots written to {outputDirectory}");
            }
            finally
            {
                RenderSettings.fog = previousFog;
                Object.DestroyImmediate(root);
            }
        }

        private static void PlaceBody(Transform parent, GameObject prefab, Vector3 spot, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent);
            instance.transform.position = spot;
            instance.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
        }

        /// <summary>
        /// The city as a CITY: aerial and skyline views of the downtown core
        /// and its fall-off, then street-level looks at a park, a plaza, an old
        /// town and an industrial estate. The zoning (CityZones) is a claim
        /// about the whole map, and the fixed-position shots above cannot show
        /// it -- from a 1.7m eye the difference between a downtown and an
        /// outskirts block is a few metres of wall.
        ///
        ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
        ///             -executeMethod CityX.ViewEditor.CityMassingCapture.CaptureCity
        /// </summary>
        [MenuItem("CityX/Capture City Overview")]
        public static void CaptureCity()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("CaptureRig");
            try
            {
                Material ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityGround.mat");
                Material pavement = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityPavement.mat");
                Material structure = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityConcrete.mat");
                if (ground == null || pavement == null || structure == null)
                {
                    Debug.LogError("CityX: materials missing -- run CityX/Rebuild Massing Scene first.");
                    return;
                }

                var sunObject = new GameObject("CaptureSun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                AttachSunFlare(sun);

                var moonObject = new GameObject("CaptureMoon");
                moonObject.transform.SetParent(root.transform);
                Light moon = moonObject.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.shadows = LightShadows.Soft;

                var cycleObject = new GameObject("CaptureCycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.Sun = sun;
                cycle.Moon = moon;
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.StartSeconds;

                var streamerObject = new GameObject("CaptureStreamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                Dress(streamer);

                var cameraObject = new GameObject("CaptureCamera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 4500.0f;
                camera.nearClipPlane = 0.3f;
                if (!AttachEnvironment(root, camera, cycle))
                {
                    return;
                }
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));

                var world = new CityWorld(WorldSeed);
                TerrainField terrain = world.Terrain;
                float baseHeight = terrain.PadHeightFor(0, 0);

                // Above the downtown, looking across it and out over the fall-off.
                Shoot(streamer, camera, outputDirectory, "city-aerial-core",
                    new Vector3(-60.0f, baseHeight + 520.0f, -820.0f),
                    new Vector3(30.0f, 4.0f, 0.0f), 34);

                // From outside the core, above the low ground and looking in: the
                // skyline should rise toward the middle rather than being one
                // height. Streams out to ~1km so the far side of the core loads.
                Shoot(streamer, camera, outputDirectory, "city-skyline-from-outside",
                    new Vector3(-720.0f, baseHeight + 300.0f, -720.0f),
                    new Vector3(10.0f, 45.0f, 0.0f), 44);

                ShootOpenBlock(streamer, camera, outputDirectory, world, "city-park",
                    (x, y) => world.RoleAt(x, y).Kind == LotKind.Open &&
                              CityChunkGenerator.ZoneOfRole(world.RoleAt(x, y)) != ZoneKind.Industrial &&
                              !CityMeshBuilder.IsPlazaZone(CityChunkGenerator.ZoneOfRole(world.RoleAt(x, y))));
                ShootOpenBlock(streamer, camera, outputDirectory, world, "city-plaza",
                    (x, y) => world.RoleAt(x, y).Kind == LotKind.Open &&
                              CityMeshBuilder.IsPlazaZone(CityChunkGenerator.ZoneOfRole(world.RoleAt(x, y))));
                ShootZone(streamer, camera, outputDirectory, world, "city-oldtown", ZoneKind.OldTown);
                ShootZone(streamer, camera, outputDirectory, world, "city-industrial", ZoneKind.Industrial);

                Debug.Log($"CityX: city shots written to {outputDirectory}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Photographs the spots listed in Logs/street-spots.txt, one per line:
        ///
        ///   name chunkX chunkY yawDegrees backMetres pitchDegrees upMetres
        ///
        /// The camera stands backMetres BEHIND the chunk's centre (opposite the way it faces) at
        /// upMetres above the street there, looking yawDegrees (0 = +Z) and pitched down by
        /// pitchDegrees. A text file rather than code so a spot can be looked at without a compile.
        ///
        ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
        ///             -executeMethod CityX.ViewEditor.CityMassingCapture.CaptureSpots
        /// </summary>
        public static void CaptureSpots()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);
            string listPath = Path.Combine(projectRoot, "Logs", "street-spots.txt");
            if (!File.Exists(listPath))
            {
                Debug.LogError($"CityX: no spot list at {listPath}.");
                return;
            }

            var root = new GameObject("CaptureRig");
            try
            {
                Material ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityGround.mat");
                Material pavement = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityPavement.mat");
                Material structure = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityConcrete.mat");
                if (ground == null || pavement == null || structure == null)
                {
                    Debug.LogError("CityX: materials missing -- run CityX/Rebuild Massing Scene first.");
                    return;
                }

                var sunObject = new GameObject("CaptureSun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                var moonObject = new GameObject("CaptureMoon");
                moonObject.transform.SetParent(root.transform);
                Light moon = moonObject.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.shadows = LightShadows.Soft;

                var cycleObject = new GameObject("CaptureCycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.Sun = sun;
                cycle.Moon = moon;
                cycle.Running = false;
                // Daylight, so the geometry reads: a night shot of a street is a shot of nothing.
                cycle.TimeOfDay = 12.0f * 3600.0f;

                var streamerObject = new GameObject("CaptureStreamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                Dress(streamer);

                var cameraObject = new GameObject("CaptureCamera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 3000.0f;
                camera.nearClipPlane = 0.2f;
                if (!AttachEnvironment(root, camera, cycle))
                {
                    return;
                }
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));

                var world = new CityWorld(WorldSeed);
                float chunk = CityLayout.ChunkSize;
                var invariant = System.Globalization.CultureInfo.InvariantCulture;

                foreach (string line in File.ReadAllLines(listPath))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 7 || parts[0].StartsWith("#"))
                    {
                        continue;
                    }
                    int cx = int.Parse(parts[1], invariant);
                    int cy = int.Parse(parts[2], invariant);
                    float yaw = float.Parse(parts[3], invariant);
                    float back = float.Parse(parts[4], invariant);
                    float pitch = float.Parse(parts[5], invariant);
                    float up = float.Parse(parts[6], invariant);

                    Vector3 facing = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward;
                    double px = (cx + 0.5) * chunk - facing.x * back;
                    double pz = (cy + 0.5) * chunk - facing.z * back;
                    var position = new Vector3(
                        (float)px, (float)world.Terrain.ContinuousHeightAt(px, pz) + up, (float)pz);
                    Shoot(streamer, camera, outputDirectory, "spot-" + parts[0], position,
                        new Vector3(pitch, yaw, 0.0f), 6);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Stands in the street beside the nearest chunk of an open block that
        /// satisfies <paramref name="wanted"/> and looks across it.
        /// </summary>
        private static void ShootOpenBlock(CityStreamer streamer, Camera camera, string directory,
            CityWorld world, string name, System.Func<int, int, bool> wanted)
        {
            (int dx, int dy)[] sides = { (1, 0), (-1, 0), (0, 1), (0, -1) };
            float chunk = CityLayout.ChunkSize;

            for (int ring = 0; ring <= 220; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int y = -ring; y <= ring; y++)
                    {
                        if (System.Math.Max(System.Math.Abs(x), System.Math.Abs(y)) != ring || !wanted(x, y))
                        {
                            continue;
                        }
                        foreach ((int dx, int dy) in sides)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!CityLayout.IsStreetChunk(nx, ny) || CityLayout.IsHighwayChunk(nx, ny))
                            {
                                continue;
                            }

                            // Stand on the street's FAR side, so a whole chunk of
                            // the block is in frame.
                            double px = (nx + 0.5) * chunk + dx * 8.0;
                            double pz = (ny + 0.5) * chunk + dy * 8.0;
                            Vector3 position = new Vector3(
                                (float)px, (float)world.Terrain.ContinuousHeightAt(px, pz) + 1.7f, (float)pz);
                            float yaw = Mathf.Atan2(-dx, -dy) * Mathf.Rad2Deg;
                            Debug.Log($"CityX: {name} shot -- open chunk ({x},{y}) seen from ({nx},{ny})");
                            Shoot(streamer, camera, directory, name, position, new Vector3(4.0f, yaw, 0.0f), 9);
                            return;
                        }
                    }
                }
            }
            Debug.LogWarning($"CityX: no chunk found for the {name} shot.");
        }

        /// <summary>True when a 5x5 spread of samples out to +-8 chunks around (x, y) are all in the zone -- the middle of it, not its edge.</summary>
        private static bool ZoneFillsAround(int x, int y, ZoneKind zone)
        {
            for (int dx = -8; dx <= 8; dx += 4)
            {
                for (int dy = -8; dy <= 8; dy += 4)
                {
                    if (CityZones.ZoneAt(x + dx + 0.5, y + dy + 0.5) != zone)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>A street-level look down a street of the nearest block whose zone is <paramref name="zone"/>.</summary>
        private static void ShootZone(CityStreamer streamer, Camera camera, string directory,
            CityWorld world, string name, ZoneKind zone)
        {
            float chunk = CityLayout.ChunkSize;
            for (int ring = 0; ring <= 260; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int y = -ring; y <= ring; y++)
                    {
                        if (System.Math.Max(System.Math.Abs(x), System.Math.Abs(y)) != ring ||
                            !CityLayout.IsStreetChunk(x, y) || CityLayout.IsHighwayChunk(x, y) ||
                            !ZoneFillsAround(x, y, zone))
                        {
                            continue;
                        }
                        bool alongX = CityLayout.StreetRunsAlongX(x, y);
                        double px = (x + 0.5) * chunk;
                        double pz = (y + 0.5) * chunk;
                        Vector3 position = new Vector3(
                            (float)px, (float)world.Terrain.ContinuousHeightAt(px, pz) + 22.0f, (float)pz);
                        float yaw = alongX ? 90.0f : 0.0f;
                        Debug.Log($"CityX: {name} shot -- {zone} street chunk ({x},{y})");
                        Shoot(streamer, camera, directory, name, position, new Vector3(14.0f, yaw, 0.0f), 12);
                        return;
                    }
                }
            }
            Debug.LogWarning($"CityX: no {zone} street found for the {name} shot.");
        }

        /// <summary>
        /// The same street at four moments of the cycle.
        ///
        /// The whole point of a day/night cycle is how it looks, and the two
        /// moments worth judging -- the horizon crossings -- last seconds out
        /// of thirty-four minutes. Waiting for them in play mode is not a way
        /// to iterate on them.
        /// </summary>
        [MenuItem("CityX/Capture Day-Night Screenshots")]
        public static void CaptureDayNight()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            Material ground = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityGround.mat");
            Material pavement = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityPavement.mat");
            Material structure = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityConcrete.mat");
            if (ground == null || pavement == null || structure == null)
            {
                Debug.LogError("CityX: materials missing -- run CityX/Rebuild Massing Scene first.");
                return;
            }

            var root = new GameObject("DayNightRig");
            var previousFog = RenderSettings.fog;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                AttachSunFlare(sun);

                var moonObject = new GameObject("Moon");
                moonObject.transform.SetParent(root.transform);
                Light moon = moonObject.AddComponent<Light>();
                moon.type = LightType.Directional;

                var streamerObject = new GameObject("Streamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                Dress(streamer);
                streamer.Rebuild();

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 3000.0f;
                camera.nearClipPlane = 0.2f;

                var cycleObject = new GameObject("Cycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                // The windows are glass now, so this is what has to be told the
                // hour -- without it every pane in the shot renders unlit.
                cycle.LitWindows = GlassMaterial(true);
                cycle.Sun = sun;
                cycle.Moon = moon;
                cycle.Running = false;
                if (!AttachEnvironment(root, camera, cycle))
                {
                    return;
                }

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 140.0f;
                RenderSettings.fogEndDistance = 330.0f;

                TerrainField terrain = new TerrainField(WorldSeed);
                float streetLevel = terrain.PadHeightFor(0, 0);
                float chunk = CityLayout.ChunkSize;
                var eye = new Vector3(chunk * 0.5f, streetLevel + 1.7f, chunk * 4.5f);
                var look = new Vector3(-10.0f, 0.0f, 0.0f);

                (string name, float time)[] moments =
                {
                    ("time-morning", SkyCycle.StartSeconds),
                    ("time-noon", SkyCycle.DaySeconds * 0.5f),
                    ("time-sunset", SkyCycle.DaySeconds),
                    ("time-night", SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f),
                };

                foreach ((string name, float time) in moments)
                {
                    cycle.Apply(SkyCycle.At(time));
                    Shoot(streamer, camera, outputDirectory, name, eye, look, 20);
                }

                Debug.Log($"CityX: day-night shots written to {outputDirectory}");
            }
            finally
            {
                RenderSettings.fog = previousFog;
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Finds the sharpest terrace drop along a street near the origin and
        /// photographs it from the low side, looking up at the wall.
        ///
        /// Restricted to street chunks on purpose: a step in the middle of a
        /// block is buried under a building and would photograph as nothing.
        /// </summary>
        private static void ShootTerraceEdge(CityStreamer streamer, Camera camera, string directory,
            TerrainField terrain)
        {
            float chunk = CityLayout.ChunkSize;
            int bestX = 0, bestY = 0;
            float bestDrop = 0.0f;

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y) || !CityLayout.IsStreetChunk(x, y + 1))
                    {
                        continue;
                    }
                    float drop = terrain.PadHeightFor(x, y + 1) - terrain.PadHeightFor(x, y);
                    if (drop > bestDrop)
                    {
                        bestDrop = drop;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            if (bestDrop <= 0.0f)
            {
                Debug.LogWarning("CityX: no terrace step found along any street near the origin.");
                return;
            }

            float low = terrain.PadHeightFor(bestX, bestY);
            Vector3 position = new Vector3(
                (bestX + 0.5f) * chunk, low + 1.7f, (bestY + 0.5f) * chunk - chunk * 1.2f);

            Debug.Log($"CityX: terrace shot at chunk ({bestX},{bestY}), a {bestDrop}m step");
            Shoot(streamer, camera, directory, "terrace", position, new Vector3(-6.0f, 0.0f, 0.0f), 14);
        }

        /// <summary>
        /// Standing partway up the steepest continuous run of street this
        /// world's own hillside grading produces, looking down it.
        ///
        /// ShootTerraceEdge answers "does a wall exist"; this answers the
        /// question the whole continuous-tilt system exists for -- "does a
        /// street read as San Francisco hills, not a staircase of flat
        /// plateaus". A single chunk's own corner-to-corner drop is a poor
        /// proxy for that (it says nothing about whether the NEXT chunk
        /// keeps descending the same way or levels right back out), so this
        /// scans RUNS of consecutive same-axis street chunks and scores each
        /// by its total drop end to end, the same "continuously graded, not
        /// a single tall step" thing a real hillside street is.
        /// </summary>
        private static void ShootSlopedStreet(CityStreamer streamer, Camera camera, string directory,
            TerrainField terrain)
        {
            const int RunLength = 8;
            float chunk = CityLayout.ChunkSize;

            int bestX = 0, bestY = 0;
            bool bestAlongX = false;
            double bestDrop = 0.0;

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }

                    if (CityLayout.StreetRunsAlongX(x, y))
                    {
                        bool runIsStreet = true;
                        for (int i = 1; i < RunLength && runIsStreet; i++)
                        {
                            runIsStreet = CityLayout.IsStreetChunk(x + i, y);
                        }
                        if (runIsStreet)
                        {
                            double drop = terrain.ContinuousHeightAt(x * (double)chunk, (y + 0.5) * chunk) -
                                terrain.ContinuousHeightAt((x + RunLength) * (double)chunk, (y + 0.5) * chunk);
                            if (drop > bestDrop)
                            {
                                bestDrop = drop;
                                bestX = x;
                                bestY = y;
                                bestAlongX = true;
                            }
                        }
                    }
                    else
                    {
                        bool runIsStreet = true;
                        for (int i = 1; i < RunLength && runIsStreet; i++)
                        {
                            runIsStreet = CityLayout.IsStreetChunk(x, y + i);
                        }
                        if (runIsStreet)
                        {
                            double drop = terrain.ContinuousHeightAt((x + 0.5) * chunk, y * (double)chunk) -
                                terrain.ContinuousHeightAt((x + 0.5) * chunk, (y + RunLength) * (double)chunk);
                            if (drop > bestDrop)
                            {
                                bestDrop = drop;
                                bestX = x;
                                bestY = y;
                                bestAlongX = false;
                            }
                        }
                    }
                }
            }

            if (bestDrop <= 0.0)
            {
                Debug.LogWarning("CityX: no continuously-descending street run found near the origin.");
                return;
            }

            // Two chunks up the run, standing on the street's own tilted
            // surface (ContinuousHeightAt, not the quantised pad -- see
            // BuildGround's own "hillside streets" comment), looking down
            // the remaining length of the drop.
            Vector3 position;
            Vector3 euler;
            if (bestAlongX)
            {
                double px = (bestX + 2.0) * chunk;
                double pz = (bestY + 0.5) * chunk;
                position = new Vector3((float)px, (float)terrain.ContinuousHeightAt(px, pz) + 1.7f, (float)pz);
                euler = new Vector3(8.0f, 90.0f, 0.0f);
            }
            else
            {
                double px = (bestX + 0.5) * chunk;
                double pz = (bestY + 2.0) * chunk;
                position = new Vector3((float)px, (float)terrain.ContinuousHeightAt(px, pz) + 1.7f, (float)pz);
                euler = new Vector3(8.0f, 0.0f, 0.0f);
            }

            Debug.Log($"CityX: sloped-street shot from chunk ({bestX},{bestY}), " +
                $"{bestDrop:0.0}m of continuous drop over {RunLength} chunks");
            Shoot(streamer, camera, directory, "sloped-street", position, euler, 16);
        }

        /// <summary>
        /// A real T-junction: one arm of a 4-way crossing closed off with a
        /// barrier instead of a crosswalk -- CityLayout.IsClosedJunctionArm's
        /// own answer to explicit feedback: "roads shouldnt be just wiggly
        /// for no reason... a turn or dead end or 4 way intersection."
        /// Scans outward from the origin for the first one rather than
        /// hoping a fixed shot happens to land on one -- chunk (0,0) itself
        /// never qualifies, since IsArterialChunk chunks never close.
        /// Stands in the CLOSED arm's own chunk looking back at the
        /// junction, the approach where the barrier actually reads as
        /// blocking the way rather than as a fence glimpsed side-on.
        /// </summary>
        private static void ShootClosedJunction(CityStreamer streamer, Camera camera, string directory,
            TerrainField terrain)
        {
            float chunk = CityLayout.ChunkSize;
            (int dx, int dy)[] arms = { (1, 0), (-1, 0), (0, 1), (0, -1) };

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (!CityLayout.IsStreetChunk(x - 1, y) || !CityLayout.IsStreetChunk(x + 1, y) ||
                        !CityLayout.IsStreetChunk(x, y - 1) || !CityLayout.IsStreetChunk(x, y + 1))
                    {
                        continue;
                    }

                    foreach ((int dx, int dy) in arms)
                    {
                        if (!CityLayout.IsClosedJunctionArm(x, y, dx, dy))
                        {
                            continue;
                        }

                        double px = (x + dx + 0.5) * chunk;
                        double pz = (y + dy + 0.5) * chunk;
                        Vector3 position = new Vector3(
                            (float)px, (float)terrain.ContinuousHeightAt(px, pz) + 1.7f, (float)pz);
                        float yaw = Mathf.Atan2(-dx, -dy) * Mathf.Rad2Deg;

                        Debug.Log($"CityX: closed-junction shot -- junction ({x},{y}), arm ({dx},{dy})");
                        Shoot(streamer, camera, directory, "closed-junction", position,
                            new Vector3(2.0f, yaw, 0.0f), 10);
                        return;
                    }
                }
            }

            Debug.LogWarning("CityX: no closed junction arm found near the origin.");
        }

        /// <summary>
        /// One building's wall, square on and close.
        ///
        /// THE ANGLE THE OTHER SHOTS CANNOT GIVE. Everything that happens on a
        /// facade -- balconies, ribs, and eventually real windows -- lives in
        /// the bottom few storeys, which the rooftop shot flies 90m above and
        /// the street shot only ever sees edge-on down a canyon. Balconies went
        /// in and were invisible in both, and the triangle count was the only
        /// thing saying they existed at all.
        ///
        /// Aimed at a wall that is NOT the entrance, since that is the one wall
        /// deliberately kept clear of decoration.
        /// </summary>
        private static void ShootFacade(CityStreamer streamer, Camera camera, string directory)
        {
            var world = new CityWorld(WorldSeed);

            BuildingSite chosen = default;
            var chosenSubjects = new List<Vector3>();
            float best = float.MaxValue;
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y ||
                        site.Params.Width < 16.0f)
                    {
                        continue;
                    }
                    // The wall at minimum Z has to face a street, or backing
                    // off from it puts the camera inside the neighbour -- which
                    // is what the first version of this shot did, and it
                    // photographed the inside of someone else's wall.
                    if (!CityLayout.IsStreetChunk(x, y - 1))
                    {
                        continue;
                    }
                    // ON THIS WALL, not just somewhere on the building. Two of
                    // the three walls a balcony can land on face sideways, so
                    // "has a balcony" picked a building whose balconies were
                    // both round the corner, and the shot proved nothing.
                    var subjects = new List<Vector3>();
                    foreach (BuildingDecor.Balcony balcony in BuildingDecor.Balconies(
                                 world.ChunkSeed(x, y),
                                 site.Params.Width, site.Params.Depth, site.Params.Height))
                    {
                        if (balcony.Normal.z < -0.5f)
                        {
                            subjects.Add(new Vector3(
                                (float)site.CentreX + balcony.Centre.x,
                                site.BaseHeight + balcony.Centre.y,
                                (float)site.CentreZ + balcony.Centre.z));
                        }
                    }

                    bool ribbed = false;
                    foreach (BuildingDecor.Rib rib in BuildingDecor.Ribs(
                                 world.ChunkSeed(x, y),
                                 site.Params.Width, site.Params.Depth, site.Params.Height,
                                 -BuildingInterior.DoorWidth * 0.5f,
                                 BuildingInterior.DoorWidth * 0.5f).Fins)
                    {
                        ribbed |= rib.Normal.z < -0.5f;
                    }

                    if (subjects.Count == 0 && !ribbed)
                    {
                        continue;
                    }

                    // A wall carrying BOTH beats a wall carrying one, which
                    // beats nearer -- one photograph that answers two questions
                    // is worth more than a short walk. Every candidate here is
                    // already within 150m of the origin.
                    float score = x * x + y * y
                                  - subjects.Count * 1000.0f
                                  - (ribbed ? 20000.0f : 0.0f)
                                  - (ribbed && subjects.Count > 0 ? 40000.0f : 0.0f);
                    if (score < best)
                    {
                        best = score;
                        chosen = site;
                        chosenSubjects = subjects;
                    }
                }
            }

            if (!chosen.Exists)
            {
                Debug.LogWarning("CityX: no decorated building near the origin to photograph.");
                return;
            }

            // AIMED AT THE BALCONIES THEMSELVES, not at a height that ought to
            // contain them. The first version framed the wall from 9m up, which
            // is a sound guess -- rows 1..6 at a 3.2m floor span 3m to 19m --
            // and the one balcony on the building it picked was on row 6, one
            // tenth of a degree outside the top of the frame. A shot that only
            // works when the subject lands mid-range is a shot that will lie
            // again the next time the seed moves.
            var look = new Vector3(
                (float)chosen.CentreX,
                chosen.BaseHeight + Mathf.Min(9.0f, chosen.Params.Height * 0.5f),
                (float)chosen.MinZ);
            if (chosenSubjects.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (Vector3 subject in chosenSubjects)
                {
                    sum += subject;
                }
                look.x = sum.x / chosenSubjects.Count;
                look.y = sum.y / chosenSubjects.Count;
            }

            // OFF TO ONE SIDE, NOT SQUARE ON. Everything a balcony is -- a slab
            // standing 1.1m off the wall with a rail round it -- is depth, and
            // depth photographed head-on is a rectangle drawn on a wall. The
            // yaw is derived from where the camera actually ends up rather than
            // guessed, so moving the offsets cannot swing the aim off the wall.
            const float SideOffset = 13.0f;
            const float Standoff = 17.0f;

            var from = look + new Vector3(-SideOffset, 0.0f, -Standoff);
            float yaw = Mathf.Atan2(SideOffset, Standoff) * Mathf.Rad2Deg;

            Debug.Log($"CityX: facade shot of ({chosen.AnchorX},{chosen.AnchorY}), " +
                      $"{chosenSubjects.Count} balconies on the -Z wall, camera {from}");
            Shoot(streamer, camera, directory, "facade", from, new Vector3(0.0f, yaw, 0.0f), 6);
        }

        /// <summary>
        /// Inside a building: the lobby, and the top of a flight.
        ///
        /// A BUILDING BEING ENTERABLE IS EXACTLY THE THING A SKYLINE SHOT
        /// CANNOT SHOW. From the street a hollow building and a solid one are
        /// the same picture. These two stand in the doorway and on the first
        /// landing, which is the only way to see whether the floor is really
        /// there, whether the stairwell lines up with the opening above it,
        /// and whether the walls face inward.
        /// </summary>
        private static void ShootInterior(CityStreamer streamer, Camera camera, string directory)
        {
            var world = new CityWorld(WorldSeed);

            // A wide, low-ish building near the origin: a tower's ground floor
            // is a room you cannot see the far wall of.
            BuildingSite chosen = default;
            float best = float.MaxValue;
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y ||
                        site.Params.Width < 14.0f || site.Params.Depth < 14.0f)
                    {
                        continue;
                    }
                    float distance = x * x + y * y + site.Params.Height * 0.02f;
                    if (distance < best)
                    {
                        best = distance;
                        chosen = site;
                    }
                }
            }

            if (!chosen.Exists)
            {
                Debug.LogWarning("CityX: no building near the origin big enough to stand inside.");
                return;
            }

            float storey = BuildingInterior.StoreyHeight(chosen.Params.Height);
            int floors = BuildingInterior.FloorCountFor(chosen.Params.Height);
            Debug.Log($"CityX: interior shot inside ({chosen.AnchorX},{chosen.AnchorY}), "
                      + $"{chosen.Params.Width:0}x{chosen.Params.Depth:0}m, {floors} floors, "
                      + $"{storey:0.0}m per storey");

            // Just inside the door, which is centred on the wall at maximum Z,
            // looking back into the room the way somebody walking in would.
            var doorway = new Vector3(
                (float)chosen.CentreX, chosen.BaseHeight + 1.7f, (float)chosen.MaxZ - 1.5f);
            Shoot(streamer, camera, directory, "interior-lobby", doorway,
                new Vector3(4.0f, 180.0f, 0.0f), 6);

            // On the first floor, looking down its own stairwell at the flight
            // that got you there.
            float run = BuildingInterior.RunFor(
                chosen.Params.Width * 0.5f, chosen.Params.Depth * 0.5f);
            BuildingInterior.Stairwell well = BuildingInterior.StairwellFor(
                0, chosen.Params.Width * 0.5f, chosen.Params.Depth * 0.5f, run);
            Vector3 foot = well.FootOf(run);
            var landing = new Vector3(
                (float)chosen.CentreX + foot.x,
                chosen.BaseHeight + storey + 1.7f,
                (float)chosen.CentreZ + foot.z);
            Shoot(streamer, camera, directory, "interior-stairs", landing,
                new Vector3(24.0f, Mathf.Atan2(-well.RunDirection.x, -well.RunDirection.z)
                                   * Mathf.Rad2Deg, 0.0f), 6);
        }

        /// <summary>
        /// A front door, from the street.
        ///
        /// THE ONLY ANGLE THAT SHOWS A DOORWAY FROM OUTSIDE, which turns out to
        /// be the shot that says most about whether a building reads as
        /// enterable: you can see the opening, the lobby behind it and the
        /// first flight of stairs through the ground-floor glass, all at once.
        /// The interior shots stand inside and cannot show any of that.
        ///
        /// It went in to photograph a special building's sign, which no fixed
        /// camera would ever have caught at one lot in thirty. The signs came
        /// back off; the shot earned its place anyway.
        /// </summary>
        private static void ShootEntrance(CityStreamer streamer, Camera camera, string directory)
        {
            var world = new CityWorld(WorldSeed);

            BuildingSite chosen = default;
            float best = float.MaxValue;
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y ||
                        site.Params.Width < 14.0f)
                    {
                        continue;
                    }
                    // The door is on the wall at maximum Z, so that side has to
                    // be a street or there is nowhere to stand and look at it.
                    if (!CityLayout.IsStreetChunk(x, y + site.Role.SpanY))
                    {
                        continue;
                    }
                    float distance = x * x + y * y;
                    if (distance < best)
                    {
                        best = distance;
                        chosen = site;
                    }
                }
            }

            if (!chosen.Exists)
            {
                Debug.LogWarning("CityX: no building faces a street near the origin.");
                return;
            }

            var from = new Vector3(
                (float)chosen.CentreX,
                chosen.BaseHeight + 2.2f,
                (float)chosen.MaxZ + 12.0f);
            Debug.Log($"CityX: entrance shot of ({chosen.AnchorX},{chosen.AnchorY})");
            Shoot(streamer, camera, directory, "entrance", from, new Vector3(0.0f, 180.0f, 0.0f), 6);
        }

        /// <summary>
        /// A real window, shot a few times, photographed from a couple of metres: what the bullet holes and
        /// their crack decals actually look like on the glass. Written for the report that each hole showed a
        /// visible square round it.
        ///
        ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
        ///             -executeMethod CityX.ViewEditor.CityMassingCapture.CaptureBulletHoles
        /// </summary>
        public static void CaptureBulletHoles()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("CaptureRig");
            try
            {
                Material ground = Material_("CityGround");
                Material pavement = Material_("CityPavement");
                Material structure = Material_("CityConcrete");
                if (ground == null || pavement == null || structure == null)
                {
                    Debug.LogError("CityX: materials missing -- run CityX/Rebuild Massing Scene first.");
                    return;
                }

                var sunObject = new GameObject("CaptureSun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                var moonObject = new GameObject("CaptureMoon");
                moonObject.transform.SetParent(root.transform);
                Light moon = moonObject.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.shadows = LightShadows.Soft;

                var cycleObject = new GameObject("CaptureCycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.Sun = sun;
                cycle.Moon = moon;
                cycle.Running = false;
                // NIGHT, not the default daytime start -- the report's own screenshot was a dim interior at
                // night, and a translucent decal's own faint edges read very differently against a bright sky
                // than against the dark, low-contrast lighting the bug was actually seen under.
                cycle.TimeOfDay = SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f;

                var streamerObject = new GameObject("CaptureStreamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                Dress(streamer);
                streamer.CrackDecalMaterial = Material_("BulletHoleDecal");
                streamer.Rebuild();

                var cameraObject = new GameObject("CaptureCamera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 600.0f;
                camera.nearClipPlane = 0.05f;
                camera.fieldOfView = 50.0f;
                if (!AttachEnvironment(root, camera, cycle))
                {
                    return;
                }
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));

                var world = new CityWorld(WorldSeed);
                for (int x = -8; x <= 8; x++)
                {
                    for (int y = -8; y <= 8; y++)
                    {
                        BuildingSite site = world.BuildingAt(x, y);
                        if (!site.Exists || site.AnchorX != x || site.AnchorY != y || site.Params.Width < 8.0f)
                        {
                            continue;
                        }
                        WindowPlan plan = WindowPlan.For(world, site);
                        CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                        CellLayout columns = plan.ColumnsFor(site.Params.Width);
                        int row = 1;
                        for (int column = 1; column < columns.Count - 1; column++)
                        {
                            if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                            {
                                continue;
                            }
                            var centre = new Vector3(
                                (float)site.MinX + (column + 0.5f) * columns.Size,
                                site.BaseHeight + (row + 0.5f) * rows.Size,
                                (float)site.MaxZ);
                            if (!GlassDamage.TryPaneAt(world, site, centre + Vector3.forward * 0.02f, out PaneKey key))
                            {
                                continue;
                            }

                            Vector3 standing = centre + Vector3.forward * 2.4f;
                            // The damage goes in BEFORE the chunk is built: this rig runs in the editor, where the
                            // streamer is not listening for damage changes (OnEnable does not run) and so would
                            // never rebuild a chunk that was already there.
                            streamer.Damage.Shoot(key, new Vector2(0.3f, 0.65f), out _);
                            streamer.Damage.Shoot(key, new Vector2(0.62f, 0.4f), out _);
                            streamer.Damage.Shoot(key, new Vector2(0.5f, 0.75f), out _);
                            streamer.RadiusChunks = 2;
                            streamer.StreamAround(standing, int.MaxValue);

                            Shoot(streamer, camera, outputDirectory, "bullet-holes", standing, new Vector3(0.0f, 180.0f, 0.0f), 2);
                            Shoot(streamer, camera, outputDirectory, "bullet-holes-close",
                                centre + Vector3.forward * 1.0f, new Vector3(0.0f, 180.0f, 0.0f), 2);

                            // HOW MUCH OF THE DECAL'S QUAD DIFFERS FROM PLAIN GLASS? The same view with and
                            // without the decal material -- its transparent surround must leave the glass exactly
                            // as it was, so only the crack lines (a small share of the quad) may differ; a
                            // material that adds anything to transparent pixels lights the whole square.
                            Vector3 sideView = centre + new Vector3(2.4f, -0.1f, 0.9f);
                            Vector3 angledEuler = Quaternion.LookRotation((centre - sideView).normalized, Vector3.up).eulerAngles;
                            void RebuildAround(Vector3 at)
                            {
                                streamer.RadiusChunks = 2;
                                streamer.Rebuild();
                                streamer.StreamAround(at, int.MaxValue);
                            }
                            void ReshootAllHoles()
                            {
                                streamer.Damage.Shoot(key, new Vector2(0.3f, 0.65f), out _);
                                streamer.Damage.Shoot(key, new Vector2(0.62f, 0.4f), out _);
                                streamer.Damage.Shoot(key, new Vector2(0.5f, 0.75f), out _);
                            }
                            foreach ((string label, Vector3 at, Vector3 euler) in new[]
                                     {
                                         ("head-on", centre + Vector3.forward * 1.0f, new Vector3(0.0f, 180.0f, 0.0f)),
                                         ("angled", sideView, angledEuler),
                                     })
                            {
                                Material decalMaterial = streamer.CrackDecalMaterial;
                                Texture2D withDecal = Render(streamer, camera, at, euler);

                                streamer.CrackDecalMaterial = null;
                                RebuildAround(at);
                                ReshootAllHoles();
                                Texture2D withoutDecal = Render(streamer, camera, at, euler);
                                streamer.CrackDecalMaterial = decalMaterial;
                                RebuildAround(at);
                                ReshootAllHoles();

                                DiffCoverage(withDecal, withoutDecal, out float coverage, out float peak, out int boxW, out int boxH);
                                Debug.Log($"CityX: decal coverage [{label}]: {coverage * 100.0f:0.0}% of its {boxW}x{boxH}px bounding box differs from plain glass, peak channel difference {peak:0}/255");
                                Object.DestroyImmediate(withDecal);
                                Object.DestroyImmediate(withoutDecal);
                            }

                            // The angled view above, also saved as a file to actually look at (the current
                            // decal material is back in force after the loop's last iteration restored it).
                            RebuildAround(sideView);
                            ReshootAllHoles();
                            Shoot(streamer, camera, outputDirectory, "bullet-holes-angled", sideView, angledEuler, 2);
                            Debug.Log($"CityX: bullet hole shots written for pane {key}");
                            return;
                        }
                    }
                }
                Debug.LogError("CityX: found no window to shoot at.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Every material a chunk wears beyond the three the capture already
        /// loads by hand.
        ///
        /// IN ONE PLACE BECAUSE FORGETTING ONE IS SILENT. The streamer skips
        /// anything it has no material for, so a capture missing one renders a
        /// city with that feature simply absent -- which is exactly what
        /// happened the first time signs were photographed: the geometry was
        /// there, the test passed, and the picture showed a police station with
        /// a blank wall over its door.
        /// </summary>
        private static void Dress(CityStreamer streamer)
        {
            streamer.DarkGlassMaterial = Material_("CityGlassDark");
            streamer.LitGlassMaterial = Material_("CityGlassLit");
            streamer.CrackedGlassMaterial = Material_("CityGlassCracked");
            streamer.RoadLineMaterial = Material_("RoadLine");
            streamer.CrosswalkMaterial = Material_("Crosswalk");
            streamer.DoorMaterial = Material_("BuildingDoor");
            streamer.DoorHandleMaterial = Material_("BuildingDoorHandle");
            streamer.GuardRailMaterial = Material_("CityGuardRail");
            streamer.BenchMaterial = Material_("CityBenchWood");
            streamer.GrassMaterial = Material_("CityGrass");
            streamer.BarkMaterial = Material_("CityBenchWood");
        }

        private static Material Material_(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(
                $"Assets/CityX/View/Materials/{name}.mat");
        }

        /// <summary>
        /// Same reason as <see cref="Dress"/> and <see cref="AttachEnvironment"/>:
        /// the capture rig builds its own Sun rather than reusing the scene's,
        /// so anything the real Sun grows has to be repeated here by hand or
        /// this stops photographing the actual game. Loads the real,
        /// on-disk flare asset CityMassingSceneBuilder builds rather than
        /// improvising a second one.
        /// </summary>
        private static void AttachSunFlare(Light sun)
        {
            LensFlareComponentSRP flare = sun.gameObject.AddComponent<LensFlareComponentSRP>();
            flare.lensFlareData = CityMassingSceneBuilder.EnsureSunLensFlareData();
            flare.intensity = 1.4f;
            flare.useOcclusion = true;
            flare.occlusionRadius = 0.2f;
            flare.sampleCount = 16;
        }

        /// <summary>
        /// Hangs the scene's real sky and grade on a capture rig.
        ///
        /// SAME REASON AS <see cref="Dress"/>, one level up. A rig that builds
        /// its own lighting stops photographing the game the moment the game's
        /// lighting changes -- and a camera whose only job is catching what a
        /// test cannot is worth nothing if it is quietly shooting something
        /// else. Everything the look depends on is loaded from disk here, so
        /// there is nothing to keep in step by hand.
        ///
        /// The tonemap matters more than it sounds: without the grade volume,
        /// the renderer's HDR output is simply clamped and every sunlit face in
        /// the photograph lands on flat white.
        /// </summary>
        private static bool AttachEnvironment(GameObject root, Camera camera, DayNightCycle cycle)
        {
            var skyDome = Material_("CityXSky");
            var grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/CityX/View/Materials/CityGrade.asset");
            if (skyDome == null || grade == null)
            {
                Debug.LogError("CityX: sky or grade missing -- run CityX/Rebuild Massing Scene " +
                               "first.");
                return false;
            }

            RenderSettings.skybox = skyDome;
            RenderSettings.defaultReflectionMode =
                UnityEngine.Rendering.DefaultReflectionMode.Skybox;

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

        /// <summary>The lit-window glass, which the day/night cycle drives.</summary>
        private static Material GlassMaterial(bool lit)
        {
            return Material_(lit ? "CityGlassLit" : "CityGlassDark");
        }

        private static void Shoot(CityStreamer streamer, Camera camera, string directory, string name,
            Vector3 position, Vector3 euler, int radiusChunks)
        {
            streamer.RadiusChunks = radiusChunks;
            // int.MaxValue: build the whole visible city in this one call. A
            // shot of a half-streamed world would look like a bug that is not
            // there.
            streamer.StreamAround(position, int.MaxValue);

            camera.transform.position = position;
            camera.transform.rotation = Quaternion.Euler(euler);

            // Same AA the real player camera gets (see CityMassingSceneBuilder's
            // own reasoning) -- without this a screenshot is honestly a worse
            // picture than the game itself, since MSAA needs the RENDER
            // TEXTURE it draws into to itself request samples, which a plain
            // GetTemporary call below does not do on its own.
            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            // Same renderPostProcessing gap as the real camera in
            // CityMassingSceneBuilder -- without it these screenshots would
            // have kept "confirming" tonemapping/bloom/vignette/grain/flare
            // that was never actually rendering, in either the real game or
            // the picture meant to verify it.
            cameraData.renderPostProcessing = true;
            camera.allowMSAA = true;

            RenderTexture target = RenderTexture.GetTemporary(
                Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, PC_MsaaSamples);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
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

        /// <summary>Renders one frame to an in-memory RGB24 texture, for a pixel comparison rather than a saved file. Caller destroys it.</summary>
        private static Texture2D Render(CityStreamer streamer, Camera camera, Vector3 position, Vector3 euler)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.Euler(euler);

            RenderTexture target = RenderTexture.GetTemporary(
                Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, PC_MsaaSamples);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>
        /// How much of the smallest box containing every changed pixel actually differs between two equally
        /// sized renders -- built for comparing a pane rendered with and without its crack decal, to tell a
        /// hole's OWN handful of crack pixels apart from a stray rectangle the decal's texture leaves lit.
        /// </summary>
        private static void DiffCoverage(Texture2D a, Texture2D b, out float coverage, out float peakChannelDiff, out int boxWidth, out int boxHeight)
        {
            Color32[] pa = a.GetPixels32();
            Color32[] pb = b.GetPixels32();
            int minX = a.width, minY = a.height, maxX = -1, maxY = -1;
            int changed = 0;
            peakChannelDiff = 0.0f;
            const int Threshold = 6;   // ignores ordinary anti-aliasing/dithering noise between two otherwise-identical renders
            for (int y = 0; y < a.height; y++)
            {
                for (int x = 0; x < a.width; x++)
                {
                    Color32 ca = pa[y * a.width + x];
                    Color32 cb = pb[y * a.width + x];
                    int dr = Mathf.Abs(ca.r - cb.r), dg = Mathf.Abs(ca.g - cb.g), db = Mathf.Abs(ca.b - cb.b);
                    int d = Mathf.Max(dr, Mathf.Max(dg, db));
                    peakChannelDiff = Mathf.Max(peakChannelDiff, d);
                    if (d <= Threshold)
                    {
                        continue;
                    }
                    changed++;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            }
            if (maxX < minX)
            {
                coverage = 0.0f;
                boxWidth = 0;
                boxHeight = 0;
                return;
            }
            boxWidth = maxX - minX + 1;
            boxHeight = maxY - minY + 1;
            coverage = changed / (float)(boxWidth * boxHeight);
        }
    }
}
#endif // CITYX_LEGACY_GRID_DIAGNOSTICS
