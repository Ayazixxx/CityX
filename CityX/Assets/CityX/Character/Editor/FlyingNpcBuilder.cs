using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the flying NPC prefab -- a normal white player body wearing a
    /// real, permanently-equipped jetpack, driven by FlyingNpc rather than a
    /// player.
    ///
    /// Shares the player's body and animator wholesale, the same way
    /// CivilianBuilder does. No CharacterController and no Ragdoll, unlike
    /// Civilian -- this NPC only ever flies and was never asked to be
    /// punchable or knocked down, so both would be scope nobody asked for.
    ///
    /// Run after the player rig (for the shared body/animator) and after the
    /// jetpack (whose already-built prefab is handed in rather than rebuilt
    /// here, the exact same "built once, handed to whoever needs it" idiom
    /// CityMassingSceneBuilder already uses for every other shared prefab).
    /// </summary>
    public static class FlyingNpcBuilder
    {
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const string JetpackPrefabPath = "Assets/CityX/Weapons/Jetpack.prefab";
        private const string PrefabPath = "Assets/CityX/Character/FlyingNpc.prefab";

        [MenuItem("CityX/Rebuild Flying NPC")]
        public static GameObject Build()
        {
            GameObject jetpackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(JetpackPrefabPath);
            return Build(jetpackPrefab);
        }

        public static GameObject Build(GameObject jetpackPrefab)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Player Rig first -- the flying NPC shares its animator controller.");
                return null;
            }
            if (jetpackPrefab == null)
            {
                Debug.LogError("CityX: no jetpack prefab -- run CityX/Rebuild Jetpack first.");
                return null;
            }

            var root = new GameObject("FlyingNpc");
            try
            {
                // The same skeleton, avatar and fused mesh the player and
                // every civilian get, painted the project's own established
                // "plain white NPC" colour (CivilianColour's own doc comment
                // -- "a crowd reads as background" -- applies just as well
                // to a single background flier).
                Material skin = CharacterSkin.Ensure("FlyingNpcSkin", CharacterSkin.CivilianColour);
                GameObject body = FusedBodyBuilder.BuildBody(skin, controller);
                body.transform.SetParent(root.transform, false);

                // A real, permanently-equipped jetpack -- OnEquip finds the
                // Animator/Spine on this body itself and parents onto it,
                // exactly the same call the real player's own equip flow
                // makes.
                var jetpackInstance = (GameObject)PrefabUtility.InstantiatePrefab(jetpackPrefab);
                Jetpack jetpack = jetpackInstance.GetComponent<Jetpack>();
                jetpack.OnEquip(root, ItemInstanceData.None);

                root.AddComponent<FlyingNpc>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: flying NPC written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
