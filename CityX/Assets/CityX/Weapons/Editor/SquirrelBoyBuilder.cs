using CityX.Character;
using CityX.CharacterEditor;
using CityX.Core;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds SquirrelBoy.prefab (owner, 2026-11-25: "make an npc called squirrel boy that uses my squirrel boy character i made and the outfit he
    /// is wearing now. the sb shirt i designed, the squirrel mask, the brown cape and gloves, and khakis with bwsneakers"): the player's own body
    /// (EnemyBody) in the owner's character "squirrel boy" (his save copied to NpcLooks/SquirrelBoy.json: his skin and paint, no hair, his
    /// outfit), the SB design his shirt is printed with copied beside it (NpcLooks/SquirrelBoyShirt.bytes, as the heat press keeps it), and
    /// SquirrelBoyBrain. /spawn squirrelboy. Run after the player rig and the clothes.
    /// </summary>
    public static class SquirrelBoyBuilder
    {
        public const string PrefabPath = "Assets/CityX/Character/SquirrelBoy.prefab";

        public const string LookPath = "Assets/CityX/Character/NpcLooks/SquirrelBoy.json";

        public const string ShirtDesignPath = "Assets/CityX/Character/NpcLooks/SquirrelBoyShirt.bytes";

        /// <summary>His skin if the look is missing: the look's own.</summary>
        public static readonly Color FallbackSkin = new Color(0.78f, 0.62f, 0.53f);

        [MenuItem("CityX/Rebuild Squirrel Boy")]
        public static GameObject Build()
        {
            GameObject root = EnemyBody.FromPlayer("SquirrelBoy");
            if (root == null)
            {
                return null;
            }
            try
            {
                CharacterSave look = DuelistBuilder.Look(LookPath);
                Material skin = look != null ? DuelistBuilder.PaintedSkin(look, "SquirrelBoySkin") : null;
                EnemyBody.Reskin(root, skin != null ? skin : CharacterSkin.Ensure("SquirrelBoySkin", FallbackSkin));
                root.AddComponent<ClawTrail>();
                var claws = root.AddComponent<ClawSwipe>();   // his claws (owner, 2026-11-25)
                claws.SlashMaterial = ClawSlashMaterialBuilder.Ensure();
                claws.SparkMaterial = ClawSlashMaterialBuilder.EnsureSpark();
                var brain = root.AddComponent<SquirrelBoyBrain>();
                brain.Wearing = DuelistBuilder.Wearing(look, new string[0]);
                brain.ShirtDesign = AssetDatabase.LoadAssetAtPath<TextAsset>(ShirtDesignPath);
                if (brain.ShirtDesign == null)
                {
                    Debug.LogWarning($"CityX: no {ShirtDesignPath} -- Squirrel Boy's shirt is plain.");
                }
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: Squirrel Boy written to {PrefabPath} ({brain.Wearing.Length} pieces worn)");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
