using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A sword equipped, swung through its own ComboChain -- same technique
    /// MeleeCombatPlayTests already establishes for the unarmed chain, aimed
    /// at ComboChain.Sword's own two stages plus its heavy variant instead.
    /// The chain/window/finisher machinery itself is already covered there;
    /// this only checks that a weapon-driven chain actually engages it and
    /// that the sword's own numbers (base damage, heavy stage) come through.
    /// </summary>
    public class SwordCombatPlayTests
    {
        private GameObject _attackerObject;
        private MeleeAttacker _attacker;
        private SwordWeapon _sword;
        private GameObject _dummyObject;
        private SparringDummy _dummy;

        [SetUp]
        public void SetUp()
        {
            _attackerObject = new GameObject("Attacker");
            _attackerObject.transform.position = Vector3.zero;
            _attackerObject.transform.rotation = Quaternion.identity;
            _attacker = _attackerObject.AddComponent<MeleeAttacker>();

            var swordObject = new GameObject("Sword");
            _sword = swordObject.AddComponent<SwordWeapon>();
            _sword.OnEquip(_attackerObject, CityX.Items.ItemInstanceData.None);

            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.name = "Dummy";
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            _dummy = _dummyObject.AddComponent<SparringDummy>();
            _dummy.Reset();

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackerObject != null) Object.DestroyImmediate(_attackerObject);
            if (_dummyObject != null) Object.DestroyImmediate(_dummyObject);
        }

        private void Step(float seconds, float slice = 1.0f / 60.0f)
        {
            for (float t = 0.0f; t < seconds; t += slice)
            {
                _attacker.Tick(Mathf.Min(slice, seconds - t));
            }
        }

        [Test]
        public void EquippingASwordSwingsThroughItsOwnChainRatherThanTheFlatPath()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual("sword_attack", _attacker.CurrentStage.Animation,
                "the sword did not engage its own combo chain");
        }

        [Test]
        public void TheChainWalksBothStagesThenResets()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Sword);

            for (int i = 0; i < chain.Length; i++)
            {
                Assert.IsTrue(_attacker.TryAttack(), $"could not start sword stage {i}");
                Assert.AreEqual(chain[i].Animation, _attacker.CurrentStage.Animation);
                Step(_sword.MeleeCooldownSeconds + 0.01f);
            }

            Assert.AreEqual(chain.Length, _dummy.TimesHit, "not every stage of the sword chain landed");
            Assert.AreEqual(0, _attacker.NextStageIndex, "the sword chain did not reset after its finisher");
        }

        /// <summary>
        /// SAME damage, not more -- an earlier version of this test asserted
        /// the opposite ("the finisher hits harder"), on the reasoning that a
        /// two-stage chain should escalate like the unarmed one's own
        /// haymaker does. Checked directly against combo_data.gd instead of
        /// trusting that reasoning: sword_attack and sword_attack2 both carry
        /// damage_scale = 1.0, with no escalation between them at all. What
        /// makes sword_attack2 a finisher is that it ragdolls the target and
        /// closes the chain (see ComboChain.Sword's own doc comment), not
        /// that it hits harder -- the real trade the chain offers is
        /// "commit to two matching swings for a knockdown", not "grind
        /// toward one big number".
        /// </summary>
        [Test]
        public void TheFinisherDealsTheSameDamageAsTheOpener()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Sword);
            Assert.IsTrue(chain.Length >= 2, "this test needs a real multi-stage chain to compare");

            Assert.IsTrue(_attacker.TryAttack());
            Step(_sword.MeleeCooldownSeconds + 0.01f);
            float opener = _dummy.TotalDamageTaken;

            Assert.IsTrue(_attacker.TryAttack());
            Step(_sword.MeleeCooldownSeconds + 0.01f);
            float finisher = _dummy.TotalDamageTaken - opener;

            Assert.AreEqual(opener, finisher, 0.001f,
                $"the sword finisher did {finisher:0.0} against an opener's {opener:0.0}");
            Assert.AreEqual(0.0f, _dummy.LastHitstun, 0.001f,
                "the sword finisher applied hitstun as well as ragdolling");
        }

        [Test]
        public void DamageScalesAgainstTheSwordsOwnBaseNotThePunchBase()
        {
            ComboStage opener = ComboChain.For(ComboChain.Sword)[0];
            float expected = CombatTuning.DamageFor(SwordWeapon.BaseMeleeDamage * opener.DamageScale, 0);

            Assert.IsTrue(_attacker.TryAttack());
            Step(_sword.MeleeCooldownSeconds + 0.01f);

            Assert.AreEqual(expected, _dummy.TotalDamageTaken, 0.01f);
        }

        [Test]
        public void HoldingForAHeavySwingPlaysTheSwordsOwnHeavyStage()
        {
            Assert.IsTrue(_attacker.TryAttack(heavy: true));
            Assert.AreEqual(CombatTuning.SwordHeavyAttack.Animation, _attacker.CurrentStage.Animation);

            Step(_sword.MeleeCooldownSeconds + 0.01f);
            float expected = CombatTuning.DamageFor(
                SwordWeapon.BaseMeleeDamage * CombatTuning.SwordHeavyAttack.DamageScale, 0);
            Assert.AreEqual(expected, _dummy.TotalDamageTaken, 0.01f);
        }

        /// <summary>
        /// A held swing commits rather than upgrading whichever chain stage
        /// was next -- the same "heavy resets, does not advance" rule
        /// CombatTuning.HeavyPunch already enforces for bare fists.
        /// </summary>
        [Test]
        public void AHeavySwingResetsTheChainRatherThanAdvancingIt()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Step(_sword.MeleeCooldownSeconds + 0.01f);
            Assert.AreEqual(1, _attacker.NextStageIndex, "setup failed -- the chain never advanced");

            Assert.IsTrue(_attacker.TryAttack(heavy: true));
            Step(_sword.MeleeCooldownSeconds + 0.01f);

            Assert.AreEqual(0, _attacker.NextStageIndex, "a heavy swing left the chain mid-combo");
        }

        [Test]
        public void UnequippingReturnsToTheFlatUnarmedChain()
        {
            _sword.OnUnequip(_attackerObject);

            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(ComboChain.For(ComboChain.Unarmed)[0].Animation, _attacker.CurrentStage.Animation,
                "unequipping the sword did not return combat to bare fists");
        }
    }
}
