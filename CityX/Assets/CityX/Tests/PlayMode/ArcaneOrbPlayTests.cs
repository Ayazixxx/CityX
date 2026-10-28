using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Arcane Orb in play (docs/ARCANE_ORB.md): the ball hits someone for a little and curses them, and the orb strikes four seconds later for
    /// its damage; ranks give more orbs and more damage; re-hits stack to five and push the strike back; a wall gets no orbs; the bar spell pays
    /// 20 mana and waits its cooldown; another player's ball and orbs, shown on a copy, hurt nobody.
    /// </summary>
    public sealed class ArcaneOrbPlayTests
    {
        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public bool Alive = true;
            public float Taken;
            public int Hits;

            public bool IsAlive => Alive;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
                Hits++;
                return HitResponse.Landed;
            }
        }

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (CursedOrbs c in Object.FindObjectsByType<CursedOrbs>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
            foreach (ArcaneOrbProjectile b in Object.FindObjectsByType<ArcaneOrbProjectile>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            foreach (ArcaneBurst b in Object.FindObjectsByType<ArcaneBurst>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
        }

        private Dummy DummyAt(Vector3 position)
        {
            var go = new GameObject("Dummy");
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = new Vector3(1.0f, 2.6f, 1.0f);
            _made.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<Dummy>();
        }

        private ArcaneOrbCaster Caster(bool withVitals = false)
        {
            var go = new GameObject("Caster");
            _made.Add(go);
            if (withVitals)
            {
                go.AddComponent<PlayerVitals>();
            }
            return go.AddComponent<ArcaneOrbCaster>();
        }

        private static IEnumerator Frames(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds * 60.0f); i++)
            {
                yield return null;
            }
        }

        private static IEnumerator UntilCursed(Transform target, GameObject caster)
        {
            for (int i = 0; i < 90 && CursedOrbs.Find(target, caster) == null; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ABallThatHitsSomeoneCursesThemAndTheOrbStrikesFourSecondsLater()
        {
            Dummy dummy = DummyAt(new Vector3(0.0f, 1.3f, 6.0f));
            ArcaneOrbCaster caster = Caster();
            Assert.IsTrue(caster.TryCast(new Vector3(0.0f, 1.3f, 0.0f), Vector3.forward, null));
            yield return UntilCursed(dummy.transform, caster.gameObject);
            CursedOrbs curse = CursedOrbs.Find(dummy.transform, caster.gameObject);
            Assert.IsNotNull(curse, "the ball should have cursed whoever it hit");
            Assert.AreEqual(1, curse.Curse.Orbs, "rank 1: one orb");
            Assert.AreEqual(ArcaneOrbRules.BallDamagePerOrb, dummy.Taken, 0.01f, "the ball's own small hit");

            yield return Frames(3.5f);
            Assert.AreEqual(ArcaneOrbRules.BallDamagePerOrb, dummy.Taken, 0.01f, "the orb waits");
            yield return Frames(1.2f);
            Assert.AreEqual(ArcaneOrbRules.BallDamagePerOrb + ArcaneOrbRules.OrbDamage, dummy.Taken, 0.01f, "four seconds on, it strikes");
            Assert.IsTrue(curse == null, "spent, the curse is gone");
        }

        [UnityTest]
        public IEnumerator RankThreeLeavesThreeOrbsThatHitHarder()
        {
            Dummy dummy = DummyAt(new Vector3(0.0f, 1.3f, 6.0f));
            ArcaneOrbCaster caster = Caster();
            PlayerSkills skills = caster.gameObject.AddComponent<PlayerSkills>();
            int points = 2;
            skills.Book.TryRankUp(SkillCatalog.ArcaneOrb, ref points);
            skills.Book.TryRankUp(SkillCatalog.ArcaneOrb, ref points);
            Assert.AreEqual(3, caster.Rank, "setup: rank 3");

            Assert.IsTrue(caster.TryCast(new Vector3(0.0f, 1.3f, 0.0f), Vector3.forward, null));
            yield return UntilCursed(dummy.transform, caster.gameObject);
            Assert.AreEqual(3, CursedOrbs.Find(dummy.transform, caster.gameObject).Curse.Orbs);
            yield return Frames(5.0f);
            float factor = ArcaneOrbRules.DamageFactor(3);
            Assert.AreEqual((ArcaneOrbRules.BallDamagePerOrb * 3 + ArcaneOrbRules.OrbDamage * 3) * factor, dummy.Taken, 0.05f);
            Assert.AreEqual(4, dummy.Hits, "the ball, then each orb a blow of its own");
        }

        [UnityTest]
        public IEnumerator ReHitsStackToFiveOrbsAndPushTheStrikeBack()
        {
            Dummy dummy = DummyAt(new Vector3(0.0f, 1.3f, 6.0f));
            ArcaneOrbCaster caster = Caster();
            ArcaneOrbProjectile.Spawn(new Vector3(0.0f, 1.3f, 0.0f), Vector3.forward * ArcaneOrbRules.BallSpeed, 3, 1.0f, 10.0f, 0.0f, caster.gameObject, null);
            yield return UntilCursed(dummy.transform, caster.gameObject);
            CursedOrbs curse = CursedOrbs.Find(dummy.transform, caster.gameObject);
            yield return Frames(1.0f);
            float before = curse.Curse.SecondsLeft;
            ArcaneOrbProjectile.Spawn(new Vector3(0.0f, 1.3f, 0.0f), Vector3.forward * ArcaneOrbRules.BallSpeed, 3, 1.0f, 10.0f, 0.0f, caster.gameObject, null);
            for (int i = 0; i < 60 && curse.Curse.Orbs < 5; i++)
            {
                yield return null;
            }
            Assert.AreEqual(5, curse.Curse.Orbs, "three and three, but five at most");
            Assert.Greater(curse.Curse.SecondsLeft, before + 2.5f, "a full shot's re-hit pushes the strike back three seconds");
            Assert.LessOrEqual(curse.Curse.Age + curse.Curse.SecondsLeft, ArcaneOrbRules.LatestStrikeSeconds + 0.01f);
        }

        [UnityTest]
        public IEnumerator AWallGetsNoOrbs()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 1.3f, 6.0f);
            _made.Add(wall);
            Physics.SyncTransforms();
            ArcaneOrbCaster caster = Caster();
            yield return null;
            int curses = CursedOrbs.All.Count;
            Assert.IsTrue(caster.TryCast(new Vector3(0.0f, 1.3f, 0.0f), Vector3.forward, null));
            yield return Frames(1.0f);
            Assert.AreEqual(curses, CursedOrbs.All.Count, "only someone who can be hurt is cursed");
            Assert.AreEqual(0, Object.FindObjectsByType<ArcaneOrbProjectile>(FindObjectsSortMode.None).Length, "the ball burst on the wall");
        }

        [UnityTest]
        public IEnumerator TheBarSpellPaysTwentyManaAndWaitsItsCooldown()
        {
            ArcaneOrbCaster caster = Caster(withVitals: true);
            PlayerVitals vitals = caster.GetComponent<PlayerVitals>();
            float mana = vitals.Mana;
            Assert.IsTrue(caster.TryCast(Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(mana - ArcaneOrbRules.SpellManaCost, vitals.Mana, 0.01f);
            Assert.IsFalse(caster.TryCast(Vector3.zero, Vector3.forward, null), "on cooldown");
            caster.Tick(ArcaneOrbRules.SpellCooldownSeconds + 0.1f);
            vitals.TrySpendMana(vitals.Mana - 10.0f);
            Assert.IsFalse(caster.TryCast(Vector3.zero, Vector3.forward, null), "10 mana is not enough");
            Assert.AreEqual(10.0f, vitals.Mana, 0.01f, "and none is taken");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnotherPlayersBallAndOrbsShownOnACopyHurtNobody()
        {
            Dummy dummy = DummyAt(new Vector3(0.0f, 1.3f, 6.0f));
            ArcaneOrbCaster copy = Caster();
            copy.PlayRemote(new AbilityEvent(ArcaneOrbCaster.BallFired, new Vector3(0.0f, 1.3f, 0.0f), Vector3.forward, ArcaneOrbRules.BallSpeed, 0.0f, 2));
            Assert.AreEqual(1, Object.FindObjectsByType<ArcaneOrbProjectile>(FindObjectsSortMode.None).Length, "their ball flies here too");
            copy.PlayRemote(new AbilityEvent(ArcaneOrbCaster.OrbsPlaced, dummy.transform.position, Vector3.back, 4.0f, 0.0f, 2));
            CursedOrbs curse = CursedOrbs.Find(dummy.transform, copy.gameObject);
            Assert.IsNotNull(curse, "their orbs circle this machine's copy of whoever stood there");
            Assert.IsTrue(curse.Cosmetic);
            Assert.AreEqual(2, curse.Curse.Orbs);
            yield return Frames(5.0f);
            Assert.AreEqual(0.0f, dummy.Taken, "only the caster's own machine hurts anyone");
        }
    }
}
