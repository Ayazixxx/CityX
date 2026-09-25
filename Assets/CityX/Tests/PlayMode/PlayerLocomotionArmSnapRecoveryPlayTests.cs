using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT: "it is imperative that we fix this immediately"
    /// -- the console/grapple stretch, reported as happening again after
    /// seven previous rounds of fixes (see grapple_swing_deprioritized
    /// memory). This test's own history is itself part of that saga's
    /// lesson: a round-7 fix held a bone at its own last plausible rotation
    /// whenever a frame-to-frame change looked too fast, but never advanced
    /// that reference when holding -- correct for a one-frame Mecanim
    /// glitch that self-corrects the next frame, but a LEGITIMATE fast
    /// transition (landing, finishing a swing, a sharp direction change)
    /// tripped the same check and never recovered, permanently locking the
    /// bone. A follow-up patch accepted the live value unconditionally on
    /// the very next call after a hold -- which stopped the lock-up, but
    /// reopened a WORSE, separate gap: GrappleSwing.Attach/DevConsoleView.
    /// Open call SuppressImplausibleArmSnapsNow at the exact instant a pose
    /// gets captured or frozen, and that unconditional-accept-next-call
    /// flag was SHARED with the ordinary per-frame path -- an unrelated
    /// hold moments earlier could make that exact capture call accept
    /// literally anything, with zero validation. A bounded RotateTowards
    /// catch-up was tried next, but that only closes PART of a large jump
    /// per call, so a genuinely big glitch still showed through
    /// substantially once frozen -- the user reported zero improvement.
    ///
    /// ROUND 8's ACTUAL FIX: the on-demand capture path
    /// (SuppressImplausibleArmSnapsNow) now has its OWN, completely
    /// independent hold logic (SuppressBoneSnapForCapture) that never
    /// reads or writes the ordinary per-frame path's own recovery flag.
    /// A full, unconditional hold is safe here specifically because this
    /// method only ever runs ONCE per Attach/Open call -- there is no
    /// repeating per-frame process here to get permanently stuck, so
    /// nothing needs an "accept anyway" escape hatch at all. This test
    /// proves the hold is FULL (not the bounded/partial catch-up that
    /// shipped and didn't help), and that it stays that way call after
    /// call -- exactly what a capture moment needs.
    ///
    /// A REAL [UnityTest], NOT [Test] -- EXPLICIT BUG found live building
    /// this: an EditMode [Test] calling SuppressImplausibleArmSnapsNow
    /// synchronously has no real per-frame Time.deltaTime to measure a
    /// rate against (EditMode has no frame loop at all), so an injected
    /// 170-degree jump measured as an artificially slow rate and was
    /// accepted outright. Matches this project's own established rule for
    /// this exact class of test (LocomotionAnimatorStabilityPlayTests,
    /// GrappleSwingPosePlayTests): Time.captureDeltaTime pinned to a real
    /// frame length is required whenever a rate (degrees per SECOND) is
    /// being measured.
    /// </summary>
    public sealed class PlayerLocomotionArmSnapRecoveryPlayTests
    {
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Transform _rightUpperArm;

        private const float StepSeconds = 1.0f / 60.0f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0.0f;
            if (_player != null)
            {
                Object.Destroy(_player);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator OnDemandCaptureFullyHoldsAnImplausibleJumpAndKeepsHoldingIt()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(_locomotion, "Player.prefab has no PlayerLocomotion");
            _locomotion.Bind();

            Animator animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Player.prefab has no Animator");
            // Disabled -- otherwise Mecanim's own live evaluation would
            // overwrite this test's own precisely-injected rotations
            // before SuppressImplausibleArmSnapsNow ever saw them, the
            // same reason PoseDistortionWatchdogPlayTests.
            // ASuddenBoneSnapIsCaughtAndLogged disables it for its own
            // synthetic injection. Also means PlayerLocomotion's own
            // ordinary per-frame LateUpdate correction never runs here
            // (gated on _animator.enabled) -- exactly what isolates the
            // on-demand path this test targets.
            animator.enabled = false;
            _rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.IsNotNull(_rightUpperArm, "Player.prefab's Animator has no RightUpperArm bone");

            Time.captureDeltaTime = StepSeconds;
            yield return null;

            Quaternion originalPose = _rightUpperArm.localRotation;

            // Bootstrap call: the very first call this component ever
            // makes has nothing yet to plausibly compare against, so it
            // just records the current pose.
            _locomotion.SuppressImplausibleArmSnapsNow();
            yield return null;

            // An instantaneous 170-degree jump -- nothing in this
            // project's own real pose systems moves a bone this far in a
            // single frame (GrappleSwing's own reach eases at 240 degrees
            // per SECOND). Whether it started life as a genuine glitch or
            // a legitimate fast transition is indistinguishable from a
            // single frame's data alone -- and unlike the ordinary
            // per-frame path, this on-demand capture call is not allowed
            // to guess: GrappleSwing.Attach/DevConsoleView.Open call this
            // exact method at the exact instant a pose gets captured or
            // frozen for as long as a swing/console stays engaged, so it
            // must always reject an implausible jump outright.
            Quaternion transitionedPose = originalPose * Quaternion.Euler(170.0f, 0.0f, 0.0f);
            _rightUpperArm.localRotation = transitionedPose;
            _locomotion.SuppressImplausibleArmSnapsNow();

            Assert.AreEqual(originalPose, _rightUpperArm.localRotation,
                "an implausible jump must be held FULLY at the last known-good pose -- a bounded/partial " +
                "catch-up still leaves most of a large glitch visible once the console/swing freezes it");

            // Calling it again with the SAME implausible destination must
            // hold again too -- this is a ONE-SHOT capture call, not a
            // repeating per-frame process, so there is no "next frame"
            // concept to ever justify passing this exact jump through
            // unvalidated. (A genuinely NEW, later Attach/Open call made
            // after real time has passed -- and the ordinary per-frame
            // path has had a chance to track legitimate intervening
            // motion -- is a different scenario, not exercised here.)
            _rightUpperArm.localRotation = transitionedPose;
            _locomotion.SuppressImplausibleArmSnapsNow();

            Assert.AreEqual(originalPose, _rightUpperArm.localRotation,
                "a repeated on-demand call with the same implausible destination must keep holding -- " +
                "it must never start accepting it just because an earlier call also rejected it");
#else
            yield break;
#endif
        }
    }
}
