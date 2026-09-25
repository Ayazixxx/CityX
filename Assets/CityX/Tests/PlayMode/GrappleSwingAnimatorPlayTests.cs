using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// GrappleSwing's interaction with the Animator, rebuilt from square
    /// one -- see GrappleSwing's own class doc comment for why. Four
    /// earlier real-physics designs and then eight rounds of fixing a
    /// bone-owning procedural pose (GrappleSwingPosePlayTests, now deleted)
    /// never fully eliminated a live pose-distortion report. This version
    /// never disables the Animator or touches a bone at all -- it drives
    /// the exact same Speed/MoveX/MoveZ/Grounded parameters ordinary WASD
    /// movement already drives, so there is no bone-posing mechanism left
    /// to test for distortion in. What actually needs checking now is much
    /// smaller: the Animator is never disabled, and it gets fed a pose
    /// that actually reflects the swing's own velocity.
    /// </summary>
    public sealed class GrappleSwingAnimatorPlayTests
    {
        private GameObject _player;
        private GrappleSwing _swing;
        private Animator _animator;
        private PlayerLocomotion _locomotion;

        private const float StepSeconds = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0.0f;
            if (_player != null) Object.Destroy(_player);
        }

        private void SpawnPlayer(float spawnHeight = 20.0f)
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, spawnHeight, 0.0f), Quaternion.identity);
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _locomotion.Bind();

            _swing = _player.AddComponent<GrappleSwing>();
            _swing.Bind();
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
#endif
        }

        [UnityTest]
        public IEnumerator AttachingAndReleasingNeverDisableTheAnimator()
        {
            SpawnPlayer();
            Assert.IsTrue(_animator.enabled, "setup failed -- the animator should start enabled");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            Assert.IsTrue(_animator.enabled,
                "attaching the swing disabled the animator -- GrappleSwing must never own a bone or the " +
                "animator any more, see its own class doc comment");

            for (int i = 0; i < 30; i++)
            {
                _swing.Tick(new Vector2(0.5f, 0.5f), i * 3.0f, false, false, false, false, StepSeconds);
                yield return null;
                Assert.IsTrue(_animator.enabled, $"the animator got disabled mid-swing on tick {i}");
            }

            _swing.Release(false);
            Assert.IsTrue(_animator.enabled, "releasing the swing disabled the animator");
        }

        /// <summary>
        /// Replaces the old "the right arm reaches for the rope" pose test
        /// -- there is no reach pose any more. What matters instead is that
        /// the swing's own velocity actually reaches the Animator at all,
        /// through the same Speed/Grounded parameters ordinary movement
        /// uses (see PlayerLocomotion.DriveAnimatorFromExternalVelocity).
        /// </summary>
        [UnityTest]
        public IEnumerator SwingingDrivesTheAnimatorsSpeedFromItsOwnVelocity()
        {
            SpawnPlayer();

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            // A real, sustained push builds real horizontal speed -- see
            // GrappleSwingTests (EditMode) for the rope physics this relies
            // on; this test only cares that the ANIMATOR hears about it.
            for (int i = 0; i < 60; i++)
            {
                _swing.Tick(new Vector2(0.0f, 1.0f), 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            float speedParameter = _animator.GetFloat("Speed");
            float actualHorizontalSpeed = new Vector2(_swing.Velocity.x, _swing.Velocity.z).magnitude;
            Assert.Greater(speedParameter, 0.5f,
                $"the animator's own Speed parameter read {speedParameter:0.00} despite a real swing velocity " +
                $"of {actualHorizontalSpeed:0.00} m/s -- DriveAnimatorFromExternalVelocity is not reaching it");
            // NOT a match to the swing's own raw velocity, and deliberately
            // so -- DriveAnimatorFromExternalVelocity clamps to SprintSpeed
            // before easing (see its own doc comment for the real bug this
            // fixes: an unclamped swing speed can reach nearly 5x
            // SprintSpeed, a query point Mecanim's own blend tree was never
            // authored to extrapolate past safely). The animation only
            // needs to read as "moving," not prove the swing's true speed.
            Assert.LessOrEqual(speedParameter, _locomotion.SprintSpeed + 0.5f,
                $"the animator's Speed parameter read {speedParameter:0.00}, above SprintSpeed -- " +
                "DriveAnimatorFromExternalVelocity's own clamp regressed");
            Assert.IsFalse(_animator.GetBool("Grounded"), "setup failed -- the swing should still be airborne");
        }

        /// <summary>
        /// ClampFeetAboveGround's own historical runaway (see its doc
        /// comment) only ever happened because the OLD GrappleSwing rotated
        /// the leg bones directly every frame. This version never touches
        /// them, so the same real, varied, sustained swing that used to
        /// drift the visual rig over 1000 metres from the root should now
        /// show no drift at all, the same way an ordinary jump/fall never
        /// has.
        /// </summary>
        [UnityTest]
        public IEnumerator SwingingNeverDriftsTheVisualRigFromTheRoot()
        {
            SpawnPlayer();
            Transform animatorTransform = _animator.transform;

            _swing.Attach(_player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));

            float worstOffset = 0.0f;
            for (int i = 0; i < 300; i++)
            {
                float t = i / 60.0f;
                Vector2 move = new Vector2(Mathf.Sin(t * 0.7f), Mathf.Cos(t * 0.5f) * 0.5f + 0.5f);
                float yaw = i * 2.0f;
                bool reel = (i / 30) % 4 == 0;
                bool payout = (i / 30) % 4 == 2;
                _swing.Tick(move, yaw, false, false, reel, payout, StepSeconds);
                yield return null;

                float offset = animatorTransform.localPosition.magnitude;
                worstOffset = Mathf.Max(worstOffset, offset);

                if (!_swing.IsActive)
                {
                    break;
                }
            }

            Assert.Less(worstOffset, 0.5f,
                $"the visual rig drifted {worstOffset:0.0}m from the root during a real varied swing");
        }

        /// <summary>
        /// THE SLEEVE MESH USED TO CRUMPLE against the old ~150-degree
        /// reach pose (see HoodedCloak's own UpdateSwingHiding doc
        /// comment) -- that pose no longer exists, but the hide-while-
        /// swinging behaviour itself only ever depended on IsActive, never
        /// on how the arm was posed, so it still runs and is still worth
        /// covering directly.
        /// </summary>
        [UnityTest]
        public IEnumerator AHoodedCloakHidesItselfWhileSwingingAndReappearsOnRelease()
        {
            SpawnPlayer();
#if UNITY_EDITOR
            GameObject cloakPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Wearables/HoodedCloak.prefab");
            Assert.IsNotNull(cloakPrefab, "HoodedCloak.prefab is missing");
            var cloakInstance = Object.Instantiate(cloakPrefab);
            var cloak = cloakInstance.GetComponent<HoodedCloak>();
            cloak.OnEquip(_player, ItemInstanceData.None);

            var combiner = _player.GetComponent<SkinnedGarmentCombiner>();
            Assert.IsNotNull(combiner, "couldn't find the shared equipment combiner");
            Assert.IsTrue(combiner.IsShowing(cloak.ItemId), "setup failed -- the cloak should start showing");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            for (int i = 0; i < 10; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                cloak.UpdateSwingHiding();
                yield return null;
            }
            Assert.IsFalse(combiner.IsShowing(cloak.ItemId), "the cloak's own sleeve is still showing mid-swing");

            _swing.Release(false);
            cloak.UpdateSwingHiding();
            yield return null;

            Assert.IsTrue(combiner.IsShowing(cloak.ItemId), "the cloak never came back after the swing ended");

            Object.Destroy(cloakInstance);
#else
            yield break;
#endif
        }
    }
}
