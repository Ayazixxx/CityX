using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The ragdoll's get-up from however it lies, on whatever it lies on (owner, 2026-11-10: "make the standing up animation look good. the
    /// ragdoll to stand animation. use hand and foot ik to interact with the terrain it is standing on properly. right now it looks glitchy").
    /// </summary>
    public sealed class GetUpOnTheGroundPlayTests
    {
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

        /// <summary>The clip starts face down. From its back the body used to be turned bone by bone, straight through itself, in 0.4 s.</summary>
        [UnityTest]
        public IEnumerator FromItsBackItRollsOverAlongItsOwnLengthOntoItsFront()
        {
            yield return Lay("level", Vector3.up);
            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), head = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.Greater(Vector3.Dot(LyingBody.ChestFront(animator), Vector3.up), 0.7f, "setup: it is not lying on its back");
            Vector3 lay = Vector3.ProjectOnPlane(head.position - hips.position, Vector3.up).normalized;
            float lyingAt = hips.position.y;

            ragdoll.BeginStandingUp(force: true);
            Assert.Greater(ragdoll.RollSeconds, 0.25f, "from its back it is given no time to roll over");
            yield return null;   // the get-up poses the body after the Animator, in LateUpdate
            Assert.Greater(Vector3.Dot(LyingBody.ChestFront(animator), Vector3.up), 0.6f, "it was turned over on the first frame");
            Quaternion before = hips.rotation;
            float swung = 0.0f, fastest = 0.0f, lowest = float.MaxValue, highest = 0.0f;
            for (float t = 0.0f; t < ragdoll.RollSeconds; t += Time.deltaTime)
            {
                ragdoll.Tick(Time.deltaTime);
                yield return null;
                swung = Mathf.Max(swung, Vector3.Angle(lay, Vector3.ProjectOnPlane(head.position - hips.position, Vector3.up)));
                fastest = Mathf.Max(fastest, Quaternion.Angle(before, hips.rotation));
                before = hips.rotation;
                lowest = Mathf.Min(lowest, hips.position.y);
                highest = Mathf.Max(highest, hips.position.y);
            }
            Assert.Less(Vector3.Dot(LyingBody.ChestFront(animator), Vector3.up), -0.6f, "it did not end the roll on its front");
            Assert.Less(swung, 30.0f, "it swung round on the ground instead of rolling over where it lay");
            Assert.Less(fastest, 25.0f, "the hips snapped round in one frame");
            Assert.Greater(lowest, lyingAt - 0.08f, "the hips sank into the floor as it rolled");
            Assert.Greater(highest, lyingAt + 0.05f, "it rolled over without its hips rising over its side");
        }

        /// <summary>
        /// The clip pushes up, kneels and stands on a level floor at the height of the root. Lying up a staircase, its hands are on steps
        /// above that and its feet on steps below.
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
            float elapsed = 0.0f;
            while (ragdoll.IsLimp && elapsed < 4.0f)
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
                    handUnder = Mathf.Max(handUnder, LimbFloorClamp.WristClearance - over);
                    if (over < LimbFloorClamp.WristClearance + 0.05f)
                    {
                        // Resting on the ground under it: how far that ground is above the level floor the clip was made on.
                        handOnAStepAbove = Mathf.Max(handOnAStepAbove, ground - _player.transform.position.y);
                    }
                }
                foreach (Transform foot in feet)
                {
                    footUnder = Mathf.Max(footUnder, LimbFloorClamp.AnkleClearance - (foot.position.y - LyingBody.GroundUnder(foot.position, _player.transform)));
                }
            }
            Assert.IsFalse(ragdoll.IsLimp, "it never got up");
            Assert.Less(handUnder, 0.03f, "a hand went into a step");
            Assert.Less(footUnder, 0.03f, "a foot went into a step");
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

            // At the entrance's pace: the clip lies still for its first second, long enough to be measured lying.
            ragdoll.BeginStandingUp(force: true, paceSeconds: GetUpPace.ClipSeconds);
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
                Assert.That(over, Is.InRange(0.1f, 0.5f), "a foot is in the slope or in the air");
            }
        }
    }
}
