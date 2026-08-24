using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "RAISE THE GUN WITH HIS RIGHT HAND" -- pistol_aim_in.fbx was already
    /// sitting retargeted and unused in the Animations folder (see
    /// UpdateHeldPose's own pre-existing "no dedicated reload mocap clip
    /// exists (unlike shoot/aim-in/locomotion, all real retargeted Mixamo
    /// clips)" comment, which named it before anything actually used it).
    /// PlayerRigBuilder.AddAimingState now wires it to a real Animator
    /// state gated on a bool, fed from LocomotionInput.Aiming, itself fed
    /// from PlayerInputReader's own gun-aiming right-click state.
    ///
    /// NEEDS THE REAL Player.prefab -- see PlayerLocomotionHipsPlayTests's
    /// own comment on why; there is no "Aiming" animator state on a bare
    /// GameObject with no Animator at all.
    /// </summary>
    public sealed class PlayerAimingPlayTests
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
        public IEnumerator HoldingAimEntersTheAimingStateAndReleasingLeavesIt()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            var notAiming = new LocomotionInput { Move = Vector2.zero, Aiming = false };
            var aiming = new LocomotionInput { Move = Vector2.zero, Aiming = true };

            for (int step = 0; step < 5; step++)
            {
                _locomotion.Tick(notAiming, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Aiming"),
                "should not start in the aiming pose");

            // MANY frames, not a handful -- the Animator's own transition
            // blend advances on real Time.deltaTime, not the fixed dt this
            // test hands to Tick() for its own movement math, and batch
            // mode runs uncapped at roughly 2500fps (see
            // PlayerLocomotionPlayTests's own comment on the identical
            // gotcha) -- a short loop finishes while barely any real
            // transition time has passed.
            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(aiming, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Aiming"),
                "holding aim should raise the gun into the Aiming state");

            for (int step = 0; step < 3000; step++)
            {
                _locomotion.Tick(notAiming, 1.0f / 60.0f);
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Aiming"),
                "releasing aim should leave the Aiming state");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
