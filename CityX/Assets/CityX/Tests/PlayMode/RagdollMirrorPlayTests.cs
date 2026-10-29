using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A copy posed as its owner's limp body is (docs/MULTIPLAYER_STATE.md), with no network: the owner's poses are packed and unpacked as the
    /// wire would and handed straight to the copy, which stands 12 m to the side (the poses are moved over by as much). Lying still, the copy
    /// lies exactly as the owner does; getting up, it gets up with it and is its Animator's again; a copy that fell by itself is taken over by
    /// the owner's pose; and a pose from before the owner got up is ignored.
    /// </summary>
    public sealed class RagdollMirrorPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private static readonly Vector3 Offset = new Vector3(12.0f, 0.0f, 0.0f);
        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly RagdollPose _pose = new RagdollPose();
        private readonly RagdollPose _wire = new RagdollPose();
        private float _sinceSend;
        private float _lastSent;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                Physics.SyncTransforms();
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            if (player.GetComponent<RagdollMirror>() == null)
            {
                player.AddComponent<RagdollMirror>();
            }
            player.GetComponent<RagdollMirror>().Bind();
            return player;
#else
            return null;
#endif
        }

        /// <summary>Owner and copy on the ground, standing a moment.</summary>
        private IEnumerator TwoBodies(System.Action<GameObject, GameObject> got)
        {
            GameObject owner = Body(new Vector3(0.0f, 0.05f, 0.0f));
            GameObject copy = Body(new Vector3(0.0f, 0.05f, 0.0f) + Offset);
            for (int i = 0; i < 10; i++)
            {
                owner.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                copy.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            _sinceSend = NetworkedRagdollSync.SendSeconds;
            got(owner, copy);
        }

        /// <summary>The owner's pose, every <see cref="NetworkedRagdollSync.SendSeconds"/>, over the wire's packing, to the copy.</summary>
        private void Send(GameObject owner, GameObject copy, bool force = false)
        {
            _sinceSend += Step;
            if (!force && _sinceSend < NetworkedRagdollSync.SendSeconds)
            {
                return;
            }
            _sinceSend = 0.0f;
            owner.GetComponent<RagdollMirror>().Capture(_pose, Time.time);
            _pose.HipsPosition += Offset;
            _lastSent = _pose.Time;
            var mirror = copy.GetComponent<RagdollMirror>();
            Assert.IsTrue(RagdollPoseCodec.TryUnpack(RagdollPoseCodec.Pack(_pose), mirror.BoneCount, copy.transform.position, _wire), "the copy refused a pose");
            mirror.Receive(_wire);
        }

        private static float WorstBoneGap(GameObject owner, GameObject copy)
        {
            Transform[] ownerBones = RagdollMirror.BonesOf(owner.GetComponentInChildren<Animator>());
            Transform[] copyBones = RagdollMirror.BonesOf(copy.GetComponentInChildren<Animator>());
            Assert.AreEqual(ownerBones.Length, copyBones.Length);
            float worst = 0.0f;
            for (int i = 0; i < ownerBones.Length; i++)
            {
                worst = Mathf.Max(worst, Vector3.Distance(copyBones[i].position - Offset, ownerBones[i].position));
            }
            return worst;
        }

        [UnityTest]
        public IEnumerator LyingStillTheCopyLiesExactlyAsTheOwnerDoes()
        {
            GameObject owner = null, copy = null;
            yield return TwoBodies((o, c) => { owner = o; copy = c; });
            var ownerRagdoll = owner.GetComponent<Ragdoll>();
            var copyMirror = copy.GetComponent<RagdollMirror>();
            ownerRagdoll.Activate(new Vector3(0.0f, 3.0f, 5.0f));
            float worstFalling = 0.0f;
            for (float t = 0.0f; t < 5.0f; t += Step)
            {
                ownerRagdoll.Tick(Step);
                Send(owner, copy);
                yield return null;
                if (t > 0.4f && t < 1.4f)
                {
                    worstFalling = Mathf.Max(worstFalling, Vector3.Distance(copy.GetComponent<Ragdoll>().Torso.position - Offset, ownerRagdoll.Torso.position));
                }
            }
            Assert.IsTrue(ownerRagdoll.IsRestingOnGround, "setup: the owner never came to rest");
            Assert.IsTrue(copyMirror.IsMirroring, "the copy is not posed as its owner");
            Assert.IsTrue(copy.GetComponent<Ragdoll>().IsMirrored);
            Assert.IsFalse(copy.GetComponentInChildren<Animator>().enabled, "the copy's Animator fights the owner's pose");
            Assert.IsFalse(copy.GetComponent<Ragdoll>().IsLimp, "the copy runs physics of its own");
            float lying = WorstBoneGap(owner, copy);
            Debug.Log($"CityX ragdoll mirror: falling, the copy's hips {worstFalling:0.00} m from the owner's at most; lying, every bone within {lying * 100.0f:0.0} cm");
            Assert.Less(lying, 0.02f, "lying still, the copy's bones are not where the owner's are");
            Assert.Less(worstFalling, 1.2f, "falling, the copy is far from the owner (it plays a little behind, not elsewhere)");
        }

        [UnityTest]
        public IEnumerator TheCopyGetsUpWithItsOwnerAndIsItsAnimatorsAgain()
        {
            GameObject owner = null, copy = null;
            yield return TwoBodies((o, c) => { owner = o; copy = c; });
            var ownerRagdoll = owner.GetComponent<Ragdoll>();
            var copyMirror = copy.GetComponent<RagdollMirror>();
            ownerRagdoll.Activate(new Vector3(0.0f, 2.0f, 3.0f));
            for (float t = 0.0f; t < 3.0f; t += Step)
            {
                ownerRagdoll.Tick(Step);
                Send(owner, copy);
                yield return null;
            }
            ownerRagdoll.BeginStandingUp(force: true);
            for (float t = 0.0f; t < 6.0f && ownerRagdoll.IsLimp; t += Step)
            {
                ownerRagdoll.Tick(Step);
                Send(owner, copy, force: !ownerRagdoll.IsLimp);
                yield return null;
            }
            Assert.IsFalse(ownerRagdoll.IsLimp, "setup: the owner never got up");
            copyMirror.End(_lastSent);
            for (float t = 0.0f; t < 1.0f && copyMirror.IsMirroring; t += Step)
            {
                yield return null;
            }
            Assert.IsFalse(copyMirror.IsMirroring, "the copy is still posed after its owner got up");
            Assert.IsFalse(copy.GetComponent<Ragdoll>().IsMirrored);
            Assert.IsTrue(copy.GetComponentInChildren<Animator>().enabled, "the copy's Animator is still off");
            Assert.IsTrue(copy.GetComponent<PlayerLocomotion>().enabled, "the copy's locomotion is still off");
            Assert.IsTrue(copy.GetComponent<CharacterController>().enabled, "the copy's capsule is still off");

            // A pose from before it got up, arriving late (the poses are sent unreliably), is old news.
            owner.GetComponent<RagdollMirror>().Capture(_pose, _lastSent - 0.05f);
            _pose.HipsPosition += Offset;
            copyMirror.Receive(_pose);
            Assert.IsFalse(copyMirror.IsMirroring, "a pose from before the owner got up laid the copy down again");
        }

        [UnityTest]
        public IEnumerator ACopyThatFellByItselfIsTakenOverByTheOwnersPose()
        {
            GameObject owner = null, copy = null;
            yield return TwoBodies((o, c) => { owner = o; copy = c; });
            var ownerRagdoll = owner.GetComponent<Ragdoll>();
            var copyRagdoll = copy.GetComponent<Ragdoll>();
            copyRagdoll.Activate(new Vector3(0.0f, 2.0f, -4.0f));   // the attacker's machine throws the copy it hit at once
            ownerRagdoll.Activate(new Vector3(0.0f, 2.0f, 4.0f));   // and the owner's machine throws the real body its own way
            for (int i = 0; i < 6; i++)
            {
                ownerRagdoll.Tick(Step);
                yield return null;
            }
            Send(owner, copy, force: true);
            Assert.IsTrue(copy.GetComponent<RagdollMirror>().IsMirroring, "the owner's pose did not take the copy over");
            Assert.IsFalse(copyRagdoll.IsLimp, "the copy's own fall goes on under the owner's pose");
            foreach (Rigidbody body in copyRagdoll.Bodies)
            {
                Assert.IsTrue(body == null || body.isKinematic, "a body of the copy is still thrown about by its own physics");
            }
            copyRagdoll.Activate(Vector3.up);   // another blow on the copy while it is posed: the owner's pose stays in charge
            Assert.IsFalse(copyRagdoll.IsLimp, "a blow on a posed copy threw it about on its own again");
            for (float t = 0.0f; t < 4.0f; t += Step)
            {
                ownerRagdoll.Tick(Step);
                Send(owner, copy);
                yield return null;
            }
            Assert.Less(WorstBoneGap(owner, copy), 0.02f, "the copy that fell by itself does not end up lying as its owner does");
        }
    }
}
