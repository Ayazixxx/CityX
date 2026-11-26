using CityX.Character;
using CityX.Items;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the hobos (docs/HOBO.md; owner, 2026-11-24: "make an enemy called hobo and it spawns as either the character i made named hobo2 ...
    /// and then the other type that can spawn is just hobo ... make it so they spawn with either a metal pole, broken glass bottle. or a chain,
    /// or a machete as their weapon"; "also make wood plank one of the weapons hobos can spawn with". "its random which one and which skin they get. make them not hostile until hit they jus walk around"): the
    /// swordsman's make (DuelistBuilder) in each of the owner's two hobo characters (their saves copied to NpcLooks/Hobo.json and Hobo2.json),
    /// once with each weapon (HoboKinds): fifteen prefabs, Character/Hobos/Hobo_1_Pole ... Hobo_3_Plank. With the broken bottle he also
    /// carries three whole empty ones, which he throws and which are found on his body. Run after the player rig, the weapons and the clothes.
    /// </summary>
    public static class HoboBuilder
    {
        public const string Folder = "Assets/CityX/Character/Hobos";

        /// <summary>Each look's save, in HoboKinds.Looks' order: the owner's "Hobo", "hobo2", then "brack guy".</summary>
        public static readonly string[] LookPaths =
        {
            "Assets/CityX/Character/NpcLooks/Hobo.json",
            "Assets/CityX/Character/NpcLooks/Hobo2.json",
            "Assets/CityX/Character/NpcLooks/BrackGuy.json",
        };

        /// <summary>Each look's hood, in the same order: brack guy's up (owner, 2026-11-24: "make him wearing a brown coat ... and put the hood on").</summary>
        public static readonly bool[] LookHoodUp = { false, false, true };

        /// <summary>The bottle: the plain glass one, broken (BottleWeapon.DurabilityState.Broken).</summary>
        public const string Bottle = GlassBottleKinds.Classic;

        /// <summary>How many whole empty bottles a hobo with a broken one carries (owner: "3 not broken but empty glass bottles").</summary>
        public const int SpareBottles = 3;

        /// <summary>Less than the swordsman's 80: a street fight, not a duelist (mine).</summary>
        public const int XpReward = 40;

        /// <summary>A skin if a look is missing: weathered.</summary>
        public static readonly Color Weathered = new Color(0.62f, 0.5f, 0.4f);

        public static string PrefabPath(int look, int weapon) => $"{Folder}/{HoboKinds.PrefabNameOf(look, weapon)}.prefab";

        /// <summary>Each weapon of HoboKinds.Weapons: its prefab and item.</summary>
        private static (string Prefab, string ItemId) WeaponOf(string weapon)
        {
            switch (weapon)
            {
                case "pole": return (BoStaffBuilder.PrefabPath, ItemRegistry.BoStaff);
                case "bottle": return (GlassBottleKinds.PrefabPath(Bottle), Bottle);
                case "chain": return (ChainBuilder.PrefabPath, ItemRegistry.Chain);
                case "plank": return (WoodPlankBuilder.PrefabPath, ItemRegistry.WoodPlank);
                default: return (MacheteBuilder.PrefabPath, ItemRegistry.Machete);
            }
        }

        [MenuItem("CityX/Rebuild Hobos")]
        public static GameObject[] BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Character", "Hobos");
            }
            var built = new GameObject[HoboKinds.All.Length];
            for (int look = 0; look < HoboKinds.Looks.Length; look++)
            {
                for (int weapon = 0; weapon < HoboKinds.Weapons.Length; weapon++)
                {
                    built[look * HoboKinds.Weapons.Length + weapon] = Build(look, weapon);
                }
            }
            return built;
        }

        public static GameObject Build(int look, int weapon)
        {
            (string prefab, string itemId) = WeaponOf(HoboKinds.Weapons[weapon]);
            bool bottle = itemId == Bottle;
            var carried = new string[SpareBottles];   // every hobo: 1 to 3 of them, rolled when he is spawned (owner, 2026-11-26)
            for (int i = 0; i < carried.Length; i++)
            {
                carried[i] = Bottle;
            }
            return DuelistBuilder.Build(new DuelistBuilder.Recipe
            {
                Name = HoboKinds.PrefabNameOf(look, weapon),
                PrefabPath = PrefabPath(look, weapon),
                LookPath = LookPaths[look],
                BladePrefabPath = prefab,
                BladeItemId = itemId,
                BladeDurability = bottle ? (int)CityX.Weapons.BottleWeapon.DurabilityState.Broken : -1,
                Style = DuelistStyleKind.Swordsman,
                FallbackSkin = Weathered,
                XpReward = XpReward,
                SkinName = $"Hobo{HoboKinds.Looks[look]}Skin",   // one painted skin a look, shared by its four
                PeacefulUntilHurt = true,
                CanDash = false,   // owner, 2026-11-26: "make it so hobos dont have dash"
                RollsHowManyBottles = true,
                CalmArmedShare = HoboKinds.CalmArmedShare,
                HoodUp = LookHoodUp[look],
                Carried = carried,
            });
        }
    }
}
