using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE SPEAR ON THE REAL BODY (owner, 2026-11-18; docs/SPEAR.md): carried in the right hand alone; a thrust has both hands on the shaft, the
    /// front one staying before its shoulder while the shaft slides through it, and the point goes straight out further than a sword reaches;
    /// beside a shield the left hand stays off the shaft; the held click sweeps and catches people either side of the front; and another
    /// player's copy thrusts when told the attack's name.
    /// </summary>
    public sealed class SpearPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<Object> _made = new List<Object>();
        private GameObject _body;
        private SwordWeapon _spear;

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public float Taken;
            public int Hits;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
                Hits++;
                return HitResponse.Landed;
            }
        }

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            _made.Add(ground);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (Object made in _made)
            {
                if (made != null) Object.Destroy(made);
            }
            _made.Clear();
        }

#if UNITY_EDITOR
        private void MakeBody(bool copy, bool shield = false)
        {
            _body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(_body);
            _body.GetComponent<PlayerInputReader>().enabled = false;
            _body.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Spear.prefab");
            Assert.IsNotNull(prefab, "no Spear.prefab -- rebuild the scene");
            _spear = Object.Instantiate(prefab, new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            _made.Add(_spear.gameObject);
            if (copy)
            {
                _spear.MountCosmetic(_body);
            }
            else
            {
                _spear.OnEquip(_body, ItemInstanceData.None);
                _body.GetComponent<SoulsSwordCombat>().HitStopSeconds = 0.0f;
            }
            if (shield)
            {
                GameObject held = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RoundShield.prefab"));
                _made.Add(held);
                held.GetComponent<IEquippableItem>().OnEquip(_body, ItemInstanceData.None);
            }
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            go.transform.localScale = new Vector3(1.0f, 1.3f, 1.0f);
            _made.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<Dummy>();
        }

        private IEnumerator Frame(bool copy = false)
        {
            if (copy)
            {
                _body.GetComponentInChildren<Animator>().SetBool("Grounded", true);   // what NetworkedAnimatorSync hands a copy every tick
            }
            else
            {
                _body.GetComponent<SoulsSwordCombat>().Tick(Dt);
                _body.GetComponent<PlayerVitals>().TickHitstun(Dt);
                _body.GetComponent<PlayerLocomotion>().Tick(default, Dt);
            }
            yield return null;
        }

        private IEnumerator Frames(int count, bool copy = false)
        {
            for (int i = 0; i < count; i++)
            {
                yield return Frame(copy);
            }
        }

        /// <summary>How far the left palm is from the shaft's line, and how far up the shaft from the right hand (negative: behind it), metres.</summary>
        private (float off, float along) LeftHandOnTheShaft()
        {
            Animator animator = _body.GetComponentInChildren<Animator>();
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.IsTrue(_body.GetComponent<PlayerLocomotion>().TryGetSupportPalm(out Vector3 palm));
            Vector3 shaft = (_spear.BladeTipWorld - _spear.BladeBaseWorld).normalized;
            Vector3 from = palm - _spear.BladeBaseWorld;
            float along = Vector3.Dot(left.position - right.position, shaft);
            return ((from - shaft * Vector3.Dot(from, shaft)).magnitude, along);
        }

        private Vector3 TipInBody => _body.transform.InverseTransformPoint(_spear.BladeTipWorld);
#endif

        [UnityTest]
        public IEnumerator CarriedInTheRightHandAloneAndThrustWithBothThePointGoingStraightOutPastASwordsReach()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            Animator animator = _body.GetComponentInChildren<Animator>();
            yield return Frames(50);

            // The carry: the right hand has it, the point ahead and up off the ground, the butt off the ground too; the left hand swings free.
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.RightHand), _spear.transform.parent);
            Assert.IsFalse(animator.GetBool("SwordEquipped") || animator.GetBool("Sword1HEquipped"), "a spear is not walked about as a sword is");
            Vector3 butt = _spear.transform.TransformPoint(new Vector3(0.0f, -0.65f, 0.0f));
            Assert.Greater(TipInBody.z, 1.0f, "carried, the point is not ahead");
            Assert.Greater(_spear.BladeTipWorld.y, 1.2f, "carried, the point is down at the ground");
            Assert.Greater(butt.y, 0.3f, "carried, the butt drags on the ground");
            Assert.Greater(LeftHandOnTheShaft().off, 0.3f, "carried, the left hand is on the shaft: it is carried in one hand");

            // A target further off than a sword reaches, inside the spear's reach.
            float sword = combat.ReachMetres;
            Assert.Greater(combat.Reach, sword + 0.5f);
            Dummy target = Target(new Vector3(0.0f, 1.3f, sword + 0.95f));   // its near side 0.45 m past a sword's reach
            combat.RequestLight(0.0f);
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(SwordStrikeKind.SpearJab);
            float strikeFrom = (timing.WindUp + timing.Hold) / timing.Total, contact = timing.Contact / timing.Total;
            var tips = new List<Vector3>();
            float worstOff = 0.0f, leastAhead = float.MaxValue, mostAhead = 0.0f;
            bool struck = false;
            for (int i = 0; i < 120 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
                if (!poser.IsStriking)
                {
                    continue;
                }
                struck = true;
                Assert.AreEqual(SwordStrikeKind.SpearJab, poser.Kind, "the first light is not the jab");
                if (poser.Progress >= strikeFrom * 0.6f && poser.Progress <= contact + 0.05f)
                {
                    (float off, float along) = LeftHandOnTheShaft();
                    worstOff = Mathf.Max(worstOff, off);
                    leastAhead = Mathf.Min(leastAhead, along);
                    mostAhead = Mathf.Max(mostAhead, along);
                }
                if (poser.Progress >= strikeFrom && poser.Progress <= contact)
                {
                    tips.Add(_spear.BladeTipWorld);
                }
            }
            Assert.IsTrue(struck, "the click threw nothing");
            TestContext.WriteLine($"left hand: {worstOff:0.000} m off the shaft at worst, {leastAhead:0.00} to {mostAhead:0.00} m ahead of the right; {tips.Count} tip samples");
            Assert.Less(worstOff, 0.09f, "through the thrust the left hand is not on the shaft");
            Assert.Greater(leastAhead, 0.15f, "the left hand is not ahead of the right on the shaft");
            Assert.Greater(mostAhead - leastAhead, 0.12f, "the shaft did not slide through the front hand: the hands kept their distance");
            Assert.GreaterOrEqual(tips.Count, 3);
            Vector3 travel = tips[tips.Count - 1] - tips[0];
            Assert.Greater(travel.magnitude, 0.5f, "the point hardly went out");
            TestContext.WriteLine($"the point travelled {_body.transform.InverseTransformDirection(travel).ToString("0.00")} in the body's frame");
            Assert.Less(Vector3.Angle(travel, _body.transform.forward), 20.0f, "the point did not go out ahead");
            foreach (Vector3 tip in tips)
            {
                Vector3 at = tip - tips[0];
                Assert.Less((at - Vector3.Project(at, travel)).magnitude, 0.15f, "the point wandered off its line: a thrust is straight");
            }
            Assert.AreEqual(1, target.Hits, "the jab did not land on a target a sword could not reach");
            Assert.AreEqual(SpearRules.BaseDamage * combat.SpearLight1.DamageScale, target.Taken, 1.5f, "not the jab's damage");
            yield return Frames(40);
            Assert.Greater(LeftHandOnTheShaft().off, 0.3f, "after the thrust the left hand stayed on the shaft: back to the carry");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator BesideAShieldTheSpearIsThrustWithTheRightHandAloneAndStillLands()
        {
#if UNITY_EDITOR
            MakeBody(false, shield: true);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            var locomotion = _body.GetComponent<PlayerLocomotion>();
            Animator animator = _body.GetComponentInChildren<Animator>();
            yield return Frames(50);
            Assert.IsNotNull(locomotion.Shield, "setup: no shield on the body");
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.LeftHand), locomotion.Shield.parent, "the shield is not in the left hand");
            Assert.IsTrue(ShieldTraits.Round.GoesBeside(HeldKind.OneHandedMelee) && ShieldTraits.Riot.GoesBeside(HeldKind.OneHandedMelee));

            Dummy target = Target(new Vector3(0.0f, 1.3f, combat.ReachMetres + 0.95f));
            combat.RequestLight(0.0f);
            float nearest = float.MaxValue;
            for (int i = 0; i < 120 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
                if (poser.IsStriking)
                {
                    nearest = Mathf.Min(nearest, LeftHandOnTheShaft().off);
                }
            }
            Assert.Greater(nearest, 0.25f, "with a shield in it the left hand went to the shaft");
            Assert.AreEqual(1, target.Hits, "the one-handed thrust did not land");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheHeldClicksSweepCatchesPeopleEitherSideOfTheFrontAndTheChainEndsInTheLunge()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            yield return Frames(50);
            Dummy left = Target(new Vector3(-1.5f, 1.3f, 1.9f));
            Dummy right = Target(new Vector3(1.5f, 1.3f, 1.9f));
            combat.RequestHeavy(0.0f);
            float furthestLeft = 0.0f, furthestRight = 0.0f;
            for (int i = 0; i < 200 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
                if (poser.IsStriking)
                {
                    Assert.AreEqual(SwordStrikeKind.SpearSweep, poser.Kind, "the heavy is not the sweep");
                    furthestLeft = Mathf.Min(furthestLeft, TipInBody.x);
                    furthestRight = Mathf.Max(furthestRight, TipInBody.x);
                }
            }
            Assert.Less(furthestLeft, -0.8f, "the sweep's point never went out to the left");
            Assert.Greater(furthestRight, 0.8f, "the sweep's point never came round to the right");
            Assert.AreEqual(1, left.Hits, "the sweep missed the one on the left");
            Assert.AreEqual(1, right.Hits, "the sweep missed the one on the right");
            Object.Destroy(left.gameObject);
            Object.Destroy(right.gameObject);
            yield return Frames(30);

            // The chain: jab, thrust, lunge, each click buffered into the next.
            var thrown = new List<SwordStrikeKind>();
            combat.RequestLight(0.0f);
            for (int i = 0; i < 400 && thrown.Count < 3; i++)
            {
                yield return Frame();
                if (poser.IsStriking && (thrown.Count == 0 || thrown[thrown.Count - 1] != poser.Kind))
                {
                    thrown.Add(poser.Kind);
                }
                combat.RequestLight(0.0f);
            }
            CollectionAssert.AreEqual(new[] { SwordStrikeKind.SpearJab, SwordStrikeKind.SpearThrust, SwordStrikeKind.SpearLunge }, thrown);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyThrustsWithBothHandsWhenToldTheAttacksName()
        {
#if UNITY_EDITOR
            MakeBody(true);
            var poser = _body.GetComponent<SwordPoseDriver>();
            Animator animator = _body.GetComponentInChildren<Animator>();
            yield return Frames(50, true);
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.RightHand), _spear.transform.parent, "the copy does not hold the spear");
            float tipBefore = TipInBody.z;
            _body.GetComponent<PlayerAnimationDriver>().PlayRemoteState(SwordStrikes.SpearLungeName);
            float furthest = tipBefore, nearestHand = float.MaxValue;
            bool struck = false;
            for (int i = 0; i < 150 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame(true);
                if (poser.IsStriking)
                {
                    struck = true;
                    Assert.AreEqual(SwordStrikeKind.SpearLunge, poser.Kind);
                    furthest = Mathf.Max(furthest, TipInBody.z);
                    if (poser.Progress > 0.25f && poser.Progress < 0.5f)
                    {
                        nearestHand = Mathf.Min(nearestHand, LeftHandOnTheShaft().off);
                    }
                }
            }
            Assert.IsTrue(struck, "the copy did not play the lunge");
            Assert.Greater(furthest, tipBefore + 0.4f, "the copy's point did not go out");
            Assert.Less(nearestHand, 0.09f, "the copy's left hand never took the shaft");
#else
            yield break;
#endif
        }
    }
}
