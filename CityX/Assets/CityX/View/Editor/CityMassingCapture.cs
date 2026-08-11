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
                Material structure = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/CityX/View/Materials/CityConcrete.mat");
                if (ground == null || structure == null)
                {
                    Debug.LogError("CityX: materials missing -- run CityX/Rebuild Massing Scene first.");
                    return;
                }

                var sunObject = new GameObject("CaptureSun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.8f;
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
