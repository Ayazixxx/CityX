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
    /// THE BO STAFF ON THE REAL BODY (owner, 2026-11-19; docs/BO_STAFF.md): carried upright in both hands; swung with both hands near the end, reaching further than a sword; the chain is the chop, the low sweep and the rising blow (owner, 2026-11-22: the thrust cut), the held click
    /// the spin; the rising blow and the spin throw a player down; and another player's copy swings it (the chop) when told the attack's name.
    /// </summary>
    public sealed class BoStaffPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<Object> _made = new List<Object>();
        private GameObject _body;
        private SwordWeapon _staff;

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public int Hits;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
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
        private GameObject Player(Vector3 at)
        {
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body;
        }

        private void MakeBody(bool copy)
        {
            _body = Player(new Vector3(0.0f, 0.05f, 0.0f));
            _body.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/BoStaff.prefab");
            Assert.IsNotNull(prefab, "no BoStaff.prefab -- rebuild the scene");
            _staff = Object.Instantiate(prefab, new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            _made.Add(_staff.gameObject);
            if (copy)
            {
                _staff.MountCosmetic(_body);
            }
            else
            {
                _staff.OnEquip(_body, ItemInstanceData.None);
                _body.GetComponent<SoulsSwordCombat>().HitStopSeconds = 0.0f;
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

        /// <summary>How far the left palm is from the pole's line, and how far along it from the right hand (negative: toward the near end), metres.</summary>
        private (float off, float along) LeftHandOnThePole()
        {
            Animator animator = _body.GetComponentInChildren<Animator>();
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.IsTrue(_body.GetComponent<PlayerLocomotion>().TryGetSupportPalm(out Vector3 palm));
            Vector3 pole = (_staff.BladeTipWorld - _staff.BladeBaseWorld).normalized;
            Vector3 from = palm - _staff.BladeBaseWorld;
            return ((from - pole * Vector3.Dot(from, pole)).magnitude, Vector3.Dot(left.position - right.position, pole));
        }

        private Vector3 TipInBody => _body.transform.InverseTransformPoint(_staff.BladeTipWorld);
#endif

        [UnityTest]
        public IEnumerator CarriedUprightAndSwungWithBothHandsNearTheEndPastASwordsReach()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            Animator animator = _body.GetComponentInChildren<Animator>();
            yield return Frames(50);

            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.RightHand), _staff.transform.parent);
            Assert.IsFalse(animator.GetBool("SwordEquipped") || animator.GetBool("Sword1HEquipped"), "a staff is not walked about as a sword is");
            TestContext.WriteLine($"carried: the far end at {TipInBody.ToString("0.00")} in the body's frame");
            Assert.Greater(TipInBody.y, 2.4f, "carried, the far end is not standing up past the head");
            Assert.Less(TipInBody.z, 1.5f, "carried, the far end sticks out ahead");
            Assert.Less(LeftHandOnThePole().off, 0.09f, "carried, the left hand is not on the pole");

            float sword = combat.ReachMetres;
            Assert.Greater(combat.Reach, sword + 0.4f, "a staff reaches no further than a sword");
            Dummy target = Target(new Vector3(0.0f, 1.3f, sword + 0.85f));   // its near side 0.35 m past a sword's reach
            combat.RequestLight(0.0f);
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(SwordStrikeKind.StaffChop);
            float strikeFrom = (timing.WindUp + timing.Hold) / timing.Total, contact = timing.Contact / timing.Total;
            float worstOff = 0.0f, leastBehind = float.MaxValue;
            bool struck = false;
            for (int i = 0; i < 120 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
                if (!poser.IsStriking)
                {
                    continue;
                }
                struck = true;
                Assert.AreEqual(SwordStrikeKind.StaffChop, poser.Kind, "the first light is not the chop");
                if (poser.Progress >= strikeFrom * 0.6f && poser.Progress <= contact + 0.05f)
                {
                    (float off, float along) = LeftHandOnThePole();
                    worstOff = Mathf.Max(worstOff, off);
                    leastBehind = Mathf.Min(leastBehind, -along);
                }
            }
            Assert.IsTrue(struck, "the click threw nothing");
            TestContext.WriteLine($"left hand: {worstOff:0.000} m off the pole at worst, at least {leastBehind:0.00} m toward the near end from the right");
            Assert.Less(worstOff, 0.09f, "through the chop the left hand is not on the pole");
            Assert.Greater(leastBehind, 0.1f, "the left hand is not below the right, near the end");
            Assert.AreEqual(1, target.Hits, "the chop did not land on a target a sword could not reach");
            yield return Frames(40);
            Assert.Greater(TipInBody.y, 2.4f, "after the chop the pole is not back upright in the carry");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheChainIsTheChopTheSweepAndTheRisingBlowAndTheHeavyIsTheSpin()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            yield return Frames(50);
            var thrown = new List<SwordStrikeKind>();
            float sweepLow = float.MaxValue, sweepHigh = float.MinValue;
            combat.RequestLight(0.0f);
            for (int i = 0; i < 600 && (thrown.Count < 3 || poser.IsStriking); i++)
            {
                yield return Frame();
                if (poser.IsStriking && (thrown.Count == 0 || thrown[thrown.Count - 1] != poser.Kind))
                {
                    thrown.Add(poser.Kind);
                }
                if (poser.IsStriking && poser.Kind == SwordStrikeKind.StaffSweep && poser.BladeIsLive)
                {
                    sweepLow = Mathf.Min(sweepLow, _staff.BladeTipWorld.y);
                    sweepHigh = Mathf.Max(sweepHigh, _staff.BladeTipWorld.y);
                }
                if (thrown.Count < 3)
                {
                    combat.RequestLight(0.0f);
                }
            }
            CollectionAssert.AreEqual(new[] { SwordStrikeKind.StaffChop, SwordStrikeKind.StaffSweep, SwordStrikeKind.StaffRising }, thrown);
            TestContext.WriteLine($"while it could hit, the sweep's far end went from {sweepLow:0.00} m to {sweepHigh:0.00} m");
            Assert.Greater(sweepLow, 0.9f, "the sweep dips low (owner, 2026-11-22: \"just make it horizontal\")");
            yield return Frames(30);
            combat.RequestHeavy(0.0f);
            bool spun = false;
            for (int i = 0; i < 200 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
                spun |= poser.IsStriking && poser.Kind == SwordStrikeKind.StaffSpin;
            }
            Assert.IsTrue(spun, "the held click is not the spin");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSpinThrowsAPlayerDown()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            GameObject victim = Player(new Vector3(0.0f, 0.05f, 2.4f));
            victim.transform.rotation = Quaternion.LookRotation(Vector3.back);
            var vitals = victim.GetComponent<PlayerVitals>();
            float before = vitals.Health;
            yield return Frames(50);
            combat.RequestHeavy(0.0f);
            for (int i = 0; i < 200 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
            }
            yield return null;
            Assert.Less(vitals.Health, before, "the spin did not land");
            Assert.IsTrue(victim.GetComponent<Ragdoll>().IsLimp, "the spin did not throw the player it hit down");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopySwingsItWithBothHandsWhenToldTheAttacksName()
        {
#if UNITY_EDITOR
            MakeBody(true);
            var poser = _body.GetComponent<SwordPoseDriver>();
            Animator animator = _body.GetComponentInChildren<Animator>();
            yield return Frames(50, true);
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.RightHand), _staff.transform.parent, "the copy does not hold the staff");
            _body.GetComponent<PlayerAnimationDriver>().PlayRemoteState(SwordStrikes.StaffChopName);
            float furthest = float.MinValue, nearestHand = float.MaxValue;
            bool struck = false;
            for (int i = 0; i < 150 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame(true);
                if (poser.IsStriking)
                {
                    struck = true;
                    Assert.AreEqual(SwordStrikeKind.StaffChop, poser.Kind);
                    furthest = Mathf.Max(furthest, TipInBody.z);
                    if (poser.Progress > 0.3f && poser.Progress < 0.55f)
                    {
                        nearestHand = Mathf.Min(nearestHand, LeftHandOnThePole().off);
                    }
                }
            }
            Assert.IsTrue(struck, "the copy did not play the chop");
            Assert.Greater(furthest, 2.0f, "the copy's chop did not reach out");
            Assert.Less(nearestHand, 0.09f, "the copy's left hand is not on the pole for the chop");
#else
            yield break;
#endif
        }
    }
}
