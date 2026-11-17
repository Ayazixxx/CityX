using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What Old Guy says at each step of the tutorial and what a reply or a finished talk moves on (docs/TUTORIAL.md), driven through his
    /// real dialogue box. Where he stands and his fall need a scene: TutorialEndingPlayTests.
    /// </summary>
    public sealed class OldGuyTests
    {
        private GameObject _object;
        private OldGuy _oldGuy;
        private DialogueBox _box;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("OldGuy", typeof(CharacterController));
            _oldGuy = _object.AddComponent<OldGuy>();
            _oldGuy.Bind();

            var dialogueBoxPrefab = new GameObject("DialogueBoxPrefab");
            _box = dialogueBoxPrefab.AddComponent<DialogueBox>();
            _oldGuy.DialogueBoxPrefab = dialogueBoxPrefab;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_box.gameObject);
        }

        private DialogueBox OpenAndFindLiveBox()
        {
            _oldGuy.Talk();
            // Talk() instantiates its own clone of DialogueBoxPrefab, so the live conversation is on that instance, not on _box itself.
            return _object.GetComponentInChildren<DialogueBox>();
        }

        private static void ReadToTheEnd(DialogueBox live)
        {
            for (int i = 0; i < 20 && live.IsOpen && !live.IsChoosing; i++)
            {
                live.Advance();
            }
        }

        [Test]
        public void HeOpensWithTheFallLineThenOffersToShowYouTheRopes()
        {
            DialogueNode[] nodes = _oldGuy.PreviewDialogueNodes();

            Assert.AreEqual(2, nodes.Length);
            Assert.AreEqual("Dude I just saw you fall from the sky", nodes[0].Text);
            Assert.AreEqual("I can show you the ropes around here if you'd like", nodes[1].Text);
            Assert.AreEqual(2, nodes[1].Options.Length);
            Assert.AreEqual("Sure", nodes[1].Options[0].Text);
            Assert.AreEqual("I'll figure things out myself", nodes[1].Options[1].Text);
        }

        [Test]
        public void SayingYesStartsTheTutorialAndHeSendsYouIntoHisHouse()
        {
            DialogueBox live = OpenAndFindLiveBox();
            live.Advance();          // past "Dude I just saw..."
            live.Choose(0);          // "Sure"

            Assert.AreEqual(TutorialStep.GetIn, _oldGuy.Progress.Step);
            Assert.IsTrue(live.IsOpen, "saying yes should go straight on into what he wants");
            ReadToTheEnd(live);
            Assert.AreEqual(TutorialStep.GetIn, _oldGuy.Progress.Step, "hearing him out passed a step that is done by doing");
            Assert.AreEqual(TutorialScript.WhileOutside[0], _oldGuy.PreviewDialogueNodes()[0].Text);
        }

        [Test]
        public void SayingNoLeavesTheOfferStandingWithoutTheFallLine()
        {
            DialogueBox live = OpenAndFindLiveBox();
            live.Advance();
            live.Choose(1);           // "I'll figure things out myself"

            Assert.AreEqual(TutorialStep.Offered, _oldGuy.Progress.Step);
            DialogueNode[] nodes = _oldGuy.PreviewDialogueNodes();
            Assert.AreEqual(1, nodes.Length, "turning him down goes straight back to the offer, no fall line again");
            Assert.AreEqual(TutorialScript.Offer, nodes[0].Text);
        }

        [Test]
        public void HearingHimOutInTheDenPassesToThePower()
        {
            _oldGuy.Progress.JumpTo(TutorialStep.ReportBack);
            DialogueBox live = OpenAndFindLiveBox();
            Assert.AreEqual(TutorialScript.HouseCleared[0], _oldGuy.PreviewDialogueNodes()[0].Text);
            ReadToTheEnd(live);
            Assert.AreEqual(TutorialStep.Power, _oldGuy.Progress.Step);
        }

        [Test]
        public void HeNoticesTheOpenedBoxesFirst()
        {
            _oldGuy.Progress.JumpTo(TutorialStep.BackToOldGuy);
            Assert.AreEqual(TutorialScript.PowerBack[0], _oldGuy.PreviewDialogueNodes()[0].Text);
            _oldGuy.Progress.OpenedTheBoxes = true;
            DialogueNode[] nodes = _oldGuy.PreviewDialogueNodes();
            Assert.AreEqual(TutorialScript.SawTheBoxes, nodes[0].Text);
            Assert.AreEqual(TutorialScript.PowerBack.Length + 1, nodes.Length);
        }

        [Test]
        public void TheLastTalkAsksAboutTheFactionAndEndsOnSafeNow([Values(0, 1)] int answer)
        {
            _oldGuy.Progress.JumpTo(TutorialStep.LastTalk);
            DialogueBox live = OpenAndFindLiveBox();
            ReadToTheEnd(live);
            Assert.IsTrue(live.IsChoosing, "no faction question");
            live.Choose(answer);
            Assert.AreEqual(answer == 0, _oldGuy.Progress.StartedAFaction);
            Assert.AreEqual(TutorialStep.LastTalk, _oldGuy.Progress.Step, "the ending started before he finished talking");
            for (int i = 0; i < 20 && live.IsOpen; i++)
            {
                live.Advance();
            }
            Assert.AreEqual(TutorialStep.Ambush, _oldGuy.Progress.Step);
            Assert.AreEqual("Alright, I think we're safe now.", TutorialScript.Farewell[TutorialScript.Farewell.Length - 1]);
        }

        [Test]
        public void WalkingOffBeforeTheFactionQuestionAsksItAgain()
        {
            _oldGuy.Progress.JumpTo(TutorialStep.LastTalk);
            DialogueBox live = OpenAndFindLiveBox();
            live.Close();
            Assert.AreEqual(TutorialStep.LastTalk, _oldGuy.Progress.Step);
            Assert.IsNull(_oldGuy.Progress.StartedAFaction);
        }

        [Test]
        public void HeHasNothingToSayOnceTheShootingStarts()
        {
            _oldGuy.Progress.JumpTo(TutorialStep.Ambush);
            Assert.AreEqual(0, _oldGuy.PreviewDialogueNodes().Length);
        }
    }
}
