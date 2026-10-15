using System.IO;
using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Renders the HUD to a PNG so its layout can actually be looked at.
    ///
    /// Every test around the HUD checks that things are wired and that numbers
    /// move. None of them can answer whether the bars are legible, whether the
    /// level badge collides with the health bar, or whether YOU DIED is
    /// centred -- and those are the only questions a layout really has.
    ///
    /// A screen-space-OVERLAY canvas is composited straight to the display and
    /// never appears in a camera's render, so this temporarily switches the
    /// instance to screen-space-CAMERA and points it at the capture camera.
    /// The prefab on disk is untouched.
    ///
    /// Needs a real graphics device, so it must run WITHOUT -nographics:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.UIEditor.PlayerHudCapture.Capture
    /// </summary>
    public static class PlayerHudCapture
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const string PrefabPath = "Assets/CityX/UI/PlayerHud.prefab";

        /// <summary>
        /// Renders exactly what the portrait camera would actually frame --
        /// PlayerHud.CapturePortrait is a coroutine (needs a running Update
        /// loop, which a plain batch-mode editor call has none of), so this
        /// replicates its own positioning directly against a real
        /// Player.prefab instance rather than trying to pump it.
        /// </summary>
        [MenuItem("CityX/Capture Portrait Framing")]
        public static void CapturePortraitFraming()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: no Player.prefab -- run CityX/Rebuild Player Rig first.");
                return;
            }

            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("PortraitFramingRig");
            RenderTexture target = null;
            try
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = Vector3.zero;
                player.transform.rotation = Quaternion.identity;

                Renderer headRenderer = null;
                foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.gameObject.name == "Head")
                    {
                        headRenderer = renderer;
                        break;
                    }
                }
                if (headRenderer == null)
                {
                    Debug.LogError("CityX: no 'Head' renderer found on the player.");
                    return;
                }

                var lightObject = new GameObject("Light");
                lightObject.transform.SetParent(root.transform);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(35.0f, 30.0f, 0.0f);

                var cameraObject = new GameObject("PortraitCamera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = PlayerHud.PortraitBackgroundColour;
                camera.fieldOfView = 35.0f;
                camera.nearClipPlane = 0.05f;
                // The same framing the game uses (head, hair, chest), not a copy of it.
                PlayerHud.Frame(camera, headRenderer.bounds, headRenderer.bounds.max.y, player.transform.forward);

                target = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                image.Apply();
                string path = Path.Combine(outputDirectory, "portrait-framing.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        [MenuItem("CityX/Capture HUD Screenshot")]
        public static void Capture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"CityX: no HUD at {PrefabPath} -- run CityX/Rebuild Player HUD.");
                return;
            }

            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            // Alive at 45% health, so the bar shows a real fraction and the
            // yellow band rather than a full green rectangle that would look
            // identical whether or not anything works.
            Shoot(prefab, outputDirectory, "hud_alive", 0.45f, 0.7f, false);
            Shoot(prefab, outputDirectory, "hud_dead", 0.0f, 0.3f, true);
        }

        private static void Shoot(GameObject prefab, string outputDirectory, string name,
            float healthFraction, float staminaFraction, bool dead)
        {
            var cameraObject = new GameObject("HudCaptureCamera");
            GameObject instance = null;
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                // A flat mid-grey stands in for the city behind the HUD. Black
                // would flatter white text unfairly and hide anything dark.
                camera.backgroundColor = new Color(0.34f, 0.36f, 0.39f);

                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                var canvas = instance.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1.0f;

                var hud = instance.GetComponent<PlayerHud>();

                // Posed by hand. Nothing calls Awake or Update outside play
                // mode, so the HUD cannot drive itself here -- and driving it
                // from a real PlayerVitals would need play mode and a frame,
                // which is what the PlayMode tests already cover.
                SetFill(hud.HealthFill, healthFraction);
                SetFill(hud.StaminaFill, staminaFraction);
                if (hud.ManaFill != null) SetFill(hud.ManaFill, 0.65f);
                HudArt.Paint(hud);   // the frame's art is drawn at run time (Awake), which nothing runs here
                hud.HealthFillImage.color = PlayerHud.ColourForHealth(healthFraction);
                hud.DeathScreen.SetActive(dead);
                hud.Crosshair.SetActive(!dead);

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

                string path = Path.Combine(outputDirectory, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);

                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void SetFill(RectTransform fill, float fraction)
        {
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(fraction, 1.0f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}
