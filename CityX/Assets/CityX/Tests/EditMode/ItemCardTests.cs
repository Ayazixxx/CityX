using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The appraisal card (docs/ITEM_INSPECT.md) says what an item is and the numbers it fights with, read from the same place the weapon
    /// reads them; the arrows point the better way against the item in hand; clothes have their slot and no numbers yet.
    /// </summary>
    public sealed class ItemCardTests
    {
        private static ItemCard Card(string itemId, string prefabName)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/" + prefabName + ".prefab");
            Assert.IsNotNull(prefab, "no " + prefabName + ".prefab: rebuild the scene");
            return ItemCards.Describe(itemId, prefab);
        }

        private static int Index(ItemCard card, string label)
        {
            for (int i = 0; i < card.Rows.Count; i++)
            {
                if (card.Rows[i].Label == label) return i;
            }
            Assert.Fail(card.Name + " has no '" + label + "' line");
            return -1;
        }

        private static CardRow Row(ItemCard card, string label) => card.Rows[Index(card, label)];

        [Test]
        public void EveryItemHasACardWithANameATypeAndARarity()
        {
            foreach (string itemId in ItemRegistry.Ids)
            {
                ItemCard card = ItemCards.Describe(itemId, null);
                Assert.IsFalse(string.IsNullOrEmpty(card.Name), itemId + " has no name");
                Assert.IsFalse(string.IsNullOrEmpty(card.Kind), itemId + " has no kind");
                Assert.IsTrue(card.TypeLine.Contains(" / "), itemId + "'s type line '" + card.TypeLine + "' does not say how it is held");
                Assert.AreEqual(ItemRarity.Of(itemId), card.Rarity);
            }
        }

        [Test]
        public void TheKnivesAreQuickOneHandedBladesWithTheBiggestBackstab()
        {
            ItemCard knife = Card(ItemRegistry.KitchenKnife, "KitchenKnife");
            Assert.AreEqual("Knife", knife.Kind);
            Assert.AreEqual(ItemCards.OneHand, knife.Hands);
            Assert.AreEqual("Knife / One Hand", knife.TypeLine);
            Assert.AreEqual("One-Handed Melee", knife.Style);
            Assert.AreEqual(ItemCards.MeleeDamageStat, knife.ScalesWith);
            Assert.AreEqual(KnifeRules.BaseDamage, Row(knife, "Attack").Number, 1e-4f);
            Assert.AreEqual(KnifeRules.SwingSpeed, Row(knife, "Speed").Number, 1e-4f);
            Assert.AreEqual(KnifeRules.ReachShare, Row(knife, "Reach").Number, 1e-4f);
            Assert.IsTrue(knife.Notes.Exists(n => n.Contains("Backstab x3")), "the knife's backstab is not on its card");

            ItemCard butter = Card(ItemRegistry.ButterKnife, "ButterKnife");
            Assert.AreEqual(KnifeRules.DullDamage, Row(butter, "Attack").Number, 1e-4f);
            Assert.IsTrue(butter.Notes.Exists(n => n.Contains("No edge")), "the butter knife's dull blade is not on its card");

            ItemCard bat = Card(ItemRegistry.BaseballBat, "BaseballBat");
            Assert.AreEqual("Bat / Two Hands", bat.TypeLine);
            Assert.AreEqual("Two-Handed Melee", bat.Style);
            Assert.IsFalse(bat.Notes.Exists(n => n.Contains("Backstab")), "a two-handed bat has no backstab");
        }

        [Test]
        public void TheGunsReadTheirProfiles()
        {
            ItemCard glock = Card(ItemRegistry.Pistol, "Pistol");
            FirearmProfile profile = FirearmProfile.Glock();
            Assert.AreEqual("Pistol / One Hand", glock.TypeLine);
            Assert.AreEqual("Handguns", glock.Style);
            Assert.AreEqual(ItemCards.FirearmDamageStat, glock.ScalesWith);
            Assert.AreEqual(profile.Damage, Row(glock, "Attack").Number, 1e-4f);
            Assert.AreEqual(profile.MagazineSize, Row(glock, "Magazine").Number, 1e-4f);
            Assert.AreEqual(profile.Range, Row(glock, "Range").Number, 1e-4f);
            StringAssert.Contains("semi", Row(glock, "Fire rate").Text);

            ItemCard ak = Card(ItemRegistry.AK47, "AK47");
            Assert.AreEqual("Rifle / Two Hands", ak.TypeLine);
            Assert.AreEqual("Long Guns", ak.Style);
            StringAssert.Contains("auto", Row(ak, "Fire rate").Text);

            ItemCard crossbow = Card(ItemRegistry.Crossbow, "Crossbow");
            Assert.AreEqual("Crossbow / Two Hands", crossbow.TypeLine);
            Assert.AreEqual("Archery and Throwing", crossbow.Style);
            StringAssert.Contains("bolts", Row(crossbow, "Magazine").Text);

            ItemCard slingshot = Card(ItemRegistry.Slingshot, "Slingshot");
            Assert.AreEqual("Slingshot / Two Hands", slingshot.TypeLine);
            Assert.AreEqual(SlingshotRules.FullDamage, Row(slingshot, "Attack").Number, 1e-4f);
            Assert.IsFalse(Row(slingshot, "Draw").HigherIsBetter, "a quicker draw is the better one");

            ItemCard boomerang = Card(ItemRegistry.WoodenBoomerang, "Boomerang");
            Assert.AreEqual("Boomerang / One Hand", boomerang.TypeLine);
            Assert.AreEqual(BoomerangRules.Damage, Row(boomerang, "Attack").Number, 1e-4f);
            Assert.AreEqual(BoomerangRules.Range, Row(boomerang, "Range").Number, 1e-4f);
            Assert.AreEqual(Rarity.Uncommon, ItemRarity.Of(ItemRegistry.WoodenBoomerang));
        }

        [Test]
        public void AStaffSaysWhatItCastsAndWhatItCosts()
        {
            ItemCard flame = Card(ItemRegistry.FlameStaff, "FlameStaff");
            Assert.AreEqual("Staff / Two Hands", flame.TypeLine);
            Assert.AreEqual("Magic Weapons", flame.Style);
            Assert.AreEqual(ItemCards.SpellPowerStat, flame.ScalesWith);
            StringAssert.Contains("Flames", Row(flame, "Casts").Text);
            Assert.AreEqual(FlameStreamRules.DamagePerSecond, Row(flame, "Attack").Number, 1e-4f);
            Assert.AreEqual(FlameStreamRules.ManaPerSecond, Row(flame, "Mana").Number, 1e-4f);
            Assert.IsFalse(Row(flame, "Mana").HigherIsBetter, "cheaper mana is the better");

            ItemCard wand = Card(ItemRegistry.MagicWand, "MagicWand");
            Assert.AreEqual("Wand / One Hand", wand.TypeLine);
            Assert.AreEqual("Free", Row(wand, "Mana").Text);
            Assert.AreEqual(MagicSpeckRules.WandDamage, Row(wand, "Attack").Number, 1e-4f);
            Assert.IsTrue(wand.Notes.Exists(n => n.Contains("+5%")), "the wand's boost is not on its card");

            ItemCard healing = Card(ItemRegistry.HealingStaff, "HealingStaff");
            Assert.AreEqual(HealingBeamRules.HealPerSecond, Row(healing, "Heals").Number, 1e-4f);
            Assert.IsFalse(healing.Rows.Exists(r => r.Label == "Attack"), "a healing staff attacks nothing");
        }

        [Test]
        public void ClothesHaveTheirSlotAndNoNumbersYet()
        {
            ItemCard shirt = ItemCards.Describe(ItemRegistry.TShirt, null);
            Assert.AreEqual("Top / Worn", shirt.TypeLine);
            Assert.AreEqual(0, shirt.Rows.Count, "a T-shirt is clothing: no armour line (docs/ARMOUR.md)");
            Assert.AreEqual("Headwear / Worn", ItemCards.Describe(ItemRegistry.KnightHelmet, null).TypeLine);
            Assert.AreEqual("Shoes / Worn", ItemCards.Describe(ItemRegistry.WhiteSneaker, null).TypeLine);
            Assert.AreEqual("Shield / Worn", ItemCards.Describe(ItemRegistry.RoundShield, null).TypeLine);
        }

        [Test]
        public void TheArrowsPointTheBetterWay()
        {
            ItemCard knife = Card(ItemRegistry.KitchenKnife, "KitchenKnife");
            ItemCard sword = Card(ItemRegistry.CombatSword, "CombatSword");
            Assert.AreEqual(-1, knife.Compare(Index(knife, "Attack"), sword), "the knife hits softer than the sword");
            Assert.AreEqual(1, sword.Compare(Index(sword, "Attack"), knife));
            Assert.AreEqual(1, knife.Compare(Index(knife, "Speed"), sword), "the knife is the quicker");
            Assert.AreEqual(-1, knife.Compare(Index(knife, "Reach"), sword));
            Assert.AreEqual(0, knife.Compare(Index(knife, "Attack"), null), "nothing in hand, nothing to compare");
            Assert.AreEqual(0, knife.Compare(Index(knife, "Attack"), ItemCards.Describe(ItemRegistry.TShirt, null)), "a shirt has no attack to compare");

            ItemCard wand = Card(ItemRegistry.MagicWand, "MagicWand");
            ItemCard flame = Card(ItemRegistry.FlameStaff, "FlameStaff");
            Assert.AreEqual(1, wand.Compare(Index(wand, "Mana"), flame), "free is the better mana");
        }

        [Test]
        public void RarityFollowsTheOwnersList()
        {
            Assert.AreEqual(Rarity.Common, ItemRarity.Of(ItemRegistry.MetalCrowbar));
            Assert.AreEqual(Rarity.Uncommon, ItemRarity.Of(ItemRegistry.Pistol));
            Assert.AreEqual(Rarity.Rare, ItemRarity.Of(ItemRegistry.AK47));
            Assert.AreEqual(Rarity.VeryRare, ItemRarity.Of(ItemRegistry.Jetpack));
            Assert.AreEqual(Rarity.Common, ItemRarity.Of("no_such_item"));
            Assert.AreEqual("Very Rare", ItemRarity.Word(Rarity.VeryRare));
            foreach (string itemId in ItemRegistry.Ids)
            {
                Assert.AreNotEqual(Rarity.Godly, ItemRarity.Of(itemId), "nothing is godly yet (the owner's list has none)");
            }
        }
    }
}
