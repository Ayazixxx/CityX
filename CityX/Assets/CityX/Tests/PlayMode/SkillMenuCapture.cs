using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the skill menu and the HUD's bar as the game draws them (owner, 2026-10-14), for looking at. A character part way along
    /// (vaulting, Dash, Climb, the fireball, the sword spin and poison learnt, two points to spend, Climb chosen): the menu where it opens, at the
    /// hybrid between fire and water, shown whole, and close on Freerunner; every skill's icon large; the bar in the HUD's corner. Written to
    /// Logs/CityShots/skills-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SkillMenuCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest]
        public IEnumerator RenderTheSkillMenuAndTheBar()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);

            var body = new GameObject("Body");
            PlayerVitals vitals = body.AddComponent<PlayerVitals>();
            PlayerSkills skills = PlayerSkills.On(body);
            skills.Load(CharacterSave.Create("Shots", Color.white));
            skills.SetPoints(8);
            foreach (string id in new[] { SkillCatalog.Dash, SkillCatalog.Climb, SkillCatalog.Fireball, SkillCatalog.SwordSpin,
                         SkillCatalog.PoisonBreath, SkillCatalog.Bloodrage })
            {
                skills.TryLearn(id);
            }
            PlayerSkills.Local = skills;

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var shotCamera = new GameObject("ShotCamera").AddComponent<Camera>();
            shotCamera.clearFlags = CameraClearFlags.SolidColor;
            shotCamera.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
            shotCamera.targetTexture = target;

            // --- the menu ---
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            Canvas canvas = menu.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = shotCamera;
            canvas.planeDistance = 1.0f;
            shotCamera.cullingMask = 1 << canvas.gameObject.layer;
            menu.Select(SkillCatalog.Climb);
            yield return null;
            yield return null;
            Shoot(shotCamera, target, directory, "skills-menu-start", null);

            menu.Select(SkillCatalog.Fireball);
            menu.JumpTree(1);
            menu.JumpTree(1);
            menu.JumpTree(1);
            yield return null;
            Shoot(shotCamera, target, directory, "skills-menu-hybrid", null);

            menu.Select(null);
            menu.ShowAll();
            yield return null;
            Shoot(shotCamera, target, directory, "skills-menu-all", null);

            menu.Select(SkillCatalog.SuperJump);
            menu.ZoomBy(1.0f / menu.Zoom, Vector2.zero);
            for (int i = 0; i < 4 && menu.Layout.TreeAt(menu.Scroll) != "freerunner"; i++)
            {
                menu.JumpTree(-1);
            }
            menu.ZoomBy(1.5f, Vector2.zero);
            yield return null;
            Shoot(shotCamera, target, directory, "skills-menu-close", null);
            menu.Close();

            // --- every icon, large ---
            var sheetObject = new GameObject("Icons", typeof(RectTransform), typeof(Canvas));
            var sheet = sheetObject.GetComponent<Canvas>();
            sheet.renderMode = RenderMode.ScreenSpaceCamera;
            sheet.worldCamera = shotCamera;
            sheet.planeDistance = 1.0f;
            sheetObject.AddComponent<CanvasScaler>().referenceResolution = new Vector2(Width, Height);
            int index = 0;
            foreach (SkillNode node in SkillCatalog.RealSkills())
            {
                var face = new GameObject(node.Id, typeof(RectTransform)).AddComponent<Image>();
                face.transform.SetParent(sheetObject.transform, false);
                face.sprite = HudArt.Disc;
                face.color = HotbarView.FaceColour;
                var rect = face.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.0f, 1.0f);
                rect.sizeDelta = new Vector2(150.0f, 150.0f);
                rect.anchoredPosition = new Vector2(110.0f + (index % 7) * 175.0f, -120.0f - (index / 7) * 200.0f);
                var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
                icon.transform.SetParent(face.transform, false);
                icon.sprite = SkillIcons.For(node.Id);
                icon.color = HotbarView.IconColour;
                icon.rectTransform.sizeDelta = new Vector2(100.0f, 100.0f);
                var label = new GameObject("Name", typeof(RectTransform)).AddComponent<Text>();
                label.transform.SetParent(face.transform, false);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 18;
                label.alignment = TextAnchor.UpperCenter;
                label.color = Color.white;
                label.text = node.Name;
                label.rectTransform.anchoredPosition = new Vector2(0.0f, -95.0f);
                label.rectTransform.sizeDelta = new Vector2(170.0f, 24.0f);
                index++;
            }
            shotCamera.cullingMask = 1 << sheetObject.layer;
            yield return null;
            Shoot(shotCamera, target, directory, "skills-icons", null);
            Object.Destroy(sheetObject);

            // --- the HUD's bar, in its corner ---
            var hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/UI/PlayerHud.prefab");
            GameObject hudObject = Object.Instantiate(hudPrefab);
            hudObject.GetComponent<PlayerHud>().Target = vitals;
            var hudCanvas = hudObject.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hudCanvas.worldCamera = shotCamera;
            hudCanvas.planeDistance = 1.0f;
            shotCamera.cullingMask = 1 << hudObject.layer;
            skills.PutOnBar(9, SkillCatalog.Bloodrage);
            yield return null;
            yield return null;
            Shoot(shotCamera, target, directory, "skills-hud", new RectInt(0, 0, 560, 110));
            Object.Destroy(hudObject);

            Object.Destroy(shotCamera.gameObject);
            Object.Destroy(body);
            target.Release();
            Object.Destroy(target);
#else
            yield break;
#endif
        }

        /// <summary>Renders the camera and writes the picture; with <paramref name="crop"/> (pixels from the bottom left) a close-up of that part, twice the size.</summary>
        private static void Shoot(Camera camera, RenderTexture target, string directory, string name, RectInt? crop)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Write(directory, name, image);
            if (crop.HasValue)
            {
                RectInt r = crop.Value;
                var close = new Texture2D(r.width * 2, r.height * 2, TextureFormat.RGB24, false);
                for (int y = 0; y < r.height * 2; y++)
                {
                    for (int x = 0; x < r.width * 2; x++)
                    {
                        close.SetPixel(x, y, image.GetPixel(r.x + x / 2, r.y + y / 2));
                    }
                }
                close.Apply();
                Write(directory, name + "-close", close);
                Object.Destroy(close);
            }
            Object.Destroy(image);
        }

        private static void Write(string directory, string name, Texture2D image)
        {
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
        }
    }
}
