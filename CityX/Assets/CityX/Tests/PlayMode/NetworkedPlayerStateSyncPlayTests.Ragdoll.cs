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

        private static float WorstBoneGap(GameObject a, GameObject b)
        {
            Transform[] aBones = RagdollMirror.BonesOf(a.GetComponentInChildren<Animator>());
            Transform[] bBones = RagdollMirror.BonesOf(b.GetComponentInChildren<Animator>());
            Assert.AreEqual(aBones.Length, bBones.Length, "the two bodies are not the same rig");
            float worst = 0.0f;
            for (int i = 0; i < aBones.Length; i++)
            {
                worst = Mathf.Max(worst, Vector3.Distance(aBones[i].position, bBones[i].position));
            }
            return worst;
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

            owner.GetComponent<PlayerVitals>().KnockDown(new Vector3(0.0f, 3.0f, 5.0f));
            Assert.IsTrue(ownerRagdoll.IsLimp, "setup: A was not thrown down");
            yield return WaitUntil(() => copyMirror.IsMirroring, "B's copy of A to take A's limp pose");

            float still = 0.0f;
            yield return WaitUntil(() =>
            {
                still = ownerRagdoll.IsRestingOnGround ? still + Time.deltaTime : 0.0f;
                return still > 1.0f;
            }, "A to lie still");
            float gap = WorstBoneGap(owner, copy);
            Debug.Log($"CityX networked ragdoll: lying still, B's copy of A within {gap * 100.0f:0.0} cm of A, bone for bone");
            Assert.Less(gap, 0.04f, "lying still, B's copy of A does not lie as A does");
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
                    worstThen = Mathf.Max(worstThen, Vector3.Distance(then, copyHand.position));
                    worstHipsThen = Mathf.Max(worstHipsThen, Vector3.Distance(hipsThen, copyHips.position));
                    worstBehind = Mathf.Max(worstBehind, Vector3.Distance(ownerHand.position, copyHand.position));
                    lag = Mathf.Max(lag, Time.time - copyMirror.PlaybackTime);
                }
                Debug.Log($"CityX networked grapple: B's copy of A's rope hand within {worstThen:0.00} m (hips {worstHipsThen:0.00} m) of where A's was at " +
                          $"the moment it shows ({lag:0.00} s behind A, {worstBehind:0.0} m at most; {copyMirror.Received - receivedBefore} poses in 1.5 s, " +
                          $"{widestGap:0.00} s apart at most)");
                Assert.Greater(measured, 0.5f, "the copy's moment was never within what A's hand was seen to do");
                // Between two poses the copy goes straight while A's hand went round its arc; with three machines in one process here poses
                // arrive up to ~0.3 s apart, and the chord across that is about this far off the arc.
                Assert.Less(worstThen, 0.5f, "B's copy of A is not posed as A was at the moment it shows");

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
