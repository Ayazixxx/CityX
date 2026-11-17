using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds Swordsman.prefab (owner, 2026-10-17: "make a swordsman npc that wields the one handed sword so i can duel with him", "make the npc
    /// have all the movement animations that i do"): the player's own body (Player.prefab, so his animation, his sword combat and every change to
    /// them are the player's), without what only the player's own body needs (EnemyBody: his SwordsmanBrain drives the body instead of the input
    /// reader), and with the combat sword to equip at spawn. He wears the owner's character "Knight" (2026-10-18: "make the character i made called
    /// Knight the skin for the swordsman npc"; its save copied to NpcLooks/Knight.json) and the Knight Helmet. Run after the player rig, the combat
    /// sword and the helmet. DuelistBuilder does the building, as it does Crazy Guy's.
    /// </summary>
    public static class SwordsmanBuilder
    {
        public const string PrefabPath = "Assets/CityX/Character/Swordsman.prefab";

        /// <summary>His look (owner, 2026-10-18: "make the character i made called Knight the skin for the swordsman npc").</summary>
        public const string LookPath = "Assets/CityX/Character/NpcLooks/Knight.json";

        /// <summary>His skin if the Knight look is missing: a deep slate blue, so he stands out against the grey city and from the white Shoota.</summary>
        public static readonly Color Slate = new Color(0.24f, 0.30f, 0.46f);

        [MenuItem("CityX/Rebuild Swordsman")]
        public static GameObject Build()
        {
            return DuelistBuilder.Build(new DuelistBuilder.Recipe
            {
                Name = "Swordsman",
                PrefabPath = PrefabPath,
                LookPath = LookPath,
                BladePrefabPath = "Assets/CityX/Weapons/CombatSword.prefab",
                ExtraWearingPaths = new[] { "Assets/CityX/Wearables/KnightHelmet.prefab" },
                Style = DuelistStyleKind.Swordsman,
                FallbackSkin = Slate,
            });
        }
    }
}
