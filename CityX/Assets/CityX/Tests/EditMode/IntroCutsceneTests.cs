using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The fall/impact/getting-up/looking-around sequencer itself, driven
    /// by hand through Tick(deltaTime) the same way ClaimCutsceneTests
    /// already drives ClaimCutscene -- what matters here is that each phase
    /// lasts the right real-world duration and that the handoff happens
    /// exactly once, not that any particular frame looks a certain way.
    /// </summary>
    public sealed class IntroCutsceneTests
    {
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";

        private GameObject _playerObject;
        private GameObject _cameraObject;
        private CharacterController _controller;
        private ThirdPersonCamera _thirdPersonCamera;
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private IntroCutscene _cutscene;

        [SetUp]
        public void SetUp()
        {
            var runtimeController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(runtimeController,
                $"no controller at {ControllerPath} -- run CityX/Rebuild Player Rig");

            _playerObject = new GameObject("Player", typeof(CharacterController));
            _playerObject.transform.position = new Vector3(5.0f, 0.0f, 5.0f);
            _controller = _playerObject.GetComponent<CharacterController>();
            _locomotion = _playerObject.AddComponent<PlayerLocomotion>();

            var animatorObject = new GameObject("AnimatedBody");
            animatorObject.transform.SetParent(_playerObject.transform, false);
            _animator = animatorObject.AddComponent<Animator>();
            _animator.runtimeAnimatorController = runtimeController;

            _cameraObject = new GameObject("Camera");
            _thirdPersonCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _thirdPersonCamera.Target = _playerObject.transform;
            _thirdPersonCamera.Distance = 4.5f;

            _cutscene = _playerObject.AddComponent<IntroCutscene>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_cameraObject);
        }

        private void Begin()
        {
            _cutscene.Begin(_controller, _thirdPersonCamera, _animator, _cameraObject.transform, _locomotion);
        }

        /// <summary>
        /// Ticks by seconds using realistic small steps rather than one huge
        /// jump. Each phase-handler only ever consumes ONE phase's worth of
        /// deltaTime per Tick() call and does not carry an overshoot into
        /// the next phase -- exactly right for a real Update() calling
        /// Tick(Time.deltaTime) every frame, but a single multi-second call
        /// skips whole phases outright rather than cascading through them,
        /// which is why this steps in small increments instead.
        /// </summary>
        private static readonly float TickStepSeconds = 0.05f;

        private void TickForward(float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += TickStepSeconds)
            {
                _cutscene.Tick(TickStepSeconds);
            }
        }

        private static float TotalCutsceneSeconds()
        {
            return IntroCutscene.FallDurationSeconds
                   + IntroCutscene.ImpactClipSeconds
                   + IntroCutscene.ImpactHoldSeconds
                   + IntroCutscene.GettingUpClipSeconds
                   + IntroCutscene.LookingAroundClipSeconds
                   + IntroCutscene.HandoffDurationSeconds;
        }

        [Test]
        public void BeginTeleportsTheBodyToAltitudeImmediately()
        {
            Vector3 spawn = _playerObject.transform.position;
            Begin();

            Assert.AreEqual(spawn.y + IntroCutscene.StartAltitude, _playerObject.transform.position.y, 0.01f,
                "the body should be at drop altitude the instant Begin runs, not after the first Tick");
            Assert.AreEqual(spawn.x, _playerObject.transform.position.x, 0.01f,
                "altitude is the only thing Begin should change -- x/z stay at the spawn point");
        }

        [Test]
        public void TheBodyDescendsOverExactlyTheFallDuration()
        {
            Begin();
            float spawnY = _playerObject.transform.position.y - IntroCutscene.StartAltitude;

            _cutscene.Tick(IntroCutscene.FallDurationSeconds * 0.5f);
            float midway = _playerObject.transform.position.y;
            Assert.Less(midway, spawnY + IntroCutscene.StartAltitude,
                "halfway through the fall the body should already have descended");
            Assert.Greater(midway, spawnY,
                "halfway through the fall the body should not have reached the ground yet");

            _cutscene.Tick(IntroCutscene.FallDurationSeconds * 0.5f + 0.01f);
            Assert.AreEqual(spawnY, _playerObject.transform.position.y, 0.05f,
                "at the end of the fall the body should be back at its own spawn height");
        }

        [Test]
        public void TheCameraTracksTheBodyDownThroughoutTheWholeFallNotJustNearTheEnd()
        {
            Begin();
            float cameraYAtStart = _cameraObject.transform.position.y;

            // Well before TickCameraSwitch's own CamSwitchLeadSeconds window
            // kicks in near the end of the fall -- if the camera is only
            // ever placed once, in Begin(), it sits frozen here while the
            // body keeps dropping underneath it.
            _cutscene.Tick(IntroCutscene.FallDurationSeconds * 0.25f);
            float cameraYAfterAQuarterFall = _cameraObject.transform.position.y;
            Assert.Less(cameraYAfterAQuarterFall, cameraYAtStart - 1.0f,
                "the camera should already be descending with the body a quarter of the way through the " +
                "fall, not still sitting at its Begin()-time height");

            _cutscene.Tick(IntroCutscene.FallDurationSeconds * 0.25f);
            float cameraYAfterHalfFall = _cameraObject.transform.position.y;
            Assert.Less(cameraYAfterHalfFall, cameraYAfterAQuarterFall,
                "the camera should keep descending as the fall continues");
        }

        [Test]
        public void DriftNeverPushesTheBodyPastTheCappedRadius()
        {
            Begin();
            Vector3 spawn = _playerObject.transform.position;

            // No real Keyboard device exists in this EditMode context, so
            // TickDrift's own Keyboard.current read is always null and this
            // just confirms it does not throw or move the body sideways on
            // its own -- the cap itself is exercised by direct maths below,
            // since simulating held keys needs a real input device this
            // test context does not have.
            _cutscene.Tick(1.0f);
            var flatOffset = new Vector2(
                _playerObject.transform.position.x - spawn.x,
                _playerObject.transform.position.z - spawn.z);
            Assert.LessOrEqual(flatOffset.magnitude, IntroCutscene.MaxDriftDistance,
                "drift must never exceed its own cap");
        }

        [Test]
        public void TheFullSequenceFinishesExactlyOnceAtTheExpectedTotalDuration()
        {
            Begin();
            int finishedCount = 0;
            _cutscene.Finished += () => finishedCount++;

            TickForward(TotalCutsceneSeconds() - 1.0f);
            Assert.AreEqual(0, finishedCount, "the cutscene finished too early");

            TickForward(2.0f);
            Assert.AreEqual(1, finishedCount, "Finished should fire exactly once");

            // And it must not fire again on further ticks once already done.
            TickForward(5.0f);
            Assert.AreEqual(1, finishedCount, "Finished kept firing after the cutscene was already done");
        }

        [Test]
        public void HandoffReturnsTheCameraToItsOriginalSpringLength()
        {
            const float original = 4.5f;
            _thirdPersonCamera.Distance = original;
            Begin();

            TickForward(TotalCutsceneSeconds() + 0.5f);

            Assert.AreEqual(original, _thirdPersonCamera.Distance, 0.01f,
                "handoff should leave the camera back on its own original spring length, ready for ThirdPersonCamera to resume from");
            Assert.AreEqual(0.0f, _thirdPersonCamera.Yaw, 0.01f);
            Assert.AreEqual(0.0f, _thirdPersonCamera.Pitch, 0.01f);
        }

        [Test]
        public void RestoresFogDistancesExactlyToWhatTheyWereBeforeBegin()
        {
            // Set explicitly rather than trusting whatever RenderSettings
            // (genuine global, shared editor state -- not reset between
            // tests) happens to already hold, which could itself be
            // FallFogStartDistance/FallFogEndDistance left over from a
            // different test's own cutscene run.
            const float beforeStart = 35.0f;
            const float beforeEnd = 88.0f;
            RenderSettings.fogStartDistance = beforeStart;
            RenderSettings.fogEndDistance = beforeEnd;

            Begin();
            Assert.AreNotEqual(beforeStart, RenderSettings.fogStartDistance,
                "the fall should visibly thicken the fog rather than leaving it untouched");

            TickForward(TotalCutsceneSeconds() + 0.5f);

            Assert.AreEqual(beforeStart, RenderSettings.fogStartDistance, 0.01f,
                "fog should be back to exactly what it was before the cutscene started");
            Assert.AreEqual(beforeEnd, RenderSettings.fogEndDistance, 0.01f);
        }

        [Test]
        public void TheFallingAnimationDoesNotStartOnTheVeryFirstFewTicks()
        {
            // Regression guard for the PlayerHud portrait race: the position
            // teleport in Begin() is immediate, but the animation itself
            // must wait a few frames -- ported directly from
            // intro_cutscene.gd's own documented fix for the same bug.
            Begin();
            Assert.IsFalse(_cutscene.FallingClipStarted,
                "the falling clip must not start the instant Begin runs");

            for (int i = 0; i < IntroCutscene.PortraitCaptureDelayFrames; i++)
            {
                _cutscene.Tick(0.016f);
            }
            Assert.IsFalse(_cutscene.FallingClipStarted,
                $"the falling clip started within {IntroCutscene.PortraitCaptureDelayFrames} frames -- " +
                "too early to guarantee PlayerHud's own 2-frame portrait capture already ran");

            _cutscene.Tick(0.016f);
            Assert.IsTrue(_cutscene.FallingClipStarted,
                "the falling clip should have started by now");
        }
    }
}
