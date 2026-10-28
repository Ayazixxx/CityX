using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Arcane Orb's numbers (docs/ARCANE_ORB.md): the staff's charge on Shadow Flare's frames, the bar spell's ranks, and the curse's clock --
    /// four seconds to the strike, re-hits stacking to five and pushing it back, never past 7.5 s from the first, nothing added once striking.
    /// </summary>
    public sealed class ArcaneOrbTests
    {
        [Test]
        public void TheStaffsChargeGivesTwoOrbsFromSeventeenFramesAndThreeFromTwentySix()
        {
            Assert.AreEqual(1, ArcaneOrbRules.OrbsForCharge(0.0f), "a click");
            Assert.AreEqual(1, ArcaneOrbRules.OrbsForCharge(16.0f / 60.0f));
            Assert.AreEqual(2, ArcaneOrbRules.OrbsForCharge(17.0f / 60.0f));
            Assert.AreEqual(2, ArcaneOrbRules.OrbsForCharge(25.0f / 60.0f));
            Assert.AreEqual(3, ArcaneOrbRules.OrbsForCharge(26.0f / 60.0f));
            Assert.AreEqual(3, ArcaneOrbRules.OrbsForCharge(10.0f), "held on, still three");
        }

        [Test]
        public void EachRankAddsAnOrbUpToThreeAndATenthMoreDamage()
        {
            Assert.AreEqual(1, ArcaneOrbRules.OrbsForRank(1), "just learnt: one orb");
            Assert.AreEqual(2, ArcaneOrbRules.OrbsForRank(2));
            Assert.AreEqual(3, ArcaneOrbRules.OrbsForRank(3));
            Assert.AreEqual(3, ArcaneOrbRules.OrbsForRank(20), "no more than three a shot");
            Assert.AreEqual(1.0f, ArcaneOrbRules.DamageFactor(1), 1e-5f);
            Assert.AreEqual(1.2f, ArcaneOrbRules.DamageFactor(3), 1e-5f);
            Assert.AreEqual(2.0f, ArcaneOrbRules.DamageFactor(11), 1e-5f);
        }

        [Test]
        public void TheOrbsStrikeFourSecondsAfterTheHit()
        {
            var curse = new ArcaneCurse();
            Assert.AreEqual(1, curse.Hit(1));
            Assert.AreEqual(1, curse.Orbs);
            Assert.AreEqual(4.0f, curse.SecondsLeft, 1e-5f);
            Assert.IsFalse(curse.Tick(3.9f), "not yet");
            Assert.IsTrue(curse.Tick(0.2f), "four seconds in, they strike");
            Assert.IsTrue(curse.Striking);
            Assert.IsFalse(curse.Tick(1.0f), "the strike begins once");
        }

        [Test]
        public void ReHitsStackToFiveAndPushTheStrikeBackButNoLaterThanSevenAndAHalfSeconds()
        {
            var curse = new ArcaneCurse();
            curse.Hit(1);
            curse.Tick(1.0f);
            Assert.AreEqual(3, curse.Hit(3), "a full shot's three join the one");
            Assert.AreEqual(4, curse.Orbs);
            Assert.AreEqual(6.0f, curse.SecondsLeft, 1e-4f, "three left, pushed back three more by a full shot");
            Assert.AreEqual(1, curse.Hit(3), "only one more fits: five at most");
            Assert.AreEqual(5, curse.Orbs);
            Assert.AreEqual(6.5f, curse.SecondsLeft, 1e-4f, "7.5 s from the first hit, of which one has gone");
            Assert.AreEqual(0, curse.Hit(1), "full");
        }

        [Test]
        public void AOneOrbReHitPushesHalfASecondAndATwoOrbOneMoreThanThat()
        {
            Assert.AreEqual(0.5f, ArcaneOrbRules.ExtensionFor(1), 1e-5f);
            Assert.AreEqual(1.75f, ArcaneOrbRules.ExtensionFor(2), 1e-5f);
            Assert.AreEqual(3.0f, ArcaneOrbRules.ExtensionFor(3), 1e-5f);
        }

        [Test]
        public void NothingIsAddedOnceTheOrbsStrike()
        {
            var curse = new ArcaneCurse();
            curse.Hit(2);
            curse.Tick(4.0f);
            Assert.IsTrue(curse.Striking);
            Assert.AreEqual(0, curse.Hit(3));
            Assert.AreEqual(2, curse.Orbs);
        }

        [Test]
        public void AStaffShotPaysByTheOrbAndCarriesNoMoreThanTheManaPaysFor()
        {
            Assert.AreEqual(24.0f, ArcaneOrbRules.StaffManaFor(3), 1e-5f);
            Assert.AreEqual(3, ArcaneOrbRules.StaffOrbsAffordable(100.0f, 3));
            Assert.AreEqual(2, ArcaneOrbRules.StaffOrbsAffordable(20.0f, 3), "20 mana pays for two");
            Assert.AreEqual(1, ArcaneOrbRules.StaffOrbsAffordable(8.0f, 3));
            Assert.AreEqual(0, ArcaneOrbRules.StaffOrbsAffordable(7.9f, 1), "not even one");
        }

        [Test]
        public void TheSpellIsTheArcanistsRootAndRanksForEver()
        {
            SkillNode node = SkillCatalog.Find(SkillCatalog.ArcaneOrb);
            Assert.IsNotNull(node);
            Assert.AreEqual("arcanist", node.TreeId);
            Assert.AreEqual(SkillKind.Spell, node.Kind);
            Assert.IsTrue(node.HasRanks);
            Assert.AreEqual(SkillNode.EndlessRanks, node.MaxRank);
            Assert.AreEqual(ArcaneOrbCaster.Id, SkillCatalog.ArcaneOrb, "the same name the ability sends to other players");
        }
    }
}
