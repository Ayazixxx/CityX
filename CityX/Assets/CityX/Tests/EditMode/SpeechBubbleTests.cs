using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The floating chat-message text over a character's head. Ported from
    /// character.gd's speech-bubble handling.
    /// </summary>
    public sealed class SpeechBubbleTests
    {
        private GameObject _object;
        private SpeechBubble _bubble;
        private GameObject _anchorObject;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("SpeechBubble");
            _bubble = _object.AddComponent<SpeechBubble>();
            _bubble.Bind();

            _anchorObject = new GameObject("Anchor");
            _bubble.Anchor = _anchorObject.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_anchorObject);
        }

        [Test]
        public void StartsHiddenWithNothingToShow()
        {
            Assert.IsFalse(_bubble.IsShowing);
        }

        [Test]
        public void ShowingATextMakesItVisible()
        {
            _bubble.Show("hello");
            Assert.IsTrue(_bubble.IsShowing);
        }

        [Test]
        public void ItFollowsItsAnchorWhileShowing()
        {
            _anchorObject.transform.position = new Vector3(5.0f, 0.0f, 10.0f);
            _bubble.Show("hi");
            _bubble.Tick(0.1f);

            Vector3 expected = _anchorObject.transform.position
                + Vector3.up * (_bubble.AnchorHeadHeight + SpeechBubble.HeightOffset);
            Assert.AreEqual(expected, _object.transform.position);
        }

        [Test]
        public void TickingPastTheDurationHidesIt()
        {
            _bubble.Show("bye");
            _bubble.Tick(SpeechBubble.DurationSeconds + 1.0f);

            Assert.IsFalse(_bubble.IsShowing);
        }

        [Test]
        public void ReShowingRestartsTheDuration()
        {
            _bubble.Show("first");
            _bubble.Tick(SpeechBubble.DurationSeconds - 0.5f);
            Assert.IsTrue(_bubble.IsShowing, "should still be up just before it would time out");

            _bubble.Show("second");
            _bubble.Tick(SpeechBubble.DurationSeconds - 0.5f);

            Assert.IsTrue(_bubble.IsShowing, "re-showing should have reset the countdown rather than letting the old one finish");
        }

        [Test]
        public void BindIsIdempotent()
        {
            Assert.DoesNotThrow(() =>
            {
                _bubble.Bind();
                _bubble.Bind();
            });
        }
    }
}
