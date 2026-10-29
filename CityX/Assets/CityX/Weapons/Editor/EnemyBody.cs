using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// What every enemy built on the player's own body needs (the swordsman, the Shoota, the giant): a copy of Player.prefab without what only
    /// the player's body does (reading the keyboard, wearing the saved character, listening to the microphone: the enemy's brain drives the
    /// body instead), its animator given the player's controller BY PATH -- the player rig is rebuilt earlier in the same run (a new controller
    /// asset), and the reference carried over from Player.prefab came out empty: the body stood in its bind pose with nothing playing (found
    /// 2026-10-17 building the swordsman).
    /// </summary>
    public static class EnemyBody
    {
        public const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";
        public const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";

        /// <summary>A copy of the player's body named <paramref name="name"/>, stripped and animated; null (and an error) without the player's rig.</summary>
        public static GameObject FromPlayer(string name)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (player == null || controller == null)
            {
                Debug.LogError($"CityX: build the player rig before the {name}.");
                return null;
            }
            var root = (GameObject)Object.Instantiate(player);
            root.name = name;
            Remove<PlayerInputReader>(root);
            Remove<CharacterIdentity>(root);
            Remove<PlayerVoiceCapture>(root);
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = controller;
            }
            return root;
        }

        private static void Remove<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }

        /// <summary>Every skin material on <paramref name="root"/> swapped for <paramref name="skin"/>.</summary>
        public static void Reskin(GameObject root, Material skin)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name.Contains("Skin"))
                    {
                        materials[i] = skin;
                        changed = true;
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }
    }
}
