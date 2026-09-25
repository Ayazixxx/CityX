using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, with a screenshot: "this is the current idle pose,
    /// are we sure this is 2hsw idle? it should be moving slightly but its
    /// frozen." It was the right clip -- imported with loop OFF, so the
    /// 2-second breathing idle played once and then held its last frame
    /// forever. Checked on the REAL Player.prefab's Animator, sampling the body
    /// over real time: a frozen idle is a body that stops moving.
    /// </summary>
    public sealed class SwordIdleAnimationPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private void Build()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null)
            {
                reader.enabled = false;
            }
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
#endif
        }

        /// <summary>Total distance travelled by a set of bones over `seconds`, stepping the real animator.</summary>
        private IEnumerator Travel(LocomotionInput input, float seconds, System.Action<float> result)
        {
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
            // Chest is an optional humanoid bone and this rig maps none -- the
            // spine is always there.
            Transform spine = _animator.GetBoneTransform(HumanBodyBones.Spine)
                              ?? _animator.GetBoneTransform(HumanBodyBones.Hips);
            Quaternion lastSpine = spine.localRotation;
            Vector3 lastHand = _player.transform.InverseTransformPoint(hand.position);
            Vector3 lastHead = _player.transform.InverseTransformPoint(head.position);
            float total = 0.0f;

            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                _locomotion.Tick(input, dt);
                yield return null;
                elapsed += dt;

                Vector3 h = _player.transform.InverseTransformPoint(hand.position);
                Vector3 hd = _player.transform.InverseTransformPoint(head.position);
                total += (h - lastHand).magnitude + (hd - lastHead).magnitude
                         + Quaternion.Angle(lastSpine, spine.localRotation) * 0.01f;
                lastHand = h;
                lastHead = hd;
                lastSpine = spine.localRotation;
            }
            result(total);
        }

        [UnityTest]
        public IEnumerator TheSwordIdleKeepsMovingInsteadOfFreezingOnItsLastFrame()
        {
#if UNITY_EDITOR
            Build();
            var idle = new LocomotionInput { SwordEquipped = true };

            // Settle into the sword idle, and then well past one 2s pass of the
            // clip -- a non-looping clip is frozen by then.
            for (float t = 0.0f; t < 4.0f;)
            {
                float dt = Time.deltaTime;
                _locomotion.Tick(idle, dt);
                t += dt;
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("SwordLocomotion"),
                "setup: the body should be in the sword idle");

            float travelled = 0.0f;
            yield return Travel(idle, 2.0f, d => travelled = d);

            Assert.Greater(travelled, 0.004f,
                $"the sword idle is frozen -- the body moved only {travelled:0.0000} in 2 seconds of standing still");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSwordWalkCycleKeepsCyclingPastOneStride()
        {
#if UNITY_EDITOR
            Build();
            var walk = new LocomotionInput { SwordEquipped = true, Move = new Vector2(0.0f, 1.0f) };

            for (float t = 0.0f; t < 4.0f;)
            {
                float dt = Time.deltaTime;
                _locomotion.Tick(walk, dt);
                t += dt;
                yield return null;
            }

            float travelled = 0.0f;
            yield return Travel(walk, 2.0f, d => travelled = d);

            // Walking swings the arms and head every stride; a frozen cycle is
            // a body that has stopped animating while still sliding along.
            Assert.Greater(travelled, 0.5f,
                $"the sword walk stopped animating after a stride -- only {travelled:0.000} of bone travel in 2 seconds");
#else
            yield break;
#endif
        }
    }
}
