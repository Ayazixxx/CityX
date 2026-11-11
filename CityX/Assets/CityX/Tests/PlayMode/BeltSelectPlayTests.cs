using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The character list on the conveyor belt (CharacterSelect, docs/CHARACTER_SELECT.md; owner, 2026-11-11: "build the conveyer belt
    /// character select", "make it so a create new button is on screen always and when you press it ... it just rolls in an empty character
    /// to the center of the screen for you and you start the customizing", "make it so i can still boot from the first start scene where you
    /// control the conveyer belt somehow (for test purposes)"), in the real title scene with real keys and a real pointer.
    /// </summary>
    public sealed class BeltSelectPlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private Scene _scene;
        private TitleScreen _title;
        private CharacterSelect _select;
        private BeltIntro _belt;
        private BeltIntroPlayTests.Server _server;
        private ICharacterSource _sourceWas;
        private bool _firstStartWas;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _sourceWas = CharacterSources.Current;
            _firstStartWas = TitleScreen.BeltOnFirstStart;
            Time.captureFramerate = 60;
        }

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            CharacterSources.Current = _sourceWas;
            TitleScreen.BeltOnFirstStart = _firstStartWas;
            PlayerPrefs.DeleteKey(TitleScreen.FirstStartRequest);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
        }

        /// <summary>The title scene on a server with <paramref name="characters"/> made, Made0 the first; on the character list unless not.</summary>
        private IEnumerator Open(int characters, bool showList = true)
        {
            _server = new BeltIntroPlayTests.Server();
            for (int i = 0; i < characters; i++)
            {
                _server.Characters.Add(CharacterSave.Create("Made" + i, CharacterSave.DefaultSkin));
            }
            CharacterSources.Current = _server;
            TitleScreen.BeltOnFirstStart = false;
            yield return CharacterCreatorPlayTests.LoadTitle();
            _scene = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            _title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(_title, "the title scene has no TitleScreen");
            _select = _title.Select;
            _belt = _title.CreatorPanel.GetComponent<BeltIntro>();
            Assert.IsNotNull(_select.Lineup, "the character list stands nobody on the belt: rebuild the title scene");
            if (showList)
            {
                _title.Show(TitleScreen.Panel.CharacterSelect);
                yield return Frames(3);
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        private IEnumerator UntilSettled()
        {
            for (float waited = 0.0f; waited < 4.0f && !_select.Line.Settled; waited += Time.deltaTime)
            {
                yield return null;
            }
            Assert.IsTrue(_select.Line.Settled, "the belt never came to rest");
        }

        private float AcrossTheScreen(Transform thing) => _belt.MakingCamera.WorldToViewportPoint(thing.position).x;

        private void PressButton(string name)
        {
            Transform found = TransformSearch.FindDeep(_select.transform, name);
            Assert.IsNotNull(found, $"no {name} on the character list");
            Assert.IsTrue(found.gameObject.activeInHierarchy, $"{name} is not on the screen");
            found.GetComponent<Button>().onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator TheCharactersStandInARowOnTheBeltTheChosenOneInTheMiddle()
        {
            yield return Open(3);
            Assert.AreEqual(CharacterSelect.Mode.List, _select.Showing);
            GameObject first = _select.Lineup.FigureAt(0), second = _select.Lineup.FigureAt(1);
            Assert.IsNotNull(first, "the chosen character is not on the belt");
            Assert.IsNotNull(second, "the next character is not on the belt");
            Assert.AreEqual(0.5f, AcrossTheScreen(first.transform), 0.01f, "the chosen one is not in the middle");
            Assert.That(AcrossTheScreen(second.transform), Is.InRange(0.6f, 0.97f), "the next one does not stand in sight on the right");
            Assert.AreEqual(_belt.Belt.transform.position.y, first.transform.position.y, 0.01f, "the chosen one does not stand on the belt");
            Assert.Less(AcrossTheScreen(_belt.Rider), -0.05f, "the creator's own body is in sight on the character list");
            Assert.IsTrue(_belt.MakingCamera.enabled, "the belt is not seen");
            Assert.AreEqual(0, _title.Backdrop.cullingMask, "the city is still drawn under the belt");
            Assert.AreEqual("Made0", _select.TagName.text, "the name over the middle one is not theirs");
        }

        [UnityTest]
        public IEnumerator DRollsTheNextOneInFromTheRightToTheMiddleWithTheBeltHeard()
        {
            yield return Open(3);
            float travel = _belt.Belt.Travel;
            Press(_keyboard.dKey);
            yield return null;
            Release(_keyboard.dKey);
            bool ranLeft = false, heard = false;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                ranLeft |= _belt.Belt.Speed < 0.0f;
                heard |= _belt.Hum.isPlaying && _belt.Hum.volume > 0.0f;
            }
            Assert.IsTrue(ranLeft, "the belt did not run left to bring in the one on the right");
            Assert.IsTrue(heard, "the belt was not heard running");
            yield return UntilSettled();
            Assert.AreEqual(1, _select.SelectedIndex);
            Assert.AreEqual("Made1", _select.Selected.Name);
            Assert.AreEqual(0.5f, AcrossTheScreen(_select.Lineup.FigureAt(1).transform), 0.01f, "the next one was not set down in the middle");
            Assert.AreEqual(travel - _select.Lineup.Spacing, _belt.Belt.Travel, 1e-3f, "the belt did not run one character along");
            yield return null;
            Assert.IsFalse(_belt.Hum.isPlaying, "the belt is still heard at rest");
        }

        [UnityTest]
        public IEnumerator HeldItRollsOnToTheLastOneAndStopsThere()
        {
            yield return Open(3);
            Press(_keyboard.rightArrowKey);
            yield return Frames(150);
            Release(_keyboard.rightArrowKey);
            yield return UntilSettled();
            Assert.AreEqual(2, _select.SelectedIndex, "held, it did not roll on to the last");
            Assert.IsFalse(_select.NextButton.activeSelf, "the right arrow is offered past the last one");
            Assert.IsTrue(_select.PreviousButton.activeSelf, "the left arrow is not offered");
        }

        [UnityTest]
        public IEnumerator EnterClosesInOnTheChosenOneWithItsCardAndEscapeDrawsBack()
        {
            yield return Open(2);
            Vector3 home = _belt.Creator.CameraHome;
            yield return Tap(_keyboard.enterKey);
            Assert.AreEqual(CharacterSelect.Mode.Inspecting, _select.Showing, "Enter did not choose the middle one");
            yield return Frames(40);
            Assert.IsTrue(_select.CardPanel.activeInHierarchy, "no card");
            Assert.AreEqual("Made0", _select.NameLabel.text);
            Assert.Less(Vector3.Distance(_belt.MakingCamera.transform.localPosition, CharacterSelect.InspectView), 0.01f, "the view did not close in");
            Assert.Less(AcrossTheScreen(_select.Lineup.FigureAt(0).transform), 0.45f, "the chosen one does not stand left of its card");
            Vector3 nextOnesNearSide = _select.Lineup.transform.TransformPoint(
                new Vector3(-(_select.Lineup.Along(1) - BeltLineup.HalfWidth), BodyRig.StandingHeight * 0.5f, 0.0f));
            Assert.Greater(_belt.MakingCamera.WorldToViewportPoint(nextOnesNearSide).x, 1.0f, "the next one stands in sight in the card's way");
            Assert.IsTrue(TransformSearch.FindDeep(_select.CardPanel.transform, "PlayButton").gameObject.activeInHierarchy, "no PLAY");
            Assert.IsTrue(TransformSearch.FindDeep(_select.CardPanel.transform, "TrashButton").gameObject.activeInHierarchy, "no bin");

            yield return Tap(_keyboard.escapeKey);
            Assert.AreEqual(CharacterSelect.Mode.List, _select.Showing, "Escape did not go back to the row");
            Assert.AreEqual(TitleScreen.Panel.CharacterSelect, _title.Showing, "Escape left the character list");
            yield return Frames(40);
            Assert.Less(Vector3.Distance(_belt.MakingCamera.transform.localPosition, home), 0.01f, "the view did not draw back");
            Assert.IsFalse(_select.CardPanel.activeInHierarchy, "the card stayed up");
        }

        [UnityTest]
        public IEnumerator ClickingTheOneOnTheRightRollsItInAndClickingTheMiddleOneClosesIn()
        {
            yield return Open(3);
            Vector3 middleOfTheNext = _select.Lineup.FigureAt(1).transform.position + Vector3.up * (BodyRig.StandingHeight * 0.5f);
            Set(_mouse.position, (Vector2)_belt.MakingCamera.WorldToScreenPoint(middleOfTheNext));
            yield return null;
            yield return Tap(_mouse.leftButton);
            yield return UntilSettled();
            Assert.AreEqual(1, _select.SelectedIndex, "clicking the one on the right did not roll it in");

            Vector3 middleOfTheChosen = _select.Lineup.FigureAt(1).transform.position + Vector3.up * (BodyRig.StandingHeight * 0.5f);
            Set(_mouse.position, (Vector2)_belt.MakingCamera.WorldToScreenPoint(middleOfTheChosen));
            yield return null;
            yield return Tap(_mouse.leftButton);
            Assert.AreEqual(CharacterSelect.Mode.Inspecting, _select.Showing, "clicking the middle one did not choose it");
        }

        /// <summary>
        /// CREATE NEW: the belt runs right by itself, no key, no prompt; the row rides off the screen's right and a blank body comes in behind
        /// it, nobody ever between it and the middle; set down in the middle, the tools fade in.
        /// </summary>
        [UnityTest]
        public IEnumerator CreateNewRollsABlankBodyInBehindTheRowAndTheToolsComeUp()
        {
            yield return Open(3);
            PressButton("NewButton");
            for (int i = 0; i < 60 && _title.Showing != TitleScreen.Panel.Creator; i++)
            {
                yield return null;
            }
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing, "CREATE NEW did not hand the belt to the creator");
            Assert.IsNotNull(_belt.Ride);
            Assert.AreEqual(BeltRide.Stage.RollingIn, _belt.Ride.Now);
            Assert.IsFalse(_belt.PromptKey.gameObject.activeSelf, "the empty belt's key prompt came up");
            GameObject chosen = _select.Lineup.FigureAt(0);
            Assert.IsNotNull(chosen, "the row was taken away before it rode off");
            float chosenWas = AcrossTheScreen(chosen.transform);

            float waited = 0.0f;
            bool movedRight = false;
            while (_belt.Ride.Now == BeltRide.Stage.RollingIn && waited < 6.0f)
            {
                float bodyAlong = -_belt.Rider.localPosition.x;
                for (int slot = 0; slot < _select.Lineup.Count; slot++)
                {
                    if (_select.Lineup.FigureAt(slot) != null)
                    {
                        Assert.Greater(_select.Lineup.Along(slot), bodyAlong + 0.9f * _select.Lineup.Spacing,
                            $"a character stands between the new body and the middle ({slot})");
                    }
                }
                if (chosen != null)
                {
                    movedRight |= AcrossTheScreen(chosen.transform) > chosenWas + 0.1f;
                }
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(BeltRide.Stage.Making, _belt.Ride.Now, "the new body was never brought to the middle with no key held");
            Assert.Less(waited, 4.0f, "bringing the new body in takes too long");
            Assert.IsTrue(movedRight, "the row did not ride off to the right");
            Assert.AreEqual(0.5f, AcrossTheScreen(_belt.Rider), 0.01f, "the new body is not in the middle");
            yield return null;
            Assert.AreEqual(0, _select.Lineup.Count, "the row is still on the belt the character is made on");
            yield return Frames(45);
            Assert.AreEqual(1.0f, _belt.Tools.alpha, 1e-3f, "the tools did not come up");
            Assert.IsTrue(_belt.Creator.enabled, "the body cannot be made");
        }

        [UnityTest]
        public IEnumerator CreateNewFromTheCardDrawsTheViewBackFirst()
        {
            yield return Open(2);
            yield return Tap(_keyboard.enterKey);
            yield return Frames(40);
            PressButton("NewButton");
            yield return Frames(2);
            Assert.AreEqual(TitleScreen.Panel.CharacterSelect, _title.Showing, "the belt was handed over with the view still closed in");
            Assert.AreEqual(CharacterSelect.Mode.List, _select.Showing, "the card stayed up");
            for (int i = 0; i < 60 && _title.Showing != TitleScreen.Panel.Creator; i++)
            {
                yield return null;
            }
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing, "CREATE NEW from the card never reached the creator");
            Assert.Less(Vector3.Distance(_belt.MakingCamera.transform.localPosition, _belt.Creator.CameraHome), 0.01f,
                "the creator took over with the view somewhere else");
        }

        [UnityTest]
        public IEnumerator DeletingLeavesItsPlaceEmptyAndRollsTheNextOneIn()
        {
            yield return Open(3);
            yield return Tap(_keyboard.enterKey);
            PressButton("TrashButton");
            Assert.AreEqual(CharacterSelect.Mode.Confirming, _select.Showing, "the bin did not ask first");
            yield return null;
            PressButton("DeleteButton");
            Assert.AreEqual(2, _server.Characters.Count, "the server still has the character");
            Assert.IsTrue(_select.Lineup.FigureAt(0) == null, "the deleted character still stands on the belt");
            yield return UntilSettled();
            Assert.AreEqual("Made1", _select.Selected.Name, "the next one was not rolled in");
            Assert.AreEqual(0.5f, AcrossTheScreen(_select.Lineup.FigureAt(1).transform), 0.01f);
            Assert.IsFalse(_select.PreviousButton.activeSelf, "the empty place is offered to roll back to");
        }

        [UnityTest]
        public IEnumerator BackFromTheRowIsTheMenuAndTheBeltGoes()
        {
            yield return Open(2);
            yield return Tap(_keyboard.escapeKey);
            yield return null;
            Assert.AreEqual(TitleScreen.Panel.Menu, _title.Showing);
            Assert.IsFalse(_select.Lineup.gameObject.activeInHierarchy, "the belt stayed up behind the menu");
        }

        [UnityTest]
        public IEnumerator BackingOutOfTheCreatorStandsTheRowAgain()
        {
            yield return Open(2);
            _select.CreateNew();
            for (int i = 0; i < 60 && _title.Showing != TitleScreen.Panel.Creator; i++)
            {
                yield return null;
            }
            for (float waited = 0.0f; waited < 6.0f && _belt.Ride.Now != BeltRide.Stage.Making; waited += Time.deltaTime)
            {
                yield return null;
            }
            TransformSearch.FindDeep(_belt.transform, "BackButton").GetComponent<Button>().onClick.Invoke();
            yield return Frames(3);
            Assert.AreEqual(TitleScreen.Panel.CharacterSelect, _title.Showing, "BACK did not return to the character list");
            Assert.IsNotNull(_select.Lineup.FigureAt(0), "the row was not stood on the belt again");
            Assert.AreEqual(0.5f, AcrossTheScreen(_select.Lineup.FigureAt(0).transform), 0.01f);
            Assert.Less(AcrossTheScreen(_belt.Rider), -0.05f, "the creator's body stayed in sight");
            Assert.IsFalse(_belt.Creator.enabled, "the creator still answers the pointer on the character list");
        }

        /// <summary>CityX > Play Title As First Start sets the request: the empty belt with its prompt, characters made or not.</summary>
        [UnityTest]
        public IEnumerator AFirstStartRequestOpensTheEmptyBeltWithCharactersMade()
        {
            PlayerPrefs.SetInt(TitleScreen.FirstStartRequest, 1);
            yield return Open(2, showList: false);
            yield return null;
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing, "the first start's belt did not come up");
            Assert.AreEqual(BeltRide.Stage.RollingIn, _belt.Ride.Now);
            Assert.IsTrue(_belt.PromptKey.gameObject.activeSelf, "the empty belt has no key prompt");
            Assert.IsFalse(PlayerPrefs.HasKey(TitleScreen.FirstStartRequest), "the request was kept: every start would be a first one");
        }

        [UnityTest]
        public IEnumerator F8OpensTheFirstBootBelt()
        {
            yield return Open(2, showList: false);
            _title.Show(TitleScreen.Panel.Menu);
            yield return null;
            yield return Tap(_keyboard.f8Key);
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing, "F8 did not open the first boot's belt");
            Assert.AreEqual(BeltRide.Stage.RollingIn, _belt.Ride.Now);
            Assert.IsTrue(_belt.PromptKey.gameObject.activeSelf, "the empty belt has no key prompt");
        }
    }
}
