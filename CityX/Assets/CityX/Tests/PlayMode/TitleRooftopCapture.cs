using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Core;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the title as it plays (owner, 2026-11-11: the 3D logo top left with its neon flickering, the menu under it, the last played
    /// character on a rooftop on the right), rendered with the interface to Logs/CityShots/title-*.png at 16:9: the menu, a word pointed at,
    /// the neon lit and faltering, and the character close. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class TitleRooftopCapture
    {
        private const int Width = 1600;
        private const int Height = 900;

        private string _directory;
        private Scene _title;
        private World.Plan.CityScale _scaleWas = World.Plan.CityScale.Active;

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            if (_title.IsValid() && _title.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_title);
            }
            LogAssert.ignoreFailingMessages = false;
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            World.Plan.CityScale.Active = _scaleWas;
            if (_directory != null && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator RenderTheTitle()
        {
            CaptureGuard.NeedsGraphics();
            // The city the game plays, not the tests' classic one: the title once found no roof in it and showed a wall.
            _scaleWas = World.Plan.CityScale.Active;
            World.Plan.CityScale.Active = World.Plan.CityScale.Default;
            Time.captureFramerate = 60;
            _directory = Path.Combine(Path.GetTempPath(), "CityXShot", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            CharacterStore.DirectoryOverride = _directory;
            CharacterSave character = CharacterSave.Create("Ayaz", new Color(0.55f, 0.40f, 0.32f));
            character.HairStyle = Character.CharacterHair.KickHair;
            character.HairColour = new Color(0.12f, 0.09f, 0.08f);
            character.Wearing = new List<string> { ItemRegistry.RedCamoTShirt, ItemRegistry.BlackJeans, ItemRegistry.WhiteSneaker };
            character.PrimaryWeapon = ItemRegistry.CombatSword;
            character.LastPlayedUnixSeconds = 3000;
            LocalServer.Database.For(PlayerKey.Local).Add(character);

            yield return CharacterCreatorPlayTests.LoadTitle();
            _title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            var title = Object.FindFirstObjectByType<TitleScreen>();
            title.Show(TitleScreen.Panel.Menu);
            var logo = Object.FindFirstObjectByType<TitleLogo>();
            yield return Frames(120);
            Assert.IsNotNull(Object.FindFirstObjectByType<TitleHero>().Showing, "nobody stands on the roof");

            // Lit, then faltering.
            yield return Until(() => logo.Brightness > 0.97f);
            Shoot("title-menu", logo);
            ButtonLook word = GameObject.Find("SingleplayerButton").GetComponent<ButtonLook>();
            word.OnPointerEnter(null);
            // The buttons go by real time, not the pictures' frames: waited for till embers are well up off the word.
            EmberGraphic embers = word.GetComponent<DrawnFace>().Embers;
            yield return Until(() => embers.Count > 40);
            Shoot("title-pointed-at", logo);
            word.OnPointerExit(null);
            yield return Until(() => logo.Brightness < 0.25f);
            Shoot("title-neon-faltering", logo);

            // The feet close, through the idle: they should not move (owner, 2026-11-11: "the bottom of the feet should be glued to the floor").
            Transform body = Object.FindFirstObjectByType<TitleHero>().Body.transform;
            Camera eye = Camera.main;
            Vector3 place = eye.transform.position;
            Quaternion turn = eye.transform.rotation;
            Vector3 toEye = Vector3.ProjectOnPlane(place - body.position, Vector3.up).normalized;
            for (int i = 0; i < 4; i++)
            {
                BuildingShapeCapture.Shoot(eye, BuildingShapeCapture.ShotsFolder, $"title-feet-{i}", body.position + toEye * 2.2f + Vector3.up * 0.6f,
                    body.position + Vector3.up * 0.15f);
                eye.transform.SetPositionAndRotation(place, turn);
                yield return Frames(45);
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Until(System.Func<bool> done)
        {
            for (int i = 0; i < 60 * 20 && !done(); i++)
            {
                yield return null;
            }
        }

        /// <summary>The title camera at 16:9, the logo held to that shape first, with the interface drawn into the picture.</summary>
        private static void Shoot(string name, TitleLogo logo)
        {
            Camera camera = Camera.main;
            Canvas canvas = Object.FindFirstObjectByType<TitleScreen>().GetComponent<Canvas>();
            RenderMode modeWas = canvas.renderMode;
            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 1;
            camera.targetTexture = target;
            camera.aspect = (float)Width / Height;
            logo.Hold();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1.0f;
            Canvas.ForceUpdateCanvases();
            // The city's haze is a full-screen pass: on the canvas brought into the camera for the picture it lay over the menu, which in
            // the game draws over everything.
            View.CityHaze.Clear();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            string folder = BuildingShapeCapture.ShotsFolder;
            Directory.CreateDirectory(folder);
            CapturePng.Write(image, Path.Combine(folder, name + ".png"));
            Object.Destroy(image);
            RenderTexture.active = null;
            camera.targetTexture = null;
            camera.ResetAspect();
            RenderTexture.ReleaseTemporary(target);
            canvas.renderMode = modeWas;
        }
    }
}
