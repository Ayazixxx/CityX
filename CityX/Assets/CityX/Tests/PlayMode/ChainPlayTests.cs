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
    /// THE CHAIN ON THE REAL BODY (owner, 2026-11-22; docs/CHAIN.md): worn wound round the wrist and the palm with nothing posed over the walk; a
    /// click throws it out and lashes, the next the backhand, and so on, further than any blade reaches; a while after the last swing it winds
    /// itself back on; off the ground the swing is the one brought down out of the air; with somebody lying ahead it is the one onto the ground,
    /// unless somebody stands there; and another player's copy throws it out and swings it when told the attack's name.
    /// </summary>
    public sealed class ChainPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<Object> _made = new List<Object>();
        private GameObject _body;
        private ChainWeapon _chain;

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
            if (_body.GetComponent<FireEnhancementAbility>() == null)
            {
                _body.AddComponent<FireEnhancementAbility>();   // (on the game's player from the start: PlayerAbilityWiring)
            }
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Chain.prefab");
            Assert.IsNotNull(prefab, "no Chain.prefab -- rebuild the scene");
            _chain = Object.Instantiate(prefab, new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<ChainWeapon>();
            _made.Add(_chain.gameObject);
            if (copy)
            {
                // What NetworkedEquipmentSync does to a copy's item: mounted as a model only, its scripts off but for those that keep running
                // (switched off, the chain stayed wound on the copy's wrist through every swing: owner, 2026-11-26).
                _chain.MountCosmetic(_body);
                InertModel.Strip(_chain.gameObject);
                foreach (MonoBehaviour behaviour in _chain.GetComponents<MonoBehaviour>())
                {
                    if (behaviour is ICosmeticKeepsRunning) behaviour.enabled = true;
                }
            }
            else
            {
                _chain.OnEquip(_body, ItemInstanceData.None);
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

        /// <summary>The chain's far end in the body's frame (z ahead).</summary>
        private Vector3 FarEndInBody => _body.transform.InverseTransformPoint(_chain.Links[_chain.Links.Length - 1].position);

        /// <summary>How far the furthest link that shows is from the wrist, metres.</summary>
        private float FurthestShownFromTheWrist()
        {
            Vector3 wrist = _body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand).position;
            float furthest = 0.0f;
            foreach (Transform link in _chain.Links)
            {
                if (link.localScale.x > 0.01f)
                {
                    furthest = Mathf.Max(furthest, Vector3.Distance(link.position, wrist));
                }
            }
            return furthest;
        }
#endif

        [UnityTest]
        public IEnumerator WornWoundOnTheWristAndThePalmWithNothingPosedOverTheWalk()
        {
#if UNITY_EDITOR
            MakeBody(false);
            Animator animator = _body.GetComponentInChildren<Animator>();
            var locomotion = _body.GetComponent<PlayerLocomotion>();
            yield return Frames(50);

            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.RightHand), _chain.transform.parent, "the chain is not on the right hand");
            Assert.AreEqual(1.0f, _chain.Wound, "at rest the chain is not wound on");
            Assert.IsFalse(_chain.IsOut);
            Assert.IsFalse(locomotion.SwordPoseActive, "worn, the chain poses the body: it should walk as an empty-handed one does");
            Assert.IsFalse(animator.GetBool("SwordEquipped") || animator.GetBool("Sword1HEquipped"), "a worn chain is not carried as a sword is");
            float furthest = FurthestShownFromTheWrist();
            int shown = 0;
            foreach (Transform link in _chain.Links)
            {
                shown += link.localScale.x > 0.01f ? 1 : 0;
            }
            TestContext.WriteLine($"worn: {shown} links show, the furthest {furthest:0.00} m from the wrist");
            Assert.AreEqual(ChainWrap.WristLinks + ChainWrap.PalmLinks, shown, "the links in the fist show, or wound ones do not");
            Assert.Less(furthest, 0.35f, "worn, a link hangs off the arm");
            Assert.Less(_chain.StrongHandGrip.Index, 0.5f, "the wrapped hand is a fist: it rests open");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AClickThrowsItOutAndLashesThenTheBackhandThenTheLashAgainPastAnyBladesReach()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            yield return Frames(50);
            float sword = combat.ReachMetres;
            Dummy target = Target(new Vector3(0.0f, 1.3f, sword + 1.2f));   // its near side 0.7 m past a sword's reach
            var thrown = new List<SwordStrikeKind>();
            float outBy = -1.0f, furthestAhead = float.MinValue;
            combat.RequestLight(0.0f);
            for (int i = 0; i < 600 && (thrown.Count < 3 || poser.IsStriking); i++)
            {
                yield return Frame();
                if (poser.IsStriking && (thrown.Count == 0 || thrown[thrown.Count - 1] != poser.Kind))
                {
                    thrown.Add(poser.Kind);
                }
                if (thrown.Count == 1 && poser.IsStriking)
                {
                    if (outBy < 0.0f && _chain.Wound <= 0.0f && _chain.PaidOut >= ChainWrap.FreeLinks)
                    {
                        outBy = poser.Progress * SwordStrikes.Seconds(poser.Kind);
                    }
                    furthestAhead = Mathf.Max(furthestAhead, FarEndInBody.z);
                }
                if (thrown.Count < 3)
                {
                    combat.RequestHeavy(0.0f);   // a chain has no heavy: a held click lashes too
                }
            }
            CollectionAssert.AreEqual(new[] { SwordStrikeKind.ChainLash, SwordStrikeKind.ChainBackhand, SwordStrikeKind.ChainLash }, thrown);
            TestContext.WriteLine($"all out {outBy:0.00} s into the first lash; its far end got {furthestAhead:0.00} m ahead; the target was struck {target.Hits} times");
            Assert.That(outBy, Is.InRange(0.0f, SwordStrikes.TimingOf(SwordStrikeKind.ChainLash).Contact), "the chain is not all out by the time the lash lands");
            Assert.Greater(furthestAhead, 2.2f, "the lash's far end did not go out ahead");
            Assert.AreEqual(3, target.Hits, "each swing did not land once on a target no blade reaches");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AWhileAfterTheLastSwingItWindsItselfBackOn()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            yield return Frames(50);
            combat.RequestLight(0.0f);
            for (int i = 0; i < 200 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
            }
            Assert.IsTrue(_chain.IsOut, "after a swing the chain is not out");
            yield return Frames(30);
            Assert.IsTrue(_chain.IsOut, "the chain is wound back on at once: it hangs a while first");
            Assert.Greater(FurthestShownFromTheWrist(), 0.8f, "out, the chain does not hang from the fist to the ground");
            Assert.Greater(_chain.StrongHandGrip.Index, 0.9f, "the chain hangs from an open hand");
            float wait = ChainWeapon.RewindAfterSeconds + ChainWeapon.PayInSeconds + ChainWeapon.WindSeconds + 0.3f;
            yield return Frames(Mathf.CeilToInt(wait / Dt));
            Assert.AreEqual(1.0f, _chain.Wound, "the chain did not wind itself back on");
            Assert.AreEqual(ChainWrap.PalmLinks, _chain.PaidOut, 1e-3f);
            Assert.Less(FurthestShownFromTheWrist(), 0.35f, "wound back on, a link still hangs off the arm");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SwungOffTheGroundItIsTheOneBroughtStraightDown()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            var locomotion = _body.GetComponent<PlayerLocomotion>();
            yield return Frames(30);
            var controller = _body.GetComponent<CharacterController>();
            controller.enabled = false;
            _body.transform.position = new Vector3(0.0f, 9.0f, 0.0f);
            controller.enabled = true;
            yield return Frames(4);
            Assert.IsFalse(locomotion.IsGrounded, "setup: the body is not in the air");
            combat.RequestLight(0.0f);
            bool struck = false;
            float lowest = float.MaxValue;
            for (int i = 0; i < 80 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
                if (poser.IsStriking)
                {
                    struck = true;
                    Assert.AreEqual(SwordStrikeKind.ChainAir, poser.Kind, "off the ground the swing is not the one out of the air");
                    lowest = Mathf.Min(lowest, FarEndInBody.y);
                }
            }
            Assert.IsTrue(struck, "the click threw nothing");
            TestContext.WriteLine($"in the air the chain's far end got {-lowest:0.00} m below the feet");
            Assert.Less(lowest, -0.8f, "the swing in the air does not come down below the body");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WithSomebodyLyingAheadItWhipsDownOntoThemUnlessSomebodyStandsThere()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var combat = _body.GetComponent<SoulsSwordCombat>();
            var poser = _body.GetComponent<SwordPoseDriver>();
            GameObject victim = Player(new Vector3(0.0f, 0.05f, 2.0f));
            var vitals = victim.GetComponent<PlayerVitals>();
            yield return Frames(30);
            CityX.Character.Throws.Limp(vitals, Vector3.up * 0.5f);
            for (int i = 0; i < 90; i++)
            {
                yield return Frame();
            }
            Assert.IsTrue(victim.GetComponent<Ragdoll>().IsLimp, "setup: the victim is not down");
            float before = vitals.Health;
            combat.RequestLight(0.0f);
            bool struck = false;
            for (int i = 0; i < 120 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame();
                if (poser.IsStriking)
                {
                    struck = true;
                    Assert.AreEqual(SwordStrikeKind.ChainGround, poser.Kind, "with somebody lying ahead the swing is not the one onto the ground");
                }
            }
            Assert.IsTrue(struck, "the click threw nothing");
            Assert.Less(vitals.Health, before, "the blow onto the ground did not land on the body lying there");

            // Somebody on their feet there is swung at as ever, whoever lies at their feet.
            Target(new Vector3(0.6f, 1.3f, 2.2f));
            yield return Frames(40);
            if (victim.GetComponent<Ragdoll>().IsLimp)
            {
                combat.RequestLight(0.0f);
                yield return Frame();
                yield return Frame();
                Assert.IsTrue(poser.IsStriking && poser.Kind == SwordStrikeKind.ChainLash, "with somebody standing ahead the swing is not the lash");
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WithTheFireEnhancementOnItsSteelGlowsAndIsALight()
        {
#if UNITY_EDITOR
            MakeBody(false);
            var fire = _body.GetComponent<FireEnhancementAbility>();
            Assert.IsNotNull(fire, "setup: the player has no fire enhancement");
            var skin = _chain.GetComponentInChildren<SkinnedMeshRenderer>();
            Material cold = skin.sharedMaterial;
            yield return Frames(30);
            Assert.IsFalse(_chain.IsHot);
            Assert.IsTrue(fire.TryCast(), "the fire enhancement could not be cast");
            yield return Frames(10);
            Assert.IsTrue(_chain.IsHot, "with the fire on, the chain is not hot");
            Assert.AreNotSame(cold, skin.sharedMaterial, "hot, the steel looks as it does cold");
            Assert.IsTrue(skin.sharedMaterial.IsKeywordEnabled("_EMISSION"), "hot, the steel gives off no light of its own");
            WeaponAblaze ablaze = Object.FindFirstObjectByType<WeaponAblaze>();
            Assert.IsNotNull(ablaze, "no light for the hot chain");
            Light light = ablaze.GetComponentInChildren<Light>();
            Assert.IsTrue(light.enabled, "the hot chain is not a light");
            Assert.AreEqual(WeaponAblaze.GlowingSteelLightRange, light.range, 0.3f, "the hot chain's light is a blade's, not its own");
            Animator animator = _body.GetComponentInChildren<Animator>();
            Assert.Less(Vector3.Distance(light.transform.position, animator.GetBoneTransform(HumanBodyBones.RightHand).position), 0.8f,
                "wound on, the light is not at the hand");
            fire.Tick(FireEnhancementRules.ArmedSeconds + 1.0f);
            yield return Frames(5);
            Assert.IsFalse(_chain.IsHot, "the fire spent, the chain still glows");
            Assert.AreSame(cold, skin.sharedMaterial);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyThrowsItOutAndSwingsItWhenToldTheAttacksName()
        {
#if UNITY_EDITOR
            MakeBody(true);
            var poser = _body.GetComponent<SwordPoseDriver>();
            Animator animator = _body.GetComponentInChildren<Animator>();
            yield return Frames(50, true);
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.RightHand), _chain.transform.parent, "the copy does not wear the chain");
            Assert.AreEqual(1.0f, _chain.Wound, "the copy's chain is not wound on at rest");
            _body.GetComponent<PlayerAnimationDriver>().PlayRemoteState(SwordStrikes.ChainLashName);
            float furthest = 0.0f;
            bool struck = false;
            for (int i = 0; i < 150 && (poser.IsStriking || i < 3); i++)
            {
                yield return Frame(true);
                if (poser.IsStriking)
                {
                    struck = true;
                    Assert.AreEqual(SwordStrikeKind.ChainLash, poser.Kind);
                    furthest = Mathf.Max(furthest, FurthestShownFromTheWrist());
                }
            }
            Assert.IsTrue(struck, "the copy did not play the lash");
            TestContext.WriteLine($"the copy's chain reached {furthest:0.00} m from its wrist");
            Assert.Greater(furthest, 2.4f, "the copy's chain did not come off and swing");
            float wait = ChainWeapon.RewindAfterSeconds + ChainWeapon.PayInSeconds + ChainWeapon.WindSeconds + 0.3f;
            yield return Frames(Mathf.CeilToInt(wait / Dt), true);
            Assert.AreEqual(1.0f, _chain.Wound, "the copy's chain did not wind itself back on");
#else
            yield break;
#endif
        }
    }
}
