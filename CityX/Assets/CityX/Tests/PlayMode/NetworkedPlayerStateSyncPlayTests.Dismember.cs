using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Dismemberment over real sockets (docs/DISMEMBERMENT.md): B's sword kills A with plenty to spare by striking B's copy of A; A's own body,
    /// where the death is decided, loses its head, B's copy of A loses it too; B hacking on at the corpse takes the forearm off on both; and
    /// both are whole again when A is revived.
    /// </summary>
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        [UnityTest]
        public IEnumerator ASwordsOverkillTakesTheSameHeadOffOnEveryScreen()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            GameObject owner = _aOnA.gameObject;
            GameObject copy = _aOnB.gameObject;
            var ownerVitals = owner.GetComponent<PlayerVitals>();
            var copyVitals = copy.GetComponent<PlayerVitals>();
            var ownerDismemberer = owner.GetComponent<Dismemberer>();
            var copyDismemberer = copy.GetComponent<Dismemberer>();
            Assert.IsNotNull(copyDismemberer, "the joining player's body cannot be taken apart");

            ownerVitals.TakeDamage(ownerVitals.Health - 20.0f);
            Animator animator = copy.GetComponentInChildren<Animator>();
            Vector3 neck = Vector3.Lerp(animator.GetBoneTransform(HumanBodyBones.Neck).position,
                animator.GetBoneTransform(HumanBodyBones.Head).position, 1.2f);
            using (DamageContext.Cutting(true))
            {
                DamageContext.Strike(copyVitals, NetworkedDamage.MaxHitDamage, neck + copy.transform.forward * 1.5f, 0.0f, DamageKind.Physical, 1.0f, neck);
            }
            yield return WaitUntil(() => ownerVitals.IsDead, "B's blow on its copy of A to reach A");
            Assert.AreEqual(Dismemberment.Sever, ownerDismemberer.Last, "A died to a sword's overkill and was not cut");
            Assert.AreEqual(BodyPart.Head, ownerDismemberer.LastPart, "a sword through A's neck did not take A's head");
            yield return WaitUntil(() => copyDismemberer.Last == Dismemberment.Sever, "B's copy of A to lose its head");
            Assert.AreEqual(BodyPart.Head, copyDismemberer.LastPart, "B's copy of A lost a different part");
            Assert.IsNotNull(copyDismemberer.LastPiece, "B's copy of A's head did not fly off");

            // B goes on hacking at its copy of A's corpse: each blow goes to A's machine, which takes the forearm off, and B's copy follows.
            // Once the corpse lies still, so B's copy shows it where it is (a falling body's copy is a moment behind it).
            yield return WaitUntil(() => copyVitals.IsDead, "B's copy of A to know A is dead");
            var ownerRagdoll = owner.GetComponent<Ragdoll>();
            float still = 0.0f;
            yield return WaitUntil(() =>
            {
                still = ownerRagdoll.IsRestingOnGround ? still + Time.deltaTime : 0.0f;
                return still > 1.0f;
            }, "A's corpse to lie still");
            // Where A's forearm is (B's copy shows it a moment behind): this is the relay of a blow on a corpse, not the copy's pose, under test.
            Animator ownerAnimator = owner.GetComponentInChildren<Animator>();
            Vector3 forearm = Vector3.Lerp(ownerAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position,
                ownerAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position, 0.5f);
            using (DamageContext.Cutting(true))
            {
                DamageContext.Strike(copyVitals, 40.0f, forearm + copy.transform.forward * 1.5f, 0.0f, DamageKind.Physical, 1.0f, forearm);
            }
            yield return WaitUntil(() => !ownerDismemberer.Has(BodyPart.LeftLowerArm), "B's blow on the corpse to take A's forearm off");
            yield return WaitUntil(() => !copyDismemberer.Has(BodyPart.LeftLowerArm), "B's copy of A to lose the forearm too");
            Assert.IsTrue(ownerDismemberer.Has(BodyPart.LeftUpperArm), "the blow on the forearm took the whole arm");

            ownerVitals.Revive();
            Assert.IsTrue(ownerDismemberer.IsWhole, "A revived in pieces");
            yield return WaitUntil(() => copyDismemberer.IsWhole, "B's copy of A to be whole again");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
