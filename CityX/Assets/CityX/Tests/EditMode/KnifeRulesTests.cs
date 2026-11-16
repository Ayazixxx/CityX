using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The knife against the one-handed sword, on numbers alone (docs/KNIFE.md; owner, 2026-10-29: "knives should do less damage on average,
    /// maybe have like 10% less reach and be around 30% faster swing speed"), and where a backstab comes from.
    /// </summary>
    public sealed class KnifeRulesTests
    {
        private sealed class Blade : ISwordBlade
        {
            public bool Knife;

            public Vector3 BladeBaseWorld => Vector3.zero;

            public Vector3 BladeTipWorld => Vector3.forward;

            public bool IsOneHanded => true;

            public bool IsKnife => Knife;

            public float GetMeleeDamage() => Knife ? KnifeRules.BaseDamage : CombatTuning.PunchDamage;
        }

        private GameObject _object;
        private SoulsSwordCombat _combat;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Combat");
            _combat = _object.AddComponent<SoulsSwordCombat>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        /// <summary>Damage a second through the light chain (back and forth) for the blade in hand: each blow over the time to the next.</summary>
        private float ChainDamagePerSecond(ISwordBlade blade)
        {
            _combat.Equip(blade);
            float damage = 0.0f, seconds = 0.0f;
            for (int i = 0; i < _combat.LightChainLength; i++)
            {
                SoulsAttack attack = _combat.LightAttack(i);
                damage += blade.GetMeleeDamage() * attack.DamageScale;
                seconds += (attack.Startup + attack.Active + attack.Recovery * attack.ChainAt) / _combat.BladeSwingSpeed;
            }
            return damage / seconds;
        }

        [Test]
        public void AKnifeSwingsAThirdFasterReachesAlmostAsFarAndHits9AsOftenAsItSwings()
        {
            var sword = new Blade { Knife = false };
            var knife = new Blade { Knife = true };
            _combat.Equip(sword);
            float swordReach = _combat.Reach;
            Assert.AreEqual(1.0f, _combat.BladeSwingSpeed);
            _combat.Equip(knife);
            Assert.IsTrue(_combat.IsKnife && _combat.IsOneHanded);
            Assert.AreEqual(1.3f, _combat.BladeSwingSpeed, 1e-4f, "about 30% faster");
            Assert.AreEqual(0.9f * swordReach, _combat.Reach, 1e-4f, "10% less reach");

            float swordDps = ChainDamagePerSecond(sword);
            float knifeDps = ChainDamagePerSecond(knife);
            // Owner, 2026-11-15: "make the kitchen knife do 9 damage instead of 7". A blow is 9 against the sword's 10, and it swings 1.3 times as
            // often, so it does about 1.17 times the sword's damage a second (at 7 it was 0.91).
            Assert.AreEqual(9.0f, KnifeRules.BaseDamage);
            Assert.AreEqual(KnifeRules.DamagePerSecondShare, knifeDps / swordDps, 0.05f);
        }

        [Test]
        public void AKnifesLightsStaggerNoOneItsHeavyStillBreaksGuardsAndItsGuardIsThin()
        {
            _combat.Equip(new Blade { Knife = true });
            Assert.AreEqual(0.0f, _combat.LightAttack(0).HitstunSeconds, "a knife's light must not stop what its target is doing");
            Assert.AreEqual(0.0f, _combat.LightAttack(1).HitstunSeconds);
            Assert.IsTrue(_combat.HeavyAttack.BreaksGuard, "the lunging stab is a heavy: it breaks guards");
            Assert.Greater(_combat.HeavyAttack.LungeMetres, _combat.OneHandHeavy.LungeMetres, "the knife's heavy lunges in");
            Assert.Less(_combat.LightAttack(0).StaminaCost, _combat.OneHandLight1.StaminaCost, "a knife's light is cheaper");
            float knifeGuard = _combat.GuardStaminaPerDamage;
            _combat.Equip(new Blade { Knife = false });
            Assert.Greater(knifeGuard, _combat.GuardStaminaPerDamage, "a knife's guard pays more for each blow");
            Assert.Greater(_combat.LightAttack(0).HitstunSeconds, 0.0f, "a sword's light still staggers");
        }

        [Test]
        public void ABackstabComesFromTheBodysBackThirdAndAKnifesIsTheBiggest()
        {
            Vector3 body = Vector3.zero;
            Vector3 facing = Vector3.forward;
            Assert.IsTrue(Backstab.IsBehind(body, facing, new Vector3(0.0f, 0.0f, -1.5f)), "straight behind");
            Assert.IsTrue(Backstab.IsBehind(body, facing, new Vector3(0.7f, 0.5f, -1.5f)), "behind and a little to the side, a little higher");
            Assert.IsFalse(Backstab.IsBehind(body, facing, new Vector3(1.5f, 0.0f, 0.0f)), "beside is not behind");
            Assert.IsFalse(Backstab.IsBehind(body, facing, new Vector3(0.0f, 0.0f, 1.5f)), "face to face is not behind");
            Assert.Greater(Backstab.MultiplierFor(true), Backstab.MultiplierFor(false));
            Assert.Greater(Backstab.MultiplierFor(false), 1.0f, "every one-handed blade backstabs a little");
        }
    }
}
