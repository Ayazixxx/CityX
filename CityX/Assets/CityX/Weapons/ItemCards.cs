using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// FILLS AN ITEM'S CARD (Items.ItemCard, docs/ITEM_INSPECT.md) from what its prefab is: the sword's own blade settings, the gun's profile,
    /// the bow's draw, which staff it is and the rules that staff shoots by. Every number is the one the weapon fights with, read from the same
    /// constant or field, so the card can never say one thing and the blow do another. Clothes get their slot; armour its tier and rating (docs/ARMOUR.md).
    /// </summary>
    public static class ItemCards
    {
        public const string OneHand = "One Hand";
        public const string TwoHands = "Two Hands";
        public const string Worn = "Worn";

        public const string MeleeDamageStat = "Melee Damage";
        public const string FirearmDamageStat = "Firearm Damage";
        public const string SpellPowerStat = "Spell Power";

        /// <summary>The card of <paramref name="itemId"/>, read off <paramref name="prefab"/> (its model with its weapon component; null for the name alone).</summary>
        public static ItemCard Describe(string itemId, GameObject prefab)
        {
            bool known = ItemRegistry.TryGet(itemId, out ItemDefinition definition);
            var card = new ItemCard(itemId, known ? definition.DisplayName : itemId, ItemRarity.Of(itemId));
            if (known && (definition.IsWearable || definition.EquipSlot == EquipSlots.Shield))
            {
                WornOn(card, definition.EquipSlot);
                Armour(card);
                return card;
            }
            if (prefab != null)
            {
                if (prefab.GetComponent<SwordWeapon>() is SwordWeapon sword) Melee(card, sword);
                else if (prefab.GetComponent<PistolWeapon>() is PistolWeapon gun) Gun(card, gun.Profile);
                else if (prefab.GetComponent<BowWeapon>() is BowWeapon bow) Drawn(card, bow.Profile);
                else if (prefab.GetComponent<StaffItem>() is StaffItem staff) Staff(card, staff);
                else if (prefab.GetComponent<Flamethrower>() != null) FlamethrowerOf(card);
                else if (prefab.GetComponent<GrenadeItem>() != null) Grenade(card);
                else if (prefab.GetComponent<AmmoBox>() is AmmoBox ammo) Ammo(card, ammo);
                else if (prefab.GetComponent<BoomerangItem>() != null) Boomerang(card);
                else if (prefab.GetComponent<BottleWeapon>() != null) Bottle(card);
                else if (prefab.GetComponent<FlightGear>() != null) Gear(card, "Flight gear", "On the back", "Space takes off and climbs; let go to hover; Ctrl sinks");
                else if (prefab.GetComponent<Parachute>() != null) Gear(card, "Parachute", "On the back", "Opens in a fall and floats you down");
                else if (prefab.GetComponent<GrapplingHookItem>() != null) Gear(card, "Grapple", OneHand, "Fires a rope at what you aim at and swings you from it");
                else if (prefab.GetComponent<SprayCanItem>() != null) Gear(card, "Spray can", OneHand, "Right-click a wall to paint graffiti");
                else if (prefab.GetComponent<FlashlightItem>() != null) Gear(card, "Flashlight", OneHand, "F or a click switches it; it points where you look");
                else if (prefab.GetComponent<JerryCanItem>() != null) Gear(card, "Gas can", OneHand, "Held, it pours gasoline for any fire to light");
            }
            if (card.Kind == "Item" && known)
            {
                card.Kind = definition.Category == "tool" ? "Tool" : "Weapon";
            }
            if (HitTypes.TryGet(itemId, out string hits))
            {
                card.Hits = hits;
            }
            return card;
        }

        /// <summary>A box of rounds: what loads it, how many a box holds and a bag slot takes.</summary>
        private static void Ammo(ItemCard card, AmmoBox ammo)
        {
            card.Kind = "Ammunition";
            card.Hands = "In the bag";
            card.Style = "For the " + ammo.ForGuns;
            card.Add("Per box", ammo.Rounds, ammo.Rounds.ToString());
            int stack = ItemRegistry.TryGet(ammo.ItemId, out ItemDefinition definition) ? definition.StackSize : 0;
            card.Add("Per bag slot", stack, stack.ToString());
            card.Note("Kept in the bag; R reloads from it");
        }

        private static void Melee(ItemCard card, SwordWeapon sword)
        {
            bool knife = sword.Knife, oneHanded = sword.IsOneHanded, spear = sword.IsSpear, staff = sword.IsStaff;
            card.Kind = knife ? "Knife" : MeleeKind(card.ItemId, oneHanded);
            card.Hands = spear ? "One or two hands" : oneHanded ? OneHand : TwoHands;
            card.Style = oneHanded ? "One-Handed Melee" : "Two-Handed Melee";
            card.ScalesWith = MeleeDamageStat;
            card.Add("Attack", sword.GetMeleeDamage(), sword.GetMeleeDamage().ToString("0.#"));
            card.Add("Speed", knife ? KnifeRules.SwingSpeed : 1.0f, knife ? "Quick (x" + KnifeRules.SwingSpeed.ToString("0.0") + ")" : "Normal");
            float reach = spear ? SpearRules.ReachShare : staff ? StaffRules.ReachShare : knife ? KnifeRules.ReachShare : 1.0f;
            card.Add("Reach", reach, reach == 1.0f ? "Full" : Mathf.RoundToInt(reach * 100.0f) + "%");
            if (spear)
            {
                card.Note("Thrusts; hold the click to sweep");
                card.Note("Goes beside a shield");
            }
            if (staff)
            {
                card.Note("A chain of three; hold the click to spin");
                card.Note("The fourth and the spin knock down");
            }
            if (sword.IsHammer)
            {
                card.Note("Ignores " + Mathf.RoundToInt(ArmourRules.HammerPierce * 100.0f) + "% of armour");
            }
            if (oneHanded)
            {
                card.Note("Backstab x" + Backstab.MultiplierFor(knife).ToString("0.#") + " from behind");
            }
            if (sword.Dull)
            {
                card.Note("No edge: it bruises, and cuts nothing off");
            }
        }

        private static string MeleeKind(string itemId, bool oneHanded)
        {
            switch (itemId)
            {
                case ItemRegistry.MetalCrowbar: return "Crowbar";
                case ItemRegistry.BaseballBat: return "Bat";
                case ItemRegistry.Sledgehammer: return "Hammer";
                case ItemRegistry.Spear: return "Spear";
                case ItemRegistry.BoStaff: return "Staff";
                case ItemRegistry.CombatSword: return "Sword";
                case ItemRegistry.SpikeSword: return "Greatsword";
                default: return oneHanded ? "Blade" : "Greatsword";
            }
        }

        private static void Gun(ItemCard card, FirearmProfile profile)
        {
            bool bolts = profile.ArrowFallSpeed > 0.0f, rifle = profile.Kind == FirearmKind.Rifle, pellets = profile.Pellets > 1;
            card.Kind = bolts ? "Crossbow" : pellets ? "Shotgun" : rifle ? "Rifle" : "Pistol";
            card.Hands = bolts || rifle ? TwoHands : OneHand;
            card.Style = bolts ? "Archery and Throwing" : rifle ? "Long Guns" : "Handguns";
            card.ScalesWith = FirearmDamageStat;
            card.Add("Attack", profile.Damage * profile.Pellets, profile.Damage.ToString("0.#") + (pellets ? " x " + profile.Pellets + " pellets" : ""));
            card.Add("Fire rate", profile.FireRatePerSecond, profile.FireRatePerSecond.ToString("0.#") + "/s " + (profile.Automatic ? "auto" : "semi"));
            card.Add("Magazine", profile.MagazineSize, profile.MagazineSize + (bolts ? " bolts" : pellets ? " shells" : " rounds"));
            card.Add("Range", profile.Range, profile.Range.ToString("0") + " m");
        }

        private static void Drawn(ItemCard card, DrawnProfile profile)
        {
            card.Kind = profile.Kind == DrawnKind.Slingshot ? "Slingshot" : "Bow";
            card.Hands = TwoHands;
            card.Style = "Archery and Throwing";
            card.ScalesWith = FirearmDamageStat;
            card.Add("Attack", profile.FullDamage, profile.TapDamage.ToString("0") + "-" + profile.FullDamage.ToString("0") + " by the draw");
            card.Add("Range", profile.FullFlatRange, profile.TapFlatRange.ToString("0") + "-" + profile.FullFlatRange.ToString("0") + " m");
            card.Add("Draw", profile.FullDrawSeconds, profile.FullDrawSeconds.ToString("0.#") + " s to full", false);
        }

        private static void Staff(ItemCard card, StaffItem staff)
        {
            bool wand = staff is MagicWand;
            card.Kind = wand ? "Wand" : "Staff";
            card.Hands = wand ? OneHand : TwoHands;
            card.Style = "Magic Weapons";
            card.ScalesWith = SpellPowerStat;
            switch (staff)
            {
                case FireballStaff _:
                    card.Add("Casts", "Fireball, held to charge");
                    card.Add("Attack", FireballProjectile.BaseDamage, FireballProjectile.BaseDamage.ToString("0") + " a fireball");
                    card.Add("Fire rate", 1.0f / FireballCaster.StaffShotSeconds, (1.0f / FireballCaster.StaffShotSeconds).ToString("0.#") + "/s");
                    card.Add("Mana", FireballCaster.StaffLeastMana, FireballCaster.StaffLeastMana.ToString("0") + "-" + FireballCaster.StaffMostMana.ToString("0") + " a shot", false);
                    break;
                case IceShardStaff _:
                    card.Add("Casts", "Ice shards, held");
                    card.Add("Attack", IceShardProjectile.BaseDamage, IceShardProjectile.BaseDamage.ToString("0") + " a shard");
                    card.Add("Fire rate", 1.0f / IceShardCaster.StaffShotSeconds, (1.0f / IceShardCaster.StaffShotSeconds).ToString("0.#") + "/s");
                    card.Add("Mana", IceShardCaster.StaffManaPerShard, IceShardCaster.StaffManaPerShard.ToString("0") + " a shard", false);
                    break;
                case ArcaneStaff _:
                    card.Add("Casts", "Arcane Orb, charged to 2 or 3");
                    card.Add("Attack", ArcaneOrbRules.OrbDamage, ArcaneOrbRules.OrbDamage.ToString("0") + " an orb");
                    card.Add("Mana", ArcaneOrbRules.StaffManaPerOrb, ArcaneOrbRules.StaffManaPerOrb.ToString("0") + " an orb", false);
                    break;
                case ArcaneBeamStaff _:
                    Held(card, "Arcane beam", ArcaneBeamRules.DamagePerSecond, ArcaneBeamRules.ManaPerSecond);
                    break;
                case FlameStaff _:
                    Held(card, "Flames", FlameStreamRules.DamagePerSecond, FlameStreamRules.ManaPerSecond);
                    break;
                case FrostStaff _:
                    Held(card, "Frost", FrostRules.DamagePerSecond, FrostRules.ManaPerSecond);
                    card.Note("Slows what it touches, then freezes it");
                    break;
                case WaterStaff _:
                    Held(card, "Water jet", WaterJetRules.DamagePerTick / WaterJetRules.TickSeconds, WaterJetRules.ManaPerSecond);
                    card.Note("Shoves bodies back and knocks them down");
                    break;
                case HealingStaff _:
                    card.Add("Casts", "Healing beam, held");
                    card.Add("Heals", HealingBeamRules.HealPerSecond, HealingBeamRules.HealPerSecond.ToString("0") + "/s");
                    card.Add("Mana", HealingBeamRules.ManaPerSecond, HealingBeamRules.ManaPerSecond.ToString("0") + "/s", false);
                    break;
                case MagicWand _:
                    Speck(card, MagicSpeckRules.WandDamage, MagicSpeckRules.WandShotSeconds);
                    card.Note("Spells cast through it: +" + Mathf.RoundToInt(MagicSpeckRules.WandSpellDamageBonus * 100.0f) + "% damage, " +
                              Mathf.RoundToInt((1.0f - MagicSpeckRules.WandManaCostFactor) * 100.0f) + "% less mana");
                    break;
                case MagicStaff _:
                    Speck(card, MagicSpeckRules.StaffDamage, MagicSpeckRules.StaffShotSeconds);
                    break;
                case WraithStaff _:
                    card.Add("Casts", "Summons a Wraith that fights for you, one at a time");
                    card.Add("Summon", Wraith.SummonedMaxHealth, Wraith.SummonedMaxHealth.ToString("0") + " health");
                    card.Add("Mana", WraithSummonCaster.ManaCost, WraithSummonCaster.ManaCost.ToString("0") + " a summon", false);
                    break;
            }
        }

        private static void Held(ItemCard card, string spell, float damagePerSecond, float manaPerSecond)
        {
            card.Add("Casts", spell + ", held");
            card.Add("Attack", damagePerSecond, damagePerSecond.ToString("0.#") + "/s");
            card.Add("Mana", manaPerSecond, manaPerSecond.ToString("0.#") + "/s", false);
        }

        private static void Speck(ItemCard card, float damage, float shotSeconds)
        {
            card.Add("Casts", "Magic speck");
            card.Add("Attack", damage, damage.ToString("0"));
            card.Add("Fire rate", 1.0f / shotSeconds, (1.0f / shotSeconds).ToString("0.#") + "/s");
            card.Add("Mana", 0.0f, "Free", false);
        }

        private static void FlamethrowerOf(ItemCard card)
        {
            card.Kind = "Flamethrower";
            card.Hands = TwoHands;
            card.Style = "Special";
            card.ScalesWith = FirearmDamageStat;
            card.Add("Attack", Flamethrower.DamagePerSecond, Flamethrower.DamagePerSecond.ToString("0") + "/s");
            card.Add("Range", Flamethrower.Range, Flamethrower.Range.ToString("0") + " m");
            card.Note("Sets what it touches alight");
        }

        private static void Grenade(ItemCard card)
        {
            card.Kind = "Grenade";
            card.Hands = OneHand;
            card.Style = "Explosives";
            card.Add("Attack", GrenadeRules.Damage, GrenadeRules.Damage.ToString("0") + " at the middle");
            card.Add("Radius", GrenadeRules.Radius, GrenadeRules.Radius.ToString("0.#") + " m");
            card.Add("Fuse", GrenadeRules.FuseSeconds.ToString("0.#") + " s");
            card.Note("Thrown; a stack of " + ItemRegistry.GrenadeStack);
        }

        private static void Boomerang(ItemCard card)
        {
            card.Kind = "Boomerang";
            card.Hands = OneHand;
            card.Style = "Archery and Throwing";
            card.ScalesWith = MeleeDamageStat;
            card.Add("Attack", BoomerangRules.Damage, BoomerangRules.Damage.ToString("0"));
            card.Add("Range", BoomerangRules.Range, BoomerangRules.Range.ToString("0") + " m");
            card.Note("Comes back, through walls, hitting everyone it passes");
        }

        private static void Bottle(ItemCard card)
        {
            card.Kind = "Bottle";
            card.Hands = OneHand;
            card.Style = "Improvised";
            card.ScalesWith = MeleeDamageStat;
            card.Add("Attack", BottleWeapon.IntactMeleeDamage, BottleWeapon.IntactMeleeDamage.ToString("0") + ", broken " + BottleWeapon.BrokenMeleeDamage.ToString("0"));
            card.Note("Breaks on a blow, and can be thrown");
        }

        private static void Gear(ItemCard card, string kind, string hands, string note)
        {
            card.Kind = kind;
            card.Hands = hands;
            card.Note(note);
        }

        /// <summary>A piece of armour's tier and rating, and the share of a blow the rating alone stops (clothes: nothing to say).</summary>
        private static void Armour(ItemCard card)
        {
            ArmourTier tier = ArmourRatings.TierOf(card.ItemId);
            float rating = ArmourRatings.RatingOf(card.ItemId);
            if (tier == ArmourTier.None || rating <= 0.0f)
            {
                return;
            }
            card.Style = ArmourRatings.NameOf(tier);
            card.Add("Armour", rating, rating.ToString("0.#") + " (stops " + Mathf.RoundToInt(StatCatalog.RatingReduction(rating) * 100.0f) + "%)");
        }

        private static void WornOn(ItemCard card, string slot)
        {
            card.Hands = Worn;
            card.Style = slot == EquipSlots.Shield ? "Shield" : "Clothing";
            switch (slot)
            {
                case EquipSlots.Helmet: card.Kind = "Headwear"; break;
                case EquipSlots.Torso: card.Kind = "Top"; break;
                case EquipSlots.Legs: card.Kind = "Legs"; break;
                case EquipSlots.Shoes: card.Kind = "Shoes"; break;
                case EquipSlots.Shield: card.Kind = "Shield"; break;
                case EquipSlots.Back: card.Kind = "Back"; break;
                default: card.Kind = "Wearable"; break;
            }
        }
    }
}
