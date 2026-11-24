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
    /// The hobos on the real bodies (docs/HOBO.md; owner, 2026-11-24: "make them not hostile until hit they jus walk around. and make it so they
    /// throw a bottle at you if you are distanced from them and they have a bottle in hand ... 3 not broken but empty glass bottles in their
    /// inventory they can throw and are lootable upon death"): beside a player he leaves it alone; hurt, he fights it; each weapon is in his
    /// hand and swung; with the bottle he throws his spares from a distance, a copy of him on another screen carries one fewer too, and what
    /// he has left is found on his body.
    /// </summary>
    public sealed class HoboPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _hobo;
        private GameObject _copy;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (GameObject made in new[] { _player, _hobo, _copy, _ground })
            {
                if (made != null) Object.Destroy(made);
            }
            foreach (ThrownBottle thrown in Object.FindObjectsByType<ThrownBottle>(FindObjectsSortMode.None))
            {
                Object.Destroy(thrown.gameObject);
            }
        }

        private static GameObject Prefab(string name)
        {
#if UNITY_EDITOR
            string path = $"Assets/CityX/Character/Hobos/{name}.prefab";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} is missing -- rebuild the scene");
            return prefab;
#else
            return null;
#endif
        }

        private PlayerVitals SetUp(string hobo, float apart)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            _hobo = Object.Instantiate(Prefab(hobo), new Vector3(0.0f, 0.05f, apart), Quaternion.LookRotation(Vector3.back));   // facing the player
            _hobo.GetComponent<SwordsmanBrain>().Seed = 20261124;
            return vitals;
#else
            return null;
#endif
        }

        private static IEnumerator Run(float seconds, PlayerVitals vitals)
        {
            for (float clock = 0.0f; clock < seconds; clock += Dt)
            {
                vitals.TickHitstun(Dt);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HeLeavesYouAloneUntilYouHurtHim()
        {
            PlayerVitals vitals = SetUp("Hobo_1_Machete", 2.0f);
            _player.GetComponent<PlayerInputReader>().enabled = true;   // a player he could take on (PlayerBodies), right beside him
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            int hits = 0;
            vitals.Hit += from => hits++;
            yield return Run(4.0f, vitals);
            Assert.IsNull(brain.Target, "a hobo took on a player who never touched him");
            Assert.AreEqual(0, hits, "a hobo cut a player who never touched him");

            ((IDamageable)_hobo.GetComponent<PlayerVitals>()).ApplyHit(5.0f, _player.transform.position, 0.0f);
            float nearest = float.MaxValue;
            for (float clock = 0.0f; clock < 8.0f; clock += Dt)
            {
                vitals.TickHitstun(Dt);
                yield return null;
                nearest = Mathf.Min(nearest, Vector3.Distance(_hobo.transform.position, _player.transform.position));
            }
            // He fights as the swordsman does: in to fighting distance, then his guard, circling and cuts as the fight goes (a player who
            // only stands there may be circled a long while: SwordsmanBrain's duel).
            Assert.AreEqual(vitals, brain.Target, "hurt, he did not turn on the one who hurt him");
            Assert.Less(nearest, 4.0f, "hurt, he never came at the one who hurt him");
            Assert.AreNotEqual(DuelistState.Idle, brain.State, "hurt, he does not fight");
        }

        /// <summary>Each weapon in his hand is swung as the player swings it.</summary>
        [UnityTest]
        public IEnumerator EachWeaponIsInHisHand([Values("Hobo_1_Pole", "Hobo_2_Bottle", "Hobo_1_Chain", "Hobo_2_Machete", "Hobo_1_Plank")] string hobo)
        {
            PlayerVitals vitals = SetUp(hobo, 8.0f);
            yield return null;
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            Assert.IsNotNull(brain.Combat, $"{hobo}'s weapon did not equip");
            Assert.IsTrue(brain.Combat.IsEquipped, $"{hobo}'s weapon did not equip");
            var bottle = _hobo.GetComponentInChildren<BottleWeapon>();
            if (hobo.EndsWith("Bottle"))
            {
                Assert.IsNotNull(bottle, "no bottle in his hand");
                Assert.AreEqual(BottleWeapon.DurabilityState.Broken, bottle.State, "his bottle is not broken");
            }
            Assert.IsNotNull(vitals);
        }

        /// <summary>
        /// Wandering, his legs walk (owner, 2026-11-24: "the hobos dont have their ... walking animations"): while he moves, the walk plays, not
        /// the standing pose half blended in (the stroll used to be under half a walk, and the body glided with its legs nearly still).
        /// </summary>
        [UnityTest]
        public IEnumerator WanderingHisLegsWalk()
        {
            PlayerVitals vitals = SetUp("Hobo_2_Machete", 30.0f);
            Object.Destroy(_player);   // nobody about: he wanders
            var animator = _hobo.GetComponentInChildren<Animator>();
            var body = _hobo.GetComponent<PlayerLocomotion>();
            int moving = 0;
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null;
                if (body.CurrentSpeed < 3.5f || animator.IsInTransition(0))
                {
                    continue;
                }
                float walking = 0.0f;
                foreach (AnimatorClipInfo clip in animator.GetCurrentAnimatorClipInfo(0))
                {
                    if (clip.clip.name.Contains("walk")) walking += clip.weight;
                }
                Assert.Greater(walking, 0.8f, $"moving at {body.CurrentSpeed:0.0} m/s his legs are mostly standing still");
                moving++;
            }
            Assert.Greater(moving, 60, "he never wandered");
            Assert.IsNotNull(vitals);
        }

        /// <summary>
        /// Hurt from a distance, he throws his whole bottles at the player and they hit; a copy of him on another screen carries one fewer each
        /// time, and shows a whole bottle in the hand as it goes. Then, beaten, the ones he has left are on his body.
        /// </summary>
        [UnityTest]
        public IEnumerator FromADistanceHeThrowsHisSpareBottlesAndTheRestAreLoot()
        {
            PlayerVitals vitals = SetUp("Hobo_2_Bottle", 14.0f);
            _copy = Object.Instantiate(Prefab("Hobo_2_Bottle"), new Vector3(30.0f, 0.05f, 0.0f), Quaternion.identity);
            _copy.GetComponent<SwordsmanBrain>().enabled = false;   // another screen's copy: only shown what he does
            yield return null;
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            var carried = _hobo.GetComponent<CarriedLoot>();
            var thrower = _hobo.GetComponent<BottleThrower>();
            var copyThrower = _copy.GetComponent<BottleThrower>();
            var copyCarried = _copy.GetComponent<CarriedLoot>();
            var copyBottle = _copy.GetComponentInChildren<BottleWeapon>();
            bool copyShowedWhole = false;
            thrower.Fired += evt =>
            {
                copyThrower.PlayRemote(evt);
                copyShowedWhole |= copyBottle.ShowsSpare;
                // The copy's bottle would fly on the other screen: here it would meet his own in the air.
                foreach (ThrownBottle flying in Object.FindObjectsByType<ThrownBottle>(FindObjectsSortMode.None))
                {
                    if (flying.Harmless) Object.DestroyImmediate(flying.gameObject);
                }
            };
            int hits = 0;
            vitals.Hit += from => hits++;

            brain.Target = vitals;   // as when the player has hurt him
            for (float clock = 0.0f; clock < 7.0f; clock += Dt)
            {
                vitals.TickHitstun(Dt);
                yield return null;
            }
            Assert.GreaterOrEqual(brain.BottlesThrown, 1, "from 14 m he threw no bottle");
            Assert.AreEqual(3 - brain.BottlesThrown, carried.Count(GlassBottleKinds.Classic), "a thrown bottle is still carried");
            Assert.AreEqual(carried.Count(GlassBottleKinds.Classic), copyCarried.Count(GlassBottleKinds.Classic), "the copy carries a thrown bottle still");
            Assert.IsTrue(copyShowedWhole, "the copy never showed a whole bottle in the hand for the throw");
            Assert.IsFalse(copyBottle.ShowsSpare, "the copy's hand still shows the thrown bottle");
            Assert.Greater(hits, 0, "his bottles never hit the player standing still");

            int left = carried.Count(GlassBottleKinds.Classic);
            _hobo.GetComponent<PlayerVitals>().TakeDamage(10000.0f);
            yield return null;
            var loot = _hobo.GetComponent<CorpseLoot>();
            Assert.IsNotNull(loot.Container, "his body cannot be searched");
            Assert.AreEqual("Hobo", loot.Container.Label);
            int listed = loot.Container.Items.FindAll(id => id == GlassBottleKinds.Classic).Count;
            Assert.AreEqual(left, listed, "the bottles he had left are not all on his body");
        }
    }
}
