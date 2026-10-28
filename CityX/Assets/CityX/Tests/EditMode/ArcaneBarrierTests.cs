using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Arcane Barrier's rules (docs/ARCANE_BARRIER.md): what its face covers (in front of it, and the line to the caster's chest crossing it), where
    /// a blow shows on it, its health by rank, its mana, and how see-through its face is drawn.
    /// </summary>
    public sealed class ArcaneBarrierTests
    {
        // A wall before a body at the origin facing +Z, on the built body.
        private static readonly Vector3 Centre = new Vector3(0.0f, ArcaneBarrierRules.Bottom + ArcaneBarrierRules.Height * 0.5f, ArcaneBarrierRules.Distance);
        private static readonly Vector3 Chest = Vector3.up * (BodyRig.StandingHeight * ArcaneBarrierRules.ChestShare);

        private static bool Covers(Vector3 from) =>
            ArcaneBarrierRules.Covers(Centre, Vector3.forward, Vector3.up, ArcaneBarrierRules.Width * 0.5f, ArcaneBarrierRules.Height * 0.5f, from, Chest);

        [Test]
        public void ItCoversWhatComesAtItsFace()
        {
            Assert.IsTrue(Covers(new Vector3(0.0f, 1.6f, 20.0f)), "a shot from straight ahead");
            Assert.IsTrue(Covers(new Vector3(6.0f, 1.0f, 12.0f)), "a shot from ahead and to one side");
            Assert.IsTrue(Covers(new Vector3(0.0f, 9.0f, 15.0f)), "a shot from a balcony ahead");
            Assert.IsTrue(Covers(new Vector3(0.4f, 0.0f, 2.3f)), "a blade from someone standing against it");
        }

        [Test]
        public void ItCoversNothingBehindBesideOrOverIt()
        {
            Assert.IsFalse(Covers(new Vector3(0.0f, 1.6f, -10.0f)), "from behind");
            Assert.IsFalse(Covers(new Vector3(10.0f, 1.6f, 0.5f)), "from the side");
            Assert.IsFalse(Covers(new Vector3(8.0f, 1.6f, 2.0f)), "from round its edge");
            Assert.IsFalse(Covers(new Vector3(0.0f, 30.0f, 3.0f)), "from high above");
        }

        [Test]
        public void ABlowShowsWhereItsLineCrossesTheWall()
        {
            Vector3 point = ArcaneBarrierRules.StruckPoint(Centre, Vector3.forward, Vector3.up, ArcaneBarrierRules.Width * 0.5f,
                ArcaneBarrierRules.Height * 0.5f, new Vector3(0.0f, Chest.y, 20.0f), Chest);
            Assert.AreEqual(Centre.z, point.z, 1e-3f, "on the wall's face");
            Assert.AreEqual(Chest.y, point.y, 1e-3f, "where the line meets it");
            Vector3 edge = ArcaneBarrierRules.StruckPoint(Centre, Vector3.forward, Vector3.up, ArcaneBarrierRules.Width * 0.5f,
                ArcaneBarrierRules.Height * 0.5f, new Vector3(20.0f, Chest.y, 4.0f), Chest);
            Assert.LessOrEqual(edge.x, ArcaneBarrierRules.Width * 0.5f + 1e-3f, "kept on the wall");
        }

        [Test]
        public void EachRankAddsATenthToItsHealth()
        {
            Assert.AreEqual(ArcaneBarrierRules.BaseHealth, ArcaneBarrierRules.HealthFor(1), 1e-3f);
            Assert.AreEqual(ArcaneBarrierRules.BaseHealth * 1.1f, ArcaneBarrierRules.HealthFor(2), 1e-3f);
            Assert.AreEqual(ArcaneBarrierRules.BaseHealth * 1.5f, ArcaneBarrierRules.HealthFor(6), 1e-3f);
            Assert.AreEqual(ArcaneBarrierRules.BaseHealth, ArcaneBarrierRules.HealthFor(0), 1e-3f, "no rank is rank 1");
        }

        [Test]
        public void HoldingItCostsMoreManaThanComesBack()
        {
            Assert.Greater(ArcaneBarrierRules.UpkeepManaPerSecond, PlayerVitals.ManaRegenPerSecond, "it could be held for ever");
            Assert.Less(ArcaneBarrierRules.RaiseManaCost, PlayerVitals.BaseMaxMana);
        }

        /// <summary>Owner: "make sure its transparent enough to see through".</summary>
        [Test]
        public void ItsFaceIsSeeThroughWithABrightRim()
        {
            Assert.Less(ArcaneVisuals.WallAlpha(0.5f, 0.5f), 0.3f, "the middle");
            Assert.Less(ArcaneVisuals.WallAlpha(0.3f, 0.7f), 0.3f, "off the middle");
            Assert.Greater(ArcaneVisuals.WallAlpha(0.04f, 0.5f), 0.9f, "the rim");
            Assert.Less(ArcaneVisuals.WallAlpha(0.0f, 0.5f), 0.1f, "past the rim");
        }
    }
}
