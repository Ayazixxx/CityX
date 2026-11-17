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
                Material skin = look != null ? PaintedSkin(look, recipe.Name + "Skin") : null;
                if (skin == null)
                {
                    skin = CharacterSkin.Ensure(recipe.Name + "Skin", recipe.FallbackSkin);
                }
                EnemyBody.Reskin(root, skin);   // (the hair goes on in the scene builder, which alone reaches HairAssets: WearTheLooksHair)

                // A bullet his guard turns aside flies off at the players, as a player's does at the enemies (BulletRicochet).
                root.AddComponent<BulletRicochet>();
                var brain = root.AddComponent<SwordsmanBrain>();
                brain.SwordPrefab = blade;
                brain.Style = recipe.Style;
                brain.XpReward = recipe.XpReward;
                brain.Wearing = Wearing(look, recipe.ExtraWearingPaths);

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
        private static GameObject[] Wearing(CharacterSave look, string[] extraPaths)
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
        private static Material PaintedSkin(CharacterSave look, string materialName)
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
