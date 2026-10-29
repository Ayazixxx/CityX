using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds Shoota.prefab (owner, 2026-10-15: "he is just a white playermodel and he wields the ak47"): the player's own body (Player.prefab,
    /// so a change to the body or its animation reaches him with a rebuild), in plain white, without what only the player's own body needs
    /// (<see cref="EnemyBody"/>: his ShootaBrain drives the body instead of the input reader), and with the AK-47 to mount in his hands. Run
    /// after the player rig and the AK-47.
    /// </summary>
    public static class ShootaBuilder
    {
        private const string GunPrefabPath = "Assets/CityX/Weapons/AK47.prefab";
        public const string PrefabPath = "Assets/CityX/Character/Shoota.prefab";

        /// <summary>His skin: white, a little warm so it is not a glare in the sun.</summary>
        public static readonly Color White = new Color(0.93f, 0.93f, 0.91f);

        [MenuItem("CityX/Rebuild Shoota")]
        public static GameObject Build()
        {
            var gun = AssetDatabase.LoadAssetAtPath<GameObject>(GunPrefabPath);
            if (gun == null)
            {
                Debug.LogError("CityX: build the AK-47 before the Shoota.");
                return null;
            }
            GameObject root = EnemyBody.FromPlayer("Shoota");
            if (root == null)
            {
                return null;
            }
            try
            {
                EnemyBody.Reskin(root, CharacterSkin.Ensure("ShootaSkin", White));
                var brain = root.AddComponent<ShootaBrain>();
                brain.GunPrefab = gun;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the Shoota written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
