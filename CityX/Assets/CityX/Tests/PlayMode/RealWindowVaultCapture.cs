using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the real player diving through shattered windows on the real facades near spawn (2026-10-14), rendered to
    /// Logs/CityShots/vault-real-*.png. One whose room stands higher than the street (the kind that never vaulted before), run at from a quarter
    /// of its width off its middle and 15 degrees off square; one more run at straight. From along the wall, from behind, and from inside once
    /// landed. The windows are found with a bare streamer (the same search as RealCityWindowVaultPlayTests); the city scene is loaded after,
    /// for its sky, light and materials, with its streamer told to look where the dive is. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RealWindowVaultCapture
    {
        private const int Seed = 19077151;
        private readonly List<GameObject> _made = new List<GameObject>();
        private CityStreamer _streamer;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

#if UNITY_EDITOR
        /// <summary>Waits, by the clock, until the streamer has nothing left to put on screen and every window change has landed.</summary>
        private IEnumerator Settle()
        {
            float until = Time.realtimeSinceStartup + 20.0f;
            int quiet = 0;
            while (Time.realtimeSinceStartup < until && quiet < 10)
            {
                quiet = _streamer.IsStreaming || _streamer.WindowRefreshPending ? 0 : quiet + 1;
                yield return null;
            }
            Physics.SyncTransforms();
        }

        private IEnumerator Stream(Vector3 at)
        {
            _streamer.StreamAround(at, int.MaxValue);
            yield return null;
            yield return null;
            yield return Settle();
        }
#endif

        [UnityTest]
        public IEnumerator RenderTheRealWindowDive()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(directory);

            // 1. The windows: a raised room (a punched window 1.3 to 1.9 m over the street) and an ordinary one, both with a clear run-up.
            var bare = new GameObject("BareStreamer");
            bare.SetActive(false);
            _streamer = bare.AddComponent<CityStreamer>();
            _streamer.WorldSeed = Seed;
            _streamer.RadiusChunks = 2;
            _streamer.GeneratorThreads = 0;
            _streamer.SpawnPreStreamBudget = 0;
            bare.SetActive(true);
            var world = new CityWorld(Seed);
            RealCityWindowVaultPlayTests.Candidate raised = null, ordinary = null;
            int buildingsTried = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.OnRoof || site.Params.IsSpecial || site.IsOpenGround || site.Sides >= 3) continue;
                if (buildingsTried++ >= 30 || (raised != null && ordinary != null)) break;
                foreach (WindowGrid.Wall wall in new[] { WindowGrid.Wall.East, WindowGrid.Wall.West, WindowGrid.Wall.South })
                {
                    var panes = new List<RealCityWindowVaultPlayTests.Candidate>(RealCityWindowVaultPlayTests.PanesOf(world, site, wall));
                    if (panes.Count == 0) continue;
                    yield return Stream(panes[0].Outside);
                    foreach (RealCityWindowVaultPlayTests.Candidate c in panes)
                    {
                        if (!RealCityWindowVaultPlayTests.GroundAt(c.Outside, out float ground)) continue;
                        c.SillOverGround = c.SillAboveGround + c.Site.BaseHeight - ground;
                        c.Outside.y = ground;
                        if (!RealCityWindowVaultPlayTests.ApproachClear(c) || c.Opening < VaultProbe.MinHeadroomMetres + 0.1f) continue;
                        if (raised == null && c.SillOverGround >= 1.3f && c.SillOverGround <= 1.9f && c.Opening < 2.1f) raised = c;
                        else if (ordinary == null && c.SillOverGround >= 0.8f && c.SillOverGround < 1.2f) ordinary = c;
                    }
                    if (raised != null && ordinary != null) break;
                }
            }
            Assert.IsNotNull(raised, "no window into a raised room near spawn");
            Assert.IsNotNull(ordinary, "no ordinary sill-high window near spawn");
            Object.Destroy(bare);
            yield return null;

            // 2. The city scene for its look; its own player stays out of the way and its streamer looks where the dive is.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/CityX/Scenes/CityMassing.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _streamer = Object.FindFirstObjectByType<CityStreamer>();
            Assert.AreEqual(Seed, _streamer.WorldSeed, "the scene's world is not the one these windows were found in");
            foreach (PlayerInputReader reader in Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
            {
                reader.gameObject.SetActive(false);
            }
            var viewer = new GameObject("DiveViewer");
            _made.Add(viewer);
            _streamer.Viewer = viewer.transform;
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }
            var cameraObject = new GameObject("VaultCam");
            _made.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            const float dt = 1.0f / 60.0f;

            // 3. Each dive, frame by frame.
            foreach ((RealCityWindowVaultPlayTests.Candidate c, string label, float side, float turn) in new[] { (raised, "raised", 0.25f, 15.0f), (ordinary, "ordinary", 0.0f, 0.0f) })
            {
                viewer.transform.position = c.Outside;
                yield return Stream(c.Outside);
                _streamer.Damage.Hit(c.Key, shatterOutright: true);
                yield return Settle();
                Vector3 across = Vector3.Cross(Vector3.up, c.Inward).normalized;
                Vector3 feet = c.Outside - c.Inward * 0.3f + across * (side * c.Width);   // a metre out from the wall
                RealCityWindowVaultPlayTests.GroundAt(feet, out float y);
                feet.y = y;
                Vector3 run = Quaternion.AngleAxis(turn, Vector3.up) * c.Inward;
                GameObject player = Object.Instantiate(prefab, feet + Vector3.up * 0.05f, Quaternion.LookRotation(run, Vector3.up));
                _made.Add(player);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var vaulting = player.GetComponent<PlayerVaulting>();
                yield return null;
                yield return null;
                Assert.IsTrue(vaulting.TryStart(true, true, true, 5.0f, dt, run),
                    $"the {label} window (building {c.Site.Id} {c.Wall} column {c.Column}, sill {c.SillOverGround:0.00} m over the street) was not vaultable");
                Vector3 wallFace = c.Outside + c.Inward * 0.7f;
                float sillY = c.Site.BaseHeight + c.SillAboveGround;
                var window = new Vector3(wallFace.x, sillY + c.Opening * 0.5f, wallFace.z);
                Vector3 alongWall = window - c.Inward * 2.6f + across * 4.2f + Vector3.up * 0.3f;
                Vector3 behind = feet - run * 3.4f + Vector3.up * 3.1f;
                int shot = 0;
                float nextShotAt = 0.0f;
                for (int i = 0; i < 300 && vaulting.IsVaulting; i++)
                {
                    vaulting.Tick(dt);
                    if (vaulting.Progress >= nextShotAt && shot < 7)
                    {
                        Shoot(camera, directory, $"vault-real-{label}-{shot}-side", alongWall, player.transform.position + Vector3.up * 1.2f);
                        Shoot(camera, directory, $"vault-real-{label}-{shot}-behind", behind, window);
                        shot++;
                        nextShotAt += 0.16f;
                    }
                    yield return null;
                }
                Assert.IsFalse(vaulting.IsVaulting, "the vault never finished");
                for (int i = 0; i < 20; i++) yield return null;
                float inside = Vector3.Dot(player.transform.position - wallFace, c.Inward);
                Shoot(camera, directory, $"vault-real-{label}-landed-inside", window + c.Inward * 4.5f + across * 1.2f + Vector3.up * 0.2f,
                    player.transform.position + Vector3.up * 1.2f);
                Debug.Log($"CityX real window dive ({label}): building {c.Site.Id} {c.Wall} column {c.Column}, sill {c.SillOverGround:0.00} m over the street, " +
                    $"opening {c.Opening:0.00} m; landed {inside:0.00} m inside the wall face at y {player.transform.position.y:0.00} (street {feet.y:0.00}, room floor {c.Site.BaseHeight:0.00})");
                Assert.Greater(inside, 0.3f, $"the {label} dive did not end inside the building");
                Object.Destroy(player);
                yield return null;
            }
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int w = 1024, h = 640;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(image);
        }
#endif
    }
}
