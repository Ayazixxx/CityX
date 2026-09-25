using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The dialogue content and the stage transitions a chosen reply
    /// drives -- the pure, directly-testable half of old_guy.gd's own
    /// state machine. The travel/claim-watching/door/wander half needs a
    /// real ClaimSystem, physics and a streamed building to exercise
    /// meaningfully, which this suite does not attempt; see this project's
    /// own established EditMode/PlayMode split.
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
            // Talk() instantiates its own clone of DialogueBoxPrefab, so the
            // live conversation is on that instance, not on _box itself.
            return _object.GetComponentInChildren<DialogueBox>();
        }

        [Test]
        public void StartsAtGreeting()
        {
            Assert.AreEqual(OldGuy.Stage.Greeting, _oldGuy.CurrentStage);
        }

        [Test]
        public void GreetingOpensWithTheFallLineThenTheOffer()
        {
            DialogueNode[] nodes = _oldGuy.PreviewDialogueNodes();

            Assert.AreEqual(2, nodes.Length);
            Assert.AreEqual("Dude I just saw you fall from the sky", nodes[0].Text);
            Assert.AreEqual("I can teach you the ropes around here if you'd like", nodes[1].Text);
            Assert.AreEqual(2, nodes[1].Options.Length);
            Assert.AreEqual("Sure", nodes[1].Options[0].Text);
            Assert.AreEqual("I'll figure things out myself", nodes[1].Options[1].Text);
        }

        [Test]
        public void AcceptingMovesToAwaitingClaimAndContinuesWithTheReminderLines()
        {
            DialogueBox live = OpenAndFindLiveBox();
            live.Advance();          // past "Dude I just saw..."
            live.Choose(0);          // "Sure"

            Assert.AreEqual(OldGuy.Stage.AwaitingClaim, _oldGuy.CurrentStage);
            Assert.IsTrue(live.IsOpen, "accepting should continue straight into the follow-on lines");
        }

        [Test]
        public void DecliningMovesToDeclinedAndReTalkingReOffers()
        {
            DialogueBox live = OpenAndFindLiveBox();
            live.Advance();
            live.Choose(1);           // "I'll figure things out myself"

            Assert.AreEqual(OldGuy.Stage.Declined, _oldGuy.CurrentStage);

            DialogueNode[] nodes = _oldGuy.PreviewDialogueNodes();
            Assert.AreEqual(1, nodes.Length, "turning him down goes straight back to the offer, no fall line again");
            Assert.AreEqual("I can teach you the ropes around here if you'd like", nodes[0].Text);
        }

        [Test]
        public void AwaitingClaimShowsOnlyTheReminderOnANewConversation()
        {
            DialogueBox live = OpenAndFindLiveBox();
            live.Advance();
            live.Choose(0); // accept -> AwaitingClaim, still mid-conversation on the reminder lines
            // Close out the continued conversation so the next Talk() is fresh.
            while (live.IsOpen)
            {
                live.Advance();
            }

            DialogueNode[] nodes = _oldGuy.PreviewDialogueNodes();
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual(
                "You gotta open your chat box and type /claim once your inside the building you like",
                nodes[0].Text);
        }
    }
}
