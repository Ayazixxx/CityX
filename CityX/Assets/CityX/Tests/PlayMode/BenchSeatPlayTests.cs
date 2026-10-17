using System.Collections;
using CityX.Character;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT ASK: "add benches you can go up to and press E to sit as
    /// well." Through the REAL PlayerInputReader.Update() dispatch, with a
    /// real simulated keyboard (InputTestFixture, the same pattern
    /// JetpackGroundedJumpPlayTests already establishes) and a real
    /// CharacterController walked in via Move() (the same pattern
    /// PistolWeaponPlayTests' own WalkingUpToTheRealPrefabAndStoppingLetsE
    /// PickItUp establishes) -- proves the whole chain end to end: the
    /// trigger actually fires, E actually sits, the Animator actually
    /// reaches the Sitting state, and standing back up actually returns
    /// control.
    /// </summary>
    public sealed class BenchSeatPlayTests : InputTestFixture
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _benchObject;
        private BenchSeat _bench;
        private PlayerInputReader _reader;
        private PlayerLocomotion _locomotion;
        private CharacterController _controller;
        private Animator _animator;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            _benchObject = new GameObject("Bench");
            _benchObject.transform.position = new Vector3(0.0f, 0.0f, 6.0f);
            _bench = _benchObject.AddComponent<BenchSeat>();
            _bench.Build(null);
        }

        // [UnityTearDown], NOT the plain [TearDown] InputTestFixture itself
        // already declares -- a [TearDown] shadowing that same method broke its own
        // device cleanup between tests (a later test's Press() failed with
        // "Key:/Keyboard/e does not have an associated state"), the same
        // reason GrappleSwingRealInputPlayTests' own teardown is also
        // [UnityTearDown] rather than [TearDown].
        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
            if (_benchObject != null) Object.DestroyImmediate(_benchObject);
            if (_ground != null) Object.DestroyImmediate(_ground);
            yield break;
        }

        private void LoadPlayer(Vector3 position)
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, position, Quaternion.identity);
            _reader = _player.GetComponent<PlayerInputReader>();
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _controller = _player.GetComponent<CharacterController>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_reader, "Player.prefab has no PlayerInputReader");
            Assert.IsNotNull(_controller, "Player.prefab has no CharacterController");
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
#endif
        }

        /// <summary>
        /// Walked in via ordinary `yield return null` frames, NOT
        /// WaitForFixedUpdate -- a real failure found while first writing
        /// this test: pairing WaitForFixedUpdate with InputTestFixture's
        /// simulated Keyboard made the very next Press() throw
        /// "Key:/Keyboard/e does not have an associated state" (no other
        /// InputTestFixture test in this project combines the two).
        ///
        /// STILL SCALED BY Time.fixedDeltaTime, NOT Time.deltaTime, despite
        /// yielding on a plain frame -- see unity_headless_workflow's own
        /// "PlayMode batch runs at ~2500fps" gotcha: real Time.deltaTime in
        /// batch mode is around 0.0004s, which moved the controller a few
        /// centimetres over 120 frames and never reached the bench. The
        /// fixed constant gives a real, deterministic per-frame step
        /// regardless of how fast batch mode actually renders frames.
        /// </summary>
        private IEnumerator WalkToBench()
        {
            Vector3 toward = (_benchObject.transform.position - _player.transform.position).normalized;
            for (int step = 0; step < 120; step++)
            {
                float remaining = Vector3.Distance(_player.transform.position, _benchObject.transform.position);
                if (_bench.HasNearbyPlayer && remaining <= 1.5f)
                {
                    yield break;
                }
                _controller.Move(toward * (4.0f * Time.fixedDeltaTime));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WalkingUpAndPressingESitsOnTheBench()
        {
#if UNITY_EDITOR
            LoadPlayer(new Vector3(0.0f, 0.0f, 3.0f));
            yield return WalkToBench();

            Assert.IsTrue(_bench.HasNearbyPlayer,
                "walking the real CharacterController up to the real bench's real trigger " +
                "should have fired OnTriggerEnter");

            // A settle window before capturing the "standing" baseline --
            // WalkToBench only just stopped calling Move(), and the
            // Locomotion blend tree needs a few real frames to ease from
            // its walking swing back to a genuinely still idle pose, or
            // this snapshot catches the thigh mid-stride instead.
            for (int step = 0; step < 30; step++)
            {
                yield return null;
            }

            // Captured BEFORE sitting -- this rig's own local bone axis
            // convention for a leg is undocumented (ApplySittingPose itself
            // rotates in WORLD space for exactly that reason), so this
            // checks the ROTATION ACTUALLY CHANGED by a meaningful amount
            // rather than assuming which local axis "down the thigh" is.
            Transform upperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Assert.IsNotNull(upperLeg, "setup failed -- Player.prefab's rig has no LeftUpperLeg");
            Quaternion standingRotation = upperLeg.localRotation;

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);

            Assert.IsTrue(_bench.IsOccupied, $"pressing E next to the bench should sit down (bench still knows the player: {_bench.HasNearbyPlayer}, distance {Vector3.Distance(_player.transform.position, _benchObject.transform.position):0.00} m, player at {_player.transform.position})");
            Assert.IsTrue(_locomotion.IsSitting);
            Assert.IsFalse(_controller.enabled, "the CharacterController should be disabled while seated");
            Assert.AreEqual(_bench.SeatPoint.position, _player.transform.position,
                "sitting should snap the player root onto the bench's own seat point");

            // A real frame so LateUpdate's own ApplySittingPose actually
            // runs once -- IsSitting alone doesn't bend the legs, the next
            // Animator-enabled LateUpdate does.
            yield return null;
            float bendDegrees = Quaternion.Angle(standingRotation, upperLeg.localRotation);
            Assert.Greater(bendDegrees, 15.0f,
                "sitting should visibly bend the thigh off its standing-idle rotation");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PressingEAgainStandsBackUp()
        {
#if UNITY_EDITOR
            // Spawned already inside InteractRange rather than walked in --
            // this test is about the sit/stand toggle, not the trigger
            // itself (WalkingUpAndPressingESitsOnTheBench already covers
            // that). A couple of physics steps is enough for the already-
            // overlapping trigger to fire OnTriggerEnter.
            LoadPlayer(new Vector3(0.0f, 0.0f, 4.5f));
            for (int step = 0; step < 60 && !_bench.HasNearbyPlayer; step++)
            {
                yield return null;
            }
            Assert.IsTrue(_bench.HasNearbyPlayer, "setup failed -- trigger never fired");

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);
            Assert.IsTrue(_bench.IsOccupied, "setup failed -- never sat down");

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);

            Assert.IsFalse(_bench.IsOccupied, "pressing E again should stand back up");
            Assert.IsFalse(_locomotion.IsSitting);
            Assert.IsTrue(_controller.enabled, "the CharacterController should be re-enabled after standing");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        /// <summary>
        /// Walking away is the more natural "get up" gesture than making E
        /// the only way out -- see BenchSeat.Update's own comment.
        /// </summary>
        [UnityTest]
        public IEnumerator PressingAMovementKeyAlsoStandsUp()
        {
#if UNITY_EDITOR
            LoadPlayer(new Vector3(0.0f, 0.0f, 4.5f));
            for (int step = 0; step < 60 && !_bench.HasNearbyPlayer; step++)
            {
                yield return null;
            }
            Assert.IsTrue(_bench.HasNearbyPlayer, "setup failed -- trigger never fired");

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);
            Assert.IsTrue(_bench.IsOccupied, "setup failed -- never sat down");

            Press(_keyboard.wKey);
            yield return null;
            Release(_keyboard.wKey);

            Assert.IsFalse(_bench.IsOccupied, "pressing a movement key while seated should stand back up");
            Assert.IsTrue(_controller.enabled);
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TooFarAwayPressingEDoesNothing()
        {
#if UNITY_EDITOR
            LoadPlayer(new Vector3(0.0f, 0.0f, 0.0f));
            yield return null;

            Assert.IsFalse(_bench.HasNearbyPlayer, "setup failed -- player should not start near the bench");

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);

            Assert.IsFalse(_bench.IsOccupied, "pressing E far from the bench should not sit down");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
