using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: finds a building of each new shape near the spawn (L/T/U wings, a staggered pair, a twisted, a wonky and a cantilevered tower) and
    /// renders each from the air and from the street, plus a skyline, to Logs/CityShots/shape-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BuildingShapeCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheShapes()
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
            UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }

            // One of each, nearest the spawn first.
            var picks = new Dictionary<string, BuildingSite>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 80))
            {
                if (picks.Count >= 15) break;
                if (site.OnRoof || site.Wing) continue;   // classify whole buildings, from their first volume
                string kind = KindOf(world, site);
                if (kind == null && site.Params.Height > 20.0f && site.Params.Width > 14.0f)
                {
                    kind = "box-" + WindowPlan.For(world, site).Style.ToString().ToLowerInvariant();   // a plain building of each facade family
                }
                if (kind != null && !picks.ContainsKey(kind)) picks[kind] = site;
                // A door well above the paving: its entrance stairs (EntranceStairs) must run down along the wall to the ground.
                if (!picks.ContainsKey("stairs") && !site.Shell && site.Sides < 3
                    && BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).Rise > 1.5f)
                {
                    picks["stairs"] = site;
                }
            }

            var cameraObject = new GameObject("ShapeCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55.0f;
            camera.farClipPlane = 2500.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            foreach (KeyValuePair<string, BuildingSite> pick in picks)
            {
                BuildingSite site = pick.Value;
                Vector3 centre = TestSites.At(site, site.CentreX, site.BaseHeight, site.CentreZ);
                streamer.StreamAround(centre, int.MaxValue);
                yield return null;
                yield return null;
                float top = TallestOver(world, site);
                float size = Mathf.Max(site.Params.Width, site.Params.Depth, (top - site.BaseHeight) * 0.6f);
                Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                Vector3 side = Vector3.Cross(Vector3.up, front);
                Vector3 mid = new Vector3(centre.x, (site.BaseHeight + top) * 0.5f, centre.z);
                // Steep, from well above the roofs around it, so the footprint reads and no neighbour is in the way.
                Shoot(camera, directory, $"shape-{pick.Key}-top", new Vector3(centre.x, top + size * 1.4f + 40.0f, centre.z) + front * size * 0.9f + side * size * 0.4f,
                    new Vector3(centre.x, site.BaseHeight, centre.z));
                Shoot(camera, directory, $"shape-{pick.Key}-air", mid + (front * 1.1f + side * 0.9f).normalized * size * 2.2f + Vector3.up * size * 0.9f, mid);
                Shoot(camera, directory, $"shape-{pick.Key}-back", mid + (-front * 1.0f - side * 0.8f).normalized * size * 2.2f + Vector3.up * size * 0.7f, mid);
                Vector3 street = centre + front * (site.Params.Depth * 0.5f + 14.0f) + side * 6.0f + Vector3.up * 1.8f;
                Shoot(camera, directory, $"shape-{pick.Key}-street", street, new Vector3(centre.x, site.BaseHeight + (top - site.BaseHeight) * 0.55f, centre.z));
                Debug.Log($"CityX shape capture: {pick.Key} = #{site.Id} {site.Params.Width:F0}x{site.Params.Depth:F0}x{site.Params.Height:F0}, top {top:F0}");
            }

            // The skyline over the spawn.
            world.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vector3((float)sx, (float)world.Plan.GroundHeight(sx, sz), (float)sz);
            streamer.StreamAround(spawn, int.MaxValue);
            yield return null;
            yield return null;
            Shoot(camera, directory, "shape-skyline", spawn + new Vector3(-160.0f, 140.0f, -160.0f), spawn + new Vector3(0.0f, 40.0f, 0.0f));
            Shoot(camera, directory, "shape-skyline2", spawn + new Vector3(170.0f, 90.0f, 60.0f), spawn + new Vector3(0.0f, 50.0f, 0.0f));

            Debug.Log($"CityX shape capture: found {string.Join(", ", picks.Keys)}");

            // Studio shots: each building rebuilt alone, high above the city, so its shape reads with nothing in the way (and no distance fog).
            RenderSettings.fog = false;
            // No city edge to hide up here: a studio camera stands further from a tall tower than the streamed city reaches.
            RenderSettings.fogStartDistance = 4000.0f;
            RenderSettings.fogEndDistance = 5000.0f;
            if (cycle != null)
            {
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            foreach (KeyValuePair<string, BuildingSite> pick in picks)
            {
                GameObject alone = BuildAlone(world, streamer, FirstOf(world, pick.Value), new Vector3(0.0f, 3000.0f, 0.0f), out float top, out float size);
                Vector3 mid = new Vector3(0.0f, 3000.0f + top * 0.5f, 0.0f);
                Vector3 front = CityMeshBuilder.WallNormal(pick.Value, WindowGrid.Wall.North);
                Vector3 side = Vector3.Cross(Vector3.up, front);
                Shoot(camera, directory, $"shape-{pick.Key}-alone-front", mid + (front + side * 0.7f).normalized * size * 2.4f + Vector3.up * size * 0.5f, mid);
                Shoot(camera, directory, $"shape-{pick.Key}-alone-back", mid + (-front + side * 0.6f).normalized * size * 2.4f + Vector3.up * size * 0.9f, mid);
                Shoot(camera, directory, $"shape-{pick.Key}-alone-above", mid + Vector3.up * (top * 0.5f + size * 1.8f) + front * size * 0.5f, new Vector3(0.0f, 3000.0f, 0.0f));
                Object.Destroy(alone);
                yield return null;
            }
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }

        /// <summary>
        /// The city scene is loaded ADDITIVELY, so the render settings in force are the test scene's, not its own: set the fog distances the haze's
        /// edge is drawn from (DayNightCycle) to the city's clear-weather ones, which the scene builder hands RainEffect.
        /// </summary>
        public static void UseTheScenesAir()
        {
            var rain = Object.FindFirstObjectByType<RainEffect>();
            RenderSettings.fogStartDistance = rain != null ? rain.ClearFogStart : 150.0f;
            RenderSettings.fogEndDistance = rain != null ? rain.ClearFogEnd : 380.0f;
            // And its sky: the render settings are the test scene's, whose skybox is Unity's default daytime one -- every "night" capture showed a
            // pale blue sky and no stars, and reflected it. The city's own dome is what the game draws.
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null && cycle.SkyDome != null)
            {
                RenderSettings.skybox = cycle.SkyDome;
            }
        }

        /// <summary>
        /// One whole building (its first volume and every wing and volume that follows it) built with the city's own builder and materials, its floor
        /// at <paramref name="at"/>, on a plain square of pavement. Returns the root to destroy, its height and its size across.
        /// </summary>
        internal static GameObject BuildAlone(CityWorld world, CityStreamer streamer, int firstId, Vector3 at, out float height, out float size)
        {
            BuildingSite first = world.Building(firstId);
            var origin = new Vector3((float)first.CentreX - at.x, first.BaseHeight - at.y, (float)first.CentreZ - at.z);
            var structures = new MeshBuffer();
            var concrete = new MeshBuffer();
            var lit = new MeshBuffer();
            var dark = new MeshBuffer();
            float top = first.Top;
            float reach = Mathf.Max(first.Params.Width, first.Params.Depth);
            for (int id = firstId; id < world.BuildingCount; id++)
            {
                BuildingSite site = world.Building(id);
                if (id > firstId && !site.OnRoof && !site.Wing)
                {
                    break;
                }
                CityMeshBuilder.BuildSite(world, site, origin, structures, concrete, lit, dark, null, null, null, null, false);
                top = Mathf.Max(top, site.Top);
                float dx = (float)(site.CentreX - first.CentreX), dz = (float)(site.CentreZ - first.CentreZ);
                reach = Mathf.Max(reach, Mathf.Sqrt(dx * dx + dz * dz) * 2.0f + Mathf.Max(site.Params.Width, site.Params.Depth));
            }
            height = top - first.BaseHeight;
            size = Mathf.Max(reach, height * 0.55f);

            var root = new GameObject("Alone");
            AddMesh(root, structures, streamer.StructureMaterial);
            AddMesh(root, concrete, streamer.PavementMaterial);
            AddMesh(root, dark, streamer.DarkGlassMaterial);
            AddMesh(root, lit, streamer.LitGlassMaterial);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform, false);
            ground.transform.position = at + new Vector3(0.0f, -0.02f, 0.0f);
            ground.transform.localScale = Vector3.one * (reach * 0.4f);
            ground.GetComponent<Renderer>().sharedMaterial = streamer.PavementMaterial;
            return root;
        }

        private static void AddMesh(GameObject root, MeshBuffer buffer, Material material)
        {
            if (buffer.IsEmpty || material == null)
            {
                return;
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(buffer.Vertices);
            mesh.SetNormals(buffer.Normals);
            mesh.SetUVs(0, buffer.Uvs);
            mesh.SetColors(buffer.Colors);
            mesh.SetTriangles(buffer.Triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject("part");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>What shape the building whose first volume this is has, read off the volumes that follow it (see Massing.Compose), or null for a box.</summary>
        private static string KindOf(CityWorld world, BuildingSite first)
        {
            int shells = 0, wings = 0, sideWings = 0, centred = 0;
            bool turnedShell = false;
            for (int k = first.Id + 1; k < world.BuildingCount; k++)
            {
                BuildingSite v = world.Building(k);
                if (!v.OnRoof && !v.Wing) break;
                if (v.Wing)
                {
                    wings++;
                    if (!SameAxis(v, first)) sideWings++;
                }
                if (v.Shell)
                {
                    shells++;
                    if (!SameAxis(v, first)) turnedShell = true;
                    if (System.Math.Abs(v.CentreX - first.CentreX) < 0.01 && System.Math.Abs(v.CentreZ - first.CentreZ) < 0.01) centred++;
                }
            }
            if (first.Sides >= 3) return shells > 0 ? "round-crowned" : "round";
            if (sideWings > 0) return sideWings == 2 ? "ushape" : "lshape";
            if (wings > 0) return "staggered";
            if (shells >= 3 && centred == shells && turnedShell) return "twisted";
            if (shells == 1 && !turnedShell) return "cantilever";
            if (shells >= 2) return "wonky";
            return null;
        }

        private static bool SameAxis(BuildingSite a, BuildingSite b) =>
            System.Math.Abs(a.AxisX - b.AxisX) < 1e-6 && System.Math.Abs(a.AxisZ - b.AxisZ) < 1e-6;

        /// <summary>The main block of the building a wing belongs to: walk back while the ids are wings or volumes standing on something.</summary>
        private static int FirstOf(CityWorld world, BuildingSite wing)
        {
            int id = wing.Id;
            while (id > 0 && (world.Building(id).Wing || world.Building(id).OnRoof))
            {
                id--;
            }
            return id;
        }

        /// <summary>The top of the tallest volume standing (directly or not) on this one, or its own top.</summary>
        private static float TallestOver(CityWorld world, BuildingSite site)
        {
            float top = site.Top;
            for (int k = site.Id + 1; k < world.BuildingCount; k++)
            {
                BuildingSite next = world.Building(k);
                if (!next.OnRoof && !next.Wing)
                {
                    break;
                }
                top = Mathf.Max(top, next.Top);
            }
            return top;
        }

        /// <summary>Where every capture's pictures go: Logs/CityShots (made if missing).</summary>
        internal static string ShotsFolder
        {
            get
            {
                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        internal static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int w = 1280, h = 800;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])enc.Invoke(null, new object[] { image }));
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(image);
        }
    }
}
