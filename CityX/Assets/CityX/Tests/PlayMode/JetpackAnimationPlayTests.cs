using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "get rid of the walking and running animation... just when the
    /// jetpack is activated or when space is being held" -- an explicit
    /// new-mechanic ask. PlayerRigBuilder.AddJetpackIdleState wires the
    /// idle clip to a real Animator state gated on a bool, fed from
    /// LocomotionInput.JetpackThrusting, the same "bool in, real state
    /// out" shape PlayerAimingPlayTests' own class comment already proves
    /// for Aiming.
    ///
    /// NEEDS THE REAL Player.prefab -- see PlayerLocomotionHipsPlayTests's
    /// own comment on why; there is no "JetpackIdle" animator state on a
    /// bare GameObject with no Animator at all.
    /// </summary>
    public sealed class JetpackAnimationPlayTests
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

        [UnityTest]
        public IEnumerator ThrustingEntersTheJetpackIdleStateAndLettingGoLeavesIt()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            // WALKING, NOT STANDING STILL -- the whole point is that a
            // walk/run cycle must not keep showing once thrust is live, so
            // the check has to start from a state where Locomotion would
            // otherwise be playing something other than its own idle pose.
            var walking = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), JetpackThrusting = false };
            var thrusting = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), JetpackThrusting = true };

            for (int step = 0; step < 5; step++)
            {
                _locomotion.Tick(walking, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("JetpackIdle"),
                "should not start in the jetpack idle pose");

            // MANY frames, not a handful -- the Animator's own transition
            // blend advances on real Time.deltaTime, not the fixed dt this
            // test hands to Tick() for its own movement math, and batch
            // mode runs uncapped at roughly 2500fps (see
            // PlayerLocomotionPlayTests's own comment on the identical
            // gotcha) -- a short loop finishes while barely any real
            // transition time has passed.
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(thrusting, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("JetpackIdle"),
                "thrusting while moving should still force the jetpack idle pose, not a walk/run cycle");

            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(walking, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("JetpackIdle"),
                "letting go of thrust should return to ordinary locomotion");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
