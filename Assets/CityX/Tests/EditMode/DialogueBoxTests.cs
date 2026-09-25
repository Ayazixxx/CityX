using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The node-advancement state machine, driven directly through the
    /// public API rather than simulated key presses -- Keyboard.current is
    /// null in this EditMode context (no real input device), and Update is
    /// only a thin wrapper reading it. A bare, unwired DialogueBox is
    /// enough: Open/Advance/Choose all tolerate null visual fields, so this
    /// tests the logic without needing DialogueBoxBuilder's own prefab.
    /// </summary>
    public sealed class DialogueBoxTests
    {
        private GameObject _object;
        private DialogueBox _box;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("DialogueBox");
            _box = _object.AddComponent<DialogueBox>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void OpeningWithNoNodesClosesImmediatelyButStillFires()
        {
            int finishedCount = 0;
            _box.Finished += () => finishedCount++;

            _box.Open("Nobody", System.Array.Empty<DialogueNode>());

            Assert.IsFalse(_box.IsOpen, "an empty conversation should never show the box");
            Assert.AreEqual(1, finishedCount, "Finished should still fire so the caller's own gate gets released");
        }

        [Test]
        public void OpeningAPlainLineShowsItAndAdvancingClosesOnTheLast()
        {
            _box.Open("Old Guy", new[] { new DialogueNode("Line one") });
            Assert.IsTrue(_box.IsOpen);
            Assert.IsFalse(_box.IsChoosing, "a plain line has no options to choose between");

            int finishedCount = 0;
            _box.Finished += () => finishedCount++;
            _box.Advance();

            Assert.IsFalse(_box.IsOpen, "advancing past the last line should close the box");
            Assert.AreEqual(1, finishedCount);
        }

        [Test]
        public void AdvancingAMultiLineConversationStepsThroughEachLine()
        {
            _box.Open("Old Guy", new[]
            {
                new DialogueNode("First"),
                new DialogueNode("Second"),
                new DialogueNode("Third"),
            });

            _box.Advance();
            Assert.IsTrue(_box.IsOpen, "there are still lines left after the first advance");
            _box.Advance();
            Assert.IsTrue(_box.IsOpen, "still one more line after the second advance");
            _box.Advance();
            Assert.IsFalse(_box.IsOpen, "the third advance runs off the end");
        }

        [Test]
        public void ANodeWithOptionsWaitsForAChoiceRatherThanAdvancing()
        {
            _box.Open("Old Guy", new[]
            {
                new DialogueNode("Well?", new[]
                {
                    new DialogueOption("Sure", "accept"),
                    new DialogueOption("Nah", "decline"),
                }),
            });

            Assert.IsTrue(_box.IsChoosing);

            // Enter's own equivalent here does nothing useful while
            // choosing -- Advance() is what a plain line uses; a choice is
            // resolved through Choose/ConfirmSelection instead, matching
            // how the real Update() branches on IsChoosing before deciding
            // which one to call.
            Assert.IsTrue(_box.IsOpen, "should still be open, waiting on a reply");
        }

        [Test]
        public void ChoosingAReplyFiresTheIdAndClosesTheBox()
        {
            string chosenId = null;
            int finishedCount = 0;
            _box.OptionChosen += id => chosenId = id;
            _box.Finished += () => finishedCount++;

            _box.Open("Old Guy", new[]
            {
                new DialogueNode("Well?", new[]
                {
                    new DialogueOption("Sure", "accept"),
                    new DialogueOption("Nah", "decline"),
                }),
            });
            _box.Choose(1);

            Assert.AreEqual("decline", chosenId);
            Assert.IsFalse(_box.IsOpen, "picking a reply closes the box on the spot");
            Assert.AreEqual(0, finishedCount,
                "OptionChosen fires instead of Finished for a picked reply -- the owner decides what happens next");
        }

        [Test]
        public void ReopeningAfterAChoiceStartsCleanNotStackedOnTheOldOptions()
        {
            _box.Open("Old Guy", new[]
            {
                new DialogueNode("Well?", new[] { new DialogueOption("Sure", "accept") }),
            });
            _box.Choose(0);

            _box.Open("Old Guy", new[] { new DialogueNode("Alright then.") });

            Assert.IsTrue(_box.IsOpen);
            Assert.IsFalse(_box.IsChoosing, "the follow-on line has no options of its own");
        }
    }
}
