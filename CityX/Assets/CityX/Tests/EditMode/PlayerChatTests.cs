using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Chat's open/close bookkeeping and message dispatch. Ported from
    /// ui/chat_box.gd. Enter/Escape keypresses themselves are not simulated
    /// here -- see MapPanelViewPlayTests for why this project tests that
    /// class of thing through the public Open/Close/Submit methods a real
    /// keypress would call, rather than through simulated input.
    /// </summary>
    public sealed class PlayerChatTests
    {
        private GameObject _chatObject;
        private GameObject _playerObject;
        private GameObject _cameraObject;
        private PlayerChat _chat;
        private PlayerInputReader _inputReader;
        private ThirdPersonCamera _orbitCamera;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _playerObject.AddComponent<CharacterController>();
            _playerObject.AddComponent<PlayerLocomotion>();
            _inputReader = _playerObject.AddComponent<PlayerInputReader>();

            _cameraObject = new GameObject("Camera");
            _orbitCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _orbitCamera.Target = _playerObject.transform;

            _chatObject = new GameObject("Chat");
            _chat = _chatObject.AddComponent<PlayerChat>();
            _chat.Panel = new GameObject("Panel");
            _chat.Panel.transform.SetParent(_chatObject.transform, false);
            _chat.Panel.SetActive(false);
            _chat.InputReader = _inputReader;
            _chat.OrbitCamera = _orbitCamera;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_chatObject);
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void StartsClosed()
        {
            Assert.IsFalse(_chat.IsOpen);
        }

        [Test]
        public void OpeningDisablesMovementAndCameraLook()
        {
            _chat.Open();

            Assert.IsTrue(_chat.IsOpen);
            Assert.IsFalse(_inputReader.enabled);
            Assert.IsFalse(_orbitCamera.enabled);
        }

        [Test]
        public void ClosingRestoresMovementAndCameraLook()
        {
            _chat.Open();
            _chat.Close();

            Assert.IsFalse(_chat.IsOpen);
            Assert.IsTrue(_inputReader.enabled);
            Assert.IsTrue(_orbitCamera.enabled);
        }

        [Test]
        public void OpeningTwiceInARowDoesNothingExtra()
        {
            _chat.Open();
            _inputReader.enabled = true; // simulate something else re-enabling it mid-session
            _chat.Open();

            Assert.IsTrue(_inputReader.enabled, "the guard at the top of Open() should have refused the second call");
        }

        [TestCase("/claim")]
        [TestCase("/CLAIM")]
        [TestCase("  /claim  ")]
        public void IsClaimCommandRecognisesTheClaimCommandRegardlessOfCaseOrWhitespace(string text)
        {
            Assert.IsTrue(PlayerChat.IsClaimCommand(text));
        }

        [TestCase("claim")]
        [TestCase("/claiming")]
        [TestCase("hello /claim")]
        [TestCase("")]
        [TestCase(null)]
        public void IsClaimCommandRejectsEverythingElse(string text)
        {
            Assert.IsFalse(PlayerChat.IsClaimCommand(text));
        }

        [Test]
        public void SubmittingAClaimCommandDispatchesToTheClaimSystemNotTheSpeechBubble()
        {
            var claimObject = new GameObject("Claim");
            var claim = claimObject.AddComponent<ClaimSystem>();
            var audioObject = new GameObject("Audio");
            claim.Audio = audioObject.AddComponent<PlayerAudio>();
            _chat.Claim = claim;

            var bubbleObject = new GameObject("Bubble");
            var bubble = bubbleObject.AddComponent<SpeechBubble>();
            bubble.Bind();
            _chat.Bubble = bubble;

            try
            {
                _chat.Open();
                _chat.Submit("/claim");

                // Outdoors, so TryClaim refuses -- what matters is that it was
                // actually invoked, not the outcome. A bubble would be visible
                // proof it was dispatched down the wrong path.
                Assert.IsFalse(bubble.IsShowing, "/claim should never show as a speech bubble");
            }
            finally
            {
                Object.DestroyImmediate(claimObject);
                Object.DestroyImmediate(audioObject);
                Object.DestroyImmediate(bubbleObject);
            }
        }

        [Test]
        public void SubmittingOrdinaryTextShowsASpeechBubble()
        {
            var bubbleObject = new GameObject("Bubble");
            var bubble = bubbleObject.AddComponent<SpeechBubble>();
            bubble.Bind();
            _chat.Bubble = bubble;

            try
            {
                _chat.Open();
                _chat.Submit("hello there");

                Assert.IsTrue(bubble.IsShowing);
            }
            finally
            {
                Object.DestroyImmediate(bubbleObject);
            }
        }

        [Test]
        public void SubmittingBlankTextShowsNothing()
        {
            var bubbleObject = new GameObject("Bubble");
            var bubble = bubbleObject.AddComponent<SpeechBubble>();
            bubble.Bind();
            _chat.Bubble = bubble;

            try
            {
                _chat.Open();
                _chat.Submit("   ");

                Assert.IsFalse(bubble.IsShowing, "whitespace-only text should not turn into an empty bubble");
            }
            finally
            {
                Object.DestroyImmediate(bubbleObject);
            }
        }

        [Test]
        public void SubmittingAlwaysClosesTheChatRegardlessOfContent()
        {
            _chat.Open();
            _chat.Submit("anything");

            Assert.IsFalse(_chat.IsOpen);
        }
    }
}
