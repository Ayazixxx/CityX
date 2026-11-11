using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The ragdoll's get-up from however it lies, on whatever it lies on (owner, 2026-11-10: "make the standing up animation look good. the
    /// ragdoll to stand animation. use hand and foot ik to interact with the terrain it is standing on properly. right now it looks glitchy";
    /// "i think it needs a better standing up animation").
    /// </summary>
    public sealed class GetUpOnTheGroundPlayTests
    {
        /// <summary>How far over the ground under it a wrist and an ankle are, resting their skin on it (the clips' own contacts).</summary>
        private const float WristOverTheGround = 0.06f;

        private const float AnkleOverTheGround = 0.09f;

        private GameObject _ground;
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private IEnumerator Lay(string ground, Vector3 chestFaces)
        {
#if UNITY_EDITOR
            _ground = LyingBody.BuildGround(ground);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.3f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            yield return null;
            ragdoll.Activate(Vector3.zero, allowImmediateStandUp: true);
            yield return new WaitForFixedUpdate();
            LyingBody.LayDown(ragdoll, _player.GetComponentInChildren<Animator>(), chestFaces);
            yield return new WaitForSeconds(2.5f);
#else
            yield break;
#endif
        }

        /// <summary>
        /// The fall lands the new character on its back, and the only clip there was started face down: it was rolled over first, turned bone
        /// by bone. Now a body on its back gets up from its back, where it lies, eased into the clip with nothing snapping round.
        /// </summary>
        [UnityTest]
        public IEnumerator FromItsBackItGetsUpFromItsBackWhereItLies([Values(true, false)] bool onItsBack)
        {
            yield return Lay("level", onItsBack ? Vector3.up : Vector3.down);
            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), head = animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 lay = Vector3.ProjectOnPlane(head.position - hips.position, Vector3.up).normalized;
            Vector3 lyingHips = hips.position;

            ragdoll.BeginStandingUp(force: true);
            Assert.AreEqual(onItsBack ? GetUpMotion.FromTheBack : GetUpMotion.FromTheFront, ragdoll.GetUpSegment);
            Assert.Less(ragdoll.RollSeconds, 0.15f, "lying as its clip starts, it is rolled over anyway");
            yield return null;   // the get-up poses the body after the Animator, in LateUpdate
            Quaternion before = hips.rotation;
            float swung = 0.0f, fastest = 0.0f, slid = 0.0f;
            for (float t = 0.0f; t < Ragdoll.SettleSeconds; t += Time.deltaTime)
            {
                ragdoll.Tick(Time.deltaTime);
                yield return null;
                swung = Mathf.Max(swung, Vector3.Angle(lay, Vector3.ProjectOnPlane(head.position - hips.position, Vector3.up)));
                fastest = Mathf.Max(fastest, Quaternion.Angle(before, hips.rotation));
                before = hips.rotation;
                slid = Mathf.Max(slid, Vector3.ProjectOnPlane(hips.position - lyingHips, Vector3.up).magnitude);
            }
            Assert.Less(swung, 30.0f, "it swung round on the ground as it went into the clip");
            Assert.Less(fastest, 15.0f, "the hips snapped round in one frame");
            Assert.Less(slid, 0.4f, "the hips slid off where they lay");

            float elapsed = 0.0f;
            while (ragdoll.IsLimp && elapsed < Ragdoll.StandUpRecoverySeconds + 1.0f)
            {
                ragdoll.Tick(Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(ragdoll.IsLimp, "it never got up");
            yield return null;
            Assert.Greater(Vector3.Dot(head.position - hips.position, Vector3.up), 0.9f * (head.position - hips.position).magnitude, "it is not standing");
        }

        /// <summary>
        /// The clips push up, kneel and stand on a level floor at the height of the root. Lying up a staircase, its hands are on steps above
        /// that and its feet on steps below.
        /// </summary>
        [UnityTest]
        public IEnumerator OnStairsItsHandsAndFeetAreOnTheStepsUnderThemNotOnALevelFloor()
        {
            yield return Lay("stairs", Vector3.down);
            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform[] hands = { animator.GetBoneTransform(HumanBodyBones.LeftHand), animator.GetBoneTransform(HumanBodyBones.RightHand) };
            Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };

            ragdoll.BeginStandingUp(force: true);
            float handUnder = 0.0f, footUnder = 0.0f, handOnAStepAbove = 0.0f;
            string footWhere = "";
            float elapsed = 0.0f;
            while (ragdoll.IsLimp && elapsed < Ragdoll.StandUpRecoverySeconds + 1.0f)
            {
                ragdoll.Tick(Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
                if (elapsed < Ragdoll.SettleSeconds)
                {
                    continue;   // still easing out of the pose the ragdoll lay in
                }
                foreach (Transform hand in hands)
                {
                    float ground = LyingBody.GroundUnder(hand.position, _player.transform);
                    float over = hand.position.y - ground;
                    handUnder = Mathf.Max(handUnder, WristOverTheGround - over);
                    if (over < WristOverTheGround + 0.08f)
                    {
                        // Resting on the ground under it: how far that ground is above the level floor the clip was made on.
                        handOnAStepAbove = Mathf.Max(handOnAStepAbove, ground - _player.transform.position.y);
                    }
                }
                foreach (Transform foot in feet)
                {
                    float under = AnkleOverTheGround - (foot.position.y - LyingBody.GroundUnder(foot.position, _player.transform));
                    if (under > footUnder)
                    {
                        footUnder = under;
                        footWhere = $"{foot.name} at {ragdoll.GetUpClipSeconds:0.00} s of {ragdoll.GetUpSegment}, {foot.position.y - _player.transform.position.y:0.00} m over the root, the step under it {LyingBody.GroundUnder(foot.position, _player.transform) - _player.transform.position.y:0.00}";
                    }
                }
            }
            Assert.IsFalse(ragdoll.IsLimp, "it never got up");
            Assert.Less(handUnder, 0.03f, "a hand went into a step");
            Assert.Less(footUnder, 0.03f, $"a foot went into a step: {footWhere}");
            Assert.Greater(handOnAStepAbove, LyingBody.Riser * 0.8f, "no hand was ever put down on a step above the one its hips lay on");
        }

        /// <summary>Lying on a slope the pose lies along it; the clip alone lay level, its head in the hill or its feet in the air.</summary>
        [UnityTest]
        public IEnumerator OnASlopeItLiesAlongTheSlopeBeforeItPushesUp()
        {
            yield return Lay("slope", Vector3.down);
            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };

            // At the entrance's pace: the clip lies flat while its hands come in under the chest, long enough to be measured lying.
            ragdoll.BeginStandingUp(force: true, speed: IntroCutscene.GettingUpSpeed);
            for (float t = 0.0f; t < Ragdoll.SettleSeconds + 0.05f; t += Time.deltaTime)
            {
                ragdoll.Tick(Time.deltaTime);
                yield return null;
            }
            // Just settled into the clip, not yet pushed up: the trunk and the feet are as far over the slope as each other.
            float hipsOver = hips.position.y - LyingBody.GroundUnder(hips.position, _player.transform);
            float chestOver = chest.position.y - LyingBody.GroundUnder(chest.position, _player.transform);
            Assert.That(hipsOver, Is.InRange(0.1f, 0.5f), "the hips are in the slope or floating over it");
            Assert.That(chestOver, Is.InRange(0.1f, 0.6f), "the chest is in the slope or floating over it");
            foreach (Transform foot in feet)
            {
                float over = foot.position.y - LyingBody.GroundUnder(foot.position, _player.transform);
                Assert.That(over, Is.InRange(0.05f, 0.5f), "a foot is in the slope or in the air");
            }
        }
    }
}
