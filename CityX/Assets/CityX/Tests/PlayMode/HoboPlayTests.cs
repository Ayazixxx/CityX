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
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
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
            _hobo.GetComponent<SwordsmanBrain>().CalmArmedShare = 1.0f;   // in hand from the start (a test of the weapon put away sets 0)
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

        /// <summary>
        /// Hit from 10 m while a friend stands 3 m from him, he goes for the one who hit him and stays on them (bug sweep 2026-11-25: he went
        /// for whoever stood nearest).
        /// </summary>
        [UnityTest]
        public IEnumerator HitFromAfarHeGoesForWhoeverHitHimNotTheNearest()
        {
#if UNITY_EDITOR
            PlayerVitals hitter = SetUp("Hobo_1_Machete", 10.0f);
            _player.GetComponent<PlayerInputReader>().enabled = true;   // players he could take on (PlayerBodies)
            _copy = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(1.0f, 0.05f, 7.0f), Quaternion.identity);
            var friend = _copy.GetComponent<PlayerVitals>();
            friend.InfiniteHealth = true;
            yield return null;
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            DamageContext.Strike(_hobo.GetComponent<PlayerVitals>(), 5.0f, _player.transform.position, 0.3f, DamageKind.Physical, 0.0f, null, _player);
            for (float clock = 0.0f; clock < 3.0f; clock += Dt)
            {
                hitter.TickHitstun(Dt);
                friend.TickHitstun(Dt);
                yield return null;
                Assert.AreNotSame(friend, brain.Target, "hit from 10 m, he went for the friend standing 3 m from him");
            }
            Assert.AreSame(hitter, brain.Target, "he did not go for the one who hit him");
#else
            yield break;
#endif
        }

        /// <summary>
        /// His bottle broken to nothing, he stops telling the other screens his weapon is out (told every few seconds so a late arrival sees
        /// it): a player who joined afterwards was shown a fresh bottle in his empty hand (bug sweep 2026-11-25).
        /// </summary>
        [UnityTest]
        public IEnumerator WithHisBottleBrokenToNothingHeStopsTellingOthersItIsOut()
        {
            PlayerVitals vitals = SetUp("Hobo_1_Bottle", 8.0f);
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            brain.CalmArmedShare = 0.0f;   // put away until a fight: told to others when it comes out
            int told = 0;
            _hobo.GetComponent<PlayerAnimationDriver>().TimedStatePlayed += (state, speed, start) =>
            {
                if (state == PlayerAnimationDriver.WeaponDrawnName) told++;
            };
            yield return null;
            _hobo.GetComponent<PlayerVitals>().TakeDamage(1.0f);   // out for a fight
            yield return Run(SwordsmanBrain.DrawnRepeatSeconds + 1.0f, vitals);
            Assert.IsTrue(brain.WeaponDrawn);
            Assert.Greater(told, 0, "his bottle out, he never told the other screens");

            var bottle = _hobo.GetComponentInChildren<BottleWeapon>();
            Assert.IsNotNull(bottle, "no bottle in his hand");
            Object.Destroy(bottle.gameObject);   // broken to nothing (BottleWeapon takes itself away so)
            yield return null;
            told = 0;
            yield return Run(SwordsmanBrain.DrawnRepeatSeconds * 2.0f + 1.0f, vitals);
            Assert.AreEqual(0, told, "with nothing left in his hand he still told the other screens his weapon is out");
        }

        /// <summary>Each hobo carries one to three empty bottles, how many rolled when he is spawned (owner, 2026-11-26: "3 max").</summary>
        [UnityTest]
        public IEnumerator EachHoboCarriesOneToThreeBottles()
        {
#if UNITY_EDITOR
            PlayerVitals vitals = SetUp("Hobo_1_Machete", 40.0f);
            var many = new List<GameObject>();
            for (int i = 0; i < 12; i++)
            {
                many.Add(Object.Instantiate(Prefab("Hobo_1_Machete"), new Vector3(-30.0f + i * 5.0f, 0.05f, 60.0f), Quaternion.identity));
            }
            yield return Run(0.2f, vitals);
            var seen = new HashSet<int>();
            foreach (GameObject hobo in many)
            {
                int bottles = hobo.GetComponent<CarriedLoot>().Count(GlassBottleKinds.Classic);
                Assert.That(bottles, Is.InRange(1, 3), $"a hobo carries {bottles} bottles");
                seen.Add(bottles);
                Object.Destroy(hobo);
            }
            Assert.Greater(seen.Count, 1, "every hobo carries the same number of bottles: it is not random");
#else
            yield break;
#endif
        }

        /// <summary>
        /// A hobo with a machete throws his bottles too (owner, 2026-11-26): with someone standing off at a distance in his sight, a bottle in
        /// his hand in place of the machete, thrown, and the machete back in his hand.
        /// </summary>
        [UnityTest]
        public IEnumerator AHoboWithAMacheteThrowsABottleFromADistanceAndHasHisMacheteBack()
        {
            PlayerVitals vitals = SetUp("Hobo_1_Machete", 14.0f);
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            brain.RollsHowManyBottles = false;
            yield return null;
            brain.Target = vitals;   // as when the player has hurt him
            var thrower = _hobo.GetComponent<BottleThrower>();
            bool bottleShown = false;
            for (float clock = 0.0f; clock < 7.0f && brain.BottlesThrown == 0; clock += Dt)
            {
                vitals.TickHitstun(Dt);
                yield return null;
                bottleShown |= thrower.ShowsSpareInHand;
            }
            Assert.GreaterOrEqual(brain.BottlesThrown, 1, "from 14 m the machete hobo threw no bottle");
            Assert.IsTrue(bottleShown, "no bottle was shown in his hand for the throw");
            yield return Run(0.5f, vitals);
            Assert.IsFalse(thrower.ShowsSpareInHand, "the bottle stayed in his hand after the throw");
            var machete = _hobo.GetComponentInChildren<CityX.Items.IEquippableItem>() as Component;
            Assert.IsNotNull(machete, "he has no machete");
            foreach (Renderer drawn in machete.GetComponentsInChildren<Renderer>(true))
            {
                Assert.IsTrue(drawn.enabled, "his machete is still hidden after the throw");
            }
            Assert.AreEqual(2, _hobo.GetComponent<CarriedLoot>().Count(GlassBottleKinds.Classic), "the thrown bottle is still carried");
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
            Assert.AreEqual(0, brain.Dashes, "a hobo dashed (owner, 2026-11-26: \"make it so hobos dont have dash\")");
        }

        /// <summary>
        /// Most walk about with nothing in hand and take the weapon out when hit (owner, 2026-11-24: "make it so homeless guys dont usually walk
        /// around with weapon in hand maybe 20 percent of them do. they usually only pull it out when attacked"); a copy of him on another screen
        /// has nothing in hand either until it is told, then holds it too.
        /// </summary>
        [UnityTest]
        public IEnumerator HeTakesHisWeaponOutWhenHitAndSoDoesHisCopy()
        {
            PlayerVitals vitals = SetUp("Hobo_1_Machete", 8.0f);
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            brain.CalmArmedShare = 0.0f;
            _copy = Object.Instantiate(Prefab("Hobo_1_Machete"), new Vector3(30.0f, 0.05f, 0.0f), Quaternion.identity);
            var copy = _copy.GetComponent<SwordsmanBrain>();
            copy.enabled = false;   // another screen's copy
            var copyDriver = _copy.GetComponent<PlayerAnimationDriver>();
            _hobo.GetComponent<PlayerAnimationDriver>().TimedStatePlayed += (state, speed, start) =>
            {
                if (state == PlayerAnimationDriver.WeaponDrawnName) copyDriver.PlayRemoteState(state, speed, start);
            };
            yield return Run(2.0f, vitals);
            Assert.IsFalse(brain.WeaponDrawn, "he walks about with his machete out");
            Assert.IsTrue(brain.Combat == null || !brain.Combat.IsEquipped, "a machete is in his hand");
            Assert.IsFalse(copy.WeaponDrawn, "his copy walks about with the machete out");
            Assert.IsNull(_hobo.GetComponentInChildren<ISwordBlade>(), "a machete is on him");

            ((IDamageable)_hobo.GetComponent<PlayerVitals>()).ApplyHit(5.0f, _player.transform.position, 0.0f);
            yield return null;
            Assert.IsTrue(brain.WeaponDrawn, "hit, he did not take his machete out");
            Assert.IsTrue(brain.Combat != null && brain.Combat.IsEquipped, "hit, the machete is not in his hand");
            Assert.IsTrue(copy.WeaponDrawn, "his copy was not told he took it out");
            Assert.IsTrue(copy.Combat != null && copy.Combat.IsEquipped, "his copy has no machete in hand");
        }

        /// <summary>Brack guy wears the owner's outfit with the Brown Coat's hood up (owner, 2026-11-24: "make him wearing a brown coat, black gloves ,sneakers and shirt. and grey jeans. and put the hood on").</summary>
        [UnityTest]
        public IEnumerator BrackGuyWearsHisHoodUp()
        {
            PlayerVitals vitals = SetUp("Hobo_3_Pole", 8.0f);
            yield return null;
            var coat = _hobo.GetComponentInChildren<CityX.Wearables.HoodedCoat>();
            Assert.IsNotNull(coat, "brack guy has no coat on");
            Assert.AreEqual(ItemRegistry.BrownCoat, coat.ItemId);
            Assert.IsTrue(coat.HoodUp, "brack guy's hood is down");
            Assert.IsNotNull(vitals);
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
        /// A throw cut short by a knockdown: a copy of him on another screen stops showing the whole bottle in his hand too (bug sweep 2026-11-25:
        /// it showed it until his next throw).
        /// </summary>
        [UnityTest]
        public IEnumerator KnockedDownMidThrowTheCopyLetsGoOfTheSpareToo()
        {
            PlayerVitals vitals = SetUp("Hobo_2_Bottle", 14.0f);
            _copy = Object.Instantiate(Prefab("Hobo_2_Bottle"), new Vector3(30.0f, 0.05f, 0.0f), Quaternion.identity);
            _copy.GetComponent<SwordsmanBrain>().enabled = false;
            var copyDriver = _copy.GetComponent<PlayerAnimationDriver>();
            _hobo.GetComponent<PlayerAnimationDriver>().TimedStatePlayed += (state, speed, start) =>
            {
                if (state == PlayerAnimationDriver.WeaponDrawnName) copyDriver.PlayRemoteState(state, speed, start);
            };
            yield return null;
            var brain = _hobo.GetComponent<SwordsmanBrain>();
            var copyThrower = _copy.GetComponent<BottleThrower>();
            var copyBottle = _copy.GetComponentInChildren<BottleWeapon>();
            _hobo.GetComponent<BottleThrower>().Fired += evt => copyThrower.PlayRemote(evt);
            brain.Target = vitals;
            for (float clock = 0.0f; clock < 7.0f && !copyBottle.ShowsSpare; clock += Dt)
            {
                vitals.TickHitstun(Dt);
                yield return null;
            }
            Assert.IsTrue(copyBottle.ShowsSpare, "from 14 m he never began a throw");
            _hobo.GetComponent<Ragdoll>().Activate(Vector3.back * 3.0f);   // knocked down before the bottle left
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            Assert.AreEqual(0, brain.BottlesThrown, "the bottle left after all");
            Assert.IsFalse(copyBottle.ShowsSpare, "knocked down mid-throw, the copy still shows a whole bottle in his hand");
        }

        /// <summary>
        /// Hurt from a distance, he throws his whole bottles at the player and they hit; a copy of him on another screen carries one fewer each
        /// time, and shows a whole bottle in the hand as it goes. Then, beaten, the ones he has left are on his body.
        /// </summary>
        [UnityTest]
        public IEnumerator FromADistanceHeThrowsHisSpareBottlesAndTheRestAreLoot()
        {
            PlayerVitals vitals = SetUp("Hobo_2_Bottle", 14.0f);
            _hobo.GetComponent<SwordsmanBrain>().RollsHowManyBottles = false;   // all three (how many is rolled is tested apart)
            _copy = Object.Instantiate(Prefab("Hobo_2_Bottle"), new Vector3(30.0f, 0.05f, 0.0f), Quaternion.identity);
            _copy.GetComponent<SwordsmanBrain>().enabled = false;   // another screen's copy: only shown what he does
            var copyDriver = _copy.GetComponent<PlayerAnimationDriver>();
            _hobo.GetComponent<PlayerAnimationDriver>().TimedStatePlayed += (state, speed, start) =>
            {
                if (state == PlayerAnimationDriver.WeaponDrawnName) copyDriver.PlayRemoteState(state, speed, start);   // as the network tells it
            };
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
