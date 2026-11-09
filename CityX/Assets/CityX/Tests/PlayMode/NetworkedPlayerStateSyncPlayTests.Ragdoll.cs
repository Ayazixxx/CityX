using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Ragdolls over real sockets (docs/MULTIPLAYER_STATE.md; owner, 2026-10-29: "make it so whatever ragdoll position you are in is the same
    /// on all multiplayer screens ... i want all the ragdolls to be synced up"): A thrown down lies the same on B's screen, bone for bone, and
    /// gets up with it; A hanging from the grapple rope hangs on B's screen too, the rope drawn from the hook in the copy's hand. All the
    /// machines share one scene here, so A's body and B's copy of it are compared where they stand.
    /// </summary>
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        /// <summary>Where the owner's hand was at <paramref name="time"/>, between the two frames either side of it; false outside what was seen.</summary>
        private static bool OwnerAt(System.Collections.Generic.List<(float time, Vector3 at)> path, float time, out Vector3 at)
        {
            at = Vector3.zero;
            for (int i = 1; i < path.Count; i++)
            {
                if (path[i - 1].time <= time && time <= path[i].time)
                {
                    float span = path[i].time - path[i - 1].time;
                    at = Vector3.Lerp(path[i - 1].at, path[i].at, span > 1e-6f ? (time - path[i - 1].time) / span : 0.0f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Every bone of a body, frame by frame, with the time: what the owner did, to measure a copy against the owner as they were at the
        /// moment the copy shows (it shows them a little behind, and a body lying still is sent only twice a second, NetworkedRagdollSync: a
        /// limb settling the last centimetres is on the copy a moment later, as it should be).
        /// </summary>
        private sealed class PoseHistory
        {
            private readonly Transform[] _bones;
            private readonly System.Collections.Generic.List<(float time, Vector3[] at)> _frames = new System.Collections.Generic.List<(float, Vector3[])>();

            public PoseHistory(GameObject body)
            {
                _bones = RagdollMirror.BonesOf(body.GetComponentInChildren<Animator>());
            }

            public void Record()
            {
                var at = new Vector3[_bones.Length];
                for (int i = 0; i < _bones.Length; i++)
                {
                    at[i] = _bones[i].position;
                }
                _frames.Add((Time.time, at));
            }

            /// <summary>The worst gap, bone for bone, between <paramref name="copy"/> now and the body as it was at <paramref name="time"/>; -1 outside what was seen.</summary>
            public float WorstGap(GameObject copy, float time)
            {
                Transform[] copyBones = RagdollMirror.BonesOf(copy.GetComponentInChildren<Animator>());
                Assert.AreEqual(_bones.Length, copyBones.Length, "the two bodies are not the same rig");
                for (int f = 1; f < _frames.Count; f++)
                {
                    if (_frames[f - 1].time > time || time > _frames[f].time)
                    {
                        continue;
                    }
                    float span = _frames[f].time - _frames[f - 1].time;
                    float share = span > 1e-6f ? (time - _frames[f - 1].time) / span : 0.0f;
                    float worst = 0.0f;
                    for (int i = 0; i < copyBones.Length; i++)
                    {
                        worst = Mathf.Max(worst, Vector3.Distance(copyBones[i].position, Vector3.Lerp(_frames[f - 1].at[i], _frames[f].at[i], share)));
                    }
                    return worst;
                }
                return -1.0f;
            }
        }

        [UnityTest]
        public IEnumerator AKnockedDownBodyLiesTheSameOnAnotherPlayersCopyAndGetsUpWithIt()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            GameObject owner = _aOnA.gameObject;
            GameObject copy = _aOnB.gameObject;
            var ownerRagdoll = owner.GetComponent<Ragdoll>();
            var copyMirror = copy.GetComponent<RagdollMirror>();
            Assert.IsNotNull(copyMirror, "the joining player's body cannot be posed as its owner's ragdoll");
            Assert.IsNotNull(copy.GetComponent<NetworkedRagdollSync>(), "the joining player's body has no ragdoll sync");
            // Driven by the test: its reader would also tick the knockdown's own get-up 1.5 s on, before A is measured lying still.
            owner.GetComponent<PlayerInputReader>().enabled = false;

            var history = new PoseHistory(owner);
            owner.GetComponent<PlayerVitals>().KnockDown(new Vector3(0.0f, 3.0f, 5.0f));
            Assert.IsTrue(ownerRagdoll.IsLimp, "setup: A was not thrown down");
            yield return WaitUntil(() =>
            {
                history.Record();
                return copyMirror.IsMirroring;
            }, "B's copy of A to take A's limp pose");

            float still = 0.0f;
            yield return WaitUntil(() =>
            {
                history.Record();
                still = ownerRagdoll.IsRestingOnGround ? still + Time.deltaTime : 0.0f;
                return still > 1.0f;
            }, "A to lie still");
            history.Record();
            float gap = history.WorstGap(copy, copyMirror.PlaybackTime);
            Debug.Log($"CityX networked ragdoll: lying still, B's copy of A within {gap * 100.0f:0.0} cm of A as A was at the moment it shows " +
                      $"({Time.time - copyMirror.PlaybackTime:0.00} s behind), bone for bone");
            Assert.GreaterOrEqual(gap, 0.0f, "the copy shows a moment of A's that was never seen");
            Assert.Less(gap, 0.04f, "lying still, B's copy of A does not lie as A did");
            Assert.IsFalse(copy.GetComponentInChildren<Animator>().enabled, "B's copy of A is animated over A's pose");

            ownerRagdoll.BeginStandingUp(force: true);
            yield return WaitUntil(() =>
            {
                ownerRagdoll.Tick(Time.deltaTime);   // A's input reader would; there is no keyboard here
                return !ownerRagdoll.IsLimp;
            }, "A to get up");
            yield return WaitUntil(() => !copyMirror.IsMirroring && copy.GetComponentInChildren<Animator>().enabled,
                "B's copy of A to get up and be animated again");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        /// <summary>
        /// RECOVERY ON EVERY SCREEN (docs/RECOVERY.md): A, thrown, flips back onto its feet, and B's copy of A flips as A does -- the flip is part
        /// of being down, so it travels with the ragdoll's pose -- posed as A was at the moment it shows, then is its Animator's again, upright.
        /// </summary>
        [UnityTest]
        public IEnumerator ARecoveryFlipShowsOnAnotherPlayersCopy()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            GameObject owner = _aOnA.gameObject;
            GameObject copy = _aOnB.gameObject;
            var ownerRagdoll = owner.GetComponent<Ragdoll>();
            var ownerLocomotion = owner.GetComponent<PlayerLocomotion>();
            var copyMirror = copy.GetComponent<RagdollMirror>();
            owner.GetComponent<PlayerInputReader>().enabled = false;   // driven here: there is no keyboard

            owner.GetComponent<PlayerVitals>().KnockDown(new Vector3(0.0f, 12.0f, 4.0f));
            Assert.IsTrue(ownerRagdoll.IsLimp, "setup: A was not thrown");
            yield return WaitUntil(() =>
            {
                ownerRagdoll.Tick(Time.deltaTime);
                return copyMirror.IsMirroring;
            }, "B's copy of A to take A's limp pose");
            Assert.IsTrue(ownerRagdoll.Recover(owner.transform.forward), "setup: A could not flip up");

            // The copy is measured by which way its body points (hips to neck) against A's at the moment it shows: where it is in the air is
            // the network's (the copy's root follows A's on its own clock, a few centimetres behind at the speed it flies).
            Animator ownerAnimator = owner.GetComponentInChildren<Animator>();
            Animator copyAnimator = copy.GetComponentInChildren<Animator>();
            System.Func<Animator, Vector3> bodyUp = animator =>
                (animator.GetBoneTransform(HumanBodyBones.Neck).position - animator.GetBoneTransform(HumanBodyBones.Hips).position).normalized;
            var ownerUps = new System.Collections.Generic.List<(float time, Vector3 at)>();
            var angles = new System.Collections.Generic.List<float>();
            Vector3 lastUp = bodyUp(copyAnimator);
            float copyTurned = 0.0f;
            for (float t = 0.0f; t < 6.0f && (ownerRagdoll.IsLimp || copyMirror.IsMirroring || !copyAnimator.enabled); t += Time.deltaTime)
            {
                if (ownerRagdoll.IsLimp)
                {
                    ownerRagdoll.Tick(Time.deltaTime);
                }
                else
                {
                    ownerLocomotion.Tick(default, Time.deltaTime);
                }
                yield return null;
                ownerUps.Add((Time.time, bodyUp(ownerAnimator)));
                Vector3 up = bodyUp(copyAnimator);
                copyTurned += Vector3.Angle(lastUp, up);
                lastUp = up;
                if (copyMirror.IsMirroring && OwnerAt(ownerUps, copyMirror.PlaybackTime, out Vector3 ownerUp))
                {
                    angles.Add(Vector3.Angle(ownerUp, up));
                }
            }
            angles.Sort();
            float typical = angles.Count > 0 ? angles[angles.Count / 2] : float.MaxValue;
            Debug.Log($"CityX networked recovery: B's copy of A turned {copyTurned:0} deg (A's flip {ownerRagdoll.Flip.SweepDegrees:0}), pointing " +
                      $"within {typical:0} deg typically of A as A was at the moment it shows ({angles.Count} frames measured)");
            Assert.IsFalse(ownerRagdoll.IsLimp, "A never came out of the flip");
            Assert.IsFalse(copyMirror.IsMirroring, "B's copy of A never came out of it");
            Assert.IsTrue(copyAnimator.enabled, "B's copy of A was not handed back to its Animator");
            Assert.Greater(copyTurned, RecoveryFlip.MinSweepDegrees - 30.0f, "B's copy of A did not flip as A did");
            Assert.Greater(angles.Count, 3, "the copy's moment was never within what A was seen to do");
            Assert.Less(typical, 20.0f, "B's copy of A is not turned as A was at the moment it shows");
            yield return null;
            Assert.Greater(copyAnimator.GetBoneTransform(HumanBodyBones.Head).position.y - copy.transform.position.y, 1.6f, "B's copy of A is not upright");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AGrappleHangShowsOnAnotherPlayersCopyWithItsRope()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            bool modeBefore = GrappleSwing.RagdollMode;
            GrappleSwing.RagdollMode = true;
            try
            {
                GameObject owner = _aOnA.gameObject;
                GameObject copy = _aOnB.gameObject;
                var ownerSwing = owner.GetComponent<GrappleSwing>();
                var copySwing = copy.GetComponent<GrappleSwing>();
                Assert.IsNotNull(ownerSwing, "a joining player cannot swing at all");
                Assert.IsNotNull(copySwing);
                Assert.IsTrue(owner.GetComponent<Inventory>().EquipItemId(ItemRegistry.GrapplingHook, owner), "setup: A could not take the hook");
                yield return WaitUntil(() => copy.GetComponentInChildren<GrapplingHookItem>() != null, "B's copy of A to hold the hook");

                // In the air, as a body that has jumped off a roof: hooked from there it hangs at once.
                var controller = owner.GetComponent<CharacterController>();
                controller.enabled = false;
                owner.transform.position += Vector3.up * 8.0f;
                controller.enabled = true;
                Physics.SyncTransforms();
                Vector3 anchor = owner.transform.position + new Vector3(0.0f, 6.0f, 6.0f);
                ownerSwing.Attach(anchor, Vector3.forward * 5.0f);
                Assert.IsTrue(ownerSwing.IsRoped, "setup: A did not hang from the rope");

                var copyMirror = copy.GetComponent<RagdollMirror>();
                var copyHook = copy.GetComponentInChildren<GrapplingHookItem>();
                yield return WaitUntil(() =>
                {
                    ownerSwing.Tick(Vector2.up, 0.0f, false, false, false, false, Time.deltaTime);
                    return copySwing.IsShowingRemoteRope && copyHook.IsShowingRope && copyMirror.IsMirroring;
                }, "B's copy of A to hang from a rope drawn from its hand");

                // The copy shows A a moment ago (the network's delay and the playback's): on a fast swing that is metres behind. So the copy's
                // hand is measured against where A's hand WAS at the moment the copy is showing (RagdollMirror.PlaybackTime, on A's clock),
                // between the two frames either side of it.
                Transform ownerHand = owner.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                Transform copyHand = copy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                Transform ownerHips = owner.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
                Transform copyHips = copy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
                var path = new System.Collections.Generic.List<(float time, Vector3 at)>();
                var hipsPath = new System.Collections.Generic.List<(float time, Vector3 at)>();
                float worstThen = 0.0f, worstHipsThen = 0.0f, worstBehind = 0.0f, lag = 0.0f, widestGap = 0.0f, lastArrival = Time.time;
                int receivedBefore = copyMirror.Received, received = copyMirror.Received;
                float measured = 0.0f;
                var gaps = new System.Collections.Generic.List<float>();
                for (float t = 0.0f; t < 1.5f; t += Time.deltaTime)
                {
                    ownerSwing.Tick(Vector2.up, 0.0f, false, false, false, false, Time.deltaTime);
                    yield return null;
                    path.Add((Time.time, ownerHand.position));
                    hipsPath.Add((Time.time, ownerHips.position));
                    if (copyMirror.Received != received)
                    {
                        received = copyMirror.Received;
                        widestGap = Mathf.Max(widestGap, Time.time - lastArrival);
                        lastArrival = Time.time;
                    }
                    if (t <= 0.3f || !OwnerAt(path, copyMirror.PlaybackTime, out Vector3 then) || !OwnerAt(hipsPath, copyMirror.PlaybackTime, out Vector3 hipsThen))
                    {
                        continue;
                    }
                    measured += Time.deltaTime;
                    gaps.Add(Vector3.Distance(then, copyHand.position));
                    worstThen = Mathf.Max(worstThen, gaps[gaps.Count - 1]);
                    worstHipsThen = Mathf.Max(worstHipsThen, Vector3.Distance(hipsThen, copyHips.position));
                    worstBehind = Mathf.Max(worstBehind, Vector3.Distance(ownerHand.position, copyHand.position));
                    lag = Mathf.Max(lag, Time.time - copyMirror.PlaybackTime);
                }
                gaps.Sort();
                float typical = gaps.Count > 0 ? gaps[gaps.Count / 2] : float.MaxValue;
                Debug.Log($"CityX networked grapple: B's copy of A's rope hand within {typical:0.00} m typically, {worstThen:0.00} m at worst (hips " +
                          $"{worstHipsThen:0.00} m) of where A's was at the moment it shows ({lag:0.00} s behind A, {worstBehind:0.0} m at most; " +
                          $"{copyMirror.Received - receivedBefore} poses in 1.5 s, {widestGap:0.00} s apart at most)");
                Assert.Greater(measured, 0.5f, "the copy's moment was never within what A's hand was seen to do");
                // Between two poses the copy goes straight while A's hand went round its arc. Poses are sent twenty times a second, but with
                // three machines in one process here some are lost and some come half a second apart, and the chord across a gap like that is
                // a metre off a fast arc: so the copy is held to the typical moment, and to no more than a metre at its worst.
                Assert.Less(typical, 0.2f, "B's copy of A is not posed as A was at the moment it shows");
                Assert.Less(worstThen, 1.0f, "B's copy of A was once far from where A was at the moment it shows");

                ownerSwing.Release(false);
                yield return WaitUntil(() => !copySwing.IsShowingRemoteRope && !copyHook.IsShowingRope, "B's copy of A to let go of the rope");
                Assert.IsFalse(ownerSwing.IsShowingRemoteRope, "A was shown its own rope back");
            }
            finally
            {
                GrappleSwing.RagdollMode = modeBefore;
            }
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
