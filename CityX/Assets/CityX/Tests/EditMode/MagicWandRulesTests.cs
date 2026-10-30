using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Magic Wand and the Magic Staff in numbers (docs/MAGIC_WAND.md; owner, 2026-10-30): the wand's speck once a second, the staff's a little
    /// harder and a little slower; a spell cast through the wand hurting 5% more and paying 10% less mana, and nothing of it once it is put away.
    /// </summary>
    public sealed class MagicWandRulesTests
    {
        private GameObject _body;
        private PlayerVitals _vitals;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _vitals = _body.AddComponent<PlayerVitals>();
            _vitals.Revive();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
        }

        private sealed class Focus : ISpellFocus
        {
            public float SpellPowerBonus => MagicSpeckRules.WandSpellPowerBonus;

            public float ManaCostFactor => MagicSpeckRules.WandManaCostFactor;
        }

        [Test]
        public void TheWandShootsOnceASecondAndTheStaffALittleHarderAndALittleSlower()
        {
            Assert.AreEqual(1.0f, MagicSpeckRules.Wand.Seconds, 1e-5f, "about one a second");
            Assert.Greater(MagicSpeckRules.Staff.Damage, MagicSpeckRules.Wand.Damage, "the staff's speck hits harder");
            Assert.Greater(MagicSpeckRules.Staff.Seconds, MagicSpeckRules.Wand.Seconds, "and comes slower");
            float share = MagicSpeckRules.Staff.DamagePerSecond / MagicSpeckRules.Wand.DamagePerSecond;
            Assert.That(share, Is.InRange(1.0f, 1.15f), $"the staff does {share:P0} of the wand's damage a second: only slightly more");
            Assert.Greater(MagicSpeckRules.Staff.Size, MagicSpeckRules.Wand.Size);
            Assert.AreEqual(MagicSpeckRules.Staff.Kind, MagicSpeckRules.For(MagicSpeckRules.StaffKind).Kind);
            Assert.AreEqual(MagicSpeckRules.Wand.Kind, MagicSpeckRules.For(99).Kind, "an unknown kind from another player is the wand's");
        }

        [Test]
        public void ASpellCastThroughTheWandHurtsFivePercentMoreAndPaysATenthLessMana()
        {
            const float spell = 20.0f;
            Assert.AreEqual(spell, CombatTuning.DamageFor(spell, PlayerVitals.SpellPowerOf(_vitals)), 1e-4f, "no wand, no boost");

            _vitals.SpellFocus = new Focus();
            Assert.AreEqual(spell * 1.05f, CombatTuning.DamageFor(spell, PlayerVitals.SpellPowerOf(_vitals)), 1e-4f, "5% more damage");
            float full = _vitals.Mana;
            Assert.IsTrue(_vitals.TrySpendMana(10.0f));
            Assert.AreEqual(full - 9.0f, _vitals.Mana, 1e-4f, "10% less mana");
            Assert.IsTrue(_vitals.TrySpendMana(_vitals.Mana + 5.0f), "a spell a little dearer than the mana left is paid for at the wand's price");

            _vitals.SpellFocus = null;
            Assert.AreEqual(spell, CombatTuning.DamageFor(spell, PlayerVitals.SpellPowerOf(_vitals)), 1e-4f, "the boost went with the wand");
        }
    }
}
