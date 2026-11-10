using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: plays the conveyor belt a character is made on (BeltIntro, docs/BELT_INTRO.md) in the real title scene with a held key,
    /// and renders each beat to Logs/CityShots/belt-NN-*.png: the empty belt and its prompt, the body rolled in, set down, the tools up,
    /// the arms lifting, the ride off, the wider view, the fall. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BeltIntroCapture : InputTestFixture
    {
        private Keyboard _keyboard;
        private Scene _title;
        private BeltIntro _belt;
        private bool _firstStartWas;
        private int _shot;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            TitleScreen.BeltOnFirstStart = _firstStartWas;
            ScreenBlack.FadeTo(0.0f, 0.0f);
            if (_title.IsValid() && _title.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_title);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheBelt()
        {
            CaptureGuard.NeedsGraphics();
            Time.captureFramerate = 60;
            _firstStartWas = TitleScreen.BeltOnFirstStart;
            TitleScreen.BeltOnFirstStart = false;
            yield return CharacterCreatorPlayTests.LoadTitle();
            _title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            var title = Object.FindFirstObjectByType<TitleScreen>();
            title.BeginCreator();
            yield return null;
            yield return null;
            _belt = title.CreatorPanel.GetComponent<BeltIntro>();
            Assert.IsNotNull(_belt.Ride, "the belt did not begin");

            Shoot("empty");

            Press(_keyboard.dKey);
            float waited = 0.0f;
            while (_belt.MakingCamera.WorldToViewportPoint(_belt.Rider.position).x < 0.05f && waited < 10.0f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Shoot("rolling-in");
            yield return Frames(45);
            Shoot("rolling-in-further");
            waited = 0.0f;
            while (_belt.Ride.Now == BeltRide.Stage.RollingIn && waited < 10.0f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Release(_keyboard.dKey);
            Assert.AreEqual(BeltRide.Stage.Making, _belt.Ride.Now, "holding D never set the body down in the middle");
            Shoot("set-down");
            yield return Frames(20);
            Shoot("tools-fading-in");
            yield return Frames(60);
            Shoot("making-alive");
            for (int i = 0; i < 6; i++)
            {
                ShootLegs(i);
                yield return Frames(40);
            }

            // Painted while it idles: down the hanging arm and across the belly, then the other two pages over the bare sky.
            Animator animator = _belt.Life.Animator;
            _belt.Creator.Colour = new Color(0.8f, 0.1f, 0.5f);
            _belt.Creator.BrushRadiusPixels = 8;
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg })
            {
                _belt.Creator.PressAt(_belt.MakingCamera.WorldToScreenPoint(animator.GetBoneTransform(bone).position));
                yield return null;
            }
            yield return Frames(2);
            Shoot("painted-alive");
            CreatorTabs tabs = _belt.Tools.GetComponent<CreatorTabs>();
            tabs.Show(CreatorTabs.Hair);
            yield return null;
            Shoot("hair-page");
            tabs.Show(CreatorTabs.Face);
            yield return null;
            Shoot("face-page");
            tabs.Show(CreatorTabs.Paint);
            yield return null;

            _belt.TogglePose();
            yield return Frames(12);
            Shoot("arms-lifting");
            yield return Frames(40);
            Shoot("holding-still");
            _belt.TogglePose();
            yield return Frames(45);

            bool entered = false;
            _belt.RollOut(() => entered = true);
            yield return Frames(30);
            Shoot("rolling-out");
            while (_belt.Ride.Now == BeltRide.Stage.RollingOut)
            {
                yield return null;
            }
            yield return Frames(2);
            Shoot("wide-view");
            yield return Frames(70);
            Shoot("toward-the-end");
            while (_belt.Ride.Now == BeltRide.Stage.OnTheEdge)
            {
                yield return null;
            }
            // Stood up short of the end, it tips off by itself: a picture every tenth of a second.
            Shoot("stood-up");
            for (int i = 1; i <= 16; i++)
            {
                yield return Frames(6);
                Shoot($"tipping-{i}");
            }
            yield return Frames(16);
            Shoot("falling");
            waited = 0.0f;
            while (!entered && waited < 8.0f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(entered, "the world was never entered after the fall");
            Assert.Greater(ScreenBlack.Level, 0.95f, "the world is entered before the screen is black");
            TestContext.WriteLine($"entered the world {waited:0.0} s after the last picture");
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// <summary>Close on the legs, the belt under them, to see the feet stay put through the idle (beltfeet-N.png).</summary>
        private void ShootLegs(int index)
        {
            Camera camera = _belt.MakingCamera;
            Transform stage = _belt.Rider.parent;
            Vector3 place = camera.transform.position;
            Quaternion turn = camera.transform.rotation;
            float fieldWas = camera.fieldOfView;
            camera.fieldOfView = 30.0f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"beltfeet-{index}", stage.TransformPoint(new Vector3(0.0f, 0.7f, 3.4f)), stage.TransformPoint(new Vector3(0.0f, 0.55f, 0.0f)));
            camera.fieldOfView = fieldWas;
            camera.transform.SetPositionAndRotation(place, turn);
        }

        /// <summary>The belt's camera that is on, with the title's interface drawn into the picture.</summary>
        private void Shoot(string name)
        {
            Camera camera = _belt.WideCamera.enabled ? _belt.WideCamera : _belt.MakingCamera;
            Canvas canvas = _belt.GetComponentInParent<Canvas>().rootCanvas;
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
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"belt-{++_shot:00}-{name}", place, place + turn * Vector3.forward);
            camera.transform.SetPositionAndRotation(place, turn);
            for (int i = 0; i < drawn.Length; i++)
            {
                drawn[i].gameObject.layer = layersWere[i];
            }
            canvas.renderMode = modeWas;
        }
    }
}
