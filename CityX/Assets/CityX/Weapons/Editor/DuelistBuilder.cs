using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Core;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// A blade-fighting enemy (SwordsmanBrain) wearing one of the owner's characters: the player's own body (<see cref="EnemyBody"/>), the character's
    /// paint, hair and clothes, the blade to equip at spawn and the style it fights in. The character's save is copied into the project
    /// (Character/NpcLooks/*.json) so the enemy never depends on one machine's saves. SwordsmanBuilder (the Knight) and CrazyGuyBuilder (guycrazy).
    /// </summary>
    public static class DuelistBuilder
    {
        public sealed class Recipe
        {
            public string Name;
            public string PrefabPath;
            public string LookPath;
            public string BladePrefabPath;

            /// <summary>Worn on top of what the character wears (the swordsman's Knight Helmet).</summary>
            public string[] ExtraWearingPaths = new string[0];

            public DuelistStyleKind Style;

            /// <summary>The skin if the look is missing or has no paint.</summary>
            public Color FallbackSkin;

            public int XpReward = 80;

            /// <summary>The painted skin's name, when several enemies share one look (the hobos); empty for Name + "Skin".</summary>
            public string SkinName = "";

            /// <summary>Which item the blade is (dropped when he falls); empty to read it off a sword.</summary>
            public string BladeItemId = "";

            /// <summary>The blade's state when he is spawned (a broken glass bottle: 1); below 0 as it comes.</summary>
            public int BladeDurability = -1;

            /// <summary>Takes no one on until he is hurt (a hobo).</summary>
            public bool PeacefulUntilHurt;

            /// <summary>The share spawned with the weapon in hand; the rest take it out when the fight starts (SwordsmanBrain.CalmArmedShare).</summary>
            public float CalmArmedShare = 1.0f;

            /// <summary>A hooded coat in the look is worn with its hood up.</summary>
            public bool HoodUp;

            /// <summary>What he carries besides (a hobo's three spare bottles, which he throws): lootable from his body.</summary>
            public string[] Carried = new string[0];
        }

        public static GameObject Build(Recipe recipe)
        {
            var blade = AssetDatabase.LoadAssetAtPath<GameObject>(recipe.BladePrefabPath);
            if (blade == null)
            {
                Debug.LogError($"CityX: build {recipe.BladePrefabPath} before {recipe.Name}.");
                return null;
            }
            GameObject root = EnemyBody.FromPlayer(recipe.Name);
            if (root == null)
            {
                return null;
            }
            try
            {
                CharacterSave look = Look(recipe.LookPath);
                string skinName = !string.IsNullOrEmpty(recipe.SkinName) ? recipe.SkinName : recipe.Name + "Skin";
                Material skin = look != null ? PaintedSkin(look, skinName) : null;
                if (skin == null)
                {
                    skin = CharacterSkin.Ensure(skinName, recipe.FallbackSkin);
                }
                EnemyBody.Reskin(root, skin);   // (the hair goes on in the scene builder, which alone reaches HairAssets: WearTheLooksHair)

                // A bullet his guard turns aside flies off at the players, as a player's does at the enemies (BulletRicochet).
                root.AddComponent<BulletRicochet>();
                var brain = root.AddComponent<SwordsmanBrain>();
                brain.SwordPrefab = blade;
                brain.Style = recipe.Style;
                brain.XpReward = recipe.XpReward;
                brain.Wearing = Wearing(look, recipe.ExtraWearingPaths);
                brain.BladeItemId = recipe.BladeItemId;
                brain.BladeDurability = recipe.BladeDurability;
                brain.PeacefulUntilHurt = recipe.PeacefulUntilHurt;
                brain.CalmArmedShare = recipe.CalmArmedShare;
                brain.HoodUp = recipe.HoodUp;
                if (recipe.Carried.Length > 0)
                {
                    root.AddComponent<CarriedLoot>().Items = new List<string>(recipe.Carried);
                }
                if (blade.GetComponent<BottleWeapon>() != null)
                {
                    PlayerAbilityWiring.AddBottleThrower(root);   // a bottle's moves, throws and breaking, here and on every other screen
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, recipe.PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: {recipe.Name} written to {recipe.PrefabPath} ({brain.Wearing.Length} pieces worn)");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>An owner's character copied out of their save, or null (with a warning) when it is not in the project.</summary>
        public static CharacterSave Look(string path)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (json == null)
            {
                Debug.LogWarning($"CityX: no {path} -- the enemy keeps a plain skin.");
                return null;
            }
            return JsonUtility.FromJson<CharacterSave>(json.text);
        }

        /// <summary>The character's clothes as item prefabs (each as an Inventory would show it), then the extras.</summary>
        internal static GameObject[] Wearing(CharacterSave look, string[] extraPaths)
        {
            var worn = new List<GameObject>();
            if (look?.Wearing != null)
            {
                Inventory.ItemPrefabEntry[] clothes = NetworkedPlayerWiring.ItemPrefabEntries(definition => definition.IsWearable);
                foreach (string id in look.Wearing)
                {
                    GameObject prefab = System.Array.Find(clothes, entry => entry.ItemId == id).Prefab;
                    if (prefab != null)
                    {
                        worn.Add(prefab);
                    }
                    else
                    {
                        Debug.LogWarning($"CityX: the look wears '{id}', which has no prefab: left off.");
                    }
                }
            }
            foreach (string path in extraPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    worn.Add(prefab);
                }
            }
            return worn.ToArray();
        }

        /// <summary>
        /// The character's paint on the body, as CharacterIdentity.Apply puts it on the player, baked into a material asset. Compressed (DXT1,
        /// an eighth of the raw size): he is only ever worn, never painted. Null when the character has no paint.
        /// </summary>
        internal static Material PaintedSkin(CharacterSave look, string materialName)
        {
            byte[] painted = look.GetPaint();
            var paint = new BodyPaint(look.Skin);
            if (painted == null || !paint.Decode(painted))
            {
                return null;
            }
            paint.Apply();
            EditorUtility.CompressTexture(paint.Atlas, TextureFormat.DXT1, TextureCompressionQuality.Best);
            return CharacterSkin.EnsurePainted(materialName, paint.Atlas);
        }
    }
}
