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
        private GameObject _floor;
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
            if (_floor != null) Object.DestroyImmediate(_floor);
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

        [TestCase("/dance")]
        [TestCase("/DANCE")]
        [TestCase("  /dance  ")]
        public void IsDanceCommandRecognisesTheDanceCommandRegardlessOfCaseOrWhitespace(string text)
        {
            Assert.IsTrue(PlayerChat.IsDanceCommand(text));
        }

        [TestCase("dance")]
        [TestCase("/dancing")]
        [TestCase("/breakdance")]
        [TestCase("")]
        [TestCase(null)]
        public void IsDanceCommandRejectsEverythingElse(string text)
        {
            Assert.IsFalse(PlayerChat.IsDanceCommand(text));
        }

        [TestCase("/breakdance")]
        [TestCase("/BREAKDANCE")]
        [TestCase("  /breakdance  ")]
        public void IsBreakdanceCommandRecognisesTheBreakdanceCommandRegardlessOfCaseOrWhitespace(string text)
        {
            Assert.IsTrue(PlayerChat.IsBreakdanceCommand(text));
        }

        [TestCase("breakdance")]
        [TestCase("/dance")]
        [TestCase("")]
        [TestCase(null)]
        public void IsBreakdanceCommandRejectsEverythingElse(string text)
        {
            Assert.IsFalse(PlayerChat.IsBreakdanceCommand(text));
        }

        /// <summary>
        /// A real floor and a settling tick, since StartDance refuses while
        /// airborne (see its own doc comment) and a fresh CharacterController
        /// reports isGrounded false until it has actually moved onto
        /// something.
        /// </summary>
        private void GroundThePlayer()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _floor.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            Physics.SyncTransforms();

            var locomotion = _playerObject.GetComponent<PlayerLocomotion>();
            locomotion.Bind();
            for (int i = 0; i < 5; i++)
            {
                locomotion.Tick(default, 1.0f / 60.0f);
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the player never actually settled on the floor");
        }

        [Test]
        public void SubmittingADanceCommandStartsDancingNotASpeechBubble()
        {
            GroundThePlayer();
            var bubbleObject = new GameObject("Bubble");
            var bubble = bubbleObject.AddComponent<SpeechBubble>();
            bubble.Bind();
            _chat.Bubble = bubble;

            try
            {
                _chat.Open();
                _chat.Submit("/dance");

                Assert.IsTrue(_inputReader.IsDancing);
                Assert.IsFalse(bubble.IsShowing, "/dance should never show as a speech bubble");
            }
            finally
            {
                Object.DestroyImmediate(bubbleObject);
            }
        }

        [Test]
        public void SubmittingABreakdanceCommandStartsBreakdancingNotASpeechBubble()
        {
            GroundThePlayer();
            var bubbleObject = new GameObject("Bubble");
            var bubble = bubbleObject.AddComponent<SpeechBubble>();
            bubble.Bind();
            _chat.Bubble = bubble;

            try
            {
                _chat.Open();
                _chat.Submit("/breakdance");

                Assert.IsTrue(_inputReader.IsBreakdancing);
                Assert.IsFalse(bubble.IsShowing, "/breakdance should never show as a speech bubble");
            }
            finally
            {
                Object.DestroyImmediate(bubbleObject);
            }
        }

        [TestCase("/sleep")]
        [TestCase("/SLEEP")]
        [TestCase("  /sleep  ")]
        public void IsSleepCommandRecognisesTheSleepCommandRegardlessOfCaseOrWhitespace(string text)
        {
            Assert.IsTrue(PlayerChat.IsSleepCommand(text));
        }

        [TestCase("sleep")]
        [TestCase("/sleeping")]
        [TestCase("")]
        [TestCase(null)]
        public void IsSleepCommandRejectsEverythingElse(string text)
        {
            Assert.IsFalse(PlayerChat.IsSleepCommand(text));
        }

        /// <summary>
        /// EXPLICIT ASK: "the character just goes limp and ragdolls
        /// immediately no animation... lets see if its something do it with
        /// it being playable" -- Bodies is an empty array rather than a real
        /// built rig, since the DISPATCH path is what's under test here
        /// (does /sleep actually reach Ragdoll.Activate), not RagdollBuilder's
        /// own rig construction, which RagdollPlayTests already owns.
        /// </summary>
        [Test]
        public void SubmittingASleepCommandActivatesTheRagdollNotASpeechBubble()
        {
            var ragdoll = _playerObject.AddComponent<Ragdoll>();
            ragdoll.Bodies = new Rigidbody[0];
            ragdoll.Bind(); // Awake never runs in an EditMode test -- see Bind's own doc comment
            _chat.Ragdoll = ragdoll;

            var bubbleObject = new GameObject("Bubble");
            var bubble = bubbleObject.AddComponent<SpeechBubble>();
            bubble.Bind();
            _chat.Bubble = bubble;

            try
            {
                _chat.Open();
                _chat.Submit("/sleep");

                Assert.IsTrue(ragdoll.IsLimp);
                Assert.IsFalse(_playerObject.GetComponent<PlayerLocomotion>().enabled,
                    "going limp should stop normal locomotion too, not just play dead visually");
                Assert.IsFalse(bubble.IsShowing, "/sleep should never show as a speech bubble");

                // EXPLICIT ASK: "make it so i can press space to get up at
                // any time after /sleep is used" -- see Ragdoll.CanStandUp's
                // own doc comment for why /sleep passes allowImmediateStandUp.
                Assert.IsTrue(ragdoll.CanStandUp,
                    "/sleep should let Space stand the body back up immediately, with no rest wait");
            }
            finally
            {
                Object.DestroyImmediate(bubbleObject);
            }
        }
    }
}
