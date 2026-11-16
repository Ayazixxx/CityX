using System.Collections;
using System.Collections.Generic;
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
    /// Not a test: renders the character list on the conveyor belt (docs/CHARACTER_SELECT.md) with five characters made, to
    /// Logs/CityShots/select-NN-*.png: the row, rolling to the next, set down, the card closed in on, CREATE NEW bringing a blank body in behind
    /// the row, and the creator's tools up. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CharacterSelectCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CharacterSelectCapture
    {
        private Scene _title;
        private ICharacterSource _sourceWas;
        private bool _firstStartWas;
        private BeltIntro _belt;
        private CharacterSelect _select;
        private int _shot;

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            CharacterSources.Current = _sourceWas;
            TitleScreen.BeltOnFirstStart = _firstStartWas;
            if (_title.IsValid() && _title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_title);
            }
        }

        [UnityTest]
        public IEnumerator RenderTheCharacterListOnTheBelt()
        {
            CaptureGuard.NeedsGraphics();
            Time.captureFramerate = 60;
            _sourceWas = CharacterSources.Current;
            _firstStartWas = TitleScreen.BeltOnFirstStart;
            TitleScreen.BeltOnFirstStart = false;
            var server = new BeltIntroPlayTests.Server();
            string[] names = { "Happy", "Ayaz", "Wraith Food", "Glock Guy", "Khaki" };
            Color[] skins =
            {
                new Color(0.95f, 0.85f, 0.1f), new Color(0.35f, 0.25f, 0.2f), new Color(0.78f, 0.62f, 0.53f),
                new Color(0.55f, 0.4f, 0.32f), new Color(0.86f, 0.76f, 0.68f),
            };
            for (int i = 0; i < names.Length; i++)
            {
                CharacterSave save = CharacterSave.Create(names[i], skins[i]);
                save.Level = 1 + i * 3;
                save.PlaytimeSeconds = 600 + i * 2400;
                save.Wearing = new List<string> { ItemRegistry.TShirt, i % 2 == 0 ? ItemRegistry.Khakis : ItemRegistry.GreyJeans, ItemRegistry.WhiteSneaker };
                server.Characters.Add(save);
            }
            CharacterSources.Current = server;
            yield return CharacterCreatorPlayTests.LoadTitle();
            _title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            var title = Object.FindFirstObjectByType<TitleScreen>();
            _belt = title.CreatorPanel.GetComponent<BeltIntro>();
            _select = title.Select;
            View.CityHaze.Clear();
            // The picture's shape, not the batch run's screen's: the row is spaced for the screen it is seen on (CharacterSelect.RowSpacing).
            _belt.MakingCamera.aspect = (float)BuildingShapeCapture.ShotWidth / BuildingShapeCapture.ShotHeight;

            title.Show(TitleScreen.Panel.CharacterSelect);
            yield return Frames(40);   // the row made, its feet set down, the name up
            Shoot("row");
            _select.Next();
            yield return Frames(18);
            Shoot("rolling");
            yield return Until(() => _select.Line.Settled);
            yield return Frames(15);
            Shoot("next-one");
            _select.Inspect();
            yield return Frames(40);
            Shoot("card");
            _select.CreateNew();
            yield return Until(() => title.Showing == TitleScreen.Panel.Creator);
            yield return Frames(30);
            Shoot("create-new-rolling");
            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Making);
            Shoot("new-body-in");
            yield return Frames(50);
            Shoot("tools-up");
        }

        /// <summary>
        /// The row with hair on every character and the middle one in the Midnight Coat, shot every 0.4 s for 3 s (owner, 2026-11-15: "on the
        /// character select screen the hair is disappearing every couple seconds"), then the card and its pencil, and the redraw creator it opens
        /// (owner: "add a pencil icon next to the trash icon to redraw"), to Logs/CityShots/select-hair-*.png and select-redraw-*.png.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheHairOverTimeAndThePencilsRedraw()
        {
            CaptureGuard.NeedsGraphics();
            Time.captureFramerate = 60;
            _sourceWas = CharacterSources.Current;
            _firstStartWas = TitleScreen.BeltOnFirstStart;
            TitleScreen.BeltOnFirstStart = false;
            var server = new BeltIntroPlayTests.Server();
            string[] styles = { CityX.Character.CharacterHair.KickHair, CityX.Character.CharacterHair.KickHair, CityX.Character.CharacterHair.Dreads };
            for (int i = 0; i < styles.Length; i++)
            {
                CharacterSave save = CharacterSave.Create("Hair" + i, new Color(0.78f, 0.62f, 0.53f));
                save.HairStyle = styles[i];
                save.HairColour = new Color(0.15f, 0.1f, 0.08f);
                save.Wearing = new List<string> { i == 1 ? ItemRegistry.MidnightCoat : ItemRegistry.TShirt, ItemRegistry.GreyJeans, ItemRegistry.WhiteSneaker };
                server.Characters.Add(save);
            }
            CharacterSources.Current = server;
            yield return CharacterCreatorPlayTests.LoadTitle();
            _title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            var title = Object.FindFirstObjectByType<TitleScreen>();
            _belt = title.CreatorPanel.GetComponent<BeltIntro>();
            _select = title.Select;
            View.CityHaze.Clear();
            _belt.MakingCamera.aspect = (float)BuildingShapeCapture.ShotWidth / BuildingShapeCapture.ShotHeight;

            title.Show(TitleScreen.Panel.CharacterSelect);
            yield return Frames(40);
            _select.Next();
            yield return Until(() => _select.Line.Settled);
            for (int i = 0; i < 8; i++)
            {
                Shoot($"hair-{i * 4:00}");
                yield return Frames(24);
            }
            _select.Inspect();
            yield return Frames(40);
            Shoot("redraw-card");
            _select.BeginRedrawSelected();
            yield return Frames(10);
            GameObject overlay = _select.RedrawOverlay;
            Assert.IsNotNull(overlay, "the pencil opened no redraw");
            Camera redraw = overlay.GetComponent<CreatorStage>().Stage.GetComponentInChildren<Camera>();
            redraw.aspect = _belt.MakingCamera.aspect;
            Shoot("redraw-open", redraw, overlay.GetComponent<Canvas>());
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
            for (int i = 0; i < 600 && !done(); i++)
            {
                yield return null;
            }
            Assert.IsTrue(done(), "it never got there");
        }

        /// <summary>The belt's camera, with the title's interface drawn into the picture.</summary>
        private void Shoot(string name) => Shoot(name, _belt.MakingCamera, _select.GetComponentInParent<Canvas>().rootCanvas);

        /// <summary>A camera, with an interface drawn into the picture.</summary>
        private void Shoot(string name, Camera camera, Canvas canvas)
        {
            RenderMode modeWas = canvas.renderMode;
            Transform[] drawn = canvas.GetComponentsInChildren<Transform>(true);
            var layersWere = new int[drawn.Length];
            for (int i = 0; i < drawn.Length; i++)
            {
                layersWere[i] = drawn[i].gameObject.layer;
                drawn[i].gameObject.layer = camera.gameObject.layer;
            }
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1.0f;
            Canvas.ForceUpdateCanvases();
            Vector3 place = camera.transform.position;
            Quaternion turn = camera.transform.rotation;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"select-{++_shot:00}-{name}", place, place + turn * Vector3.forward);
            camera.transform.SetPositionAndRotation(place, turn);
            for (int i = 0; i < drawn.Length; i++)
            {
                drawn[i].gameObject.layer = layersWere[i];
            }
            canvas.renderMode = modeWas;
        }
    }
}
