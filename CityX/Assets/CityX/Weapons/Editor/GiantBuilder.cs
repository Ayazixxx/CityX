using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the giant (docs/GIANT.md): the player's own body at the Grow spell's size (the prefab's root scale, so its capsule, ragdoll and
    /// all scale with it and its movement reads it), in a weathered stone-grey skin so it is no player, with its mind (GiantBrain).
    /// </summary>
    public static class GiantBuilder
    {
        public const string PrefabPath = "Assets/CityX/Character/Giant.prefab";

        /// <summary>Weathered stone grey: a giant from the old city, not a player grown.</summary>
        public static readonly Color Stone = new Color(0.46f, 0.44f, 0.41f);

        [MenuItem("CityX/Rebuild Giant")]
        public static GameObject Build()
        {
            GameObject root = EnemyBody.FromPlayer("Giant");
            if (root == null)
            {
                return null;
            }
            try
            {
                EnemyBody.Reskin(root, CharacterSkin.Ensure("GiantSkin", Stone));
                root.transform.localScale = Vector3.one * GiantRules.Scale;
                // The controller's skin is world metres (only its height and radius scale with the body): kept in proportion, as Grow keeps it.
                var controller = root.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.skinWidth *= GiantRules.Scale;
                }
                root.AddComponent<GiantBrain>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the giant written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
