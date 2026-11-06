using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Gear armour (docs/ARMOUR.md; owner, 2026-11-06: "there should certainly be stronger armor than others", "make sure it isnt too op for
    /// pvp"): worn pieces add a rating by slot and tier, a full heavy set stops about 40%, and no armour stops more than half of a player's
    /// blow or two thirds of anything else's; the sledgehammer goes through a share of it.
    /// </summary>
    public sealed class ArmourTests
    {
        private GameObject _body;

        [TearDown]
        public void TearDown()
        {
            if (_body != null) Object.DestroyImmediate(_body);
        }

        [Test]
        public void AFullHeavySetStopsAboutFortyPercent()
        {
            float stopped = StatCatalog.RatingReduction(ArmourRatings.BestSetRating);
            Assert.That(stopped, Is.InRange(0.38f, 0.42f), $"the best set stops {stopped:P0}");
        }

        [Test]
        public void HeavierPiecesProtectMoreAndClothesNotAtAll()
        {
            Assert.Less(ArmourRatings.TierShare(ArmourTier.Light), ArmourRatings.TierShare(ArmourTier.Medium));
            Assert.Less(ArmourRatings.TierShare(ArmourTier.Medium), ArmourRatings.TierShare(ArmourTier.Heavy));
            Assert.AreEqual(ArmourTier.Heavy, ArmourRatings.TierOf(ItemRegistry.KnightHelmet));
            Assert.AreEqual(ArmourRatings.HelmetBest, ArmourRatings.RatingOf(ItemRegistry.KnightHelmet));
            foreach (string clothing in new[] { ItemRegistry.TShirt, ItemRegistry.BlackJeans, ItemRegistry.WhiteSneaker, ItemRegistry.FarmerHat })
            {
                Assert.AreEqual(0.0f, ArmourRatings.RatingOf(clothing), $"{clothing} is clothing, not armour");
            }
            Assert.AreEqual(0.0f, ArmourRatings.RatingOf(ItemRegistry.Sledgehammer), "a weapon is not worn armour");
            Assert.AreEqual(0.0f, ArmourRatings.RatingOf(""));
        }

        [Test]
        public void ArmourIsCappedLowerAgainstAPlayersBlow()
        {
            Assert.AreEqual(ArmourRules.PlayerBlowCap, ArmourRules.Reduction(10000.0f, 0.0f, true), 1e-5f);
            Assert.AreEqual(ArmourRules.OtherBlowCap, ArmourRules.Reduction(10000.0f, 0.0f, false), 1e-5f);
            Assert.Less(ArmourRules.PlayerBlowCap, ArmourRules.OtherBlowCap);
            // Under the caps it is the stat's own rule, as before.
            Assert.AreEqual(StatCatalog.RatingReduction(50.0f), ArmourRules.Reduction(50.0f, 0.0f, true), 1e-5f);
        }

        [Test]
        public void PenetrationLowersTheRatingBeforeTheCap()
        {
            // The best gear and 20 points of the stat: 167 armour, 63% uncapped, so a player's blow meets the cap...
            float rating = ArmourRatings.BestSetRating + 100.0f;
            Assert.AreEqual(ArmourRules.PlayerBlowCap, ArmourRules.Reduction(rating, 0.0f, true), 1e-5f);
            // ...and a blow with 50% penetration meets half of it, under the cap.
            Assert.AreEqual(StatCatalog.RatingReduction(rating * 0.5f), ArmourRules.Reduction(rating, 0.5f, true), 1e-5f);
            Assert.Less(ArmourRules.Reduction(rating, 0.5f, true), ArmourRules.PlayerBlowCap);
            Assert.AreEqual(0.0f, ArmourRules.Reduction(10000.0f, 1.0f, false), 1e-5f, "whole penetration ignores all armour");
        }

        [Test]
        public void TheHammerGoesThroughAShareOfArmourOnTopOfTheStat()
        {
            var hammer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Sledgehammer.prefab").GetComponent<SwordWeapon>();
            var sword = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab").GetComponent<SwordWeapon>();
            Assert.AreEqual(ArmourRules.HammerPierce, ArmourRules.PierceOf(hammer), 1e-5f);
            Assert.AreEqual(0.0f, ArmourRules.PierceOf(sword), "a sword goes through no armour by itself");
            Assert.AreEqual(1.0f - 0.8f * (1.0f - ArmourRules.HammerPierce), ArmourRules.Combine(0.2f, ArmourRules.HammerPierce), 1e-5f);
            ItemCard card = ItemCards.Describe(ItemRegistry.Sledgehammer, hammer.gameObject);
            Assert.Contains($"Ignores {Mathf.RoundToInt(ArmourRules.HammerPierce * 100.0f)}% of armour", card.Notes, "the hammer's card does not say it");
        }

        [Test]
        public void AWornHelmetStopsBlowsAndBulletsButNotSpells()
        {
            PlayerVitals vitals = Body(wearHelmet: true);
            float stops = StatCatalog.RatingReduction(ArmourRatings.HelmetBest);
            Assert.AreEqual(ArmourRatings.HelmetBest, vitals.GearArmour, 1e-4f);
            Assert.AreEqual(20.0f * (1.0f - stops), Hit(vitals, DamageKind.Physical), 1e-3f);
            Assert.AreEqual(20.0f * (1.0f - stops), Hit(vitals, DamageKind.Bullet), 1e-3f);
            Assert.AreEqual(20.0f, Hit(vitals, DamageKind.Magic), 1e-3f, "a helmet stops spells: that is magic resistance's job");
        }

        [Test]
        public void TheCapCoversTheStatAndTheGearTogether()
        {
            PlayerVitals vitals = Body(wearHelmet: true);
            vitals.Stats[StatId.Armor] = 200;   // 1000 armour from the stat alone
            float before = vitals.Health;
            using (DamageContext.StruckByPlayer(true))
            {
                DamageContext.Strike(vitals, 20.0f, vitals.transform.position + Vector3.forward, 0.0f, DamageKind.Physical, 0.0f);
            }
            Assert.AreEqual(20.0f * (1.0f - ArmourRules.PlayerBlowCap), before - vitals.Health, 1e-3f, "a player's blow took less than its cap");
            Assert.AreEqual(20.0f * (1.0f - ArmourRules.OtherBlowCap), Hit(vitals, DamageKind.Physical), 1e-3f, "an enemy's blow took less than its cap");
        }

        [Test]
        public void AStrikerWhoIsNoPlayerIsCappedAsAnEnemy()
        {
            PlayerVitals vitals = Body(wearHelmet: false);
            vitals.Stats[StatId.Armor] = 200;
            var enemy = new GameObject("Enemy");
            try
            {
                float before = vitals.Health;
                using (DamageContext.StruckByPlayer(true))   // an outer player's scope must not leak onto an enemy's blow
                {
                    DamageContext.Strike(vitals, 20.0f, enemy.transform.position, 0.0f, DamageKind.Physical, 0.0f, null, enemy);
                }
                Assert.AreEqual(20.0f * (1.0f - ArmourRules.OtherBlowCap), before - vitals.Health, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void TheHelmetsCardSaysItsTierAndRating()
        {
            ItemCard helmet = ItemCards.Describe(ItemRegistry.KnightHelmet, null);
            Assert.AreEqual("Heavy Armour", helmet.Style);
            Assert.AreEqual(1, helmet.Rows.Count);
            Assert.AreEqual("Armour", helmet.Rows[0].Label);
            StringAssert.StartsWith(ArmourRatings.HelmetBest.ToString("0.#"), helmet.Rows[0].Text);
            Assert.AreEqual(0, ItemCards.Describe(ItemRegistry.TShirt, null).Rows.Count, "a T-shirt is no armour");
        }

        private PlayerVitals Body(bool wearHelmet)
        {
            _body = new GameObject("Body");
            var inventory = _body.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry
                {
                    ItemId = ItemRegistry.KnightHelmet,
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/KnightHelmet.prefab"),
                },
            };
            PlayerVitals vitals = _body.AddComponent<PlayerVitals>();
            vitals.Revive();
            if (wearHelmet)
            {
                Assert.IsTrue(inventory.EquipItemId(ItemRegistry.KnightHelmet, _body), "the helmet could not be put on");
            }
            return vitals;
        }

        private static float Hit(PlayerVitals vitals, DamageKind kind)
        {
            float before = vitals.Health;
            DamageContext.Strike(vitals, 20.0f, vitals.transform.position + Vector3.forward, 0.0f, kind, 0.0f);
            return before - vitals.Health;
        }
    }
}
