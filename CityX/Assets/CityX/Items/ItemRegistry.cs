using System.Collections.Generic;

namespace CityX.Items
{
    /// <summary>
    /// Every item in the game, by id (ported from items/item_registry.gd). Pure data: prefabs are wired elsewhere (Inventory.ItemPrefabs),
    /// so this stays testable without Unity objects and is the one place each item id is spelled.
    /// </summary>
    public static class ItemRegistry
    {
        public const string GlassBottle = "glass_bottle";
        public const string HoodedCloak = "hooded_cloak";
        public const string MidnightCoat = "midnight_coat";
        public const string GreenFurHoodedParka = "green_fur_hooded_parka";
        public const string MaroonFurHoodedParka = "maroon_fur_hooded_parka";
        public const string TShirt = "t_shirt";
        public const string BloodRavenTShirt = "blood_raven_t_shirt";
        public const string RedCamoTShirt = "red_camo_t_shirt";
        public const string BlackTShirt = "black_t_shirt";
        public const string CityXShirt = "cityx_shirt";
        public const string RedStripedTShirt = "red_striped_t_shirt";
        public const string LongSleeveTShirt = "long_sleeve_t_shirt";
        public const string BlackLongSleeveTShirt = "black_long_sleeve_t_shirt";
        public const string BlackJeans = "black_jeans";
        public const string BlueJeans = "blue_jeans";
        public const string BredJeans = "bred_jeans";
        public const string MaroonJeans = "maroon_jeans";
        public const string Khakis = "khakis";
        public const string GreyJeans = "grey_jeans";
        public const string WhiteSneaker = "white_sneaker";
        public const string BWSneakers = "bw_sneakers";
        public const string GrapplingHook = "grappling_hook";
        public const string Pistol = "pistol";
        public const string Jetpack = "jetpack";
        public const string Parachute = "parachute";
        public const string AngelWings = "angel_wings";
        public const string DarkAngelWings = "dark_angel_wings";
        public const string SpikeSword = "spike_sword";
        public const string CombatSword = "combat_sword";
        public const string MetalCrowbar = "metal_crowbar";
        public const string BaseballBat = "baseball_bat";
        public const string Sledgehammer = "sledgehammer";

        public const string Spear = "spear";
        public const string BoStaff = "bo_staff";
        public const string Chain = "chain";
        public const string KitchenKnife = "kitchen_knife";
        public const string ButterKnife = "butter_knife";
        public const string Flamethrower = "flamethrower";
        public const string AK47 = "ak47";

        public const string Shotgun = "shotgun";

        /// <summary>A shoulder-fired rocket launcher after the RPG-7 (docs/RPG.md).</summary>
        public const string RPG = "rpg";
        public const string SprayCan = "spray_can";
        public const string KnightHelmet = "knight_helmet";
        public const string FarmerHat = "farmer_hat";
        public const string ClawSkull = "claw_skull";
        public const string ThreeDGlasses = "3d_glasses";
        public const string RoundShield = "round_shield";

        public const string RiotShield = "riot_shield";
        public const string ArcaneStaff = "arcane_staff";
        public const string WaterStaff = "water_staff";
        public const string FlameStaff = "flame_staff";
        public const string JerryCan = "jerry_can";
        public const string Bow = "bow";
        public const string Crossbow = "crossbow";
        public const string Slingshot = "slingshot";

        /// <summary>Thrown and caught, as Terraria's (docs/BOOMERANG.md).</summary>
        public const string WoodenBoomerang = "woodenboomerang";

        /// <summary>As tall as the player, carried on the back, drawn into an aim or thrown straight off it (docs/BOOMERANG.md).</summary>
        public const string GiantBoomerang = "giantboomerang";

        /// <summary>A Giant Boomerang of spiked steel, very rare, hitting harder (owner, 2026-11-19; docs/BOOMERANG.md).</summary>
        public const string IronSpikedGiantBoomerang = "ironspikedgiantboomerang";
        public const string FireballStaff = "fireball_staff";
        public const string IceShardStaff = "ice_shard_staff";
        public const string ArcaneBeamStaff = "arcane_beam_staff";
        public const string HealingStaff = "healing_staff";
        public const string Flashlight = "flashlight";
        public const string FrostStaff = "frost_staff";
        public const string MagicWand = "magic_wand";
        public const string MagicStaff = "magic_staff";
        public const string WraithStaff = "wraith_staff";
        public const string Grenade = "grenade";
        public const string Semtex = "semtex";

        /// <summary>How many grenades make a stack: they are spent as they are thrown, the next one coming into the hand.</summary>
        public const int GrenadeStack = 10;

        /// <summary>
        /// AMMUNITION (owner, 2026-11-21: "make a basic ammunition model for each type of gun that is an in game right now as an item spawn you can
        /// spawn and pick up into your inventory and press r to reload into your guns"). Kept in the bag as rounds, a stack each; a gun reloads
        /// from it once its own spare rounds are spent (PistolWeapon, FirearmProfile.AmmoItemId). Never equipped.
        /// </summary>
        public const string AmmoCategory = "ammo";

        public const string PistolAmmo = "pistol_ammo";
        public const string RifleAmmo = "rifle_ammo";
        public const string ShotgunShells = "shotgun_shells";
        public const string RpgRockets = "rpg_rockets";

        /// <summary>The most rounds of each kind one bag slot holds.</summary>
        public const int PistolAmmoStack = 240;
        public const int RifleAmmoStack = 300;
        public const int ShotgunShellStack = 60;
        public const int RpgRocketStack = 6;

        private static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>
        {
            [GlassBottle] = new ItemDefinition(GlassBottle, "Glass Bottle", "weapon", EquipSlots.Primary, 1),
            [HoodedCloak] = new ItemDefinition(HoodedCloak, "Hooded Cloak", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            [MidnightCoat] = new ItemDefinition(MidnightCoat, "Midnight Coat", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // Meshy's parka remodelled onto this body (Wearables/FurHoodedParka); each colour is its own item.
            [GreenFurHoodedParka] = new ItemDefinition(GreenFurHoodedParka, "Green Fur Hooded Parka", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            [MaroonFurHoodedParka] = new ItemDefinition(MaroonFurHoodedParka, "Maroon Fur Hooded Parka", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            [TShirt] = new ItemDefinition(TShirt, "T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The T-Shirt with the Blood Raven logo printed on it (Wearables/Prints/BloodRavenPrint).
            [BloodRavenTShirt] = new ItemDefinition(BloodRavenTShirt, "Blood Raven T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The T-Shirt covered all over in red camo (Wearables/Prints/RedCamoPrint).
            [RedCamoTShirt] = new ItemDefinition(RedCamoTShirt, "Red Camo T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The plain T-Shirt in black cotton.
            [BlackTShirt] = new ItemDefinition(BlackTShirt, "Black T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The black T-Shirt with the game's own logo printed on it (Wearables/Prints/CityXPrint); the owner's name for it.
            [CityXShirt] = new ItemDefinition(CityXShirt, "cityXshirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The T-Shirt in red and white stripes (TShirtBuilder.BuildRedStriped).
            [RedStripedTShirt] = new ItemDefinition(RedStripedTShirt, "Red Striped T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The T-Shirt with its sleeves down to the wrists, in white cotton and in black.
            [LongSleeveTShirt] = new ItemDefinition(LongSleeveTShirt, "Long Sleeve T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            [BlackLongSleeveTShirt] = new ItemDefinition(BlackLongSleeveTShirt, "Black Long Sleeve T-Shirt", ItemDefinition.WearableCategory,
                EquipSlots.Torso, 1),
            // Denim jeans: the owner's cut of a Meshy model, fitted to this body (Wearables/Jeans), in black, blue, black-and-red, maroon, khaki and grey.
            [BlackJeans] = new ItemDefinition(BlackJeans, "Black Jeans", ItemDefinition.WearableCategory, EquipSlots.Legs, 1),
            [BlueJeans] = new ItemDefinition(BlueJeans, "Blue Jeans", ItemDefinition.WearableCategory, EquipSlots.Legs, 1),
            [BredJeans] = new ItemDefinition(BredJeans, "Bred Jeans", ItemDefinition.WearableCategory, EquipSlots.Legs, 1),
            [MaroonJeans] = new ItemDefinition(MaroonJeans, "Maroon Jeans", ItemDefinition.WearableCategory, EquipSlots.Legs, 1),
            [Khakis] = new ItemDefinition(Khakis, "Khakis", ItemDefinition.WearableCategory, EquipSlots.Legs, 1),
            [GreyJeans] = new ItemDefinition(GreyJeans, "Grey Jeans", ItemDefinition.WearableCategory, EquipSlots.Legs, 1),
            // A pair of chunky sneakers: the owner's trim of a Meshy model, each shoe on its foot (Wearables/Shoes), in white and in black and white.
            [WhiteSneaker] = new ItemDefinition(WhiteSneaker, "White Sneaker", ItemDefinition.WearableCategory, EquipSlots.Shoes, 1),
            [BWSneakers] = new ItemDefinition(BWSneakers, "BW Sneakers", ItemDefinition.WearableCategory, EquipSlots.Shoes, 1),
            [GrapplingHook] = new ItemDefinition(GrapplingHook, "Grappling Hook", "weapon", EquipSlots.Primary, 1),
            [Pistol] = new ItemDefinition(Pistol, "Pistol", "weapon", EquipSlots.Primary, 1),
            // The jetpack and the parachute share the back slot: worn with a coat, never with each other (which would Space work?).
            [Jetpack] = new ItemDefinition(Jetpack, "Jetpack", "tool", EquipSlots.Back, 1),
            [Parachute] = new ItemDefinition(Parachute, "Parachute", "tool", EquipSlots.Back, 1),
            // Angel wings (2026-10-28): flight gear like the jetpack, on the same back slot (docs/WINGS.md).
            [AngelWings] = new ItemDefinition(AngelWings, "Angel Wings", "tool", EquipSlots.Back, 1),
            // The same wings in black (2026-10-29, docs/WINGS.md).
            [DarkAngelWings] = new ItemDefinition(DarkAngelWings, "Dark Angel Wings", "tool", EquipSlots.Back, 1),
            // Two-handed: fought with the greatsword's moves.
            [SpikeSword] = new ItemDefinition(SpikeSword, "Spike Sword", "weapon", EquipSlots.Primary, 1),
            // One-handed, with its own attacks and guard; the crowbar is fought with its moves, the bat with the greatsword's.
            [CombatSword] = new ItemDefinition(CombatSword, "Combat Sword", "weapon", EquipSlots.Primary, 1),
            [MetalCrowbar] = new ItemDefinition(MetalCrowbar, "Metal Crowbar", "weapon", EquipSlots.Primary, 1),
            [BaseballBat] = new ItemDefinition(BaseballBat, "Baseball Bat", "weapon", EquipSlots.Primary, 1),
            // Two-handed and blunt: the greatsword's moves, with an overhead smash for its third light (owner, 2026-11-05).
            [Sledgehammer] = new ItemDefinition(Sledgehammer, "Sledgehammer", "weapon", EquipSlots.Primary, 1),
            // The spear (2026-11-18, docs/SPEAR.md): thrusts, a sweep for the heavy, beside a shield or in both hands. Character/SpearRules.
            [Spear] = new ItemDefinition(Spear, "Spear", "weapon", EquipSlots.Primary, 1),
            // The bo staff (2026-11-19, docs/BO_STAFF.md): a plain metal pole, swung near one end in both hands, from the owner's clip. Character/StaffRules.
            [BoStaff] = new ItemDefinition(BoStaff, "Bo Staff", "weapon", EquipSlots.Primary, 1),
            // The chain (2026-11-22, docs/CHAIN.md): worn wound round the wrist and the palm, thrown out and lashed in one hand. Character/ChainRules.
            [Chain] = new ItemDefinition(Chain, "Chain", "weapon", EquipSlots.Primary, 1),
            // A knife: one-handed, quicker and lighter than the sword, with the biggest backstab (docs/KNIFE.md, KnifeRules).
            [KitchenKnife] = new ItemDefinition(KitchenKnife, "Kitchen Knife", "weapon", EquipSlots.Primary, 1),
            // A dull knife: the kitchen knife's moves, softer blows, no edge.
            [ButterKnife] = new ItemDefinition(ButterKnife, "Butter Knife", "weapon", EquipSlots.Primary, 1),
            [Flamethrower] = new ItemDefinition(Flamethrower, "Flamethrower", "weapon", EquipSlots.Primary, 1),
            // Automatic; its handling is Weapons/FirearmProfile.AK47.
            [AK47] = new ItemDefinition(AK47, "AK-47", "weapon", EquipSlots.Primary, 1),
            // The pump shotgun (2026-11-18, docs/SHOTGUN.md): pellets, a pump after every shot, loaded a shell at a time. Weapons/FirearmProfile.Shotgun.
            [Shotgun] = new ItemDefinition(Shotgun, "Pump Shotgun", "weapon", EquipSlots.Primary, 1),
            // The RPG (2026-11-19, docs/RPG.md): a rocket that flies and bursts, loaded one at a time at the muzzle. Weapons/FirearmProfile.RPG.
            [RPG] = new ItemDefinition(RPG, "RPG", "weapon", EquipSlots.Primary, 1),
            // Right-click a wall with it to paint graffiti (GraffitiMenuView).
            [SprayCan] = new ItemDefinition(SprayCan, "Spray Can", "tool", EquipSlots.Primary, 1),
            // Hats and helmets on the head; the face (the mask slot) is worn beside them.
            [KnightHelmet] = new ItemDefinition(KnightHelmet, "Knight Helmet", ItemDefinition.WearableCategory, EquipSlots.Helmet, 1),
            // A wide straw hat: Meshy's, cut off its figure (Wearables/FarmerHat).
            [FarmerHat] = new ItemDefinition(FarmerHat, "Farmer Hat", ItemDefinition.WearableCategory, EquipSlots.Helmet, 1),
            // The Crimson Claw's own head, worn (Wearables/ClawSkull).
            [ClawSkull] = new ItemDefinition(ClawSkull, "Claw Skull", ItemDefinition.WearableCategory, EquipSlots.Helmet, 1),
            // White paper cinema glasses, red and blue lenses seen through (Wearables/ThreeDGlasses): on the face, so with any hat.
            [ThreeDGlasses] = new ItemDefinition(ThreeDGlasses, "3D Glasses", ItemDefinition.WearableCategory, EquipSlots.Mask, 1),
            // A plain wooden round shield on the left forearm, to test shields with until the owner's model (Wearables/RoundShield, docs/SHIELD.md).
            [RoundShield] = new ItemDefinition(RoundShield, "Round Shield", "shield", EquipSlots.Shield, 1),
            // The riot shield (owner, 2026-11-18; Wearables/RiotShield, docs/SHIELD.md): beside a pistol, a one-handed melee weapon or a wand.
            [RiotShield] = new ItemDefinition(RiotShield, "Riot Shield", "shield", EquipSlots.Shield, 1),
            // Magic staffs (docs/STAFFS.md): the spell on the click, every shot paying mana. Drawn in code until the owner's models.
            [ArcaneStaff] = new ItemDefinition(ArcaneStaff, "Arcane Staff", "weapon", EquipSlots.Primary, 1),
            // The water staff (2026-10-28): a fire hose of a staff, held to spray (docs/STAFFS.md).
            [WaterStaff] = new ItemDefinition(WaterStaff, "Water Staff", "weapon", EquipSlots.Primary, 1),
            // The flame staff (2026-10-28): a flamethrower of a staff, held to burn, paid in mana (docs/STAFFS.md).
            [FlameStaff] = new ItemDefinition(FlameStaff, "Flame Staff", "weapon", EquipSlots.Primary, 1),
            // The gas can (2026-10-28, docs/FIRE.md): held to pour gasoline on the ground, for any fire to light. Shown as "Gas Can"
            // (owner, 2026-10-29: "just rename the item to Gas Can"); its id stays jerry_can so what is saved still loads.
            [JerryCan] = new ItemDefinition(JerryCan, "Gas Can", "tool", EquipSlots.Primary, 1),
            // The bow and the crossbow (2026-10-28, docs/ARCHERY.md): drawn and spanned, with stand-in models drawn in code.
            [Bow] = new ItemDefinition(Bow, "Bow", "weapon", EquipSlots.Primary, 1),
            [Crossbow] = new ItemDefinition(Crossbow, "Crossbow", "weapon", EquipSlots.Primary, 1),
            // The slingshot (2026-10-30, docs/SLINGSHOT.md): the starter ranged weapon, drawn as the bow is, quicker, flinging pebbles.
            [Slingshot] = new ItemDefinition(Slingshot, "Slingshot", "weapon", EquipSlots.Primary, 1),
            [WoodenBoomerang] = new ItemDefinition(WoodenBoomerang, "Wooden Boomerang", "weapon", EquipSlots.Primary, 1),
            [GiantBoomerang] = new ItemDefinition(GiantBoomerang, "Giant Boomerang", "weapon", EquipSlots.Primary, 1),
            [IronSpikedGiantBoomerang] = new ItemDefinition(IronSpikedGiantBoomerang, "Iron Spiked Giant Boomerang", "weapon", EquipSlots.Primary, 1),
            [FireballStaff] = new ItemDefinition(FireballStaff, "Fireball Staff", "weapon", EquipSlots.Primary, 1),
            [IceShardStaff] = new ItemDefinition(IceShardStaff, "Ice Shard Staff", "weapon", EquipSlots.Primary, 1),
            // The arcane beam and the healing beam (2026-10-29, docs/STAFFS.md, docs/HEALING.md): held, a beam; Mercy's staff without its boost.
            [ArcaneBeamStaff] = new ItemDefinition(ArcaneBeamStaff, "Arcane Beam Staff", "weapon", EquipSlots.Primary, 1),
            [HealingStaff] = new ItemDefinition(HealingStaff, "Staff of Healing", "weapon", EquipSlots.Primary, 1),
            // The flashlight (2026-10-29, docs/FLASHLIGHT.md): held in the right hand, F or a click switches it, it points where you look.
            [Flashlight] = new ItemDefinition(Flashlight, "Flashlight", "tool", EquipSlots.Primary, 1),
            // The Frost Staff (2026-10-29, docs/FROST.md): Mei's blaster as a staff, held to spray frost that slows and then freezes.
            [FrostStaff] = new ItemDefinition(FrostStaff, "Frost Staff", "weapon", EquipSlots.Primary, 1),
            // The Magic Wand and the Magic Staff (2026-10-30, docs/MAGIC_WAND.md): no element; a speck of dust on the click, and the wand sways
            // every spell cast through it.
            [MagicWand] = new ItemDefinition(MagicWand, "Magic Wand", "weapon", EquipSlots.Primary, 1),
            [MagicStaff] = new ItemDefinition(MagicStaff, "Magic Staff", "weapon", EquipSlots.Primary, 1),
            // The Wraith Staff (2026-11-08, docs/WRAITH_STAFF.md): a wild Wraith's rare drop; it summons one Wraith that fights on your side.
            [WraithStaff] = new ItemDefinition(WraithStaff, "Wraith Staff", "weapon", EquipSlots.Primary, 1),
            // The frag grenade (2026-10-29, docs/GRENADE.md): thrown with the bottle's gesture, a stack of them, the next one into the hand.
            [Grenade] = new ItemDefinition(Grenade, "Frag Grenade", "weapon", EquipSlots.Primary, GrenadeStack),
            // Semtex (2026-11-11, docs/GRENADE.md): the grenade that sticks to whatever it first touches, a body included.
            [Semtex] = new ItemDefinition(Semtex, "Semtex", "weapon", EquipSlots.Primary, GrenadeStack),
            // Ammunition (2026-11-21): rounds in the bag, loaded by R (AmmoBox on the ground, AmmoBuilder).
            [PistolAmmo] = new ItemDefinition(PistolAmmo, "Pistol Ammo", AmmoCategory, string.Empty, PistolAmmoStack),
            [RifleAmmo] = new ItemDefinition(RifleAmmo, "Rifle Ammo", AmmoCategory, string.Empty, RifleAmmoStack),
            [ShotgunShells] = new ItemDefinition(ShotgunShells, "Shotgun Shells", AmmoCategory, string.Empty, ShotgunShellStack),
            [RpgRockets] = new ItemDefinition(RpgRockets, "RPG Rockets", AmmoCategory, string.Empty, RpgRocketStack),
        };

        public static bool TryGet(string itemId, out ItemDefinition definition)
        {
            return Items.TryGetValue(itemId, out definition);
        }

        public static bool Has(string itemId) => Items.ContainsKey(itemId);

        /// <summary>A blade: its killing blows cut a part off rather than bursting it (docs/DISMEMBERMENT.md).</summary>
        public static bool HasEdge(string itemId) => itemId == CombatSword || itemId == SpikeSword || itemId == KitchenKnife;

        /// <summary>A gun held in one hand, which Dual Pistols pairs with another in the other hand (docs/DUAL_PISTOLS.md).</summary>
        public static bool IsHandgun(string itemId) => itemId == Pistol;

        /// <summary>A coat: worn over a shirt, which goes under it in the top slot (Character.TopLayers).</summary>
        public static bool IsCoat(string itemId) =>
            itemId == MidnightCoat || itemId == GreenFurHoodedParka || itemId == MaroonFurHoodedParka;

        public static IReadOnlyCollection<string> Ids => Items.Keys;
    }
}
