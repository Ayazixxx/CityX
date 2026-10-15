using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player HUD's unit frame as the game shows it (owner, 2026-10-14: "zoom in the player hud portrait ... just the head and a
    /// small glimpse of the shoulders ... make it look like the world of warcraft version", with a mana bar), rendered from the real HUD prefab
    /// driving itself from a real player with hair and a name: the whole screen and the frame close up, with mana and without it, over a dark
    /// and a bright background. Written to Logs/CityShots/hud-frame-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class PlayerHudLookCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest]
        public IEnumerator RenderTheUnitFrame()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            int layer = LayerMask.NameToLayer("PlayerPortrait");
            Assert.GreaterOrEqual(layer, 0, "no PlayerPortrait layer");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);

            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            Transform headBone = null;
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "head") { headBone = child; break; }
            }
            var hair = player.GetComponent<CharacterHair>() ?? player.AddComponent<CharacterHair>();
            hair.Head = headBone;
            hair.StyleMeshes = new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset") };
            hair.HairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            var save = CharacterSave.Create("Ayazi", new Color(0.95f, 0.78f, 0.6f));
            save.HairStyle = "hair1";
            save.HairColour = new Color(0.35f, 0.18f, 0.08f);
            player.GetComponent<CharacterIdentity>().Apply(save);

            // The portrait camera, as PlayerHudBuilder.AddToScene builds it.
            var cameraObject = new GameObject("PortraitCamera");
            cameraObject.transform.SetParent(player.transform, false);
            Camera portraitCamera = cameraObject.AddComponent<Camera>();
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = PlayerHud.PortraitBackgroundColour;
            portraitCamera.cullingMask = 1 << layer;
            portraitCamera.nearClipPlane = 0.05f;
            portraitCamera.farClipPlane = 5.0f;
            portraitCamera.fieldOfView = 35.0f;
            var portraitTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "PortraitTexture" };
            portraitCamera.targetTexture = portraitTexture;
            var portraitLight = new GameObject("PortraitLight").AddComponent<Light>();
            portraitLight.transform.SetParent(cameraObject.transform, false);
            portraitLight.type = LightType.Point;
            portraitLight.intensity = 3.0f;
            portraitLight.range = 3.0f;
            portraitLight.cullingMask = 1 << layer;
            cameraObject.SetActive(false);

            var hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/UI/PlayerHud.prefab");
            GameObject hudObject = Object.Instantiate(hudPrefab);
            var hud = hudObject.GetComponent<PlayerHud>();
            hud.Target = player.GetComponent<PlayerVitals>();
            hud.PortraitCamera = portraitCamera;
            hud.PortraitImage.texture = portraitTexture;
            hud.ShowName();

            var vitals = player.GetComponent<PlayerVitals>();
            yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(hud.CaptureNow(), "the portrait could not be taken");
            vitals.TakeDamage(28.0f, false);
            vitals.TickStamina(true, 2.0f);
            vitals.TrySpendMana(35.0f);
            yield return null;
            yield return null;

            var shotCamera = new GameObject("HudShotCamera").AddComponent<Camera>();
            shotCamera.clearFlags = CameraClearFlags.SolidColor;
            shotCamera.cullingMask = 1 << hudObject.layer;   // only the HUD: a camera-space canvas is drawn by a camera that sees its layer
            var canvas = hudObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = shotCamera;
            canvas.planeDistance = 1.0f;
            yield return null;

            Shoot(shotCamera, directory, "hud-frame-dark", new Color(0.2f, 0.22f, 0.25f));
            Shoot(shotCamera, directory, "hud-frame-bright", new Color(0.62f, 0.7f, 0.8f));
            vitals.ManaUnlocked = false;
            yield return null;
            yield return null;
            Shoot(shotCamera, directory, "hud-frame-nomana", new Color(0.2f, 0.22f, 0.25f));
            Debug.Log($"CityX HUD frame: name '{hud.NameLabel.text}', health {hud.HealthText.text}, mana {hud.ManaText.text}, mana bar shown {hud.ManaBar.activeSelf}");

            Object.Destroy(hudObject);
            Object.Destroy(shotCamera.gameObject);
            Object.Destroy(player);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
            portraitTexture.Release();
            Object.Destroy(portraitTexture);
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string directory, string name, Color background)
        {
            camera.backgroundColor = background;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Save(image, Path.Combine(directory, name + ".png"));

            // The frame close up: the top-left corner, three times over (each pixel a 3 x 3 block, nothing smoothed).
            const int cropWidth = 420, cropHeight = 160, scale = 3;
            var close = new Texture2D(cropWidth * scale, cropHeight * scale, TextureFormat.RGB24, false);
            for (int y = 0; y < cropHeight * scale; y++)
            {
                for (int x = 0; x < cropWidth * scale; x++)
                {
                    close.SetPixel(x, y, image.GetPixel(x / scale, Height - cropHeight + y / scale));
                }
            }
            close.Apply();
            Save(close, Path.Combine(directory, name + "-close.png"));
            Object.Destroy(image);
            Object.Destroy(close);
        }

        private static void Save(Texture2D image, string path)
        {
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            System.Reflection.MethodInfo encode = conversion?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            if (encode?.Invoke(null, new object[] { image }) is byte[] bytes)
            {
                File.WriteAllBytes(path, bytes);
            }
        }
    }
}
