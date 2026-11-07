using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Dismemberment on a real body (docs/DISMEMBERMENT.md): a blade's killing blow with plenty to spare cuts the part it hit off (it flies, a
    /// stump stays), anything else's bursts it into gore, a bare kill or a jet of water takes nothing, a burst on the chest takes the whole body,
    /// revived the body is whole, and another player's copy told of it is taken apart, and made whole, the same way.
    /// </summary>
    public sealed partial class DismembermentPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            _made.Clear();
            foreach (Debris debris in Object.FindObjectsByType<Debris>(FindObjectsSortMode.None))
            {
                Object.Destroy(debris.gameObject);
            }
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
            Assert.IsNotNull(player.GetComponent<Dismemberer>(), "the player's body cannot be taken apart");
            player.GetComponent<PlayerVitals>().Revive();
            return player;
#else
            Assert.Ignore("needs the editor to load the player prefab.");
            return null;
#endif
        }

        private static IEnumerator Stand(GameObject body)
        {
            for (int i = 0; i < 8; i++)
            {
                body.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static Vector3 BonePoint(GameObject body, HumanBodyBones from, HumanBodyBones to, float share)
        {
            Animator animator = body.GetComponentInChildren<Animator>();
            return Vector3.Lerp(animator.GetBoneTransform(from).position, animator.GetBoneTransform(to).position, share);
        }

        /// <summary>
        /// Brings <paramref name="body"/> down to <paramref name="healthLeft"/> (not a blow), then kills it with a blow at <paramref name="at"/>
        /// that does <paramref name="overkill"/> more than that.
        /// </summary>
        private static void Kill(GameObject body, Vector3 at, float overkill, bool edged, bool bloodless = false, float healthLeft = 10.0f)
        {
            var vitals = body.GetComponent<PlayerVitals>();
            vitals.TakeDamage(vitals.Health - healthLeft);
            Hit(body, at, healthLeft + overkill, edged, bloodless);
            Assert.IsTrue(vitals.IsDead, "setup: the blow did not kill");
        }

        /// <summary>A blow of <paramref name="damage"/> on <paramref name="body"/> at <paramref name="at"/>, from behind it.</summary>
        private static void Hit(GameObject body, Vector3 at, float damage, bool edged, bool bloodless = false)
        {
            using (DamageContext.Cutting(edged))
            using (new DamageContext.BloodlessScope(bloodless))
            {
                DamageContext.Strike(body.GetComponent<PlayerVitals>(), damage, at + Vector3.back * 1.5f, 0.0f, DamageKind.Physical, 1.0f, at);
            }
        }

        private static int SkinTriangles(GameObject body)
        {
            foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name == BodySurface.SkinName)
                {
                    return skin.sharedMesh.triangles.Length / 3;
                }
            }
            return -1;
        }

        private static Renderer Skull(GameObject body)
        {
            Transform head = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            Transform skull = head.Find(BodySurface.HeadName);
            return skull != null ? skull.GetComponent<Renderer>() : null;
        }

        [UnityTest]
        public IEnumerator ABladesOverkillCutsTheForearmOffAndItFlies()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            int whole = SkinTriangles(body);
            Kill(body, BonePoint(body, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.5f), 60.0f, edged: true);
            Assert.AreEqual(Dismemberment.Sever, dismemberer.Last, "a sword's overkill did not cut");
            Assert.AreEqual(BodyPart.LeftLowerArm, dismemberer.LastPart, "it cut the wrong part");
            Assert.IsNotNull(dismemberer.LastPiece, "nothing flew off");
            Assert.IsNotNull(dismemberer.LastPiece.GetComponent<Rigidbody>(), "the cut-off forearm does not fall");
            Assert.Greater(dismemberer.LastPiece.GetComponentsInChildren<MeshRenderer>().Length, 0, "the cut-off forearm is invisible");
            Assert.Less(SkinTriangles(body), whole, "the forearm is still on the body");
            Transform upperArm = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Assert.IsNotNull(upperArm.GetComponentInChildren<StumpBleed>(), "no stump where the forearm came off");
            Vector3 startedAt = dismemberer.LastPiece.transform.position;
            yield return Wait(0.5f);
            Assert.Greater(Vector3.Distance(startedAt, dismemberer.LastPiece.transform.position), 0.3f, "the cut-off forearm did not fly");
        }

        [UnityTest]
        public IEnumerator ABluntOverkillBurstsTheHeadIntoGore()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            Assert.IsTrue(Skull(body) != null && Skull(body).enabled, "setup: no skull");
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 60.0f, edged: false);
            Assert.AreEqual(Dismemberment.Burst, dismemberer.Last, "a club's overkill did not burst");
            Assert.AreEqual(BodyPart.Head, dismemberer.LastPart);
            Assert.IsNull(dismemberer.LastPiece, "a burst head flew off whole");
            Assert.Greater(dismemberer.GibsThrown, 5, "the head burst into too little gore");
            Assert.IsFalse(Skull(body).enabled, "the skull is still on the body");
        }

        [UnityTest]
        public IEnumerator ABareKillOrAJetOfWaterTakesNothingOff()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 1.0f, edged: true);
            Assert.AreEqual(Dismemberment.None, dismemberer.Last, "a bare kill took something off");
            Assert.IsTrue(dismemberer.IsWhole);

            GameObject wet = Body(new Vector3(6.0f, 0.0f, 0.0f));
            yield return Stand(wet);
            Kill(wet, BonePoint(wet, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 90.0f, edged: false, bloodless: true);
            Assert.IsTrue(wet.GetComponent<Dismemberer>().IsWhole, "a jet of water burst a head");
        }

        [UnityTest]
        public IEnumerator AKillingBlowMustDoDoubleWhatWasLeft()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 20.0f, edged: true, healthLeft: 30.0f);
            Assert.IsTrue(body.GetComponent<Dismemberer>().IsWhole, "a blow of 50 on a body with 30 left took its head");

            GameObject other = Body(new Vector3(6.0f, 0.0f, 0.0f));
            yield return Stand(other);
            Kill(other, BonePoint(other, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 40.0f, edged: true, healthLeft: 30.0f);
            Assert.AreEqual(BodyPart.Head, other.GetComponent<Dismemberer>().LastPart, "a blow of 70 on a body with 30 left did not take its head");
        }

        /// <summary>The Knight Helmet on <paramref name="body"/>'s head, worn as the swordsman wears it.</summary>
        private GameObject PutOnHelmet(GameObject body)
        {
#if UNITY_EDITOR
            var helmet = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/KnightHelmet.prefab"));
            _made.Add(helmet);
            helmet.GetComponent<CityX.Items.IEquippableItem>().OnEquip(body, CityX.Items.ItemInstanceData.None);
            return helmet;
#else
            return null;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-06: "probably having armor makes you harder to be dismembered and if you do still get dismembered badly we could have
        /// the armor just come off the body and turn into its object form" (docs/ARMOUR.md). A heavy helmet makes the head take three times as
        /// much; a blow past that takes the head, and the helmet falls off as the item, for anyone to pick up.
        /// </summary>
        [UnityTest]
        public IEnumerator AHelmetHoldsTheHeadOnAndFallsOffAsItsItemWhenTheHeadGoesAnyway()
        {
            DroppedItems.Shared.Clear();
            GameObject body = Body(Vector3.zero);
            PutOnHelmet(body);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            Assert.AreEqual(1.0f, dismemberer.ArmourOn(BodyPart.Head), 1e-4f, "the Knight Helmet is not heavy armour on the head");
            Assert.AreEqual(0.0f, dismemberer.ArmourOn(BodyPart.RightLowerArm), 1e-4f, "a helmet covers an arm");
            // 70 on 30 takes a bare head (AKillingBlowMustDoDoubleWhatWasLeft); under the helmet it stays on.
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 40.0f, edged: true, healthLeft: 30.0f);
            Assert.IsTrue(dismemberer.IsWhole, "a blow that takes a bare head took a helmeted one");
            Assert.AreEqual(1, body.GetComponentsInChildren<CityX.Wearables.KnightHelmet>().Length);

            GameObject other = Body(new Vector3(6.0f, 0.0f, 0.0f));
            PutOnHelmet(other);
            yield return Stand(other);
            var otherDismemberer = other.GetComponent<Dismemberer>();
            Kill(other, BonePoint(other, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 100.0f, edged: true, healthLeft: 30.0f);
            Assert.AreEqual(BodyPart.Head, otherDismemberer.LastPart, "a blow past three times what was needed did not take the helmeted head");
            Assert.AreEqual(1, otherDismemberer.PiecesPoppedOff, "the helmet did not come off as its item");
            yield return null;
            Assert.AreEqual(0, other.GetComponentsInChildren<CityX.Wearables.KnightHelmet>().Length, "the helmet is still on the body");
            bool dropped = false;
            foreach (ItemDrop drop in DroppedItems.Shared.Current())
            {
                dropped |= drop.ItemId == CityX.Items.ItemRegistry.KnightHelmet;
            }
            Assert.IsTrue(dropped, "the helmet is not lying there to be picked up");
            DroppedItems.Shared.Clear();
        }

        [UnityTest]
        public IEnumerator HittingACorpseTakesItApartPartByPart()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            Kill(body, BonePoint(body, HumanBodyBones.Hips, HumanBodyBones.Neck, 0.5f) + Vector3.forward * 0.1f, 1.0f, edged: false);
            Assert.IsTrue(dismemberer.IsWhole, "setup: a bare kill took something off");
            yield return Wait(1.0f);   // down on the ground

            // A club on the head: 12 is less than the head can take, 24 is more.
            Hit(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 12.0f, edged: false);
            Assert.IsTrue(dismemberer.Has(BodyPart.Head), "one blow under what the head can take burst it");
            Hit(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 12.0f, edged: false);
            Assert.IsFalse(dismemberer.Has(BodyPart.Head), "hitting the corpse's head again and again did not burst it");
            Assert.AreEqual(Dismemberment.Burst, dismemberer.Last);

            // A sword on the right forearm, then on the right upper arm: the forearm comes off, then the arm.
            Hit(body, BonePoint(body, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.5f), 20.0f, edged: true);
            Assert.AreEqual(Dismemberment.Sever, dismemberer.Last, "a sword on the corpse's forearm did not cut it off");
            Assert.AreEqual(BodyPart.RightLowerArm, dismemberer.LastPart);
            Assert.IsTrue(dismemberer.Has(BodyPart.RightUpperArm));
            Hit(body, BonePoint(body, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.5f), 20.0f, edged: true);
            Assert.IsFalse(dismemberer.Has(BodyPart.RightUpperArm), "the upper arm stayed on");

            // Shots at the chest: enough of them burst all that is left.
            for (int i = 0; i < 2; i++)
            {
                Hit(body, BonePoint(body, HumanBodyBones.Hips, HumanBodyBones.Neck, 0.6f), 20.0f, edged: false);
            }
            Assert.IsFalse(dismemberer.Has(BodyPart.Torso), "two shots of 20 at the chest did not burst the rest");
            Assert.IsFalse(Dismemberer.TakesCorpseHits(body.GetComponent<PlayerVitals>()), "nothing is left to hit, but the corpse still takes blows");
            yield return null;

            body.GetComponent<PlayerVitals>().Revive();
            Assert.IsTrue(dismemberer.IsWhole, "revived, the body is still in pieces");
            Assert.IsTrue(Skull(body).enabled, "revived, the body has no head");
        }

        [UnityTest]
        public IEnumerator ACivilianComesApartToo()
        {
#if UNITY_EDITOR
            GameObject civilian = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Civilian.prefab"),
                new Vector3(-6.0f, 0.0f, 0.0f), Quaternion.identity);
            _made.Add(civilian);
            var dismemberer = civilian.GetComponent<Dismemberer>();
            Assert.IsNotNull(dismemberer, "a civilian's body cannot be taken apart");
            yield return null;
            Animator animator = civilian.GetComponentInChildren<Animator>();
            Vector3 head = Vector3.Lerp(animator.GetBoneTransform(HumanBodyBones.Neck).position, animator.GetBoneTransform(HumanBodyBones.Head).position, 1.4f);
            IDamageable target = civilian.GetComponent<Civilian>();
            using (DamageContext.Cutting(true))
            {
                DamageContext.Strike(target, Civilian.MaxHealth * 2.5f, head + Vector3.back, 0.0f, DamageKind.Physical, 1.0f, head);
            }
            Assert.IsFalse(target.IsAlive, "setup: the blow did not kill");
            Assert.AreEqual(BodyPart.Head, dismemberer.LastPart, "a sword's overkill on a civilian did not take the head");
            Vector3 hand = Vector3.Lerp(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position, animator.GetBoneTransform(HumanBodyBones.LeftHand).position, 0.5f);
            DamageContext.Strike(target, 40.0f, hand + Vector3.back, 0.0f, DamageKind.Physical, 1.0f, hand);
            Assert.IsFalse(dismemberer.Has(BodyPart.LeftLowerArm), "a blow on a civilian's corpse did not burst the forearm");
#else
            Assert.Ignore("needs the editor to load the civilian prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ABurstOnTheChestTakesTheWholeBodyAndRevivedItIsWhole()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            int whole = SkinTriangles(body);
            Kill(body, BonePoint(body, HumanBodyBones.Hips, HumanBodyBones.Neck, 0.6f) + Vector3.forward * 0.15f, 120.0f, edged: false);
            Assert.AreEqual(BodyPart.Torso, dismemberer.LastPart);
            Assert.Greater(dismemberer.GibsThrown, 15, "the body burst into too little gore");
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                bool solid = renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
                Assert.IsFalse(solid && renderer.enabled, $"{renderer.name} is still showing after the body burst");
            }
            yield return null;

            body.GetComponent<PlayerVitals>().Revive();
            Assert.IsTrue(dismemberer.IsWhole, "revived, the body is still in pieces");
            Assert.IsTrue(Skull(body).enabled, "revived, the body has no head");
            Assert.AreEqual(whole, SkinTriangles(body), "revived, the body's skin is not whole");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyIsTakenApartTheSameWayAndMadeWholeAgain()
        {
            GameObject owner = Body(Vector3.zero);
            GameObject copy = Body(new Vector3(8.0f, 0.0f, 0.0f));
            yield return Stand(owner);
            yield return Stand(copy);
            var ownerDismemberer = owner.GetComponent<Dismemberer>();
            var copyDismemberer = copy.GetComponent<Dismemberer>();
            var told = new List<AbilityEvent>();
            ((IReplicatedAbility)ownerDismemberer).Fired += told.Add;
            Kill(owner, BonePoint(owner, HumanBodyBones.Neck, HumanBodyBones.Head, 1.2f), 70.0f, edged: true);
            Assert.AreEqual(1, told.Count, "the owner's cut was not told to other players");
            Assert.AreEqual(BodyPart.Head, ownerDismemberer.LastPart, "a sword through the neck did not take the head");
            AbilityEvent cut = told[0];
            Assert.IsTrue(NetworkedAbilitySync.IsPlausible(cut.Origin, cut.Direction, cut.A, cut.B, cut.N, owner.transform.position),
                "the server would refuse the cut");
            copyDismemberer.PlayRemote(new AbilityEvent(cut.Kind, cut.Origin + new Vector3(8.0f, 0.0f, 0.0f), cut.Direction, cut.A, cut.B, cut.N));
            Assert.AreEqual(Dismemberment.Sever, copyDismemberer.Last, "the copy was not cut");
            Assert.AreEqual(BodyPart.Head, copyDismemberer.LastPart);
            Assert.IsNotNull(copyDismemberer.LastPiece, "the copy's head did not fly off");
            Assert.IsFalse(Skull(copy).enabled, "the copy keeps its head");
            copyDismemberer.PlayRemote(new AbilityEvent(9, Vector3.zero, Vector3.up, 0.0f, 0.0f, 99));   // garbage from the wire: nothing
            Assert.AreEqual(BodyPart.Head, copyDismemberer.LastPart);

            owner.GetComponent<PlayerVitals>().Revive();
            Assert.AreEqual(2, told.Count, "the owner's revival was not told to other players");
            copyDismemberer.PlayRemote(told[1]);
            Assert.IsTrue(copyDismemberer.IsWhole, "the copy stays in pieces after its owner is revived");
            Assert.IsTrue(Skull(copy).enabled, "the copy's head is not back");
        }
    }
}
