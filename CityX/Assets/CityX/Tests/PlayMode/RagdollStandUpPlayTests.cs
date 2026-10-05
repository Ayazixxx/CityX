using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Getting up from ANY position: the real rig is dropped in several different lying poses (face up, face down, on its side, head-first), then stands
    /// up. The first frame of the recovery must still look like the ragdoll (no pop to the clip's pose: its bones blend from the captured pose), the
    /// body must not teleport, and it must end upright with control back. Also the knockdown-timer path (PlayerVitals), which used to snap straight up.
    /// </summary>
    public sealed class RagdollStandUpPlayTests
    {
        private GameObject _ground;
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

#if UNITY_EDITOR
        private Ragdoll Spawn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.3f, 0.0f), Quaternion.identity);
            return _player.GetComponent<Ragdoll>();
        }
#endif

        private static float MaxBoneAngle(Animator animator, Quaternion[] before)
        {
            float max = 0.0f;
            int i = 0;
            for (int b = 0; b < (int)HumanBodyBones.LastBone; b++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)b);
                if (bone == null) continue;
                max = Mathf.Max(max, Quaternion.Angle(before[i++], bone.rotation));
            }
            return max;
        }

        private static Quaternion[] Rotations(Animator animator)
        {
            var list = new System.Collections.Generic.List<Quaternion>();
            for (int b = 0; b < (int)HumanBodyBones.LastBone; b++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)b);
                if (bone != null) list.Add(bone.rotation);
            }
            return list.ToArray();
        }

        [UnityTest]
        public IEnumerator StandingUpFromAnyLyingPoseStartsFromThatPoseAndEndsUpright(
            [ValueSource(nameof(Poses))] Vector3 euler)
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn();
            Animator animator = _player.GetComponentInChildren<Animator>();
            yield return null;
            ragdoll.Activate(Vector3.zero, allowImmediateStandUp: true);
            ragdoll.Torso.transform.rotation = Quaternion.Euler(euler);
            ragdoll.Torso.transform.position += Vector3.up * 0.6f;
            yield return new WaitForSeconds(2.0f);
            Assert.IsTrue(ragdoll.IsLimp);

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 hipsBefore = hips.position;
            Vector3 headBefore = head.position;
            Quaternion[] poseBefore = Rotations(animator);

            ragdoll.BeginStandingUp(force: true);
            Assert.IsTrue(ragdoll.IsStandingUp);
            yield return null;   // one frame in: the blend has had its LateUpdate

            Assert.Less(Vector3.Distance(hips.position, hipsBefore), 0.35f, $"{euler}: the hips jumped when standing began");
            Assert.Less(Vector3.Distance(head.position, headBefore), 0.6f, $"{euler}: the head jumped when standing began");
            Assert.Less(MaxBoneAngle(animator, poseBefore), 35.0f, $"{euler}: a bone snapped to the clip's pose on the first frame");

            for (float t = 0.0f; t < Ragdoll.StandUpRecoverySeconds + 0.4f; t += Time.deltaTime)
            {
                ragdoll.Tick(Time.deltaTime);
                yield return null;
            }
            Assert.IsFalse(ragdoll.IsLimp, $"{euler}: never finished standing up");
            Assert.Greater(head.position.y - _player.transform.position.y, 1.2f, $"{euler}: not upright at the end");
            Assert.Less(Vector3.Angle(_player.transform.up, Vector3.up), 5.0f);
#else
            yield break;
#endif
        }

        private static readonly Vector3[] Poses =
        {
            new Vector3(90.0f, 0.0f, 0.0f),
            new Vector3(-90.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 90.0f),
            new Vector3(90.0f, 135.0f, 0.0f),
        };

        /// <summary>REPORTED: "after standing up the character freezes for a second and can't move." The clip's last third is the character standing still.</summary>
        [UnityTest]
        public IEnumerator ControlComesBackAsSoonAsTheBodyIsUpAndTheAnimatorIsHandedToLocomotion()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn();
            Animator animator = _player.GetComponentInChildren<Animator>();
            yield return null;
            ragdoll.Activate(Vector3.zero, allowImmediateStandUp: true);
            yield return new WaitForSeconds(1.5f);
            ragdoll.BeginStandingUp(force: true);

            float elapsed = 0.0f;
            while (ragdoll.IsLimp && elapsed < 4.0f)
            {
                ragdoll.Tick(Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.Less(elapsed, 1.5f, "the body was held after it was already standing (the clip's idle tail)");
            Assert.IsTrue(_player.GetComponent<CharacterController>().enabled, "control did not come back");
            yield return null;
            yield return null;
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(0).IsName("GettingUp") && !animator.IsInTransition(0),
                "the animator was left holding the get-up pose instead of being handed to locomotion");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AFinisherKnockdownGetsUpWithTheAnimationNotAPop()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn();
            var vitals = _player.GetComponent<PlayerVitals>();
            yield return null;
            vitals.TriggerFinisherKnockdown(Vector3.forward);
            Assert.IsTrue(ragdoll.IsLimp);
            for (float t = 0.0f; t < PlayerVitals.FinisherRagdollSeconds + 0.3f; t += Time.deltaTime)
            {
                vitals.TickHitstun(Time.deltaTime);
                yield return null;
            }
            Assert.IsTrue(ragdoll.IsStandingUp || ragdoll.IsBlendingToStandUp, "the knockdown ended with a pop to standing instead of the get-up animation");
#else
            yield break;
#endif
        }
    }
}
