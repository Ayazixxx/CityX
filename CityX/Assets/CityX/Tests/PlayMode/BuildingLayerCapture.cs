using System.Collections;
using System.IO;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>Not a test: renders a podium + tower building from outside (several heights and distances) to Logs/CityShots/layer-*.png. Explicit; needs -Graphics.</summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BuildingLayerCapture
    {
        private const int Seed = 48398181;

        [UnityTest]
        public IEnumerator RenderAPodiumAndTower()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var world = new CityWorld(Seed);
            BuildingSite podium = world.Building(2), tower = world.Building(3);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CityX/Scenes/CityMassing.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return null; yield return null;
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            Assert.AreEqual(Seed, streamer.WorldSeed, "the scene's streamer uses a different world seed than this capture");
            Vector3 centre = TestSites.At(podium, podium.CentreX, podium.BaseHeight, podium.CentreZ);
            streamer.StreamAround(centre, int.MaxValue);
            yield return null; yield return null;
            Physics.SyncTransforms();

            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 1500; camera.fieldOfView = 60;
            Vector3[] outward = new Vector3[4];
            WindowGrid.Wall[] walls = { WindowGrid.Wall.North, WindowGrid.Wall.South, WindowGrid.Wall.East, WindowGrid.Wall.West };
            for (int w = 0; w < 4; w++) outward[w] = CityMeshBuilder.WallNormal(podium, walls[w]);
            int shot = 0;
            for (int w = 0; w < 2; w++)
            {
                Vector3 wallPoint = centre + outward[w] * (float)(w < 2 ? podium.Params.Depth * 0.5 : podium.Params.Width * 0.5);
                foreach (float y in new[] { podium.Top - 4.0f, podium.Top + 3.0f })
                {
                    Vector3 pos = new Vector3(wallPoint.x, y, wallPoint.z) + outward[w] * 7.0f;
                    Vector3 look = new Vector3(wallPoint.x, y, wallPoint.z);
                    Shoot(camera, directory, $"layer-{shot++}", pos, look);
                }
                Vector3 far = new Vector3(wallPoint.x, podium.Top, wallPoint.z) + outward[w] * 70.0f;
                Shoot(camera, directory, $"layer-{shot++}", far, new Vector3(centre.x, podium.Top + 5.0f, centre.z));
            }
            Debug.Log($"CityX layer capture: podium #{podium.Id} top {podium.Top:F1}, tower #{tower.Id} base {tower.BaseHeight:F1}, tower {tower.Params.Width:F0}x{tower.Params.Depth:F0} on podium {podium.Params.Width:F0}x{podium.Params.Depth:F0}");
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int w = 1280, h = 800;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0); image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])enc.Invoke(null, new object[] { image }));
            camera.targetTexture = null; RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
        }
    }
}
