using CityX.Character;
using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds Shoota.prefab (owner, 2026-10-15: "he is just a white playermodel and he wields the ak47"): the player's own body (Player.prefab,
    /// so a change to the body or its animation reaches him with a rebuild), in plain white, without what only the player's own body needs --
    /// the input reader (his ShootaBrain drives the body instead), the saved character's look, the voice capture -- and with the AK-47 to mount
    /// in his hands. Run after the player rig and the AK-47.
    /// </summary>
    public static class ShootaBuilder
    {
        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";
        private const string GunPrefabPath = "Assets/CityX/Weapons/AK47.prefab";
        public const string PrefabPath = "Assets/CityX/Character/Shoota.prefab";

        /// <summary>His skin: white, a little warm so it is not a glare in the sun.</summary>
        public static readonly Color White = new Color(0.93f, 0.93f, 0.91f);

        [MenuItem("CityX/Rebuild Shoota")]
        public static GameObject Build()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var gun = AssetDatabase.LoadAssetAtPath<GameObject>(GunPrefabPath);
            if (player == null || gun == null)
            {
                Debug.LogError("CityX: build the player rig and the AK-47 before the Shoota.");
                return null;
            }

            var root = (GameObject)Object.Instantiate(player);
            root.name = "Shoota";
            try
            {
                // Only the player's own body reads the keyboard, wears the saved character and listens to the microphone.
                Remove<PlayerInputReader>(root);
                Remove<CharacterIdentity>(root);
                Remove<PlayerVoiceCapture>(root);

                Material skin = CharacterSkin.Ensure("ShootaSkin", White);
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

        private static void Remove<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
