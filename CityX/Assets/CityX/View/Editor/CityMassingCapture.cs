using System.IO;
using CityX.View;
using CityX.World;
using UnityEditor;
using UnityEngine;

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
        private const int WorldSeed = 20260810;

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

                var sunObject = new GameObject("CaptureSun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                // Matches the scene's own sun. See CityMassingSceneBuilder for
                // why it is near 1 rather than near 2.
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sunObject.transform.rotation = Quaternion.Euler(40.0f, 150.0f, 0.0f);

                // Without this every surface facing away from the sun renders
                // near black, and in a city of narrow canyons that is most of
                // them. The first capture pass lost the entire lower half of
                // every shot to it.
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.66f);
                RenderSettings.ambientEquatorColor = new Color(0.38f, 0.40f, 0.44f);
                RenderSettings.ambientGroundColor = new Color(0.20f, 0.20f, 0.21f);

                var streamerObject = new GameObject("CaptureStreamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                streamer.Rebuild();

                var cameraObject = new GameObject("CaptureCamera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 3000.0f;
                camera.nearClipPlane = 0.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.60f, 0.66f, 0.72f);

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

                var streamerObject = new GameObject("Streamer");
                streamerObject.transform.SetParent(root.transform);
                CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
                streamer.WorldSeed = WorldSeed;
                streamer.GroundMaterial = ground;
                streamer.PavementMaterial = pavement;
                streamer.StructureMaterial = structure;
                streamer.Rebuild();

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 3000.0f;
                camera.nearClipPlane = 0.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;

                var cycleObject = new GameObject("Cycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.Sun = sun;
                cycle.Sky = camera;
                cycle.Running = false;
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
                streamer.Rebuild();

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 3000.0f;
                camera.nearClipPlane = 0.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;

                var cycleObject = new GameObject("Cycle");
                cycleObject.transform.SetParent(root.transform);
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.Sun = sun;
                cycle.Moon = moon;
                cycle.Sky = camera;
                cycle.Facades = structure;
                cycle.Running = false;

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

            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
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
    }
}
