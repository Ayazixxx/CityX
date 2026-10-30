using System;
using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.Items;
using FishNet.Component.Transforming;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// THE GUARD THAT KEEPS EVERY FUTURE HELD ITEM VISIBLE TO OTHER PLAYERS, the same standing rule as for abilities: a weapon or tool is not finished
    /// until other players can see it in the holder's hand (or on their back). A new item that is not wearable clothing must implement
    /// <see cref="ICosmeticMountable"/> (see NetworkedEquipmentSync), and its prefab must be in the networked player's inventory list
    /// (NetworkedPlayerWiring). Also guards the networked body's wiring: damage relay, equipment sync, hair, smooth movement.
    /// </summary>
    public sealed class EquipmentReplicationGuardTests
    {
        private const string NetworkedPlayerPath = "Assets/CityX/Character/NetworkedPlayer.prefab";

        private static IEnumerable<Type> HeldItemTypes()
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (!name.StartsWith("CityX.") || name.Contains("Tests") || name.Contains("Editor") || name.Contains("Spike"))
                {
                    continue;
                }
                Type[] types;
                try { types = assembly.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (Type type in types)
                {
                    if (type.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(type) || !typeof(IEquippableItem).IsAssignableFrom(type))
                    {
                        continue;
                    }
                    yield return type;   // clothes and hats too, since 2026-10-22
                }
            }
        }

        [Test]
        public void EveryHeldItemCanBeShownOnAnotherPlayersCopy()
        {
            var missing = HeldItemTypes().Where(t => !typeof(ICosmeticMountable).IsAssignableFrom(t)).Select(t => t.Name).ToList();
            Assert.IsEmpty(missing,
                "these held items do not implement ICosmeticMountable, so other players would see an empty hand: " + string.Join(", ", missing)
                + ". Implement MountCosmetic/UnmountCosmetic/SetCosmeticActive (see SwordWeapon) and add the prefab to NetworkedPlayerWiring.");
        }

#if UNITY_EDITOR
        /// <summary>
        /// A joining player's body can wear every hairstyle there is: the appearance RPCs send the style's NAME, and a body built
        /// before a style was added has no mesh for it, so other players would see that player bald (HYDE, 2026-10-12).
        /// </summary>
        [Test]
        public void TheNetworkedBodyCanWearEveryHairstyle()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPath);
            Assert.IsNotNull(prefab, "no NetworkedPlayer.prefab");
            var hair = prefab.GetComponentInChildren<CharacterHair>(true);
            Assert.IsNotNull(hair, "the networked body has no hair, so no hairstyle can show on it");
            Assert.AreEqual(CharacterHair.ValidStyles.Length - 1, hair.StyleMeshes.Length,
                "the networked body was built with a different list of hairstyles -- rebuild the scene (CityMassingSceneBuilder)");
            for (int i = 0; i < hair.StyleMeshes.Length; i++)
            {
                Assert.IsNotNull(hair.StyleMeshes[i], $"other players would see \"{CharacterHair.ValidStyles[i + 1]}\" as bald");
            }
        }

        /// <summary>
        /// The game's own player can equip everything a networked body can show: the scene's inventory list is written by its own builder
        /// (CityMassingSceneBuilder), and the gas can was left out of it, so /equip found nothing (owner, 2026-10-29: "the jerry can wasnt
        /// letting me equip").
        /// </summary>
        [Test]
        public void TheGamesPlayerCanEquipEverythingANetworkedBodyCan()
        {
            var networked = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPath).GetComponent<Inventory>();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/CityX/Scenes/CityMassing.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                Inventory player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Inventory>(true))
                    .FirstOrDefault(i => i.GetComponent<PlayerInputReader>() != null);
                Assert.IsNotNull(player, "the scene's player has no inventory");
                var equippable = new HashSet<string>((player.ItemPrefabs ?? new Inventory.ItemPrefabEntry[0])
                    .Where(e => e.Prefab != null).Select(e => e.ItemId));
                var missing = (networked.ItemPrefabs ?? new Inventory.ItemPrefabEntry[0])
                    .Select(e => e.ItemId).Where(id => !equippable.Contains(id)).Distinct().ToList();
                Assert.IsEmpty(missing, "the game's player cannot equip these (add them to CityMassingSceneBuilder's list): " + string.Join(", ", missing));
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheNetworkedBodyCarriesTheSharedState()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPath);
            Assert.IsNotNull(prefab, "no NetworkedPlayer.prefab");
            Assert.IsNotNull(prefab.GetComponent<NetworkedEquipmentSync>(), "the networked body does not share what it holds");
            Assert.IsNotNull(prefab.GetComponent<NetworkedDamage>(), "the networked body cannot take damage from other players");
            Assert.IsNotNull(prefab.GetComponentInChildren<CharacterHair>(true), "the networked body has no hair, so no hairstyle can show on it");

            var inventory = prefab.GetComponent<Inventory>();
            Assert.IsNotNull(inventory, "the networked body has no inventory: it cannot hold anything or build models of what others hold");
            foreach (Type type in HeldItemTypes())
            {
                bool found = false;
                foreach (Inventory.ItemPrefabEntry entry in inventory.ItemPrefabs ?? new Inventory.ItemPrefabEntry[0])
                {
                    if (entry.Prefab != null && entry.Prefab.GetComponent(type) != null)
                    {
                        found = true;
                    }
                }
                Assert.IsTrue(found, $"no prefab for {type.Name} in the networked body's inventory list (NetworkedPlayerWiring.ItemPrefabPaths)");
            }
        }

        [Test]
        public void TheNetworkedBodyMovesSmoothly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPath);
            var so = new SerializedObject(prefab.GetComponent<NetworkTransform>());
            Assert.GreaterOrEqual(so.FindProperty("_interpolation").intValue, 3, "movement is interpolated over too few ticks: it will step");
            Assert.AreEqual(0, so.FindProperty("_packing.Position").enumValueIndex, "position is quantised (stepped)");
            Assert.AreEqual(0, so.FindProperty("_packing.Rotation").enumValueIndex, "rotation is quantised (stepped)");
        }
#endif
    }
}
