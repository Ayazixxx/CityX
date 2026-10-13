using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE REPORTED "HOLDING JUMP CLIPS THROUGH THE GROUND" REGRESSION.
    /// charged_jump's own Hips bone carries a real, large local position
    /// drop that Humanoid retargeting inherited faithfully from the source
    /// FBX's own proportions -- correct for that file's own skeleton, but
    /// landing well below ground once mapped onto this rig's own taller
    /// legs. Confirmed directly: sampling the real feet bones mid-charge
    /// found them over a metre under the floor before this fix.
    ///
    /// PlayerLocomotion.ClampFeetAboveGround now lifts the whole visual rig
    /// (never the collider, never an individual bone) by whatever the lower
    /// foot bone is short of ground contact, every frame after the Animator
    /// evaluates the pose -- a general floor clamp rather than a one-off
    /// patch to this one clip's own curves, since a user-supplied FBX (this
    /// is the one clip in the rig that did not come from the vetted Mixamo
    /// set) has no guarantee the next one won't have the same problem.
    ///
    /// NEEDS THE REAL Player.prefab -- see PlayerLocomotionHipsPlayTests's
    /// own comment on why.
    /// </summary>
    public sealed class PlayerLocomotionGroundClampPlayTests
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
        public IEnumerator HoldingTheChargeNeverSinksTheFeetBelowGround()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
            Transform leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Assert.IsNotNull(leftFoot, "Player.prefab's Animator has no LeftFoot bone mapped");
            Assert.IsNotNull(rightFoot, "Player.prefab's Animator has no RightFoot bone mapped");

            var input = new LocomotionInput { Move = Vector2.zero, ChargingJump = true };

            // Long enough to reach and hold the full charged_jump pose --
            // the clip is short, so a handful of frames is already well
            // past its transition-in.
            for (int step = 0; step < 60; step++)
            {
                _locomotion.Tick(input, 1.0f / 60.0f);
                yield return null;

                float lowestFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                // A little slack for the foot mesh's own sole, which sits
                // below the ankle bone even standing on flat ground -- the
                // bug this guards against was over a metre, not centimetres.
                Assert.Greater(lowestFootY, _player.transform.position.y - 0.05f,
                    $"a foot bone sank to {lowestFootY:0.000} at step {step}, well below ground contact " +
                    $"at {_player.transform.position.y:0.000} -- the reported floor-clipping is still there " +
                    $"(foot placement: hip drop {_locomotion.FootPlanting.HipDrop:0.000}, landing dip {_locomotion.FootPlanting.LandingDip:0.000}, " +
                    $"weights {_locomotion.FootPlanting.Of(0).Weight:0.00}/{_locomotion.FootPlanting.Of(1).Weight:0.00}, " +
                    $"lifts {_locomotion.FootPlanting.LastLift(0):0.000}/{_locomotion.FootPlanting.LastLift(1):0.000}, " +
                    $"targets y {_locomotion.FootPlanting.Of(0).Target.y:0.000}/{_locomotion.FootPlanting.Of(1).Target.y:0.000}, " +
                    $"drawn root y {_animator.transform.localPosition.y:0.000}, active {_locomotion.FootIkActive})");
            }
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
