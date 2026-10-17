using CityX.Character;
using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds Swordsman.prefab (owner, 2026-10-17: "make a swordsman npc that wields the one handed sword so i can duel with him", "make the npc
    /// have all the movement animations that i do"): the player's own body (Player.prefab, so his animation, his sword combat and every change to
    /// them are the player's), in a deep slate blue, without what only the player's own body needs -- the input reader (his SwordsmanBrain drives
    /// the body instead), the saved character's look, the voice capture -- and with the combat sword to equip at spawn. Run after the player rig
    /// and the combat sword. The way ShootaBuilder makes the Shoota.
    /// </summary>
    public static class SwordsmanBuilder
    {
        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const string SwordPrefabPath = "Assets/CityX/Weapons/CombatSword.prefab";
        public const string PrefabPath = "Assets/CityX/Character/Swordsman.prefab";

        /// <summary>His skin: a deep slate blue, so he stands out against the grey city and from the white Shoota.</summary>
        public static readonly Color Slate = new Color(0.24f, 0.30f, 0.46f);

        [MenuItem("CityX/Rebuild Swordsman")]
        public static GameObject Build()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var sword = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (player == null || sword == null)
            {
                Debug.LogError("CityX: build the player rig and the combat sword before the swordsman.");
                return null;
            }

            var root = (GameObject)Object.Instantiate(player);
            root.name = "Swordsman";
            try
            {
                // Only the player's own body reads the keyboard, wears the saved character and listens to the microphone.
                Remove<PlayerInputReader>(root);
                Remove<CharacterIdentity>(root);
                Remove<PlayerVoiceCapture>(root);

                Material skin = CharacterSkin.Ensure("SwordsmanSkin", Slate);
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

                // THE ANIMATION CONTROLLER BY PATH, set on the copy before it is saved: the player rig is rebuilt earlier in the same run (a new
                // controller asset), and the reference carried over from Player.prefab came out empty -- the body stood in its bind pose with
                // nothing playing (the Shoota had been like that since; found 2026-10-17 building the swordsman).
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                if (controller == null)
                {
                    Debug.LogError("CityX: no player animator controller -- rebuild the player rig first.");
                    return null;
                }
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    animator.runtimeAnimatorController = controller;
                }

                var brain = root.AddComponent<SwordsmanBrain>();
                brain.SwordPrefab = sword;

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

        private static void Remove<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
