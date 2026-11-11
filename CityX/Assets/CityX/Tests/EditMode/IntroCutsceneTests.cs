using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The intro's set-up and hand-off, driven by hand through Tick(deltaTime). The fall itself is physics (the ragdoll) and is played out
    /// for real in IntroCutsceneFallPlayTests; a body with no ragdoll, as here, skips straight to looking around.
    /// </summary>
    public sealed class IntroCutsceneTests
    {
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const float TickStepSeconds = 0.05f;

        private GameObject _playerObject;
        private CityWorld _worldBefore;
        private GameObject _cameraObject;
        private CharacterController _controller;
        private ThirdPersonCamera _thirdPersonCamera;
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private IntroCutscene _cutscene;

        [SetUp]
        public void SetUp()
        {
            // A world another test left loaded would move the drop onto open ground near the spawn: these tests are of the cutscene alone.
            _worldBefore = CurrentWorld.Instance;
            CurrentWorld.Instance = null;
            var runtimeController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(runtimeController, $"no controller at {ControllerPath} -- run CityX/Rebuild Player Rig");

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
            CurrentWorld.Instance = _worldBefore;
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_cameraObject);
        }

        private void Begin()
        {
            _cutscene.Begin(_controller, _thirdPersonCamera, _animator, _cameraObject.transform, _locomotion);
        }

        private void TickForward(float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += TickStepSeconds)
            {
                _cutscene.Tick(TickStepSeconds);
            }
        }

        private static float LookAroundAndHandOffSeconds => IntroCutscene.LookingAroundClipSeconds + IntroCutscene.HandoffDurationSeconds;

        [Test]
        public void BeginPutsTheBodyAtTheDropHeightOverTheSpawn()
        {
            Vector3 spawn = _playerObject.transform.position;
            Begin();

            Assert.AreEqual(spawn.y + IntroCutscene.StartAltitude, _playerObject.transform.position.y, 0.01f);
            Assert.AreEqual(spawn.x, _playerObject.transform.position.x, 0.01f);
            Assert.AreEqual(spawn.z, _playerObject.transform.position.z, 0.01f);
        }

        [Test]
        public void TheBodyHangsStillForThePortraitBeforeFalling()
        {
            // PlayerHud captures the portrait off the head two frames in: a tumbling head would be captured turned.
            Begin();
            for (int i = 0; i < IntroCutscene.PortraitCaptureDelayFrames; i++)
            {
                _cutscene.Tick(0.016f);
                Assert.IsFalse(_cutscene.IsFalling, $"the body let go on frame {i + 1}, before the portrait was taken");
            }
        }

        [Test]
        public void TheCameraStartsLookingDownOnTheBody()
        {
            Begin();
            Vector3 toBody = _playerObject.transform.position - _cameraObject.transform.position;
            Assert.Greater(_cameraObject.transform.position.y, _playerObject.transform.position.y);
            Assert.Greater(Vector3.Angle(toBody, Vector3.ProjectOnPlane(toBody, Vector3.up)), 60.0f, "the camera should look steeply down");
        }

        [Test]
        public void TheCloudIsThickAtTheTopAndTheFogIsPutBackExactlyAfter()
        {
            const float beforeStart = 35.0f;
            const float beforeEnd = 88.0f;
            RenderSettings.fogStartDistance = beforeStart;
            RenderSettings.fogEndDistance = beforeEnd;

            Begin();
            Assert.AreEqual(IntroCutscene.CloudFogStartDistance, RenderSettings.fogStartDistance, 0.01f);
            Assert.AreEqual(IntroCutscene.CloudFogEndDistance, RenderSettings.fogEndDistance, 0.01f);

            TickForward(1.0f + LookAroundAndHandOffSeconds);

            Assert.AreEqual(beforeStart, RenderSettings.fogStartDistance, 0.01f);
            Assert.AreEqual(beforeEnd, RenderSettings.fogEndDistance, 0.01f);
        }

        [Test]
        public void ItFinishesOnceAndHandsTheCameraBackOnItsOwnFraming()
        {
            const float original = 4.5f;
            _thirdPersonCamera.Distance = original;
            Begin();
            int finishedCount = 0;
            _cutscene.Finished += () => finishedCount++;

            TickForward(LookAroundAndHandOffSeconds - 1.0f);
            Assert.AreEqual(0, finishedCount, "the cutscene finished too early");

            TickForward(2.0f);
            Assert.AreEqual(1, finishedCount, "Finished should fire exactly once");
            TickForward(5.0f);
            Assert.AreEqual(1, finishedCount, "Finished kept firing after the cutscene was done");

            Assert.AreEqual(original, _thirdPersonCamera.Distance, 0.01f);
            Assert.AreEqual(0.0f, _thirdPersonCamera.Yaw, 0.01f);
            Assert.AreEqual(0.0f, _thirdPersonCamera.Pitch, 0.01f);
        }
    }
}
