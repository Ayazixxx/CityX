using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds CrazyGuy.prefab (owner, 2026-11-16: "make a hostile npcs called "Crazy Guy" and use my character named "guycrazy" for the playermodel.
    /// make him wield a kitchen knife and utilize its advantages"): the swordsman's make (DuelistBuilder: the player's own body and the blade combat)
    /// wearing the owner's character "guycrazy" (its paint, kick hair, jeans and sneakers; its save copied to NpcLooks/GuyCrazy.json), with the kitchen
    /// knife, fighting the knife's way (DuelistStyle.Knife). /spawn crazyguy. Run after the player rig, the kitchen knife and the clothes.
    /// </summary>
    public static class CrazyGuyBuilder
    {
        public const string PrefabPath = "Assets/CityX/Character/CrazyGuy.prefab";

        public const string LookPath = "Assets/CityX/Character/NpcLooks/GuyCrazy.json";

        /// <summary>His skin if the guycrazy look is missing: a sickly yellow.</summary>
        public static readonly Color Sallow = new Color(0.78f, 0.72f, 0.42f);

        [MenuItem("CityX/Rebuild Crazy Guy")]
        public static GameObject Build()
        {
            return DuelistBuilder.Build(new DuelistBuilder.Recipe
            {
                Name = "CrazyGuy",
                PrefabPath = PrefabPath,
                LookPath = LookPath,
                BladePrefabPath = "Assets/CityX/Weapons/KitchenKnife.prefab",
                Style = DuelistStyleKind.Knife,
                FallbackSkin = Sallow,
            });
        }
    }
}
