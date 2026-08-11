using System.IO;
using CityX.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Renders the built title screen to a PNG.
    ///
    /// A title screen is entirely a matter of whether it looks right, and the
    /// only two ways to find that out are to open Unity and press play, or to
    /// render it. This is the second one.
    ///
    /// Needs a real graphics device, so it must run WITHOUT -nographics.
    /// </summary>
    public static class TitleScreenCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("CityX/Capture Title Screenshot")]
        public static void Capture()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            EditorSceneManager.OpenScene("Assets/CityX/Scenes/Title.unity", OpenSceneMode.Single);

            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("CityX: the title scene has no main camera.");
                return;
            }

            // Rebuild only RESETS the streamer -- it clears what is loaded and
            // makes a fresh world. The chunks themselves are built by Update,
            // which never runs outside play mode, so the first version of this
            // capture rendered a completely empty sky and looked like the
            // backdrop was broken. StreamAround is what actually builds, and
            // the budget has to cover the whole radius in one call because
            // there is no second frame to finish in.
            var streamer = Object.FindFirstObjectByType<View.CityStreamer>();
            if (streamer != null)
            {
                streamer.Rebuild();
                int span = streamer.RadiusChunks * 2 + 1;
                streamer.StreamAround(camera.transform.position, span * span);
            }

            // THE SKY AND THE LIT WINDOWS ARE NOT PART OF THE SCENE FILE. They
            // are RenderSettings and a shared material's emission colour, both
            // written by DayNightCycle.Update -- which, like the streamer's,
            // never runs outside play mode. So the shot came out with whatever
            // the last editor tool to touch them happened to leave behind:
            // once bright pink windows, and after a daytime city capture, no
            // lit windows at all on a night skyline. Neither was ever what the
            // game does; both were the capture rendering stale state.
            var cycle = Object.FindFirstObjectByType<View.DayNightCycle>();
            if (cycle != null)
            {
                cycle.Apply(View.SkyCycle.At(cycle.TimeOfDay));
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                // Overlay canvases are composited to the display and never
                // appear in a camera render, so the interface is pointed at the
                // capture camera for the shot. The saved scene keeps overlay.
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1.0f;
                }

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                };
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                string path = Path.Combine(outputDirectory, "title.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);

                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
            }
        }
    }
}
