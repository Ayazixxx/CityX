using CityX.Character;
using CityX.CharacterEditor;
using CityX.Core;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds Swordsman.prefab (owner, 2026-10-17: "make a swordsman npc that wields the one handed sword so i can duel with him", "make the npc
    /// have all the movement animations that i do"): the player's own body (Player.prefab, so his animation, his sword combat and every change to
    /// them are the player's), without what only the player's own body needs (<see cref="EnemyBody"/>: his SwordsmanBrain drives the body
    /// instead of the input reader), and with the combat sword to equip at spawn. He wears the owner's character "Knight" (2026-10-18: "make
    /// the character i made called Knight the skin for the swordsman npc"; its save copied to NpcLooks/Knight.json) and the Knight Helmet. Run
    /// after the player rig, the combat sword and the helmet. The way ShootaBuilder makes the Shoota.
    /// </summary>
    public static class SwordsmanBuilder
    {
        private const string SwordPrefabPath = "Assets/CityX/Weapons/CombatSword.prefab";
        public const string PrefabPath = "Assets/CityX/Character/Swordsman.prefab";
        private const string HelmetPrefabPath = "Assets/CityX/Wearables/KnightHelmet.prefab";

        /// <summary>His look (owner, 2026-10-18: "make the character i made called Knight the skin for the swordsman npc").</summary>
        public const string LookPath = "Assets/CityX/Character/NpcLooks/Knight.json";

        /// <summary>His skin if the Knight look is missing: a deep slate blue, so he stands out against the grey city and from the white Shoota.</summary>
        public static readonly Color Slate = new Color(0.24f, 0.30f, 0.46f);

        [MenuItem("CityX/Rebuild Swordsman")]
        public static GameObject Build()
        {
            var sword = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (sword == null)
            {
                Debug.LogError("CityX: build the combat sword before the swordsman.");
                return null;
            }
            GameObject root = EnemyBody.FromPlayer("Swordsman");
            if (root == null)
            {
                return null;
            }
            try
            {
                CharacterSave look = KnightLook();
                Material skin = look != null ? PaintedSkin(look) : null;
                if (skin == null)
                {
                    skin = CharacterSkin.Ensure("SwordsmanSkin", Slate);
                }
                var hair = root.GetComponentInChildren<CharacterHair>(true);
                if (hair != null && look != null)
                {
                    hair.SetStyle(look.HairStyle);
                    hair.SetColour(look.HairColour);
                }
                EnemyBody.Reskin(root, skin);

                // A bullet his guard turns aside flies off at the players, as a player's does at the enemies (BulletRicochet).
                root.AddComponent<BulletRicochet>();
                var brain = root.AddComponent<SwordsmanBrain>();
                brain.SwordPrefab = sword;
                brain.HelmetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HelmetPrefabPath);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the swordsman written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The owner's character "Knight", copied out of their save into the project so the swordsman never depends on one machine's saves.</summary>
        private static CharacterSave KnightLook()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LookPath);
            if (json == null)
            {
                Debug.LogWarning($"CityX: no {LookPath} -- the swordsman keeps his plain slate skin.");
                return null;
            }
            return JsonUtility.FromJson<CharacterSave>(json.text);
        }

        /// <summary>
        /// The character's paint on the body, as CharacterIdentity.Apply puts it on the player, baked into a material asset. Compressed (DXT1,
        /// an eighth of the raw size): he is only ever worn, never painted. Null when the character has no paint.
        /// </summary>
        private static Material PaintedSkin(CharacterSave look)
        {
            byte[] painted = look.GetPaint();
            var paint = new BodyPaint(look.Skin);
            if (painted == null || !paint.Decode(painted))
            {
                return null;
            }
            paint.Apply();
            EditorUtility.CompressTexture(paint.Atlas, TextureFormat.DXT1, TextureCompressionQuality.Best);
            return CharacterSkin.EnsurePainted("SwordsmanSkin", paint.Atlas);
        }
    }
}
