using System;
using System.Collections;
using System.Collections.Generic;
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
    /// The conveyor belt a character is made on, played in the real title scene with real keys (BeltIntro, docs/BELT_INTRO.md; owner,
    /// 2026-11-10: "when you hold right, it rolls out a plain white playermodel from the left of the screen ... once you have rolled him to
    /// the center of the screen the conveyer belt stops, and the character customization tools appear ... make this fade in ... a t pose
    /// mode where he actually lifts his arms ... once you complete your character. the conveyer belt start moving to the right again ...
    /// the camera angle changes ... rolls your character off the conveyer belt (it ragdolls in midair) and then ... the whole screen fades
    /// to black").
    /// </summary>
    public sealed class BeltIntroPlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Scene _scene;
        private TitleScreen _title;
        private BeltIntro _belt;
        private ICharacterSource _sourceWas;
        private bool _firstStartWas;

        /// <summary>A server with a given number of characters, which makes and serves whatever it is asked for.</summary>
        private sealed class Server : ICharacterSource
        {
            public readonly List<CharacterSave> Characters = new List<CharacterSave>();

            public void List(Action<List<CharacterSave>> done) => done(new List<CharacterSave>(Characters));

            public void Create(CharacterSave requested, Action<CharacterSave> done)
            {
                Characters.Add(requested);
                done(requested);
            }

            public void Delete(string characterId, Action<bool> done) => done(true);

            public void Play(string characterId, Action<CharacterSave, bool> done) => done(Characters.Find(c => c.Id == characterId), false);

            public void Save(CharacterSave character, bool adminEdited)
            {
            }
        }

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _sourceWas = CharacterSources.Current;
            _firstStartWas = TitleScreen.BeltOnFirstStart;
            Time.captureFramerate = 60;
        }

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            CharacterSources.Current = _sourceWas;
            TitleScreen.BeltOnFirstStart = _firstStartWas;
            PlaySession.Character = null;
            PlaySession.IsNewCharacter = false;
            ScreenBlack.FadeTo(0.0f, 0.0f);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
        }

        /// <summary>The title scene, on a server with <paramref name="characters"/> made already.</summary>
        private IEnumerator Open(int characters, bool beltOnFirstStart = false)
        {
            var server = new Server();
            for (int i = 0; i < characters; i++)
            {
                server.Characters.Add(CharacterSave.Create("Made" + i, CharacterSave.DefaultSkin));
            }
            CharacterSources.Current = server;
            TitleScreen.BeltOnFirstStart = beltOnFirstStart;
            yield return CharacterCreatorPlayTests.LoadTitle();
            _scene = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            _title = UnityEngine.Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(_title, "the title scene has no TitleScreen");
            _belt = _title.CreatorPanel.GetComponent<BeltIntro>();
            Assert.IsNotNull(_belt, "the creator has no belt: rebuild the title scene");
        }

        private IEnumerator Begin()
        {
            yield return Open(1);
            _title.BeginCreator();
            yield return null;
            yield return null;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator Until(Func<bool> done, float seconds, string what)
        {
            float waited = 0.0f;
            while (!done() && waited < seconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(done(), $"{what} (waited {seconds} s)");
        }

        /// <summary>Where the body is across the making camera's picture: 0 its left edge, 1 its right.</summary>
        private float BodyAcrossTheScreen() => _belt.MakingCamera.WorldToViewportPoint(_belt.Rider.position).x;

        private IEnumerator RollIn()
        {
            Press(_keyboard.dKey);
            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Making, 10.0f, "holding D never set the body down in the middle");
            Release(_keyboard.dKey);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBeltStandsEmptyUntilAKeyIsHeldAndThenRollsTheBodyInToTheMiddle()
        {
            yield return Begin();
            Assert.IsNotNull(_belt.Ride, "making a character did not start on the belt");
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing);
            Assert.IsTrue(_belt.Prompt.gameObject.activeInHierarchy, "no prompt on the empty belt");
            StringAssert.Contains("D", _belt.Prompt.text);
            Assert.AreEqual(0.0f, _belt.Tools.alpha, "the tools are up before there is anybody to make");
            Assert.IsFalse(_belt.Tools.interactable);
            Assert.Less(BodyAcrossTheScreen(), -0.05f, "the body is on the screen before it has been rolled in");
            Assert.IsTrue(_belt.Life.Alive, "the body waits in its painting pose, not alive");
            Assert.IsTrue(_belt.Life.Animator.enabled, "the body is not idling");

            yield return Frames(30);
            Assert.AreEqual(0.0f, _belt.Belt.Travel, 1e-4f, "the belt runs with no key held");
            Assert.Less(BodyAcrossTheScreen(), -0.05f);

            Press(_keyboard.dKey);
            yield return Frames(40);
            float partWay = BodyAcrossTheScreen();
            Assert.Greater(_belt.Belt.Travel, 0.5f, "D does not run the belt");
            Assert.That(partWay, Is.InRange(-0.3f, 0.5f), "the body is not riding in from the left");
            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Making, 10.0f, "holding D never set the body down in the middle");
            Release(_keyboard.dKey);
            Assert.AreEqual(0.5f, BodyAcrossTheScreen(), 0.01f, "the body was not set down in the middle of the screen");

            yield return null;
            Assert.IsFalse(_belt.Prompt.gameObject.activeInHierarchy, "the prompt stays up once the body has arrived");
            Assert.That(_belt.Tools.alpha, Is.InRange(0.0f, 0.5f), "the tools snapped on instead of fading in");
            yield return Until(() => _belt.Tools.alpha >= 1.0f, 2.0f, "the tools never finished fading in");
            Assert.IsTrue(_belt.Tools.interactable, "the tools are up but cannot be used");
            Assert.IsTrue(_belt.Creator.enabled, "the body cannot be turned or painted once it has arrived");
            float travelled = _belt.Belt.Travel;
            Press(_keyboard.dKey);
            yield return Frames(20);
            Release(_keyboard.dKey);
            Assert.AreEqual(travelled, _belt.Belt.Travel, 1e-4f, "D still runs the belt while the character is being made");
        }

        [UnityTest]
        public IEnumerator LeftRunsTheBeltTheOtherWay()
        {
            yield return Begin();
            Press(_keyboard.leftArrowKey);
            yield return Frames(40);
            Release(_keyboard.leftArrowKey);
            Assert.Less(_belt.Belt.Travel, -0.5f, "the left arrow does not run the belt left");
            Assert.Less(BodyAcrossTheScreen(), -0.05f, "the body came in while the belt ran away from the screen");
            Press(_keyboard.aKey);
            float before = _belt.Belt.Travel;
            yield return Frames(20);
            Release(_keyboard.aKey);
            Assert.Less(_belt.Belt.Travel, before, "A does not run the belt left");
        }

        [UnityTest]
        public IEnumerator AliveItCannotBePaintedAndAPressOnItLiftsItsArmsToBe()
        {
            yield return Begin();
            yield return RollIn();
            yield return Until(() => _belt.Tools.alpha >= 1.0f, 2.0f, "the tools never came up");
            CharacterCreator creator = _belt.Creator;
            Assert.IsFalse(creator.HoldingStill, "the body is not alive while it is being made");
            StringAssert.Contains("T-POSE", _belt.PoseLabel.text);

            Vector2 chest = _belt.MakingCamera.WorldToScreenPoint(_belt.Rider.position);
            creator.PressAt(chest);
            Assert.IsFalse(creator.HasEdits, "a press painted the living body, where the brush is not");
            Assert.IsFalse(_belt.Life.Alive, "a press on the living body did not have it hold still");
            Assert.IsFalse(creator.HoldingStill, "the arms snapped up instead of lifting");
            yield return Until(() => creator.HoldingStill, CreatorLife.LiftSeconds + 0.5f, "the arms never finished lifting");
            Assert.IsFalse(_belt.Life.Animator.enabled, "it still idles in its painting pose: strokes would land off target");
            StringAssert.Contains("RELAX", _belt.PoseLabel.text);

            creator.PressAt(chest);
            Assert.IsTrue(creator.HasEdits, "holding still, a press on the chest paints nothing");

            _belt.TogglePose();
            Assert.IsTrue(_belt.Life.Alive, "the button does not let the arms back down");
            Assert.IsFalse(creator.HoldingStill);
            yield return Frames(4);
            Assert.IsTrue(_belt.Life.Animator.enabled);
        }

        [UnityTest]
        public IEnumerator FinishedItIsCarriedOffDroppedOffTheEndAndTheWorldIsEnteredInTheBlack()
        {
            yield return Begin();
            yield return RollIn();
            Ragdoll ragdoll = _belt.Ragdoll;
            Assert.IsNotNull(ragdoll, "the body on the belt has no ragdoll to fall with");
            Assert.IsFalse(ragdoll.IsLimp);

            bool entered = false;
            float blackWhenEntered = 0.0f;
            _belt.RollOut(() =>
            {
                entered = true;
                blackWhenEntered = ScreenBlack.Level;
            });
            Assert.AreEqual(BeltRide.Stage.RollingOut, _belt.Ride.Now, "finishing the character does not start the belt");
            yield return Frames(60);
            Assert.Less(_belt.Tools.alpha, 1.0f, "the tools stay up as the body is carried off");
            Assert.Greater(BodyAcrossTheScreen(), 0.55f, "the belt is not carrying the body right");
            Assert.IsFalse(_belt.WideCamera.enabled);

            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.OnTheEdge, 6.0f, "the body never reached the edge of the screen");
            yield return null;
            Assert.IsTrue(_belt.WideCamera.enabled, "the view did not change at the edge of the screen");
            Assert.IsFalse(_belt.MakingCamera.enabled);
            Vector3 seen = _belt.WideCamera.WorldToViewportPoint(_belt.Rider.position);
            Assert.That(seen.x, Is.InRange(0.05f, 0.5f), "the body is not in the wider view, left of the belt's end");
            Assert.IsFalse(ragdoll.IsLimp, "it fell before the belt's end");

            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Fallen, 6.0f, "the body never reached the belt's end");
            yield return null;
            Assert.IsTrue(ragdoll.IsLimp, "at the belt's end the body did not go limp");
            float top = _belt.Rider.parent.position.y;
            yield return Until(() => entered, 8.0f, "the world was never entered after the fall");
            Assert.Greater(blackWhenEntered, 0.95f, "the world is entered before the screen is black");
            Assert.Less(ragdoll.Torso.position.y, top - 2.0f, "the body did not fall off the belt");
        }

        [UnityTest]
        public IEnumerator ANewCharactersWorldWaitsForTheRideAndAMadeOnesDoesNot()
        {
            yield return Begin();
            Assert.IsNotNull(_title.Select.Departure, "nothing is set to carry a new character off before its world loads");
            Action load = null;
            _title.Select.Departure = enter => load = enter;
            CharacterSave made = CharacterSave.Create("New", CharacterSave.DefaultSkin);
            ((Server)CharacterSources.Current).Characters.Add(made);
            _title.Select.Play(made, isNew: true);
            Assert.IsNotNull(load, "a new character's world loaded at once, with no ride");
            Assert.AreEqual(CharacterCreatorPlayTests.ScenePath, _scene.path);
            Assert.IsTrue(_scene.isLoaded, "the title was left before the ride");

            _title.Show(TitleScreen.Panel.Menu);
            yield return null;
            Assert.IsNull(_title.Select.Departure, "leaving the creator leaves the ride set for whoever is played next");
        }

        [UnityTest]
        public IEnumerator ShownWithoutTheBeltTheCreatorStandsReadyAsItAlwaysDid()
        {
            yield return Begin();
            Press(_keyboard.dKey);
            yield return Frames(20);
            Release(_keyboard.dKey);
            _title.Show(TitleScreen.Panel.Creator);
            yield return null;
            Assert.IsNull(_belt.Ride, "the ride goes on under a creator shown ready");
            Assert.AreEqual(1.0f, _belt.Tools.alpha);
            Assert.IsTrue(_belt.Tools.interactable);
            Assert.IsTrue(_belt.Creator.HoldingStill, "a creator shown ready is not in its painting pose");
            Assert.IsFalse(_belt.Prompt.gameObject.activeInHierarchy);
            Assert.AreEqual(0.5f, BodyAcrossTheScreen(), 0.01f, "the body is not in the middle");
            Assert.IsTrue(_belt.MakingCamera.enabled);
        }

        [UnityTest]
        public IEnumerator AStartWithNobodyMadeOpensOnTheEmptyBeltAndOneWithSomebodyOnTheTitle()
        {
            yield return Open(0, beltOnFirstStart: true);
            yield return null;
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing, "a first start does not open on the belt");
            Assert.IsNotNull(_belt.Ride, "a first start shows the creator without the ride");
            Assert.AreEqual(BeltRide.Stage.RollingIn, _belt.Ride.Now);
            Assert.IsFalse(_title.Vignette.activeSelf, "the title's dark corners are drawn over the belt's daylight sky");
            yield return SceneManager.UnloadSceneAsync(_scene);

            yield return Open(2, beltOnFirstStart: true);
            yield return null;
            Assert.IsFalse(_title.CreatorPanel.activeSelf, "a start with characters made opens on the belt");
            Assert.IsTrue(_title.PromptLabel.gameObject.activeSelf, "the title's own first screen is gone");
        }
    }
}
