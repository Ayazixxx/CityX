using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The /dance and /breakdance chat emotes' own Animator wiring --
    /// PlayerRigBuilder.AddDanceStates wires dance.fbx/breakdance.fbx to
    /// real Animator states gated on a bool each, fed from
    /// LocomotionInput.Dancing/Breakdancing, the same "bool in, real state
    /// out" shape PlayerAimingPlayTests' own class comment already proves.
    ///
    /// NEEDS THE REAL Player.prefab -- see PlayerLocomotionHipsPlayTests's
    /// own comment on why.
    /// </summary>
    public sealed class DanceAnimationPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private void LoadPlayer()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
#endif
        }

        [UnityTest]
        public IEnumerator DancingEntersTheDanceStateAndMovingLeavesIt()
        {
#if UNITY_EDITOR
            LoadPlayer();

            var still = new LocomotionInput { Move = Vector2.zero, Dancing = false };
            var dancing = new LocomotionInput { Move = Vector2.zero, Dancing = true };
            var walking = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Dancing = false };

            for (int step = 0; step < 5; step++)
            {
                _locomotion.Tick(still, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Dance"));

            // MANY frames -- see PlayerAimingPlayTests' own identical comment
            // on why (real Time.deltaTime drives the transition blend, not
            // the fixed dt handed to Tick()).
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(dancing, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Dance"),
                "Dancing=true should enter the Dance state");

            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(walking, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Dance"),
                "moving should leave the Dance state");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator BreakdancingEntersTheBreakdanceState()
        {
#if UNITY_EDITOR
            LoadPlayer();

            var breakdancing = new LocomotionInput { Move = Vector2.zero, Breakdancing = true };

            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(breakdancing, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Breakdance"),
                "Breakdancing=true should enter the Breakdance state");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        /// <summary>
        /// The two emotes are mutually exclusive Any State entries, the same
        /// shape AddClimbingStates already uses for ClimbUp/ClimbDown --
        /// switching from one straight to the other needs no direct
        /// transition between them, only each one's own independent entry
        /// condition being re-evaluated every frame.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingFromDanceToBreakdanceGoesStraightAcross()
        {
#if UNITY_EDITOR
            LoadPlayer();

            var dancing = new LocomotionInput { Move = Vector2.zero, Dancing = true };
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(dancing, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Dance"), "setup failed");

            var breakdancing = new LocomotionInput { Move = Vector2.zero, Breakdancing = true };
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(breakdancing, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Breakdance"),
                "switching straight to breakdance should reach the Breakdance state");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
